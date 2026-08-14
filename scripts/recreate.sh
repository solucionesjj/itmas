#!/usr/bin/env bash
# Recrea el entorno desde cero: borra todo (contenedores + volumen de Mongo),
# lo vuelve a crear y lo levanta. Atajo de delete.sh + create.sh + start.sh.
#
# delete.sh pedirá confirmación escrita antes de borrar los datos.
#
# Uso: ./scripts/recreate.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"${SCRIPT_DIR}/delete.sh"
"${SCRIPT_DIR}/create.sh"
"${SCRIPT_DIR}/start.sh"
