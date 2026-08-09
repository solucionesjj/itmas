#!/usr/bin/env bash
# Builds a universal2 (arm64 + x86_64) itmas-agent binary via PyInstaller.
#
# Why this isn't a single `pyinstaller --target-arch universal2` call:
# verified empirically on real hardware that even though python.org's
# universal2 Python interpreter can target universal2, pip-installed
# C-extension dependencies (psutil) come as SINGLE-ARCH wheels matching
# whatever architecture pip resolved on — PyInstaller then fails with
# "is not a fat binary!" trying to merge a single-arch .so into a
# universal2 executable. The actual working approach (confirmed by
# building both and testing the merged binary): build once per
# architecture in its own venv (the x86_64 one via Rosetta 2, arch
# -x86_64), each producing a correctly single-arch PyInstaller binary, then
# `lipo -create` fuses the two into one true universal2 executable.
#
# Usage: ./Scripts/macos/build.sh [--sign <identity>] [--notarize <profile>]
# --sign/--notarize are accepted but no-op today (no Developer ID yet,
# confirmed with the user) — kept as real flags so adding signing later is
# a small change here, not a redesign.

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"
cd "${AGENT_ROOT}"

SIGN_IDENTITY=""
NOTARIZE_PROFILE=""
while [ $# -gt 0 ]; do
  case "$1" in
    --sign) SIGN_IDENTITY="$2"; shift 2 ;;
    --notarize) NOTARIZE_PROFILE="$2"; shift 2 ;;
    *) log_error "Argumento desconocido: $1"; exit 1 ;;
  esac
done

require_cmd python3

ARM64_VENV=".venv-build-arm64"
X86_64_VENV=".venv-build-x86_64"
DIST_DIR="dist"

log_info "Preparando entorno arm64..."
if [ ! -d "${ARM64_VENV}" ]; then
  arch -arm64 python3 -m venv "${ARM64_VENV}"
fi
arch -arm64 "${ARM64_VENV}/bin/pip" install --quiet --upgrade pip
arch -arm64 "${ARM64_VENV}/bin/pip" install --quiet -e ".[dev]"

log_info "Preparando entorno x86_64 (vía Rosetta 2)..."
if ! arch -x86_64 /usr/bin/true >/dev/null 2>&1; then
  log_error "Rosetta 2 no está disponible — instálalo con: softwareupdate --install-rosetta"
  exit 1
fi
if [ ! -d "${X86_64_VENV}" ]; then
  arch -x86_64 python3 -m venv "${X86_64_VENV}"
fi
arch -x86_64 "${X86_64_VENV}/bin/pip" install --quiet --upgrade pip
arch -x86_64 "${X86_64_VENV}/bin/pip" install --quiet -e ".[dev]"

rm -rf build "${DIST_DIR}" itmas-agent-arm64.spec itmas-agent-x86_64.spec

log_info "Construyendo binario arm64..."
arch -arm64 "${ARM64_VENV}/bin/pyinstaller" --onefile --name itmas-agent-arm64 \
  --target-arch arm64 --paths src src/itmas_agent/__main__.py

log_info "Construyendo binario x86_64..."
arch -x86_64 "${X86_64_VENV}/bin/pyinstaller" --onefile --name itmas-agent-x86_64 \
  --target-arch x86_64 --paths src src/itmas_agent/__main__.py

log_info "Fusionando con lipo..."
lipo -create -output "${DIST_DIR}/itmas-agent" \
  "${DIST_DIR}/itmas-agent-arm64" "${DIST_DIR}/itmas-agent-x86_64"
rm -f "${DIST_DIR}/itmas-agent-arm64" "${DIST_DIR}/itmas-agent-x86_64"

if [ -n "${SIGN_IDENTITY}" ]; then
  log_warn "--sign fue pasado pero no hay Developer ID configurado todavía — no-op por ahora."
fi
if [ -n "${NOTARIZE_PROFILE}" ]; then
  log_warn "--notarize fue pasado pero no hay credenciales de notarización todavía — no-op por ahora."
fi

lipo -info "${DIST_DIR}/itmas-agent"
log_success "Binario universal listo en ${DIST_DIR}/itmas-agent"
