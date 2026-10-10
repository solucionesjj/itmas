# Modelo de amenazas — línea base de la fundación

**Alcance:** navegador ↔ nginx ↔ API ↔ PostgreSQL, y API ↔ Microsoft Entra ID; módulo de autorización (SPEC-0001);
agente de inventario ↔ nginx ↔ API, y agente ↔ ipinfo.io (SPEC-0002); registro y consulta del inventario (SPEC-0003); manual de uso público (SPEC-0004).

## Activos

| Activo | Ubicación | Protección principal |
|---|---|---|
| Sesión del usuario | Cookie `__Host-itmas3-session` | HttpOnly, Secure, SameSite=Strict, cifrado Data Protection, expiración |
| Credencial del cliente Entra ID | Variable de entorno de `api` | Fuera del repositorio; recomendado certificado/identidad administrada |
| Key ring de Data Protection | `app.data_protection_keys` | Solo accesible por la API; cifrado en reposo pendiente para producción |
| Credenciales de BD | Variables de entorno | Fuera del repositorio; roles separados |
| Datos de la aplicación | PostgreSQL | Red interna, rol con DML mínimo |
| Códigos de enrolamiento por equipo | Respuesta de alta/regeneración (una vez); BD solo SHA-256 | 100 bits, un solo uso, `no-store`, invalidados al regenerar o eliminar; límite de frecuencia (ADR-0012) |
| API keys de dispositivos | Equipos (0600 / DPAPI); BD solo SHA-256 | Única por equipo, invalidada al reenrolar o al eliminar el equipo |
| Manual de uso | `/manual/` (estáticos públicos de la SPA) | Sin contenido confidencial (constitución §10.4); revisión de `security`; HTML saneado y CSP |
| Paquetes del agente | `/downloads/agent/` (volumen de solo lectura) | HTTPS + `SHA256SUMS` (sin firma de código) |
| Inventario (nombres de cuenta, IPs, ubicación) y correo del propietario | `app.inventory_reports`, `app.devices` | Acceso solo vía API con `devices.reports.read` / `devices.read`; retención de 90 días |

## STRIDE

| Categoría | Amenaza | Mitigación en la fundación |
|---|---|---|
| **S**poofing | Robo de token en el navegador (XSS) | BFF: no hay tokens en el navegador; CSP `script-src 'self'`; cookie HttpOnly. |
| | Suplantación de cabeceras `X-Forwarded-*` | Solo se aceptan desde la subred del proxy; nginx sobrescribe las del cliente. |
| **T**ampering | CSRF sobre endpoints con cookie | SameSite=Strict + cabecera `X-CSRF` + validación de `Origin`. |
| | Manipulación del historial de migraciones | Esquema `app_migrations` inaccesible para el rol de la API. |
| | Alteración de la auditoría | `audit_events` *append-only* para el rol de la API. |
| | Mass assignment | JSON estricto (propiedades desconocidas rechazadas); DTOs separados de entidades. |
| **R**epudiation | Acciones sin rastro | Logs estructurados con `TraceId` y `oid` en denegaciones. Auditoría de negocio: futura spec. |
| **I**nformation disclosure | Stack traces / detalles internos | Problem Details genéricos; probado por tests. |
| | Secretos en logs | Logging HTTP sin query/cabeceras/cuerpos; `IncludeErrorDetail=false`; `ToString()` sin contraseña. |
| | Open redirect en login | `returnUrl` debe ser local (backend y frontend). |
| | Clickjacking | `frame-ancestors 'none'`, `X-Frame-Options: DENY`. |
| **D**enial of service | Fuerza bruta / abuso | Rate limiting global y específico de `/auth/login`; límite de cuerpo 1 MB. |
| **S**poofing | Equipo falso enrolado con un código filtrado | Código por equipo y de un solo uso, 100 bits, 10/min por IP, auditoría `device.enrolled` con IP (ADR-0012). |
| | Usuario con `devices.update` regenera el código de un equipo activo y enrola otro en su lugar | Permiso explícito, auditoría (`device.enrollment-code.regenerated`, `device.relinked` con IP y *hostname*); el equipo legítimo recibe `401`. |
| | Máquina que se apropia del registro de otra presentando su `hardwareId` | Necesita un código vigente; hardware de otro equipo no eliminado → `409` auditado. |
| | Uso de la API key robada de un equipo | Clave por equipo, solo envía el inventario de ese equipo, invalidada al reenrolar. |
| **T**ampering | Inventario falseado por el propio equipo | Inherente al agente; la IP de conexión la registra el servidor y contrasta la IP declarada. |
| | Binario del agente alterado | Descarga HTTPS + `SHA256SUMS`; compilación reproducible en contenedor. |
| **I**nformation disclosure | Datos personales del inventario | Lectura con permisos separados (`devices.read`, `devices.reports.read`); nada en logs; el código del equipo solo en la respuesta que lo emite. |
| | XSS con datos declarados por un equipo | La SPA solo interpola texto (sin `innerHTML`); CSP sin `unsafe-inline` en scripts. |
| **D**enial of service | Avalancha de inventarios | 12/h por credencial, cuerpo < 1 MB, límites por colección, envío con variación aleatoria de ±30 min. |
| **E**levation of privilege | Agente con privilegios elevados usado como vector | Solo lectura, sin puertos, sin órdenes remotas, comandos con ruta absoluta sin *shell*, *sandbox* de systemd. |
| | Usuario autenticado accede a recursos | Default deny + permisos solo vía políticas asignadas explícitamente (SPEC-0001). |
| | Administrador delegado se auto-otorga permisos | Salvaguardas RN-9 a RN-12, Root no otorgable, intentos auditados (`denied`). |
| | Sistema sin Root / Root recuperado indebidamente | Siempre al menos un Root activo; *bootstrap* solo sin Root activo y auditado. |
| | Inyección SQL | EF Core parametrizado; prohibido SQL concatenado. |
| | Escape del contenedor | No root, `cap_drop: ALL`, `no-new-privileges`, `read_only`. |

## Riesgos residuales aceptados (fundación)

| Riesgo | Motivo | Tratamiento previsto |
|---|---|---|
| Key ring sin cifrado en reposo | Requiere certificado/Key Vault del entorno | Checklist de producción (SECURITY.md §8) |
| Variables de entorno visibles con `docker inspect` | Requisito del proyecto: secretos por variables de entorno | Gestor de secretos en producción |
| HTTP dentro de las redes Docker | Tráfico interno en red privada | TLS en el borde; mTLS si se distribuye en varios hosts |
| `style-src 'unsafe-inline'` | Requerido por Angular/Material | Revisar nonces de estilo cuando Angular lo soporte sin SSR |
| Auditoría de negocio fuera de autorización | Solo existe el módulo de autorización | Incluir en cada spec de features |
| Sesión revocada vigente hasta la siguiente solicitud | Validación por solicitud | Aceptable: no hay estado cliente con privilegios |
| Códigos de enrolamiento sin caducidad | Decisión del solicitante (SPEC-0003 P-3b) | Un solo uso; regenerar o eliminar el equipo invalida el código |
| El código del equipo pasa por el navegador del administrador | Hay que entregarlo a quien instala (SPEC-0003 §10.7) | Una sola vez, `no-store`, un solo uso |
| Nombre del colaborador deducible probando correos | La coincidencia por correo es la trazabilidad pedida (SPEC-0003 RF-4b) | Requiere `devices.create`/`devices.update` y queda auditado |
| Binarios del agente sin firma de código | Decisión del solicitante (P-11) | `SHA256SUMS`; reevaluar firma si la flota crece |
| Datos del inventario declarados por el equipo | Inherente a un agente | IP de conexión registrada por el servidor como contraste |
