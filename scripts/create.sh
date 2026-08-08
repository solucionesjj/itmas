#!/usr/bin/env bash
# Prepara el proyecto para poder arrancarlo: verifica requisitos, genera
# backend/.env a partir de backend/.env.example (con secretos aleatorios en
# vez de los placeholders "change-me-*") y construye las imágenes Docker.
#
# Idempotente: si backend/.env ya existe, no lo toca ni lo sobreescribe.
#
# Uso: ./scripts/create.sh

source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/common.sh"

resolve_compose_cmd
require_cmd openssl

if [ -f "${ENV_FILE}" ]; then
  log_info "backend/.env ya existe — no se modifica."
else
  if [ ! -f "${ENV_EXAMPLE_FILE}" ]; then
    log_error "No se encontró ${ENV_EXAMPLE_FILE}."
    exit 1
  fi

  log_info "Creando backend/.env a partir de backend/.env.example..."
  cp "${ENV_EXAMPLE_FILE}" "${ENV_FILE}"

  JWT_ACCESS_SECRET="$(openssl rand -hex 32)"
  JWT_REFRESH_SECRET="$(openssl rand -hex 32)"
  ADMIN_SEED_PASSWORD="$(openssl rand -hex 12)"

  # -i.bak es portable entre BSD sed (macOS) y GNU sed (Linux); el .bak se
  # borra justo después. '|' como delimitador porque los secretos son hex
  # (sin '/'), pero por si acaso evitamos el delimitador '/' habitual.
  sed -i.bak \
    -e "s|^JWT_ACCESS_SECRET=.*|JWT_ACCESS_SECRET=${JWT_ACCESS_SECRET}|" \
    -e "s|^JWT_REFRESH_SECRET=.*|JWT_REFRESH_SECRET=${JWT_REFRESH_SECRET}|" \
    -e "s|^ADMIN_SEED_PASSWORD=.*|ADMIN_SEED_PASSWORD=${ADMIN_SEED_PASSWORD}|" \
    "${ENV_FILE}"
  rm -f "${ENV_FILE}.bak"

  log_success "backend/.env creado con secretos JWT y contraseña de administrador generados aleatoriamente."
  log_warn "Guarda esta contraseña del administrador seed — solo se muestra una vez:"
  printf '\n    usuario:     %s\n    contraseña:  %s\n\n' \
    "$(grep '^ADMIN_SEED_USERNAME=' "${ENV_FILE}" | cut -d= -f2-)" \
    "${ADMIN_SEED_PASSWORD}"
  log_warn "El sistema forzará el cambio de esta contraseña en el primer login (mustChangePassword)."
  log_warn "Revisa también MONGO_URI y el resto de backend/.env antes de ir a producción."
fi

log_info "Construyendo imágenes Docker (backend + frontend)..."
compose build

log_success "Proyecto listo. Ejecuta ./scripts/start.sh para iniciarlo."
