#!/usr/bin/env bash
# Shared helpers sourced by every script in this folder. Not meant to be run directly.
# Mirrors the style of the root project's scripts/common.sh.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AGENT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

if [ -t 1 ]; then
  COLOR_RED=$'\033[0;31m'
  COLOR_GREEN=$'\033[0;32m'
  COLOR_YELLOW=$'\033[0;33m'
  COLOR_BLUE=$'\033[0;34m'
  COLOR_RESET=$'\033[0m'
else
  COLOR_RED=""; COLOR_GREEN=""; COLOR_YELLOW=""; COLOR_BLUE=""; COLOR_RESET=""
fi

log_info()    { printf '%s[info]%s %s\n'  "${COLOR_BLUE}"   "${COLOR_RESET}" "$1"; }
log_success() { printf '%s[ok]%s %s\n'    "${COLOR_GREEN}"  "${COLOR_RESET}" "$1"; }
log_warn()    { printf '%s[warn]%s %s\n'  "${COLOR_YELLOW}" "${COLOR_RESET}" "$1" >&2; }
log_error()   { printf '%s[error]%s %s\n' "${COLOR_RED}"    "${COLOR_RESET}" "$1" >&2; }

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    log_error "'$1' es necesario y no se encontró en el PATH."
    exit 1
  fi
}

require_root() {
  if [ "$(id -u)" -ne 0 ]; then
    log_error "Este script debe ejecutarse como root (usa sudo) — instala/gestiona un LaunchDaemon de sistema."
    exit 1
  fi
}
