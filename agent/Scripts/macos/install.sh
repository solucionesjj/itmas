#!/usr/bin/env bash
# Installs itmas-agent as a system LaunchDaemon: copies the built binary to
# a permanent location, stores the node API key in the System keychain,
# writes non-secret config, and registers/loads the LaunchDaemon.
#
# Must run as root (sudo) — it writes to /usr/local, /Library/Application
# Support, /Library/LaunchDaemons, /Library/Logs, and the System keychain.
#
# Usage:
#   sudo ./Scripts/macos/install.sh \
#     --api-key <deviceId>.<secret> \
#     --api-base-url https://itmas.example.com/api/v1 \
#     --category collaborator \
#     [--binary dist/itmas-agent] [--scheduled-hour 9] [--scheduled-minute 0]
#
# The <deviceId>.<secret> comes from running, once, against the backend:
#   npm run device:provision -- --hostname <this-machine-hostname> --category <collaborator|infrastructure>
# (see backend/scripts/provision-device.ts — printed exactly once, not retrievable again).

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"
cd "${AGENT_ROOT}"

require_root

INSTALL_DIR="/usr/local/itmas-agent/bin"
BINARY_SRC="dist/itmas-agent"
API_KEY=""
API_BASE_URL=""
CATEGORY=""
SCHEDULED_HOUR="9"
SCHEDULED_MINUTE="0"

while [ $# -gt 0 ]; do
  case "$1" in
    --api-key) API_KEY="$2"; shift 2 ;;
    --api-base-url) API_BASE_URL="$2"; shift 2 ;;
    --category) CATEGORY="$2"; shift 2 ;;
    --binary) BINARY_SRC="$2"; shift 2 ;;
    --scheduled-hour) SCHEDULED_HOUR="$2"; shift 2 ;;
    --scheduled-minute) SCHEDULED_MINUTE="$2"; shift 2 ;;
    *) log_error "Argumento desconocido: $1"; exit 1 ;;
  esac
done

if [ -z "${API_KEY}" ] || [ -z "${API_BASE_URL}" ] || [ -z "${CATEGORY}" ]; then
  log_error "Uso: sudo $0 --api-key <deviceId>.<secret> --api-base-url <url> --category <collaborator|infrastructure>"
  exit 1
fi

if [ ! -f "${BINARY_SRC}" ]; then
  log_error "No se encontró ${BINARY_SRC} — ejecuta ./Scripts/macos/build.sh primero."
  exit 1
fi

log_info "Instalando binario en ${INSTALL_DIR}..."
mkdir -p "${INSTALL_DIR}"
cp "${BINARY_SRC}" "${INSTALL_DIR}/itmas-agent"
chmod 755 "${INSTALL_DIR}/itmas-agent"

log_info "Guardando configuración y API key en el Keychain de Sistema..."
"${INSTALL_DIR}/itmas-agent" configure \
  --api-key "${API_KEY}" \
  --api-base-url "${API_BASE_URL}" \
  --category "${CATEGORY}" \
  --scheduled-hour "${SCHEDULED_HOUR}" \
  --scheduled-minute "${SCHEDULED_MINUTE}"

log_info "Registrando el LaunchDaemon..."
"${INSTALL_DIR}/itmas-agent" service install

log_success "Instalado. Verifica con: sudo ${INSTALL_DIR}/itmas-agent service status"
log_info "Para una corrida inmediata de prueba: sudo ${INSTALL_DIR}/itmas-agent run --now"
