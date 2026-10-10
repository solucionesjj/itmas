# Seguridad

Este documento describe los controles de seguridad de la fundación. Las reglas obligatorias están en
[`constitution.md` §3–§4](constitution.md). Cualquier cambio que afecte a estos controles requiere revisión
del agente de seguridad ([`agents/security.md`](agents/security.md)).

## 1. Autenticación

**Proveedor:** Microsoft Entra ID (OpenID Connect) mediante `Microsoft.Identity.Web`.

**Patrón:** Backend-for-Frontend (BFF), [ADR-0002](specs/architecture/adr-0002-authentication-bff.md).

| Aspecto | Implementación |
|---|---|
| Cliente OIDC | La API, como **cliente confidencial** (client secret hoy; certificado o identidad administrada recomendados en producción). |
| Flujo | *Authorization code* + **PKCE (S256)**, `response_mode=form_post`. Sin flujo implícito. |
| Tokens | No se guardan (`SaveTokens=false`) y **nunca llegan al navegador**. |
| Sesión | Cookie `__Host-itmas3-session`: `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/`, cifrada con ASP.NET Core Data Protection. |
| Expiración | 60 min de inactividad (deslizante) y 8 h absolutas (`Authentication:Session`). Al expirar, la API responde `401` y la SPA redirige a `/login?reason=expired`. |
| Inicio de sesión | `GET /auth/login?returnUrl=` (navegación). `returnUrl` debe ser local: se rechazan URLs absolutas y `//host` (*open redirect*). Limitado a 20 intentos/min por IP. |
| Cierre de sesión | `POST /auth/logout` (form del mismo origen): borra la cookie y cierra la sesión en Entra ID. |
| Identidad | `GET /auth/session` devuelve solo `displayName` y `username`. Identificador estable: `oid` (+ `tid`); `preferred_username` es solo para mostrar. |
| Fail-fast | Fuera de `Development` la API **no arranca** sin `TenantId`, `ClientId` y una credencial. |

**Manual de uso público** (SPEC-0004, ADR-0013). `/manual` se lee sin sesión. No da acceso a nada (cada permiso lo
evalúa la API), pero cualquiera en Internet puede leer los procesos y los nombres de los permisos: por eso no puede
contener secretos, datos personales reales ni detalles internos de infraestructura (constitución §10.4) y `security`
revisa cada cambio de `frontend/public/manual/`. El HTML que se genera a partir del Markdown se sanea en Angular y la
CSP (`script-src 'self'`) impide ejecutar scripts aunque se colaran en un archivo.

**Secretos en el frontend: ninguno.** La SPA no contiene tenant, client ID, secretos ni tokens; tampoco los necesita.

### Dispositivos (agente de inventario, ADR-0009, ADR-0012)

| Aspecto | Implementación |
|---|---|
| Código de enrolamiento | Uno por equipo registrado (SPEC-0003): 100 bits aleatorios en Base32 de Crockford (`XXXXX-XXXXX-XXXXX-XXXXX`). Se muestra una sola vez al crear el equipo o al regenerarlo (`Cache-Control: no-store`; la SPA no lo guarda). En la BD solo está su SHA-256. Es de un solo uso: se consume al enrolar y deja de servir al regenerarlo o al eliminar el equipo. |
| Enrolamiento | `Authorization: Enrollment <código>` → se busca el equipo por el hash del código. Código desconocido, usado, regenerado o de un equipo eliminado → `401` sin motivo. Hardware ya registrado en otro equipo → `409` auditado (`device.enrollment.rejected`). 10 intentos/min por IP. Los fallos se registran en el log, sin el código. |
| Credencial del dispositivo | 256 bits aleatorios, entregados una sola vez. En la BD solo se guarda su SHA-256 (único). Reenrolar el equipo con un código regenerado la sustituye e invalida la anterior (`device.relinked`). Eliminar el equipo la invalida (`device.deleted`) y el autenticador solo acepta equipos activos. |
| Uso | `Authorization: Device <apiKey>` solo en `POST /api/v1/inventory-reports` (política `EnrolledDevice`). Un dispositivo no tiene permisos de usuario ni sesión. |
| En el equipo | Windows: DPAPI (máquina) + ACL de `SYSTEM`/Administradores. Linux y macOS: archivos `0600` de `root`. El código de autorización no se guarda. |
| Entrada no confiable | Contrato estricto: propiedades desconocidas → `400`, límites por colección y longitud, sin caracteres de control, IPs validadas, cuerpo < 1 MB. |
| Auditoría | `device.created`, `device.updated`, `device.enrollment-code.regenerated`, `device.deleted` (actor: el usuario) y `device.enrolled`, `device.relinked`, `device.enrollment.rejected` (actor: el agente, con IP de origen, *hostname* y versión de los términos) en `app.audit_events`. Nunca contienen códigos, API keys ni sus hashes. |
| Consulta del inventario | `devices.read` (registro) y `devices.reports.read` (historial e informes, que contienen cuentas locales, IPs y ubicación) por separado. La SPA muestra todo dato del equipo como texto (sin `innerHTML`). |

## 2. Autorización

Modelo basado en políticas, inspirado en AWS IAM: **Usuario → Políticas → Permisos → Recurso/Acción**, sin roles
([SPEC-0001](specs/features/SPEC-0001-policy-based-authorization.md), [ADR-0008](specs/architecture/adr-0008-policy-based-authorization.md);
base *default deny* en [ADR-0003](specs/architecture/adr-0003-authorization-default-deny.md)).

**Declaración por endpoint**
- **Fallback policy**: todo endpoint que no declare autorización se deniega a todos, incluido el Root.
- Cada endpoint declara explícitamente una de estas opciones:
  - `[AllowAnonymous]`: público (health, sesión, login/logout).
  - `[Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]`: **solo** para operaciones sobre la propia identidad.
  - `[RequirePermission("<recurso>.<acción>")]`: cualquier recurso de la aplicación.
- Pruebas de convención: `EndpointAuthorizationConventionTests` (todo endpoint declara su autorización, sin `[Authorize]`
  sin política) y `PermissionCatalogConventionTests` (todo permiso usado está en el catálogo).

**Evaluación** (`PolicyPermissionEvaluator`, en cada solicitud y siempre en el servidor)
1. Usuario de la aplicación inexistente o **inactivo** → denegar.
2. **Root** → permitir.
3. Si no → permitir solo si alguna política **activa** asignada al usuario contiene el permiso, y ese permiso es
   otorgable y no está obsoleto (unión de políticas).

**Usuarios y Root**
- Alta automática en el primer inicio de sesión, **sin políticas** (cero privilegios). La identidad se vincula por
  `(proveedor, tenant, oid)`, nunca por correo. Las cuentas invitadas B2B se rechazan salvo `AUTHZ_ALLOW_GUEST_USERS=true`.
- Root: `AUTHZ_BOOTSTRAP_ROOT_OIDS` convierte en Root a las cuentas listadas **solo si no existe ningún Root activo**
  (instalación inicial y recuperación de emergencia). Después, solo un Root puede promover o revocar Root, y siempre debe quedar al menos uno activo.
- Desactivar un usuario rota su marca de seguridad: `SessionValidationMiddleware` termina su sesión en la siguiente solicitud.

**Salvaguardas anti-escalada** para administradores delegados (no Root):
- No pueden modificar sus propias asignaciones ni su propio estado.
- No pueden otorgar, ni en políticas ni mediante asignaciones, permisos que ellos mismos no tienen.
- No pueden modificar a un Root.
- No pueden gestionar Root: el permiso `authorization.root.manage` no es otorgable.

**Propagación inmediata.** Los permisos efectivos se cachean por `(versión de autorización, usuario)`. Cada cambio
incrementa la versión en la misma transacción, así que las revocaciones se aplican en la siguiente solicitud, también con varias réplicas.

**Auditoría.** Cada operación de administración (y cada intento rechazado o denegado) se registra en `app.audit_events`
con actor, acción, recurso, fecha, resultado y valores anterior y posterior. La tabla es *append-only*: el rol de la API solo
tiene `INSERT` y `SELECT`. Consulta: permiso `audit.read`.

El frontend usa los permisos de `/auth/session` solo para ocultar menús y botones: **nunca es una frontera de seguridad**.

Los endpoints de dispositivos usan las políticas `DeviceEnrollment` y `EnrolledDevice` (constitución §3.3, ADR-0009).
`EndpointAuthorizationConventionTests` verifica que solo `DeviceEnrollmentsController` e `InventoryReportsController`
las usan.

## 3. Manejo de secretos y variables de entorno

```text
Código fuente → Configuración (appsettings, sin secretos) → Variables de entorno → Runtime del contenedor
```

- Ningún secreto en el repositorio: ni en código, ni en `appsettings*.json`, ni en Dockerfiles, ni en `docker-compose.yml`.
- `.env` (local) está en `.gitignore`; [`.env.example`](.env.example) solo documenta nombres.
- `scripts/init-env.sh` genera contraseñas aleatorias y crea `.env` con permisos `600`.
- La cadena de conexión se construye en memoria desde `Database__*`; no se escribe en ningún archivo ni en logs
  (`DatabaseOptions.ToString()` omite la contraseña). El migrador lee las mismas variables: ninguna contraseña
  viaja por la línea de comandos.
- El script de roles de PostgreSQL lee contraseñas con `\getenv` (no aparecen en el proceso ni en el archivo).
- Archivos ignorados por Git: `.env*` (excepto `.env.example`), `*.pfx`, `*.p12`, `*.pem`, `*.key`, `*.crt`, `*.cer`,
  `secrets/`, `appsettings.*.local.json`, `docker-compose.override.yml`.

> **Limitación conocida:** las variables de entorno de un contenedor son visibles con `docker inspect` para quien
> controle el daemon. En producción, inyéctalas desde un gestor de secretos (Azure Key Vault, AWS Secrets Manager,
> secretos de Kubernetes/ACA) y usa credenciales sin secreto (certificado o identidad administrada) para Entra ID.

## 4. Protección de la API

| Control | Implementación |
|---|---|
| Entrada | `[ApiController]` + DataAnnotations → `400` Problem Details. JSON estricto: propiedades desconocidas rechazadas (`UnmappedMemberHandling.Disallow`); mensajes del serializador ocultos. Cuerpo máximo 1 MB. |
| Inyección | Solo EF Core / consultas parametrizadas. Prohibido concatenar SQL (constitution §5). |
| CSRF | Cookie `SameSite=Strict` + cabecera `X-CSRF` obligatoria en métodos mutantes de `/api` + validación de `Origin`. |
| CORS | Cerrado por defecto (mismo origen vía proxy). Solo se abre con `Cors:AllowedOrigins` explícito. |
| Cabeceras API | `X-Content-Type-Options`, `X-Frame-Options: DENY`, `CSP: default-src 'none'`, `Referrer-Policy: no-referrer`, `Cross-Origin-Resource-Policy`, `Permissions-Policy`, `Cache-Control: no-store`. Sin cabecera `Server`. |
| Cabeceras SPA (nginx) | CSP estricta (`script-src 'self'`, `frame-ancestors 'none'`, `form-action` limitado a Entra ID), `nosniff`, `DENY`, `COOP`. `server_tokens off`. |
| Rate limiting | 300 req/min por usuario (o IP si anónimo); 20/min por IP en `/auth/login`. `429` + `Retry-After`. |
| Concurrencia | Políticas con `ETag`/`If-Match` (`412` si cambió, `428` si falta); cambios de autorización serializados en la base de datos. |
| Host / proxy | `AllowedHosts` restringido; `X-Forwarded-*` solo se aceptan desde la subred del proxy. |
| Errores | Problem Details uniformes con `traceId`. Nunca stack traces, mensajes internos, datos de infraestructura ni secretos (verificado por pruebas). |
| Health checks | `/health*` solo en la red interna; nginx responde `404` desde fuera. Exponen estado, no detalles. |

### Excepción documentada de CSP

`style-src 'unsafe-inline'` es necesario para los estilos que Angular/Material insertan en tiempo de ejecución.
No existen scripts inline (`script-src 'self'`); por eso el *inline critical CSS* está desactivado en `angular.json`.

## 5. Base de datos

- Mínimo privilegio: la API se conecta con un rol sin DDL ni acceso al historial de migraciones; el superusuario
  solo lo usa el contenedor `postgres` para inicializarse.
- `REVOKE ALL ... FROM PUBLIC` en la base de datos y el esquema `public`.
- PostgreSQL en una red Docker `internal` (sin salida) y sin puertos publicados.
- `IncludeErrorDetail=false`: los errores del servidor no se copian en excepciones ni logs.
- Producción: `Database__SslMode=VerifyFull` contra un servidor administrado con TLS.

## 6. Logging seguro

- Logs estructurados JSON (`Information`, `Warning`, `Error`, `Critical`) con `TraceId`/`SpanId`.
- El log HTTP registra solo método, ruta, estado y duración: **sin query strings, cabeceras, cookies ni cuerpos**.
- Nunca se registran contraseñas, secretos, tokens, cookies, cadenas de conexión ni datos personales.
  Los denegados de autorización registran el `oid` (identificador seudónimo), nunca nombre ni correo.
- `IdentityModelEventSource.ShowPII` permanece en su valor por defecto (`false`).

## 7. HTTPS y TLS

- La terminación TLS se hace en el borde (ingress, balanceador o nginx con certificado). La API está preparada:
  `UseHsts` fuera de Development, `UseHttpsRedirection` activable con `Security:UseHttpsRedirection`,
  y respeta `X-Forwarded-Proto` del proxy confiable.
- Las cookies se emiten siempre con `Secure`. En local funciona sobre `http://localhost` porque los navegadores
  tratan `localhost` como contexto seguro; cualquier otro host exige HTTPS.

## 8. Consideraciones para producción (checklist)

- [ ] `ASPNETCORE_ENVIRONMENT=Production`, `APP_ALLOWED_HOSTS` con el dominio real.
- [ ] TLS en el borde y Redirect URIs `https://` en Entra ID.
- [ ] Credencial de Entra ID por certificado o identidad administrada (sin client secret).
- [ ] Secretos inyectados desde un gestor de secretos; rotación definida.
- [ ] Cifrado en reposo del key ring de Data Protection (`ProtectKeysWithCertificate` / Azure Key Vault):
      hoy las claves se guardan sin cifrar en `app.data_protection_keys` (la API lo advierte al arrancar).
- [ ] PostgreSQL administrado con TLS (`SslMode=VerifyFull`), backups y cifrado en reposo.
- [ ] Imágenes fijadas por digest y escaneadas (p. ej. `docker scout`, Trivy); `npm audit` / NuGet audit en CI.
- [ ] `AUTHZ_BOOTSTRAP_ROOT_OIDS` vacío una vez creado el Root (dejarlo solo como procedimiento de emergencia documentado).
- [ ] Política de retención y exportación de `app.audit_events`.
- [ ] Exportación de telemetría (`OTEL_EXPORTER_OTLP_ENDPOINT`) y alertas sobre `Error`/`Critical`.
- [ ] **IP real del cliente (SPEC-0002, ADR-0011).** Si nginx es el borde y tiene el certificado de Let's Encrypt, no
      hay que hacer nada. Si TLS lo termina otro proxy del host (certbot con un nginx del host, Caddy, Traefik) o un
      balanceador, configura en nginx `set_real_ip_from <red del proxy>` y `real_ip_header X-Forwarded-For`. Si no,
      la IP de conexión de los inventarios será la del proxy.
- [ ] Entregar los códigos de enrolamiento por un canal interno y seguro, solo a quien instala el agente. Si un código
      no usado pudo filtrarse, regenerarlo (o eliminar el equipo).
- [ ] **Revocar un dispositivo comprometido (SPEC-0003).** En **Inventario → Equipos**, *Eliminar* el equipo (permiso
      `devices.delete`): su código y su API key dejan de servir de inmediato y el historial se conserva. Para volver a
      inventariar esa máquina, regístrala como equipo nuevo. Registra la acción en el gestor de incidentes.
- [ ] Revisar periódicamente quién tiene `devices.update` (puede regenerar códigos) y `devices.reports.read` (datos personales).
- [ ] Publicar solo paquetes del agente generados por `scripts/build-agent.sh` y su `SHA256SUMS`. Los binarios no están
      firmados (SPEC-0002 P-11, riesgo aceptado): la integridad depende de HTTPS y de que los usuarios verifiquen la suma.

## 9. Reportar una vulnerabilidad

No abras un issue público. Contacta al responsable de seguridad del proyecto por un canal privado con
la descripción, el impacto y los pasos para reproducirla.
