# ITMAS3 — Application Foundation

Base técnica (fundación) de una aplicación web empresarial, preparada para evolucionar mediante
**Specification-Driven Development (SDD)**. Esta etapa **no contiene funcionalidades de negocio**:
solo la arquitectura, la seguridad, la infraestructura y las reglas sobre las que se construirá el producto.

> Antes de modificar nada, lee [`constitution.md`](constitution.md). Toda funcionalidad nueva empieza
> con una especificación en [`specs/`](specs/README.md).

## Arquitectura general

```text
 Navegador ──HTTPS/HTTP──▶ frontend (nginx, :8080)
                           ├── /            SPA Angular 22 + Material 3
                           └── /api, /auth  ──reverse proxy──▶ api (ASP.NET Core 10, :8080)
                                                               ├──▶ Microsoft Entra ID (OIDC)
                                                               ├──▶ Microsoft Graph (opcional: agenda en Outlook)
                                                               └──▶ postgres (PostgreSQL 18)  [red interna]
                           migrator (one-shot, EF Core bundle) ──▶ postgres
                           /downloads/agent/  paquetes del agente (volumen de solo lectura)

 Equipos (Windows/Linux/macOS) ── itmas-agent (servicio) ──HTTPS──▶ /api/v1/device-enrollments, /api/v1/inventory-reports,
                                                          │          /api/v1/sql-audit-snapshots
                                                          ├─HTTPS──▶ ipinfo.io (IP pública y ubicación)
                                                          └─TLS────▶ motores SQL configurados en el equipo (solo lectura)
```

- **Un único origen público**: nginx sirve la SPA y hace de proxy de la API. La API y la base de datos
  no se publican.
- **Autenticación BFF**: la API es cliente confidencial OIDC de Microsoft Entra ID; el navegador solo tiene
  una cookie de sesión `HttpOnly` y nunca ve tokens.
- **Autorización *default deny***: estar autenticado no concede privilegios; cada endpoint declara su permiso.
- **Clean Architecture** en el backend: `Domain` ← `Application` ← `Infrastructure` ← `Api`.
- **Agente de inventario** ([SPEC-0002](specs/features/SPEC-0002-os-inventory-agent.md)): servicio .NET para
  Windows, Linux y macOS que envía cada día el inventario del equipo. Se autentica con una API key propia del
  dispositivo, obtenida al enrolarse con el código de un solo uso de su equipo. Guía: [agent/README.md](agent/README.md).
- **Inventario de equipos** ([SPEC-0003](specs/features/SPEC-0003-device-registry-and-inventory-viewer.md)): registro
  de estaciones de trabajo y servidores con su propietario, código de enrolamiento por equipo (ADR-0012), listado
  resumido, historial de registros e informe por secciones del último inventario.
- **Auditoría SQL** ([SPEC-0005](specs/features/SPEC-0005-sql-engine-access-audit.md)): módulo opcional del agente que
  lee cada día, en solo lectura y por TLS, las cuentas, roles y permisos de motores SQL Server, PostgreSQL, MySQL y
  MariaDB configurados en el equipo (ADR-0014, ADR-0015). ITMas los muestra con su nivel de riesgo, su registro de
  cambios y una casilla "Válido" por cuenta, rol y permiso.
- **Agenda de infraestructura** ([SPEC-0006](specs/features/SPEC-0006-infrastructure-agenda.md)): plan anual de las
  actividades del área (parches, auditorías, hardening, pruebas de vulnerabilidad, cambios de claves…) con responsable,
  estado, etiquetas y evidencia de ejecución; lista del año y calendarios de año, mes y semana (ADR-0017); exportación
  `.ics`; y, opcional, publicación en un calendario compartido de Outlook con Microsoft Graph (ADR-0016).

Detalle completo en [ARCHITECTURE.md](ARCHITECTURE.md) y [SECURITY.md](SECURITY.md).

## Tecnologías

| Capa | Tecnología | Versión |
|---|---|---|
| Frontend | Angular, Angular Material (Material Design 3), TypeScript, Vitest | 22.2 / 22.2 / 6.0 |
| Calendarios de la agenda | FullCalendar (solo complementos estándar, ADR-0017) | 7.1 |
| Runtime de build frontend | Node.js LTS | 24 |
| Servidor web / proxy | nginx (imagen *unprivileged*) | stable |
| Backend | ASP.NET Core / .NET (LTS), C# | 10.0 |
| Autenticación | Microsoft Entra ID vía Microsoft.Identity.Web | 4.16 |
| Persistencia | PostgreSQL, EF Core + Npgsql | 18 / 10.0 |
| Pruebas backend | xUnit v3, WebApplicationFactory, Testcontainers | 4.0 / 10.0 / 4.15 |
| Contenedores | Docker, Docker Compose | 29 / v5 |
| Agente de dispositivo | .NET Worker Service *self-contained* (Windows Service / systemd / launchd) | 10.0 |

## Requisitos

- Docker Desktop (o Docker Engine) con Docker Compose v2+.
- Para desarrollar fuera de contenedores: .NET SDK 10.0.4xx y Node.js 24 LTS (ver [DEVELOPMENT.md](DEVELOPMENT.md)).
- Para iniciar sesión: un *App Registration* de Microsoft Entra ID (ver [Configurar Microsoft Entra ID](#configurar-microsoft-entra-id)).

## Ejecutar

```bash
./scripts/init-env.sh
```

Crea `.env` a partir de `.env.example` con contraseñas aleatorias para PostgreSQL (permisos `600`).
Después, levanta todo el entorno con un único comando:

```bash
docker compose up --build
```

Abre <http://localhost:8080> (o el puerto definido en `FRONTEND_PORT`). Orden de arranque garantizado por
*health checks*: `postgres` (healthy) → `migrator` (aplica migraciones y termina) → `api` (healthy) → `frontend`.

Sin configurar Entra ID la aplicación arranca en `Development`, pero el botón de inicio de sesión
informa que el servicio no está disponible.

## Detener

```bash
docker compose down
```

Para borrar también los datos de PostgreSQL (el volumen `postgres-data`):

```bash
docker compose down -v
```

## Variables de entorno

Toda la configuración sensible llega por variables de entorno (`.env` → Docker Compose → contenedor).
`.env` está excluido de Git; [`.env.example`](.env.example) documenta cada variable sin valores reales.

| Variable | Uso | Secreta |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Staging` / `Production`. Fuera de `Development` la API exige Entra ID configurado. | No |
| `APP_ALLOWED_HOSTS` | Hosts públicos válidos (filtrado de cabecera `Host`). | No |
| `FRONTEND_PORT` | Puerto publicado en `127.0.0.1`. | No |
| `EDGE_NETWORK_SUBNET` | Subred de la red del proxy (confiable para `X-Forwarded-*`). | No |
| `POSTGRES_DB` | Nombre de la base de datos. | No |
| `POSTGRES_SUPERUSER` / `POSTGRES_SUPERUSER_PASSWORD` | Superusuario de arranque, solo lo usa el contenedor `postgres`. | **Sí** (password) |
| `APP_DB_MIGRATOR_USER` / `APP_DB_MIGRATOR_PASSWORD` | Rol dueño del esquema; solo lo usa `migrator`. | **Sí** (password) |
| `APP_DB_USER` / `APP_DB_PASSWORD` | Rol de ejecución de la API (solo DML). | **Sí** (password) |
| `ENTRA_INSTANCE`, `ENTRA_AUTHORITY_ORIGIN` | Autoridad de Entra ID (nube pública por defecto). | No |
| `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID` | Identificadores del *App Registration*. | No |
| `ENTRA_CLIENT_SECRET` | Credencial del cliente confidencial (backend). | **Sí** |
| `AUTHZ_BOOTSTRAP_ROOT_OIDS` | `oid` de Entra ID (separados por comas) que se convierten en Root al iniciar sesión **solo si no hay ningún Root activo**. | No |
| `AUTHZ_ALLOW_GUEST_USERS` | Permite el acceso a invitados B2B del tenant (`false` por defecto). | No |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint OTLP opcional para trazas, métricas y logs. | No |
| `INVENTORY_HISTORY_RETENTION_DAYS` | Días de histórico de inventarios por equipo (90 por defecto; el último se conserva siempre). | No |
| `SQL_AUDIT_CHANGE_LOG_RETENTION_DAYS` | Días que se conservan el registro de cambios de la auditoría SQL, los recibos de instantáneas y los elementos eliminados de los motores (365 por defecto). Las validaciones quedan en la auditoría sin límite. | No |
| `SQL_AUDIT_SNAPSHOTS_PER_HOUR` | Instantáneas de auditoría SQL aceptadas por equipo y hora (120 por defecto: hasta 50 conexiones y sus reintentos). | No |
| `AGENT_DOWNLOADS_DIR` | Carpeta con los paquetes del agente que nginx sirve en `/downloads/agent/` (`./agent/dist`). | No |
| `AGENDA_TIME_ZONE` | Zona horaria IANA de la agenda de infraestructura (`America/Bogota` por defecto). La API no arranca si no la reconoce. No la cambies con actividades ya guardadas: conservan la zona con la que se guardaron. | No |
| `AGENDA_ALLOWED_ATTENDEE_DOMAINS` | Dominios de los correos que pueden ser participantes de una actividad, separados por comas. Vacía: con Outlook activo, el dominio de `AGENDA_OUTLOOK_MAILBOX`; con Outlook apagado no se admite ningún participante, así que indícalos aquí si los usas. | No |
| `AGENDA_PUBLIC_URL` | URL pública de ITMas, para el enlace del cuerpo de las invitaciones. Obligatoria (URL `https` absoluta) con `AGENDA_OUTLOOK_ENABLED=true`. Sin Outlook es opcional: con ella, el archivo `.ics` que exporta la agenda lleva el enlace a cada actividad (propiedad `URL`); sin ella, el `.ics` no la incluye. | No |
| `AGENDA_PUBLISH_REQUESTS_PER_HOUR` | Operaciones por hora que avisan por Outlook: programar, cancelar, sincronizar y modificar actividades programadas (60 por defecto). Se cuentan en conjunto para toda la API y solo después de autorizar (una petición rechazada no gasta): protegen contra el envío masivo de invitaciones. | No |
| `AGENDA_OUTLOOK_ENABLED` | Publica las actividades programadas en un calendario compartido de Outlook con Microsoft Graph (`false` por defecto: sin llamadas externas y todo lo demás funciona). Con `true` hay que informar las demás variables `AGENDA_OUTLOOK_*` (salvo la dirección del equipo) y los prerrequisitos de Microsoft 365 de [SECURITY.md](SECURITY.md); sin ellas, la API no arranca. | No |
| `AGENDA_OUTLOOK_TENANT_ID`, `AGENDA_OUTLOOK_CLIENT_ID` | Identificadores de la aplicación de Entra ID de la agenda (distinta de la del inicio de sesión). | No |
| `AGENDA_OUTLOOK_CLIENT_SECRET` | Credencial de esa aplicación, con caducidad corta. Solo existe en el contenedor `api`; nunca en el repositorio, en logs ni en la línea de comandos. | **Sí** |
| `AGENDA_OUTLOOK_MAILBOX` | Dirección del buzón compartido cuyo calendario recibe las actividades. | No |
| `AGENDA_OUTLOOK_TEAM_ADDRESS` | Opcional. Lista de distribución del equipo, invitada como opcional cuando "notificar al equipo" está activo; debe pertenecer a un dominio permitido. | No |

El host y el puerto de PostgreSQL son internos de Docker (`postgres:5432`); la API los recibe como
`Database__Host` / `Database__Port` (ver `docker-compose.yml`). Cambiar contraseñas de la base de datos
después del primer arranque requiere recrear el volumen o alterar los roles (ver [DEVELOPMENT.md](DEVELOPMENT.md)).

## Configurar Microsoft Entra ID

1. En Entra ID → *App registrations* → *New registration* (cuentas de un solo tenant).
2. *Authentication* → plataforma **Web** (no "SPA": el cliente es el backend) con dos *Redirect URIs*:
   `http://localhost:8080/signin-oidc` y `http://localhost:8080/signout-callback-oidc`
   (ajusta el puerto si cambiaste `FRONTEND_PORT`; en producción, `https://<tu-dominio>/...`).
   Deja desactivados *Access tokens* e *ID tokens* (flujo implícito): se usa *authorization code* + PKCE.
3. *Certificates & secrets* → nuevo *client secret* (en producción, preferir certificado o identidad administrada).
4. Completa en `.env`: `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, `ENTRA_CLIENT_SECRET` y ejecuta `docker compose up -d`.

### Primer administrador (Root)

Los usuarios se dan de alta automáticamente, sin privilegios, al iniciar sesión por primera vez. Para el primer Root:

1. Obtén tu **Object ID** en Entra ID (*Users* → tu usuario → *Object ID*).
2. Añádelo en `.env`: `AUTHZ_BOOTSTRAP_ROOT_OIDS=<object-id>` y ejecuta `docker compose up -d`.
3. Inicia sesión: al no existir ningún Root activo, tu cuenta pasa a ser Root (queda auditado) y verás la sección
   **Administración**.
4. Desde ahí crea políticas y asígnalas. La variable solo vuelve a actuar si alguna vez no queda ningún Root activo.

Para la sesión local basta con `http://localhost`: los navegadores modernos aceptan cookies `Secure` en
`localhost`. Cualquier otro host requiere HTTPS (ver [SECURITY.md](SECURITY.md#https-y-tls)).

## Agente de inventario

1. Compila los paquetes de las 5 plataformas, dentro de Docker y sin SDK local. Quedan en `agent/dist/<versión>/` y
   `agent/dist/latest/`, cada uno con su `SHA256SUMS`, y nginx los publica en `/downloads/agent/`:

   ```bash
   ./scripts/build-agent.sh
   ```

2. Registra el equipo en **Inventario → Equipos → Nuevo equipo** (permiso `devices.create`): nombre, tipo (estación de
   trabajo o servidor) y, opcionalmente, el correo del propietario. ITMas muestra **una sola vez** su código de
   enrolamiento (`XXXXX-XXXXX-XXXXX-XXXXX`); cópialo y entrégalo de forma segura a quien instalará el agente.
3. Quien instala descarga el paquete de su plataforma, verifica la suma y ejecuta `itmas-agent install` como
   administrador, con el código del equipo. Detalle por sistema operativo: [agent/README.md](agent/README.md).

El código sirve una sola vez. Para reinstalar el agente, usa **Regenerar código** en el menú del equipo. **Eliminar** un
equipo invalida su código y su API key: el agente deja de poder enviar inventario. Los permisos del inventario
(`devices.read`, `devices.create`, `devices.update`, `devices.delete`, `devices.reports.read`) se conceden con políticas.

### Auditoría SQL (opcional)

Tras instalar el agente, el administrador del equipo puede añadir motores SQL con `itmas-agent sql add` (y gestionarlos
con `sql list`, `sql test`, `sql update` y `sql remove`). Antes, el DBA crea en cada motor una cuenta de solo lectura
para el agente y activa TLS. Guías: manual de uso, procesos *Preparar un motor SQL para la auditoría* y *Configurar las
conexiones SQL del agente*, y [agent/README.md](agent/README.md). Los resultados se ven en **Auditoría SQL →
Instancias** con los permisos `sql-audit.read` y `sql-audit.accesses.validate` (para validar).

Las instalaciones nuevas aceptan la versión 2 de los términos del agente; la API acepta las versiones 1 y 2, así que los
equipos ya enrolados no tienen que reinstalarse.

Desde SPEC-0003 ya no existe un código de enrolamiento general: si tu `.env` conserva `INVENTORY_ENROLLMENT_CODE`, puedes
borrar esa línea (la API ya no la lee). Los equipos enrolados antes siguen funcionando: la migración los incorpora
al inventario como equipos activos.

## Manual de uso

El manual para las personas usuarias está en **`/manual`** (por ejemplo, `http://localhost:8088/manual`). Es público:
no hace falta iniciar sesión. Se llega también desde la página de inicio de sesión, desde el menú "Ayuda → Manual de
uso" y desde el botón de ayuda de la barra superior. Describe los procesos del sistema (alta de usuarios, asignación
de privilegios, registro de equipos, instalación del agente, consulta del inventario, auditoría de accesos SQL…), con el permiso que necesita
cada uno.

Su contenido está en [`frontend/public/manual/`](frontend/public/manual/) y se actualiza en el mismo cambio que cada
funcionalidad ([constitución §10.4](constitution.md), [ADR-0013](specs/architecture/adr-0013-user-manual.md)).
Cómo editarlo: [DEVELOPMENT.md §8.1](DEVELOPMENT.md#81-mantener-el-manual-de-uso).

## Documentación

| Documento | Contenido |
|---|---|
| [Manual de uso](frontend/public/manual/index.json) | Procesos del sistema para las personas usuarias (en la aplicación: `/manual`). |
| [constitution.md](constitution.md) | Reglas permanentes que toda implementación debe respetar. |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Componentes, capas, comunicación y decisiones. |
| [SECURITY.md](SECURITY.md) | Autenticación, autorización, secretos, protección de la API. |
| [agent/README.md](agent/README.md) | Instalación, comandos y funcionamiento del agente de inventario. |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Desarrollo local, migraciones, pruebas, estructura, skills y hooks de desarrollo. |
| [specs/](specs/README.md) | Proceso SDD, plantillas, ADRs y especificaciones. |
| [agents/](agents/README.md) | Agentes especializados: responsabilidades y límites. |
| [AGENTS.md](AGENTS.md) | Punto de entrada para herramientas de IA. |
