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
| Uso | `Authorization: Device <apiKey>` solo en `POST /api/v1/inventory-reports` y `POST /api/v1/sql-audit-snapshots` (política `EnrolledDevice`). Un dispositivo no tiene permisos de usuario ni sesión. |
| En el equipo | Windows: DPAPI (máquina) + ACL de `SYSTEM`/Administradores. Linux y macOS: archivos `0600` de `root`. El código de autorización no se guarda. |
| Entrada no confiable | Contrato estricto: propiedades desconocidas → `400`, límites por colección y longitud, sin caracteres de control, IPs validadas, cuerpo < 1 MB. |
| Auditoría | `device.created`, `device.updated`, `device.enrollment-code.regenerated`, `device.deleted` (actor: el usuario) y `device.enrolled`, `device.relinked`, `device.enrollment.rejected` (actor: el agente, con IP de origen, *hostname* y versión de los términos) en `app.audit_events`. Nunca contienen códigos, API keys ni sus hashes. |
| Consulta del inventario | `devices.read` (registro) y `devices.reports.read` (historial e informes, que contienen cuentas locales, IPs y ubicación) por separado. La SPA muestra todo dato del equipo como texto (sin `innerHTML`). |

### Auditoría de motores SQL (SPEC-0005, ADR-0014, ADR-0015)

Los datos recolectados son un **mapa de las cuentas privilegiadas** de los motores de la organización.

| Aspecto | Implementación |
|---|---|
| Lectura y validación | Permisos propios: `sql-audit.read` (consultar) y `sql-audit.accesses.validate` (marcar y desmarcar; exige también el de lectura). Cada validación queda en `app.audit_events` (`sql-audit.account.validated`, `sql-audit.account.unvalidated`, `sql-audit.item.validated`, `sql-audit.item.unvalidated`) con la instancia, la cuenta, el elemento y la huella validada. |
| Recepción | `POST /api/v1/sql-audit-snapshots` con la API key del equipo; la instancia se asocia al equipo autenticado, nunca a datos de la instantánea (RN-1). Contrato estricto (propiedades desconocidas, enumeraciones cerradas, referencias que existen, identidades sin repetir, sin caracteres de control) validado antes de procesar. 120 instantáneas/h por equipo. |
| Tamaño | 16 MB **solo** en esa ruta: `location` exacta en nginx y `[RequestSizeLimit]` en la acción. El resto de la API sigue en 1 MB. Límites por colección: 1 000 bases de datos, 20 000 cuentas, 100 000 pertenencias y 200 000 permisos. |
| Datos | Sin hashes, contraseñas, datos de tablas ni texto de procedimientos (el agente no los lee). Nombres de cuentas, bases y objetos tratados como no confiables: nunca en los logs (solo ids), mostrados como texto en la SPA (sin `innerHTML`; una prueba estática lo vigila). Retención del registro de cambios: 1 año. |
| Agente | Cuenta de solo lectura por motor, credencial protegida como la API key (DPAPI de máquina / `0600` de `root`), nunca en argumentos, logs ni la API. Consultas de catálogo de texto fijo, una conexión por base de datos, tiempo máximo por consulta y por conexión. Siempre TLS; `unverified` solo por elección expresa del administrador del equipo, por conexión, y señalado en ITMas con el aviso *Certificado sin verificar* (riesgo aceptado, ADR-0015). |
| SQL en el servidor | Sin SQL escrito a mano: EF Core para consultas y `ExecuteUpdate`/`ExecuteDelete`. La inserción masiva usa `COPY ... FROM STDIN (FORMAT BINARY)` de Npgsql con el texto del comando derivado del modelo de EF Core (nombres de tabla y columnas entrecomillados) y los valores en el flujo binario, nunca en el texto (constitución §5.2, justificado en SPEC-0005 §13). |

### Agenda de infraestructura y Outlook (SPEC-0006, ADR-0016)

La agenda guarda *cuándo* y *quién* de las actividades de seguridad e infraestructura (pruebas de penetración, rotaciones,
congelamientos): su calendario es, en sí mismo, información sobre la postura de seguridad. **No debe contener valores de claves ni
hallazgos**; la ayuda del formulario y del manual lo dicen, y no se puede impedir técnicamente.

| Aspecto | Implementación |
|---|---|
| Permisos | Seis, separados por acción: `agenda.read`, `agenda.create`, `agenda.update`, `agenda.publish` (programar, cancelar y sincronizar con Outlook), `agenda.complete` y `agenda.types.manage`. Cada endpoint declara el suyo y la matriz de `401`/`403` está probada. Todo lo que avisa a otras personas exige `agenda.publish`: también **modificar un evento Programado o una serie con ocurrencias Programadas**, que se comprueba en el caso de uso, en la misma transacción que lee el estado (`403` si falta; enmienda del 2026-10-21). Las operaciones de escritura llevan `ETag`/`If-Match`. |
| Publicación | Con `AGENDA_OUTLOOK_ENABLED=false` (por defecto) la API no hace ninguna llamada externa. Con `true`, publicar es una petición del servidor a un **solo buzón** con una **aplicación de Entra ID propia** y un solo permiso de calendario concedido por *RBAC for Applications*, no por consentimiento global (ADR-0016; runbook en [DEVELOPMENT.md §11](DEVELOPMENT.md#11-agenda-de-infraestructura-outlook-y-el-tenant-spec-0006-adr-0016)). |
| Credencial | `AGENDA_OUTLOOK_CLIENT_SECRET`: variable de entorno solo del contenedor `api`; nunca en el repositorio, en logs ni en la línea de comandos. El código de la v1 solo admite **secreto** (riesgo aceptado hasta producción): pasar a certificado exige un cambio de código, previsto en SPEC-0008. Mientras tanto, secreto de caducidad corta cuyo vencimiento se anota en la propia agenda. El token de aplicación vive en la memoria de MSAL; un `401` hace que el siguiente intento pida otro. |
| Destinos | Solo `login.microsoftonline.com` y `graph.microsoft.com`, con rutas fijas y el buzón escapado como un solo segmento; ninguna URL procede de la entrada. El cliente HTTP no sigue redirecciones, no usa cookies, tiene un tiempo máximo de 10 s y respeta `Retry-After` (un `429` detiene el lote). |
| Contenido enviado | Texto plano (el marcado se entrega literal), solo los campos del evento y un enlace a ITMas; las etiquetas no viajan. Invitados: el responsable (solo si su dirección es de un dominio permitido: una cuenta invitada o externa no se invita) y los participantes (30 como máximo, solo de los dominios permitidos `AGENDA_ALLOWED_ATTENDEE_DOMAINS`), y, opcional, la lista del equipo. |
| Errores y logs | Solo se guarda y muestra un código de un conjunto cerrado (`graph_*`, `not_configured`). El cuerpo de las respuestas de Graph no se registra ni se devuelve. Los logs llevan operación, id, código y el `request-id` de Graph (si es un GUID); nunca títulos, correos, buzón, cuerpos, motivos, tokens ni el secreto. Los *spans* HTTP de Graph no se exportan, porque la ruta lleva el buzón. |
| Abuso | Un máximo global por hora de operaciones que avisan: `schedule`, `cancel`, `outlook-sync` y modificar lo Programado (`AGENDA_PUBLISH_REQUESTS_PER_HOUR`, 60 por defecto; `429` con `Retry-After` al excederlo). Se gasta **después** de la sesión, la cabecera anti-CSRF y los permisos, así que una petición rechazada no lo toca (antes, 60 peticiones anónimas bloqueaban a todos). Lotes de hasta 50, series de hasta 60 ocurrencias, rango de consulta de 366 días y 1 000 resultados. Un fallo de Outlook nunca impide guardar, programar ni cancelar. **Riesgos aceptados** (SPEC-0006 §19.8): el límite cuenta peticiones, no destinatarios (50 eventos × ~32 invitados por petición pueden superar el límite de envío del buzón en Exchange Online), y vive en la memoria de cada proceso (se reinicia con la API y se multiplica con varias réplicas). |
| Entrada | Contrato estricto (propiedades desconocidas → `400`); enlaces de evidencia solo `http`/`https`, sin credenciales y hasta 10; etiquetas de 1 a 30 caracteres sin controles. La SPA muestra todo como texto (sin `innerHTML` ni `bypassSecurityTrust*`) y los enlaces llevan `rel="noopener noreferrer"`. |
| Exportación `.ics` | `GET /api/v1/agenda-events/export.ics` con `agenda.read`: solo eventos Programados, rango y cantidad acotados, escape y plegado RFC 5545 (probados contra la inyección de propiedades), sin personas (ni responsable ni correos), `Cache-Control: no-store`, `nosniff` y adjunto. La descarga la hace el navegador con un `Blob` del mismo origen. |
| Auditoría | Un evento de `app.audit_events` por operación (`agenda.*`), incluidos los resultados de Outlook, sin secretos ni respuestas de Graph. Editar una serie guarda el antes de cada ocurrencia cambiada. |
| Riesgos aceptados | Además de los de **Abuso** y **Credencial** (SPEC-0006 §19.8): el acotamiento a un buzón depende del tenant y **no está verificado**, así que Outlook **no se activa en producción sin cumplir SPEC-0008** (CA-24); la cadena de suministro de FullCalendar (ADR-0017); lo enviado a Microsoft 365 queda fuera del control de `agenda.read` y cancelar no lo borra; secretos o enlaces con *tokens* escritos por error en el texto o la evidencia, que quedan también en la auditoría; y el abuso de personas con permisos dentro de los dominios permitidos. |

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
| Entrada | `[ApiController]` + DataAnnotations → `400` Problem Details. JSON estricto: propiedades desconocidas rechazadas (`UnmappedMemberHandling.Disallow`); mensajes del serializador ocultos. Cuerpo máximo 1 MB (16 MB solo en `POST /api/v1/sql-audit-snapshots`). |
| Inyección | Solo EF Core / consultas parametrizadas. Prohibido concatenar SQL (constitution §5). |
| CSRF | Cookie `SameSite=Strict` + cabecera `X-CSRF` obligatoria en métodos mutantes de `/api` + validación de `Origin`. |
| CORS | Cerrado por defecto (mismo origen vía proxy). Solo se abre con `Cors:AllowedOrigins` explícito. |
| Cabeceras API | `X-Content-Type-Options`, `X-Frame-Options: DENY`, `CSP: default-src 'none'`, `Referrer-Policy: no-referrer`, `Cross-Origin-Resource-Policy`, `Permissions-Policy`, `Cache-Control: no-store`. Sin cabecera `Server`. |
| Cabeceras SPA (nginx) | CSP estricta (`script-src 'self'`, `frame-ancestors 'none'`, `form-action` limitado a Entra ID), `nosniff`, `DENY`, `COOP`. `server_tokens off`. |
| Rate limiting | 300 req/min por usuario (o IP si anónimo); 20/min por IP en `/auth/login`; por credencial de equipo, 12 inventarios/h y 120 instantáneas SQL/h; 60 operaciones/h, globales en el proceso y contadas después de autorizar, a programar, cancelar, sincronizar y modificar lo programado de la agenda. `429` + `Retry-After`. |
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
- Producción con un servidor administrado: `Database__SslMode=VerifyFull`. En el **servidor único** PostgreSQL es un contenedor de la red `data` interna y se acepta
  `Database__SslMode=Disable` (desviación aceptada el 2026-10-25, SPEC-0009 R-3), con las copias cifradas fuera del servidor como compensación.

## 6. Logging seguro

- Logs estructurados JSON (`Information`, `Warning`, `Error`, `Critical`) con `TraceId`/`SpanId`.
- El log HTTP registra solo método, ruta, estado y duración: **sin query strings, cabeceras, cookies ni cuerpos**.
- Nunca se registran contraseñas, secretos, tokens, cookies, cadenas de conexión ni datos personales.
  Los denegados de autorización registran el `oid` (identificador seudónimo), nunca nombre ni correo.
- `IdentityModelEventSource.ShowPII` permanece en su valor por defecto (`false`).

## 7. HTTPS y TLS

- La terminación TLS se hace en el borde. En el servidor de producción es un proxy inverso del host delante de `frontend`
  (Caddy como referencia, [ADR-0018](specs/architecture/adr-0018-host-tls-reverse-proxy.md)); en otros despliegues puede ser un
  balanceador o el propio nginx con certificado. La API está preparada: `UseHsts` fuera de Development,
  `UseHttpsRedirection` activable con `Security:UseHttpsRedirection`, y respeta `X-Forwarded-Proto` del proxy confiable.
- Con el proxy del host, `frontend` solo escucha en `127.0.0.1`. nginx toma la IP del cliente de la última dirección de
  `X-Forwarded-For` solo cuando la conexión llega desde la red `edge`, y da por bueno `X-Forwarded-Proto` solo si vale `https`.
  Ese rango es `EDGE_TRUSTED_PROXY_FROM` (toda la subred de `edge` por defecto; se acota a la puerta de enlace tras comprobar en Linux el origen que ve nginx). **Modelo de confianza:**
  cualquier proceso del servidor que alcance el puerto de loopback es de confianza para esas cabeceras y podría fijar su IP y el esquema (riesgo residual R-5, aceptado en ADR-0018); la seguridad de ambos
  depende además de que `frontend` no se publique en otra interfaz (si se hiciera, un cliente remoto podría falsearlos). Si el origen no cae en el rango, todos los clientes comparten la IP de la puerta de enlace y sus límites por IP.
- Las cookies se emiten siempre con `Secure`. En local funciona sobre `http://localhost` porque los navegadores
  tratan `localhost` como contexto seguro; cualquier otro host exige HTTPS.

## 8. Consideraciones para producción (checklist)

- [ ] `ASPNETCORE_ENVIRONMENT=Production`, `APP_ALLOWED_HOSTS` con el dominio real.
- [ ] TLS en el borde y Redirect URIs `https://` en Entra ID.
- [ ] Credencial de Entra ID por certificado o identidad administrada (sin client secret).
- [ ] Secretos inyectados desde un gestor de secretos; rotación definida. **Desviación aceptada en el servidor único (SPEC-0009 R-4):** los secretos están en un `.env` de modo 600 del host,
      visible con `docker inspect` para quien controle el daemon; la persona administradora guarda una copia sellada fuera de línea.
- [ ] Cifrado en reposo del key ring de Data Protection (`ProtectKeysWithCertificate` / Azure Key Vault):
      hoy las claves se guardan sin cifrar en `app.data_protection_keys` (la API lo advierte al arrancar).
- [ ] PostgreSQL. Con un servidor administrado: TLS (`SslMode=VerifyFull`), backups y cifrado en reposo. **En el servidor único (SPEC-0009):** sin TLS dentro de la red `data` interna (R-3 aceptado);
      copia diaria **cifrada con `age` y copiada fuera del servidor** (S3 de otra cuenta, solo añadir, vencimiento a 30 días); restauración probada antes de publicar y cada trimestre (`scripts/restore-db.sh`);
      volumen EBS cifrado (`aws ec2 describe-volumes`).
- [ ] Imágenes fijadas por digest y escaneadas; `npm audit` / NuGet audit en CI. Implementado en SPEC-0009 (ADR-0020): el CI ejecuta `npm audit`, NuGet Audit y Trivy (bloquea alta o crítica con corrección) y `deploy.sh` despliega por digest. Verificar con la primera publicación.
- [ ] `AUTHZ_BOOTSTRAP_ROOT_OIDS` vacío una vez creado el Root (dejarlo solo como procedimiento de emergencia documentado).
- [ ] Política de retención y exportación de `app.audit_events`.
- [ ] Exportación de telemetría (`OTEL_EXPORTER_OTLP_ENDPOINT`) y alertas sobre `Error`/`Critical`.
- [ ] **IP real del cliente y esquema (SPEC-0002, ADR-0011, ADR-0018).** Con el proxy TLS del host, nginx usa `set_real_ip_from` con `EDGE_TRUSTED_PROXY_FROM` (toda la subred de `edge` por defecto; acótalo a
      `EDGE_NETWORK_GATEWAY/32` solo tras comprobar el origen en Linux, SPEC-0009 CA-22). Tras desplegar, comprueba en **Inventario** que la IP de conexión es la del cliente (no una `172.x`) y que el `POST` a
      `/api/v1/device-enrollments` con un `X-Forwarded-For` falsificado desde fuera da `401` con la IP real en el log de `api` (DEVELOPMENT.md §12.7, paso 5). `./scripts/test-nginx-realip.sh` ejecuta la tabla de ADR-0018.
- [ ] **`frontend` solo en loopback (ADR-0018).** Publícalo únicamente en `127.0.0.1` (`FRONTEND_PORT`). El cortafuegos del
      host debe abrir solo 22, 80 y 443 (el *security group* de AWS abre los mismos); Docker publica puertos saltándose `ufw`, así que no publiques nada más: `scripts/deploy.sh` se detiene si Compose publica algo distinto de
      `127.0.0.1:${FRONTEND_PORT}`. **SSH (22) está abierto a Internet** por decisión del solicitante (SPEC-0009 R-1: solo clave, sin `root` y `fail2ban`), no «restringido».
- [ ] Entregar los códigos de enrolamiento por un canal interno y seguro, solo a quien instala el agente. Si un código
      no usado pudo filtrarse, regenerarlo (o eliminar el equipo).
- [ ] **Revocar un dispositivo comprometido (SPEC-0003).** En **Inventario → Equipos**, *Eliminar* el equipo (permiso
      `devices.delete`): su código y su API key dejan de servir de inmediato y el historial se conserva. Para volver a
      inventariar esa máquina, regístrala como equipo nuevo. Registra la acción en el gestor de incidentes.
- [ ] **Auditoría SQL (SPEC-0005).** Revisar periódicamente las instancias con el aviso *Certificado sin verificar* y
      pasarlas a `verify` o `pinned` (ADR-0015). Comprobar con `itmas-agent sql test` que las cuentas del agente siguen
      con el mínimo privilegio. Revisar quién tiene `sql-audit.read` (expone las cuentas privilegiadas de los motores).
- [ ] **Agenda y Outlook (SPEC-0006, ADR-0016).** Antes de poner `AGENDA_OUTLOOK_ENABLED=true`: cumplir **SPEC-0008** (CA-24 con el tenant real), hacer los prerrequisitos del tenant y
      verificar el alcance (DEVELOPMENT.md §11.2: `Test-ServicePrincipalAuthorization` dentro del ámbito solo para el buzón de la agenda),
      confirmar que **no** hay consentimiento global de `Calendars.ReadWrite` en Entra ID y decidir la credencial: certificado, o secreto de
      caducidad corta aceptado de forma explícita (`security`). Repetir la verificación del alcance de forma periódica. Revisar quién tiene
      `agenda.publish` (envía invitaciones, actualizaciones y cancelaciones desde el buzón corporativo) y `agenda.types.manage`. Configurar `AGENDA_PUBLIC_URL` y los dominios
      permitidos. Anotar el vencimiento de la credencial en la agenda. Comprobar en Entra ID (Aplicaciones empresariales → Permisos) que **no**
      hay ninguna concesión de `Calendars.ReadWrite` para la aplicación, y limitar sus propietarios y quién puede añadirle credenciales: quien
      pueda hacerlo hereda el acceso al buzón de la agenda.
- [ ] Revisar periódicamente quién tiene `devices.update` (puede regenerar códigos) y `devices.reports.read` (datos personales).
- [ ] Publicar solo paquetes del agente generados por `scripts/build-agent.sh` y su `SHA256SUMS`: el *workflow* `Release` los construye y `deploy.sh` los verifica y no sobrescribe una versión publicada. Los binarios no están
      firmados (SPEC-0002 P-11, riesgo aceptado): la integridad depende de HTTPS y de que los usuarios verifiquen la suma.
- [ ] **Operación del servidor único (SPEC-0009, constitución §8.7).** `sudo ./scripts/harden-host.sh check` e `install-host-tasks.sh check` sin diferencias respecto a `deploy/**`.
      Caddy: administración desactivada (`admin off`), actualizado al menos cada 7 días (no lo actualiza `unattended-upgrades`), `/var/lib/caddy` en modo 700, huella de su clave GPG comprobada, HSTS escalonado
      (300 s, 86 400 s y 31 536 000 s tras una renovación real) y emisor fijado a Let's Encrypt.
- [ ] **Credenciales de despliegue.** El repositorio es público: el servidor clona por HTTPS **sin credencial** (no hay *deploy key*). Con los paquetes de GHCR públicos (recomendado, ADR-0020 §8) el servidor no guarda
      **ninguna** credencial de GitHub; si se dejan privados, solo un token personal clásico con `read:packages` de una cuenta de servicio, en el `docker login` de `root`, modo 600, rotado al caducar y sin acceso al código. El CI no
      tiene ninguna credencial hacia el servidor. En GitHub: entorno `production` con revisores obligatorios y limitado a las etiquetas `v*`, protección de `main` y de las etiquetas `v*`, y aprobación obligatoria de los
      *workflows* de colaboradores externos. Todo lo del servidor lo ejecuta `root` con `sudo` (nadie en el grupo `docker`, que equivale a `root`).
- [ ] **Repositorio público (decisión del solicitante, 2026-10-27).** Nada confidencial en el repositorio, en los *workflows* ni en las imágenes (§4 de la constitución): ni identificadores de cuentas de AWS, direcciones, *buckets* ni claves.
      La topología (un servidor, SSH con clave y `fail2ban`) y los riesgos aceptados de abajo son legibles por cualquiera: la seguridad no depende de ocultarlos, y las vulnerabilidades se reportan por un canal privado (§9; activa el
      reporte privado de vulnerabilidades de GitHub).
- [ ] **Recursos de AWS (SPEC-0009 §10.6).** IMDSv2 obligatorio con *hop limit* 1; rol de instancia que solo puede añadir objetos al prefijo de las copias; bucket de destino en otra cuenta con versionado, *Object Lock*
      y vencimiento a 30 días; *security group* con solo 22, 80 y 443 (`deploy/aws/README.md`).
- [ ] **Clave de las copias y URL de latido.** La clave privada de `age` nunca está en el servidor (gestor de contraseñas y copia sellada); la URL de latido del servicio de monitoreo es un secreto que solo vive en
      `/etc/itmas3/host.conf` (modo 600, `root`).

### Riesgos residuales aceptados en el servidor único (SPEC-0009, 2026-10-25)

Los acepta el solicitante (`jmartinez`) con las mitigaciones de la columna de decisión; se revisan al cambiar la topología o el número de personas administradoras (constitución §8.7 c).

| ID | Riesgo | Decisión |
|---|---|---|
| R-1 | SSH abierto a Internet | Aceptado con solo clave, sin `root` y `fail2ban` |
| R-2 | Una sola persona administradora (RTO, custodia de la clave y aprobación del entorno; sin separación de funciones) | Aceptado, con copia sellada de la clave y del `.env` |
| R-3 | `Database__SslMode=Disable` (el checklist pide `VerifyFull`) | Aceptado: red `data` interna de un solo host y copias cifradas |
| R-4 | Secretos en un `.env` modo 600 del host | Aceptado |
| R-5 | El loopback del host es de confianza total para `X-Forwarded-*` | Aceptado (ADR-0018, decisión 3), con el endurecimiento de la plantilla de nginx |
| R-7 | El repositorio es público: la topología y estos riesgos los puede leer cualquiera | Aceptado (decisión del solicitante, 2026-10-27): sin identificadores ni secretos, reporte privado de vulnerabilidades y aprobación de los *workflows* de colaboradores externos |

## 9. Reportar una vulnerabilidad

No abras un issue público. Contacta al responsable de seguridad del proyecto por un canal privado con
la descripción, el impacto y los pasos para reproducirla.
