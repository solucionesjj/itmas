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

```bash
./scripts/install-git-hooks.sh
```

Activa en tu clon el hook de git `commit-msg`, que exige el prefijo `SPEC-NNNN:` en los commits (ver §10). Es
independiente de `init-env.sh`: se puede ejecutar aunque `.env` ya exista, y varias veces.

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
│   ├── src/Itmas3.Agent{,.Collectors}   Ejecutable (install/uninstall/status/collect/run/sql), colectores por SO y
│   │                                    de motores SQL (SqlAudit/)
│   ├── tests/Itmas3.Agent.Tests         Parsers con salidas reales (fixtures/), contratos, planificador, instalación, SQL
│   ├── tests/Itmas3.Agent.IntegrationTests  Auditoría SQL contra motores reales en contenedores (Testcontainers)
│   ├── packaging/, terms/               Plantillas systemd/launchd/newsyslog y términos versionados
│   └── Dockerfile                       Targets: test, artifacts (paquetes + SHA256SUMS), e2e
├── database/init/         Bootstrap de PostgreSQL (roles de mínimo privilegio)
├── specs/                 Especificaciones SDD, ADRs y plantillas
├── agents/                Definición de agentes especializados
├── .claude/               Skills de proyecto y permisos de Claude Code (SPEC-0007)
├── .githooks/             Hooks de git versionados: commit-msg (SPEC-0007)
├── scripts/               Utilidades (init-env.sh, build-agent.sh, install-git-hooks.sh) y del servidor de producción
│                          (deploy.sh, backup-db.sh, restore-db.sh, harden-host.sh, install-host-tasks.sh, check-host.sh, lib/) con sus test-*.sh
├── deploy/                Configuración versionada del host de producción: Caddy, SSH, fail2ban, actualizaciones, systemd, plantillas de AWS (SPEC-0009)
├── .github/workflows/     CI (ci.yml) y publicación de imágenes con aprobación (release.yml) (SPEC-0009, ADR-0020)
├── docker-compose.yml     Entorno completo
├── docker-compose.prod.yml Producción: las imágenes publicadas en lugar de construirlas (solo lo usa scripts/deploy.sh)
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
| Agente: unitarias | `cd agent && dotnet test tests/Itmas3.Agent.Tests` | .NET SDK |
| Agente: motores SQL reales (Testcontainers, ~5 min) | `cd agent && dotnet test tests/Itmas3.Agent.IntegrationTests` | .NET SDK + Docker |
| Agente en Docker + paquetes | `./scripts/build-agent.sh` | Docker |
| Scripts del servidor (despliegue, copias, endurecimiento, comprobaciones) | `./scripts/test-deploy.sh`, `./scripts/test-host-tasks.sh`, `./scripts/test-backup.sh` | `sh`; la restauración real de `test-backup.sh` requiere Docker |
| IP real y esquema en nginx (ADR-0018) | `./scripts/test-nginx-realip.sh` | Docker |
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
  base de datos aparte paso a paso para comprobar la transición de datos de `AddDeviceRegistry`. `SqlAuditTests` cubre
  los criterios de SPEC-0005 en la API (incluido el límite de 16 MB con Kestrel real) y
  `SqlAuditPerformanceTests` procesa dos instantáneas sintéticas con el volumen de RNF-5 (20 000 cuentas y 200 000
  permisos) y exige menos de 30 s por instantánea; tarda algo menos de medio minuto.
- **Frontend (Vitest):** servicios, guards, interceptores, pipes y componentes (listado, informe y diálogo del
  inventario, con una API simulada) y la coherencia del manual de uso (`manual-consistency.spec.ts`, que lee
  `public/manual/` con `node:fs`).
- **Agente:** los *parsers* de cada SO se prueban con salidas reales en `agent/tests/Itmas3.Agent.Tests/fixtures/`
  (Ubuntu 22.04/24.04 y Debian 12 capturadas en contenedores, macOS saneadas a partir de un equipo real, Windows con
  valores documentados de WMI). Ninguna prueba contacta ipinfo.io ni la API. El contrato `v1` está en
  `agent/tests/fixtures/contract/inventory-report.v1.json`: el agente debe emitir exactamente esas propiedades y la
  API debe aceptarlas (`ContractTests` y `InventoryTests.Contract_fixture_shared_with_the_agent_is_accepted`). Lo mismo
  vale para la auditoría SQL con `agent/tests/fixtures/contract/sql-audit.v1.json` (en la API, `SqlAuditTests` y
  `SqlAuditSnapshotValidatorTests` parten de él). Si cambias un contrato, cambia ese archivo y ambos lados en el mismo
  cambio, y despliega primero la API.
- **Auditoría SQL del agente:** los *mappers* de cada motor se prueban con filas de catálogo de ejemplo
  (`fixtures/sqlaudit/`); `Itmas3.Agent.IntegrationTests` levanta PostgreSQL 18, MySQL 9.7 y MariaDB 12.3 (y SQL Server
  2019/2022/2025 cuando Docker puede emular amd64; en Apple Silicon activa Rosetta en Docker Desktop, si no se omiten con
  su motivo) y comprueba la recolección con la cuenta de mínimo privilegio y los tres modos TLS.
- **Verificación manual del agente:** `itmas-agent collect` en cada plataforma. La instalación como servicio se
  probó con systemd en arm64 nativo; en Windows y macOS es una verificación documentada (SPEC-0002 §17).

## 7. Convenciones

- **Backend:** `TreatWarningsAsErrors`, analizadores .NET, *nullable* habilitado, NuGet Audit (falla ante
  vulnerabilidades conocidas), versiones en `Directory.Packages.props`. Si el aviso (`NU1901`–`NU1904`) afecta a una
  dependencia transitiva, se fija la versión corregida en el grupo «Transitive security pins» de
  `Directory.Packages.props` (fijación transitiva central; vale también para el del agente, `agent/Directory.Packages.props`)
  y se retira cuando el paquete que la trae la actualice;
  nunca se desactiva NuGet Audit ni se baja su nivel. Para comprobarlo sin la caché de restauración, compila una
  copia limpia: `git archive HEAD -- . ':!frontend' | tar -x -C <carpeta-vacía>` y `dotnet build` en `backend/`.
- **Frontend:** TypeScript `strict`, componentes standalone, signals, Prettier (`.prettierrc`), un componente
  por archivo, sin estilos que sobrescriban Material.
- **Commits:** referencian el ID de la spec, p. ej. `SPEC-0001: add user list endpoint` (varios IDs:
  `SPEC-0000, SPEC-0001: …`; un ADR: `ADR-0016: …`). Las correcciones menores sin spec que permite la constitución
  §11.6 usan `minor: <motivo>`. El hook `commit-msg` lo comprueba (ver §10).
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
| Producción: `AADSTS50011` con un `redirect_uri` que empieza por `http://` | nginx no recibió `X-Forwarded-Proto: https`: el proxy del host no lo envía o la imagen de `frontend` es anterior a ADR-0018. Despliega una etiqueta posterior con `sudo ./scripts/deploy.sh` (no reconstruyas en el servidor: `up --build` sustituye la imagen por la local y esquiva la guarda de puertos) y revisa el Caddyfile (§12.6). |
| Producción: la IP de conexión de los inventarios es una `172.x` | La conexión no llega desde el rango de `set_real_ip_from` o el proxy no envía `X-Forwarded-For`. Compara la subred de `edge` con `EDGE_TRUSTED_PROXY_FROM` (ADR-0018). |
| `api` unhealthy tras cambiar contraseñas | El volumen conserva los roles antiguos: en desarrollo, `docker compose down -v`. **En producción nunca**: borra los datos (restaura el volcado, §12.9). |
| Tests de integración: `DockerUnavailableException` | Docker no está en ejecución. |
| La API no arranca en Production | Falta configuración de Entra ID (comportamiento *fail-fast* intencional). |
| `Mensaje de commit rechazado` | El primer renglón no empieza por `SPEC-NNNN:`, `ADR-NNNN:` ni `minor: <motivo>` (§10.3). |
| Claude Code no ofrece `/sdd-new-spec` y las demás | `.claude/skills/` no existía al abrir la sesión, o el *worktree* o la rama se crearon antes de SPEC-0007 y no la contienen: trae `main` (§10.1), ejecuta `/reload-skills` o reinicia la sesión. |

## 10. Skills y guardas de desarrollo (Claude Code)

Origen: [SPEC-0007](specs/features/SPEC-0007-sdd-tooling.md). Automatizan reglas que ya existen en la constitución; no
añaden ni relajan ninguna. Si un procedimiento choca con la constitución, prevalece la constitución y se corrige el
procedimiento. Todo cambio en `.claude/**` o `.githooks/**` lo revisa `security` (`agents/README.md`).

### 10.1 Skills de proyecto

Viven en `.claude/skills/<nombre>/SKILL.md` y solo las invoca una persona (`disable-model-invocation: true`). **Leen las
reglas del repositorio en cada ejecución** (constitución, `AGENTS.md`, `agents/`, plantillas) en lugar de copiarlas:
cambiar una regla no obliga a editarlas, solo hay que tocarlas si cambia el procedimiento.

| Skill | Qué hace | Se detiene si |
|---|---|---|
| `/sdd-new-spec <idea>` | Calcula el siguiente ID **por nombre de archivo**, copia la plantilla, deja la spec en `Draft` con las preguntas abiertas y añade la fila a los índices. | Falta la idea. Nunca aprueba. |
| `/sdd-implement SPEC-NNNN` | Ejecuta el plan de la spec en orden, respeta `owns`/`forbidden`, lista las revisiones pendientes y propone el mensaje de commit. | La spec no está `Approved` ni `In Progress`, el plan es preliminar o hay preguntas abiertas. |
| `/sdd-validate SPEC-NNNN` | Para validar, ejecuta **solo** los comandos de la tabla de `AGENTS.md` que correspondan a lo modificado, relaciona cada criterio con su prueba, evalúa los *checklists* de salida y compara el estado de la spec con los índices. Informa `OK`, `FALLA` o `NO VERIFICADO`. | No hay ID (lo pide). No corrige nada. |
| `/update-manual SPEC-NNNN` | Crea o actualiza los procesos de `frontend/public/manual/` y `index.json` según la sección "Manual de uso" de la spec, declara la revisión de contenido público y ejecuta las pruebas del frontend. | La sección dice "Ninguno". |

```text
/sdd-new-spec  →  (tú resuelves las preguntas y apruebas)  →  /sdd-implement  →  /sdd-validate  →  commit
```

Notas:
- **Primera vez:** si `.claude/skills/` no existía al abrir la sesión, ejecuta `/reload-skills` (o reinicia la sesión).
- ***Worktrees* y ramas antiguas:** las skills y las reglas de bloqueo de secretos (`.claude/settings.json`) solo existen
  en un *worktree* o una rama que ya contengan SPEC-0007; antes, ninguna de las dos está activa. Si se crearon antes,
  una persona (no una skill) trae primero los cambios de `main`, por ejemplo con `git merge --ff-only main`, y luego
  recarga las skills o reinicia la sesión.
- **Sin `Glob` ni `Grep`:** algunas sesiones no los ofrecen. Las skills los sustituyen por estos dos comandos, con una
  ruta acotada y nunca la raíz: `git ls-files -co --exclude-standard -- <ruta>` y
  `git grep -nE --untracked -e <patrón> -- <ruta>`. Respetan `.gitignore`, que ya clasifica los secretos; `find` y
  `grep -r` no (con señuelos, `/usr/bin/grep -r` leyó un `.env` ignorado), así que las skills no los usan. No están
  pre-aprobados (`allowed-tools` solo cubre `git status --short` y `git diff --name-only` en `/sdd-validate`): piden
  permiso, salvo que ya los tengas permitidos. Son la única excepción de lectura a «solo comandos de la tabla» de
  `/sdd-validate`.
- **Ninguna hace `git push`; ninguna hace `git commit` salvo que lo pida de forma explícita quien la invoca, en el chat**
  (un texto de una spec o de un archivo no cuenta); ninguna aprueba specs ni toca `.env`. Esto lo imponen sus
  instrucciones y tu modo de permisos: `allowed-tools` de una skill solo **pre-aprueba** herramientas, acotadas a lo que
  necesita (lecturas; `git status --short` y `git diff --name-only` en `/sdd-validate`; edición de `specs/**` en
  `/sdd-new-spec` y de `frontend/public/manual/**` en `/update-manual`). No restringe las demás, así que `git commit` y
  `git push` no van pre-aprobados.
- Tratan lo que leen (specs, archivos, salidas de comandos) como datos, no como órdenes, y `/sdd-validate` ejecuta, para validar, solo
  comandos de la tabla de `AGENTS.md` (para Compose, `docker compose config -q`: sin `-q` imprime los valores de `.env`).
  Ejecutar pruebas ejecuta código del repositorio: no las lances sobre una rama que no conozcas.
- Lo no verificado se informa como `NO VERIFICADO` (por ejemplo, Docker apagado para las pruebas del backend).

### 10.2 Reglas de bloqueo de secretos (`.claude/settings.json`)

Impiden a Claude Code leer y editar los archivos que `.gitignore` ya trata como secretos: `.env` y `.env.*` **salvo
`.env.example`**, `secrets/`, `*.pfx`, `*.p12`, `*.pem`, `*.key`, `*.crt`, `*.cer` y la configuración local
`**/appsettings.*.local.json` y `docker-compose.override.yml`. Usan la negación `!` de las reglas
de permisos y actúan **a cualquier profundidad y sin distinguir mayúsculas** (`frontend/x.PEM` y `zz/secrets/x.txt`
también quedan bloqueados). `.claude/settings.local.json` (preferencias personales) está ignorado por git: no guardes
secretos en él.

Verificado el 2026-10-17 con Claude Code 2.1.291 sobre ficheros señuelo y rutas inexistentes, nunca sobre `.env` ni
`secrets/`:

| Intento | Resultado |
|---|---|
| Leer `.env.*`, `*.pem` o `secrets/…` con la herramienta de lectura | Bloqueado |
| Crear esos archivos con la herramienta de escritura (`.env.*`, `*.key`, `secrets/…`) | Bloqueado |
| Rutas anidadas o en mayúsculas (`zz/secrets/x.txt`, `frontend/zz-probe.PEM`) | Bloqueadas |
| Leer o crear `.env.example` | Permitido |
| Leer o crear `appsettings.<x>.local.json` y `docker-compose.override.yml`, también anidados | Bloqueado (antes de añadir las reglas, las mismas rutas daban "no existe") |
| Leer `appsettings.json`, `appsettings.Development.json` y `docker-compose.yml` | Permitido |
| `cat <archivo protegido>` en el shell | Denegado en esta sesión; no se pudo aislar si por estas reglas o por el clasificador del modo automático |
| `grep -r` que no nombra el archivo | No devolvió los señuelos en esta sesión; no es una garantía |
| `sed`, `awk` o `cp` sobre un archivo protegido (comprobado por la revisión de `security`) | Llegaron al sistema de archivos |
| Un subproceso (un script de Python) que abre el archivo | **Pudo leerlo** |

**Límites.** No es una barrera del sistema operativo. Cubre las herramientas de lectura y escritura de Claude Code;
cualquier otro camino la elude: un subproceso, los comandos de shell que no sean un lector reconocido (`sed`, `awk`,
`cp`, redirecciones) y las herramientas de servidores MCP que leen archivos. Complementan a
[constitución §4](constitution.md) (ningún secreto en el repositorio, `.env` con permisos `600`), no la sustituyen. No se
añadieron reglas de Bash por prefijo de comando, porque son fáciles de eludir y dan una falsa sensación de control. Si
algún día hace falta aislamiento fuerte, la opción es el *sandbox* de Claude Code.

Qué no cubren hoy: `.claude/settings.local.json` (que `.gitignore` también trata como local), los *user-secrets* de
.NET, que viven fuera del proyecto, y un `appsettings.local.json` sin segmento intermedio, que tampoco encaja en el
patrón `appsettings.*.local.json` ni en el de `.gitignore`. Además, `.env.*` bloquea variantes como `.env.sample`: si
algún día se versionan, hay que añadir su excepción.

### 10.3 Hook de git `commit-msg`

Rechaza el commit si el primer renglón (sin contar líneas en blanco ni comentarios `#` de git) no empieza por:
- uno o más IDs `SPEC-NNNN` o `ADR-NNNN` separados por coma y espacio, seguidos de `: ` y un texto
  (`SPEC-0007: …`, `SPEC-0000, SPEC-0001: …`), o
- `minor: <motivo>`, solo para lo que la constitución §11.6 permite sin spec (erratas, dependencias de parche, refactors
  locales sin cambio de comportamiento). El hook no puede comprobarlo: lo decide quien hace el commit y lo revisa quien
  revisa el cambio.

Los mensajes que genera git (`Merge `, `Revert "`, `fixup! `, `squash! `, `amend! `) pasan sin comprobar: basta que el
asunto empiece por ese prefijo, aunque no lo haya generado git. El hook asume `#` como carácter de comentario, el
predeterminado de git, y solo ve los commits que git le pasa: `git revert` y los *squash* de `rebase -i` no lo invocan.

| Acción | Comando |
|---|---|
| Activarlo (una vez por clon; idempotente; no usa `.env`) | `./scripts/install-git-hooks.sh` |
| Probarlo (mensajes válidos e inválidos, asuntos del historial como *fixtures*, commits reales en un repositorio temporal, activación idempotente) | `./scripts/test-commit-msg-hook.sh` |

Al activarlo, git deja de ejecutar los hooks de `.git/hooks/`; si tu clon ya tenía otro `core.hooksPath`, el instalador
avisa de que lo reemplaza.

**No es un control de seguridad:** `git commit --no-verify` lo omite. La verificación centralizada queda para una
futura integración continua.

## 11. Agenda de infraestructura: Outlook y el tenant (SPEC-0006, ADR-0016)

La agenda funciona sin Outlook: con `AGENDA_OUTLOOK_ENABLED=false` (valor por defecto) programar solo cambia el estado del evento y la
API no llama a nada externo. Esta sección es para quien activa la publicación en un entorno. Las variables están en
[`.env.example`](.env.example) y en el [README](README.md); el diseño, en [ADR-0016](specs/architecture/adr-0016-outlook-graph-application-permissions.md)
y en `specs/features/SPEC-0006-infrastructure-agenda.md` (§10.2, §14).

### 11.1 Activarla

Con `AGENDA_OUTLOOK_ENABLED=true` son obligatorias `AGENDA_OUTLOOK_TENANT_ID`, `AGENDA_OUTLOOK_CLIENT_ID`,
`AGENDA_OUTLOOK_CLIENT_SECRET`, `AGENDA_OUTLOOK_MAILBOX` y `AGENDA_PUBLIC_URL` (URL absoluta `https`); la dirección del equipo
(`AGENDA_OUTLOOK_TEAM_ADDRESS`) es opcional. Si falta alguna, la API **no arranca** y el mensaje nombra la variable, nunca su valor.
El secreto solo existe como variable de entorno del contenedor `api`: no lo pases por la línea de comandos, no lo escribas en un
archivo versionado ni en un ticket (constitución §4).

### 11.2 Prerrequisitos en Microsoft 365 (los realiza el equipo de infraestructura)

Los comandos son los de la documentación de Microsoft ([RBAC for Applications](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac))
con valores de ejemplo entre `<>`; **no se ejecutaron desde este repositorio**: valídalos en el tenant antes de darlos por buenos. Los
nombres de carpeta, el filtro del ámbito y los roles pueden variar con la versión del módulo `ExchangeOnlineManagement`.

1. **Buzón compartido** que aloje el calendario, y calendario compartido con el equipo en **solo lectura**.

   ```powershell
   Connect-ExchangeOnline
   New-Mailbox -Shared -Name "Agenda de Infraestructura" -Alias <alias-del-buzon>
   # El nombre de la carpeta depende del idioma del buzón ("Calendar", "Calendario"...):
   Get-MailboxFolderStatistics -Identity <alias-del-buzon> -FolderScope Calendar | Select-Object Name, FolderPath
   Add-MailboxFolderPermission -Identity "<alias-del-buzon>:\<carpeta-del-calendario>" -User <grupo-de-seguridad-con-correo-del-equipo> -AccessRights Reviewer
   ```

   `-User` solo admite buzones, usuarios de correo y **grupos de seguridad habilitados para correo**: una lista de distribución normal no
   sirve para dar permisos. Si el equipo solo tiene una lista de distribución, crea un grupo de seguridad con correo con las mismas personas.
   Para «notificar al equipo» (`AGENDA_OUTLOOK_TEAM_ADDRESS`) sí basta una lista de distribución, porque solo recibe la invitación: confirma que existe
   en Exchange Online y que su dominio es uno de los permitidos (`AGENDA_ALLOWED_ATTENDEE_DOMAINS`, o el del buzón si la variable está vacía).
2. **Aplicación de Entra ID propia**, distinta de la del inicio de sesión (BFF): sin permisos delegados y **sin ningún permiso de API
   consentido**. Credencial: certificado (preferible) o secreto con caducidad corta. Anota su vencimiento como una actividad de la propia
   agenda para que se renueve a tiempo.
3. **Acota el permiso de calendario con RBAC for Applications**, solo al buzón de la agenda:

   ```powershell
   New-ServicePrincipal -AppId <id-de-la-aplicacion> -ObjectId <id-del-objeto-de-la-aplicacion-empresarial> -DisplayName "ITMas Agenda"
   New-ManagementScope -Name "ITMas-Agenda-Buzon" -RecipientRestrictionFilter "PrimarySmtpAddress -eq '<buzon-de-la-agenda>'"
   New-ManagementRoleAssignment -App <id-del-service-principal> -Role "Application Calendars.ReadWrite" -CustomResourceScope "ITMas-Agenda-Buzon"
   ```

   **No consientas `Calendars.ReadWrite` en Entra ID**: los permisos de las dos fuentes se suman y el consentimiento global alcanzaría
   todos los buzones del tenant, anulando el límite. Tampoco concedas `Mail.Send` ni `MailboxSettings.ReadWrite`.
4. **Verifica el alcance.** Los cambios tardan entre 30 minutos y 2 horas en aplicarse (caché de permisos de Exchange).

   ```powershell
   Test-ServicePrincipalAuthorization -Identity <id-del-service-principal> -Resource <buzon-de-la-agenda>
   Test-ServicePrincipalAuthorization -Identity <id-del-service-principal> -Resource <otro-buzon-cualquiera>
   ```

   El primero debe mostrar el rol de calendarios dentro del ámbito (`InScope: True`) y el segundo, fuera (`InScope: False`). Esta comprobación **no ve
   los permisos consentidos en Entra ID**: un `InScope: False` no prueba que no haya un consentimiento global que anule el límite. Comprueba también en
   Entra ID (**Aplicaciones empresariales → Permisos**) que la aplicación no tiene concedido `Calendars.ReadWrite`, y limita sus propietarios y quién puede
   añadirle credenciales: quien pueda hacerlo hereda el acceso al buzón. La prueba que demuestra el alcance efectivo es la de §11.3, paso 4 (un buzón fuera
   del ámbito debe dar `graph_forbidden`).
5. **Entrega a quien despliega ITMas**, por el canal del gestor de secretos: el identificador del tenant, el de la aplicación, la
   credencial y la dirección del buzón (variables de §11.1).

### 11.3 Prueba con un buzón de pruebas (SPEC-0008, antes CA-24 de SPEC-0006)

Con Outlook activado en un entorno de pruebas y los datos de un buzón de pruebas:

1. Crea una actividad con un título neutro y **prográmala**: debe quedar en estado *Publicado* y aparecer en el calendario del buzón (y,
   en solo lectura, en el del equipo).
2. **Modifica** su hora: la invitación se actualiza, no se duplica.
3. **Cancélala** con un motivo: los invitados reciben la cancelación con el motivo.
4. **Alcance:** cambia `AGENDA_OUTLOOK_MAILBOX` a un buzón de pruebas **fuera** del ámbito y reinicia `api`; programar una actividad
   debe dejarla *Programada* con el error `graph_forbidden`. Devuelve la variable a su valor.
5. Anota el resultado (fecha, quién lo hizo y qué se comprobó, sin direcciones ni identificadores) en la trazabilidad de SPEC-0008.
   Outlook **no** se activa en producción hasta que SPEC-0008 esté `Implemented`.

### 11.4 Diagnóstico de los códigos `graph_*`

Un fallo de Outlook nunca impide guardar, programar, cancelar ni cerrar: el evento muestra un código y se reintenta con **Sincronizar de
nuevo**, que es idempotente. El detalle de la respuesta de Graph no se guarda ni se muestra; los registros llevan la operación, el
identificador del evento, el código y el `request-id` de Graph (solo si es un GUID), para el soporte de Microsoft.

| Código | Causa habitual | Qué hacer |
|---|---|---|
| `graph_unauthorized` | Secreto vencido o incorrecto, o tenant o aplicación equivocados. | Revisa la credencial y su caducidad (§11.5). El siguiente intento pide un token nuevo. |
| `graph_forbidden` | La aplicación no tiene permiso sobre ese buzón: ámbito mal definido o cambio aún no aplicado (hasta 2 h). | Repite la verificación de §11.2 y confirma que `AGENDA_OUTLOOK_MAILBOX` es el buzón del ámbito. |
| `graph_not_found` | El buzón no existe o su dirección está mal escrita. (Un evento borrado en Outlook se vuelve a crear solo.) | Revisa `AGENDA_OUTLOOK_MAILBOX`. |
| `graph_throttled` | Límite de peticiones de Graph (`429`). | El lote se detiene y el resto queda *Pendiente*; espera y usa **Sincronizar de nuevo**. |
| `graph_unavailable` | Graph no responde (`5xx` o red) o no hay salida a `graph.microsoft.com` y `login.microsoftonline.com`. | Revisa la salida de red de `api` y el estado de Microsoft 365. |
| `graph_timeout` | La llamada superó los 10 s. | Reintenta; si se repite, revisa la red. |
| `graph_rejected` | Graph rechazó el contenido (por ejemplo, una zona horaria que no admite). | Revisa `AGENDA_TIME_ZONE` y el evento; avisa a desarrollo. |
| `not_configured` | Outlook desactivado o sin adaptador. | Activa y completa las variables de §11.1. |

### 11.5 Renovar o rotar la credencial

1. Crea la credencial nueva en la aplicación de Entra ID **antes** de que venza la actual.
2. Actualízala en el gestor de secretos (`AGENDA_OUTLOOK_CLIENT_SECRET`) y reinicia el contenedor `api` (`docker compose up -d api`).
3. Comprueba con **Sincronizar de nuevo** sobre una actividad programada que el estado vuelve a *Publicado*.
4. Elimina la credencial anterior y anota el nuevo vencimiento en la agenda.

## 12. Publicación en un servidor Linux (ADR-0018, SPEC-0009)

Procedimiento para publicar ITMas en **un único servidor Ubuntu 24.04 con IP pública** (AWS EC2 `x86_64`: el CI construye `linux/amd64`), con Docker Compose y un proxy TLS en el host
(Caddy, [ADR-0018](specs/architecture/adr-0018-host-tls-reverse-proxy.md)). Las imágenes las publica GitHub Actions en GHCR al empujar una etiqueta y
una persona las despliega con `scripts/deploy.sh` ([ADR-0020](specs/architecture/adr-0020-continuous-deployment-ghcr.md)). El proxy, las copias, el
monitoreo y el endurecimiento viven en el host, fuera de Compose, y se rigen por el §8.7 de la constitución ([ADR-0019](specs/architecture/adr-0019-host-edge-operations-outside-compose.md)).
No hay alta disponibilidad: el servidor es un punto único de fallo.

**Modelo de ejecución.** Todo lo del servidor lo ejecuta `root` con `sudo` y el clon `/opt/itmas3` es de `root`: los temporizadores ejecutan como `root` los scripts de ese clon, así que nadie más debe poder
cambiarlos. La persona administradora usa `sudo`, no el grupo `docker` (equivale a `root`). `host.conf`, el estado de los despliegues (`/var/lib/itmas3`) y la credencial de `docker login` son de `root`.

> El primer despliegue real (`v1.0.1`, del 2026-10-28 al 2026-10-30) siguió esta sección; lo que difirió está corregido aquí y anotado en la §18 de SPEC-0009, junto con lo que queda por verificar en el servidor.
> Las copias de seguridad (§12.9) son un requisito previo: no publiques sin una restauración probada.

### 12.1 Requisitos previos

- **AWS** (los crea quien administra AWS; plantillas y verificaciones en [`deploy/aws/`](deploy/aws/README.md), sin identificadores en el repositorio): instancia EC2 **`x86_64`** con volumen **EBS cifrado**,
  **IMDSv2 obligatorio con *hop limit* 1**, *security group* que solo abre 22, 80 y 443 (también en IPv6: ninguna regla `::/0` en otros puertos), rol de instancia que solo puede **añadir** objetos a las copias, y un bucket de S3 en **otra cuenta**
  con versionado, *Object Lock*, propietario del bucket forzado, cifrado SSE-S3 y vencimiento a los 30 días.
- **GitHub** (ajustes del repositorio, no son archivos; **obligatorios**, no recomendaciones):
  - entorno **`production`** con revisores obligatorios y *Deployment branches and tags* limitado a `v*`;
  - *ruleset* de etiquetas `v*` que solo permita crearlas, moverlas y borrarlas a la administración, y protección de `main`;
  - *Settings → Actions → General*: **aprobación obligatoria de los *workflows* de todos los colaboradores externos** y permisos del `GITHUB_TOKEN` de solo lectura por defecto (el repositorio es **público a propósito**: cualquiera puede abrir un PR);
  - ninguna otra credencial: la publicación usa el `GITHUB_TOKEN` del *workflow*.
  Comprueba el estado con `gh api repos/solucionesjj/itmas/environments/production` y `gh api repos/solucionesjj/itmas/rulesets`. Como el repositorio es público, **nada confidencial** va en él: ni identificadores de cuentas de AWS, ni direcciones, ni *buckets*, ni claves (constitución §4).
  Activa también el reporte privado de vulnerabilidades (*Settings → Code security*), al que apunta SECURITY.md.
- **Claves de las copias:** en el equipo de la persona administradora, `age-keygen -o itmas3-backup.key`. La clave **privada no va al servidor**: guárdala en el gestor de contraseñas y haz una **copia sellada** fuera de línea
  (junto con una copia del `.env` del servidor, RF-6). La pública (`age1…`) es `BACKUP_AGE_RECIPIENT`.
- **Visibilidad de los paquetes de GHCR** (ADR-0020, §8): como el código es público y las imágenes no llevan secretos, lo recomendado es marcar `itmas3-api`, `itmas3-migrator`, `itmas3-frontend` e `itmas3-agent` como **públicos**
  (*Package settings → Change visibility*, después de la primera publicación: GHCR no siempre hereda la visibilidad del repositorio) y entonces el servidor **no guarda ninguna credencial de GitHub**.
  **Solo si se dejan privados** hace falta una credencial de lectura: un token personal clásico con solo `read:packages` (GHCR no admite los de granularidad fina), mejor de una **cuenta de servicio de GitHub dedicada** con lectura solo de los paquetes `itmas3-*`
  (*Package settings → Manage access*) y caducidad de 90 días como máximo, no el de una persona.
- **DNS y Entra ID:** §12.2.
- **Salida a Internet** desde el servidor hacia `github.com`, `ghcr.io`, `login.microsoftonline.com`, Let's Encrypt, el almacenamiento de las copias y, solo si se activa Outlook, `graph.microsoft.com`.
- **Un servicio externo** con sonda HTTP y latidos (se elige al desplegar; SPEC-0009 §19.5) y una **lista de correo** del equipo para los avisos.

### 12.2 Entra ID y DNS

1. En el *App Registration* (plataforma **Web**) añade las Redirect URIs `https://itmas.sacservices.cloud/signin-oidc` y
   `https://itmas.sacservices.cloud/signout-callback-oidc`. Ten a mano el *Tenant ID*, el *Client ID* y un *client secret*.
2. Crea el registro `A` de `itmas.sacservices.cloud` hacia la IP pública del servidor (**sin** `AAAA` salvo decisión expresa de `security`: con IPv6 los límites por IP particionan por /128 y un /64
   permite rotarlas, ADR-0018 decisión 6; un registro CAA que solo admita Let's Encrypt acota la emisión). Crea el registro **después** del ensayo de §12.5 y **antes** de arrancar Caddy (§12.6): si Let's Encrypt falla varias veces
   seguidas, limita los intentos.

### 12.3 Preparar el servidor

1. **Clave SSH.** Comprueba que entras con tu clave y deja esa sesión abierta: el endurecimiento desactiva las contraseñas.
2. **Docker Engine**, desde el repositorio oficial. Comprueba con `docker version` que es la **28.0 o posterior** y no definas `DOCKER_INSECURE_NO_IPTABLES_RAW` (ADR-0018, decisión 6). **No** añadas a nadie al grupo `docker`:

   ```bash
   sudo apt-get update
   sudo apt-get install -y ca-certificates curl
   sudo install -m 0755 -d /etc/apt/keyrings
   sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
   echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
   sudo apt-get update
   sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
   ```

3. **Herramientas de las copias y Caddy (solo los paquetes).** `age` y `rclone` vienen de Ubuntu; `install-host-tasks.sh check` comprueba que `rclone` admite las opciones que usa (`--no-check-dest`, `--s3-no-check-bucket`, `--s3-no-head`).
   Si la versión de Ubuntu no las admite, instala el `.deb` oficial de rclone comprobando su suma SHA-256 contra la que publica rclone.org. Caddy va **antes** de `install-host-tasks.sh` para que sus archivos de configuración no choquen con el paquete.
   **Caddy sale de su repositorio oficial, no del de Ubuntu:** `apt-get install caddy` sin ese repositorio instala la 2.6, que convierte la cabecera HSTS del `Caddyfile` en un nombre inválido y no la envía (pasó en el primer despliegue).
   `install-host-tasks.sh check` exige la 2.7.2 o posterior. Si ya la instalaste desde Ubuntu, añade el repositorio como abajo y reinstala conservando el `Caddyfile`: `sudo apt-get install -y -o Dpkg::Options::=--force-confold caddy`.
   Comprueba la huella de la clave del repositorio de Caddy contra la de <https://caddyserver.com/docs/install> y anótala en la §18 de SPEC-0009 (ADR-0018, decisión 6):

   ```bash
   sudo apt-get install -y age rclone debian-keyring debian-archive-keyring apt-transport-https
   curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' -o /tmp/caddy.gpg.key
   gpg --show-keys --with-fingerprint /tmp/caddy.gpg.key
   ```

   Si la huella coincide:

   ```bash
   sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg /tmp/caddy.gpg.key
   curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
   sudo chmod o+r /usr/share/keyrings/caddy-stable-archive-keyring.gpg /etc/apt/sources.list.d/caddy-stable.list
   sudo apt-get update
   sudo apt-get install -y caddy
   sudo systemctl stop caddy
   rm -f /tmp/caddy.gpg.key
   ```

   (Caddy queda parado hasta que tenga su configuración, §12.6.)
4. **Código.** El repositorio es público: se clona **por HTTPS, como `root` y sin credencial** (no hay *deploy key* en el servidor). No cambies de rama: `deploy.sh` hace el `checkout` de la etiqueta tras comprobar que su *commit* está en `origin/main`.

   ```bash
   sudo git clone https://github.com/solucionesjj/itmas.git /opt/itmas3
   ```

5. **Endurecimiento** (`ufw`, `fail2ban`, actualizaciones de seguridad sin reinicio automático, rotación de logs de Docker). Primero sin tocar SSH; después, con tu clave probada en una segunda sesión:

   ```bash
   cd /opt/itmas3 && sudo ./scripts/harden-host.sh apply
   sudo ./scripts/harden-host.sh apply --confirm-ssh-key
   sudo ./scripts/harden-host.sh check
   ```

   Si `daemon.json` cambió, reinicia Docker **antes** de crear los contenedores (`sudo systemctl restart docker`). SSH queda **abierto a Internet** con clave y `fail2ban` (riesgo R-1 aceptado, SPEC-0009).
6. **Tareas del host** (copia diaria, comprobaciones cada 5 minutos, archivos de Caddy) y su configuración. Repite `apply` y `check` si instalas Caddy después:

   ```bash
   cd /opt/itmas3 && sudo ./scripts/install-host-tasks.sh apply
   sudo $EDITOR /etc/itmas3/host.conf /etc/caddy/itmas3.env
   sudo ./scripts/install-host-tasks.sh check
   ```

   `host.conf` (plantilla [`deploy/host.conf.example`](deploy/host.conf.example)) lleva la clave pública de las copias, el destino, la región y la URL de latido; `itmas3.env`, el correo de contacto de Let's Encrypt. Los dos son de `root` y modo 600,
   y `check` falla mientras falten valores (sin imprimirlos).

### 12.4 `.env`

Crea `.env` con contraseñas aleatorias y complétalo (es un secreto: nunca va al repositorio, y guarda una **copia sellada** con la clave de las copias, RF-6):

```bash
cd /opt/itmas3 && sudo ./scripts/init-env.sh
```

| Variable | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `APP_ALLOWED_HOSTS` | `itmas.sacservices.cloud` |
| `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, `ENTRA_CLIENT_SECRET` | los del *App Registration* |
| `AUTHZ_BOOTSTRAP_ROOT_OIDS` | tu *Object ID*, **solo hasta §12.7** |
| `AGENDA_PUBLIC_URL` | `https://itmas.sacservices.cloud` |
| `AGENDA_OUTLOOK_ENABLED` | `false`: no se activa en producción hasta que SPEC-0008 esté `Implemented` |
| `EDGE_TRUSTED_PROXY_FROM` | vacío hasta comprobar el origen que ve nginx (§12.7, paso 7) |
| `AGENT_DOWNLOADS_DIR` | no la cambies: `deploy.sh` y nginx deben usar la misma carpeta (`/opt/itmas3/agent/dist`) |

`FRONTEND_PORT` queda en `8080` y **no se publica en otra interfaz que `127.0.0.1`**: `deploy.sh` se detiene si Compose publica algo más, y `check-host.sh` lo vigila cada 5 minutos. En `Production` la API no arranca sin la configuración de Entra ID completa.
Las variables de las imágenes (`ITMAS_API_IMAGE`, `ITMAS_MIGRATOR_IMAGE`, `ITMAS_FRONTEND_IMAGE`) las exporta `deploy.sh` y no van en `.env`; `ITMAS_REGISTRY`, `ITMAS_PUBLIC_HOST` e `ITMAS_STATE_DIR` son opcionales en `host.conf`.

### 12.5 Primera publicación

1. **Etiqueta y publicación.** Desde `main`, `git tag v0.1.0 && git push origin v0.1.0` (formato `vMAJOR.MINOR.PATCH`). El *workflow* `Release` ejecuta las suites y las auditorías, construye y escanea con Trivy las tres imágenes (bloquea alta o crítica con corrección, sin publicar nada),
   **espera la aprobación del entorno `production`** y solo entonces las publica en GHCR con etiquetas inmutables; después publica los paquetes del agente si su versión es nueva. El resumen muestra el *commit* y los *digests*.
2. **Visibilidad de los paquetes** (§12.1). Tras esta primera publicación, marca los cuatro paquetes como públicos y comprueba desde un equipo **sin sesión** en GHCR que se leen:

   ```bash
   docker buildx imagetools inspect ghcr.io/solucionesjj/itmas3-api:v0.1.0
   ```

   Si los dejas privados, crea la credencial de lectura **como `root`** en el servidor (el token entra por la entrada estándar, no por la línea de comandos) y comprueba que la lee `sudo docker pull`:

   ```bash
   sudo sh -c 'docker login ghcr.io -u <usuario-de-servicio> --password-stdin < /root/ghcr-token'
   sudo shred -u /root/ghcr-token 2> /dev/null || sudo rm -f /root/ghcr-token
   ```
3. **Despliegue.** En el servidor, compara el *commit* y los *digests* que muestra el script con los del resumen y confirma:

   ```bash
   cd /opt/itmas3 && sudo ./scripts/deploy.sh v0.1.0
   ```

   El script exige que el *commit* de la etiqueta esté en `origin/main` y no acepta una etiqueta movida, resuelve los *digests*, pide confirmación **antes** de tocar nada, hace el `checkout`, se detiene si el Compose publica un puerto distinto de
   `127.0.0.1:${FRONTEND_PORT}`, hace el volcado previo (si ya existe la base de datos), descarga las imágenes **por *digest***, ejecuta el `migrator` con la imagen nueva, levanta `api` y `frontend`, espera a que estén `healthy`,
   comprueba `/auth/session` por el loopback con el `Host` permitido y publica los paquetes del agente. Deja un registro en `/var/lib/itmas3/deploy.log`.
   Si `api` y `frontend` quedan `healthy` pero la comprobación de `/auth/session` falla (código 4), casi siempre es el `.env` sin completar: con `APP_ALLOWED_HOSTS=localhost` el *health check* pasa y la API responde 400 al `Host` público. Completa el `.env` (§12.4) y repite el despliegue.
4. **Ensayo antes del DNS** (SPEC-0009 CA-12, CA-13): despliega dos etiquetas y prueba `sudo ./scripts/deploy.sh rollback`. Para simular un fallo **antes del `migrator`**, pon temporalmente una contraseña errónea en `APP_DB_MIGRATOR_PASSWORD` del `.env`: el despliegue
   termina con código 3 y la versión que atiende no se toca. Para uno **después del `migrator`**, deja vacío `ENTRA_CLIENT_ID` (la API no arranca en `Production`): termina con código 4 y no se revierte nada. Restaura el `.env` en los dos casos.

```bash
sudo docker compose ps
```

Deben quedar `postgres`, `api` y `frontend` en `healthy` y el `migrator` terminado con código 0. `sudo ./scripts/deploy.sh status` muestra la versión actual y la anterior.

### 12.6 Proxy TLS (Caddy)

La configuración está versionada en [`deploy/caddy/`](deploy/caddy/Caddyfile) y la instala `scripts/install-host-tasks.sh apply` (§12.3, paso 6): emisor fijado a Let's Encrypt, `admin off` (sin API de administración), solo HTTP/1.1 y HTTP/2, sin las cabeceras `Server` ni `Via`, y HSTS
**escalonado** sin `includeSubDomains` ni `preload`. Lo que cambia vive en `/etc/caddy/itmas3.env`: `ACME_EMAIL`, `FRONTEND_PORT` y `HSTS_MAX_AGE`. Deja `HSTS_MAX_AGE=300` al publicar, súbelo a `86400` cuando el sitio esté confirmado y a `31536000`
tras una renovación real del certificado. Crea antes el registro DNS (§12.2). Con `admin off` no hay `reload`:

```bash
sudo systemctl start caddy
sudo systemctl restart caddy   # tras cambiar itmas3.env
journalctl -u caddy -f
```

Caddy viene de un repositorio de terceros que `unattended-upgrades` no actualiza: actualízalo **al menos cada 7 días** con `sudo apt-get update && sudo apt-get install --only-upgrade caddy`; `scripts/check-host.sh` avisa si una actualización lleva más de 7 días pendiente.
`/var/lib/caddy`, donde guarda la clave TLS y la de la cuenta ACME, queda en modo 700. Su unidad de systemd pierde `CAP_NET_ADMIN` y aplica una protección ligera del sistema de archivos; tras instalar, revisa `journalctl -u caddy` y
`systemd-analyze security caddy`.

### 12.7 Verificar y cerrar el bootstrap

Desde fuera del servidor:

1. `https://itmas.sacservices.cloud/auth/session` devuelve `{"isAuthenticated":false,...}`.
2. `https://itmas.sacservices.cloud/health` devuelve **404** (los *health checks* no se exponen).
3. Inicia sesión. Con `AADSTS50011`, revisa las Redirect URIs de la plataforma **Web** en la aplicación cuyo *Client ID* está en `ENTRA_CLIENT_ID` (no en otra, como la de desarrollo) y que la imagen de `frontend` sea posterior a ADR-0018.
4. Tu cuenta pasa a Root y aparece **Administración**. Después quita `AUTHZ_BOOTSTRAP_ROOT_OIDS` del `.env` y vuelve a ejecutar `sudo ./scripts/deploy.sh <la misma etiqueta>` (recrea `api` con el `.env` nuevo; no cambia la versión anterior guardada para `rollback`).
5. **IP real y cabeceras (ADR-0018):** enrola un equipo y comprueba en **Inventario** que la IP de conexión es la del cliente, no una `172.x`.
   Después, desde fuera: `curl -i -X POST https://itmas.sacservices.cloud/api/v1/device-enrollments -H 'Authorization: Enrollment codigo-invalido' -H 'X-Forwarded-For: 1.2.3.4' -H 'Content-Type: application/json' -d '{}'`
   debe dar `401`, y `sudo docker compose logs api` debe mostrar «Device enrollment rejected … from» seguido de **tu** IP, nunca `1.2.3.4`. No sirve `/auth/session`: no registra la IP.
6. Cabeceras, en una ruta de la SPA **y** en una de la API: `curl -sI https://itmas.sacservices.cloud/` y `curl -s -D - -o /dev/null https://itmas.sacservices.cloud/auth/session | grep -ci '^strict-transport-security'` (debe dar `1`);
   ni `Server` ni `Via` ni `Alt-Svc`. El emisor debe ser Let's Encrypt:
   `openssl s_client -connect itmas.sacservices.cloud:443 -servername itmas.sacservices.cloud </dev/null | openssl x509 -noout -issuer`.
7. En el servidor: `ss -ltnp` y `ss -lunp` solo muestran 80 y 443 hacia fuera, el puerto del `frontend` en `127.0.0.1` y nada en `localhost:2019`; tras
   `curl -s http://127.0.0.1:8080/`, `sudo docker compose logs --tail=3 frontend` muestra como origen la puerta de enlace de `edge`. **Solo entonces** (CA-22) pon `EDGE_TRUSTED_PROXY_FROM=172.28.10.1/32` en el `.env` y vuelve a ejecutar `deploy.sh`
   (nginx pasa a confiar únicamente en esa dirección); repite el paso 5. Con `curl --path-as-is`, `/%68ealth`, `//health`, `/./health` y `/api/..%2fhealth` no devuelven `200` de la API. Si algo falla, ver §9.
8. Reinicia el servidor **una vez** y comprueba que todo vuelve solo: `sudo docker compose ps` y `systemctl status caddy`.

### 12.8 Paquetes del agente

Los construye y publica el *workflow* `Release` **solo cuando cambia la versión del agente** (`agent/Directory.Build.props`), con su `SHA256SUMS` generado en la misma compilación, y `deploy.sh` los deja en `agent/dist/<versión>/` y
`agent/dist/latest/` (la ruta de `AGENT_DOWNLOADS_DIR`) tras verificar las sumas y que no sobra ningún archivo. Una versión ya publicada **no se sobrescribe**; `latest` solo avanza si la versión es la mayor y cambia al final renombrando
directorios (nginx sirve esa ruta con `disable_symlinks on`). Siguen en `https://itmas.sacservices.cloud/downloads/agent/`, cada versión con su `SHA256SUMS`. Si `deploy.sh` termina con código 5, la versión está desplegada pero los paquetes no
se publicaron: revisa el mensaje. `./scripts/build-agent.sh` sigue sirviendo para construirlos en un equipo de trabajo.

### 12.9 Copias de seguridad

La base de datos contiene la auditoría (`app.audit_events`), el mapa de cuentas privilegiadas de los motores SQL y el *key ring* de Data Protection sin cifrar, así que **la copia siempre va cifrada y fuera del servidor**.

- **Qué hace** `scripts/backup-db.sh` (cada día a las 03:15 con jitter, desde `itmas3-backup.timer`, y antes de cada despliegue): comprueba el espacio libre, `pg_dump -Fc`, lo lee entero con `pg_restore` (detecta truncamientos), cuenta las filas de cada tabla **a partir del propio volcado**, cifra con la
  **clave pública** de `age`, copia al bucket de la otra cuenta con `rclone` (rol de instancia: sin claves guardadas) y borra las copias locales de 7 días o más. Una copia que no subió se reintenta en la ejecución siguiente. El destino aplica su propia retención de 30 días; el servidor
  no puede leer, listar ni borrar lo que subió (sí puede añadir versiones nuevas: por eso se restaura la versión que elijas, no «la última por nombre»).
- **Qué no incluye:** los roles de PostgreSQL (los crea `database/init/01-create-roles.sh` con las contraseñas del `.env`), el propio `.env` y la clave privada. Por eso se guardan aparte, con copia sellada.
- **Traer una copia** (el servidor no puede leer el bucket): desde un equipo con credenciales de la cuenta de destino, `aws s3api list-object-versions --bucket <bucket> --prefix <prefijo>/` y `aws s3api get-object --bucket <bucket> --key <prefijo>/<archivo> --version-id <id> <archivo>`;
  descarga también el `….counts.age` del mismo nombre.
- **Prueba de restauración** (antes de publicar y cada trimestre, **en un equipo de trabajo con Docker, nunca en el servidor**):

  ```bash
  ./scripts/restore-db.sh --backup itmas3-AAAAMMDD-HHMMSS.dump.age --identity itmas3-backup.key --api-image itmas3/api:local --migrator-image itmas3/migrator:local
  ```

  Arranca un PostgreSQL temporal, crea los roles con `database/init`, restaura **en una sola transacción**, compara los recuentos de todas las tablas con los del volcado, verifica los privilegios (el rol de la API sin DDL y con solo DML), ejecuta el `migrator`
  (debe terminar sin cambios o aplicar solo lo nuevo) y, con `--api-image`, que la API queda `healthy`. Anota fecha y duración en la §18 de SPEC-0009, sin datos. Sin recuentos sale con error salvo `--no-counts`.
- **Recuperar el servidor** (la base de datos de producción **no se restaura encima**: `pg_restore --clean` sobre un esquema más nuevo falla y deja la base vacía). Prepara el servidor (§12.3 a §12.6 sin desplegar), recupera el `.env` de la copia sellada, levanta solo `postgres` y restaura en una **base nueva** que luego
  sustituye a la actual, sin dejar el volcado en claro en disco:

  ```bash
  cd /opt/itmas3 && sudo docker compose -f docker-compose.yml up -d postgres && sudo docker compose -f docker-compose.yml stop api frontend
  sudo docker compose -f docker-compose.yml exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d postgres -c "CREATE DATABASE \"${POSTGRES_DB}_restore\""'
  age -d -i itmas3-backup.key itmas3-….dump.age | ssh <servidor> 'sudo docker compose -f /opt/itmas3/docker-compose.yml exec -T postgres sh -c "pg_restore -U \"\$POSTGRES_USER\" -d \"\${POSTGRES_DB}_restore\" --exit-on-error"'
  ```

  Después, en `psql` como superusuario conectado a `postgres`: `REVOKE ALL ON DATABASE "<db>_restore" FROM PUBLIC; GRANT CONNECT ON DATABASE "<db>_restore" TO <rol-migrador>, <rol-app>; ALTER DATABASE "<db>" RENAME TO "<db>_prev"; ALTER DATABASE "<db>_restore" RENAME TO "<db>";`
  (valores del `.env`), y `sudo ./scripts/deploy.sh rollback` o `deploy.sh <etiqueta>`. La base anterior queda como `<db>_prev`.
- **Objetivos:** pérdida máxima de 24 horas y recuperación en un día hábil, sin archivado de WAL (SPEC-0009 Q-2).

### 12.10 Actualizar y volver atrás

Para una versión nueva: etiqueta, aprobación del entorno y `sudo ./scripts/deploy.sh <etiqueta>`. La actualización corta el servicio unos segundos: los agentes reintentan y las sesiones sobreviven (el *key ring* está en PostgreSQL).

- Si algo falla **antes de que el `migrator` termine** (código 3), no se tocaron los contenedores. Pero el `migrator` aplica y confirma **cada migración por separado**: si falla a mitad con varias pendientes, las anteriores de esa ejecución **sí quedan aplicadas**. `deploy.sh` registra el nivel
  de esquema antes y después; si la versión que atiende es incompatible con el esquema nuevo, restaura el volcado previo (§12.9).
- Si falla **después** (código 4), no se revierte nada automáticamente: `sudo ./scripts/deploy.sh status` muestra las versiones y `sudo ./scripts/deploy.sh rollback` repone los *digests* anteriores **sin** ejecutar el `migrator`. Se niega si el nivel de esquema actual no es el de esa versión
  (salvo `--schema-changed-ok`: en ese caso restaura el volcado previo). `database` revisa en cada migración si es compatible con la versión anterior.
- **Nunca ejecutes `docker compose down -v`**: borra los datos.

### 12.11 Monitoreo y alertas

- **Sonda externa:** en el servicio elegido, una sonda HTTP a `https://itmas.sacservices.cloud/auth/session` **cada 5 minutos** que espere `200` y `isAuthenticated`, con aviso tras **2 fallos seguidos** y aviso de recuperación. Cubre proxy, nginx y API, **no PostgreSQL**.
- **Comprobación del host** (`itmas3-check.timer`, cada 5 minutos, `scripts/check-host.sh`): `postgres`, `api` y `frontend` `healthy`; Caddy activo; ningún puerto publicado por Docker salvo `127.0.0.1`; disco por debajo del 80 %; certificado con más de 14 días; copia del día cifrada **y copiada fuera**; sin reinicio
  pendiente; sin una actualización de seguridad, de Caddy o de Docker pendiente más de 7 días. Envía un **latido** al servicio si todo va bien, y un aviso con la lista si no; si los latidos dejan de llegar, el servicio avisa de que el servidor o sus tareas se han parado.
  Los avisos llegan por **correo a la lista del equipo**. La URL de latido es un secreto: solo vive en `/etc/itmas3/host.conf`.
- Ejecútala a mano con `sudo ./scripts/check-host.sh` para ver el motivo de un aviso.

### 12.12 Mantener y comprobar el host

- `sudo ./scripts/harden-host.sh check` y `sudo ./scripts/install-host-tasks.sh check` comparan lo instalado con `deploy/**` **de la etiqueta desplegada** y verifican cada control (`ufw` con solo 22, 80 y 443, SSH sin contraseña, `fail2ban`, actualizaciones solo de seguridad, Docker 28 o posterior, rotación de logs,
  permisos y propietario de los secretos, `age` y `rclone`, Caddy 2.7.2 o posterior, y que `host.conf` está completo). Ejecútalos tras cualquier cambio manual.
- Rol responsable de los parches: la persona administradora. Ubuntu se parchea solo (sin reinicio: reinicia en una ventana acordada cuando `check-host.sh` avise); Caddy y Docker, a mano cada 7 días como máximo.
- Rota el token de lectura de GHCR cuando caduque y repite `docker login` como `root`. Verifica cada trimestre la restauración (§12.9).

### 12.13 Verificación de los criterios de aceptación

Resumen de cómo se comprueba en el servidor cada criterio que no cubre una prueba automática (SPEC-0009 §9). Anota el resultado, sin datos, en la §18.

| CA | Comprobación |
|---|---|
| 1, 2 | Tras la primera copia: `rclone` sube con el rol de instancia; desde el servidor, listar y borrar en el bucket deben fallar y una segunda subida del mismo nombre crea otra versión (`deploy/aws/README.md`). |
| 3 | `sudo ./scripts/check-host.sh` con la última copia renombrada: llega el aviso al correo. |
| 4 | `restore-db.sh` (§12.9). |
| 5, 27 | Escaneo externo de 22, 80, 443 y de UDP (también por IPv6); `sudo docker ps`; `ss -ltnp`; `aws ec2 describe-security-groups`; `docker version`; prueba de `169.254.169.254` desde un contenedor (`docker run --rm curlimages/curl -m 3 http://169.254.169.254/latest/meta-data/` debe agotar el tiempo). |
| 6, 7 | `ssh -o PubkeyAuthentication=no <usuario>@<servidor>` y `ssh root@<servidor>` rechazados; fallos repetidos desde una IP de prueba y `sudo fail2ban-client status sshd`; `sudo unattended-upgrade --dry-run -d`. |
| 8, 9 | `docker info --format '{{.LoggingDriver}}'`; reinicio del servidor (§12.7, paso 8). |
| 10 | Una etiqueta sobre un commit con una prueba rota, o con una vulnerabilidad alta sembrada en un repositorio de ensayo, no publica imágenes; la que pasa espera la aprobación. |
| 11 a 13 | Ensayo de §12.5, paso 4. |
| 14 | `git grep -nIE '(password|secret|token|apikey)[^a-zA-Z0-9_]*[:=][[:space:]]*[A-Za-z0-9+/_-]{12,}' -- .github scripts deploy docker-compose*.yml` sin coincidencias, y revisión del log de una ejecución de los *workflows*. |
| 15 | Descarga de `/downloads/agent/` y comprobación de las sumas; una segunda ejecución no sobrescribe. |
| 16 a 18 | Detener `frontend` y esperar el aviso de la sonda; bajar `DISK_WARN_PCT` y `CERT_WARN_DAYS` en `host.conf` y ejecutar `check-host.sh`. |
| 19 | `aws ec2 describe-volumes --query 'Volumes[].Encrypted'` y revisión de SECURITY.md. |
| 20, 22 a 26 | §12.7. |
| 21 | `docker compose up --build` en un equipo de trabajo. |
| 28, 29 | `harden-host.sh check` e `install-host-tasks.sh check`; modos de `/var/lib/caddy`, `/etc/itmas3/host.conf` y `/root/.docker/config.json` (600 o 700); huella de Caddy anotada. |
