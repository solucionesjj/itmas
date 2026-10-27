#!/bin/sh
# Tests for scripts/deploy.sh (SPEC-0009 CA-11, CA-12, CA-13, CA-15). Self-contained: docker, git, curl and the backup command are
# replaced by stubs that log every call, so the test needs no Docker, network, registry or .env. It runs the real script against
# temporary state and download directories.
set -eu

root=$(cd "$(dirname "$0")/.." && pwd)
deploy="$root/scripts/deploy.sh"
tmp=$(mktemp -d)
trap 'chmod -R u+rwx "$tmp" 2> /dev/null || true; rm -rf "$tmp"' EXIT
trap 'exit 143' TERM HUP
trap 'exit 130' INT

bin="$tmp/bin"
mkdir -p "$bin"
log="$tmp/calls.log"
pings="$tmp/pings.log"
checks=0
failures=0
rc=0
agent_version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/agent/Directory.Build.props")"

cat > "$bin/docker" << 'EOF'
#!/bin/sh
echo "docker $*" >> "$STUB_LOG"
schema_file="$STUB_DIR/schema"
case "$1" in
  compose)
    shift
    while [ "${1:-}" = "-f" ]; do shift 2; done
    sub="$1"
    shift
    case "$sub" in
      config)
        if [ "${1:-}" = "-q" ]; then
          [ "${STUB_CONFIG_FAIL:-0}" = 1 ] && exit 1
          exit 0
        fi
        # the shape of "docker compose config": every published port has mode, host_ip (when set), target, published and protocol
        {
          echo "services:"
          # STUB_PORT_EXTRA=first lists the api (with a published loopback port) before the frontend
          [ "${STUB_PORT_EXTRA:-0}" != first ] || {
            echo "  api:"
            echo "    image: y"
            echo "    ports:"
            echo "      - mode: ingress"
            echo "        host_ip: 127.0.0.1"
            echo "        target: 8080"
            echo '        published: "9090"'
            echo "        protocol: tcp"
          }
          echo "  frontend:"
          echo "    image: x"
          if [ "${STUB_NO_FRONTEND_PORTS:-0}" != 1 ]; then
            echo "    ports:"
            echo "      - mode: ingress"
            [ "${STUB_PORT_NOHOST:-0}" = 1 ] || echo "        host_ip: ${STUB_PORT_HOST:-127.0.0.1}"
            echo "        target: 8080"
            echo '        published: "8088"'
            echo "        protocol: tcp"
          fi
          if [ "${STUB_PORT_EXTRA:-0}" != first ]; then
            echo "  api:"
            echo "    image: y"
          fi
          if [ "${STUB_PORT_EXTRA:-0}" = 1 ]; then
            echo "    ports:"
            echo "      - mode: ingress"
            echo "        host_ip: 127.0.0.1"
            echo "        target: 8080"
            echo '        published: "9090"'
            echo "        protocol: tcp"
          fi
          echo "networks:"
          echo "  edge:"
          echo "    driver: bridge"
        }
        ;;
      ps)
        svc="$2"
        if [ "$svc" = postgres ] && [ "${STUB_POSTGRES_UP:-1}" = 0 ] && [ ! -f "$STUB_DIR/postgres-started" ]; then exit 0; fi
        echo "cid-$svc"
        ;;
      up)
        case " $* " in *" postgres "*) touch "$STUB_DIR/postgres-started" ;; esac
        ;;
      exec)
        # schema level query of deploy.sh
        [ -f "$schema_file" ] && cat "$schema_file" || echo "20260101000000_Initial"
        ;;
      run)
        [ "${STUB_MIGRATOR_ADVANCES:-0}" = 1 ] && echo "20260301000000_New" > "$schema_file"
        exit "${STUB_MIGRATOR_RC:-0}"
        ;;
      pull) exit "${STUB_PULL_RC:-0}" ;;
    esac
    ;;
  volume) exit "${STUB_VOLUME_RC:-0}" ;;
  inspect)
    for last; do :; done
    case "${last#cid-}" in
      postgres) echo "${STUB_HEALTH_POSTGRES:-healthy}" ;;
      api) echo "${STUB_HEALTH_API:-healthy}" ;;
      frontend) echo "${STUB_HEALTH_FRONTEND:-healthy}" ;;
      *) echo healthy ;;
    esac
    ;;
  buildx)
    [ "${STUB_NO_DIGEST:-0}" = 1 ] && exit 1
    # docker buildx imagetools inspect <ref> --format ...
    ref="$4"
    printf 'sha256:%064x\n' "$(printf '%s' "$ref" | cksum | cut -d' ' -f1)"
    ;;
  pull) exit "${STUB_AGENT_PULL_RC:-0}" ;;
  create) echo "cid-agent" ;;
  cp)
    src="$2"
    dest="$3"
    version="${src#*:/}"
    version="${version%%/*}"
    printf 'package %s\n' "$version" > "$dest/itmas-agent-linux-x64.tar.gz"
    (cd "$dest" && sha256sum itmas-agent-linux-x64.tar.gz > SHA256SUMS)
    [ "${STUB_AGENT_BAD_SUM:-0}" = 1 ] && printf '%064d  itmas-agent-linux-x64.tar.gz\n' 0 > "$dest/SHA256SUMS"
    [ "${STUB_AGENT_EXTRA_FILE:-0}" = 1 ] && echo "unexpected" > "$dest/extra.txt"
    exit 0
    ;;
esac
exit 0
EOF

cat > "$bin/git" << 'EOF'
#!/bin/sh
echo "git $*" >> "$STUB_LOG"
case "$1" in
  status) [ "${STUB_GIT_DIRTY:-0}" = 1 ] && echo " M file" ;;
  fetch)
    case " $* " in *" --tags "*) [ "${STUB_TAG_MOVED:-0}" = 1 ] && exit 1 ;; esac
    ;;
  rev-parse)
    [ "${STUB_TAG_MISSING:-0}" = 1 ] && exit 1
    echo "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef"
    ;;
  merge-base) [ "${STUB_NOT_IN_MAIN:-0}" = 1 ] && exit 1 ;;
esac
exit 0
EOF

cat > "$bin/curl" << 'EOF'
#!/bin/sh
echo "curl $*" >> "$STUB_LOG"
case " $* " in
  *" -K "*)
    # heartbeat: the URL arrives on stdin (it is a secret) and is recorded apart
    cat >> "$STUB_PINGS"
    echo "args: $*" >> "$STUB_PINGS"
    ;;
  *)
    [ "${STUB_PROBE_FAIL:-0}" = 1 ] && exit 22
    if [ -f "$STUB_DIR/probe-body" ]; then cat "$STUB_DIR/probe-body"; exit 0; fi
    echo '{"isAuthenticated":false,"user":null,"isRoot":false,"permissions":[]}'
    ;;
esac
exit 0
EOF

cat > "$bin/backup-stub" << 'EOF'
#!/bin/sh
echo "backup $*" >> "$STUB_LOG"
exit "${STUB_BACKUP_RC:-0}"
EOF

# macOS has no sha256sum; the stub uses shasum so that the script runs unchanged on both systems.
if ! command -v sha256sum > /dev/null 2>&1; then
  printf '#!/bin/sh\nexec shasum -a 256 "$@"\n' > "$bin/sha256sum"
fi
chmod +x "$bin"/*

fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$1" >&2
}

# reset: new state, downloads and logs; STUB_* variables of a scenario are set by the caller around run().
reset() {
  chmod -R u+rwx "$tmp/state" "$tmp/downloads" 2> /dev/null || true
  rm -rf "$tmp/state" "$tmp/downloads" "$tmp/stubdir"
  mkdir -p "$tmp/state" "$tmp/downloads" "$tmp/stubdir"
  : > "$log"
  : > "$pings"
  printf 'HEARTBEAT_URL=https://monitor.example.invalid/ping/secret-token-123\n' > "$tmp/host.conf"
  chmod 600 "$tmp/host.conf"
}

run() {
  rc=0
  PATH="$bin:$PATH" STUB_LOG="$log" STUB_PINGS="$pings" STUB_DIR="$tmp/stubdir" \
    ITMAS_STATE_DIR="$tmp/state" AGENT_DOWNLOADS_DIR="$tmp/downloads" ITMAS_HOST_CONF="$tmp/host.conf" \
    ITMAS_BACKUP_CMD="${TEST_BACKUP_CMD-$bin/backup-stub}" ITMAS_POLL_SECONDS=0 ITMAS_HEALTH_TIMEOUT=2 \
    sh "$deploy" "$@" > "$tmp/out" 2>&1 < /dev/null || rc=$?
}

expect_rc() { # description, expected rc
  checks=$((checks + 1))
  [ "$rc" -eq "$2" ] || { fail "$1: exit status $rc, expected $2 (output: $(tr '\n' ' ' < "$tmp/out" | cut -c1-300))"; return 0; }
}

expect_called() { # description, fixed string expected in the call log
  checks=$((checks + 1))
  grep -qF -- "$2" "$log" || fail "$1: '$2' was not called"
}

expect_not_called() {
  checks=$((checks + 1))
  if grep -qF -- "$2" "$log"; then fail "$1: '$2' was called (first call: $(grep -F -- "$2" "$log" | head -n 1))"; fi
}

expect_order() { # description, first, second: both called and first before second
  checks=$((checks + 1))
  a=$(grep -nF -- "$2" "$log" | head -n 1 | cut -d: -f1 || true)
  b=$(grep -nF -- "$3" "$log" | head -n 1 | cut -d: -f1 || true)
  if [ -z "$a" ] || [ -z "$b" ] || [ "$a" -ge "$b" ]; then fail "$1: '$2' must come before '$3' (lines '$a' and '$b')"; fi
}

expect_file_has() { # description, file, fixed string
  checks=$((checks + 1))
  grep -qF -- "$3" "$2" 2> /dev/null || fail "$1: '$3' not found in $2"
}

expect_file_lacks() {
  checks=$((checks + 1))
  if grep -qF -- "$3" "$2" 2> /dev/null; then fail "$1: '$3' found in $2"; fi
}

expect_exists() {
  checks=$((checks + 1))
  [ -e "$2" ] || fail "$1: $2 does not exist"
}

expect_missing() {
  checks=$((checks + 1))
  [ ! -e "$2" ] || fail "$1: $2 exists"
}

# Scenario 1: first installation (no data volume yet, so no dump), everything healthy.
reset
STUB_POSTGRES_UP=0 STUB_VOLUME_RC=1 && export STUB_POSTGRES_UP STUB_VOLUME_RC
run v1.0.0 --yes
unset STUB_POSTGRES_UP STUB_VOLUME_RC
expect_rc "first deployment" 0
expect_file_has "state records the tag" "$tmp/state/current.env" "ITMAS_TAG=v1.0.0"
expect_file_has "state is verified ok" "$tmp/state/current.env" "ITMAS_STATUS=ok"
expect_file_has "state records the schema level" "$tmp/state/current.env" "ITMAS_SCHEMA=20260101000000_Initial"
expect_file_has "images are pinned by digest" "$tmp/state/current.env" "itmas3-api@sha256:"
expect_file_has "deployment log records the result and the commit" "$tmp/state/deploy.log" "ok deploy v1.0.0 commit=deadbeef"
expect_file_has "the commit is shown before the confirmation" "$tmp/out" "commit    deadbeef"
expect_not_called "first installation does not dump" "backup --pre-deploy"
expect_order "the tag is verified against main before anything else" "merge-base --is-ancestor" "checkout"
expect_called "the clone moves to the verified commit, not to the tag name" "checkout --quiet --detach deadbeefdeadbeefdeadbeefdeadbeefdeadbeef"
expect_file_has "every release image is recorded with its own digest" "$tmp/state/current.env" "ITMAS_MIGRATOR_IMAGE=ghcr.io/solucionesjj/itmas3-migrator@sha256:"
checks=$((checks + 1))
[ "$(sed -n 's/^ITMAS_API_IMAGE=.*@//p' "$tmp/state/current.env")" != "$(sed -n 's/^ITMAS_MIGRATOR_IMAGE=.*@//p' "$tmp/state/current.env")" ] || fail "the api and the migrator are recorded with the same digest"
expect_file_has "the deployment log records the digest of the api" "$tmp/state/deploy.log" "api=sha256:"
checks=$((checks + 1))
grep -Eq '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:]{8}Z ok deploy v1.0.0' "$tmp/state/deploy.log" || fail "the deployment log has no timestamp on the result"
expect_file_lacks "the secret heartbeat URL is not in the deployment log" "$tmp/state/deploy.log" "secret-token-123"
expect_order "the migrator runs before the switch" "run --rm --no-deps migrator" "up -d --no-build --no-deps api"
expect_order "api is switched before the frontend" "up -d --no-build --no-deps api" "up -d --no-build --no-deps frontend"
expect_called "images are pulled by digest, not built" "pull api migrator frontend"
expect_not_called "nothing is ever torn down with its volumes" "docker-compose.prod.yml down"
expect_not_called "a tag is never fetched with --force" "--force"
expect_called "the probe sends an allowed Host" "-H Host: itmas.sacservices.cloud http://127.0.0.1:8088/auth/session"
expect_exists "agent packages are published" "$tmp/downloads/$agent_version/itmas-agent-linux-x64.tar.gz"
expect_exists "latest is a directory with the packages" "$tmp/downloads/latest/SHA256SUMS"
expect_missing "no temporary latest is left" "$tmp/downloads/latest.new"
checks=$((checks + 1))
[ ! -L "$tmp/downloads/latest" ] || fail "latest must be a directory, not a symlink"

# Scenario 2: second release with the database: dump first, previous release kept, published agent not overwritten.
echo "marker" > "$tmp/downloads/$agent_version/marker"
: > "$log"
run v1.1.0 --yes
expect_rc "second deployment" 0
expect_order "the dump comes before the pull" "backup --pre-deploy" "pull api migrator frontend"
expect_file_has "previous release is kept" "$tmp/state/previous.env" "ITMAS_TAG=v1.0.0"
expect_file_has "current release is the new one" "$tmp/state/current.env" "ITMAS_TAG=v1.1.0"
expect_exists "a published agent version is not overwritten" "$tmp/downloads/$agent_version/marker"
expect_not_called "no agent image is pulled when the version exists" "pull -q"
cp "$tmp/state/current.env" "$tmp/after-v1.1.0.env"
cp "$tmp/state/previous.env" "$tmp/after-v1.1.0-previous.env"

# Scenario 3: redeploying the same tag (the documented steps after editing the .env) must not replace the release to roll back to.
: > "$log"
run v1.1.0 --yes
expect_rc "redeployment of the same tag" 0
expect_file_has "previous release is still the real previous one" "$tmp/state/previous.env" "ITMAS_TAG=v1.0.0"
expect_file_has "current release is unchanged" "$tmp/state/current.env" "ITMAS_TAG=v1.1.0"

# Scenario 4: manual rollback with the schema unchanged: previous images, no migrator, releases swapped.
: > "$log"
run rollback --yes
expect_rc "rollback" 0
expect_not_called "rollback does not run the migrator" "run --rm"
expect_called "rollback restarts api" "up -d --no-build --no-deps api"
expect_file_has "rollback restores the previous tag" "$tmp/state/current.env" "ITMAS_TAG=v1.0.0"
expect_file_has "rollback keeps the rolled-back release" "$tmp/state/previous.env" "ITMAS_TAG=v1.1.0"
expect_file_has "rollback is logged" "$tmp/state/deploy.log" "ok rollback to v1.0.0"
expect_not_called "a rollback never tears anything down" "docker-compose.prod.yml down"

# Scenario 5: a release whose migration advanced the schema: rollback refuses unless told the schema change is compatible.
reset
STUB_MIGRATOR_ADVANCES=0 && export STUB_MIGRATOR_ADVANCES
run v1.0.0 --yes
STUB_MIGRATOR_ADVANCES=1
run v1.1.0 --yes
unset STUB_MIGRATOR_ADVANCES
expect_rc "a release with a migration" 0
expect_file_has "the new schema level is recorded" "$tmp/state/current.env" "ITMAS_SCHEMA=20260301000000_New"
expect_file_has "the schema change is logged" "$tmp/state/deploy.log" "migrator ok schema 20260101000000_Initial -> 20260301000000_New"
: > "$log"
run rollback --yes
expect_rc "rollback over a changed schema" 1
expect_file_has "the refusal points to the dump" "$tmp/out" "restore the pre-deployment dump"
expect_not_called "nothing is switched when the schema changed" "up -d --no-build --no-deps api"
expect_file_has "the current release is unchanged" "$tmp/state/current.env" "ITMAS_TAG=v1.1.0"
run rollback --yes --schema-changed-ok
expect_rc "rollback with --schema-changed-ok" 0
expect_file_has "it then restores the previous tag" "$tmp/state/current.env" "ITMAS_TAG=v1.0.0"
run deploy.sh-is-not-a-command
expect_rc "an unknown command" 1
run v1.2.0 --schema-changed-ok
expect_rc "--schema-changed-ok only belongs to rollback" 1

# Scenario 6: the migrator fails: the running containers are not touched; a partial migration is reported.
reset
cp "$tmp/after-v1.1.0.env" "$tmp/state/current.env"
STUB_MIGRATOR_RC=1 && export STUB_MIGRATOR_RC
run v1.2.0 --yes
unset STUB_MIGRATOR_RC
expect_rc "migrator failure" 3
expect_not_called "nothing is switched after a migrator failure" "up -d --no-build --no-deps api"
expect_file_has "the recorded release is unchanged" "$tmp/state/current.env" "ITMAS_TAG=v1.1.0"
expect_file_has "the failure is reported through the heartbeat channel" "$pings" "secret-token-123"
expect_file_has "the failure ping goes to /fail" "$pings" "/fail"
expect_file_lacks "the secret URL never appears in a command line" "$log" "secret-token-123"
reset
STUB_MIGRATOR_RC=1 STUB_MIGRATOR_ADVANCES=1 && export STUB_MIGRATOR_RC STUB_MIGRATOR_ADVANCES
run v1.2.0 --yes
unset STUB_MIGRATOR_RC STUB_MIGRATOR_ADVANCES
expect_rc "migrator failing after an earlier migration was applied" 3
expect_file_has "the message says the schema may have advanced" "$tmp/out" "the schema may have advanced"
expect_file_has "the levels before and after are logged" "$tmp/state/deploy.log" "migrator failed schema 20260101000000_Initial -> 20260301000000_New"

# Scenario 7: api is not healthy after the migrator ran: no automatic rollback; the failed release never becomes the one to roll back to.
reset
cp "$tmp/after-v1.1.0.env" "$tmp/state/current.env"
cp "$tmp/after-v1.1.0-previous.env" "$tmp/state/previous.env"
STUB_HEALTH_API=unhealthy && export STUB_HEALTH_API
run v1.2.0 --yes
unset STUB_HEALTH_API
expect_rc "failure after the migrator" 4
expect_file_has "the new release is recorded as unverified" "$tmp/state/current.env" "ITMAS_STATUS=unverified"
expect_file_has "the last verified release is available for rollback" "$tmp/state/previous.env" "ITMAS_TAG=v1.1.0"
expect_file_has "the output points to the rollback command" "$tmp/out" "rollback"
expect_file_has "the failure is reported with its phase" "$log" "failed in phase switch"
expect_file_has "the failure is in the deployment log" "$tmp/state/deploy.log" "failed phase=switch code=4"
expect_not_called "images are not reverted automatically" "up -d --no-build --no-deps frontend"
: > "$log"
run v1.3.0 --yes
expect_rc "a later release after a failed one" 0
expect_file_has "an unverified release does not replace the one to roll back to" "$tmp/state/previous.env" "ITMAS_TAG=v1.1.0"

# Scenario 8: the probe fails.
reset
STUB_PROBE_FAIL=1 && export STUB_PROBE_FAIL
run v1.0.0 --yes
unset STUB_PROBE_FAIL
expect_rc "failed probe" 4
expect_file_has "a failed probe is not recorded as ok" "$tmp/state/current.env" "ITMAS_STATUS=unverified"

# Scenario 9: a published port other than the frontend on 127.0.0.1 stops the deployment before anything starts.
reset
STUB_PORT_HOST=0.0.0.0 && export STUB_PORT_HOST
run v1.0.0 --yes
unset STUB_PORT_HOST
expect_rc "port guard" 2
expect_not_called "nothing is pulled when the guard fails" "pull api"
expect_not_called "nothing is started when the guard fails" " up "
expect_not_called "no migration when the guard fails" "run --rm"

# Scenario 10: the dump fails, and a stopped postgres with data is still dumped (the decision is the volume, not a running container).
reset
STUB_BACKUP_RC=1 && export STUB_BACKUP_RC
run v1.0.0 --yes
unset STUB_BACKUP_RC
expect_rc "pre-deployment dump failure" 1
expect_not_called "nothing is pulled when the dump fails" "pull api"
expect_not_called "no migration when the dump fails" "run --rm"
reset
STUB_POSTGRES_UP=0 && export STUB_POSTGRES_UP
run v1.0.0 --yes
unset STUB_POSTGRES_UP
expect_rc "postgres stopped but its volume exists" 0
expect_order "postgres is started before the dump" "up -d --no-build postgres" "backup --pre-deploy"
expect_called "the data is dumped although postgres was stopped" "backup --pre-deploy"

# Scenario 11: preflight failures happen before anything is checked out, pulled or started.
reset
STUB_NO_DIGEST=1 && export STUB_NO_DIGEST
run v1.0.0 --yes
unset STUB_NO_DIGEST
expect_rc "digest not resolvable" 1
expect_not_called "nothing is pulled without digests" "pull api"
expect_not_called "the clone does not move without digests" "checkout"
reset
STUB_TAG_MISSING=1 && export STUB_TAG_MISSING
run v9.9.9 --yes
unset STUB_TAG_MISSING
expect_rc "tag that does not exist" 1
reset
STUB_NOT_IN_MAIN=1 && export STUB_NOT_IN_MAIN
run v1.0.0 --yes
unset STUB_NOT_IN_MAIN
expect_rc "a tag whose commit is not in origin/main" 1
expect_file_has "the refusal says why" "$tmp/out" "not in origin/main"
expect_not_called "the clone does not move to an unverified commit" "checkout"
expect_not_called "nothing runs for an unverified commit" "pull api"
reset
STUB_TAG_MOVED=1 && export STUB_TAG_MOVED
run v1.0.0 --yes
unset STUB_TAG_MOVED
expect_rc "a tag that moved since it was fetched" 1
expect_not_called "a moved tag is not checked out" "checkout"
reset
STUB_GIT_DIRTY=1 && export STUB_GIT_DIRTY
run v1.0.0 --yes
unset STUB_GIT_DIRTY
expect_rc "clone with local changes" 1
reset
run 1.0.0 --yes
expect_rc "tag without the v" 1
reset
run 'v1.0.0;touch-pwned' --yes
expect_rc "a tag with shell metacharacters" 1
expect_not_called "nothing runs for a malformed tag" "git"
reset
run v1.0.0
expect_rc "confirmation needs a terminal or --yes" 1
expect_not_called "nothing is pulled without confirmation" "pull api"
expect_not_called "the clone does not move before the confirmation" "checkout"

# Scenario 12: locks. A lock held by a live process stops the deployment; one left by a dead process is taken over.
reset
mkdir "$tmp/state/lock"
echo $$ > "$tmp/state/lock/pid"
run v1.0.0 --yes
expect_rc "a deployment already running" 1
expect_not_called "nothing runs under a held lock" "pull api"
reset
mkdir "$tmp/state/lock"
echo 99999999 > "$tmp/state/lock/pid"
run v1.0.0 --yes
expect_rc "a lock left by a process that is gone" 0
expect_missing "the lock is released at the end" "$tmp/state/lock"

# Scenario 13: the packages of the agent. Bad checksum, an unlisted file, an unwritable folder, the highest version rule and the replacement of latest.
reset
STUB_AGENT_BAD_SUM=1 && export STUB_AGENT_BAD_SUM
run v1.0.0 --yes
unset STUB_AGENT_BAD_SUM
expect_rc "agent packages with a bad checksum" 5
expect_missing "a package that fails verification is not published" "$tmp/downloads/$agent_version"
expect_file_has "the release itself is recorded as ok" "$tmp/state/current.env" "ITMAS_STATUS=ok"
checks=$((checks + 1))
[ -z "$(find "$tmp/downloads" -name '.incoming.*' 2> /dev/null)" ] || fail "temporary extraction directory left behind"
reset
STUB_AGENT_EXTRA_FILE=1 && export STUB_AGENT_EXTRA_FILE
run v1.0.0 --yes
unset STUB_AGENT_EXTRA_FILE
expect_rc "an agent image with a file SHA256SUMS does not list" 5
expect_missing "nothing is published when a file is not listed" "$tmp/downloads/$agent_version"
reset
STUB_AGENT_PULL_RC=1 && export STUB_AGENT_PULL_RC
run v1.0.0 --yes
unset STUB_AGENT_PULL_RC
expect_rc "agent image missing" 5
expect_file_has "the release is still recorded" "$tmp/state/current.env" "ITMAS_STATUS=ok"
if [ "$(id -u)" -ne 0 ]; then
  reset
  chmod 555 "$tmp/downloads"
  run v1.0.0 --yes
  chmod 755 "$tmp/downloads"
  expect_rc "an agent folder that cannot be written" 5
  expect_not_called "nothing is extracted when the folder cannot be written" "docker cp"
  expect_not_called "nothing is created when the folder cannot be written" "docker create"
fi
reset
mkdir -p "$tmp/downloads/9.9.9" "$tmp/downloads/latest"
echo "old" > "$tmp/downloads/latest/marker"
run v1.0.0 --yes
expect_rc "an agent version lower than one already published" 0
expect_exists "the version is published" "$tmp/downloads/$agent_version/SHA256SUMS"
expect_exists "latest keeps pointing at the highest version" "$tmp/downloads/latest/marker"
reset
mkdir -p "$tmp/downloads/0.0.1" "$tmp/downloads/latest"
echo "old" > "$tmp/downloads/latest/marker"
run v1.0.0 --yes
expect_rc "an agent version above the published ones" 0
expect_missing "latest is replaced, not merged" "$tmp/downloads/latest/marker"
expect_exists "latest has the new packages" "$tmp/downloads/latest/itmas-agent-linux-x64.tar.gz"
expect_missing "no previous latest is left" "$tmp/downloads/latest.old"

# Scenario 16: a rollback after a failed release leaves nothing to roll back to: the failed one is never offered again.
reset
run v1.0.0 --yes
STUB_HEALTH_API=unhealthy && export STUB_HEALTH_API
run v1.1.0 --yes
unset STUB_HEALTH_API
expect_rc "a release that does not become healthy" 4
run rollback --yes
expect_rc "rollback after a failed release" 0
expect_file_has "the verified release is current again" "$tmp/state/current.env" "ITMAS_TAG=v1.0.0"
expect_file_has "and it is verified" "$tmp/state/current.env" "ITMAS_STATUS=ok"
expect_missing "the failed release is not kept as the one to go back to" "$tmp/state/previous.env"
run rollback --yes
expect_rc "a second rollback has nothing to go back to" 1
expect_file_has "it says so" "$tmp/out" "no previous release"

# Scenario 17: failures before the migrator never reach the running containers.
reset
STUB_PULL_RC=1 && export STUB_PULL_RC
run v1.0.0 --yes
unset STUB_PULL_RC
expect_rc "images that cannot be pulled" 1
expect_not_called "no migration when the images cannot be pulled" "run --rm"
expect_not_called "nothing is switched when the images cannot be pulled" "up -d --no-build --no-deps api"
reset
STUB_CONFIG_FAIL=1 && export STUB_CONFIG_FAIL
run v1.0.0 --yes
unset STUB_CONFIG_FAIL
expect_rc "a Compose configuration that is not valid" 2
expect_not_called "nothing is pulled with an invalid configuration" "pull api"
reset
STUB_HEALTH_POSTGRES=unhealthy && export STUB_HEALTH_POSTGRES
run v1.0.0 --yes
unset STUB_HEALTH_POSTGRES
expect_rc "postgres not healthy before the dump" 1
expect_not_called "no dump of a database that is not healthy" "backup --pre-deploy"
expect_not_called "nothing is pulled when postgres is not healthy" "pull api"
reset
STUB_VOLUME_RC=1 STUB_HEALTH_POSTGRES=unhealthy && export STUB_VOLUME_RC STUB_HEALTH_POSTGRES
run v1.0.0 --yes
unset STUB_VOLUME_RC STUB_HEALTH_POSTGRES
expect_rc "postgres not healthy before the migrator" 3
expect_not_called "the migrator does not run against an unhealthy postgres" "run --rm"
reset
STUB_HEALTH_FRONTEND=unhealthy && export STUB_HEALTH_FRONTEND
run v1.0.0 --yes
unset STUB_HEALTH_FRONTEND
expect_rc "frontend that does not become healthy" 4
expect_called "api was switched first" "up -d --no-build --no-deps api"
expect_file_has "the release is recorded as unverified" "$tmp/state/current.env" "ITMAS_STATUS=unverified"
reset
echo '{"isAuthenticated":true,"user":{"name":"x"}}' > "$tmp/stubdir/probe-body"
run v1.0.0 --yes
expect_rc "a probe that answers as an authenticated session" 4
reset
echo '<html>400 Bad Request</html>' > "$tmp/stubdir/probe-body"
run v1.0.0 --yes
expect_rc "a probe that answers with an error page" 4
expect_file_has "the probe failure is reported with its phase" "$log" "failed in phase probe"

# Scenario 18: the port guard, in every shape of offender, and also before a rollback.
reset
STUB_PORT_EXTRA=1 && export STUB_PORT_EXTRA
run v1.0.0 --yes
unset STUB_PORT_EXTRA
expect_rc "another service publishes a port (even on loopback)" 2
expect_not_called "nothing is pulled when another service publishes a port" "pull api"
reset
STUB_PORT_EXTRA=first && export STUB_PORT_EXTRA
run v1.0.0 --yes
unset STUB_PORT_EXTRA
expect_rc "another service publishes a loopback port before the frontend does" 2
reset
STUB_PORT_EXTRA=1 STUB_NO_FRONTEND_PORTS=1 && export STUB_PORT_EXTRA STUB_NO_FRONTEND_PORTS
run v1.0.0 --yes
unset STUB_PORT_EXTRA STUB_NO_FRONTEND_PORTS
expect_rc "only another service publishes a loopback port: it is not the frontend" 2
reset
STUB_PORT_NOHOST=1 && export STUB_PORT_NOHOST
run v1.0.0 --yes
unset STUB_PORT_NOHOST
expect_rc "a published port without host_ip (all interfaces)" 2
reset
STUB_NO_FRONTEND_PORTS=1 && export STUB_NO_FRONTEND_PORTS
run v1.0.0 --yes
unset STUB_NO_FRONTEND_PORTS
expect_rc "a frontend that publishes nothing" 2
expect_file_has "the message says what is missing" "$tmp/out" "does not publish a port on 127.0.0.1"
reset
run v1.0.0 --yes
run v1.1.0 --yes
: > "$log"
STUB_PORT_HOST=0.0.0.0 && export STUB_PORT_HOST
run rollback --yes
unset STUB_PORT_HOST
expect_rc "the port guard also stops a rollback" 2
expect_not_called "a rollback with a port beyond loopback starts nothing" "up -d --no-build --no-deps api"

# Scenario 19: rollback needs postgres and a healthy result like a deployment does.
reset
run v1.0.0 --yes
run v1.1.0 --yes
: > "$log"
STUB_POSTGRES_UP=0 && export STUB_POSTGRES_UP
rm -f "$tmp/stubdir/postgres-started"
run rollback --yes
unset STUB_POSTGRES_UP
expect_rc "a rollback with postgres stopped" 1
expect_not_called "nothing is switched when postgres is stopped" "up -d --no-build --no-deps api"
: > "$log"
STUB_PULL_RC=1 && export STUB_PULL_RC
run rollback --yes
unset STUB_PULL_RC
expect_rc "a rollback whose images cannot be pulled" 1
expect_file_has "the current release is untouched" "$tmp/state/current.env" "ITMAS_TAG=v1.1.0"
STUB_PROBE_FAIL=1 && export STUB_PROBE_FAIL
run rollback --yes
unset STUB_PROBE_FAIL
expect_rc "a rollback whose probe fails" 4
expect_file_has "a failed rollback does not rewrite the state" "$tmp/state/current.env" "ITMAS_TAG=v1.1.0"

# Scenario 19b: without the test hook, the pre-deployment dump is the real scripts/backup-db.sh (here it stops at the missing host.conf values).
reset
TEST_BACKUP_CMD="" && export TEST_BACKUP_CMD
run v1.0.0 --yes
unset TEST_BACKUP_CMD
expect_rc "the real backup script is what deploy.sh calls" 1
expect_file_has "it is backup-db.sh that refuses" "$tmp/out" "BACKUP_AGE_RECIPIENT"
expect_not_called "nothing is pulled when the real dump fails" "pull api"

# Scenario 20: a variable exported in the environment wins over host.conf (the heartbeat of this run goes to the exported URL).
reset
HEARTBEAT_URL="https://other.example.invalid/ping/env-token-999" && export HEARTBEAT_URL
STUB_MIGRATOR_RC=1 && export STUB_MIGRATOR_RC
run v1.0.0 --yes
unset HEARTBEAT_URL STUB_MIGRATOR_RC
expect_file_has "the exported URL receives the failure" "$pings" "env-token-999"
expect_file_lacks "the URL of host.conf is not used" "$pings" "secret-token-123"
expect_file_has "the failure and its reason are in the deployment log" "$tmp/state/deploy.log" "failed phase=migrator code=3 v1.0.0: the migrator failed"

# Scenario 14: status.
reset
cp "$tmp/after-v1.1.0.env" "$tmp/state/current.env"
cp "$tmp/after-v1.1.0-previous.env" "$tmp/state/previous.env"
run status
expect_rc "status" 0
expect_file_has "status shows the current release" "$tmp/out" "ITMAS_TAG=v1.1.0"
expect_file_has "status shows the previous release" "$tmp/out" "ITMAS_TAG=v1.0.0"

# Scenario 15: the configuration file is refused when it could run someone else's code.
reset
chmod 666 "$tmp/host.conf"
run v1.0.0 --yes
expect_rc "a writable host.conf" 1
expect_file_has "the refusal names the reason" "$tmp/out" "writable by group or others"
chmod 600 "$tmp/host.conf"
if [ "$(id -u)" -ne 0 ]; then
  # an unreadable file reproduces what an administrator without sudo sees
  chmod 000 "$tmp/host.conf"
  run v1.0.0 --yes
  chmod 600 "$tmp/host.conf"
  expect_rc "a host.conf that the user cannot read" 1
  expect_file_has "the message says to use sudo" "$tmp/out" "run this script with sudo"
fi

printf '%d of %d checks passed.\n' "$((checks - failures))" "$checks"
[ "$failures" -eq 0 ]
