# Backlog de IT-MAS

Listado único de requisitos pendientes, **sin división en fases**. Cada elemento tiene un id `BL-xxx`
estable y es solicitable de forma independiente: "implementa BL-017" es una instrucción completa y
suficiente, sin necesidad de arrastrar ningún bloque de alcance mayor.

Este documento sustituye a las Fases 2, 3 y 4 del roadmap original (`spec.md` §20). La Fase 1 (MVP)
y la extensión EXT-1 ya están entregadas y **no** se re-listan aquí; su alcance vive en `spec.md`
§8/§9/§21 y su trazabilidad en [`ca-traceability.md`](ca-traceability.md).

## Cómo se usa

- **Solicitar trabajo**: se pide por id (`BL-005`). Si un elemento tiene dependencias sin cerrar, se
  indica antes de empezar en vez de implementarlas silenciosamente de paso.
- **Numeración**: los ids no se reutilizan ni se renumeran. Un elemento descartado se marca
  `Descartado` y conserva su id. Los elementos nuevos continúan la secuencia.
- **Agrupación por tema (A–G)**: es solo ayuda de lectura. **No** es una fase, no impone orden de
  ejecución ni requiere completar un tema antes de tocar otro.
- **Requisitos**: cada elemento define sus propios criterios de aceptación aquí. No se acuñan nuevos
  `RF-xx`/`CA-xx` en `spec.md` para el trabajo pendiente — se evita así una segunda numeración
  compitiendo con esta. Cuando un elemento sí desarrolla un `RF`/`UC`/`CA` ya existente, se referencia
  en la columna de trazabilidad.
- **Al cerrar un elemento**: se marca `Hecho`, y si tocó arquitectura o el contrato de la API, se
  registra el ADR correspondiente en `docs/adr/` (Quality Gates, `agent.md` §11).

## Estado y prioridad

| Estado | Significado |
|---|---|
| `Pendiente` | No empezado. |
| `En curso` | Con trabajo iniciado en alguna rama. |
| `Hecho` | Entregado y verificado contra sus criterios de aceptación. |
| `Descartado` | Decidido no hacer; se conserva el id y el motivo. |

Prioridad: **Alta** (hueco funcional real o incumplimiento de un NFR declarado) · **Media** (valor
claro, sin bloqueo) · **Baja** (mejora a largo plazo, alto costo o poco valor inmediato).

---

## Resumen

| ID | Tema | Título | Prioridad | Estado | Depende de | Trazabilidad |
|----|------|--------|-----------|--------|------------|--------------|
| [BL-001](#bl-001) | A | Consulta del log de auditoría desde el portal | Alta | Pendiente | — | UC-R3, RF-18, CA-12 |
| [BL-002](#bl-002) | A | Consulta de eventos de acceso (SO y BD) desde el portal | Alta | Pendiente | — | RF-04, RF-05, UC-03, UC-04 |
| [BL-003](#bl-003) | A | Exportación del log de auditoría y de eventos de acceso | Media | Pendiente | BL-001, BL-002 | RF-19 |
| [BL-004](#bl-004) | A | Ampliar la cobertura del log de auditoría | Media | Pendiente | — | RF-18, CA-12 |
| [BL-005](#bl-005) | B | Motor de notificaciones y configuración por regla | Alta | Pendiente | — | RF-20, UC-R2 |
| [BL-006](#bl-006) | B | Canal de notificación por correo (SMTP) | Alta | Pendiente | BL-005 | RF-20, UC-R2 |
| [BL-007](#bl-007) | B | Canal de notificación por webhook | Alta | Pendiente | BL-005 | RF-20, UC-R2 |
| [BL-008](#bl-008) | B | Registro de entregas y reintentos de notificación | Media | Pendiente | BL-005 | RF-20 |
| [BL-009](#bl-009) | C | Gestión del propio perfil | Alta | Pendiente | — | UC-11, RF-17 |
| [BL-010](#bl-010) | C | Política de contraseñas reforzada y configurable | Alta | Pendiente | — | RF-17 |
| [BL-011](#bl-011) | C | Bloqueo de cuenta por intentos fallidos | Alta | Pendiente | — | §15 Seguridad |
| [BL-012](#bl-012) | C | RBAC avanzado y ampliación del perfil Auditor | Media | Pendiente | BL-001 | RF-11, RF-15, RF-16 |
| [BL-013](#bl-013) | C | Roles y permisos personalizables | Baja | Pendiente | BL-012 | RF-11 |
| [BL-014](#bl-014) | C | Autenticación multifactor (TOTP) | Media | Pendiente | BL-009 | §5 Fuera de alcance |
| [BL-015](#bl-015) | C | Autenticación federada (SSO / OIDC) | Media | Pendiente | — | §5 Fuera de alcance |
| [BL-016](#bl-016) | C | Integración con LDAP / Active Directory | Media | Pendiente | BL-015 | §5 Fuera de alcance |
| [BL-017](#bl-017) | D | Detección de inactividad de nodos | Alta | Pendiente | — | RF-06 |
| [BL-018](#bl-018) | D | Dashboards adicionales | Media | Pendiente | — | RF-07, UC-07 |
| [BL-019](#bl-019) | D | Análisis de tendencias históricas | Baja | Pendiente | BL-018 | UC-07 |
| [BL-020](#bl-020) | D | Detección de anomalías con machine learning | Baja | Pendiente | BL-019 | — |
| [BL-021](#bl-021) | E | Integración con SIEM | Media | Pendiente | BL-007 | §5 Fuera de alcance |
| [BL-022](#bl-022) | E | Integración con ITSM | Baja | Pendiente | BL-005 | §5 Fuera de alcance |
| [BL-023](#bl-023) | F | Inventario de software y gestión de licencias | Baja | Pendiente | — | §5 Fuera de alcance |
| [BL-024](#bl-024) | F | Remediación y acciones automatizadas | Baja | Pendiente | BL-005 | §5 Fuera de alcance |
| [BL-025](#bl-025) | G | Cerrar el NFR de latencia de ingesta (p95 < 500 ms) | Alta | Pendiente | — | NFR §14 |
| [BL-026](#bl-026) | G | Elevar la cobertura de pruebas a ≥ 80 % en lógica crítica | Media | Pendiente | — | Quality Gates §11 |
| [BL-027](#bl-027) | G | Verificación en navegador de CA-08 | Baja | Pendiente | — | CA-08 |
| [BL-028](#bl-028) | G | Corregir la testabilidad de `UsersListComponent` | Baja | Pendiente | — | — |

**Temas:** A · Auditoría y trazabilidad — B · Notificaciones — C · Identidad y control de acceso —
D · Monitoreo y detección — E · Integraciones externas — F · Gestión de activos — G · Calidad y deuda técnica

---

## A. Auditoría y trazabilidad

### BL-001
**Consulta del log de auditoría desde el portal** · Alta · Pendiente · Trazabilidad: UC-R3, RF-18, CA-12

Hoy `audit_log` se **escribe** en cada acción administrativa, pero `backend/src/modules/audit-log/`
no tiene controlador: no existe forma de leerlo salvo consultando MongoDB directamente. RF-18 exige
el registro, y UC-R3 la consulta; solo la primera mitad está entregada.

Criterios de aceptación:
1. `GET /api/v1/audit-log` devuelve el envelope paginado `{items,total,page,limit}` ya usado por
   `/devices` y `/alerts`.
2. Filtros validados en servidor: `action`, `actorId`, rango `from`/`to`.
3. Rol: Administrador y Auditor. Un perfil Usuario recibe **403**.
4. Vista Angular con tabla, filtros y paginación, enlazada desde el menú solo para los roles con acceso.
5. La respuesta nunca expone `passwordHash` ni secretos, aunque el `detail` registrado los mencionara.

Notas: reutilizar `AuditLogRepository`; el escape de regex para filtros de texto libre ya existe en
`common/util/escape-regex.util.ts`.

### BL-002
**Consulta de eventos de acceso (SO y BD) desde el portal** · Alta · Pendiente · Trazabilidad: RF-04, RF-05, UC-03, UC-04

`access_events` se ingesta con `level: os | database` (`access-event-level.enum.ts`) pero no existe
ningún endpoint de consulta: los datos entran y solo son visibles indirectamente, cuando el motor de
alertas genera un `off_hours_access`. El acceso a BD (RF-05) hoy no es observable de ninguna forma
desde el portal.

Criterios de aceptación:
1. `GET /api/v1/access-events` paginado, con el mismo envelope que el resto de endpoints de consulta.
2. Filtros validados: `level`, `action`, `user`, `deviceId`, rango `from`/`to`.
3. Rol: Administrador y Auditor (es material de auditoría, no de consulta general).
4. Vista Angular con tabla, filtros y paginación.
5. Un evento de nivel `database` es distinguible de uno de nivel `os` en la interfaz.

### BL-003
**Exportación del log de auditoría y de eventos de acceso** · Media · Pendiente · Depende de BL-001, BL-002 · Trazabilidad: RF-19

Extiende `GET /reports/export` con `reportType=audit-log` y `reportType=access-events`, en CSV y PDF.

Criterios de aceptación:
1. Ambos tipos aceptan los mismos filtros que su endpoint de consulta y los respetan en la salida.
2. Rol: Administrador y Auditor; un perfil Usuario recibe **403** en ambos tipos — misma comprobación
   condicional en servicio que ya existe para `reportType=alerts` (`ReportsService.generate()`).
3. La exportación reutiliza `csv.util.ts` y el generador PDF existentes, sin una segunda ruta de código.

### BL-004
**Ampliar la cobertura del log de auditoría** · Media · Pendiente · Trazabilidad: RF-18, CA-12

Acciones administrativas o sensibles que hoy no dejan rastro en `audit_log`: cierre de sesión,
cambio de contraseña propio, y exportación de reportes (quién extrajo qué datos, relevante para una
auditoría de fuga de información).

Criterios de aceptación:
1. Nuevos valores en `AuditLogAction` para cada acción incorporada.
2. Cada uno se registra con la convención existente `record(action, actorId, target, detail)`.
3. `detail` nunca contiene contraseñas, tokens ni claves de API.
4. Pruebas que verifican la entrada en `audit_log` tras cada acción.

---

## B. Notificaciones

### BL-005
**Motor de notificaciones y configuración por regla** · Alta · Pendiente · Trazabilidad: RF-20, UC-R2

Base común para BL-006 y BL-007: hoy una alerta se genera y queda esperando a que alguien entre al
portal. RF-20 está declarado como "recomendado" y nunca se implementó.

Criterios de aceptación:
1. `alert_rules` acepta una sección de configuración de notificación (canales activos y destinatarios)
   validada, como el resto de `config`, contra el `type` de la regla.
2. Al generarse una alerta, el motor despacha a los canales habilitados **sin bloquear** la respuesta
   de ingesta (el NFR de latencia de `POST /inventory` se mide sobre esa ruta — ver BL-025).
3. Un fallo de notificación nunca impide que la alerta se persista.
4. La configuración es editable por Administrador vía `PATCH /alert-rules/:id`.

### BL-006
**Canal de notificación por correo (SMTP)** · Alta · Pendiente · Depende de BL-005 · Trazabilidad: RF-20, UC-R2

Criterios de aceptación:
1. Configuración SMTP por variables de entorno, nunca en código ni en base de datos en claro.
2. Plantilla con tipo de alerta, equipo, marca de tiempo y enlace al detalle en el portal.
3. Credenciales SMTP ausentes o inválidas producen un error registrado, no una caída del proceso.

### BL-007
**Canal de notificación por webhook** · Alta · Pendiente · Depende de BL-005 · Trazabilidad: RF-20, UC-R2

Criterios de aceptación:
1. `POST` JSON al endpoint configurado, con firma HMAC verificable por el receptor.
2. URL de destino validada (rechazo de destinos internos no permitidos, para evitar SSRF).
3. Tiempo de espera acotado; un receptor lento no degrada la ingesta.

### BL-008
**Registro de entregas y reintentos de notificación** · Media · Pendiente · Depende de BL-005 · Trazabilidad: RF-20

Criterios de aceptación:
1. Colección propia con el resultado de cada intento (destino, canal, estado, error).
2. Reintento con retroceso exponencial y número máximo de intentos configurable.
3. Retención por TTL configurable, siguiendo el patrón de `ensure-ttl-index.util.ts`.

---

## C. Identidad y control de acceso

### BL-009
**Gestión del propio perfil** · Alta · Pendiente · Trazabilidad: UC-11, RF-17

UC-11 cubre dos capacidades: cambiar la contraseña propia (entregada en 1.1) y actualizar los datos
básicos propios (no entregada). Hoy un usuario no puede corregir su propio correo sin pedírselo a un
Administrador.

Criterios de aceptación:
1. `GET /api/v1/profile` y `PATCH /api/v1/profile` para el usuario autenticado, cualquier rol.
2. Solo permite datos básicos. El rol y el estado `active` **no** son modificables por esta vía
   (evita una escalada de privilegios trivial).
3. La respuesta pasa por `toUserResponse()`; `passwordHash`/`tokenVersion` nunca se exponen.
4. Correo duplicado devuelve **409**.
5. Vista de perfil en el portal, accesible a los tres roles.

### BL-010
**Política de contraseñas reforzada y configurable** · Alta · Pendiente · Trazabilidad: RF-17

Criterios de aceptación:
1. Longitud mínima, clases de carácter y caducidad configurables por entorno, no fijas en código.
2. Historial de contraseñas: rechazo de las N anteriores.
3. La política se aplica por igual en `/auth/change-password`, en `POST /users` y en el
   `PATCH /users/:id` que restablece contraseña — una sola implementación compartida, como hoy
   comparten `change-password.dto.ts`.
4. Una contraseña caducada fuerza el cambio en el siguiente inicio de sesión, con el mecanismo
   `mustChangePassword` existente.

### BL-011
**Bloqueo de cuenta por intentos fallidos** · Alta · Pendiente · Trazabilidad: `spec.md` §15

El límite de tasa de `POST /auth/login` (perfil `login`, sub-fase 1.7) acota por origen, no por
cuenta: un ataque distribuido contra un único usuario no lo activa.

Criterios de aceptación:
1. Tras N fallos consecutivos la cuenta se bloquea durante un periodo configurable.
2. El contador se reinicia con un inicio de sesión exitoso.
3. La respuesta al usuario bloqueado no revela si el usuario existe (sin enumeración de cuentas).
4. Bloqueo y desbloqueo quedan registrados en `audit_log`.
5. Un Administrador puede desbloquear manualmente.

### BL-012
**RBAC avanzado y ampliación del perfil Auditor** · Media · Pendiente · Depende de BL-001 · Trazabilidad: RF-11, RF-15, RF-16

Criterios de aceptación:
1. Definición explícita de las capacidades adicionales del Auditor, con ADR previo.
2. Cada capacidad nueva se refleja en `@Roles()` en backend y en los guards del portal.
3. Pruebas de 401/403 por rol para cada endpoint afectado (Quality Gates §11).

### BL-013
**Roles y permisos personalizables** · Baja · Pendiente · Depende de BL-012 · Trazabilidad: RF-11

Sustituye los tres roles fijos por permisos componibles. Cambio estructural: requiere ADR y muy
probablemente una versión `/v2` de los endpoints cuyo contrato cambie (`agent.md` §4 prohíbe cambios
silenciosos en `/v1`).

Criterios de aceptación:
1. Los tres roles actuales siguen existiendo como conjuntos de permisos predefinidos.
2. Ningún rol personalizado puede otorgar permisos que su creador no posee.
3. La migración de los usuarios existentes no interrumpe el acceso.

### BL-014
**Autenticación multifactor (TOTP)** · Media · Pendiente · Depende de BL-009 · Trazabilidad: `spec.md` §5

Criterios de aceptación:
1. Alta voluntaria de TOTP por el usuario, con códigos de recuperación de un solo uso.
2. Un Administrador puede exigir MFA por rol.
3. Los secretos TOTP se almacenan cifrados, nunca en claro.
4. `POST /auth/login` incorpora el segundo paso sin romper el contrato existente para las cuentas
   sin MFA.

### BL-015
**Autenticación federada (SSO / OIDC)** · Media · Pendiente · Trazabilidad: `spec.md` §5

Criterios de aceptación:
1. Inicio de sesión contra un proveedor OIDC externo, con el rol resuelto desde una reivindicación
   del proveedor o desde el usuario local.
2. La autenticación local sigue funcionando en paralelo (no es un reemplazo excluyente).
3. Ningún usuario federado obtiene rol de Administrador por omisión.

### BL-016
**Integración con LDAP / Active Directory** · Media · Pendiente · Depende de BL-015 · Trazabilidad: `spec.md` §5

Criterios de aceptación:
1. Validación de credenciales contra el directorio y correspondencia de grupos a roles de IT-MAS.
2. Conexión mediante LDAPS; nunca LDAP en claro.
3. Un directorio inaccesible no impide el inicio de sesión de las cuentas locales.

---

## D. Monitoreo y detección

### BL-017
**Detección de inactividad de nodos** · Alta · Pendiente · Trazabilidad: RF-06

Un equipo que deja de reportar es hoy invisible: `devices.lastSeen` se actualiza en cada ingesta,
pero nada evalúa su antigüedad. Un agente detenido —o un equipo retirado sin avisar— no genera
ninguna señal, que es justo el escenario que un sistema de auditoría debería detectar.

Criterios de aceptación:
1. Nuevo tipo de regla en `alert_rules` con umbral de inactividad configurable, siguiendo el modelo
   dirigido por configuración existente (nunca un umbral fijo en código).
2. Trabajo programado que evalúa `lastSeen` y genera una alerta de tipo nuevo por cada equipo
   inactivo.
3. No se generan alertas duplicadas mientras el equipo siga inactivo.
4. La alerta se resuelve o se marca automáticamente cuando el equipo vuelve a reportar.
5. La regla se siembra deshabilitada o con un umbral conservador, para no inundar de alertas una
   instalación existente al desplegar.

Notas: `@nestjs/schedule` ya es dependencia del proyecto (ADR-0015, sincronización de AWS).

### BL-018
**Dashboards adicionales** · Media · Pendiente · Trazabilidad: RF-07, UC-07

Criterios de aceptación:
1. Al menos: evolución de alertas en el tiempo, equipos con más cambios de recursos, y distribución
   horaria de accesos.
2. Los gráficos siguen la misma construcción sin librería del gráfico de SO existente, con la
   alternativa accesible que exige WCAG AA.
3. Cada consulta nueva se apoya en agregaciones en MongoDB, no en filtrado en el cliente.

### BL-019
**Análisis de tendencias históricas** · Baja · Pendiente · Depende de BL-018 · Trazabilidad: UC-07

Criterios de aceptación:
1. Comparación entre periodos y detección de desviaciones sobre el comportamiento habitual.
2. El cálculo respeta las políticas de retención por TTL: no asume datos ya purgados.

### BL-020
**Detección de anomalías con machine learning** · Baja · Pendiente · Depende de BL-019

Criterios de aceptación:
1. ADR previo que justifique el enfoque y dónde se ejecuta la inferencia.
2. Las anomalías detectadas se distinguen claramente de las alertas por regla determinista.
3. El sistema sigue siendo plenamente funcional con el módulo deshabilitado.

---

## E. Integraciones externas

### BL-021
**Integración con SIEM** · Media · Pendiente · Depende de BL-007 · Trazabilidad: `spec.md` §5

Criterios de aceptación:
1. Reenvío de alertas y eventos de acceso en un formato estándar (CEF o JSON estructurado).
2. Transporte configurable y cifrado.
3. Un SIEM inaccesible no degrada la ingesta ni la generación de alertas.

### BL-022
**Integración con ITSM** · Baja · Pendiente · Depende de BL-005 · Trazabilidad: `spec.md` §5

Criterios de aceptación:
1. Creación de un ticket al dispararse una alerta de los tipos configurados.
2. El identificador del ticket queda asociado a la alerta y visible en el portal.
3. La correspondencia entre tipo de alerta y cola/prioridad del ITSM es configurable.

---

## F. Gestión de activos

### BL-023
**Inventario de software y gestión de licencias** · Baja · Pendiente · Trazabilidad: `spec.md` §5

Criterios de aceptación:
1. Los agentes reportan software instalado; se persiste con el mismo criterio append-only de
   `inventories`.
2. Registro de licencias adquiridas y comparación contra lo instalado.
3. Alerta por incumplimiento (instalaciones por encima de lo licenciado).
4. Requiere una extensión del contrato de ingesta: ADR y, si rompe el contrato, `/v2`.

### BL-024
**Remediación y acciones automatizadas** · Baja · Pendiente · Depende de BL-005 · Trazabilidad: `spec.md` §5

Ejecutar acciones sobre los nodos invierte el modelo actual (hoy los agentes solo empujan datos hacia
la API, nunca reciben órdenes). Es el elemento de mayor impacto en seguridad del backlog.

Criterios de aceptación:
1. ADR previo sobre el modelo de control y su superficie de ataque.
2. Toda acción exige autorización explícita de un Administrador; nada se ejecuta automáticamente sin
   aprobación en la primera entrega.
3. Cada acción queda registrada en `audit_log` con actor, destino y resultado.
4. Los agentes validan el origen de la orden; una API comprometida no debe traducirse en ejecución
   arbitraria sobre el parque.

---

## G. Calidad y deuda técnica

### BL-025
**Cerrar el NFR de latencia de ingesta (p95 < 500 ms)** · Alta · Pendiente · Trazabilidad: NFR `spec.md` §14

`npm run load:smoke` midió **p95 ≈ 1096 ms** con 50 `POST /inventory` concurrentes, frente al objetivo
declarado de < 500 ms. La medición se hizo en una instancia única sin ajuste del pool de conexiones y
en un entorno compartido, así que la cifra no es concluyente — pero el NFR sigue sin estar demostrado.

Criterios de aceptación:
1. Medición reproducible en un entorno representativo, con el procedimiento documentado.
2. Ajuste del pool de conexiones de Mongoose y revisión de los índices en la ruta de ingesta.
3. p95 < 500 ms demostrado, o bien una revisión formal del NFR mediante ADR si se concluye que el
   objetivo original no era realista.

### BL-026
**Elevar la cobertura de pruebas a ≥ 80 % en lógica crítica** · Media · Pendiente · Trazabilidad: Quality Gates `agent.md` §11

La cobertura agregada ronda el 35 %: controladores, DTOs y wiring de módulos se cubren por e2e y no
por pruebas unitarias. El gate exige ≥ 80 % **sobre lógica crítica de negocio**, no sobre todo el
repositorio.

Criterios de aceptación:
1. Definición explícita de qué se considera lógica crítica, con esa frontera reflejada en la
   configuración de cobertura de Jest.
2. ≥ 80 % sobre ese conjunto, verificable con `npm run test:cov`.
3. El umbral queda configurado como gate en CI, no solo como informe.

### BL-027
**Verificación en navegador de CA-08** · Baja · Pendiente · Trazabilidad: CA-08

Único criterio de aceptación de Fase 1 sin verificación visual (la sesión donde se validó el resto no
tenía navegador disponible). El comportamiento está cubierto por lectura de código y por el 401 del
backend, pero no observado.

Criterios de aceptación:
1. Flujo de redirección a login comprobado en navegador real y registrado en `ca-traceability.md`.

### BL-028
**Corregir la testabilidad de `UsersListComponent`** · Baja · Pendiente

El componente importa `MatDialogModule` para obtener el servicio `MatDialog`. Como ese `@NgModule`
declara `providers: [MatDialog]`, se crea una instancia con alcance de componente que oculta
cualquier sustitución a nivel de `TestBed`, dejando el componente imposible de probar con un
`MatDialog` simulado. `DevicesListComponent` ya tuvo este mismo defecto y se corrigió retirando el
import (su plantilla no usa directivas `mat-dialog-*`).

Criterios de aceptación:
1. `MatDialogModule` retirado de los imports si la plantilla no usa sus directivas.
2. Prueba unitaria que verifica la apertura del diálogo con un `MatDialog` simulado.
