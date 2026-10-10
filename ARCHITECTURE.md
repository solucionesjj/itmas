# Arquitectura

Este documento describe la arquitectura de la fundación. Las decisiones de mayor impacto tienen su ADR
en [`specs/architecture/`](specs/architecture/); las reglas que no pueden romperse están en
[`constitution.md`](constitution.md).

## 1. Vista general

```text
                                   ┌──────────────────────────── red "edge" (bridge) ─────────────────────────────┐
 Navegador ── 127.0.0.1:8080 ──▶   │  frontend (nginx unprivileged)                 api (ASP.NET Core 10)         │ ──▶ Microsoft Entra ID
                                   │   /                → SPA estática               /auth/*   BFF (sesión)        │     (OIDC, salida HTTPS)
                                   │   /api, /auth,                                  /api/*    API REST            │
                                   │   /signin-oidc, ... → proxy ─────────────────▶  /health*  (solo red interna)  │
                                   └───────────────────────────────────────────────────────┬──────────────────────┘
                                                                                           │
                                   ┌──────────────────── red "data" (internal: sin salida a Internet) ───────────┐
                                   │  migrator (one-shot, EF bundle) ──▶ postgres (PostgreSQL 18) ◀── api        │
                                   └─────────────────────────────────────────────────────────────────────────────┘
```

| Componente | Responsabilidad | Publicado |
|---|---|---|
| `frontend` | Sirve la SPA y actúa como **reverse proxy** (único origen público). Aplica CSP y cabeceras de seguridad de los documentos. | Sí, solo en `127.0.0.1` |
| `api` | API REST, Backend-for-Frontend de autenticación, autorización, health checks. | No |
| `migrator` | Aplica migraciones EF Core y termina. Único proceso con privilegios DDL. | No |
| `postgres` | Persistencia. Red interna sin salida. | No |
| `itmas-agent` (fuera de Docker) | Agente de inventario instalado como servicio en los equipos (SPEC-0002). Envía su inventario por HTTPS al mismo origen público. | No (solo conexiones salientes) |

nginx también sirve los paquetes del agente en `/downloads/agent/` desde un volumen de solo lectura (`AGENT_DOWNLOADS_DIR`).

## 2. Frontend (Angular 22 + Angular Material / Material Design 3)

Aplicación *standalone*, *zoneless*, con signals y carga diferida de rutas.

```text
frontend/src/app/
├── core/                    Servicios singleton y transversales (sin UI de features)
│   ├── config/              APP_CONFIG (valores públicos), TitleStrategy
│   ├── services/            ThemeService, NotificationService, BrowserNavigation
│   ├── interceptors/        csrfInterceptor, httpErrorInterceptor
│   ├── error-handling/      GlobalErrorHandler
│   └── models/              ProblemDetails, ApiError
├── auth/                    Sesión BFF (sin tokens en el navegador) y permisos para UX
│   ├── services/            AuthService (sesión, isRoot, hasPermission en signals)
│   ├── guards/              authGuard, guestGuard, permissionGuard
│   ├── directives/          *appIfPermission
│   ├── interceptors/        sessionExpiryInterceptor (401 → re-login)
│   └── models/              SessionState, UserProfile
├── layout/                  Marco de la aplicación autenticada
│   ├── shell/               Top app bar + navigation drawer + <router-outlet>
│   ├── public-layout/       Marco de las páginas públicas (manual): barra con tema e "Iniciar sesión"
│   ├── navigation/          NAVIGATION_ITEMS (punto de extensión)
│   ├── theme-toggle/        Selector Claro / Oscuro / Sistema
│   └── user-menu/           Cuenta y cierre de sesión
├── shared/                  Componentes de presentación reutilizables (sin estado global)
│   ├── components/          status-message, confirm-dialog
│   └── pipes/               bytes (tamaños en unidades binarias)
├── features/                Una carpeta por área funcional, cargada de forma diferida
│   ├── home/  login/  errors/
│   ├── authorization/       Administración (SPEC-0001): users/, policies/, permissions/, audit/, data/ (API y modelos)
│   ├── inventory/           Inventario (SPEC-0003): devices/ (listado, alta, código), history/ (registros),
│                            report/ (informe por secciones), data/ (API, modelos, etiquetas)
│   └── manual/              Manual de uso público (SPEC-0004): índice con búsqueda y página por proceso
├── app.routes.ts            Rutas (lazy), guards
└── app.config.ts            Providers: router, HttpClient + interceptores, inicializadores
```

**Reglas de dependencia del frontend:** `features` → `shared`, `core`, `auth`; `layout` → `core`, `auth`;
`shared` no depende de `features` ni de `layout`; `core` no depende de nada de lo anterior.

**Flujo HTTP** (orden de interceptores): `csrfInterceptor` añade `X-CSRF: 1` a las peticiones del mismo origen →
`httpErrorInterceptor` normaliza errores a `ApiError` (Problem Details) y notifica los errores no recuperables →
`sessionExpiryInterceptor` convierte un `401` de `/api` en "sesión expirada" y redirige a `/login`.

**Temas.** `styles.scss` define un único tema M3 (`mat.theme` con paletas por defecto `azure`/`blue`, Roboto,
densidad 0). `mat.theme` emite valores `light-dark()`; `html { color-scheme: light dark }` sigue al sistema y
`ThemeService` fija `light` o `dark` con `<html data-theme>`. No hay tokens ni componentes personalizados.

**Manual de uso** ([ADR-0013](specs/architecture/adr-0013-user-manual.md)). Su contenido son archivos estáticos en
`frontend/public/manual/` (`index.json` + un `.md` por proceso) que el build copia a `/manual/`. La ruta `/manual` es
pública y está fuera del `Shell`. El Markdown se convierte con `marked` y se inserta con `[innerHTML]`, que Angular sanea.
nginx sirve `/manual/*.md|json` como estáticos (`no-cache`, `404` si no existen) y cualquier otra ruta de `/manual/`
como la SPA.

**Configuración.** `src/environments/*.ts` solo contiene valores públicos. Como la API está en el mismo origen,
el frontend **no necesita ninguna configuración de Entra ID**.

## 3. Backend (ASP.NET Core 10, Clean Architecture)

```text
backend/
├── src/
│   ├── Itmas3.Domain/          Modelo de negocio puro. Sin dependencias. (Entity<TId>, DomainException)
│   │   ├── Authorization/      User, Policy, PolicyStatement, PermissionDefinition (SPEC-0001)
│   │   └── Inventory/          Device (registro + enrolamiento, SPEC-0002/0003), InventoryReport
│   ├── Itmas3.Application/     Casos de uso y puertos. Depende solo de Domain.
│   │   ├── Abstractions/       ICurrentUser, IPermissionEvaluator (puertos)
│   │   ├── Authorization/      Convención de nombres de permisos
│   │   ├── Common/Exceptions/  NotFoundException
│   │   ├── Identity/GetCurrentUserProfile/   Caso de uso (handler + DTO)
│   │   ├── Authorization/      Catálogo de permisos, evaluador, salvaguardas, casos de uso
│   │   │                       (Users/, Policies/, Permissions/) y puerto de consultas (Queries/)
│   │   └── Inventory/          EnrollDevice, SubmitInventoryReport, AuthenticateDevice, PurgeHistory, DeviceSecrets,
│   │                           ManageDevices/ (registro), Queries/ (listado, historial, informe), permisos
│   ├── Itmas3.Infrastructure/  Adaptadores: EF Core/PostgreSQL, Data Protection, evaluador de permisos
│   │   ├── Persistence/        AppDbContext, DatabaseOptions, Configurations/, Migrations/
│   │   ├── Authorization/      Repositorios, unidad de trabajo auditada, instantáneas en caché, consultas
│   │   └── Inventory/          Repositorios, consultas del inventario, unidad de trabajo con auditoría
│   └── Itmas3.Api/             Presentación y composition root
│       ├── Controllers/        Controllers delgados (Authorization/: users, policies, permissions, audit-events;
│       │                       Inventory/: devices, devices/{id}/reports, device-enrollments, inventory-reports)
│       ├── Inventory/          Esquemas de autenticación de dispositivos, políticas, rate limiting, purga del histórico
│       ├── Contracts/          DTOs HTTP (request/response)
│       ├── Authentication/     BFF OIDC + cookie de sesión
│       ├── Authorization/      Políticas, [RequirePermission], handler
│       ├── ErrorHandling/      GlobalExceptionHandler (Problem Details)
│       ├── Security/           Cabeceras, CSRF, CORS, rate limiting, forwarded headers
│       ├── HealthChecks/       /health, /health/live, /health/ready
│       ├── Observability/      Logging HTTP, OpenTelemetry opcional
│       └── Composition/        Registro de servicios y pipeline
└── tests/
    ├── Itmas3.UnitTests/         Dominio, aplicación, reglas de arquitectura y convenciones
    └── Itmas3.IntegrationTests/  Pipeline HTTP real + PostgreSQL desechable (Testcontainers)
```

**Regla de dependencias** (verificada por `LayerDependencyTests`): `Api → Infrastructure → Application → Domain`.
`Application` define puertos (interfaces) que `Infrastructure` implementa; `Api` solo los compone.

**Controllers delgados.** Un controller valida el contrato HTTP, invoca un caso de uso de `Application` y
traduce el resultado. No contiene lógica de negocio ni acceso a datos. Los casos de uso son clases *handler*
registradas en `Application/DependencyInjection.cs` (sin mediator: no aporta valor a este tamaño).

### Pipeline HTTP del backend

El orden importa y está en `Composition/RequestPipeline.cs`:

1. `UseForwardedHeaders`: confía en `X-Forwarded-*` solo desde la red del proxy (`ReverseProxy:KnownNetworks`).
2. `UseExceptionHandler` + `UseStatusCodePages`: todo error termina en Problem Details.
3. `UseHsts` (fuera de Development) y `UseHttpsRedirection` (opcional).
4. `SecurityHeadersMiddleware`: cabeceras defensivas, `Cache-Control: no-store`.
5. `UseHttpLogging`: método, ruta, estado y duración. Nada más.
6. `UseRouting` → `UseCors` → `UseAuthentication` → `SessionValidationMiddleware` (anula sesiones de usuarios
   inactivos o con marca de seguridad rotada) → `UseRateLimiter` → `UseAuthorization`.
7. `CsrfProtectionMiddleware`: exige `X-CSRF` en peticiones mutantes a `/api` y valida `Origin`.
8. Endpoints: controllers, health checks y OpenAPI (solo Development).

## 4. Base de datos (PostgreSQL 18 + EF Core 10)

- Un `AppDbContext` con esquema por defecto `app` y convención de nombres `snake_case`.
- Cada tipo persistido tiene su `IEntityTypeConfiguration<T>` en `Persistence/Configurations/`.
- **Roles** (creados por [`database/init/01-create-roles.sh`](database/init/01-create-roles.sh)):
  - `itmas3_migrator`: dueño de los esquemas `app` y `app_migrations`; solo lo usa el contenedor `migrator`.
  - `itmas3_app`: rol de la API; solo `SELECT/INSERT/UPDATE/DELETE` en `app` (por *default privileges*).
    No puede ejecutar DDL ni leer o alterar el historial de migraciones.
- **Migraciones**: EF Core *migration bundle* ejecutado por el servicio `migrator` antes de que arranque la API
  (`depends_on: service_completed_successfully`). Nunca se migra desde la API.
- **Entidad técnica**: `app.data_protection_keys` (key ring de ASP.NET Core Data Protection) para que la
  cookie de sesión sobreviva reinicios y réplicas.
- **Autorización (SPEC-0001)**: `users`, `user_external_identities`, `policies`, `policy_statements`,
  `policy_assignments`, `permissions`, `authorization_state` (versión para invalidar cachés) y `audit_events`
  (*append-only*). Detalle en [specs/database/README.md](specs/database/README.md).
- **Inventario (SPEC-0002, SPEC-0003)**: `devices` (registro del equipo, estado `pending`/`active`/`deleted`, hash del
  código de enrolamiento y de la API key, resumen del último inventario; nombre e identificador de hardware únicos
  entre equipos no eliminados mediante índices parciales; `xmin` como token de concurrencia) e `inventory_reports`
  (inventario completo en `jsonb`, histórico de 90 días).

### Modelo de autorización

```text
Usuario ──(asignaciones)──▶ Políticas (activas) ──(statements Allow)──▶ Permisos (<recurso>.<acción>)
   │                                                                           ▲
   └── IsRoot ─────────────────────── acceso irrestricto ─────────────────────┘
```

`[RequirePermission]` → `PermissionAuthorizationHandler` → `IPermissionEvaluator` (`PolicyPermissionEvaluator`) →
`IAuthorizationSnapshotProvider`. La instantánea se guarda en caché con clave `(versión, usuario)`. Las mutaciones pasan por
`IAuthorizationUnitOfWork`, que en una sola transacción bloquea la versión, aplica el caso de uso, escribe la auditoría
e incrementa la versión ([ADR-0008](specs/architecture/adr-0008-policy-based-authorization.md)).

## 5. Autenticación y comunicación entre componentes

Patrón **Backend-for-Frontend** ([ADR-0002](specs/architecture/adr-0002-authentication-bff.md)):

```text
Navegador                 nginx                 API (BFF)                        Entra ID
   │  GET /home             │                      │                                  │
   │──────────────────────▶ │  index.html (SPA)    │                                  │
   │  GET /auth/session ──▶ │ ───────────────────▶ │ {isAuthenticated:false}          │
   │  (navegación) GET /auth/login?returnUrl=/home │                                  │
   │──────────────────────▶ │ ───────────────────▶ │ 302 authorize?code+PKCE ───────▶ │
   │◀──────────────────────────────────────────────────────────────────── login UI ───│
   │  POST /signin-oidc (form_post: code) ───────▶ │ canjea code + client secret ───▶ │
   │                        │                      │◀──────────── id_token (validado) │
   │◀── 302 /home + Set-Cookie __Host-itmas3-session (HttpOnly; Secure; SameSite=Strict)
   │  GET /api/... (cookie + X-CSRF) ────────────▶ │ autentica cookie → autoriza → responde
```

- El navegador nunca recibe *access*, *refresh* ni *id tokens*; la cookie está cifrada con Data Protection.
- Sesión: expiración por inactividad (60 min, deslizante) y absoluta (8 h). Configurables.
- Todas las respuestas no autenticadas son `401` (nunca redirecciones); solo `/auth/login` inicia OIDC.
- Comunicación interna: HTTP plano dentro de redes privadas de Docker; TLS en el borde (ver SECURITY.md).

### Dispositivos (SPEC-0002, SPEC-0003, ADR-0009, ADR-0012)

Los agentes no usan la cookie BFF. Hay dos esquemas de autenticación que no son por defecto, y cada uno solo lo evalúa
la política que lo nombra:

| Endpoint | Política | Credencial | Límite |
|---|---|---|---|
| `POST /api/v1/device-enrollments` | `DeviceEnrollment` | `Authorization: Enrollment <código del equipo>` (un solo uso; se busca por SHA-256) | 10/min por IP |
| `POST /api/v1/inventory-reports` | `EnrolledDevice` | `Authorization: Device <apiKey>` (se busca por SHA-256) | 12/h por credencial |

Una sesión de usuario no satisface las políticas de dispositivo, y una API key no satisface las de usuario. El agente
envía `X-CSRF: 1`, así que la protección CSRF no cambia. Con cada inventario se guardan dos IPs (ADR-0011):

- La **IP pública declarada**, que el agente obtiene de ipinfo.io.
- La **IP de conexión**, que es `RemoteIpAddress` después de `UseForwardedHeaders`. nginx sobrescribe `X-Forwarded-For`,
  así que el cliente no puede falsearla.

Cada equipo se registra antes en el inventario (`/api/v1/devices`, permisos `devices.*`), que emite su código de
enrolamiento de un solo uso ([ADR-0012](specs/architecture/adr-0012-per-device-enrollment-codes.md)). El código
identifica el equipo: el enrolamiento lo vincula al identificador de hardware del agente, emite la API key y consume el
código. Eliminar el equipo invalida su código y su API key en la misma transacción.

`InventoryHistoryPurgeService` elimina cada 6 h los inventarios más antiguos que la retención configurada, conservando
siempre el último de cada equipo.

## 6. Docker

- Imágenes multi-stage: `backend/Dockerfile` (targets `api` y `migrator`), `frontend/Dockerfile` (targets
  `runtime` y `test`). Imágenes Alpine, usuarios no root, `read_only` + `tmpfs` para `api`/`migrator`,
  `cap_drop: ALL` y `no-new-privileges`.
- Health checks: `pg_isready` por TCP (no se da por lista a PostgreSQL mientras ejecuta la inicialización),
  `/health` de la API (incluye conectividad con PostgreSQL), `/healthz` de nginx.
- Redes: `edge` (proxy ↔ API, con salida para Entra ID) y `data` (`internal: true`).
- Perfil opcional `agent`: `agent-e2e` ejecuta el agente Linux en Ubuntu 24.04 y muestra su inventario, sin enviarlo.

## 6.1 Agente de dispositivo (ADR-0010)

```text
agent/  (solución independiente: no referencia backend/)
├── Itmas3.Agent.Collectors   Contrato v1 (records + JSON source-gen), InventoryCollector, ProcessRunner seguro,
│                             fuentes Windows (registro, WMI) / Linux (/proc, /etc, lsblk, lscpu, apt-mark, ufw, nft,
│                             iptables) / macOS (sw_vers, sysctl, diskutil, fdesetup, dscl, socketfilterfw), ipinfo.io
└── Itmas3.Agent              install / uninstall / status / collect / run; AgentWorker + SchedulePolicy;
                              ItmasApiClient (HTTPS, proxy del sistema); API key protegida (0600 / DPAPI);
                              gestores de servicio: sc.exe, systemd (con sandbox), launchd
```

Se publica *self-contained* en un solo archivo por RID (`win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`)
con `scripts/build-agent.sh`, dentro de Docker. Corre como `LocalSystem` o `root` porque lo necesita para leer el
sistema, con los controles compensatorios de la constitución §13.

## 7. Principios arquitectónicos

| Principio | Cómo se materializa |
|---|---|
| Clean Architecture / SoC | Cuatro proyectos con regla de dependencias verificada por pruebas. |
| SOLID | Puertos en `Application`, adaptadores en `Infrastructure`, DI en `Api`. |
| Secure by Design / Default Deny | BFF, *fallback policy* que deniega, permisos explícitos, mínimo privilegio en BD. |
| Configuration by Environment | `appsettings` sin secretos + variables de entorno con prioridad. |
| API-first | Contratos en `Contracts/`, Problem Details, documento OpenAPI (`/openapi/v1.json` en Development). |
| Container-first | Todo el sistema se levanta con `docker compose up --build`. |
| SDD | Ningún cambio relevante sin especificación (`specs/`); agentes con límites (`agents/`). |

## 8. Desviaciones respecto a la estructura de referencia

| Referencia | Implementado | Justificación |
|---|---|---|
| `backend/src/API`, `Application`, ... | `backend/src/Itmas3.Api`, `Itmas3.Application`, ... | Convención .NET: carpeta = proyecto = espacio de nombres raíz. |
| `backend/tests/UnitTests` | `backend/tests/Itmas3.UnitTests` | Ídem. |
| Carpetas `services/guards/interceptors/models` sueltas | Agrupadas por responsabilidad dentro de `core/` y `auth/` | Evita carpetas globales que crecen sin cohesión; cada concepto vive junto a su dominio técnico. |
| — | Servicio `migrator` adicional | Separa privilegios DDL de la API y evita migraciones concurrentes entre réplicas. |
| — | `specs/_templates/`, `AGENTS.md` | Plantillas para SDD y punto de entrada estándar para herramientas de IA. |

## 9. Cómo crece el sistema

Una especificación aprobada (p. ej. `specs/features/SPEC-0001-user-management.md`) se implementa así:

1. **Domain**: entidades y reglas (`Itmas3.Domain/<Módulo>/`).
2. **Application**: casos de uso y puertos (`Itmas3.Application/<Módulo>/<CasoDeUso>/`).
3. **Infrastructure**: configuración EF (`Persistence/Configurations/`), migración, adaptadores.
4. **Api**: contratos y controller delgado con `[RequirePermission("<recurso>.<acción>")]`; los permisos se declaran
   en un `IPermissionDefinitionProvider` del módulo (DEVELOPMENT.md §8).
5. **Frontend**: `features/<módulo>/` con su `*.routes.ts` lazy (`permissionGuard`), entrada en `NAVIGATION` con `permission`.
6. **Tests** en cada nivel y **documentación** actualizada.
