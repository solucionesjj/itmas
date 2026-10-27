#!/bin/sh
# Shared helpers of the host scripts (SPEC-0009): deploy.sh, backup-db.sh, restore-db.sh and check-host.sh source this file.
# It is a library: it is not executed and has no side effects when sourced. POSIX sh only.
#
# Host configuration: /etc/itmas3/host.conf (override with ITMAS_HOST_CONF), shell syntax, owned by root, mode 600.
# Its values are defaults: a variable already exported in the environment wins. The file holds no application secrets,
# but the heartbeat URLs it may contain are secrets of the monitoring service (constitution section 4).
# Template with every variable: deploy/host.conf.example.

ITMAS_HOST_CONF="${ITMAS_HOST_CONF:-/etc/itmas3/host.conf}"

# Variables the configuration file may define. Kept in one place so that the environment can win over the file.
ITMAS_CONF_VARS="ITMAS_REGISTRY ITMAS_PUBLIC_HOST ITMAS_STATE_DIR AGENT_DOWNLOADS_DIR BACKUP_DIR BACKUP_AGE_RECIPIENT \
BACKUP_REMOTE BACKUP_KEEP_DAYS BACKUP_MAX_AGE_HOURS HEARTBEAT_URL HEARTBEAT_FAIL_URL DISK_WARN_PCT CERT_WARN_DAYS \
PATCH_MAX_DAYS"

itmas_log() {
  printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"
}

itmas_err() {
  itmas_log "$*" >&2
}

# Loads ITMAS_HOST_CONF when it exists. The file is executed as shell, so it is refused when group or others can write it or when it
# belongs to anyone but the user running the script (root in production): a file that someone else owns could run code as root.
itmas_load_conf() {
  [ -e "$ITMAS_HOST_CONF" ] || return 0
  if [ ! -r "$ITMAS_HOST_CONF" ]; then
    itmas_err "Cannot read $ITMAS_HOST_CONF: it belongs to root, run this script with sudo."
    return 1
  fi
  if [ -n "$(find "$ITMAS_HOST_CONF" -maxdepth 0 \( -perm -020 -o -perm -002 \) 2> /dev/null)" ]; then
    itmas_err "Refusing to read $ITMAS_HOST_CONF: it is writable by group or others."
    return 1
  fi
  if [ -n "$(find "$ITMAS_HOST_CONF" -maxdepth 0 ! -user "$(id -u)" 2> /dev/null)" ]; then
    itmas_err "Refusing to read $ITMAS_HOST_CONF: it must belong to the user running this script (root in production)."
    return 1
  fi
  for _name in $ITMAS_CONF_VARS; do
    eval "if [ \"\${$_name+set}\" = set ]; then __env_$_name=\"\$$_name\"; __env_set_$_name=1; fi"
  done
  # shellcheck disable=SC1090
  . "$ITMAS_HOST_CONF"
  for _name in $ITMAS_CONF_VARS; do
    eval "if [ \"\${__env_set_$_name:-}\" = 1 ]; then $_name=\"\$__env_$_name\"; fi"
  done
}

# itmas_lock <dir>: a lock that holds the PID of its owner. A lock whose process is gone (kill -9, out of memory, power cut) is taken
# over; one held by a live process makes this return 1.
itmas_lock() {
  if mkdir "$1" 2> /dev/null; then
    echo "$$" > "$1/pid"
    return 0
  fi
  _pid="$(cat "$1/pid" 2> /dev/null || true)"
  if [ -n "$_pid" ] && kill -0 "$_pid" 2> /dev/null; then
    return 1
  fi
  rm -rf "$1"
  mkdir "$1" 2> /dev/null || return 1
  echo "$$" > "$1/pid"
}

itmas_unlock() {
  rm -rf "$1" 2> /dev/null || true
}

# itmas_ping <url> [body]: best-effort GET (or POST with a body) to a monitoring URL. The URL is a secret, so it goes to curl
# through its configuration on stdin and never appears in the process list or in a log.
itmas_ping() {
  [ -n "${1:-}" ] || return 0
  if [ -n "${2:-}" ]; then
    printf 'url = "%s"\n' "$1" | curl -fsS -m 10 --retry 2 -o /dev/null -K - --data-raw "$2" > /dev/null 2>&1 || return 0
  else
    printf 'url = "%s"\n' "$1" | curl -fsS -m 10 --retry 2 -o /dev/null -K - > /dev/null 2>&1 || return 0
  fi
}

itmas_notify_ok() {
  itmas_ping "${HEARTBEAT_URL:-}"
}

# itmas_notify_fail <message>: reports a failure through the heartbeat channel (HEARTBEAT_FAIL_URL, or HEARTBEAT_URL/fail).
itmas_notify_fail() {
  if [ -n "${HEARTBEAT_FAIL_URL:-}" ]; then
    itmas_ping "$HEARTBEAT_FAIL_URL" "$1"
  elif [ -n "${HEARTBEAT_URL:-}" ]; then
    itmas_ping "${HEARTBEAT_URL%/}/fail" "$1"
  fi
}

# itmas_file_status <source> <destination>: prints OK, DIFF or MISSING (used by the "check" modes, constitution 8.7 a).
itmas_file_status() {
  if [ ! -f "$2" ]; then
    echo MISSING
  elif cmp -s "$1" "$2"; then
    echo OK
  else
    echo DIFF
  fi
}

# itmas_install_file <source> <destination> <mode>: installs the file if it differs and returns 0, or returns 1 when it is already
# identical. A file that is replaced is first copied to ITMAS_REPLACED_DIR (never beside it: drop-in directories read their files).
itmas_install_file() {
  [ "$(itmas_file_status "$1" "$2")" != OK ] || return 1
  mkdir -p "$(dirname "$2")"
  if [ -f "$2" ] && [ -n "${ITMAS_REPLACED_DIR:-}" ]; then
    mkdir -p "$ITMAS_REPLACED_DIR"
    cp "$2" "$ITMAS_REPLACED_DIR/$(basename "$2").$(date -u +%Y%m%d-%H%M%S)"
  fi
  cp "$1" "$2.new"
  chmod "$3" "$2.new"
  if [ "$(id -u)" -eq 0 ]; then chown root:root "$2.new" 2> /dev/null || true; fi
  mv "$2.new" "$2"
}
