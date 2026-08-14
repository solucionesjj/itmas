# scripts/

Scripts de ciclo de vida del proyecto IT-MAS, sobre Docker Compose (`docker-compose.yml`
en la raíz — mismo mecanismo documentado en `DEPLOYMENT.md`). Pensados para
levantar/detener/limpiar todo el stack (`mongo` + `backend` + `frontend`) con
un solo comando, sin depender de Node/Mongo instalados en la máquina host.

| Script | Qué hace |
|---|---|
| `create.sh` | Setup inicial: verifica Docker, genera `backend/.env` (con secretos JWT y contraseña de administrador aleatorios) si no existe, y construye las imágenes. Idempotente — no toca un `.env` ya existente. |
| `start.sh` | Levanta los tres servicios en segundo plano (`up -d`). Acepta `--build` para reconstruir imágenes antes de arrancar. Espera a que el backend responda en `/api/v1/health` y muestra las URLs. |
| `stop.sh` | Detiene los contenedores sin borrarlos ni borrar datos. Para reanudar: `start.sh`. |
| `delete.sh` | Limpieza completa: contenedores + red + **volumen de Mongo** (`down -v`), borrando todos los datos. Pide confirmación escrita salvo que se pase `--yes`. Con `--images` borra también las imágenes construidas localmente. |
| `recreate.sh` | Atajo para recrear el entorno desde cero: `delete.sh` + `create.sh` + `start.sh` en secuencia. `delete.sh` seguirá pidiendo confirmación escrita antes de borrar. |
| `common.sh` | Helpers compartidos (logging, resolución de `docker compose` vs `docker-compose`, chequeo del daemon). No se ejecuta directamente. |

## Uso típico

```bash
./scripts/create.sh   # una vez
./scripts/start.sh    # cada vez que quieras levantar el proyecto
./scripts/stop.sh     # para pausarlo
./scripts/delete.sh   # para limpiar todo (destructivo)
```

Logs en vivo de cualquier servicio: `docker compose logs -f [servicio]`.

Puertos publicados (ver `docker-compose.yml`/`DEPLOYMENT.md`): frontend en
`8081`, backend en `3100` (Swagger en `3100/api/docs`). Mongo no se publica
al host — solo es alcanzable desde `backend` dentro de la red de Compose.
