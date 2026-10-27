#!/bin/sh
# Hardens the production host (SPEC-0009 RF-7 to RF-12, RF-30; constitution 8.7). Idempotent; run as root on Ubuntu 24.04.
#   sudo scripts/harden-host.sh apply [--confirm-ssh-key]   install and configure ufw, fail2ban, unattended-upgrades, the Docker
#                                                         log rotation and, with --confirm-ssh-key, SSH with keys only
#   sudo scripts/harden-host.sh check                       compare what is installed with deploy/** and verify each control
# SSH hardening is applied only after you confirm that a key works: open a second session with the key first, then pass the flag.
# The script refuses it when the administrator has no authorized key. Nothing here opens a port besides 22, 80 and 443.
# Tests run it against a directory (ITMAS_ROOT_PREFIX) without touching the system (ITMAS_SKIP_SYSTEM=1).
# Exit: 0 ok, 1 failure or, in check mode, any control that is missing or different.
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
  sed -n '2,9p' "$0" | sed 's/^# \{0,1\}//' >&2
  exit 1
}

mode="${1:-}"
[ -n "$mode" ] || usage
shift
confirm_ssh=0
for arg in "$@"; do
  case "$arg" in
    --confirm-ssh-key) confirm_ssh=1 ;;
    *) usage ;;
  esac
done

# <source> <destination> <mode>
managed_files() {
  cat << LIST
deploy/docker/daemon.json $R/etc/docker/daemon.json 644
deploy/fail2ban/jail.d/itmas3.local $R/etc/fail2ban/jail.d/itmas3.local 644
deploy/apt/52itmas3-unattended-upgrades $R/etc/apt/apt.conf.d/52itmas3-unattended-upgrades 644
LIST
}
ssh_source="deploy/ssh/10-itmas3-hardening.conf"
ssh_destination="$R/etc/ssh/sshd_config.d/10-itmas3-hardening.conf"

problem() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$*"
}

# Does the administrator (the user who ran sudo) have at least one authorized key? Without one, SSH hardening would lock them out.
admin_has_key() {
  admin="${SUDO_USER:-}"
  [ -n "$admin" ] || return 1
  home="$(getent passwd "$admin" | cut -d: -f6)"
  [ -n "$home" ] && [ -s "$home/.ssh/authorized_keys" ]
}

apply() {
  [ -n "$R" ] || [ "$(id -u)" -eq 0 ] || { echo "Run as root (sudo)." >&2; exit 1; }
  if [ "$SKIP" != 1 ]; then
    apt-get update -qq
    DEBIAN_FRONTEND=noninteractive apt-get install -y -qq ufw fail2ban unattended-upgrades
    # SSH is allowed BEFORE the firewall is enabled, so that enabling it cannot lock the session out (RF-7).
    ufw default deny incoming
    ufw default allow outgoing
    ufw allow 22/tcp
    ufw allow 80/tcp
    ufw allow 443/tcp
    ufw --force enable
  fi
  docker_changed=0
  fail2ban_changed=0
  while read -r src dst fmode; do
    if itmas_install_file "$src" "$dst" "$fmode"; then
      echo "Installed $dst"
      case "$dst" in */docker/daemon.json) docker_changed=1 ;; */fail2ban/*) fail2ban_changed=1 ;; esac
    else
      echo "Unchanged $dst"
    fi
  done << LIST
$(managed_files)
LIST
  if [ "$SKIP" != 1 ]; then
    systemctl enable --now fail2ban
    [ "$fail2ban_changed" -eq 0 ] || systemctl restart fail2ban
    systemctl enable --now apt-daily.timer apt-daily-upgrade.timer
    if [ "$docker_changed" -eq 1 ]; then
      echo "daemon.json changed: restart Docker before creating the containers (sudo systemctl restart docker); running containers restart with it."
    fi
  fi
  if [ "$confirm_ssh" -eq 1 ]; then
    if [ "$SKIP" != 1 ] && ! admin_has_key; then
      echo "Refusing to disable SSH passwords: ${SUDO_USER:-the administrator (run this with sudo from your own user)} has no authorized key (~/.ssh/authorized_keys). Add and test a key first." >&2
      exit 1
    fi
    if itmas_install_file "$ssh_source" "$ssh_destination" 644; then
      echo "Installed $ssh_destination"
      if [ "$SKIP" != 1 ]; then
        sshd -t || { echo "sshd rejects the configuration; restoring." >&2; rm -f "$ssh_destination"; exit 1; }
        systemctl reload ssh
      fi
    else
      echo "Unchanged $ssh_destination"
    fi
  else
    echo "SSH hardening NOT applied: test a key in a second session, then run: sudo scripts/harden-host.sh apply --confirm-ssh-key"
  fi
}

check_files() {
  while read -r src dst fmode; do
    status="$(itmas_file_status "$src" "$dst")"
    printf '%-8s %s\n' "$status" "$dst"
    [ "$status" = OK ] || problem "$dst is $status (compare with $src)"
  done << LIST
$(managed_files)
$ssh_source $ssh_destination 644
LIST
}

check_system() {
  ufw_out="$(ufw status verbose 2> /dev/null || true)"
  case "$ufw_out" in *"Status: active"*) ;; *) problem "ufw is not active" ;; esac
  case "$ufw_out" in *"deny (incoming)"*"allow (outgoing)"*) ;; *) problem "ufw default policy is not deny incoming / allow outgoing" ;; esac
  # The protocol counts: "443/udp" is not "443/tcp", and a rule without protocol ("80") allows both.
  ports="$(printf '%s\n' "$ufw_out" | awk '/ALLOW/ { print $1 }' | sort -u | tr '\n' ' ')"
  [ "$ports" = "22/tcp 443/tcp 80/tcp " ] || problem "ufw allows [${ports% }], expected exactly 22/tcp 80/tcp 443/tcp"
  sshd_out="$(sshd -T 2> /dev/null || true)"
  printf '%s\n' "$sshd_out" | grep -q '^passwordauthentication no$' || problem "sshd still accepts passwords"
  printf '%s\n' "$sshd_out" | grep -q '^permitrootlogin no$' || problem "sshd still allows root login"
  fail2ban-client status sshd > /dev/null 2>&1 || problem "fail2ban has no active sshd jail"
  reboot="$(apt-config dump Unattended-Upgrade::Automatic-Reboot 2> /dev/null || true)"
  case "$reboot" in *'"false"'*) ;; *) problem "unattended-upgrades may reboot by itself" ;; esac
  origins="$(apt-config dump Unattended-Upgrade::Allowed-Origins 2> /dev/null || true)"
  case "$origins" in *-updates*) problem "unattended-upgrades also installs non-security updates" ;; esac
  systemctl is-enabled apt-daily-upgrade.timer > /dev/null 2>&1 || problem "apt-daily-upgrade.timer is not enabled"
  docker_major="$(docker version --format '{{.Server.Version}}' 2> /dev/null | cut -d. -f1 || true)"
  case "$docker_major" in "" | *[!0-9]*) problem "cannot read the Docker Engine version" ;; *) [ "$docker_major" -ge 28 ] || problem "Docker Engine $docker_major is older than 28 (ADR-0018, decision 6)" ;; esac
  driver="$(docker info --format '{{.LoggingDriver}}' 2> /dev/null || true)"
  [ "$driver" = local ] || problem "Docker logging driver is '$driver', expected local (restart Docker after installing daemon.json)"
}

case "$mode" in
  apply) apply ;;
  check)
    check_files
    [ "$SKIP" = 1 ] || check_system
    if [ "$failures" -eq 0 ]; then
      if [ "$SKIP" = 1 ]; then
        echo "The installed files match deploy/** (the running system was not checked: ITMAS_SKIP_SYSTEM=1)."
      else
        echo "All hardening controls match deploy/** and are in place."
      fi
    else
      echo "$failures control(s) differ or are missing."
      exit 1
    fi
    ;;
  *) usage ;;
esac
