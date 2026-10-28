#!/bin/sh
# Tests for scripts/backup-db.sh and scripts/restore-db.sh (SPEC-0009 CA-1, CA-2, CA-3, CA-4).
# Part 1 replaces docker, age, rclone, curl and df with stubs: no Docker, network, destination or .env needed.
# Part 2 runs the real scripts end to end against a throwaway PostgreSQL container: backup-db.sh (real pg_dump, pg_restore and row
# counts) -> restore-db.sh (real restore in another throwaway container). Only age (pass-through unless installed), rclone (copies to a
# folder) and curl are stubs. It needs Docker and is skipped, and reported, when Docker is not running.
set -eu

root=$(cd "$(dirname "$0")/.." && pwd)
tmp=$(mktemp -d)
bin="$tmp/bin"
mkdir -p "$bin" "$tmp/plain"
log="$tmp/calls.log"
pings="$tmp/pings.log"
backups="$tmp/backups"
checks=0
failures=0
skipped=0
rc=0

cleanup() {
  [ -z "${id:-}" ] || docker rm -fv "$id-src" > /dev/null 2>&1 || true
  [ -z "${id:-}" ] || docker network rm "$id" > /dev/null 2>&1 || true
  rm -rf "$tmp"
}
trap cleanup EXIT
trap 'exit 143' TERM HUP
trap 'exit 130' INT

cat > "$bin/docker" << 'EOF'
#!/bin/sh
echo "docker $*" >> "$STUB_LOG"
case "$*" in
  *"ps -q postgres"*) [ "${STUB_PG_DOWN:-0}" = 1 ] || echo "cid-pg" ;;
  *pg_database_size*)
    [ "${STUB_SIZE_FAIL:-0}" = 1 ] && exit 1
    echo "${STUB_DB_SIZE:-1000000}"
    ;;
  *pg_dump*)
    [ "${STUB_DUMP_FAIL:-0}" = 1 ] && exit 1
    [ "${STUB_DUMP_EMPTY:-0}" = 1 ] || echo "PGDMP-stub-contents"
    ;;
  *"pg_restore --list"*)
    cat > /dev/null
    [ "${STUB_TOC_BAD:-0}" = 1 ] && exit 1
    i=0
    while [ "$i" -lt "${STUB_TOC_LINES:-8}" ]; do echo "; toc entry $i"; i=$((i + 1)); done
    ;;
  *"pg_restore -f /dev/null"*)
    cat > /dev/null
    [ "${STUB_READ_FAIL:-0}" = 1 ] && exit 1
    ;;
  *"pg_restore -f -"*)
    cat > /dev/null
    [ "${STUB_COPY_EMPTY:-0}" = 1 ] && exit 0
    # the text format of pg_restore: one COPY block per table that has rows; the SQL around it is not data
    printf 'SET statement_timeout = 0;\n\nCOPY app.devices (id, name) FROM stdin;\n1\ta\n2\tb\n3\tc\n\\.\n\n'
    printf 'COPY app.users (id) FROM stdin;\n1\n2\n\\.\n\nCOPY app_migrations.__ef_migrations_history (id) FROM stdin;\n1\n\\.\n'
    ;;
esac
exit 0
EOF
cat > "$bin/age" << 'EOF'
#!/bin/sh
echo "age $*" >> "$STUB_LOG"
[ "${STUB_AGE_FAIL:-0}" = 1 ] && exit 1
recipient=""
out=""
while [ "$#" -gt 1 ]; do
  case "$1" in -r) recipient="$2"; shift 2 ;; -o) out="$2"; shift 2 ;; *) shift ;; esac
done
printf 'AGE-ENCRYPTED for %s\n' "$recipient" > "$out"
od -An -tx1 < "$1" >> "$out"
# the plaintext of every file the script encrypts, so that the tests can read the row counts
cp "$1" "$STUB_PLAIN/$(basename "$out")"
exit 0
EOF
cat > "$bin/rclone" << 'EOF'
#!/bin/sh
echo "rclone $*" >> "$STUB_LOG"
# the plaintext dump must be gone before anything is copied off the server
if ls -d "$STUB_BACKUPS"/.tmp.* > /dev/null 2>&1; then echo "PLAINTEXT-PRESENT-DURING-COPY" >> "$STUB_LOG"; fi
exit "${STUB_RCLONE_RC:-0}"
EOF
cat > "$bin/curl" << 'EOF'
#!/bin/sh
echo "curl $*" >> "$STUB_LOG"
cat >> "$STUB_PINGS"
exit 0
EOF
cat > "$bin/df" << 'EOF'
#!/bin/sh
printf 'Filesystem 1024-blocks Used Available Capacity Mounted on\nstub 1000 1 %s 1%% /\n' "${STUB_DF_AVAIL_KB:-100000000}"
EOF
chmod +x "$bin"/*

fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$1" >&2
}

reset() {
  rm -rf "$backups" "$tmp/plain"
  mkdir -p "$tmp/plain"
  : > "$log"
  : > "$pings"
  {
    echo "BACKUP_DIR=$backups"
    echo "BACKUP_AGE_RECIPIENT=age1testrecipient"
    echo "BACKUP_REMOTE=:s3:bucket-test/prefix"
    echo "HEARTBEAT_URL=https://monitor.example.invalid/ping/secret-token-456"
  } > "$tmp/host.conf"
  chmod 600 "$tmp/host.conf"
}

run_backup() {
  rc=0
  PATH="$bin:$PATH" STUB_LOG="$log" STUB_PINGS="$pings" STUB_PLAIN="$tmp/plain" STUB_BACKUPS="$backups" ITMAS_HOST_CONF="$tmp/host.conf" \
    sh "$root/scripts/backup-db.sh" "$@" > "$tmp/out" 2>&1 || rc=$?
}

expect_rc() {
  checks=$((checks + 1))
  [ "$rc" -eq "$2" ] || fail "$1: exit status $rc, expected $2 (output: $(tr '\n' ' ' < "$tmp/out" | cut -c1-300))"
}

expect_called() {
  checks=$((checks + 1))
  grep -qF -- "$2" "$log" || fail "$1: '$2' was not called"
}

expect_not_called() {
  checks=$((checks + 1))
  if grep -qF -- "$2" "$log"; then fail "$1: '$2' was called"; fi
}

expect_count() { # description, glob in $backups, expected number of files
  checks=$((checks + 1))
  n=$(find "$backups" -maxdepth 1 -name "$2" 2> /dev/null | wc -l | tr -d ' ')
  [ "$n" -eq "$3" ] || fail "$1: found $n files $2, expected $3"
}

expect_exists() {
  checks=$((checks + 1))
  [ -e "$2" ] || fail "$1: $2 does not exist"
}

expect_missing() {
  checks=$((checks + 1))
  [ ! -e "$2" ] || fail "$1: $2 exists"
}

expect_file_has() {
  checks=$((checks + 1))
  grep -qF -- "$3" "$2" 2> /dev/null || fail "$1: '$3' not found in $2"
}

expect_file_lacks() {
  checks=$((checks + 1))
  if grep -qF -- "$3" "$2" 2> /dev/null; then fail "$1: '$3' found in $2"; fi
}

# days_ago <n> -> a touch -t timestamp (local time) n days in the past; BSD date first, then GNU date.
days_ago() {
  date -v-"$1"d +%Y%m%d%H%M 2> /dev/null || date -d "$1 days ago" +%Y%m%d%H%M
}

# Part 1 - scenario 1: a daily backup.
reset
run_backup
expect_rc "daily backup" 0
expect_count "one encrypted dump is kept" 'itmas3-*.dump.age' 1
expect_count "one encrypted count file is kept" 'itmas3-*.counts.age' 1
expect_count "no plaintext dump is left in the backup directory" '*.dump' 0
expect_count "no temporary directory is left" '.tmp.*' 0
expect_called "age uses the PUBLIC key" "age -r age1testrecipient -o $backups/itmas3-"
expect_called "pg_dump runs inside the container" "pg_dump"
expect_called "the dump is verified with pg_restore --list" "pg_restore --list"
expect_called "the whole dump is read back" "pg_restore -f /dev/null"
expect_not_called "the plaintext dump is removed before the copy leaves the server" "PLAINTEXT-PRESENT-DURING-COPY"
expect_called "the destination is not read before writing" "rclone copyto --no-check-dest --s3-no-check-bucket --s3-no-head $backups/itmas3-"
expect_called "the destination is the configured one" ":s3:bucket-test/prefix/itmas3-"
expect_count "every encrypted file has an upload marker" 'itmas3-*.age.uploaded' 2
expect_missing "the old single marker is gone" "$backups/.last-upload-ok"
expect_missing "the lock is released" "$backups/.lock"
checks=$((checks + 1))
[ -n "$(find "$backups" -maxdepth 0 -perm 700)" ] || fail "the backup directory must be mode 700"
checks=$((checks + 1))
[ -z "$(find "$backups" -type f \( -perm -040 -o -perm -004 \) 2> /dev/null)" ] || fail "backup files must not be readable by group or others"
expect_file_has "the file in the backup directory is the output of age for the public key" "$(find "$backups" -name 'itmas3-*.dump.age' | head -n 1)" "AGE-ENCRYPTED for age1testrecipient"
expect_file_lacks "the secret heartbeat URL never appears in a command line" "$log" "secret-token-456"
expect_file_lacks "a successful backup sends no failure ping" "$pings" "secret-token-456"
counts="$tmp/plain/$(find "$backups" -name 'itmas3-*.counts.age' -exec basename {} \; | head -n 1)"
expect_file_has "the counts are schema.table=rows, computed from the dump" "$counts" "app.devices=3"
expect_file_has "a table with fewer rows is counted separately" "$counts" "app.users=2"
expect_file_has "the migration history is counted too" "$counts" "app_migrations.__ef_migrations_history=1"
expect_file_lacks "SQL that is not table data is not counted" "$counts" "SET"

# Scenario 2: --pre-deploy labels the file; retention removes only this script's old files, by age in minutes.
reset
mkdir -p "$backups"
touch -t 202001010000 "$backups/itmas3-20200101-000000.dump.age" "$backups/itmas3-20200101-000000.counts.age" \
  "$backups/itmas3-20200101-000000.dump.age.uploaded" "$backups/notes-old.txt"
touch -t "$(days_ago 8)" "$backups/itmas3-eight-days.dump.age"
touch -t "$(days_ago 6)" "$backups/itmas3-six-days.dump.age"
touch "$backups/itmas3-recent.dump.age"
run_backup --pre-deploy
expect_rc "pre-deployment backup" 0
expect_count "the file name says pre-deploy" 'itmas3-*-pre-deploy.dump.age' 1
expect_missing "an old encrypted dump is deleted" "$backups/itmas3-20200101-000000.dump.age"
expect_missing "an old count file is deleted" "$backups/itmas3-20200101-000000.counts.age"
expect_missing "an old upload marker is deleted" "$backups/itmas3-20200101-000000.dump.age.uploaded"
expect_missing "a dump older than the retention is deleted" "$backups/itmas3-eight-days.dump.age"
expect_exists "a dump younger than the retention is kept" "$backups/itmas3-six-days.dump.age"
expect_exists "a recent encrypted dump is kept" "$backups/itmas3-recent.dump.age"
expect_exists "an unrelated old file is never deleted" "$backups/notes-old.txt"

# Scenario 3: a copy that failed yesterday is retried today; what is already copied is not copied again.
reset
mkdir -p "$backups"
echo "yesterday" > "$backups/itmas3-20260101-000000.dump.age"
echo "uploaded" > "$backups/itmas3-20260102-000000.dump.age"
touch "$backups/itmas3-20260102-000000.dump.age.uploaded"
run_backup
expect_rc "retry of a copy that failed before" 0
expect_called "the file that was never copied is copied now" "itmas3-20260101-000000.dump.age"
expect_exists "and it gets its marker" "$backups/itmas3-20260101-000000.dump.age.uploaded"
expect_not_called "a file that is already copied is not copied again" ":s3:bucket-test/prefix/itmas3-20260102-000000.dump.age"

# Scenario 4: failures stop before anything leaves the server, leave no plaintext and ping a failure.
reset
STUB_DUMP_FAIL=1 && export STUB_DUMP_FAIL
run_backup
unset STUB_DUMP_FAIL
expect_rc "pg_dump failure" 1
expect_not_called "nothing is copied when the dump fails" "rclone"
expect_count "no plaintext after a failed dump" '.tmp.*' 0
expect_file_has "the failure is reported" "$pings" "secret-token-456"
expect_file_has "the failure ping goes to /fail" "$pings" "/fail"
expect_file_has "the failure ping says what failed" "$log" "backup failed: pg_dump failed"
expect_missing "the lock is released after a failure" "$backups/.lock"
reset
STUB_DUMP_EMPTY=1 && export STUB_DUMP_EMPTY
run_backup
unset STUB_DUMP_EMPTY
expect_rc "empty dump" 1
expect_not_called "an empty dump is not encrypted" "age -r"
expect_not_called "an empty dump is not copied" "rclone"
reset
STUB_TOC_BAD=1 && export STUB_TOC_BAD
run_backup
unset STUB_TOC_BAD
expect_rc "unreadable dump" 1
expect_not_called "an unreadable dump is not copied" "rclone"
reset
STUB_TOC_LINES=1 && export STUB_TOC_LINES
run_backup
unset STUB_TOC_LINES
expect_rc "dump with an empty table of contents" 1
expect_not_called "a dump without contents is not copied" "rclone"
reset
STUB_READ_FAIL=1 && export STUB_READ_FAIL
run_backup
unset STUB_READ_FAIL
expect_rc "dump whose data is truncated (the table of contents reads fine)" 1
expect_file_has "the message says the dump is truncated or corrupt" "$tmp/out" "truncated or corrupt"
expect_not_called "a truncated dump is not encrypted" "age -r"
expect_not_called "a truncated dump is not copied" "rclone"
reset
STUB_COPY_EMPTY=1 && export STUB_COPY_EMPTY
run_backup
unset STUB_COPY_EMPTY
expect_rc "a dump with no table data" 1
expect_not_called "a dump without row counts is not copied" "rclone"
reset
STUB_RCLONE_RC=1 && export STUB_RCLONE_RC
run_backup
unset STUB_RCLONE_RC
expect_rc "external copy failure" 1
expect_count "the encrypted dump stays on the server after a failed copy" 'itmas3-*.dump.age' 1
expect_count "no upload marker is set after a failed copy" 'itmas3-*.age.uploaded' 0
expect_file_has "the failed copy is reported" "$pings" "/fail"
reset
STUB_AGE_FAIL=1 && export STUB_AGE_FAIL
run_backup
unset STUB_AGE_FAIL
expect_rc "encryption failure" 1
expect_not_called "nothing is copied when encryption fails" "rclone"
expect_count "no plaintext after an encryption failure" '.tmp.*' 0
reset
STUB_PG_DOWN=1 && export STUB_PG_DOWN
run_backup
unset STUB_PG_DOWN
expect_rc "postgres not running" 1
reset
sed -i.bak '/BACKUP_AGE_RECIPIENT/d' "$tmp/host.conf" && rm -f "$tmp/host.conf.bak"
run_backup
expect_rc "missing public key" 1
expect_not_called "nothing is dumped without a public key" "pg_dump"
reset
sed -i.bak '/BACKUP_REMOTE/d' "$tmp/host.conf" && rm -f "$tmp/host.conf.bak"
run_backup
expect_rc "missing destination" 1
expect_not_called "nothing is dumped without a destination" "pg_dump"
reset
chmod 666 "$tmp/host.conf"
run_backup
expect_rc "a writable host.conf" 1
expect_not_called "nothing runs with a configuration file that others can write" "pg_dump"
run_backup --unknown-option
expect_rc "an unknown option" 1

# Scenario 5: free space. The plaintext dump and the encrypted copy must not fill the disk PostgreSQL needs.
reset
STUB_DF_AVAIL_KB=1 && export STUB_DF_AVAIL_KB
run_backup
unset STUB_DF_AVAIL_KB
expect_rc "not enough free space" 1
expect_file_has "the message says how much room is missing" "$tmp/out" "not enough free space"
expect_not_called "nothing is dumped without room for it" "pg_dump"
# the room needed is twice the database (the plaintext dump and the encrypted copy): 1.5 MB is not enough for 1 MB, 2.1 MB is
reset
STUB_DF_AVAIL_KB=1500 && export STUB_DF_AVAIL_KB
run_backup
unset STUB_DF_AVAIL_KB
expect_rc "room for the dump but not for the dump and its copy" 1
reset
STUB_DF_AVAIL_KB=2100 && export STUB_DF_AVAIL_KB
run_backup
unset STUB_DF_AVAIL_KB
expect_rc "room for the dump and its copy" 0
reset
STUB_SIZE_FAIL=1 && export STUB_SIZE_FAIL
run_backup
unset STUB_SIZE_FAIL
expect_rc "the size of the database cannot be read" 1
expect_not_called "nothing is dumped when the size is unknown" "pg_dump"

# Scenario 6: locks and leftovers. A lock held by a live process stops the backup; one left by a dead process is taken over;
# the plaintext left by a killed run is removed at the next start.
reset
mkdir -p "$backups/.lock"
echo $$ > "$backups/.lock/pid"
run_backup
expect_rc "a backup already running" 1
expect_not_called "nothing runs under a held lock" "pg_dump"
expect_exists "the lock of the other backup is not touched" "$backups/.lock/pid"
reset
mkdir -p "$backups/.lock"
echo 99999999 > "$backups/.lock/pid"
run_backup
expect_rc "a lock left by a process that is gone" 0
expect_missing "the lock is released at the end" "$backups/.lock"
reset
mkdir -p "$backups/.tmp.killed"
echo "PGDMP-leftover-plaintext" > "$backups/.tmp.killed/plain.dump"
touch -t 202001010000 "$backups/.tmp.killed" "$backups/.tmp.killed/plain.dump"
run_backup
expect_rc "a backup after a killed one" 0
expect_missing "plaintext left by a killed run is removed" "$backups/.tmp.killed"

# Part 2 - the real scripts end to end: backup-db.sh against a throwaway database, restore-db.sh into another one.
if docker info > /dev/null 2>&1; then
  real_docker="$(command -v docker)"
  default_of() { sed -n "s/^$1=//p" "$root/.env.example" | head -n 1; }
  db="$(default_of POSTGRES_DB)"
  superuser="$(default_of POSTGRES_SUPERUSER)"
  migrator="$(default_of APP_DB_MIGRATOR_USER)"
  appuser="$(default_of APP_DB_USER)"
  image="$(sed -n 's/^    image: \(postgres:[^ ]*\)$/\1/p' "$root/docker-compose.yml" | head -n 1)"
  id="itmas3-backup-test-$$"
  POSTGRES_PASSWORD="$(openssl rand -hex 24)"
  APP_DB_MIGRATOR_PASSWORD="$(openssl rand -hex 24)"
  APP_DB_PASSWORD="$(openssl rand -hex 24)"
  export POSTGRES_PASSWORD APP_DB_MIGRATOR_PASSWORD APP_DB_PASSWORD
  docker network create "$id" > /dev/null
  docker run -d --name "$id-src" --network "$id" -e POSTGRES_DB="$db" -e POSTGRES_USER="$superuser" -e POSTGRES_PASSWORD \
    -e APP_DB_MIGRATOR_USER="$migrator" -e APP_DB_MIGRATOR_PASSWORD -e APP_DB_USER="$appuser" -e APP_DB_PASSWORD \
    -v "$root/database/init:/docker-entrypoint-initdb.d:ro" "$image" > /dev/null
  tries=0
  until docker exec "$id-src" pg_isready -h 127.0.0.1 -U "$superuser" -d "$db" > /dev/null 2>&1; do
    tries=$((tries + 1))
    [ "$tries" -lt 90 ] || { echo "FAIL: the source PostgreSQL did not start" >&2; exit 1; }
    sleep 1
  done
  # Data created by the migrator role, as the real migrations do, so that the default privileges of the runtime role apply.
  docker exec -i "$id-src" psql -v ON_ERROR_STOP=1 -v appuser="$appuser" -q -U "$migrator" -d "$db" > /dev/null << 'SQL'
CREATE TABLE app.sample (id integer PRIMARY KEY, note text NOT NULL);
INSERT INTO app.sample SELECT g, 'row ' || g FROM generate_series(1, 25) AS g;
CREATE TABLE app.empty_table (id integer PRIMARY KEY);
CREATE TABLE app.audit_events (id integer PRIMARY KEY);
REVOKE UPDATE, DELETE ON app.audit_events FROM :"appuser";
CREATE TABLE app_migrations.__ef_migrations_history (id text PRIMARY KEY);
INSERT INTO app_migrations.__ef_migrations_history VALUES ('20260101000000_Initial');
SQL

  # What the real scripts need besides Docker: age (pass-through unless installed), rclone (copies to a folder) and curl (a stub).
  realbin="$tmp/realbin"
  remote="$tmp/remote"
  mkdir -p "$realbin" "$remote"
  cat > "$realbin/docker" << EOF
#!/bin/sh
# backup-db.sh asks Compose for the postgres container: the throwaway source is the answer. Everything else is real Docker.
case "\$*" in *"ps -q postgres"*) echo "$id-src"; exit 0 ;; esac
exec "$real_docker" "\$@"
EOF
  cat > "$realbin/rclone" << 'EOF'
#!/bin/sh
# copyto [flags] <source> <destination>: the destination is a folder here
prev=""
src=""
for arg; do
  src="$prev"
  prev="$arg"
done
cp "$src" "$prev"
EOF
  cat > "$realbin/curl" << 'EOF'
#!/bin/sh
cat > /dev/null
EOF
  recipient="age1pass-through"
  identity="$tmp/identity.txt"
  if command -v age > /dev/null 2>&1 && command -v age-keygen > /dev/null 2>&1; then
    # age-keygen refuses to write over an existing file, so the identity must not be created beforehand.
    age-keygen -o "$identity" 2> "$tmp/age-keygen.err" || { cat "$tmp/age-keygen.err" >&2; echo "FAIL: age-keygen could not create the test identity" >&2; exit 1; }
    recipient="$(age-keygen -y "$identity")"
    real_age=1
  else
    : > "$identity"
    real_age=0
    cat > "$realbin/age" << 'EOF'
#!/bin/sh
# pass-through: only the plumbing of the scripts is tested here; the encryption itself is age's
out=""
while [ "$#" -gt 1 ]; do
  case "$1" in -o) out="$2"; shift 2 ;; *) shift ;; esac
done
cp "$1" "$out"
EOF
  fi
  chmod +x "$realbin"/*
  real_backups="$tmp/real-backups"
  {
    echo "BACKUP_DIR=$real_backups"
    echo "BACKUP_AGE_RECIPIENT=$recipient"
    echo "BACKUP_REMOTE=$remote"
  } > "$tmp/real-host.conf"
  chmod 600 "$tmp/real-host.conf"

  rc=0
  PATH="$realbin:$PATH" ITMAS_HOST_CONF="$tmp/real-host.conf" sh "$root/scripts/backup-db.sh" > "$tmp/real-backup.out" 2>&1 || rc=$?
  expect_rc "real backup of a throwaway database" 0
  real_dump="$(find "$remote" -name 'itmas3-*.dump.age' | head -n 1)"
  checks=$((checks + 1))
  [ -n "$real_dump" ] || fail "the real backup copied no dump to the destination"
  expect_file_lacks "the real backup never prints a password" "$tmp/real-backup.out" "$POSTGRES_PASSWORD"
  real_counts="$(printf '%s' "$real_dump" | sed 's/\.dump\.age$/.counts.age/')"
  expect_exists "the counts travel with the dump" "$real_counts"
  if [ "$real_age" -eq 0 ]; then
    expect_file_has "the row counts are taken from the real dump" "$real_counts" "app.sample=25"
    expect_file_has "the migration history is counted" "$real_counts" "app_migrations.__ef_migrations_history=1"
    expect_file_has "a table without rows counts as zero" "$real_counts" "app.empty_table=0"
  fi

  # restore <output name> <restore-db.sh arguments...>: runs the real restore against a throwaway container; $rc and $tmp/out are the result.
  restore() {
    restore_out="$tmp/$1.out"
    shift
    rc=0
    PATH="$realbin:$PATH" ITMAS_HOST_CONF="${RESTORE_HOST_CONF:-$tmp/no-such-host.conf}" sh "$root/scripts/restore-db.sh" "$@" > "$restore_out" 2>&1 || rc=$?
    cp "$restore_out" "$tmp/out"
  }
  dangling_before="$(docker volume ls -q --filter dangling=true | sort)"

  # A restore with the counts that travelled with the dump, found next to it.
  restore restore --backup "$real_dump" --identity "$identity"
  expect_rc "real restore of the real backup" 0
  expect_file_has "the restore compares every table with the dump" "$tmp/restore.out" "Row counts of every table match the dump"
  expect_file_has "the runtime role keeps DML only" "$tmp/restore.out" "keeps DML only"
  expect_file_lacks "the restore never prints a password" "$tmp/restore.out" "$POSTGRES_PASSWORD"

  # A count that differs is reported.
  printf 'app.sample=26\napp_migrations.__ef_migrations_history=1\n' > "$tmp/wrong-counts.txt"
  restore restore2 --backup "$real_dump" --identity "$identity" --counts "$tmp/wrong-counts.txt"
  expect_rc "a count that differs is reported" 3
  expect_file_has "the difference is shown" "$tmp/restore2.out" "Row counts differ from the dump"

  # A table that the counts do not know is also a difference.
  printf 'app.sample=25\n' > "$tmp/missing-counts.txt"
  restore restore3 --backup "$real_dump" --identity "$identity" --counts "$tmp/missing-counts.txt"
  expect_rc "a restored table that the counts do not list" 3

  # Without counts the script stops before starting anything, unless it is told to skip the comparison.
  docker exec "$id-src" pg_dump -Fc -U "$superuser" -d "$db" > "$tmp/plain.dump"
  restore restore4 --plain "$tmp/plain.dump"
  expect_rc "a restore without counts" 4
  expect_file_has "the message says how to proceed" "$tmp/restore4.out" "--no-counts"

  # A dump that is cut short is rejected and nothing is reported as restored.
  head -c 2000 "$tmp/plain.dump" > "$tmp/truncated.dump"
  restore restore5 --plain "$tmp/truncated.dump" --no-counts
  expect_rc "a truncated dump" 1
  expect_file_has "the failure says nothing was restored" "$tmp/restore5.out" "nothing was restored"

  # Atomicity: a dump that fails halfway (a GRANT to a role that the new server does not have) leaves NOTHING restored, not half a database.
  docker exec -i "$id-src" psql -v ON_ERROR_STOP=1 -q -U "$superuser" -d "$db" -c "CREATE ROLE itmas3_extra_role" -c "GRANT SELECT ON app.sample TO itmas3_extra_role" > /dev/null
  docker exec "$id-src" pg_dump -Fc -U "$superuser" -d "$db" > "$tmp/halfway.dump"
  docker exec -i "$id-src" psql -v ON_ERROR_STOP=1 -q -U "$superuser" -d "$db" -c "REVOKE SELECT ON app.sample FROM itmas3_extra_role" -c "DROP ROLE itmas3_extra_role" > /dev/null
  restore restore6 --plain "$tmp/halfway.dump" --no-counts --keep
  expect_rc "a dump that fails halfway" 1
  kept_container="$(sed -n 's/^Kept: container \([^ ]*\) and network.*/\1/p' "$tmp/restore6.out")"
  kept_network="$(sed -n 's/^Kept: container [^ ]* and network \([^ ]*\) .*/\1/p' "$tmp/restore6.out")"
  kept_files="$(sed -n 's/.*Files in \(.*\)\.$/\1/p' "$tmp/restore6.out")"
  checks=$((checks + 1))
  if [ -z "$kept_container" ]; then
    fail "--keep did not report the container to inspect"
  else
    left="$(docker exec "$kept_container" psql -At -U "$superuser" -d "$db" -c "select count(*) from information_schema.tables where table_schema in ('app', 'app_migrations')" 2> /dev/null || echo "?")"
    [ "$left" = 0 ] || fail "a failed restore left $left tables behind: it must be a single transaction"
    docker rm -fv "$kept_container" > /dev/null 2>&1 || true
    docker network rm "$kept_network" > /dev/null 2>&1 || true
    case "$kept_files" in "$tmp"* | /tmp/* | /var/folders/* | /private/*) rm -rf "$kept_files" ;; esac
  fi

  # The privileges of the runtime role: every line of scripts/lib/restore-assertions.sql is broken on purpose and must say "f".
  assert_privileges() {
    docker exec -i "$id-src" psql -At -U "$superuser" -d "$db" -v app_role="$appuser" -v migrator_role="$migrator" < "$root/scripts/lib/restore-assertions.sql"
  }
  source_sql() {
    docker exec -i "$id-src" psql -v ON_ERROR_STOP=1 -q -U "$superuser" -d "$db" -c "$1" > /dev/null
  }
  privilege_case() { # description, SQL that breaks the model, SQL that repairs it
    source_sql "$2"
    checks=$((checks + 1))
    [ "$(assert_privileges)" = f ] || fail "the privilege assertions accept: $1"
    source_sql "$3"
    checks=$((checks + 1))
    [ "$(assert_privileges)" = t ] || fail "the privilege assertions do not recover after: $1"
  }
  checks=$((checks + 1))
  [ "$(assert_privileges)" = t ] || fail "the privilege assertions reject a database with the expected privileges"
  privilege_case "CREATE on the app schema" "GRANT CREATE ON SCHEMA app TO \"$appuser\"" "REVOKE CREATE ON SCHEMA app FROM \"$appuser\""
  privilege_case "no USAGE on the app schema" "REVOKE USAGE ON SCHEMA app FROM \"$appuser\"" "GRANT USAGE ON SCHEMA app TO \"$appuser\""
  privilege_case "USAGE on the migration history schema" "GRANT USAGE ON SCHEMA app_migrations TO \"$appuser\"" "REVOKE USAGE ON SCHEMA app_migrations FROM \"$appuser\""
  privilege_case "a table that the migrator does not own" "ALTER TABLE app.sample OWNER TO \"$superuser\"" "ALTER TABLE app.sample OWNER TO \"$migrator\""
  privilege_case "no SELECT on a table" "REVOKE SELECT ON app.sample FROM \"$appuser\"" "GRANT SELECT ON app.sample TO \"$appuser\""
  privilege_case "TRUNCATE on a table" "GRANT TRUNCATE ON app.sample TO \"$appuser\"" "REVOKE TRUNCATE ON app.sample FROM \"$appuser\""
  privilege_case "an object owned by the runtime role" "CREATE SEQUENCE app.seq_owned_by_app; ALTER SEQUENCE app.seq_owned_by_app OWNER TO \"$appuser\"" "DROP SEQUENCE app.seq_owned_by_app"
  privilege_case "UPDATE on the audit table" "GRANT UPDATE ON app.audit_events TO \"$appuser\"" "REVOKE UPDATE ON app.audit_events FROM \"$appuser\""
  privilege_case "DELETE on the audit table" "GRANT DELETE ON app.audit_events TO \"$appuser\"" "REVOKE DELETE ON app.audit_events FROM \"$appuser\""
  # ... and the whole restore refuses a dump that carries one of those privileges.
  source_sql "GRANT TRUNCATE ON app.sample TO \"$appuser\""
  docker exec "$id-src" pg_dump -Fc -U "$superuser" -d "$db" > "$tmp/wide.dump"
  source_sql "REVOKE TRUNCATE ON app.sample FROM \"$appuser\""
  restore restore7 --plain "$tmp/wide.dump" --no-counts
  expect_rc "a dump that gives the runtime role more than DML" 1
  expect_file_has "the refusal names the role" "$tmp/restore7.out" "does not have the expected privileges"

  # The restore refuses to run on a production server.
  : > "$tmp/prod-host.conf"
  RESTORE_HOST_CONF="$tmp/prod-host.conf" restore restore8 --plain "$tmp/plain.dump" --no-counts
  expect_rc "the restore refuses to run on the production server" 1
  expect_file_has "the refusal explains why" "$tmp/restore8.out" "production server"

  # Nothing of the restores stays behind: containers, networks and volumes (a volume holds a copy of the production data).
  checks=$((checks + 1))
  [ -z "$(docker ps -a --filter "name=itmas3-restore-" -q)" ] || fail "a restore container was left behind"
  checks=$((checks + 1))
  [ -z "$(docker network ls --filter "name=itmas3-restore-" -q)" ] || fail "a restore network was left behind"
  dangling_after="$(docker volume ls -q --filter dangling=true | sort)"
  checks=$((checks + 1))
  new_volumes="$(printf '%s\n' "$dangling_before" > "$tmp/vol-before"; printf '%s\n' "$dangling_after" > "$tmp/vol-after"; comm -13 "$tmp/vol-before" "$tmp/vol-after" | grep -c . || true)"
  [ "$new_volumes" = 0 ] || fail "the restores left $new_volumes anonymous volumes with copies of the data"
else
  if [ "${CI:-}" = true ]; then
    fail "Docker is not available in CI: the real backup and restore round trip cannot be skipped there"
  else
    skipped=1
    echo "SKIPPED: Docker is not running, so the real backup and restore round trip did not run (start Docker to run it)."
  fi
fi

printf '%d of %d checks passed.' "$((checks - failures))" "$checks"
[ "$skipped" -eq 0 ] || printf ' The real backup and restore round trip was skipped.'
printf '\n'
[ "$failures" -eq 0 ]
