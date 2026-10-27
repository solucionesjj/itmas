#!/bin/sh
# Host checks and heartbeat (SPEC-0009 RF-20 to RF-23, RF-31). Runs as root every 5 minutes from a systemd timer
# (deploy/systemd/itmas3-check.timer). It checks, and reports through the heartbeat channel (success ping, or failure ping with the list):
#   - postgres, api and frontend are healthy (the external probe of /auth/session does not reach PostgreSQL)
#   - the Caddy service is active
#   - Docker publishes nothing beyond 127.0.0.1 (constitution 8.4 and 8.7 b, checked here every time, not only at each deployment)
#   - disk usage of Docker and of the backup directory is below DISK_WARN_PCT (default 80)
#   - the TLS certificate served on 127.0.0.1:443 does not expire within CERT_WARN_DAYS (default 14)
#   - an encrypted backup exists, was copied off the server, and is younger than BACKUP_MAX_AGE_HOURS (default 26)
#   - the system does not ask for a reboot (/var/run/reboot-required)
#   - no security update (Ubuntu security pocket), nor an update of Caddy or Docker, stays pending for more than PATCH_MAX_DAYS (default 7):
#     Caddy and Docker come from third-party repositories that unattended-upgrades skips, and it can fail silently (constitution 8.7 d)
# Configuration: /etc/itmas3/host.conf (template deploy/host.conf.example). It never prints secrets. Exit: 0 all fine, 1 problems.
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd -P)
cd "$repo"
# shellcheck disable=SC1091
. "$repo/scripts/lib/itmas-host.sh"
itmas_load_conf

: "${BACKUP_DIR:=/var/backups/itmas3}"
: "${BACKUP_MAX_AGE_HOURS:=26}"
: "${DISK_WARN_PCT:=80}"
: "${CERT_WARN_DAYS:=14}"
: "${PATCH_MAX_DAYS:=7}"
: "${ITMAS_PUBLIC_HOST:=itmas.sacservices.cloud}"
: "${ITMAS_STATE_DIR:=/var/lib/itmas3}"
: "${ITMAS_REBOOT_FILE:=/var/run/reboot-required}"
: "${ITMAS_DOCKER_DIR:=/var/lib/docker}"

problems=""
problem() {
  problems="${problems:+$problems; }$*"
}

for service in postgres api frontend; do
  cid="$(docker compose -f docker-compose.yml ps -q "$service" 2> /dev/null || true)"
  if [ -z "$cid" ]; then
    problem "$service is not running"
    continue
  fi
  status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$cid" 2> /dev/null || true)"
  [ "$status" = healthy ] || problem "$service is $status"
done

systemctl is-active --quiet caddy 2> /dev/null || problem "caddy is not active"

exposed="$(docker ps --format '{{.Names}} {{.Ports}}' 2> /dev/null | awk '
  { name = $1; line = $0; sub(/^[^ ]+ /, "", line); n = split(line, maps, /, /)
    for (i = 1; i <= n; i++) {
      m = maps[i]
      if (m !~ /->/) continue
      sub(/->.*/, "", m)
      sub(/:[0-9]+(-[0-9]+)?$/, "", m)
      if (m != "127.0.0.1" && m != "[::1]") { print name; break }
    }
  }' | tr '\n' ' ' || true)"
[ -z "$exposed" ] || problem "Docker publishes ports beyond 127.0.0.1 for: ${exposed% }"

for dir in "$ITMAS_DOCKER_DIR" "$BACKUP_DIR"; do
  [ -d "$dir" ] || continue
  used="$(df -P "$dir" 2> /dev/null | awk 'NR == 2 { gsub(/%/, "", $5); print $5 }')"
  if [ -n "$used" ] && [ "$used" -ge "$DISK_WARN_PCT" ]; then
    problem "disk of $dir is at ${used}% (limit ${DISK_WARN_PCT}%)"
  fi
done

# The certificate is read from the local listener (Caddy), so it also works before the DNS record exists.
if ! echo | openssl s_client -connect 127.0.0.1:443 -servername "$ITMAS_PUBLIC_HOST" 2> /dev/null \
  | openssl x509 -noout -checkend $((CERT_WARN_DAYS * 86400)) > /dev/null 2>&1; then
  problem "the TLS certificate is missing or expires in less than $CERT_WARN_DAYS days"
fi

# The newest encrypted dump must be recent and must have been copied off the server (it has its own .uploaded marker).
max_minutes=$((BACKUP_MAX_AGE_HOURS * 60))
newest_backup="$(find "$BACKUP_DIR" -maxdepth 1 -type f -name 'itmas3-*.dump.age' 2> /dev/null | sed 's#.*/##' | sort | tail -n 1)"
if [ -z "$newest_backup" ] || [ -z "$(find "$BACKUP_DIR/$newest_backup" -maxdepth 0 -mmin "-$max_minutes" 2> /dev/null)" ]; then
  problem "no encrypted backup in the last $BACKUP_MAX_AGE_HOURS hours"
elif [ ! -e "$BACKUP_DIR/$newest_backup.uploaded" ]; then
  problem "the latest backup was not copied off the server"
fi

[ ! -f "$ITMAS_REBOOT_FILE" ] || problem "the system asks for a reboot"

# Updates that matter and that may not be applied by themselves. A failing apt must not hide them.
first_seen="$ITMAS_STATE_DIR/pending-patches-first-seen"
apt_ok=1
upgradable="$(apt list --upgradable 2> /dev/null)" || {
  apt_ok=0
  problem "apt list failed: pending updates cannot be checked"
}
pending="$(printf '%s\n' "$upgradable" | grep -E '^(caddy|docker-ce|docker-ce-cli|containerd\.io|docker-compose-plugin|docker-buildx-plugin)/|-security' | cut -d/ -f1 | sort -u | tr '\n' ' ' || true)"
if [ -n "$pending" ]; then
  now="$(date +%s)"
  mkdir -p "$ITMAS_STATE_DIR"
  [ -s "$first_seen" ] || echo "$now" > "$first_seen"
  since="$(cat "$first_seen")"
  if [ $((now - since)) -ge $((PATCH_MAX_DAYS * 86400)) ]; then
    problem "updates pending for more than $PATCH_MAX_DAYS days: ${pending% }"
  fi
elif [ "$apt_ok" -eq 1 ]; then
  rm -f "$first_seen"
fi

if [ -z "$problems" ]; then
  itmas_log "All host checks passed."
  itmas_notify_ok
  exit 0
fi
itmas_err "PROBLEMS: $problems"
itmas_notify_fail "$problems"
exit 1
