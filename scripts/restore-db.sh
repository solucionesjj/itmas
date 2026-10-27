#!/bin/sh
# Restore test of an ITMas backup in a temporary PostgreSQL container (SPEC-0009 RF-5, CA-4, constitution 8.7 f). It never touches the
# production database: it starts its own container from the same image, creates the roles with database/init (as a fresh server does)
# and restores the dump over them in a single transaction. Run it on a workstation with Docker, not on the production server (it
# refuses unless --force). To recover a real server, see DEVELOPMENT.md 12.9: the production database is never restored in place.
#   scripts/restore-db.sh --backup <itmas3-....dump.age> --identity <age identity file> [--api-image <image>] [--migrator-image <image>] [--keep]
#   scripts/restore-db.sh --plain <plain.dump> --counts <counts.txt> [--api-image <image>] [--migrator-image <image>] [--keep]
# The row counts of every table (itmas3-....counts.age next to the dump, or --counts) are compared with the restored database; without
# them the script fails unless --no-counts. It also checks that the runtime role keeps DML only and owns nothing.
# --migrator-image runs that migrator against the restored data (it must end with exit 0: nothing to apply, or only what is new).
# --api-image starts that API image (Development environment, no Entra ID) against the restored data and waits for /health.
# The roles use random passwords: for a real recovery reuse the .env of the server and the same role names.
# Role and database names default to the ones of .env.example; override with RESTORE_POSTGRES_DB, RESTORE_SUPERUSER,
# RESTORE_MIGRATOR_USER and RESTORE_APP_USER if the server .env uses others. The private key of the backups is read here, never copied.
# Exit: 0 restored and counts match, 1 usage or failure, 3 restored but the counts differ, 4 no counts to compare.
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd -P)
cd "$repo"

backup=""
identity=""
plain=""
counts_file=""
api_image=""
migrator_image=""
keep=0
force=0
no_counts=0

usage() {
  sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit 1
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --backup) backup="${2:?--backup needs a file}"; shift 2 ;;
    --identity) identity="${2:?--identity needs a file}"; shift 2 ;;
    --plain) plain="${2:?--plain needs a file}"; shift 2 ;;
    --counts) counts_file="${2:?--counts needs a file}"; shift 2 ;;
    --api-image) api_image="${2:?--api-image needs an image}"; shift 2 ;;
    --migrator-image) migrator_image="${2:?--migrator-image needs an image}"; shift 2 ;;
    --keep) keep=1; shift ;;
    --force) force=1; shift ;;
    --no-counts) no_counts=1; shift ;;
    *) usage ;;
  esac
done
[ -n "$backup" ] || [ -n "$plain" ] || usage
[ -z "$backup" ] || [ -n "$identity" ] || usage

if [ -f "${ITMAS_HOST_CONF:-/etc/itmas3/host.conf}" ] && [ "$force" -ne 1 ]; then
  echo "This looks like the production server: restore tests run on a workstation (use --force to override)." >&2
  exit 1
fi

default_of() { sed -n "s/^$1=//p" .env.example | head -n 1; }
db="${RESTORE_POSTGRES_DB:-$(default_of POSTGRES_DB)}"
superuser="${RESTORE_SUPERUSER:-$(default_of POSTGRES_SUPERUSER)}"
migrator_user="${RESTORE_MIGRATOR_USER:-$(default_of APP_DB_MIGRATOR_USER)}"
app_user="${RESTORE_APP_USER:-$(default_of APP_DB_USER)}"
image="$(sed -n 's/^    image: \(postgres:[^ ]*\)$/\1/p' docker-compose.yml | head -n 1)"
[ -n "$db" ] && [ -n "$superuser" ] && [ -n "$migrator_user" ] && [ -n "$app_user" ] && [ -n "$image" ] || {
  echo "Cannot read the defaults from .env.example and docker-compose.yml." >&2
  exit 1
}

id="itmas3-restore-$$"
tmp="$(mktemp -d)"
started="$(date +%s)"
cleanup() {
  if [ "$keep" -eq 1 ]; then
    echo "Kept: container $id-db and network $id (remove them with docker rm -fv $id-db $id-api; docker network rm $id). Files in $tmp."
    return 0
  fi
  # -v also removes the anonymous data volume: it holds a copy of production data (audit, accounts, key ring).
  docker rm -fv "$id-api" "$id-db" > /dev/null 2>&1 || true
  docker network rm "$id" > /dev/null 2>&1 || true
  rm -rf "$tmp"
}
trap cleanup EXIT
trap 'exit 143' TERM HUP
trap 'exit 130' INT
umask 077

if [ -n "$backup" ]; then
  command -v age > /dev/null 2>&1 || { echo "age is required to decrypt the backup." >&2; exit 1; }
  age -d -i "$identity" -o "$tmp/db.dump" "$backup" || { echo "Cannot decrypt $backup with that identity." >&2; exit 1; }
  if [ -z "$counts_file" ]; then
    candidate="$(printf '%s' "$backup" | sed 's/\.dump\.age$/.counts.age/')"
    if [ -f "$candidate" ] && age -d -i "$identity" -o "$tmp/counts.txt" "$candidate" 2> /dev/null; then
      counts_file="$tmp/counts.txt"
    fi
  fi
else
  cp "$plain" "$tmp/db.dump"
fi
[ -s "$tmp/db.dump" ] || { echo "The dump is empty." >&2; exit 1; }
if [ -z "$counts_file" ] && [ "$no_counts" -ne 1 ]; then
  echo "No row counts to compare: pass --counts (or the .counts.age next to the dump), or --no-counts to skip the comparison." >&2
  exit 4
fi

random() { openssl rand -hex 24; }
POSTGRES_PASSWORD="$(random)"
APP_DB_MIGRATOR_PASSWORD="$(random)"
APP_DB_PASSWORD="$(random)"
export POSTGRES_PASSWORD APP_DB_MIGRATOR_PASSWORD APP_DB_PASSWORD

docker network create "$id" > /dev/null
docker run -d --name "$id-db" --network "$id" \
  -e POSTGRES_DB="$db" -e POSTGRES_USER="$superuser" -e POSTGRES_PASSWORD \
  -e APP_DB_MIGRATOR_USER="$migrator_user" -e APP_DB_MIGRATOR_PASSWORD \
  -e APP_DB_USER="$app_user" -e APP_DB_PASSWORD \
  -v "$repo/database/init:/docker-entrypoint-initdb.d:ro" "$image" > /dev/null

tries=0
until docker exec "$id-db" pg_isready -h 127.0.0.1 -U "$superuser" -d "$db" > /dev/null 2>&1; do
  tries=$((tries + 1))
  if [ "$tries" -ge 90 ]; then
    echo "PostgreSQL did not become ready." >&2
    docker logs "$id-db" >&2 || true
    exit 1
  fi
  sleep 1
done

# One transaction: if anything fails, nothing is half restored (--clean alone drops first and can leave the database empty).
echo "Restoring (roles created by database/init, then pg_restore --single-transaction --clean --if-exists)..."
docker exec -i "$id-db" pg_restore -U "$superuser" -d "$db" --single-transaction --clean --if-exists --exit-on-error < "$tmp/db.dump" || {
  echo "pg_restore failed (nothing was restored: the transaction was rolled back)." >&2
  exit 1
}

docker exec -i "$id-db" psql -At -U "$superuser" -d "$db" > "$tmp/restored-counts-raw.txt" << 'SQL'
SELECT format('%I.%I', table_schema, table_name) || '=' || (xpath('/row/c/text()',
       query_to_xml(format('select count(*) as c from %I.%I', table_schema, table_name), false, true, '')))[1]::text
FROM information_schema.tables
WHERE table_schema IN ('app', 'app_migrations') AND table_type = 'BASE TABLE';
SQL
sort "$tmp/restored-counts-raw.txt" > "$tmp/restored-counts.txt"
echo "Restored $(wc -l < "$tmp/restored-counts.txt" | tr -d ' ') tables (schemas app and app_migrations)."

# The runtime role of the API keeps DML and nothing else: no DDL, no TRUNCATE, no ownership, no access to the migration history.
privileges="$(docker exec -i "$id-db" psql -At -U "$superuser" -d "$db" -v app_role="$app_user" -v migrator_role="$migrator_user" \
  < "$repo/scripts/lib/restore-assertions.sql")"
if [ "$privileges" = "t" ]; then
  echo "The runtime role $app_user keeps DML only: no DDL, no TRUNCATE, no ownership, no access to the migration history."
else
  echo "The runtime role $app_user does not have the expected privileges after the restore (ACLs were not restored as in production)." >&2
  exit 1
fi

result=0
if [ -n "$counts_file" ]; then
  # Every table counts, empty ones too: a table missing after the restore must show up as a difference.
  sort "$counts_file" > "$tmp/expected-counts.txt"
  if diff "$tmp/expected-counts.txt" "$tmp/restored-counts.txt" > "$tmp/diff.txt"; then
    echo "Row counts of every table match the dump (including the migration history)."
  else
    echo "Row counts differ from the dump:"
    cat "$tmp/diff.txt"
    result=3
  fi
fi

if [ -n "$migrator_image" ]; then
  echo "Running the migrator against the restored data..."
  # The password reaches docker through the environment of the command, not through its arguments (constitution 4.5).
  if Database__Password="$APP_DB_MIGRATOR_PASSWORD" docker run --rm --network "$id" \
    -e Database__Host="$id-db" -e Database__Port=5432 -e Database__Name="$db" -e Database__Username="$migrator_user" \
    -e Database__Password -e Database__SslMode=Disable "$migrator_image" > "$tmp/migrator.out" 2>&1; then
    echo "The migrator finished without errors against the restored data."
  else
    echo "The migrator failed against the restored data:" >&2
    tail -n 15 "$tmp/migrator.out" >&2
    exit 1
  fi
fi

if [ -n "$api_image" ]; then
  echo "Starting the API image against the restored data..."
  Database__Password="$APP_DB_PASSWORD" docker run -d --name "$id-api" --network "$id" \
    -e ASPNETCORE_ENVIRONMENT=Development -e AllowedHosts=localhost \
    -e Database__Host="$id-db" -e Database__Port=5432 -e Database__Name="$db" -e Database__Username="$app_user" \
    -e Database__Password -e Database__SslMode=Disable "$api_image" > /dev/null
  tries=0
  until docker exec "$id-api" wget -qO- --header "Host: localhost" http://localhost:8080/health > /dev/null 2>&1; do
    tries=$((tries + 1))
    if [ "$tries" -ge 60 ]; then
      echo "The API did not become healthy against the restored data." >&2
      docker logs "$id-api" 2>&1 | tail -n 20 >&2 || true
      exit 1
    fi
    sleep 2
  done
  echo "The API is healthy against the restored data."
fi

echo "Restore test finished in $(($(date +%s) - started)) seconds (no data or secrets were printed)."
exit "$result"
