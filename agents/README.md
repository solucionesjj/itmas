# Agentes especializados

Cada agente es un rol con responsabilidades, límites de escritura y obligaciones de consulta. Puede ser ejecutado
por una persona o por una herramienta de IA. Todos están subordinados a [`constitution.md`](../constitution.md)
y trabajan solo sobre specs en estado `Approved` (salvo `architecture`, que ayuda a redactarlas).

## Formato

Cada archivo `agents/<nombre>.md` empieza con un bloque YAML legible por máquina:

| Campo | Significado |
|---|---|
| `name` | Identificador del agente. |
| `mission` | Responsabilidad en una frase. |
| `owns` | *Globs* de archivos que el agente PUEDE crear o modificar. |
| `reads` | Archivos que DEBE leer antes de actuar. |
| `forbidden` | *Globs* que NO PUEDE modificar (aunque estén en `owns` de otro). |
| `consults` | Agentes cuya revisión es obligatoria cuando se cumple la condición indicada. |
| `triggers` | Señales en una spec o cambio que hacen intervenir a este agente. |

## Enrutamiento

1. Identifica la spec (`SPEC-NNNN`) y su campo `agents`.
2. Para cada archivo a modificar, el agente responsable es el que lo tiene en `owns` (matriz abajo).
3. Si el cambio activa `triggers` de otro agente, ese agente debe revisar (`consults`).
4. `architecture` coordina el plan; `testing` y `security` validan antes de cerrar la spec.

| Ruta | Agente propietario | Revisión obligatoria |
|---|---|---|
| `backend/src/Itmas3.Domain/**`, `Itmas3.Application/**` | backend | architecture (nuevos puertos) |
| `backend/src/Itmas3.Api/**` | backend | security (auth, CSRF, CORS, cabeceras, errores) |
| `backend/src/Itmas3.Infrastructure/**` (excepto Persistence) | backend | architecture |
| `backend/src/Itmas3.Infrastructure/Persistence/**` | database | backend, security (privilegios) |
| `database/**` | database | security |
| `frontend/src/**` | frontend | security (auth, interceptores) |
| `frontend/nginx/**` | frontend | security (CSP, cabeceras, proxy) |
| `backend/tests/**`, `frontend/src/**/*.spec.ts` | testing | agente del código probado |
| `**/Dockerfile`, `docker-compose*.yml`, `.env.example`, `scripts/**` | architecture | security |
| `constitution.md`, `specs/architecture/**` | architecture | security (+ aprobación humana para enmiendas) |
| `specs/security/**`, `SECURITY.md` | security | architecture |
| `README.md`, `ARCHITECTURE.md`, `DEVELOPMENT.md`, `agents/**` | architecture | agente del área afectada |

## Agentes

| Agente | Misión |
|---|---|
| [architecture](architecture.md) | Coherencia arquitectónica, specs, ADRs, plan de implementación, documentación e infraestructura. |
| [backend](backend.md) | Dominio, casos de uso, API y adaptadores .NET. |
| [frontend](frontend.md) | SPA Angular con Material 3 y configuración del servidor web. |
| [database](database.md) | Modelo de persistencia, migraciones, roles y privilegios de PostgreSQL. |
| [security](security.md) | Autenticación, autorización, secretos, amenazas y controles. |
| [testing](testing.md) | Estrategia y suites de pruebas; validación de criterios de aceptación. |
