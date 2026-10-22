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
├── scripts/               Utilidades (init-env.sh, build-agent.sh, install-git-hooks.sh, test-commit-msg-hook.sh)
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
| Agente: unitarias | `cd agent && dotnet test tests/Itmas3.Agent.Tests` | .NET SDK |
| Agente: motores SQL reales (Testcontainers, ~5 min) | `cd agent && dotnet test tests/Itmas3.Agent.IntegrationTests` | .NET SDK + Docker |
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
  `Directory.Packages.props` (fijación transitiva central) y se retira cuando el paquete que la trae la actualice;
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
| `api` unhealthy tras cambiar contraseñas | El volumen conserva los roles antiguos: `docker compose down -v`. |
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
