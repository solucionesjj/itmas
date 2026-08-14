# spec.md

> Management and Audit System IT-MAS

---

## A. Visión del Proyecto

---

### 1. Información General

# Información General

- **Nombre del Proyecto:** Management and Audit System (IT-MAS)
- **Tipo:** Plataforma de monitoreo, inventario y auditoría de infraestructura tecnológica
- **Versión del Documento:** 1.4 (Extensión: auditoría de reglas de Security Groups AWS — RF-21 a RF-27, CA-15 a CA-20; ver §21 y ADR-0013)
- **Stack Tecnológico:** Angular (frontend), Node.js (backend), MongoDB (persistencia)
- **Modalidad de ingesta:** API REST expuesta a internet, alimentada por agentes/nodos distribuidos
- **Plataformas objetivo:** Equipos de colaboradores (Windows/Linux/Mac) y servidores (Windows/Linux)

El sistema centraliza la recolección de inventarios de hardware/software y eventos de acceso, permitiendo detectar cambios en las características técnicas y accesos anómalos a nivel de sistema operativo y base de datos. El acceso al portal está controlado por **perfiles de usuario** (Administrador, Usuario y Auditor) con permisos diferenciados mediante RBAC.

---

### 2. Resumen Ejecutivo

# Resumen Ejecutivo

IT-MAS es una plataforma que permite a los equipos de TI **monitorear, inventariar y auditar** todos los activos de cómputo de una organización desde un punto central. Los nodos (equipos de colaboradores y servidores) envían periódicamente su inventario y eventos de acceso a una **API pública segura**, que los almacena en **MongoDB**.

Un **portal web en Angular** presenta estadísticas (distribución de equipos, sistemas operativos) y **alertas** (cambios de recursos como CPU/RAM/Disco, accesos en horarios no habituales a servidores). El acceso al portal está protegido y organizado mediante **perfiles de usuario** —Administrador, Usuario y Auditor— garantizando que cada rol acceda solo a las funciones que le corresponden.

El valor principal es la **trazabilidad histórica** de los cambios, la **detección temprana de riesgos de seguridad** y un **control de acceso granular** al portal, apoyando cumplimiento normativo y control operativo. El perfil Auditor forma parte del modelo desde la fase inicial de RBAC, con capacidades ampliadas en fases posteriores.

---

### 3. Objetivos

# Objetivos

## Objetivo General
Proveer una plataforma centralizada para el inventario, monitoreo y auditoría de equipos de cómputo y servidores de la infraestructura tecnológica, con acceso controlado por perfiles de usuario.

## Objetivos Específicos
1. Recolectar y almacenar el inventario técnico de cada equipo (hardware y software).
2. Detectar y registrar cambios en características técnicas (CPU, RAM, discos).
3. Auditar usuarios y accesos a nivel de sistema operativo.
4. Auditar usuarios y accesos a nivel de base de datos.
5. Recibir datos de múltiples nodos a través de una API expuesta en internet.
6. Ofrecer un portal web moderno con estadísticas y alertas.
7. Diferenciar equipos de colaboradores de equipos de infraestructura.
8. Gestionar el **acceso al portal mediante autenticación** y **perfiles de usuario** (Administrador, Usuario, Auditor) con permisos diferenciados.
9. Permitir al perfil Administrador **crear y gestionar usuarios** y **definir/configurar alertas**.
10. Permitir al perfil Usuario **consultar reportes y estadísticas** de forma segura.
11. Permitir al perfil Auditor **revisar eventos de auditoría y gestionar el estado de alertas**.

---

### 4. Alcance

# Alcance

## Incluido en la fase inicial
- **Ingesta de datos** vía API REST desde agentes en nodos.
- **Inventario** de equipos: colaboradores (Windows/Linux/Mac) y servidores (Windows/Linux).
- **Detección de cambios** en recursos (CPU, RAM, discos).
- **Auditoría de accesos** a nivel de SO y de base de datos.
- **Acceso al portal** con login seguro y control por **perfiles de usuario**.
- **Gestión de usuarios y perfiles** (crear, editar, activar/desactivar usuarios) por parte del Administrador.
- **Definición y configuración de alertas** por parte del Administrador.
- **Consulta de reportes y estadísticas** por parte del perfil Usuario.
- **Exportación de reportes** en PDF/CSV para los perfiles Usuario, Auditor y Administrador.
- **Revisión de eventos de auditoría y gestión del estado de alertas** por parte del perfil Auditor.
- **Portal web** con estadísticas:
  - Conteo de equipos (colaboradores vs infraestructura).
  - Distribución por sistema operativo.
- **Alertas**:
  - Cambios de características técnicas.
  - Accesos a servidores en horario no habitual.
- Persistencia en MongoDB.

## Perfiles definidos
- **Administrador:** crea usuarios, define/configura alertas, gestiona la plataforma.
- **Usuario:** consulta reportes y estadísticas.
- **Auditor:** revisa eventos de auditoría y gestiona el estado de alertas y eventos, sin permisos de administración.

> Nota: los tres perfiles (Administrador, Usuario, Auditor) forman parte del modelo RBAC desde la fase inicial. La ampliación de capacidades del Auditor y el RBAC avanzado se abordan en la Fase 2 (ver Roadmap).

---

### 5. Fuera del Alcance

# Fuera de Alcance (Fase Inicial)

- Desarrollo detallado de los **agentes de recolección** por SO (se asume que envían datos a la API).
- Gestión de parches o **remediación automática** de equipos.
- Integración con herramientas de **ITSM** (ServiceNow, Jira, etc.).
- Monitoreo de **rendimiento en tiempo real** (APM/telemetría continua).
- Auditoría de **aplicaciones de negocio** específicas.
- Gestión de **licencias de software** como módulo dedicado.
- Respaldo/DR de los propios equipos monitoreados.
- Módulo de facturación o gestión de costos de activos.
- **Federación de identidad** con proveedores externos (SSO/SAML/OIDC/LDAP): se contempla para fases posteriores; en la fase inicial la gestión de usuarios es local.
- **Autoservicio de registro** de usuarios (el alta de usuarios la realiza el Administrador).
- **MFA** y roles/permisos personalizables a medida (fases posteriores).

---

### 6. Stakeholders

# Stakeholders

| Rol | Interés / Responsabilidad |
|-----|---------------------------|
| **Administrador de TI** | Configura la plataforma, crea usuarios, define alertas, revisa inventarios. Corresponde al perfil **Administrador**. |
| **Auditor / Seguridad** | Consulta accesos anómalos y cambios de activos; gestiona el estado de alertas y eventos. Corresponde al perfil **Auditor**. |
| **Gerencia de Tecnología** | Visualiza estadísticas y reportes para tomar decisiones. Suele usar el perfil **Usuario**. |
| **Usuario consultor** | Consulta reportes y dashboards sin capacidades de administración. Corresponde al perfil **Usuario**. |
| **Colaboradores** | Sujetos del inventario (equipos monitoreados). |
| **DBA** | Interesado en la auditoría de accesos a bases de datos. |
| **Equipo de Desarrollo** | Construye y mantiene la plataforma. |

---

## B. Especificación Funcional

---

### 7. Casos de Uso

# Casos de Uso

## Ingesta y auditoría
### UC-01: Registrar inventario de nodo
Un agente envía el inventario de un equipo a la API; el sistema lo almacena y versiona.

### UC-02: Detectar cambio de características
Al recibir un nuevo inventario, el sistema compara con el anterior y genera alerta si cambian CPU/RAM/disco.

### UC-03: Registrar evento de acceso a SO
El agente reporta login/logout de usuarios; el sistema evalúa si es horario no habitual.

### UC-04: Registrar evento de acceso a BD
Se reciben accesos a bases de datos para auditoría.

## Portal y perfiles
### UC-05: Iniciar sesión en el portal
Un usuario ingresa sus credenciales; el sistema autentica, emite JWT y carga la interfaz según su perfil.

### UC-06: Cerrar sesión
El usuario cierra sesión y el sistema invalida el token de acceso.

### UC-07: Visualizar estadísticas / consultar reportes
Un usuario con perfil **Usuario**, **Auditor** o **Administrador** consulta dashboards de equipos y sistemas operativos, y genera/exporta reportes.

### UC-08: Consultar y gestionar alertas
Un **Administrador** o **Auditor** revisa la lista de alertas activas y actualiza su estado.

### UC-09: Crear y gestionar usuarios (Administrador)
El **Administrador** crea nuevos usuarios, asigna perfil, edita, activa/desactiva y restablece contraseñas.

### UC-10: Definir y configurar alertas (Administrador)
El **Administrador** crea y ajusta reglas de alertas (p. ej. umbrales de cambio de recursos, rango de horario habitual).

### UC-11: Gestionar el propio perfil
Cualquier usuario autenticado actualiza sus datos básicos y cambia su contraseña.

## Actividades adicionales recomendadas
- **UC-R1:** Exportar reportes en PDF/CSV. *(Confirmado en Fase 1 — ver RF-19, CA-13, Roadmap §20)*
- **UC-R2:** Recibir notificaciones (correo/webhook) al dispararse una alerta.
- **UC-R3:** Consultar el registro de auditoría de acciones administrativas (quién creó usuarios, quién modificó alertas).

---

### 8. Requerimientos Funcionales

# Requisitos Funcionales

## Ingesta y auditoría
- **RF-01:** La API debe recibir inventarios de nodos identificando tipo (colaborador/infraestructura).
- **RF-02:** El sistema debe almacenar cada inventario con marca de tiempo (histórico).
- **RF-03:** El sistema debe comparar inventarios consecutivos y detectar cambios en CPU, RAM y discos.
- **RF-04:** La API debe recibir eventos de acceso a nivel de SO (usuario, fecha/hora, host).
- **RF-05:** La API debe recibir eventos de acceso a nivel de base de datos.
- **RF-06:** El sistema debe generar alertas por cambios técnicos y accesos en horario no habitual.
- **RF-07:** El portal debe mostrar conteo de equipos por categoría y por SO.
- **RF-08:** El portal debe listar y filtrar alertas.
- **RF-09:** El sistema debe permitir configurar el rango de horario habitual.

## Acceso al portal y perfiles
- **RF-10:** El portal debe requerir **autenticación** (login) para acceder a cualquier funcionalidad.
- **RF-11:** El sistema debe soportar **perfiles de usuario**: Administrador, Usuario y Auditor, con permisos diferenciados.
- **RF-12:** El perfil **Administrador** debe poder **crear, editar, activar/desactivar** usuarios y asignarles perfil.
- **RF-13:** El perfil **Administrador** debe poder **definir y configurar alertas** (reglas y umbrales).
- **RF-14:** El perfil **Usuario** debe poder **consultar reportes y estadísticas**, sin acceso a funciones administrativas.
- **RF-15:** El perfil **Auditor** debe poder consultar y **actualizar el estado de las alertas** y eventos de auditoría.
- **RF-16:** El sistema debe aplicar **control de acceso basado en roles (RBAC)** en cada endpoint y vista del portal.
- **RF-17:** El usuario debe poder **cambiar su contraseña** y cerrar sesión.
- **RF-18:** El sistema debe registrar en un **log de auditoría** las acciones administrativas (creación de usuarios, cambios de alertas, inicios de sesión).
- **RF-19:** El portal debe permitir **exportar reportes** en PDF/CSV (Fase 1, confirmado por CA-13).
- **RF-20 (recomendado):** El sistema debe permitir **notificar alertas** por correo/webhook.

## Auditoría de firewall AWS (Extensión — ver §21)
- **RF-21:** El sistema debe sincronizar diariamente (hora configurable) y bajo demanda (disparo manual) el catálogo de Security Groups y sus reglas en todas las regiones habilitadas de una cuenta AWS.
- **RF-22:** Cada regla observada debe registrar un estado de control propio de IT-MAS (`pendiente`, `revisado`, `autorizado`, `eliminado`), nunca derivado de AWS, y preservar dicho estado entre sincronizaciones mientras la regla siga existiendo en AWS.
- **RF-23:** El perfil **Auditor** debe poder marcar una regla `pendiente` como `revisado`, indicando una observación obligatoria.
- **RF-24:** El perfil **Administrador** debe poder marcar una regla `revisado` como `autorizado`, indicando una observación obligatoria. Ningún perfil puede revisar y autorizar la misma regla.
- **RF-25:** El portal debe listar/filtrar (texto libre, grupo de seguridad, estado, rangos de fecha de creación/revisión/autorización) y exportar (CSV/PDF, ordenado por id de grupo y luego id de regla) el catálogo de reglas, para los perfiles Administrador, Auditor y Usuario (consulta).
- **RF-26:** Cada corrida de sincronización (manual o automatizada) debe registrar su resultado por grupo/región y un resumen (grupos y reglas procesadas, regiones y VPCs verificadas), en un registro propio distinto del `audit_log` general.
- **RF-27:** El portal debe indicar de forma visible cuántas reglas quedaron en estado `pendiente` desde la última sincronización exitosa.

---

### 9. Criterios de Aceptación

# Criterios de Aceptación

- **CA-01:** Dado un inventario válido enviado a la API, se persiste en MongoDB y retorna 201.
- **CA-02:** Dado un segundo inventario con distinta RAM, se genera una alerta de tipo `resource_change`.
- **CA-03:** Dado un acceso a servidor fuera del rango configurado, se genera alerta `off_hours_access`.
- **CA-04:** El dashboard muestra correctamente el total de equipos separados por colaborador e infraestructura.
- **CA-05:** El dashboard muestra la distribución por sistema operativo actualizada.
- **CA-06:** Las peticiones a la API sin token válido son rechazadas con 401.
- **CA-07:** Las alertas pueden filtrarse por tipo y rango de fechas.
- **CA-08:** Un usuario no autenticado que intenta acceder al portal es redirigido al login.
- **CA-09:** Un usuario con perfil **Usuario** que intenta acceder a la gestión de usuarios o a la configuración de alertas recibe **403 Forbidden** y no ve dichas opciones en el menú.
- **CA-10:** El **Administrador** puede crear un usuario asignándole un perfil, y ese usuario puede iniciar sesión con las credenciales creadas.
- **CA-11:** El **Administrador** puede definir una nueva alerta o modificar un umbral, y el motor de alertas aplica la configuración actualizada.
- **CA-12:** Cada acción administrativa (crear usuario, modificar alerta, inicio de sesión) queda registrada en el log de auditoría con usuario y marca de tiempo.
- **CA-13:** El perfil **Usuario** puede consultar y exportar reportes correctamente.
- **CA-14:** El perfil **Auditor** puede consultar eventos de auditoría y actualizar el estado de una alerta, sin acceder a la gestión de usuarios ni a la configuración de reglas.

## Auditoría de firewall AWS (Extensión — ver §21)
- **CA-15:** Una sincronización marca como `eliminado` toda regla que ya no existe en AWS y crea como `pendiente` toda regla nueva, sin alterar el estado de revisión/autorización de las reglas que siguen existiendo.
- **CA-16:** El perfil **Auditor** puede marcar una regla `pendiente` como `revisado` con observación; recibe **403** al intentar autorizarla.
- **CA-17:** El perfil **Administrador** puede marcar una regla `revisado` como `autorizado` con observación; recibe **403** al intentar revisar una regla `pendiente`.
- **CA-18:** El perfil **Usuario** puede consultar y exportar el catálogo de reglas, y recibe **403** en cualquier acción de revisión, autorización o sincronización.
- **CA-19:** El catálogo puede filtrarse combinando texto libre, grupo, estado y los tres rangos de fecha, y exportarse respetando esos filtros con el orden grupo→regla.
- **CA-20:** Cada corrida de sincronización (manual o automatizada) queda registrada con su resultado por grupo/región y resumen, y genera una entrada gruesa correspondiente en `audit_log`.

---

## C. Diseño Técnico

---

### 10. Arquitectura

# Arquitectura

## Vista General
Arquitectura de 3 capas con ingesta distribuida y control de acceso por roles:

```
[Agentes/Nodos] --HTTPS--> [API REST Node.js] --> [MongoDB]
                                   |
                              [Motor de reglas/alertas]
                                   |
[Portal Angular] <--REST/JWT-- [API Backend]
         ^
         |__ Auth & RBAC (perfiles: Administrador / Usuario / Auditor)
```

## Componentes
- **Agentes:** recolectan y envían datos (fuera del alcance de desarrollo inicial).
- **API de Ingesta (Node.js):** autentica nodos, valida y almacena.
- **Motor de Alertas:** procesa cambios y accesos anómalos según reglas configurables.
- **Módulo de Autenticación y Autorización:** gestiona login, emisión de JWT y **control de acceso basado en roles (RBAC)** para los perfiles Administrador, Usuario y Auditor.
- **Módulo de Gestión de Usuarios:** alta/edición de usuarios y asignación de perfiles.
- **API de Consulta:** sirve datos al portal, protegida por RBAC.
- **Frontend (Angular):** dashboards, alertas, gestión de usuarios y guards de ruta por perfil.
- **MongoDB:** almacenamiento de inventarios, eventos, alertas, usuarios y logs de auditoría.

Se recomienda desplegar la API detrás de un API Gateway/Reverse Proxy con TLS.

---

### 11. Modelo de Datos

# Modelo de Datos (MongoDB)

## Colección `devices`
```json
{
  "_id": "host-uuid",
  "hostname": "PC-001",
  "category": "collaborator | infrastructure",
  "os": { "name": "Windows", "version": "11" },
  "lastSeen": "ISODate"
}
```

## Colección `inventories` (histórico)
```json
{
  "deviceId": "host-uuid",
  "timestamp": "ISODate",
  "cpu": { "model": "...", "cores": 8 },
  "ram": { "totalGB": 16 },
  "disks": [{ "name": "C", "sizeGB": 512 }]
}
```

## Colección `access_events`
```json
{
  "deviceId": "host-uuid",
  "level": "os | database",
  "user": "jdoe",
  "timestamp": "ISODate",
  "action": "login"
}
```

## Colección `alerts`
```json
{
  "type": "resource_change | off_hours_access",
  "deviceId": "host-uuid",
  "detail": {},
  "createdAt": "ISODate",
  "status": "open | reviewed"
}
```

## Colección `users`
```json
{
  "_id": "user-uuid",
  "username": "jperez",
  "email": "jperez@empresa.com",
  "passwordHash": "...",
  "role": "administrator | user | auditor",
  "active": true,
  "createdBy": "user-uuid",
  "createdAt": "ISODate",
  "lastLogin": "ISODate"
}
```

## Colección `alert_rules`
```json
{
  "_id": "rule-uuid",
  "type": "resource_change | off_hours_access",
  "enabled": true,
  "config": { "resources": ["cpu","ram","disks"], "habitualHours": { "from": "07:00", "to": "19:00" } },
  "createdBy": "user-uuid",
  "updatedAt": "ISODate"
}
```

## Colección `audit_log`
```json
{
  "actorId": "user-uuid",
  "action": "create_user | update_alert_rule | login | update_alert_status",
  "target": "user-uuid | rule-uuid | alert-id",
  "detail": {},
  "timestamp": "ISODate"
}
```

---

### 12. Interfaces y APIs

# APIs e Interfaces

## API de Ingesta
- `POST /api/v1/inventory` — Recibe inventario de un nodo.
- `POST /api/v1/access-events` — Recibe eventos de acceso (SO y BD).

## API de Autenticación y Usuarios
- `POST /api/v1/auth/login` — Autentica usuario y retorna JWT.
- `POST /api/v1/auth/logout` — Cierra sesión / invalida token.
- `POST /api/v1/auth/refresh` — Renueva token.
- `POST /api/v1/auth/change-password` — Cambio de contraseña propia.
- `GET /api/v1/users` — Lista usuarios (solo Administrador).
- `POST /api/v1/users` — Crea usuario y asigna perfil (solo Administrador).
- `PATCH /api/v1/users/:id` — Edita/activa/desactiva usuario (solo Administrador).

## API de Alertas (configuración)
- `GET /api/v1/alert-rules` — Lista reglas de alertas.
- `POST /api/v1/alert-rules` — Crea regla de alerta (solo Administrador).
- `PATCH /api/v1/alert-rules/:id` — Modifica regla/umbral (solo Administrador).

## API de Consulta (Portal)
- `GET /api/v1/devices` — Lista/filtra equipos.
- `GET /api/v1/stats/os` — Distribución por SO.
- `GET /api/v1/stats/devices` — Conteo por categoría.
- `GET /api/v1/reports/export` — Exporta reportes (PDF/CSV).
- `GET /api/v1/alerts` — Lista de alertas con filtros.
- `PATCH /api/v1/alerts/:id` — Actualizar estado de alerta (Administrador/Auditor).

## Autenticación y Autorización
- Nodos: API Key o token por nodo.
- Portal: JWT tras login de usuario.
- **RBAC:** cada endpoint valida el perfil (Administrador, Usuario, Auditor) del token.

Formato de datos: **JSON**. Versionado por prefijo `/v1`.

---

### 13. Experiencia de Usuario

# Experiencia de Usuario

- **Framework:** Angular con diseño moderno y responsivo (Angular Material o similar).
- **Login:** acceso seguro con JWT; pantalla de inicio de sesión antes de cualquier vista.
- **Menú y vistas adaptadas al perfil:** mediante *route guards*, se muestran u ocultan opciones según el rol:
  - **Administrador:** ve todo (dashboards, alertas, gestión de usuarios, configuración de alertas).
  - **Usuario:** ve dashboards y reportes; no ve administración.
  - **Auditor:** ve dashboards, alertas, su gestión de estado y eventos de auditoría; no ve gestión de usuarios ni configuración de reglas.
- **Dashboard principal:**
  - Tarjetas resumen (total equipos, colaboradores, infraestructura).
  - Gráfico de distribución por sistema operativo (pie/bar).
- **Módulo de Alertas:**
  - Tabla filtrable por tipo, fecha y estado.
  - Indicadores visuales (rojo/amarillo) por severidad.
- **Módulo de Administración (solo Administrador):**
  - Gestión de usuarios (crear, editar, activar/desactivar, asignar perfil).
  - Configuración de reglas de alertas y horario habitual.
- **Reportes:** consulta y exportación (PDF/CSV) para perfiles Usuario, Auditor y Administrador.
- **Detalle de equipo:** histórico de inventarios y cambios.
- **Perfil propio:** cambio de contraseña y datos básicos.
- **Navegación:** menú lateral claro, breadcrumbs.
- **Principios:** simplicidad, carga rápida, accesibilidad (WCAG AA).

---

## D. Calidad y Atributos

---

### 14. Requerimientos No Funcionales

# Requisitos No Funcionales

- **Disponibilidad:** API de ingesta con 99.5% de uptime objetivo.
- **Rendimiento:** procesar la ingesta de un inventario en < 500 ms.
- **Escalabilidad:** soportar cientos de nodos con crecimiento horizontal.
- **Seguridad de acceso:** control por perfiles (RBAC) aplicado en backend y frontend; sesiones con expiración.
- **Mantenibilidad:** código modular, versionado en Git.
- **Portabilidad:** despliegue en contenedores (Docker).
- **Usabilidad:** interfaz intuitiva sin capacitación extensa; opciones visibles según perfil.
- **Retención de datos:** histórico de inventarios y logs de auditoría configurable.
- **Compatibilidad:** navegadores modernos (Chrome, Edge, Firefox).

---

### 15. Seguridad

# Seguridad

- **Transporte:** TLS/HTTPS obligatorio en toda comunicación (API expuesta a internet).
- **Autenticación de nodos:** API Key o token único por nodo, rotable.
- **Autenticación de usuarios:** JWT con expiración y refresh tokens.
- **Gestión de contraseñas:** almacenamiento con hash fuerte (bcrypt/argon2), política de complejidad y cambio de contraseña.
- **Autorización (RBAC):** perfiles **Administrador**, **Usuario** y **Auditor** con permisos diferenciados aplicados en cada endpoint y ruta del portal.
- **Principio de menor privilegio:** cada perfil accede solo a lo necesario (p. ej. Usuario solo consulta reportes; Auditor consulta y gestiona estado de alertas sin administrar).
- **Gestión de usuarios controlada:** solo el Administrador crea/edita usuarios; el alta no es autoservicio.
- **Validación de entrada:** sanitización y esquemas (evitar inyección NoSQL).
- **Rate limiting** y protección contra abuso en la API pública y en el login (anti fuerza bruta).
- **Cifrado en reposo:** datos sensibles en MongoDB.
- **Registro de auditoría** de acciones administrativas (creación de usuarios, cambios de alertas, inicios de sesión, cambios de estado de alertas).
- **Hardening** del gateway/reverse proxy.
- **MFA:** contemplado para fases posteriores (ver Roadmap).

---

### 16. Observabilidad

# Observabilidad

- **Logging estructurado** (JSON) en backend con niveles (info, warn, error).
- **Auditoría de acceso:** registro de inicios de sesión, intentos fallidos y acciones administrativas por usuario.
- **Métricas:** número de inventarios recibidos, latencia de API, alertas generadas, usuarios activos/sesiones.
- **Health checks:** endpoint `/health` para API y conexión a MongoDB.
- **Trazabilidad:** correlación de peticiones por request-id.
- **Monitoreo de errores:** integración con herramientas como Prometheus/Grafana o similar.
- **Alertas operativas:** notificación ante caída de la API o de la base de datos.

---

### 17. Estrategia de Pruebas

# Estrategia de Pruebas

- **Pruebas unitarias:** lógica de comparación de inventarios, reglas de alertas y validación de permisos por perfil (Jest).
- **Pruebas de integración:** endpoints de la API contra MongoDB de prueba, incluyendo autenticación y RBAC.
- **Pruebas de contrato:** validación de esquemas JSON de ingesta.
- **Pruebas E2E:** flujos del portal Angular por perfil (login, consulta de reportes como Usuario, gestión de estado de alertas como Auditor, creación de usuarios y configuración de alertas como Administrador) con Cypress.
- **Pruebas de seguridad:** validación de autenticación/autorización, control de acceso por rol (403 en accesos no permitidos) y rate limiting.
- **Pruebas de carga:** simulación de múltiples nodos enviando datos.
- **Cobertura objetivo:** ≥ 80% en lógica de negocio crítica.

---

## E. Operación

---

### 18. Despliegue

# Despliegue

- **Contenedores Docker** para API (Node.js) y frontend (Angular servido por Nginx).
- **MongoDB** gestionado (Atlas) o autoalojado con réplica.
- **Reverse Proxy / API Gateway** con TLS para exponer la API.
- **Entornos:** desarrollo, staging y producción.
- **CI/CD:** pipeline automatizado (build, test, deploy) con GitHub Actions o similar.
- **Variables de entorno** para credenciales, secretos JWT y configuración.
- **Semilla inicial:** creación de un usuario **Administrador** por defecto en el primer despliegue (con cambio de contraseña obligatorio).
- **Backups** periódicos de MongoDB.

---

## F. Gestión del Proyecto

---

### 19. Riesgos

# Riesgos

| Riesgo | Impacto | Mitigación |
|--------|---------|------------|
| API pública expuesta a ataques | Alto | TLS, API keys, rate limiting, WAF |
| Datos inconsistentes de agentes | Medio | Validación de esquemas y versionado |
| Volumen de histórico crece rápido | Medio | Políticas de retención e índices |
| Falsos positivos en alertas | Medio | Reglas configurables y umbrales |
| Dependencia de disponibilidad de nodos | Bajo | Estado 'lastSeen' y alerta de inactividad |
| Fuga de datos sensibles de auditoría | Alto | Cifrado, control de acceso por roles |
| Escalamiento de privilegios entre perfiles | Alto | RBAC estricto en backend, pruebas de autorización, principio de menor privilegio |
| Compromiso de credenciales de usuario | Alto | Hash fuerte, MFA (futuro), protección anti fuerza bruta, expiración de sesión |
| Alta administrativa mal gestionada | Medio | Auditoría de acciones y semilla de Administrador controlada |

---

### 20. Roadmap

# Roadmap

## Fase 1 — MVP (Inicial)
- API de ingesta de inventarios y eventos.
- Persistencia en MongoDB.
- Detección de cambios de recursos y accesos en horario no habitual.
- Portal con estadísticas básicas y listado de alertas.
- **Acceso al portal con login y RBAC para los perfiles Administrador, Usuario y Auditor** (creación de usuarios y definición de alertas por Administrador; consulta de reportes por Usuario; consulta de eventos y gestión de estado de alertas por Auditor).
- **Exportación de reportes (PDF/CSV)** para los perfiles Usuario, Auditor y Administrador (RF-19, CA-13).

> El desglose de implementación de Fase 1 en sub-fases (1.0 a 1.7, con dependencias y CA cubiertos por cada una) está documentado en `agent.md` §17.

## Fase 2
- **RBAC avanzado** y ampliación de capacidades del perfil Auditor, con auditoría de acciones extendida.
- Notificaciones (correo/webhook) de alertas.
- Dashboards adicionales.
- Gestión del propio perfil y políticas de contraseñas reforzadas.

## Fase 3
- Detección de inactividad de nodos.
- Integración con ITSM y SIEM.
- **Autenticación federada (SSO/OIDC/LDAP)** y MFA.
- Análisis de tendencias y machine learning para anomalías.

## Fase 4
- Gestión de licencias de software.
- Remediación y acciones automatizadas.
- Perfiles y permisos personalizables (roles a medida).

---

### 21. Extensión: Auditoría de Firewall AWS

# Auditoría de reglas de Security Groups AWS

> Extensión formal de IT-MAS fuera del roadmap original de Fases 1-4 (ver ADR-0013). Cubre RF-21 a RF-27 y CA-15 a CA-20. No sustituye ni se cruza con el modelo `devices`/`inventories` de agentes de recolección — es un catálogo independiente, poblado desde la API de AWS, no desde nodos.

## Alcance
Una única cuenta de AWS, todas sus regiones habilitadas (auto-descubiertas). Cubre exclusivamente Security Groups de EC2/VPC (no NACLs, WAF, ni otros servicios de red de AWS).

## Modelo de datos (MongoDB)

### Colección `security_group_rules` (catálogo vivo, sin expiración)
```json
{
  "_id": "ObjectId",
  "awsAccountId": "123456789012",
  "region": "us-east-1",
  "vpcId": "vpc-xxxx",
  "securityGroupId": "sg-xxxx",
  "securityGroupName": "web-servers",
  "attachedResources": [{ "resourceType": "ec2-instance", "resourceId": "i-xxxx", "resourceName": "web-01" }],
  "ruleId": "sgr-xxxx",
  "ruleName": "HTTPS desde internet",
  "direction": "ingress",
  "remoteEndpoint": { "kind": "cidr_ipv4", "value": "0.0.0.0/0" },
  "source": "0.0.0.0/0",
  "destination": "sg-xxxx",
  "protocol": "tcp",
  "portRange": "443",
  "status": "pendiente",
  "createdAt": "ISODate",
  "lastSeenAt": "ISODate",
  "reviewObservation": null,
  "reviewedAt": null,
  "reviewedBy": null,
  "authorizationObservation": null,
  "authorizedAt": null,
  "authorizedBy": null,
  "deletedAt": null
}
```

### Colección `aws_sync_runs` (log de corridas, retención configurable vía TTL)
```json
{
  "_id": "ObjectId",
  "triggerType": "manual | automated",
  "triggeredBy": "user-uuid | null",
  "startedAt": "ISODate",
  "finishedAt": "ISODate",
  "status": "success | partial_failure | failure",
  "regionsChecked": ["us-east-1", "us-west-2"],
  "vpcsChecked": ["vpc-xxxx"],
  "groupResults": [{ "region": "us-east-1", "vpcId": "vpc-xxxx", "groupId": "sg-xxxx", "groupName": "web-servers", "outcome": "success", "ruleCount": 4 }],
  "summary": { "groupsProcessed": 12, "groupsFailed": 0, "rulesProcessed": 48, "rulesCreated": 3, "rulesMarkedDeleted": 1 }
}
```

## API

- `GET /api/v1/security-group-rules` — lista paginada/filtrada/ordenable (Administrador, Auditor, Usuario).
- `GET /api/v1/security-group-rules/groups` — listado de grupos para el filtro (mismos roles).
- `GET /api/v1/security-group-rules/export` — exportación CSV/PDF respetando filtros, orden grupo→regla (mismos roles).
- `PATCH /api/v1/security-group-rules/:id/review` — `pendiente → revisado` (solo Auditor).
- `PATCH /api/v1/security-group-rules/:id/authorize` — `revisado → autorizado` (solo Administrador).
- `POST /api/v1/security-group-sync/run` — disparo manual (Administrador, Auditor).
- `GET /api/v1/security-group-sync/runs` — historial de corridas (Administrador, Auditor).
- `GET /api/v1/security-group-sync/summary` — indicador de pendientes desde la última corrida (Administrador, Auditor).
