#!/usr/bin/env bash
# Shared helpers sourced by every script in this folder. Not meant to be run directly.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${ROOT_DIR}/backend/.env"
ENV_EXAMPLE_FILE="${ROOT_DIR}/backend/.env.example"

cd "${ROOT_DIR}"

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

# Populates the DOCKER_COMPOSE array with the right invocation ("docker compose"
# plugin vs. the legacy standalone "docker-compose" binary) and verifies the
# Docker daemon is actually reachable.
resolve_compose_cmd() {
  require_cmd docker

  if ! docker info >/dev/null 2>&1; then
    log_error "Docker no está corriendo. Inicia Docker Desktop (o el daemon de Docker) e intenta de nuevo."
    exit 1
  fi

  if docker compose version >/dev/null 2>&1; then
    DOCKER_COMPOSE=(docker compose)
  elif command -v docker-compose >/dev/null 2>&1; then
    DOCKER_COMPOSE=(docker-compose)
  else
    log_error "No se encontró 'docker compose' (plugin) ni 'docker-compose' (standalone)."
    exit 1
  fi
}

compose() {
  "${DOCKER_COMPOSE[@]}" "$@"
}

require_env_file() {
  if [ ! -f "${ENV_FILE}" ]; then
    log_error "Falta ${ENV_FILE}."
    log_error "Ejecuta primero: ./scripts/create.sh"
    exit 1
  fi
}
