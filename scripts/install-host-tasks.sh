#!/bin/sh
# Installs the scheduled tasks and the proxy configuration of the production host (SPEC-0009 RF-2, RF-23, RF-27, RF-30; constitution 8.7).
# Idempotent; run as root.
#   sudo scripts/install-host-tasks.sh apply   directories, systemd units (daily backup, host checks every 5 minutes), Caddy files
#   sudo scripts/install-host-tasks.sh check   compare what is installed with deploy/** and verify permissions
# It creates /etc/itmas3/host.conf and /etc/caddy/itmas3.env from their templates when they do not exist, empty and mode 600: fill
# them in (deploy/host.conf.example, deploy/caddy/itmas3.env.example); `check` fails while the values are missing and never overwrites
# them. It also installs age and rclone and checks that rclone supports the options backup-db.sh uses. Caddy itself is installed as in
# DEVELOPMENT.md 12.3, before this script. Tests run it against a directory (ITMAS_ROOT_PREFIX) without touching the system
# (ITMAS_SKIP_SYSTEM=1).
# Exit: 0 ok, 1 failure or, in check mode, any file missing or different.
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd -P)
cd "$repo"
# shellcheck disable=SC1091
. "$repo/scripts/lib/itmas-host.sh"

R="${ITMAS_ROOT_PREFIX:-}"
SKIP="${ITMAS_SKIP_SYSTEM:-0}"
ITMAS_REPLACED_DIR="$R${ITMAS_STATE_DIR:-/var/lib/itmas3}/replaced"
failures=0

usage() {
  sed -n '2,11p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit 1
}

mode="${1:-}"
[ "$#" -eq 1 ] || usage

managed_files() {
  cat << LIST
deploy/systemd/itmas3-backup.service $R/etc/systemd/system/itmas3-backup.service 644
deploy/systemd/itmas3-backup.timer $R/etc/systemd/system/itmas3-backup.timer 644
deploy/systemd/itmas3-check.service $R/etc/systemd/system/itmas3-check.service 644
deploy/systemd/itmas3-check.timer $R/etc/systemd/system/itmas3-check.timer 644
deploy/caddy/Caddyfile $R/etc/caddy/Caddyfile 644
deploy/caddy/itmas3.conf $R/etc/systemd/system/caddy.service.d/itmas3.conf 644
LIST
}

# <template> <destination>: files the administrator fills in. Created empty from the template, never overwritten.
private_files() {
  cat << LIST
deploy/host.conf.example $R/etc/itmas3/host.conf
deploy/caddy/itmas3.env.example $R/etc/caddy/itmas3.env
LIST
}

problem() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*"
}

# Is the file or directory closed to group and others? (mode 600 / 700 style)
private_mode() {
  [ -z "$(find "$1" -maxdepth 0 \( -perm -040 -o -perm -020 -o -perm -010 -o -perm -004 -o -perm -002 -o -perm -001 \) 2> /dev/null)" ]
}

apply() {
  [ -n "$R" ] || [ "$(id -u)" -eq 0 ] || { echo "Run as root (sudo)." >&2; exit 1; }
  state="$R${ITMAS_STATE_DIR:-/var/lib/itmas3}"
  mkdir -p "$R/etc/itmas3" "$R${BACKUP_DIR:-/var/backups/itmas3}" "$state"
  chmod 700 "$R${BACKUP_DIR:-/var/backups/itmas3}"
  # The deployment state is root's, like everything else here: deploy.sh runs with sudo.
  chmod 700 "$state"
  # The agent packages folder is created here, not by Docker's bind mount (which would create it on its own), with the mode nginx needs.
  agent_dist="$R${AGENT_DOWNLOADS_DIR:-$repo/agent/dist}"
  mkdir -p "$agent_dist"
  chmod 755 "$agent_dist"
  # Caddy keeps its TLS key and ACME account here: only its own user may read them (constitution 8.7 d).
  [ ! -d "$R/var/lib/caddy" ] || chmod 700 "$R/var/lib/caddy"
  caddy_changed=0
  while read -r src dst fmode; do
    if itmas_install_file "$src" "$dst" "$fmode"; then
      echo "Installed $dst"
      case "$dst" in *caddy*) caddy_changed=1 ;; esac
    else
      echo "Unchanged $dst"
    fi
  done << LIST
$(managed_files)
LIST
  while read -r src dst; do
    if [ -e "$dst" ]; then
      echo "Kept $dst (exists)"
    else
      mkdir -p "$(dirname "$dst")"
      cp "$src" "$dst"
      chmod 600 "$dst"
      echo "Created $dst from its template: fill it in"
    fi
    chmod 600 "$dst"
  done << LIST
$(private_files)
LIST
  if [ "$SKIP" != 1 ]; then
    command -v age > /dev/null 2>&1 && command -v rclone > /dev/null 2>&1 || {
      apt-get update -qq
      DEBIAN_FRONTEND=noninteractive apt-get install -y -qq age rclone
    }
    systemctl daemon-reload
    systemctl enable --now itmas3-backup.timer itmas3-check.timer
    if command -v caddy > /dev/null 2>&1 && [ "$caddy_changed" -eq 1 ]; then
      ACME_EMAIL=check@example.invalid caddy validate --config "$R/etc/caddy/Caddyfile" --adapter caddyfile > /dev/null \
        || { echo "Caddy rejects the Caddyfile; not restarting it." >&2; exit 1; }
      systemctl restart caddy
    fi
  fi
}

check() {
  while read -r src dst fmode; do
    status="$(itmas_file_status "$src" "$dst")"
    printf '%-8s %s\n' "$status" "$dst"
    [ "$status" = OK ] || problem "$dst is $status (compare with $src)"
  done << LIST
$(managed_files)
LIST
  while read -r src dst; do
    if [ ! -f "$dst" ]; then
      printf '%-8s %s\n' MISSING "$dst"
      problem "$dst is missing (template: $src)"
    elif ! private_mode "$dst"; then
      problem "$dst is readable by group or others (it must be mode 600)"
    elif [ -n "$(find "$dst" -maxdepth 0 ! -user "$(id -u)" 2> /dev/null)" ]; then
      problem "$dst must belong to the user running the check (root in production)"
    else
      printf '%-8s %s\n' OK "$dst"
    fi
  done << LIST
$(private_files)
LIST
  # host.conf is useless, and alerts stay silent, while its values are empty (the values are never printed).
  ITMAS_HOST_CONF="$R/etc/itmas3/host.conf"
  if [ -f "$ITMAS_HOST_CONF" ]; then
    if itmas_load_conf; then
      [ -n "${BACKUP_AGE_RECIPIENT:-}" ] || problem "host.conf: BACKUP_AGE_RECIPIENT is empty"
      [ -n "${BACKUP_REMOTE:-}" ] || problem "host.conf: BACKUP_REMOTE is empty"
      [ -n "${RCLONE_S3_REGION:-}" ] || problem "host.conf: RCLONE_S3_REGION is empty"
      [ -n "${HEARTBEAT_URL:-}" ] || problem "host.conf: HEARTBEAT_URL is empty (nobody would receive the alerts)"
    else
      problem "host.conf cannot be loaded safely (see the message above)"
    fi
  fi
  state_dir="$R${ITMAS_STATE_DIR:-/var/lib/itmas3}"
  if [ -d "$state_dir" ]; then private_mode "$state_dir" || problem "$state_dir must be closed to group and others"; else problem "$state_dir does not exist"; fi
  docker_config="$R/root/.docker/config.json"
  if [ -f "$docker_config" ]; then private_mode "$docker_config" || problem "$docker_config (registry credential) must be mode 600"; fi
  backup_dir="$R${BACKUP_DIR:-/var/backups/itmas3}"
  if [ -d "$backup_dir" ]; then private_mode "$backup_dir" || problem "$backup_dir must be closed to group and others"; else problem "$backup_dir does not exist"; fi
  caddy_data="$R/var/lib/caddy"
  if [ -d "$caddy_data" ]; then private_mode "$caddy_data" || problem "$caddy_data (TLS key and ACME account) must be closed to group and others"; fi
  if [ "$SKIP" != 1 ]; then
    command -v age > /dev/null 2>&1 || problem "age is not installed (sudo apt-get install -y age)"
    if command -v rclone > /dev/null 2>&1; then
      rclone help flags 2> /dev/null | grep -q -- '--no-check-dest' || problem "this rclone does not support --no-check-dest"
      rclone help flags s3 2> /dev/null | grep -q -- '--s3-no-head' || problem "this rclone does not support --s3-no-head"
      rclone help flags s3 2> /dev/null | grep -q -- '--s3-no-check-bucket' || problem "this rclone does not support --s3-no-check-bucket"
    else
      problem "rclone is not installed (sudo apt-get install -y rclone)"
    fi
    for unit in itmas3-backup.timer itmas3-check.timer; do
      systemctl is-enabled "$unit" > /dev/null 2>&1 || problem "$unit is not enabled"
    done
    if command -v caddy > /dev/null 2>&1; then
      systemctl is-active --quiet caddy || problem "caddy is not active"
    fi
  fi
}

case "$mode" in
  apply) apply ;;
  check)
    check
    if [ "$failures" -eq 0 ]; then
      echo "Everything installed matches deploy/**."
    else
      echo "$failures item(s) differ or are missing."
      exit 1
    fi
    ;;
  *) usage ;;
esac
