#!/usr/bin/env bash
# End-to-end smoke test of the install/service lifecycle against a REAL
# system: installs a real LaunchDaemon and writes a throwaway (fake, not a
# real credential) entry to the System keychain, validates it, then tears
# both down again — always, even on failure (trap), so it never leaves the
# machine in a different state than it found it.
#
# Meant for a disposable CI runner (macos-latest, wired into
# .github/workflows/ci.yml's agent-macos job) or an operator's deliberate
# manual run — never something invoked implicitly by `pytest` or a plain
# build, since it mutates real system state (see
# integration_smoke/macos/test_real_macos_backend.py's docstring for why
# that suite deliberately does NOT do this itself).
#
# Usage: sudo ./Scripts/macos/smoke-test.sh

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"
cd "${AGENT_ROOT}"

require_root
require_cmd plutil
require_cmd launchctl

BINARY="dist/itmas-agent"
if [ ! -x "${BINARY}" ]; then
  log_error "No se encontró ${BINARY} — ejecuta ./Scripts/macos/build.sh primero."
  exit 1
fi

TEST_INSTALL_DIR="/usr/local/itmas-agent"
PLIST_PATH="/Library/LaunchDaemons/com.ecs-la.itmas.agent.plist"

cleanup() {
  log_info "Limpiando (uninstall)..."
  "${TEST_INSTALL_DIR}/bin/itmas-agent" service uninstall >/dev/null 2>&1 || true
  rm -rf "${TEST_INSTALL_DIR}"
  rm -rf "/Library/Application Support/ITMasAgent"
  rm -rf "/Library/Logs/ITMasAgent"
}
trap cleanup EXIT

log_info "Instalando binario de prueba en ${TEST_INSTALL_DIR}..."
mkdir -p "${TEST_INSTALL_DIR}/bin"
cp "${BINARY}" "${TEST_INSTALL_DIR}/bin/itmas-agent"
chmod 755 "${TEST_INSTALL_DIR}/bin/itmas-agent"

log_info "Configurando con credenciales de prueba (no reales)..."
"${TEST_INSTALL_DIR}/bin/itmas-agent" configure \
  --api-key "smoke-test-device-id.smoke-test-secret" \
  --api-base-url "https://smoke-test.invalid/api/v1" \
  --category collaborator

log_info "Registrando el LaunchDaemon..."
"${TEST_INSTALL_DIR}/bin/itmas-agent" service install

log_info "Validando el plist con plutil..."
plutil -lint "${PLIST_PATH}"

log_info "Verificando que launchctl lo reporte cargado..."
if ! launchctl print system/com.ecs-la.itmas.agent >/dev/null 2>&1; then
  log_error "launchctl no reporta el LaunchDaemon como cargado."
  exit 1
fi

log_info "Ejecutando una corrida --dry-run bajo el binario instalado (sin red real, --dry-run nunca llama a la API)..."
"${TEST_INSTALL_DIR}/bin/itmas-agent" run --now --dry-run

log_success "Smoke test completo — install/service/launchctl/run funcionan de punta a punta."
# cleanup() corre automáticamente al salir (trap EXIT), incluso si algo falló arriba.
