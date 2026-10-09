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
                                                               └──▶ postgres (PostgreSQL 18)  [red interna]
                           migrator (one-shot, EF Core bundle) ──▶ postgres
                           /downloads/agent/  paquetes del agente (volumen de solo lectura)

 Equipos (Windows/Linux/macOS) ── itmas-agent (servicio) ──HTTPS──▶ /api/v1/device-enrollments, /api/v1/inventory-reports
                                                          └─HTTPS──▶ ipinfo.io (IP pública y ubicación)
```

- **Un único origen público**: nginx sirve la SPA y hace de proxy de la API. La API y la base de datos
  no se publican.
- **Autenticación BFF**: la API es cliente confidencial OIDC de Microsoft Entra ID; el navegador solo tiene
  una cookie de sesión `HttpOnly` y nunca ve tokens.
- **Autorización *default deny***: estar autenticado no concede privilegios; cada endpoint declara su permiso.
- **Clean Architecture** en el backend: `Domain` ← `Application` ← `Infrastructure` ← `Api`.
- **Agente de inventario** ([SPEC-0002](specs/features/SPEC-0002-os-inventory-agent.md)): servicio .NET para
  Windows, Linux y macOS que envía cada día el inventario del equipo. Se autentica con una API key propia del
  dispositivo, obtenida al enrolarse con un código de autorización. Guía: [agent/README.md](agent/README.md).

Detalle completo en [ARCHITECTURE.md](ARCHITECTURE.md) y [SECURITY.md](SECURITY.md).

## Tecnologías

| Capa | Tecnología | Versión |
|---|---|---|
| Frontend | Angular, Angular Material (Material Design 3), TypeScript, Vitest | 22.2 / 22.2 / 6.0 |
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
| `INVENTORY_ENROLLMENT_CODE` | Código de autorización con el que se enrolan los agentes (≥ 32 caracteres; lo genera `init-env.sh`). Vacío = enrolamiento desactivado. Rotarlo no afecta a los equipos ya enrolados. | **Sí** |
| `INVENTORY_HISTORY_RETENTION_DAYS` | Días de histórico de inventarios por equipo (90 por defecto; el último se conserva siempre). | No |
| `AGENT_DOWNLOADS_DIR` | Carpeta con los paquetes del agente que nginx sirve en `/downloads/agent/` (`./agent/dist`). | No |

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

2. Comparte con los colaboradores, por un canal interno, el valor de `INVENTORY_ENROLLMENT_CODE`.
3. Cada colaborador descarga el paquete de su plataforma, verifica la suma y ejecuta `itmas-agent install` como
   administrador. Detalle por sistema operativo: [agent/README.md](agent/README.md).

Si `.env` ya existía antes de SPEC-0002, añade a mano `INVENTORY_ENROLLMENT_CODE` con un valor aleatorio (por ejemplo,
`openssl rand -hex 24`). La consulta del inventario en la interfaz llegará con SPEC-0003.

## Documentación

| Documento | Contenido |
|---|---|
| [constitution.md](constitution.md) | Reglas permanentes que toda implementación debe respetar. |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Componentes, capas, comunicación y decisiones. |
| [SECURITY.md](SECURITY.md) | Autenticación, autorización, secretos, protección de la API. |
| [agent/README.md](agent/README.md) | Instalación, comandos y funcionamiento del agente de inventario. |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Desarrollo local, migraciones, pruebas, estructura. |
| [specs/](specs/README.md) | Proceso SDD, plantillas, ADRs y especificaciones. |
| [agents/](agents/README.md) | Agentes especializados: responsabilidades y límites. |
| [AGENTS.md](AGENTS.md) | Punto de entrada para herramientas de IA. |
