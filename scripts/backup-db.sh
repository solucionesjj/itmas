#!/bin/sh
# Dumps the PostgreSQL database of ITMas, encrypts the dump and copies it off the server (SPEC-0009 RF-1 to RF-4, constitution 8.7 f).
# Runs as root from a systemd timer (deploy/systemd/itmas3-backup.timer) and, with --pre-deploy, from scripts/deploy.sh.
#   scripts/backup-db.sh [--pre-deploy]
# What it does, in order: checks there is room for the dump -> pg_dump -Fc inside the postgres container -> reads the whole dump back with
# pg_restore (a truncated or corrupt dump fails here) -> counts the rows of every table FROM THE DUMP (so the counts cannot race with
# writers) for the restore test -> encrypts with age using the PUBLIC key of BACKUP_AGE_RECIPIENT (the private key is never on this
# server) -> copies every encrypted file that has not been copied yet with rclone (a copy that failed yesterday is retried today) ->
# deletes local copies of BACKUP_KEEP_DAYS days or more. The plaintext dump exists only in a private temporary directory and is
# removed in every case; one left by a killed run is removed at the next start.
# The credential for the destination is not stored: rclone uses the EC2 instance role (RCLONE_S3_ENV_AUTH=true in host.conf).
# Configuration: /etc/itmas3/host.conf (template deploy/host.conf.example). Exit: 0 ok, 1 failure (a failure ping is sent).
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd -P)
cd "$repo"
# shellcheck disable=SC1091
. "$repo/scripts/lib/itmas-host.sh"
itmas_load_conf

: "${BACKUP_DIR:=/var/backups/itmas3}"
: "${BACKUP_KEEP_DAYS:=7}"

label=""
case "${1:-}" in
  "") ;;
  --pre-deploy) label="-pre-deploy" ;;
  *)
    echo "Usage: backup-db.sh [--pre-deploy]" >&2
    exit 1
    ;;
esac

tmpdir=""
lock_held=0
cleanup() {
  [ -z "$tmpdir" ] || rm -rf "$tmpdir"
  [ "$lock_held" -eq 0 ] || itmas_unlock "$BACKUP_DIR/.lock"
}
trap cleanup EXIT
trap 'exit 143' TERM HUP
trap 'exit 130' INT

fail() {
  itmas_err "ERROR: $*"
  itmas_notify_fail "backup failed: $*"
  exit 1
}

[ -n "${BACKUP_AGE_RECIPIENT:-}" ] || fail "BACKUP_AGE_RECIPIENT (the age public key) is not set in $ITMAS_HOST_CONF"
[ -n "${BACKUP_REMOTE:-}" ] || fail "BACKUP_REMOTE (the rclone destination) is not set in $ITMAS_HOST_CONF"

umask 077
# rclone needs no configuration file: the destination is a connection string and the credentials come from the instance role.
export RCLONE_CONFIG="${RCLONE_CONFIG:-/dev/null}"
mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"
itmas_lock "$BACKUP_DIR/.lock" || fail "another backup is running (lock $BACKUP_DIR/.lock)"
lock_held=1
# Plaintext leftovers of a run that was killed (kill -9, out of memory, power cut).
find "$BACKUP_DIR" -maxdepth 1 -name '.tmp.*' -mmin +60 -exec rm -rf {} + 2> /dev/null || true
tmpdir="$(mktemp -d "$BACKUP_DIR/.tmp.XXXXXX")"

cid="$(docker compose -f docker-compose.yml ps -q postgres 2> /dev/null || true)"
[ -n "$cid" ] || fail "the postgres container is not running"

# The plaintext dump and the encrypted copy share the file system of the data: do not let a backup fill the disk PostgreSQL needs.
size="$(docker exec "$cid" sh -c 'psql -At -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "select pg_database_size(current_database())"' 2> /dev/null || true)"
case "$size" in "" | *[!0-9]*) fail "cannot read the size of the database" ;; esac
avail="$(df -Pk "$BACKUP_DIR" | awk 'NR == 2 { printf "%.0f", $4 * 1024 }')"
[ "$avail" -gt $((size * 2)) ] || fail "not enough free space in $BACKUP_DIR for the dump ($avail bytes free, database of $size bytes)"

stamp="$(date -u +%Y%m%d-%H%M%S)"
name="itmas3-$stamp$label.dump.age"
counts_name="itmas3-$stamp$label.counts.age"

# The credentials are those of the container's own environment: nothing goes through the command line.
docker exec "$cid" sh -c 'pg_dump -Fc -U "$POSTGRES_USER" -d "$POSTGRES_DB"' > "$tmpdir/plain.dump" || fail "pg_dump failed"
[ -s "$tmpdir/plain.dump" ] || fail "the dump is empty"
docker exec -i "$cid" pg_restore --list < "$tmpdir/plain.dump" > "$tmpdir/toc.txt" 2> /dev/null || fail "the dump cannot be read back with pg_restore --list"
[ "$(wc -l < "$tmpdir/toc.txt")" -ge 5 ] || fail "the table of contents of the dump is empty or too short"
# --list stops at the table of contents; reading all the data is what detects a truncated or corrupt dump.
docker exec -i "$cid" pg_restore -f /dev/null < "$tmpdir/plain.dump" 2> /dev/null || fail "the dump is truncated or corrupt (pg_restore cannot read all of it)"

# Rows per table, counted from the dump itself ("schema.table=rows"): restore-db.sh compares them with the restored database.
docker exec -i "$cid" pg_restore -f - < "$tmpdir/plain.dump" 2> /dev/null | awk '
  /^COPY / { line = $0; sub(/^COPY /, "", line); sub(/ \(.*$/, "", line); table = line; rows = 0; copying = 1; next }
  copying && /^\\\.$/ { print table "=" rows; copying = 0; next }
  copying { rows++ }
' | sort > "$tmpdir/counts.txt"
[ -s "$tmpdir/counts.txt" ] || fail "no table data found in the dump"

age -r "$BACKUP_AGE_RECIPIENT" -o "$BACKUP_DIR/$name" "$tmpdir/plain.dump" || fail "age failed to encrypt the dump"
age -r "$BACKUP_AGE_RECIPIENT" -o "$BACKUP_DIR/$counts_name" "$tmpdir/counts.txt" || fail "age failed to encrypt the row counts"
rm -rf "$tmpdir"
tmpdir=""

# Copy every encrypted file that has no .uploaded marker yet, oldest first: yesterday's failed copy is retried today. Append-only
# credentials cannot read or list the destination, so rclone must not look at it before writing.
failed_uploads=""
for file in $(find "$BACKUP_DIR" -maxdepth 1 -type f \( -name 'itmas3-*.dump.age' -o -name 'itmas3-*.counts.age' \) | sed 's#.*/##' | sort); do
  [ ! -e "$BACKUP_DIR/$file.uploaded" ] || continue
  # shellcheck disable=SC2086
  if rclone copyto --no-check-dest --s3-no-check-bucket --s3-no-head ${BACKUP_RCLONE_FLAGS:-} "$BACKUP_DIR/$file" "${BACKUP_REMOTE%/}/$file"; then
    touch "$BACKUP_DIR/$file.uploaded"
  else
    failed_uploads="$failed_uploads $file"
  fi
done
[ -z "$failed_uploads" ] || fail "the external copy of$failed_uploads failed (the encrypted files stay in $BACKUP_DIR and are retried at the next run)"

# Retention: only this script's files, only BACKUP_KEEP_DAYS days old or more; the destination applies its own rule.
find "$BACKUP_DIR" -maxdepth 1 -type f \( -name 'itmas3-*.dump.age' -o -name 'itmas3-*.counts.age' -o -name 'itmas3-*.age.uploaded' \) \
  -mmin +"$((BACKUP_KEEP_DAYS * 1440))" -delete

itmas_log "Backup $name copied to the external destination."
