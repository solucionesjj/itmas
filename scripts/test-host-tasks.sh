#!/bin/sh
# Tests for scripts/harden-host.sh, scripts/install-host-tasks.sh and scripts/check-host.sh (SPEC-0009 CA-7, CA-16 to CA-18, CA-28, CA-29).
# No Docker, network, root or real host needed: files are installed under a temporary ITMAS_ROOT_PREFIX and the system commands
# (ufw, sshd, systemctl, docker, apt, openssl, df, caddy...) are stubs that log their calls.
set -eu

root=$(cd "$(dirname "$0")/.." && pwd)
tmp=$(mktemp -d)
bin="$tmp/bin"
mkdir -p "$bin"
log="$tmp/calls.log"
pings="$tmp/pings.log"
checks=0
failures=0
rc=0
trap 'rm -rf "$tmp"' EXIT
trap 'exit 143' TERM HUP
trap 'exit 130' INT

# ---- stubs --------------------------------------------------------------------------------------------------------
cat > "$bin/ufw" << 'EOF'
#!/bin/sh
echo "ufw $*" >> "$STUB_LOG"
if [ "$1" = status ]; then
  cat << OUT
Status: ${STUB_UFW_STATUS:-active}
Logging: on (low)
Default: ${STUB_UFW_DEFAULT:-deny (incoming), allow (outgoing), disabled (routed)}

To                         Action      From
--                         ------      ----
22/tcp                     ALLOW IN    Anywhere
80/tcp                     ALLOW IN    Anywhere
443/tcp                    ALLOW IN    Anywhere
${STUB_UFW_EXTRA:-}
22/tcp (v6)                ALLOW IN    Anywhere (v6)
OUT
fi
exit 0
EOF
cat > "$bin/sshd" << 'EOF'
#!/bin/sh
echo "sshd $*" >> "$STUB_LOG"
if [ "$1" = "-T" ]; then
  echo "passwordauthentication ${STUB_SSH_PASSWORD:-no}"
  echo "permitrootlogin ${STUB_SSH_ROOT:-no}"
fi
exit "${STUB_SSHD_RC:-0}"
EOF
cat > "$bin/fail2ban-client" << 'EOF'
#!/bin/sh
exit "${STUB_F2B_RC:-0}"
EOF
cat > "$bin/apt-config" << 'EOF'
#!/bin/sh
case "$2" in
  Unattended-Upgrade::Automatic-Reboot) echo "Unattended-Upgrade::Automatic-Reboot \"${STUB_AUTOREBOOT:-false}\";" ;;
  Unattended-Upgrade::Allowed-Origins) echo 'Unattended-Upgrade::Allowed-Origins:: "${distro_id}:${distro_codename}-security";'; [ "${STUB_UPDATES_ORIGIN:-0}" = 1 ] && echo 'Unattended-Upgrade::Allowed-Origins:: "${distro_id}:${distro_codename}-updates";' ;;
esac
exit 0
EOF
cat > "$bin/apt-get" << 'EOF'
#!/bin/sh
echo "apt-get $*" >> "$STUB_LOG"
exit 0
EOF
cat > "$bin/apt" << 'EOF'
#!/bin/sh
[ "${STUB_APT_FAIL:-0}" = 1 ] && exit 1
echo "Listing..."
# STUB_UPGRADES holds the package lines of "apt list --upgradable", separated by ";"
[ -z "${STUB_UPGRADES:-}" ] || printf '%s\n' "$STUB_UPGRADES" | tr ';' '\n'
exit 0
EOF
cat > "$bin/age" << 'EOF'
#!/bin/sh
exit 0
EOF
cat > "$bin/rclone" << 'EOF'
#!/bin/sh
# only "rclone help flags [s3]" is used by install-host-tasks.sh
if [ "$1 $2" = "help flags" ]; then
  if [ "${3:-}" = s3 ]; then
    echo "      --s3-no-check-bucket   If set, don't attempt to check the bucket exists"
    [ "${STUB_RCLONE_NO_HEAD:-0}" = 1 ] || echo "      --s3-no-head           If set, don't HEAD uploaded objects"
  else
    echo "      --no-check-dest        Don't check the destination, copy regardless"
  fi
fi
exit 0
EOF
cat > "$bin/getent" << 'EOF'
#!/bin/sh
echo "$2:x:1000:1000::$STUB_HOME:/bin/sh"
EOF
cat > "$bin/systemctl" << 'EOF'
#!/bin/sh
echo "systemctl $*" >> "$STUB_LOG"
case "$1" in
  is-active) [ "${STUB_CADDY_INACTIVE:-0}" = 1 ] && exit 3 ;;
  is-enabled) [ "${STUB_TIMER_DISABLED:-0}" = 1 ] && exit 1 ;;
esac
exit 0
EOF
cat > "$bin/caddy" << 'EOF'
#!/bin/sh
echo "caddy $*" >> "$STUB_LOG"
[ "$1" != version ] || echo "${STUB_CADDY_VERSION:-v2.11.7 h1:stub=}"
exit "${STUB_CADDY_RC:-0}"
EOF
cat > "$bin/docker" << 'EOF'
#!/bin/sh
echo "docker $*" >> "$STUB_LOG"
case "$1" in
  info) echo "${STUB_LOG_DRIVER:-local}" ;;
  version) echo "${STUB_DOCKER_VERSION:-28.1.0}" ;;
  ps)
    if [ "${STUB_PORTS_EXPOSED:-0}" = 1 ]; then
      echo "itmas3-frontend-1 0.0.0.0:8088->8080/tcp, [::]:8088->8080/tcp"
    else
      echo "itmas3-frontend-1 ${STUB_PS_PORTS:-127.0.0.1:8088->8080/tcp}"
      echo "itmas3-postgres-1 5432/tcp"
    fi
    ;;
  compose)
    for last; do :; done
    case "$*" in *" ps -q "*) [ "${STUB_DOWN:-}" = "$last" ] || echo "cid-$last" ;; esac
    ;;
  inspect)
    for last; do :; done
    case "$last" in
      cid-api) echo "${STUB_HEALTH_API:-healthy}" ;;
      cid-frontend) echo "${STUB_HEALTH_FRONTEND:-healthy}" ;;
      *) echo healthy ;;
    esac
    ;;
esac
exit 0
EOF
cat > "$bin/df" << 'EOF'
#!/bin/sh
printf 'Filesystem 1024-blocks Used Available Capacity Mounted\n/dev/x 1000 500 500 %s%% /\n' "${STUB_DF_PCT:-40}"
EOF
cat > "$bin/openssl" << 'EOF'
#!/bin/sh
case "$1" in
  s_client) echo "openssl $*" >> "$STUB_LOG"; echo "CERTIFICATE" ;;
  x509) echo "openssl $*" >> "$STUB_LOG"; cat > /dev/null; exit "${STUB_CERT_RC:-0}" ;;
esac
exit 0
EOF
cat > "$bin/curl" << 'EOF'
#!/bin/sh
echo "curl $*" >> "$STUB_LOG"
cat >> "$STUB_PINGS"
echo "args: $*" >> "$STUB_PINGS"
exit 0
EOF
chmod +x "$bin"/*

# ---- helpers ------------------------------------------------------------------------------------------------------
fail() {
  failures=$((failures + 1))
  printf 'FAIL: %s\n' "$1" >&2
}

reset_tree() {
  rm -rf "${tmp:?}/root" "${tmp:?}/home"
  mkdir -p "$tmp/root" "$tmp/home/.ssh"
  : > "$log"
  : > "$pings"
}

run() { # command words...; extra environment is passed by the caller through exported variables
  rc=0
  PATH="$bin:$PATH" STUB_LOG="$log" STUB_PINGS="$pings" STUB_HOME="$tmp/home" ITMAS_ROOT_PREFIX="$tmp/root" \
    "$@" > "$tmp/out" 2>&1 < /dev/null || rc=$?
}

expect_rc() {
  checks=$((checks + 1))
  [ "$rc" -eq "$2" ] || fail "$1: exit status $rc, expected $2 (output: $(tr '\n' ' ' < "$tmp/out" | cut -c1-400))"
}

expect_out_has() {
  checks=$((checks + 1))
  grep -qF -- "$2" "$tmp/out" || fail "$1: '$2' not in the output ($(tr '\n' ' ' < "$tmp/out" | cut -c1-300))"
}

expect_out_lacks() {
  checks=$((checks + 1))
  if grep -qF -- "$2" "$tmp/out"; then fail "$1: '$2' is in the output"; fi
}

expect_file_has() {
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

expect_called() {
  checks=$((checks + 1))
  grep -qF -- "$2" "$log" || fail "$1: '$2' was not called"
}

expect_order() {
  checks=$((checks + 1))
  a=$(grep -nF -- "$2" "$log" | head -n 1 | cut -d: -f1 || true)
  b=$(grep -nF -- "$3" "$log" | head -n 1 | cut -d: -f1 || true)
  if [ -z "$a" ] || [ -z "$b" ] || [ "$a" -ge "$b" ]; then fail "$1: '$2' must come before '$3' (lines '$a' and '$b')"; fi
}

R="$tmp/root"
SKIP="ITMAS_SKIP_SYSTEM=1"

# A complete host.conf, as an administrator leaves it after filling the template (the values are fake).
fill_host_conf() {
  {
    echo "BACKUP_AGE_RECIPIENT=age1testrecipient"
    echo "BACKUP_REMOTE=:s3:bucket-test/prefix"
    echo "export RCLONE_S3_REGION=us-east-1"
    echo "HEARTBEAT_URL=https://monitor.example.invalid/ping/secret-token-321"
  } > "$R/etc/itmas3/host.conf"
  chmod 600 "$R/etc/itmas3/host.conf"
}

# ---- harden-host.sh, files only (no system commands) ---------------------------------------------------------------
reset_tree
run env $SKIP ITMAS_STATE_DIR=/var/lib/itmas3 "$root/scripts/harden-host.sh" apply
expect_rc "hardening apply" 0
expect_exists "daemon.json is installed" "$R/etc/docker/daemon.json"
expect_exists "the fail2ban jail is installed" "$R/etc/fail2ban/jail.d/itmas3.local"
expect_exists "the unattended-upgrades policy is installed" "$R/etc/apt/apt.conf.d/52itmas3-unattended-upgrades"
expect_missing "SSH hardening waits for the confirmation of a key" "$R/etc/ssh/sshd_config.d/10-itmas3-hardening.conf"
expect_out_has "the operator is told how to confirm SSH" "--confirm-ssh-key"
expect_file_has "log rotation is configured" "$R/etc/docker/daemon.json" '"max-size": "20m"'
expect_file_has "automatic reboot is off" "$R/etc/apt/apt.conf.d/52itmas3-unattended-upgrades" 'Automatic-Reboot "false"'
run env $SKIP "$root/scripts/harden-host.sh" check
expect_rc "check reports the missing SSH hardening" 1
expect_out_has "check names the missing SSH file" "10-itmas3-hardening.conf is MISSING"
run env $SKIP "$root/scripts/harden-host.sh" apply --confirm-ssh-key
expect_rc "apply with the confirmation" 0
expect_exists "SSH hardening is installed after the confirmation" "$R/etc/ssh/sshd_config.d/10-itmas3-hardening.conf"
expect_file_has "SSH passwords are off" "$R/etc/ssh/sshd_config.d/10-itmas3-hardening.conf" "PasswordAuthentication no"
expect_file_has "root login is off" "$R/etc/ssh/sshd_config.d/10-itmas3-hardening.conf" "PermitRootLogin no"
run env $SKIP "$root/scripts/harden-host.sh" check
expect_rc "check passes when installed matches deploy/**" 0
expect_out_has "check says it matches" "match deploy/**"
run env $SKIP "$root/scripts/harden-host.sh" apply --confirm-ssh-key
expect_rc "apply is idempotent" 0
expect_out_has "a second apply changes nothing" "Unchanged $R/etc/docker/daemon.json"
echo '{"log-driver":"json-file"}' > "$R/etc/docker/daemon.json"
run env $SKIP "$root/scripts/harden-host.sh" check
expect_rc "check detects a file edited by hand" 1
expect_out_has "check shows which file differs" "DIFF"
run env $SKIP ITMAS_STATE_DIR=/var/lib/itmas3 "$root/scripts/harden-host.sh" apply
expect_rc "apply restores the file" 0
expect_file_has "the versioned content is back" "$R/etc/docker/daemon.json" '"max-size": "20m"'
checks=$((checks + 1))
[ -n "$(find "$R/var/lib/itmas3/replaced" -name 'daemon.json.*' 2> /dev/null)" ] || fail "the replaced file was not kept in the state directory"

# ---- harden-host.sh with the system commands stubbed -----------------------------------------------------------------
reset_tree
run env ITMAS_STATE_DIR=/var/lib/itmas3 "$root/scripts/harden-host.sh" apply
expect_rc "apply with system commands" 0
expect_order "SSH is allowed before the firewall is enabled" "ufw allow 22/tcp" "ufw --force enable"
expect_called "port 80 is opened" "ufw allow 80/tcp"
expect_called "port 443 is opened" "ufw allow 443/tcp"
expect_called "incoming traffic is denied by default" "ufw default deny incoming"
expect_called "fail2ban is enabled" "systemctl enable --now fail2ban"
run env "$root/scripts/harden-host.sh" apply --confirm-ssh-key
expect_rc "SSH hardening is refused when the administrator has no key" 1
expect_out_has "the refusal names the reason" "authorized key"
expect_missing "nothing is installed when it is refused" "$R/etc/ssh/sshd_config.d/10-itmas3-hardening.conf"
echo "ssh-ed25519 AAAA-test-key" > "$tmp/home/.ssh/authorized_keys"
run env SUDO_USER=admin "$root/scripts/harden-host.sh" apply --confirm-ssh-key
expect_rc "SSH hardening with a key" 0
expect_called "the configuration is validated before reloading" "sshd -t"
expect_order "validated before reloading" "sshd -t" "systemctl reload ssh"
reset_tree
echo "ssh-ed25519 AAAA-test-key" > "$tmp/home/.ssh/authorized_keys"
STUB_SSHD_RC=1 && export STUB_SSHD_RC
run env SUDO_USER=admin "$root/scripts/harden-host.sh" apply --confirm-ssh-key
unset STUB_SSHD_RC
expect_rc "sshd rejects the new configuration" 1
expect_out_has "the refusal says it restores" "sshd rejects the configuration"
expect_missing "the rejected SSH configuration is removed" "$R/etc/ssh/sshd_config.d/10-itmas3-hardening.conf"
checks=$((checks + 1))
if grep -qF "systemctl reload ssh" "$log"; then fail "sshd was reloaded with a configuration it rejected"; fi
run env SUDO_USER=admin "$root/scripts/harden-host.sh" apply --confirm-ssh-key
run env "$root/scripts/harden-host.sh" check
expect_rc "system checks pass" 0
STUB_UFW_EXTRA="8080/tcp                   ALLOW IN    Anywhere" && export STUB_UFW_EXTRA
run env "$root/scripts/harden-host.sh" check
unset STUB_UFW_EXTRA
expect_rc "an extra open port is detected" 1
expect_out_has "the ports are named" "expected exactly 22/tcp 80/tcp 443/tcp"
STUB_UFW_EXTRA="443/udp                    ALLOW IN    Anywhere" && export STUB_UFW_EXTRA
run env "$root/scripts/harden-host.sh" check
unset STUB_UFW_EXTRA
expect_rc "UDP open on a port that is only allowed for TCP" 1
STUB_UFW_EXTRA="80                         ALLOW IN    Anywhere" && export STUB_UFW_EXTRA
run env "$root/scripts/harden-host.sh" check
unset STUB_UFW_EXTRA
expect_rc "a rule without protocol (TCP and UDP)" 1
STUB_UFW_DEFAULT="allow (incoming), allow (outgoing), disabled (routed)" && export STUB_UFW_DEFAULT
run env "$root/scripts/harden-host.sh" check
unset STUB_UFW_DEFAULT
expect_rc "incoming traffic allowed by default" 1
STUB_SSH_ROOT=yes && export STUB_SSH_ROOT
run env "$root/scripts/harden-host.sh" check
unset STUB_SSH_ROOT
expect_rc "root login allowed over SSH" 1
STUB_TIMER_DISABLED=1 && export STUB_TIMER_DISABLED
run env "$root/scripts/harden-host.sh" check
unset STUB_TIMER_DISABLED
expect_rc "apt-daily-upgrade.timer disabled" 1
STUB_SSH_PASSWORD=yes && export STUB_SSH_PASSWORD
run env "$root/scripts/harden-host.sh" check
unset STUB_SSH_PASSWORD
expect_rc "SSH passwords still accepted" 1
STUB_LOG_DRIVER=json-file && export STUB_LOG_DRIVER
run env "$root/scripts/harden-host.sh" check
unset STUB_LOG_DRIVER
expect_rc "wrong Docker logging driver" 1
STUB_DOCKER_VERSION=27.5.1 && export STUB_DOCKER_VERSION
run env "$root/scripts/harden-host.sh" check
unset STUB_DOCKER_VERSION
expect_rc "Docker Engine older than 28" 1
expect_out_has "the minimum version is named" "older than 28"
STUB_AUTOREBOOT=true && export STUB_AUTOREBOOT
run env "$root/scripts/harden-host.sh" check
unset STUB_AUTOREBOOT
expect_rc "automatic reboot enabled" 1
STUB_UPDATES_ORIGIN=1 && export STUB_UPDATES_ORIGIN
run env "$root/scripts/harden-host.sh" check
unset STUB_UPDATES_ORIGIN
expect_rc "non-security updates enabled" 1
STUB_UFW_STATUS=inactive && export STUB_UFW_STATUS
run env "$root/scripts/harden-host.sh" check
unset STUB_UFW_STATUS
expect_rc "firewall inactive" 1
STUB_F2B_RC=1 && export STUB_F2B_RC
run env "$root/scripts/harden-host.sh" check
unset STUB_F2B_RC
expect_rc "no sshd jail in fail2ban" 1

# ---- install-host-tasks.sh ---------------------------------------------------------------------------------------------
reset_tree
run env $SKIP "$root/scripts/install-host-tasks.sh" apply
expect_rc "install tasks" 0
for unit in itmas3-backup.service itmas3-backup.timer itmas3-check.service itmas3-check.timer; do
  expect_exists "$unit is installed" "$R/etc/systemd/system/$unit"
done
expect_exists "the Caddyfile is installed" "$R/etc/caddy/Caddyfile"
expect_exists "the agent downloads folder is created under the prefix, not in the repository" "$R$root/agent/dist"
expect_exists "the Caddy drop-in is installed" "$R/etc/systemd/system/caddy.service.d/itmas3.conf"
expect_exists "host.conf is created from its template" "$R/etc/itmas3/host.conf"
expect_exists "the Caddy settings file is created" "$R/etc/caddy/itmas3.env"
checks=$((checks + 1))
[ -n "$(find "$R/etc/itmas3/host.conf" -maxdepth 0 -perm 600)" ] || fail "host.conf must be mode 600"
checks=$((checks + 1))
[ -n "$(find "$R/var/backups/itmas3" -maxdepth 0 -perm 700)" ] || fail "the backup directory must be mode 700"
expect_file_has "Caddy has no administration API" "$R/etc/caddy/Caddyfile" "admin off"
expect_file_has "Caddy removes the Via header" "$R/etc/caddy/Caddyfile" "-Via"
expect_file_has "the HSTS max-age is a setting" "$R/etc/caddy/Caddyfile" 'max-age={$HSTS_MAX_AGE:300}'
expect_file_has "the backup unit is sandboxed" "$R/etc/systemd/system/itmas3-backup.service" "NoNewPrivileges=yes"
expect_file_has "the check unit is sandboxed" "$R/etc/systemd/system/itmas3-check.service" "ProtectSystem=strict"
checks=$((checks + 1))
[ -n "$(find "$R/var/lib/itmas3" -maxdepth 0 -perm 700)" ] || fail "the state directory must be mode 700"
# The empty template is not a configuration: the check fails and names what is missing, without printing any value.
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "an empty host.conf is not accepted" 1
expect_out_has "the missing age key is named" "BACKUP_AGE_RECIPIENT is empty"
expect_out_has "the missing destination is named" "BACKUP_REMOTE is empty"
expect_out_has "the missing region is named" "RCLONE_S3_REGION is empty"
expect_out_has "the missing heartbeat is named" "HEARTBEAT_URL is empty"
fill_host_conf
echo "# keep-me" >> "$R/etc/itmas3/host.conf"
run env $SKIP "$root/scripts/install-host-tasks.sh" apply
expect_file_has "apply never overwrites host.conf" "$R/etc/itmas3/host.conf" "keep-me"
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "install check passes" 0
expect_out_lacks "the check never prints the heartbeat URL" "secret-token"
echo "tampered" >> "$R/etc/caddy/Caddyfile"
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "install check detects a hand-edited Caddyfile" 1
expect_out_has "it names the file" "Caddyfile"
run env $SKIP "$root/scripts/install-host-tasks.sh" apply
chmod 644 "$R/etc/itmas3/host.conf"
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "host.conf readable by others" 1
expect_out_has "it explains" "mode 600"
chmod 600 "$R/etc/itmas3/host.conf"
mkdir -p "$R/var/lib/caddy"
chmod 755 "$R/var/lib/caddy"
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "Caddy data directory open to others" 1
expect_out_has "it names the TLS key" "TLS key"
chmod 700 "$R/var/lib/caddy"
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "Caddy data directory closed" 0
chmod 755 "$R/var/lib/itmas3"
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "state directory open to others" 1
expect_out_has "it names the state directory" "var/lib/itmas3 must be closed"
chmod 700 "$R/var/lib/itmas3"
mkdir -p "$R/root/.docker"
echo '{"auths":{}}' > "$R/root/.docker/config.json"
chmod 644 "$R/root/.docker/config.json"
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "registry credential readable by others" 1
expect_out_has "it names the credential" "registry credential"
chmod 600 "$R/root/.docker/config.json"
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "registry credential closed" 0
rm -rf "$R/var/lib/itmas3"
run env $SKIP "$root/scripts/install-host-tasks.sh" check
expect_rc "a missing state directory" 1
# With system commands: units are enabled, Caddy is validated before the restart, age and rclone are verified.
reset_tree
run env "$root/scripts/install-host-tasks.sh" apply
expect_rc "install tasks with system commands" 0
expect_called "timers are enabled" "systemctl enable --now itmas3-backup.timer itmas3-check.timer"
expect_order "the Caddyfile is validated before Caddy restarts" "caddy validate" "systemctl restart caddy"
checks=$((checks + 1))
if grep -qF "apt-get install" "$log"; then fail "age and rclone were installed although both are present"; fi
fill_host_conf
run env "$root/scripts/install-host-tasks.sh" check
expect_rc "a complete installation passes the system checks" 0
STUB_RCLONE_NO_HEAD=1 && export STUB_RCLONE_NO_HEAD
run env "$root/scripts/install-host-tasks.sh" check
unset STUB_RCLONE_NO_HEAD
expect_rc "an rclone without the options backup-db.sh needs" 1
expect_out_has "the missing option is named" "--s3-no-head"
# Caddy before 2.7.2 drops the deferred HSTS header of the Caddyfile (Ubuntu's own package is 2.6.2, found on the first deployment).
for version in 2.6.2 "v2.7.1 h1:stub=" "v1.0.4"; do
  STUB_CADDY_VERSION="$version" && export STUB_CADDY_VERSION
  run env "$root/scripts/install-host-tasks.sh" check
  expect_rc "Caddy $version is refused" 1
  expect_out_has "Caddy $version: the minimum is named" "is older than 2.7.2"
done
for version in "v2.7.2 h1:stub=" "v2.10.0 h1:stub=" "v3.0.0-beta.1 h1:stub="; do
  STUB_CADDY_VERSION="$version" && export STUB_CADDY_VERSION
  run env "$root/scripts/install-host-tasks.sh" check
  expect_rc "Caddy $version is accepted" 0
done
STUB_CADDY_VERSION="unknown" && export STUB_CADDY_VERSION
run env "$root/scripts/install-host-tasks.sh" check
unset STUB_CADDY_VERSION
expect_rc "an unreadable Caddy version" 1
expect_out_has "the unreadable version is reported" "cannot read the Caddy version"
STUB_CADDY_RC=1 && export STUB_CADDY_RC
reset_tree
run env "$root/scripts/install-host-tasks.sh" apply
unset STUB_CADDY_RC
expect_rc "an invalid Caddyfile stops the install" 1
checks=$((checks + 1))
if grep -qF "systemctl restart caddy" "$log"; then fail "Caddy was restarted with an invalid configuration"; fi
STUB_TIMER_DISABLED=1 && export STUB_TIMER_DISABLED
run env "$root/scripts/install-host-tasks.sh" check
unset STUB_TIMER_DISABLED
expect_rc "a disabled timer is detected" 1

# ---- check-host.sh ------------------------------------------------------------------------------------------------------
backups="$tmp/backups"
state="$tmp/state"
# hours_ago <n> -> a touch -t timestamp (local time) n hours in the past; BSD date first, then GNU date.
hours_ago() {
  date -v-"$1"H +%Y%m%d%H%M 2> /dev/null || date -d "$1 hours ago" +%Y%m%d%H%M
}
host_reset() {
  rm -rf "$backups" "$state"
  mkdir -p "$backups" "$state" "$tmp/dockerdir"
  : > "$log"
  : > "$pings"
  # a dump with its upload marker, and the count file that travels with it (it has no marker of its own that matters here)
  touch "$backups/itmas3-20990101-000000.dump.age" "$backups/itmas3-20990101-000000.dump.age.uploaded" "$backups/itmas3-20990101-000000.counts.age"
  {
    echo "BACKUP_DIR=$backups"
    echo "ITMAS_STATE_DIR=$state"
    echo "ITMAS_DOCKER_DIR=$tmp/dockerdir"
    echo "ITMAS_REBOOT_FILE=$tmp/no-reboot-required"
    echo "HEARTBEAT_URL=https://monitor.example.invalid/ping/secret-token-789"
  } > "$tmp/host.conf"
  chmod 600 "$tmp/host.conf"
  rm -f "$tmp/no-reboot-required"
}
host_run() {
  rc=0
  PATH="$bin:$PATH" STUB_LOG="$log" STUB_PINGS="$pings" ITMAS_HOST_CONF="$tmp/host.conf" \
    sh "$root/scripts/check-host.sh" > "$tmp/out" 2>&1 < /dev/null || rc=$?
}

host_reset
host_run
expect_rc "all checks pass" 0
expect_file_has "a success ping is sent" "$pings" "secret-token-789"
expect_file_lacks "a success ping is not a failure" "$pings" "/fail"
expect_file_lacks "the secret URL never appears in a command line" "$log" "secret-token-789"

host_reset
STUB_HEALTH_API=unhealthy && export STUB_HEALTH_API
host_run
unset STUB_HEALTH_API
expect_rc "an unhealthy container" 1
expect_out_has "it names the container" "api is unhealthy"
expect_file_has "a failure ping carries the problem" "$pings" "api is unhealthy"
expect_file_has "the failure goes to /fail" "$pings" "/fail"

host_reset
STUB_DOWN=postgres && export STUB_DOWN
host_run
unset STUB_DOWN
expect_rc "a container that is not running" 1
expect_out_has "postgres is reported" "postgres is not running"

host_reset
STUB_CADDY_INACTIVE=1 && export STUB_CADDY_INACTIVE
host_run
unset STUB_CADDY_INACTIVE
expect_rc "Caddy stopped" 1
expect_out_has "Caddy is reported" "caddy is not active"

host_reset
STUB_DF_PCT=85 && export STUB_DF_PCT
host_run
unset STUB_DF_PCT
expect_rc "disk at 85 percent" 1
expect_out_has "the disk is reported" "disk of"

host_reset
STUB_DF_PCT=80 && export STUB_DF_PCT
host_run
unset STUB_DF_PCT
expect_rc "disk exactly at the limit alerts" 1
host_reset
STUB_DF_PCT=79 && export STUB_DF_PCT
host_run
unset STUB_DF_PCT
expect_rc "disk at 79 percent is fine" 0

host_reset
STUB_CERT_RC=1 && export STUB_CERT_RC
host_run
unset STUB_CERT_RC
expect_rc "certificate about to expire" 1
expect_out_has "the certificate is reported" "TLS certificate"
host_reset
host_run
expect_called "the certificate is checked against 14 days" "-checkend 1209600"
host_reset
echo "CERT_WARN_DAYS=30" >> "$tmp/host.conf"
host_run
expect_called "the days come from host.conf" "-checkend 2592000"
# the environment wins over host.conf
host_reset
echo "DISK_WARN_PCT=90" >> "$tmp/host.conf"
host_run
expect_rc "a limit set in host.conf" 0
DISK_WARN_PCT=10 && export DISK_WARN_PCT
host_run
unset DISK_WARN_PCT
expect_rc "a limit exported in the environment wins over host.conf" 1

host_reset
rm -f "$backups"/itmas3-*.dump.age
host_run
expect_rc "no backup at all" 1
expect_out_has "the missing backup is reported" "no encrypted backup"

host_reset
touch -t 202001010000 "$backups/itmas3-20990101-000000.dump.age"
host_run
expect_rc "an old backup" 1
host_reset
touch -t "$(hours_ago 48)" "$backups/itmas3-20990101-000000.dump.age"
host_run
expect_rc "a backup two days old (limit 26 hours)" 1
host_reset
touch -t "$(hours_ago 20)" "$backups/itmas3-20990101-000000.dump.age"
host_run
expect_rc "a backup of 20 hours is recent enough" 0

host_reset
rm -f "$backups/itmas3-20990101-000000.dump.age.uploaded"
host_run
expect_rc "a backup that was never copied off the server" 1
expect_out_has "the missing external copy is reported" "not copied off the server"

# An older dump that was copied does not hide that the newest one was not.
host_reset
touch "$backups/itmas3-20980101-000000.dump.age" "$backups/itmas3-20980101-000000.dump.age.uploaded"
rm -f "$backups/itmas3-20990101-000000.dump.age.uploaded"
host_run
expect_rc "the newest dump has no marker although an older one has" 1

# Docker must publish nothing beyond 127.0.0.1, every time and not only at each deployment.
host_reset
STUB_PORTS_EXPOSED=1 && export STUB_PORTS_EXPOSED
host_run
unset STUB_PORTS_EXPOSED
expect_rc "a container published on all interfaces" 1
expect_out_has "the container is named" "beyond 127.0.0.1 for: itmas3-frontend-1"
expect_file_has "the failure is reported through the heartbeat" "$pings" "beyond 127.0.0.1"
# Any address that is not loopback counts, not only the wildcard ones; loopback in IPv6 and unpublished ports are fine.
host_reset
STUB_PS_PORTS="10.0.0.5:8088->8080/tcp" && export STUB_PS_PORTS
host_run
unset STUB_PS_PORTS
expect_rc "a container published on a concrete non-loopback address" 1
host_reset
STUB_PS_PORTS="127.0.0.1:8088->8080/tcp, 0.0.0.0:9000->9000/tcp" && export STUB_PS_PORTS
host_run
unset STUB_PS_PORTS
expect_rc "a second mapping beyond loopback next to a loopback one" 1
host_reset
STUB_PS_PORTS="127.0.0.1:8000-8010->8000-8010/tcp" && export STUB_PS_PORTS
host_run
unset STUB_PS_PORTS
expect_rc "a range of ports published on loopback" 0
host_reset
STUB_PS_PORTS="[::1]:8088->8080/tcp" && export STUB_PS_PORTS
host_run
unset STUB_PS_PORTS
expect_rc "loopback in IPv6 and a port that is not published" 0

host_reset
: > "$tmp/no-reboot-required"
touch "$tmp/reboot-flag"
sed -i.bak "s#ITMAS_REBOOT_FILE=.*#ITMAS_REBOOT_FILE=$tmp/reboot-flag#" "$tmp/host.conf" && rm -f "$tmp/host.conf.bak"
host_run
expect_rc "the system asks for a reboot" 1
expect_out_has "the reboot is reported" "asks for a reboot"

host_reset
STUB_UPGRADES="caddy/stable 2.9.1 amd64 [upgradable from: 2.9.0]" && export STUB_UPGRADES
host_run
expect_rc "a Caddy update pending for the first time" 0
expect_exists "the first sighting is recorded" "$state/pending-patches-first-seen"
echo $(($(date +%s) - 8 * 86400)) > "$state/pending-patches-first-seen"
host_run
expect_rc "a Caddy update pending for 8 days" 1
expect_out_has "the pending update is reported" "updates pending for more than 7 days: caddy"
echo $(($(date +%s) - 3 * 86400)) > "$state/pending-patches-first-seen"
host_run
expect_rc "a Caddy update pending for 3 days" 0
unset STUB_UPGRADES
host_run
expect_rc "no update pending" 0
expect_missing "the first sighting is cleared" "$state/pending-patches-first-seen"

# Security updates of the system and Docker updates count too; ordinary updates do not.
host_reset
STUB_UPGRADES="libssl3t64/noble-security 3.0.13 amd64 [upgradable from: 3.0.12];docker-ce/noble 5:28.1.0 amd64 [upgradable from: 5:28.0.0]" && export STUB_UPGRADES
host_run
echo $(($(date +%s) - 8 * 86400)) > "$state/pending-patches-first-seen"
host_run
expect_rc "a security update and a Docker update pending for 8 days" 1
expect_out_has "the security package is named" "libssl3t64"
expect_out_has "the Docker package is named" "docker-ce"
unset STUB_UPGRADES
host_reset
STUB_UPGRADES="docker-compose-plugin/noble 2.40.0 amd64 [upgradable from: 2.39.0]" && export STUB_UPGRADES
host_run
echo $(($(date +%s) - 8 * 86400)) > "$state/pending-patches-first-seen"
host_run
unset STUB_UPGRADES
expect_rc "a Docker plugin update pending for 8 days" 1
expect_out_has "the Docker plugin is named" "docker-compose-plugin"
host_reset
STUB_UPGRADES="vim/noble-updates 2:9.1 amd64 [upgradable from: 2:9.0]" && export STUB_UPGRADES
host_run
unset STUB_UPGRADES
expect_rc "an ordinary update does not alert" 0
expect_missing "an ordinary update is not tracked" "$state/pending-patches-first-seen"

# A failing apt must not hide the pending updates: it is a problem on its own and the first sighting is kept.
host_reset
echo "$(($(date +%s) - 2 * 86400))" > "$state/pending-patches-first-seen"
STUB_APT_FAIL=1 && export STUB_APT_FAIL
host_run
unset STUB_APT_FAIL
expect_rc "apt cannot list the updates" 1
expect_out_has "the failure of apt is reported" "apt list failed"
expect_exists "the first sighting survives a failing apt" "$state/pending-patches-first-seen"

# A configuration that group or others can write is refused: it is executed as shell.
host_reset
chmod 666 "$tmp/host.conf"
host_run
expect_rc "a world-writable host.conf is refused" 1
expect_out_has "the refusal names the reason" "writable by group or others"
chmod 600 "$tmp/host.conf"
if [ "$(id -u)" -ne 0 ]; then
  host_reset
  chmod 000 "$tmp/host.conf"
  host_run
  chmod 600 "$tmp/host.conf"
  expect_rc "a host.conf that the user cannot read" 1
  expect_out_has "the message says to use sudo" "run this script with sudo"
fi

printf '%d of %d checks passed.\n' "$((checks - failures))" "$checks"
[ "$failures" -eq 0 ]
