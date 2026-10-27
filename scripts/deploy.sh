#!/bin/sh
# Deploys a release of ITMas on the production server (SPEC-0009 RF-16 to RF-19, ADR-0020). Run by a person over SSH with sudo, from the
# repository clone (/opt/itmas3), which belongs to root like every host script and secret (constitution 8.7 c):
#   sudo scripts/deploy.sh <tag> [--yes]              deploy vMAJOR.MINOR.PATCH, published by the release workflow
#   sudo scripts/deploy.sh rollback [--yes] [--schema-changed-ok]   put back the previous release (does not run the migrator)
#   sudo scripts/deploy.sh status                     show the current and the previous release
# A deployment: the commit of the tag must be in origin/main (a moved tag is refused); the digests of the tag are resolved and shown
# and nothing is touched until you confirm; then the clone moves to the tag, the published ports are guarded, the database is
# dumped (when it exists), the images are pulled by digest, the migrator runs with the new image while the current version keeps
# serving, api and frontend are switched, health and /auth/session are checked and the agent packages are published.
# Before the migrator succeeds the running containers are never touched; the migrator applies and commits each migration on its own, so
# when it fails halfway the earlier ones stay applied (the schema level before and after is logged). After it, nothing is reverted
# automatically: `rollback` puts the previous digests back but refuses when the schema level is not the one of that release.
# Never runs `docker compose down -v`. Never prints the .env: `docker compose config` is only piped through awk.
# Exit codes: 0 ok, 1 usage or preflight, 2 validation (ports, config), 3 failed before the migrator succeeded (containers untouched),
# 4 failed after the migrator ran (no automatic rollback), 5 the agent packages could not be published.
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd -P)
cd "$repo"
# shellcheck disable=SC1091
. "$repo/scripts/lib/itmas-host.sh"
itmas_load_conf

: "${ITMAS_REGISTRY:=ghcr.io/solucionesjj}"
: "${ITMAS_PUBLIC_HOST:=itmas.sacservices.cloud}"
: "${ITMAS_STATE_DIR:=/var/lib/itmas3}"
: "${AGENT_DOWNLOADS_DIR:=$repo/agent/dist}"
: "${ITMAS_POLL_SECONDS:=3}"
: "${ITMAS_HEALTH_TIMEOUT:=180}"

state="$ITMAS_STATE_DIR"
postgres_volume="itmas3_postgres-data"
assume_yes=0
schema_changed_ok=0
phase="preflight"
lock_held=0
digest_api=""
digest_migrator=""
digest_frontend=""

usage() {
  sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit "${1:-1}"
}

compose() {
  docker compose -f docker-compose.yml -f docker-compose.prod.yml "$@"
}

record() {
  mkdir -p "$state"
  printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >> "$state/deploy.log"
}

cleanup() {
  [ "$lock_held" -eq 0 ] || itmas_unlock "$state/lock"
}
trap cleanup EXIT
trap 'exit 143' TERM HUP
trap 'exit 130' INT

fail() {
  code="$1"
  shift
  itmas_err "ERROR: $*"
  record "failed phase=$phase code=$code ${ITMAS_TAG:-?}: $*"
  itmas_notify_fail "deploy ${ITMAS_TAG:-?} failed in phase $phase: $*"
  exit "$code"
}

confirm() {
  [ "$assume_yes" -eq 1 ] && return 0
  if [ ! -t 0 ]; then
    fail 1 "confirmation required and there is no terminal (use --yes after comparing the commit and the digests with the release summary)"
  fi
  printf 'Continue? [y/N] '
  read -r answer
  case "$answer" in
    y | Y | yes | YES) return 0 ;;
    *)
      itmas_err "Cancelled by the operator; nothing was changed."
      record "cancelled phase=$phase ${ITMAS_TAG:-?}"
      exit 1
      ;;
  esac
}

take_lock() {
  mkdir -p "$state" || fail 1 "cannot create $state (run this script with sudo)"
  itmas_lock "$state/lock" || fail 1 "another deployment is running (lock $state/lock)"
  lock_held=1
}

valid_tag() { printf '%s' "$1" | grep -Eq '^v[0-9]+\.[0-9]+\.[0-9]+$'; }
valid_digest() { printf '%s' "$1" | grep -Eq '^sha256:[0-9a-f]{64}$'; }
valid_version() { printf '%s' "$1" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$'; }

# Lists "<service> <host_ip> <published>" for every published port of the resulting Compose files.
published_ports() {
  compose config 2> /dev/null | awk '
    function flush() { if (inentry) { print svc, (host_ip == "" ? "0.0.0.0" : host_ip), published; inentry = 0 } }
    /^  [A-Za-z0-9_.-]+:[ ]*$/ { flush(); inports = 0; svc = $1; sub(/:$/, "", svc); next }
    /^    ports:[ ]*$/ { inports = 1; next }
    inports && /^    [A-Za-z_]/ { flush(); inports = 0; next }
    inports && /^      - / { flush(); inentry = 1; host_ip = ""; published = "" }
    inports && /host_ip:/ { v = $0; sub(/.*host_ip:[ ]*/, "", v); gsub(/["\047 ]/, "", v); host_ip = v }
    inports && /published:/ { v = $0; sub(/.*published:[ ]*/, "", v); gsub(/["\047 ]/, "", v); published = v }
    END { flush() }
  '
}

# Only the frontend, on 127.0.0.1, may publish a port (constitution 8.4 and 8.7 b, ADR-0018). Sets frontend_port.
guard_ports() {
  ports="$(published_ports)"
  frontend_port=""
  offenders=""
  if [ -n "$ports" ]; then
    while read -r svc host_ip published; do
      if [ "$svc" = "frontend" ] && [ "$host_ip" = "127.0.0.1" ] && [ -z "$frontend_port" ]; then
        frontend_port="$published"
      else
        offenders="$offenders $svc:$host_ip:$published"
      fi
    done << EOF
$ports
EOF
  fi
  [ -z "$offenders" ] || fail 2 "the Compose files publish ports other than the frontend on 127.0.0.1:$offenders"
  [ -n "$frontend_port" ] || fail 2 "the frontend does not publish a port on 127.0.0.1"
}

image_ref() {
  printf '%s/itmas3-%s' "$ITMAS_REGISTRY" "$1"
}

resolve_digest() { # $1 image name, $2 tag
  docker buildx imagetools inspect "$(image_ref "$1"):$2" --format '{{.Manifest.Digest}}' 2> /dev/null || true
}

resolve_digests() {
  for name in api migrator frontend; do
    digest="$(resolve_digest "$name" "$1")"
    valid_digest "$digest" || fail 1 "cannot resolve the digest of $(image_ref "$name"):$1 (is it published, and are you logged in to GHCR?)"
    case "$name" in
      api) digest_api="$digest" ;;
      migrator) digest_migrator="$digest" ;;
      frontend) digest_frontend="$digest" ;;
    esac
  done
}

export_images() {
  ITMAS_API_IMAGE="$(image_ref api)@$digest_api"
  ITMAS_MIGRATOR_IMAGE="$(image_ref migrator)@$digest_migrator"
  ITMAS_FRONTEND_IMAGE="$(image_ref frontend)@$digest_frontend"
  export ITMAS_API_IMAGE ITMAS_MIGRATOR_IMAGE ITMAS_FRONTEND_IMAGE
}

# The release files are data, never sourced: every field is read and validated.
release_field() { # $1 file, $2 field
  sed -n "s/^$2=//p" "$1" | head -n 1
}

load_release() { # $1 file; sets rel_tag, rel_schema and the three digests
  rel_tag="$(release_field "$1" ITMAS_TAG)"
  rel_schema="$(release_field "$1" ITMAS_SCHEMA)"
  valid_tag "$rel_tag" || fail 1 "$1 has no valid tag"
  digest_api="$(release_field "$1" ITMAS_API_IMAGE)"
  digest_api="${digest_api##*@}"
  digest_migrator="$(release_field "$1" ITMAS_MIGRATOR_IMAGE)"
  digest_migrator="${digest_migrator##*@}"
  digest_frontend="$(release_field "$1" ITMAS_FRONTEND_IMAGE)"
  digest_frontend="${digest_frontend##*@}"
  valid_digest "$digest_api" && valid_digest "$digest_migrator" && valid_digest "$digest_frontend" || fail 1 "$1 has an invalid digest"
}

write_release() { # $1 file, $2 tag, $3 status, $4 schema level
  {
    echo "ITMAS_TAG=$2"
    echo "ITMAS_STATUS=$3"
    echo "ITMAS_SCHEMA=$4"
    echo "ITMAS_API_IMAGE=$ITMAS_API_IMAGE"
    echo "ITMAS_MIGRATOR_IMAGE=$ITMAS_MIGRATOR_IMAGE"
    echo "ITMAS_FRONTEND_IMAGE=$ITMAS_FRONTEND_IMAGE"
  } > "$1.new"
  mv "$1.new" "$1"
}

show_release() { # $1 title, $2 file
  if [ -f "$2" ]; then
    printf '%s:\n' "$1"
    sed 's/^/  /' "$2"
  else
    printf '%s: none\n' "$1"
  fi
}

# Last applied migration, "none" when the history table does not exist yet. The query has no quotes so it survives sh -c.
schema_level() {
  level="$(compose exec -T postgres sh -c 'psql -At -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "select coalesce(max(migration_id), chr(45)) from app_migrations.__ef_migrations_history"' 2> /dev/null || true)"
  case "$level" in "" | "-") level="none" ;; esac
  printf '%s' "$level"
}

wait_healthy() { # $1 service
  elapsed=0
  while [ "$elapsed" -le "$ITMAS_HEALTH_TIMEOUT" ]; do
    cid="$(compose ps -q "$1" 2> /dev/null || true)"
    if [ -n "$cid" ]; then
      status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$cid" 2> /dev/null || true)"
      case "$status" in
        healthy) return 0 ;;
        unhealthy) return 1 ;;
      esac
    fi
    sleep "$ITMAS_POLL_SECONDS"
    elapsed=$((elapsed + ITMAS_POLL_SECONDS + 1))
  done
  return 1
}

# The Host header must be an allowed one: AllowedHosts answers 400 to anything else (SPEC-0009 RF-26).
probe() {
  body="$(curl -fsS -m 10 -H "Host: $ITMAS_PUBLIC_HOST" "http://127.0.0.1:$frontend_port/auth/session" 2> /dev/null || true)"
  case "$body" in
    *'"isAuthenticated":false'*) return 0 ;;
    *) return 1 ;;
  esac
}

run_backup() {
  if [ -n "${ITMAS_BACKUP_CMD:-}" ]; then
    "$ITMAS_BACKUP_CMD" --pre-deploy
  else
    "$repo/scripts/backup-db.sh" --pre-deploy
  fi
}

# Extracts the packages of the agent version of this checkout into AGENT_DOWNLOADS_DIR/<version> (never overwriting a published
# version, RN-7) and moves latest at the end by renaming directories (nginx serves that path with disable_symlinks on), but only
# when this is the highest version present, so deploying an older tag cannot take latest back.
publish_agent_packages() {
  version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' agent/Directory.Build.props)"
  valid_version "$version" || { itmas_err "agent version not found in agent/Directory.Build.props"; return 1; }
  downloads="$AGENT_DOWNLOADS_DIR"
  if [ -d "$downloads/$version" ]; then
    itmas_log "Agent $version is already in $downloads; it is not overwritten."
    return 0
  fi
  ref="$ITMAS_REGISTRY/itmas3-agent:$version"
  if ! docker pull -q "$ref" > /dev/null 2>&1; then
    itmas_err "The packages of agent $version are not published ($ref): the release workflow publishes them when the version is new."
    return 1
  fi
  mkdir -p "$downloads" || { itmas_err "cannot create $downloads"; return 1; }
  incoming="$(mktemp -d "$downloads/.incoming.XXXXXX")" || { itmas_err "cannot write in $downloads"; return 1; }
  [ -n "$incoming" ] && [ -d "$incoming" ] || return 1
  cid="$(docker create "$ref" placeholder)" || { rm -rf "$incoming"; return 1; }
  if ! docker cp "$cid:/$version/." "$incoming/"; then
    docker rm "$cid" > /dev/null 2>&1 || true
    rm -rf "$incoming"
    return 1
  fi
  docker rm "$cid" > /dev/null 2>&1 || true
  if ! (cd "$incoming" && sha256sum -c SHA256SUMS > /dev/null); then
    itmas_err "The checksums of the agent $version packages do not match: not published."
    rm -rf "$incoming"
    return 1
  fi
  # Nothing may be published that SHA256SUMS does not list.
  listed="$(sed 's/^[0-9a-f]*  *\*\{0,1\}//' "$incoming/SHA256SUMS" | sort)"
  present="$(find "$incoming" -mindepth 1 -maxdepth 1 ! -name SHA256SUMS | sed 's#.*/##' | sort)"
  if [ "$listed" != "$present" ]; then
    itmas_err "The agent $version image has files that SHA256SUMS does not list: not published."
    rm -rf "$incoming"
    return 1
  fi
  chmod 755 "$incoming"
  find "$incoming" -type f -exec chmod 644 {} +
  mv "$incoming" "$downloads/$version" || { rm -rf "$incoming"; return 1; }
  docker image rm "$ref" > /dev/null 2>&1 || true
  newest="$(find "$downloads" -mindepth 1 -maxdepth 1 -type d | sed 's#.*/##' | grep -E '^[0-9]+\.[0-9]+\.[0-9]+$' | sort -t. -k1,1n -k2,2n -k3,3n | tail -n 1)"
  if [ "$newest" != "$version" ]; then
    itmas_log "Agent $version published in $downloads; latest stays at $newest (it is not the highest version)."
    return 0
  fi
  rm -rf "$downloads/latest.new"
  cp -R "$downloads/$version" "$downloads/latest.new" || return 1
  [ ! -d "$downloads/latest" ] || mv "$downloads/latest" "$downloads/latest.old"
  mv "$downloads/latest.new" "$downloads/latest" || return 1
  rm -rf "$downloads/latest.old"
  itmas_log "Agent $version published in $downloads (latest updated)."
}

# Keeps the images of the current and the previous release and removes the older ones of this project (best effort).
prune_images() {
  keep="$(grep -h '^ITMAS_.*_IMAGE=' "$state/current.env" "$state/previous.env" 2> /dev/null | sed 's/.*@//' || true)"
  for name in api migrator frontend; do
    docker image ls --no-trunc --format '{{.ID}} {{.Digest}}' "$(image_ref "$name")" 2> /dev/null | while read -r id digest; do
      case "
$keep
" in
        *"
$digest
"*) ;;
        *) docker image rm "$id" > /dev/null 2>&1 || true ;;
      esac
    done
  done
}

deploy() {
  tag="$1"
  ITMAS_TAG="$tag"
  valid_tag "$tag" || fail 1 "the tag must look like vMAJOR.MINOR.PATCH"
  take_lock
  record "start deploy $tag"

  phase="verify-tag"
  [ -z "$(git status --porcelain)" ] || fail 1 "the clone has local changes; commit or discard them first"
  # No --force: a tag that moved since it was fetched is refused (the digests were reviewed for the original commit).
  git fetch --quiet origin main || fail 1 "git fetch of main failed"
  git fetch --tags --quiet origin || fail 1 "git fetch of the tags failed, or the tag $tag moved"
  commit="$(git rev-parse -q --verify "refs/tags/$tag^{commit}")" || fail 1 "the tag $tag does not exist"
  git merge-base --is-ancestor "$commit" origin/main || fail 1 "the commit of $tag is not in origin/main"

  phase="digests"
  resolve_digests "$tag"
  printf 'Release %s\n  commit    %s\n' "$tag" "$commit"
  printf '  %-9s %s\n' api "$digest_api" migrator "$digest_migrator" frontend "$digest_frontend"
  echo "Compare the commit and the digests with the summary of the release workflow run."
  confirm

  phase="checkout"
  git checkout --quiet --detach "$commit" || fail 1 "cannot check out $tag"
  export_images

  phase="validation"
  compose config -q || fail 2 "docker compose config is not valid"
  guard_ports

  phase="backup"
  # The decision is the data volume, not a running container: a stopped postgres still holds the data to protect.
  if docker volume inspect "$postgres_volume" > /dev/null 2>&1; then
    compose up -d --no-build postgres || fail 1 "cannot start postgres for the pre-deployment dump"
    wait_healthy postgres || fail 1 "postgres is not healthy; nothing was changed"
    run_backup || fail 1 "the pre-deployment dump failed; nothing was changed"
  else
    itmas_log "No postgres data volume yet (first installation): no database to dump."
  fi

  phase="pull"
  compose pull api migrator frontend || fail 1 "cannot pull the images; nothing was changed"

  phase="migrator"
  compose up -d --no-build postgres || fail 3 "cannot start postgres"
  wait_healthy postgres || fail 3 "postgres is not healthy"
  schema_before="$(schema_level)"
  if ! compose run --rm --no-deps migrator; then
    schema_after="$(schema_level)"
    record "migrator failed schema $schema_before -> $schema_after"
    fail 3 "the migrator failed: the containers were not touched, but each migration is applied on its own, so the schema may have advanced ($schema_before -> $schema_after); if the running version cannot work with it, restore the pre-deployment dump (DEVELOPMENT.md 12.9)"
  fi
  schema_after="$(schema_level)"
  record "migrator ok schema $schema_before -> $schema_after"

  phase="switch"
  # The release to roll back to is the last one that was verified, and a redeployment of the same tag must not replace it.
  if [ -f "$state/current.env" ] && [ "$(release_field "$state/current.env" ITMAS_TAG)" != "$tag" ] && [ "$(release_field "$state/current.env" ITMAS_STATUS)" = "ok" ]; then
    cp "$state/current.env" "$state/previous.env"
  fi
  write_release "$state/current.env" "$tag" unverified "$schema_after"
  compose up -d --no-build --no-deps api || fail 4 "cannot start api (no automatic rollback: see 'deploy.sh rollback')"
  wait_healthy api || fail 4 "api is not healthy (no automatic rollback: see 'deploy.sh rollback')"
  compose up -d --no-build --no-deps frontend || fail 4 "cannot start frontend (no automatic rollback)"
  wait_healthy frontend || fail 4 "frontend is not healthy (no automatic rollback)"

  phase="probe"
  probe || fail 4 "/auth/session did not answer as expected through 127.0.0.1:$frontend_port (no automatic rollback)"
  write_release "$state/current.env" "$tag" ok "$schema_after"
  record "ok deploy $tag commit=$commit api=$digest_api migrator=$digest_migrator frontend=$digest_frontend"

  phase="agent"
  prune_images
  if ! publish_agent_packages; then
    record "agent packages not published for $tag"
    itmas_notify_fail "deploy $tag ok, but the agent packages could not be published"
    itmas_err "The release is deployed; the agent packages failed (see above)."
    exit 5
  fi
  itmas_log "Release $tag deployed."
}

rollback() {
  [ -f "$state/previous.env" ] || fail 1 "there is no previous release recorded in $state"
  take_lock
  load_release "$state/previous.env"
  prev_tag="$rel_tag"
  prev_schema="$rel_schema"
  ITMAS_TAG="rollback-to-$prev_tag"
  show_release "Current" "$state/current.env"
  show_release "Previous (will be restored, without running the migrator)" "$state/previous.env"
  phase="schema-check"
  export_images
  postgres_up="$(compose ps -q postgres 2> /dev/null || true)"
  [ -n "$postgres_up" ] || fail 1 "postgres is not running: start it before a rollback"
  now_schema="$(schema_level)"
  if [ -n "$prev_schema" ] && [ "$prev_schema" != "$now_schema" ]; then
    if [ "$schema_changed_ok" -ne 1 ]; then
      fail 1 "the schema is at $now_schema and $prev_tag worked with $prev_schema: restore the pre-deployment dump instead (DEVELOPMENT.md 12.9), or pass --schema-changed-ok if you know it is compatible"
    fi
    itmas_err "Warning: the schema is at $now_schema, not at $prev_schema (--schema-changed-ok)."
  fi
  confirm
  record "start rollback to $prev_tag"
  phase="checkout"
  [ -z "$(git status --porcelain)" ] || fail 1 "the clone has local changes"
  git checkout --quiet --detach "$prev_tag" || fail 1 "cannot check out $prev_tag"

  phase="validation"
  compose config -q || fail 2 "docker compose config is not valid"
  guard_ports

  phase="switch"
  compose pull api frontend || fail 1 "cannot pull the previous images"
  compose up -d --no-build --no-deps api || fail 4 "cannot start api"
  wait_healthy api || fail 4 "api is not healthy"
  compose up -d --no-build --no-deps frontend || fail 4 "cannot start frontend"
  wait_healthy frontend || fail 4 "frontend is not healthy"
  phase="probe"
  probe || fail 4 "/auth/session did not answer as expected"

  # The release that was just left becomes the one to go back to only if it had been verified: a failed one must never be offered again.
  if [ -f "$state/current.env" ] && [ "$(release_field "$state/current.env" ITMAS_STATUS)" = "ok" ]; then
    cp "$state/current.env" "$state/rolled-back.env"
  else
    rm -f "$state/rolled-back.env"
  fi
  cp "$state/previous.env" "$state/current.env.new"
  if [ -f "$state/rolled-back.env" ]; then
    mv "$state/rolled-back.env" "$state/previous.env"
  else
    rm -f "$state/previous.env"
  fi
  mv "$state/current.env.new" "$state/current.env"
  record "ok rollback to $prev_tag"
  itmas_log "Rolled back to $prev_tag."
}

command="${1:-}"
[ -n "$command" ] || usage
case "$command" in -h | --help) usage 0 ;; esac
shift
for arg in "$@"; do
  case "$arg" in
    --yes) assume_yes=1 ;;
    --schema-changed-ok) [ "$command" = rollback ] || usage; schema_changed_ok=1 ;;
    *) usage ;;
  esac
done

case "$command" in
  status)
    show_release "Current" "$state/current.env"
    show_release "Previous" "$state/previous.env"
    ;;
  rollback) rollback ;;
  v*) deploy "$command" ;;
  *) usage ;;
esac
