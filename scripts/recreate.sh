#!/usr/bin/env bash
# Recrea el entorno desde cero: borra todo (contenedores + red + volumen de
# Mongo), lo vuelve a preparar y lo levanta. Atajo de delete.sh + create.sh +
# start.sh — DESTRUCTIVO: se pierde toda la base de datos.
#
# backend/.env NO se toca: delete.sh no lo borra y create.sh es idempotente,
# así que los secretos JWT y la contraseña del administrador seed se conservan.
#
# Uso:
#   ./scripts/recreate.sh            # pide confirmación escrita
#   ./scripts/recreate.sh --yes      # sin confirmación (automatización/CI)
#   ./scripts/recreate.sh --images   # además borra las imágenes construidas localmente

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

SKIP_CONFIRM=false
# La confirmación la pide este script, no delete.sh: así cancelar aborta toda
# la secuencia en vez de continuar con create.sh + start.sh.
DELETE_FLAGS=(--yes)
for arg in "$@"; do
  case "${arg}" in
    -y|--yes) SKIP_CONFIRM=true ;;
    --images) DELETE_FLAGS+=(--images) ;;
    *)
      log_error "Argumento desconocido: ${arg}"
      exit 1
      ;;
  esac
done

resolve_compose_cmd

if [ "${SKIP_CONFIRM}" != true ]; then
  confirm_destructive_action \
    "Esto eliminará los contenedores, la red y el volumen 'mongo-data', y volverá a construir y levantar el entorno." \
    "Se perderán TODOS los datos (dispositivos, inventarios, usuarios, alertas, audit log)."
fi

log_info "Paso 1/3 — eliminando el entorno actual..."
"${SCRIPT_DIR}/delete.sh" "${DELETE_FLAGS[@]}"

log_info "Paso 2/3 — preparando el proyecto..."
"${SCRIPT_DIR}/create.sh"

log_info "Paso 3/3 — levantando los servicios..."
"${SCRIPT_DIR}/start.sh"

log_success "Entorno recreado desde cero."
