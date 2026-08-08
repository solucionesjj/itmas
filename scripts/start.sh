#!/usr/bin/env bash
# Levanta todo el proyecto (mongo + backend + frontend) vía Docker Compose.
#
# Uso:
#   ./scripts/start.sh            # arranca (usa las imágenes ya construidas)
#   ./scripts/start.sh --build    # reconstruye las imágenes antes de arrancar

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

BUILD_FLAG=()
if [ "${1:-}" = "--build" ]; then
  BUILD_FLAG=(--build)
fi

resolve_compose_cmd
require_env_file

log_info "Levantando servicios (mongo, backend, frontend)..."
# La expansión ${arr[@]+"${arr[@]}"} evita "unbound variable" bajo `set -u`
# cuando el array está vacío, en bash 3.2 (el bash por defecto en macOS).
compose up -d ${BUILD_FLAG[@]+"${BUILD_FLAG[@]}"}

BACKEND_HEALTH_URL="http://localhost:3100/api/v1/health"
if command -v curl >/dev/null 2>&1; then
  log_info "Esperando a que el backend responda en ${BACKEND_HEALTH_URL}..."
  READY=false
  for _ in $(seq 1 30); do
    if curl -sf "${BACKEND_HEALTH_URL}" >/dev/null 2>&1; then
      READY=true
      break
    fi
    sleep 2
  done

  if [ "${READY}" = true ]; then
    log_success "Backend disponible."
  else
    log_warn "El backend no respondió a tiempo. Revisa los logs con: docker compose logs -f backend"
  fi
else
  log_warn "curl no está disponible — no se pudo verificar el arranque del backend."
fi

compose ps

cat <<EOF

${COLOR_GREEN}Proyecto iniciado.${COLOR_RESET}
  Frontend:        http://localhost:8081
  Backend API:     http://localhost:3100/api/v1
  Swagger/OpenAPI: http://localhost:3100/api/docs

Logs en vivo:  docker compose logs -f
Detener:       ./scripts/stop.sh
EOF
