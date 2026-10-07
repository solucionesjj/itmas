# Modelo de amenazas — línea base de la fundación

**Alcance:** navegador ↔ nginx ↔ API ↔ PostgreSQL, y API ↔ Microsoft Entra ID; módulo de autorización (SPEC-0001).

## Activos

| Activo | Ubicación | Protección principal |
|---|---|---|
| Sesión del usuario | Cookie `__Host-itmas3-session` | HttpOnly, Secure, SameSite=Strict, cifrado Data Protection, expiración |
| Credencial del cliente Entra ID | Variable de entorno de `api` | Fuera del repositorio; recomendado certificado/identidad administrada |
| Key ring de Data Protection | `app.data_protection_keys` | Solo accesible por la API; cifrado en reposo pendiente para producción |
| Credenciales de BD | Variables de entorno | Fuera del repositorio; roles separados |
| Datos de la aplicación | PostgreSQL | Red interna, rol con DML mínimo |

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
| **E**levation of privilege | Usuario autenticado accede a recursos | Default deny + permisos solo vía políticas asignadas explícitamente (SPEC-0001). |
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
