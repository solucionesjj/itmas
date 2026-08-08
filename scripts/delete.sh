#!/usr/bin/env bash
# Elimina contenedores, red y el volumen de datos de Mongo — DESTRUCTIVO:
# borra toda la base de datos (dispositivos, inventarios, usuarios, alertas...).
# Úsalo para una limpieza completa del entorno, no para un simple reinicio
# (para eso usa ./scripts/stop.sh).
#
# Uso:
#   ./scripts/delete.sh              # pide confirmación escrita
#   ./scripts/delete.sh --yes        # sin confirmación (automatización/CI)
#   ./scripts/delete.sh --images     # además borra las imágenes construidas localmente

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

SKIP_CONFIRM=false
REMOVE_IMAGES=false
for arg in "$@"; do
  case "${arg}" in
    -y|--yes) SKIP_CONFIRM=true ;;
    --images) REMOVE_IMAGES=true ;;
    *)
      log_error "Argumento desconocido: ${arg}"
      exit 1
      ;;
  esac
done

resolve_compose_cmd

if [ "${SKIP_CONFIRM}" != true ]; then
  log_warn "Esto detendrá y eliminará los contenedores, la red y el volumen 'mongo-data'."
  log_warn "Se perderán TODOS los datos (dispositivos, inventarios, usuarios, alertas, audit log)."
  printf 'Escribe "eliminar" para confirmar: '
  read -r CONFIRMATION
  if [ "${CONFIRMATION}" != "eliminar" ]; then
    log_info "Cancelado — no se eliminó nada."
    exit 0
  fi
fi

DOWN_FLAGS=(-v --remove-orphans)
if [ "${REMOVE_IMAGES}" = true ]; then
  DOWN_FLAGS+=(--rmi local)
fi

log_info "Eliminando contenedores, red y volumen de datos..."
compose down "${DOWN_FLAGS[@]}"

log_success "Entorno eliminado. Ejecuta ./scripts/create.sh para volver a prepararlo desde cero."
