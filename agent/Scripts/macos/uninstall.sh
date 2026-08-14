#!/usr/bin/env bash
# Removes the LaunchDaemon, the installed binary, the node API key from the
# System keychain, and all local state (config, run-state, retry queue,
# cached inventory). Destructive — asks for confirmation unless --yes.
#
# Usage: sudo ./Scripts/macos/uninstall.sh [--yes]

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

require_root

INSTALL_DIR="/usr/local/itmas-agent"
DATA_DIR="/Library/Application Support/ITMasAgent"
LOG_DIR="/Library/Logs/ITMasAgent"

SKIP_CONFIRM=false
for arg in "$@"; do
  case "${arg}" in
    -y|--yes) SKIP_CONFIRM=true ;;
    *) log_error "Argumento desconocido: ${arg}"; exit 1 ;;
  esac
done

if [ "${SKIP_CONFIRM}" != true ]; then
  log_warn "Esto detiene y elimina el LaunchDaemon, el binario instalado, la API key del Keychain de Sistema y todo el estado local (config, caché, cola de reintento)."
  printf 'Escribe "eliminar" para confirmar: '
  read -r CONFIRMATION
  if [ "${CONFIRMATION}" != "eliminar" ]; then
    log_info "Cancelado — no se eliminó nada."
    exit 0
  fi
fi

if [ -x "${INSTALL_DIR}/bin/itmas-agent" ]; then
  log_info "Desregistrando el LaunchDaemon y borrando la API key del Keychain de Sistema..."
  "${INSTALL_DIR}/bin/itmas-agent" service uninstall || log_warn "No se pudo desregistrar limpiamente (¿ya estaba desinstalado?)."
else
  log_warn "No se encontró ${INSTALL_DIR}/bin/itmas-agent — se omite el paso de desregistro vía CLI."
fi

log_info "Eliminando archivos instalados..."
rm -rf "${INSTALL_DIR}"
rm -rf "${DATA_DIR}"
rm -rf "${LOG_DIR}"

log_success "Desinstalado por completo."
