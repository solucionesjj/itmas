# Desarrollo

## 1. Instalación

| Herramienta | Versión | Necesaria para |
|---|---|---|
| Docker Desktop / Engine + Compose | 29+ / v2+ | Todo el entorno, pruebas de integración (Testcontainers) |
| .NET SDK | 10.0.4xx (fijado en `backend/global.json`) | Backend fuera de contenedores |
| Node.js | 24 LTS (`>=24.15`) | Frontend fuera de contenedores |

```bash
./scripts/init-env.sh
```

Genera `.env` con contraseñas aleatorias. Completa los valores de Entra ID si vas a iniciar sesión.

## 2. Estructura del proyecto

```text
.
├── frontend/              Angular 22 + Material 3 (ver ARCHITECTURE.md §2)
│   ├── src/app/{core,auth,layout,shared,features}
│   ├── nginx/             Plantillas de nginx (proxy + cabeceras de seguridad)
│   └── Dockerfile         Targets: runtime (por defecto), test
├── backend/
│   ├── src/Itmas3.{Domain,Application,Infrastructure,Api}
│   ├── tests/Itmas3.{UnitTests,IntegrationTests}
│   ├── Directory.Build.props / Directory.Packages.props   Reglas de build y versiones centralizadas
│   └── Dockerfile         Targets: api (por defecto), migrator
├── agent/                 Agente de inventario (SPEC-0002, ADR-0010): solución .NET independiente
│   ├── src/Itmas3.Agent{,.Collectors}   Ejecutable (install/uninstall/status/collect/run) y colectores por SO
│   ├── tests/Itmas3.Agent.Tests         Parsers con salidas reales (fixtures/), contrato v1, planificador, instalación
│   ├── packaging/, terms/               Plantillas systemd/launchd/newsyslog y términos versionados
│   └── Dockerfile                       Targets: test, artifacts (paquetes + SHA256SUMS), e2e
├── database/init/         Bootstrap de PostgreSQL (roles de mínimo privilegio)
├── specs/                 Especificaciones SDD, ADRs y plantillas
├── agents/                Definición de agentes especializados
├── scripts/               Utilidades (init-env.sh, build-agent.sh)
├── docker-compose.yml     Entorno completo
└── docker-compose.dev.yml Overlay opcional: publica PostgreSQL en 127.0.0.1 para depurar
```

## 3. Desarrollo con Docker (recomendado)

| Acción | Comando |
|---|---|
| Levantar todo | `docker compose up --build` |
| En segundo plano | `docker compose up -d --build` |
| Estado y salud | `docker compose ps` |
| Logs de la API | `docker compose logs -f api` |
| Reconstruir un servicio | `docker compose up -d --build api` |
| Health de la API (red interna) | `docker compose exec api wget -qO- http://localhost:8080/health` |
| Detener | `docker compose down` |
| Detener y borrar datos | `docker compose down -v` |

> El script `database/init/01-create-roles.sh` solo se ejecuta con el volumen vacío. Si cambias
> `APP_DB_*` en `.env` tras el primer arranque, recrea el volumen (`docker compose down -v`) o
> modifica los roles con una spec de base de datos.

## 4. Desarrollo local fuera de contenedores

**Base de datos** (publicada solo en loopback mediante el overlay de desarrollo):

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d postgres migrator
```

**Backend** (`http://localhost:5080`). Las credenciales se pasan por variables de entorno de la sesión
(o `dotnet user-secrets`); nunca se escriben en `appsettings*.json`:

```bash
cd backend && set -a && . ../.env && set +a && Database__Host=localhost Database__Name=$POSTGRES_DB Database__Username=$APP_DB_USER Database__Password=$APP_DB_PASSWORD Database__SslMode=Disable EntraId__TenantId=$ENTRA_TENANT_ID EntraId__ClientId=$ENTRA_CLIENT_ID EntraId__ClientSecret=$ENTRA_CLIENT_SECRET dotnet run --project src/Itmas3.Api
```

En Development se publica el documento OpenAPI en `http://localhost:5080/openapi/v1.json`.

**Frontend** (`http://localhost:4200`, con proxy de `/api` y `/auth` hacia `:5080` en `proxy.conf.json`):

```bash
cd frontend && npm ci && npm start
```

Para iniciar sesión desde `ng serve`, registra también `http://localhost:4200/signin-oidc` y
`http://localhost:4200/signout-callback-oidc` como Redirect URIs en Entra ID.

## 5. Migraciones (EF Core)

Las herramientas están fijadas en `backend/.config/dotnet-tools.json`:

```bash
cd backend && dotnet tool restore
```

Para crear una migración (no necesita base de datos), ejecútalo desde `backend/`:

```bash
dotnet ef migrations add <NombreDescriptivo> --project src/Itmas3.Infrastructure --startup-project src/Itmas3.Infrastructure --output-dir Persistence/Migrations
```

Para comprobar que el modelo y las migraciones están sincronizados:

```bash
dotnet ef migrations has-pending-model-changes --project src/Itmas3.Infrastructure --startup-project src/Itmas3.Infrastructure
```

Para generar el script SQL idempotente (revisión o DBA):

```bash
dotnet ef migrations script --idempotent --project src/Itmas3.Infrastructure --startup-project src/Itmas3.Infrastructure
```

**Aplicación:** la realiza el servicio `migrator` (EF *migration bundle*) en cada `docker compose up`,
con el rol `itmas3_migrator`, antes de arrancar la API. La API nunca migra.

Reglas (constitution §5): cada migración se deriva de una spec, se revisa el SQL generado, es aditiva
cuando sea posible y nunca se edita una migración ya aplicada en un entorno compartido.

## 6. Pruebas

| Suite | Comando | Requisitos |
|---|---|---|
| Backend: unitarias | `cd backend && dotnet test --project tests/Itmas3.UnitTests` | .NET SDK |
| Backend: integración | `cd backend && dotnet test --project tests/Itmas3.IntegrationTests` | .NET SDK + Docker |
| Backend: todo | `cd backend && dotnet test` | .NET SDK + Docker |
| Frontend | `cd frontend && npm test -- --watch=false` | Node 24 |
| Frontend en Docker | `docker build --target test ./frontend` | Docker |
| Agente | `cd agent && dotnet test` | .NET SDK |
| Agente en Docker + paquetes | `./scripts/build-agent.sh` | Docker |
| Agente Linux en contenedor (inventario sin envío) | `docker compose --profile agent run --rm agent-e2e` | Docker |

Las pruebas usan Microsoft.Testing.Platform (configurado en `backend/global.json`).

- **Unitarias (backend):** dominio, casos de uso, validación de nombres de permisos y **reglas de arquitectura**
  (dependencias entre capas; todo endpoint declara autorización).
- **Integración (backend):** API real con `WebApplicationFactory` contra PostgreSQL 18 desechable
  (Testcontainers) inicializado con el **mismo** script de roles, migrado con el rol migrador y consultado con
  el rol de la app. Endpoints exclusivos de prueba (`TestEndpointsController`) ejercitan comportamiento
  transversal sin añadir funcionalidades falsas al producto. La autenticación se sustituye por un esquema de
  prueba (`X-Test-User`); Entra ID nunca se contacta. `ApiFactory.RegisterDeviceAsync` registra equipos y devuelve
  su código de enrolamiento, y `AgentSimulator` hace las llamadas del agente. `DeviceRegistryMigrationTests` migra una
  base de datos aparte paso a paso para comprobar la transición de datos de `AddDeviceRegistry`.
- **Frontend (Vitest):** servicios, guards, interceptores, pipes y componentes (listado, informe y diálogo del
  inventario, con una API simulada) y la coherencia del manual de uso (`manual-consistency.spec.ts`, que lee
  `public/manual/` con `node:fs`).
- **Agente:** los *parsers* de cada SO se prueban con salidas reales en `agent/tests/Itmas3.Agent.Tests/fixtures/`
  (Ubuntu 22.04/24.04 y Debian 12 capturadas en contenedores, macOS saneadas a partir de un equipo real, Windows con
  valores documentados de WMI). Ninguna prueba contacta ipinfo.io ni la API. El contrato `v1` está en
  `agent/tests/fixtures/contract/inventory-report.v1.json`: el agente debe emitir exactamente esas propiedades y la
  API debe aceptarlas (`ContractTests` y `InventoryTests.Contract_fixture_shared_with_the_agent_is_accepted`). Si
  cambias el contrato, cambia ese archivo y ambos lados en el mismo cambio, y despliega primero la API.
- **Verificación manual del agente:** `itmas-agent collect` en cada plataforma. La instalación como servicio se
  probó con systemd en arm64 nativo; en Windows y macOS es una verificación documentada (SPEC-0002 §17).

## 7. Convenciones

- **Backend:** `TreatWarningsAsErrors`, analizadores .NET, *nullable* habilitado, NuGet Audit (falla ante
  vulnerabilidades conocidas), versiones en `Directory.Packages.props`.
- **Frontend:** TypeScript `strict`, componentes standalone, signals, Prettier (`.prettierrc`), un componente
  por archivo, sin estilos que sobrescriban Material.
- **Commits:** referencian el ID de la spec, p. ej. `SPEC-0001: add user list endpoint`.
- **Idioma:** código, identificadores y comentarios en inglés; documentación y textos de UI en español.

## 8. Añadir permisos a una funcionalidad

1. Declara los permisos de la funcionalidad en un `IPermissionDefinitionProvider` de su módulo
   (`Application/<Módulo>/<Módulo>Permissions.cs`, ver `AuthorizationPermissions.cs`) y regístralo en
   `Application/DependencyInjection.cs`. Nombre: `<recurso>.<acción>`.
2. Protege cada endpoint con `[RequirePermission(<Módulo>Permissions.X)]`.
3. Al arrancar, la API sincroniza el catálogo: los permisos nuevos quedan disponibles para las políticas; los
   eliminados del código se marcan obsoletos (dejan de conceder acceso).
4. En el frontend, usa `permissionGuard` (`data: { permission }`), `*appIfPermission` y `permission` en `NAVIGATION`.
5. Pruebas: el caso permitido (usuario con política), el 403 (sin política) y el 401 (anónimo).

`PermissionCatalogConventionTests` falla si un endpoint usa un permiso no declarado.

### 8.1 Mantener el manual de uso

Toda spec que cambie algo visible para las personas usuarias actualiza el manual en el mismo cambio (constitución
§10.4). La plantilla de spec tiene la sección "Manual de uso" para identificar esos procesos desde el borrador.

1. Cada proceso es un archivo `frontend/public/manual/<id>.md` que empieza por `# <Título>` (el mismo del índice) y
   sigue la plantilla de SPEC-0004 §8.2: para qué sirve, **Quién puede hacerlo** (permiso), pasos, resultado y
   problemas frecuentes. Usa los textos exactos de la interfaz en **negrita**.
2. Regístralo en `frontend/public/manual/index.json`: `id`, `title`, `summary`, `category`, `permissions`, `specs`,
   `updated` (fecha `YYYY-MM-DD`) y `routes` (pantallas que explica). Si cambias un proceso existente, actualiza
   `updated` y añade la spec a `specs`.
3. Para enlazar otro proceso, escribe `[texto](<id-del-proceso>)`.
4. Es público: nada de secretos, códigos, datos personales reales, nombres o IPs internos ni versiones de componentes.
5. `npm test` ejecuta `manual-consistency.spec.ts`, que falla si el índice y los archivos no coinciden, si un enlace
   apunta a un proceso inexistente o si una entrada nueva de la navegación (`NAVIGATION`) no tiene ningún proceso cuyo
   `routes` la incluya.
6. Revísalo en `http://localhost:8088/manual` tras `docker compose up --build -d frontend` (o en `ng serve`).

## 9. Problemas frecuentes

| Síntoma | Causa / solución |
|---|---|
| `port is already allocated` al levantar | Otro proceso usa el puerto: cambia `FRONTEND_PORT` en `.env`. |
| `required variable ... is missing a value` | Falta `.env` o una variable obligatoria: ejecuta `./scripts/init-env.sh`. |
| Botón de login muestra "no disponible" | Entra ID no configurado (normal en Development sin `ENTRA_*`). |
| Sesión cerrada tras actualizar a SPEC-0001 | Las sesiones previas no tienen id de usuario de la aplicación: vuelve a iniciar sesión. |
| El agente dice "El código de autorización no es válido." | Desde SPEC-0003 cada equipo tiene su código de un solo uso: regístralo (o regenera su código) en **Inventario → Equipos**. |
| No veo la sección Administración | Tu usuario no tiene permisos: configura `AUTHZ_BOOTSTRAP_ROOT_OIDS` (README) o pide políticas a un Root. |
| `400 Bad Request - Invalid Hostname` | Se accedió con un host no incluido en `APP_ALLOWED_HOSTS` (p. ej. `127.0.0.1`). Usa `http://localhost:<puerto>`: además, el `redirect_uri` de Entra ID y la cookie de sesión dependen del host. |
| Entra ID: `AADSTS50011` (redirect URI mismatch) | Registra en el App Registration (plataforma Web) exactamente `http://localhost:<FRONTEND_PORT>/signin-oidc` y `/signout-callback-oidc`. |
| `api` unhealthy tras cambiar contraseñas | El volumen conserva los roles antiguos: `docker compose down -v`. |
| Tests de integración: `DockerUnavailableException` | Docker no está en ejecución. |
| La API no arranca en Production | Falta configuración de Entra ID (comportamiento *fail-fast* intencional). |
