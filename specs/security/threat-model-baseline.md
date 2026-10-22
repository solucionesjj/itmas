# Modelo de amenazas — línea base de la fundación

**Alcance:** navegador ↔ nginx ↔ API ↔ PostgreSQL, y API ↔ Microsoft Entra ID; módulo de autorización (SPEC-0001);
agente de inventario ↔ nginx ↔ API, y agente ↔ ipinfo.io (SPEC-0002); registro y consulta del inventario (SPEC-0003); manual de uso público (SPEC-0004); agente ↔ motores SQL configurados y
auditoría de accesos SQL (SPEC-0005); agenda anual de infraestructura, API ↔ Microsoft Graph (`graph.microsoft.com` y
`login.microsoftonline.com`) y exportación `.ics` (SPEC-0006, ADR-0016).

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
| Credenciales de los motores SQL auditados | Equipo del agente (DPAPI de máquina / `0600` de `root`) | Una cuenta de solo lectura por motor; nunca en la API, logs ni argumentos (ADR-0014) |
| Cuentas, roles y permisos de los motores SQL | `app.sql_accounts`, `app.sql_access_items` | Solo vía API con `sql-audit.read`; sin hashes ni contraseñas; nombres nunca en logs; registro de cambios 1 año |
| Inventario (nombres de cuenta, IPs, ubicación) y correo del propietario | `app.inventory_reports`, `app.devices` | Acceso solo vía API con `devices.reports.read` / `devices.read`; retención de 90 días |
| Credencial de la aplicación de Graph (`AGENDA_OUTLOOK_CLIENT_SECRET`) | Variable de entorno de `api` | Fuera del repositorio, los logs y la línea de comandos; permiso acotado a un buzón con RBAC for Applications; certificado recomendado o secreto de caducidad corta (ADR-0016) |
| Calendario de actividades de seguridad e infraestructura (fechas de pruebas, rotaciones, congelamientos) y su espejo en el buzón compartido | `app.agenda_*`; calendario del buzón de la agenda | Acceso con `agenda.read`; el equipo solo lee el calendario de Outlook; sin valores de claves ni hallazgos (solo *cuándo* y *quién*) |

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
| **I**nformation disclosure | Mapa de cuentas privilegiadas de los motores SQL | Permiso propio `sql-audit.read`; sin hashes ni contraseñas; nombres solo como texto en la SPA y nunca en logs (SPEC-0005 §14.1). |
| **S**/**T**/**I** | Suplantación del motor o captura de la credencial del agente en conexiones `unverified` | TLS siempre; `verify` por defecto, `pinned` propuesto; `unverified` solo expreso, por conexión y señalado en ITMas; cuenta de solo lectura (ADR-0015). |
| **E**levation of privilege | Inyección SQL en el agente con nombres devueltos por el motor | Consultas de catálogo de texto fijo, una conexión por base de datos con el *builder*, sin `USE` ni concatenación (§13.6). |
| **D**enial of service | Instantáneas SQL grandes o en ráfaga procesadas en la petición | 16 MB solo en esa ruta, límites por colección, 120/h por equipo, procesamiento por lotes con métrica de duración y criterio de cola (SPEC-0005 RNF-3). |
| **E**levation of privilege | Agente con privilegios elevados usado como vector | Solo lectura, sin puertos, sin órdenes remotas, comandos con ruta absoluta sin *shell*, *sandbox* de systemd. |
| | Usuario autenticado accede a recursos | Default deny + permisos solo vía políticas asignadas explícitamente (SPEC-0001). |
| | Administrador delegado se auto-otorga permisos | Salvaguardas RN-9 a RN-12, Root no otorgable, intentos auditados (`denied`). |
| | Sistema sin Root / Root recuperado indebidamente | Siempre al menos un Root activo; *bootstrap* solo sin Root activo y auditado. |
| | Inyección SQL | EF Core parametrizado; prohibido SQL concatenado. |
| | Escape del contenedor | No root, `cap_drop: ALL`, `no-new-privileges`, `read_only`. |
| **S**/**E** | Usar ITMas para enviar invitaciones a destinatarios arbitrarios (spam o *phishing* desde el buzón corporativo) | `agenda.publish` separado de `agenda.update`, también para modificar lo ya Programado (enmienda del 2026-10-21); responsable y participantes invitados solo de los dominios permitidos, hasta 30 participantes; cuerpo en texto plano con campos del evento y un enlace a ITMas; máximo global por hora contado después de autorizar; auditoría de cada programación (SPEC-0006 §14.1). |
| **S**/**E** | Robo o abuso de la credencial de aplicación de Graph | Un solo buzón y un solo permiso por RBAC for Applications, sin consentimiento global; credencial solo en `api`; certificado o caducidad corta; el alcance se verifica con el tenant antes de activar Outlook (SPEC-0008) y después de forma periódica. |
| **T** | Alterar eventos publicados desde Outlook | Escritura solo de la aplicación y de los administradores del buzón; lo editado allí se sobrescribe (RN-14). |
| **R** | Cambios de fechas o cancelaciones sin rastro | Un evento de auditoría por operación `agenda.*`, *append-only*. |
| **I** | La agenda revela la postura de seguridad | `agenda.read`; invitaciones solo a dominios permitidos; calendario compartido de solo lectura; títulos neutros por guía. Riesgo residual: un secreto o hallazgo escrito por error en un título no se puede impedir. |
| **I** | Un error de Graph expone nombres de buzón o detalles del tenant | Solo un código normalizado; el cuerpo de Graph no se registra ni se devuelve; los *spans* de Graph no se exportan. |
| **D** | Límites de Graph, indisponibilidad, series o consultas enormes | No bloquea el uso; 10 s máximo; lotes de 50 detenidos ante `429`; 60 ocurrencias, 366 días y 1 000 resultados. |
| **D** | Agotar el límite de avisos de la agenda con peticiones anónimas o sin permiso | El límite se gasta después de la sesión, la cabecera anti-CSRF y la autorización (SPEC-0006, hallazgo F-1, 2026-10-21): una petición rechazada no lo toca. |
| **T** | Inyección de propiedades en el `.ics` (saltos de línea) | Escape y plegado RFC 5545, con pruebas (CA-19); sin personas en el archivo. |
| **T** | SSRF desde el adaptador de Graph | Solo `login.microsoftonline.com` y `graph.microsoft.com`, rutas fijas, sin redirecciones; ninguna URL procede de la entrada. |
| **T** | Cadena de suministro de FullCalendar (dependencia nueva del frontend) | Versión exacta y `package-lock.json`, solo complementos estándar, empaquetado con el build sin CDN, `script-src 'self'`, `npm audit` al añadirla y en cada actualización (ADR-0017). Riesgo residual: 7 de los 8 paquetes nuevos tienen un único mantenedor y no publican atestaciones; una prueba estática prohíbe que la feature le dé HTML a dibujar. |

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
| Conexiones a motores SQL con certificado sin verificar (`unverified`) | Decisión del solicitante (SPEC-0005 P-26, ADR-0015) | Aviso en ITMas; revisión periódica para pasar a `verify` o `pinned` |
| En MariaDB, la cuenta mínima del agente puede leer hashes de `mysql.global_priv` | La tabla no admite permisos por columna (ADR-0014) | El agente nunca los consulta; la credencial se protege como sensible |
| Accesos SQL declarados por el equipo | Inherente a un agente | Instancias ligadas al equipo autenticado; revisión periódica por personas |
| Secreto o hallazgo escrito por error en un título, descripción o etiqueta de la agenda | No se puede detectar de forma fiable (SPEC-0006 §14.1) | Ayuda visible en el campo y en el manual; los logs no registran títulos |
| Credencial de la aplicación de Graph como secreto de cliente | Decisión de ADR-0016; el código de la v1 solo admite secreto y pasar a certificado exige código. **Aceptado por el solicitante** hasta producción (SPEC-0006 §19.8) | Caducidad corta, vencimiento anotado en la agenda; el certificado, en SPEC-0008 |
| Invitaciones legítimas pero abusivas desde el buzón corporativo | El correo sale del propio tenant. **Aceptado** (SPEC-0006 §19.8) | `agenda.publish` (también para modificar lo Programado), dominios permitidos, límites y auditoría |
| El límite de avisos cuenta peticiones, no destinatarios, y vive en la memoria de cada proceso | Decisión de SPEC-0006 (P-26, F-3). **Aceptado** (§19.8) | 60 peticiones/h por defecto; si Exchange limita el buzón, bajar `AGENDA_PUBLISH_REQUESTS_PER_HOUR` |
| El acotamiento de la aplicación de Graph a un buzón no está verificado con el tenant | Depende de la configuración del tenant (CA-24). **Aceptado** (§19.8) | Outlook no se activa en producción sin cumplir SPEC-0008; verificación periódica del alcance |
| Cadena de suministro de FullCalendar (7 de 8 paquetes con un solo mantenedor, sin atestaciones) | Decisión de ADR-0017. **Aceptado** (§19.8) | Versiones exactas, `package-lock.json` con `integrity`, sin CDN, `script-src 'self'`, `npm audit` en cada actualización |
| Lo enviado a Microsoft 365 (invitaciones, calendario compartido) queda fuera del control de `agenda.read`; cancelar no lo borra | Inherente a publicar en Outlook. **Aceptado** (§19.8) | Calendario de solo lectura para el equipo; dominios permitidos; títulos neutros |
| Enlaces de evidencia con *tokens* en la consulta (SAS, «cualquiera con el enlace») | RN-10 valida esquema y credenciales en la autoridad, no la consulta. **Aceptado** (§19.8) | Ayuda en el manual; quedan también en la auditoría, que no se purga |
