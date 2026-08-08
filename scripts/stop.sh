#!/usr/bin/env bash
# Detiene los contenedores sin eliminarlos (los datos de Mongo persisten en
# el volumen). Para volver a arrancar: ./scripts/start.sh
#
# Uso: ./scripts/stop.sh

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

resolve_compose_cmd

log_info "Deteniendo servicios..."
compose stop

log_success "Proyecto detenido. Los datos se conservan — usa ./scripts/start.sh para reanudar."
