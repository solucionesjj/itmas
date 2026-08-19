# agent.md — IT-MAS (Management and Audit System)

> Guía operativa permanente para toda IA, desarrollador o agente automatizado que implemente, mantenga, evolucione o valide este proyecto. Este documento es de cumplimiento obligatorio y prevalece sobre convenciones personales. Ante conflicto con `spec.md`, prevalece `spec.md`; ante ambigüedad, aplica la sección **Decision Framework**. **Excepción acotada**: para la capa visual del frontend — color, tipografía, espaciado, forma, elevación, estados y catálogo de componentes — la norma es `design.md`, que prevalece sobre este documento en ese ámbito y solo en ese ámbito; en todo lo demás (arquitectura, seguridad, RBAC, pruebas, DevOps, alcance) `design.md` cede ante este documento.

---

## 1. Project Context

IT-MAS es una plataforma centralizada de **monitoreo, inventario y auditoría** de infraestructura tecnológica. Recibe inventarios de hardware/software y eventos de acceso desde agentes distribuidos (equipos de colaboradores Windows/Linux/Mac y servidores Windows/Linux) a través de una **API REST pública segura**, los persiste en **MongoDB** y expone un **portal web en Angular** con estadísticas y alertas.

Núcleo funcional:
- Ingesta de inventarios versionados por nodo.
- Detección de cambios técnicos (CPU, RAM, discos).
- Auditoría de accesos a nivel de SO y base de datos.
- Alertas por cambio de recursos y accesos en horario no habitual.
- Portal con RBAC de tres perfiles: **Administrador**, **Usuario**, **Auditor**.

Stack fijo: **Node.js** (backend/API), **Angular** (frontend), **MongoDB** (persistencia), **Docker** (empaquetado), despliegue tras **reverse proxy/API Gateway con TLS**.

**El proyecto no se planifica por fases.** La Fase 1 (MVP) y la extensión EXT-1 ya están entregadas; el trabajo pendiente vive como requisitos individuales en `docs/backlog.md`, cada uno con un id `BL-xxx`. Implementa **únicamente el elemento del backlog que se te solicite**, sin arrastrar otros de paso: si detectas que uno tiene dependencias sin cerrar, indícalo antes de empezar. Los agentes de recolección por SO están **fuera del alcance de desarrollo**: asume que envían datos a la API.

---

## 2. Product Vision

Entregar a los equipos de TI una **única fuente de verdad** sobre el estado y la evolución de sus activos de cómputo, con:
- **Trazabilidad histórica** de cambios en cada equipo.
- **Detección temprana** de riesgos de seguridad (cambios de hardware no autorizados, accesos anómalos a servidores).
- **Control de acceso granular** al portal apoyando cumplimiento normativo y control operativo.

El éxito del producto se mide por la fiabilidad de la ingesta, la precisión de las alertas (bajo ratio de falsos positivos), la seguridad de la API pública y la claridad del portal para cada perfil.

---

## 3. Agent Mission

Tu misión como agente es **implementar y evolucionar IT-MAS con calidad de producción**, cumpliendo estrictamente el spec y este documento. Debes:

1. Producir código modular, seguro, probado y observable.
2. Aplicar RBAC estricto en **backend y frontend** en toda ruta y endpoint.
3. Priorizar seguridad de la API pública sobre conveniencia de desarrollo.
4. Mantener consistencia con el modelo de datos, contratos de API y arquitectura de 3 capas definidos.
5. Documentar todo supuesto en la sección **Assumptions** y toda decisión relevante en un ADR.
6. No introducir dependencias, servicios o alcance no aprobados por el spec o por esta guía.
7. Rechazar o marcar cualquier tarea que viole reglas de seguridad, RBAC o alcance de fase.

---

## 4. Architectural Principles

- **Arquitectura de 3 capas** con ingesta distribuida: `Agentes → API REST Node.js → MongoDB`, más `Portal Angular ↔ API` protegido por Auth+RBAC, y un **Motor de Reglas/Alertas** desacoplado.
- **Separación de responsabilidades**: mantén separados los módulos de Ingesta, Autenticación/Autorización, Gestión de Usuarios, Motor de Alertas y API de Consulta.
- **Modularidad**: organiza el backend por dominio (feature-based), no por tipo de archivo. Cada módulo expone servicios; los controladores solo orquestan.
- **Stateless API**: el backend no mantiene estado de sesión en memoria; usa JWT y almacenamiento persistente. Habilita escalado horizontal.
- **Contract-first**: los contratos de API (`/api/v1/...`) y los esquemas JSON son la interfaz autoritativa. No cambies contratos sin versionar.
- **Versionado de API por prefijo** (`/v1`). Cambios incompatibles requieren nueva versión.
- **Configuración externalizada**: todo secreto, umbral operativo y credencial vive en variables de entorno, nunca en código.
- **Defensa en profundidad**: valida y autoriza en cada capa; nunca confíes en el frontend para autorización.
- **Idempotencia y trazabilidad**: cada petición lleva `request-id`; la ingesta debe tolerar reenvíos de nodos.
- **Extensibilidad controlada**: las reglas de alertas son configurables vía `alert_rules`; el motor lee configuración, no la hardcodea.

---

## 5. Technical Standards

### 5.1 Backend (Node.js)

- **Runtime**: Node.js LTS (≥ 20). Usa TypeScript por defecto para tipado fuerte y mantenibilidad. Módulos ES.
- **Framework**: Express o NestJS (preferido NestJS por su modularidad, DI y guards nativos para RBAC). Documenta la elección en un ADR.
- **Estructura**: `src/modules/<dominio>/{controller,service,repository,dto,schema}`. Capa de acceso a datos aislada (repositorios).
- **Validación**: valida TODA entrada con esquemas (Zod, class-validator o Joi). Rechaza payloads no conformes con 400. Nunca pases input crudo a queries de Mongo.
- **Errores**: manejo centralizado de errores; respuestas de error consistentes `{ error: { code, message, requestId } }`. Nunca expongas stack traces ni detalles internos al cliente.
- **Async**: usa async/await; prohíbe callbacks anidados. Maneja todas las promesas (sin promesas colgantes).
- **Estilo**: ESLint + Prettier obligatorios. Sin `any` implícito. Sin `console.log` en producción (usa el logger estructurado).
- **Dependencias**: minimiza dependencias; audita con `npm audit`. No añadas librerías con vulnerabilidades conocidas.

### 5.2 Frontend (Angular)

- **Framework**: Angular (última versión estable soportada) con TypeScript strict.
- **UI**: Angular Material para diseño moderno, responsivo y accesible (**WCAG AA**).
- **Sistema de diseño**: [`design.md`](design.md) es **normativo** para todo lo visual bajo `frontend/src/` (color, tipografía, espaciado, forma, elevación, estados y el catálogo de componentes de su §9) y prevalece sobre esta sección en ese ámbito. Léelo completo antes de escribir o modificar código de frontend. Reglas duras que no se negocian: ningún literal de color en un componente (solo `var(--mat-sys-*)` o los tokens de `frontend/src/styles/_tokens.scss`); `#F2982A` es color de detalle y nunca un relleno; toda pantalla correcta en claro **y** en oscuro; espaciado desde la escala de 4px (`--sp-*`); anillo `:focus-visible` visible y área de toque >=44px en todo elemento interactivo; los cuatro estados (cargado, vacío, cargando, error) en toda vista de datos. Material 3 vía `mat.theme()`; no uses la API antigua `mat.define-theme()`. Las paletas tonales de `frontend/src/styles/_theme-colors.scss` son **generadas**: no edites sus tonos a mano, regenéralas desde las semillas de `design.md` §2.1. La adopción va por etapas (`design.md` §14) y se rastrea en `BL-029`; ver ADR-0017.
- **Arquitectura**: organización por features/modules con lazy loading. Componentes tontos vs. contenedores.
- **Estado**: servicios con RxJS o signals; evita estado global innecesario. Manejo de suscripciones con `takeUntilDestroyed`/async pipe (sin fugas de memoria).
- **Route Guards**: aplica guards por perfil (`AdministradorGuard`, `AuditorGuard`, `UsuarioGuard`) y un `AuthGuard` global. **El menú y las vistas se muestran/ocultan según el rol**, pero el ocultamiento visual NO reemplaza la autorización en backend.
- **Auth**: interceptor HTTP que adjunta el JWT; interceptor que maneja 401 (redirige a login) y 403 (mensaje de acceso denegado).
- **Estilo**: ESLint (angular-eslint) + Prettier. Sin lógica de negocio en templates.
- **UX obligatoria**: login previo a cualquier vista, dashboard con tarjetas resumen y gráficos (pie/bar por SO), tabla de alertas filtrable con indicadores de severidad, breadcrumbs y menú lateral claro, carga rápida.

### 5.3 Database (MongoDB)

- **Colecciones exactas** según el modelo de datos: `devices`, `inventories`, `access_events`, `alerts`, `users`, `alert_rules`, `audit_log`. No renombres campos ni colecciones sin ADR.
- **Índices obligatorios**:
  - `inventories`: `{ deviceId: 1, timestamp: -1 }`.
  - `access_events`: `{ deviceId: 1, timestamp: -1 }`, `{ level: 1 }`.
  - `alerts`: `{ type: 1, createdAt: -1 }`, `{ status: 1 }`.
  - `devices`: `{ category: 1 }`, `{ "os.name": 1 }`, `{ lastSeen: -1 }`.
  - `users`: `{ username: 1 }` único, `{ email: 1 }` único.
  - `audit_log`: `{ actorId: 1, timestamp: -1 }`, `{ action: 1 }`.
- **Histórico**: `inventories` es append-only; nunca sobrescribas inventarios. `devices.lastSeen` se actualiza en cada ingesta.
- **Timestamps**: siempre `ISODate` en UTC. La lógica de horario habitual maneja zona horaria explícitamente (documentar la zona en configuración).
- **Consultas seguras**: usa consultas parametrizadas del driver/ODM (Mongoose recomendado). Prohíbe interpolar strings en queries. Sanitiza claves para evitar operadores maliciosos (`$`, `.`).
- **Retención**: implementa políticas de retención configurables para `inventories`, `access_events` y `audit_log` (TTL o job de purga controlado).
- **Cifrado en reposo**: habilita cifrado a nivel de almacenamiento/clúster. `passwordHash` nunca se expone en respuestas.

### 5.4 APIs

- **Formato**: JSON. Prefijo `/api/v1`.
- **Endpoints (contrato autoritativo)**:
  - Ingesta: `POST /inventory`, `POST /access-events`.
  - Auth: `POST /auth/login`, `POST /auth/logout`, `POST /auth/refresh`, `POST /auth/change-password`.
  - Usuarios (Administrador): `GET /users`, `POST /users`, `PATCH /users/:id`.
  - Reglas de alerta (Administrador): `GET /alert-rules`, `POST /alert-rules`, `PATCH /alert-rules/:id`.
  - Consulta: `GET /devices`, `GET /stats/os`, `GET /stats/devices`, `GET /reports/export`, `GET /alerts`, `PATCH /alerts/:id` (Administrador/Auditor).
  - Salud: `GET /health`.
- **Códigos HTTP**: `201` creación de inventario/recurso, `200` consulta, `400` validación, `401` sin token válido, `403` sin permiso, `404` no encontrado, `429` rate limit, `5xx` error de servidor.
- **Autenticación dual**: nodos usan **API Key/token por nodo** (rotable); portal usa **JWT** de usuario. No mezcles ambos mecanismos en un mismo endpoint.
- **RBAC por endpoint**: cada endpoint declara y valida el perfil requerido. La ausencia de declaración de rol es un error de implementación.
- **Paginación y filtros**: `GET /devices` y `GET /alerts` deben soportar paginación y filtros (tipo, rango de fechas, estado). Filtros validados server-side.
- **Versionado**: no rompas `/v1`; cambios incompatibles → `/v2`.

---

## 6. Security Rules

1. **TLS/HTTPS obligatorio** en toda comunicación. Rechaza tráfico no cifrado. La API está expuesta a internet.
2. **Autenticación de nodos** con API Key o token único por nodo, **rotable**. Nunca compartas una única key global.
3. **Autenticación de usuarios** con **JWT** de vida corta + **refresh tokens**. Expiración obligatoria de sesión. `logout` invalida el token (lista de revocación o rotación de refresh).
4. **Contraseñas** almacenadas con **bcrypt o argon2** (nunca MD5/SHA simple ni texto plano). Aplica política de complejidad y cambio de contraseña.
5. **RBAC estricto** en backend como fuente de verdad; el frontend solo mejora UX. Aplica **principio de menor privilegio**:
   - **Administrador**: gestión de usuarios, definición/configuración de alertas, gestión total.
   - **Usuario**: solo consulta de reportes y estadísticas.
   - **Auditor**: consulta de eventos de auditoría y **gestión del estado** de alertas; sin gestión de usuarios ni configuración de reglas.
6. **Alta de usuarios controlada**: solo el Administrador crea/edita usuarios. **Prohibido el autoservicio de registro**.
7. **Validación y sanitización** de toda entrada para prevenir **inyección NoSQL** y XSS. Escapa claves con `$`/`.`.
8. **Rate limiting** en la API pública y protección **anti fuerza bruta** en `/auth/login` (bloqueo/backoff tras intentos fallidos).
9. **Cifrado en reposo** de datos sensibles en MongoDB.
10. **Registro de auditoría** obligatorio de: creación/edición de usuarios, cambios de reglas de alerta, inicios de sesión (éxito y fallo), cambios de estado de alertas.
11. **Secretos** solo en variables de entorno / gestor de secretos. Prohibido comitear secretos, keys o `.env` reales. Escanea en CI.
12. **Hardening** del reverse proxy/gateway (cabeceras de seguridad, límites de payload, timeouts).
13. **No exponer** `passwordHash`, secretos JWT ni detalles internos en respuestas ni logs.
14. **MFA y federación de identidad** están fuera de Fase 1: no los implementes ahora, pero no bloquees su futura incorporación.

---

## 7. Development Rules

- **Control de versiones**: Git obligatorio. Trabaja en ramas por feature (`feature/`, `fix/`, `chore/`). No commits directos a `main`.
- **Pull Requests**: obligatorios, con revisión. Todo PR debe pasar los Quality Gates (sección 11).
- **Commits**: convención Conventional Commits (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`).
- **Trazabilidad**: cada cambio referencia el requisito (RF-xx / UC-xx / CA-xx) que satisface.
- **DTOs y esquemas**: define contratos de entrada/salida explícitos; nunca devuelvas documentos crudos de Mongo.
- **No hardcodear**: umbrales de alerta, horario habitual, TTLs y credenciales van a configuración (`alert_rules` o env).
- **Documentación**: mantén actualizados README, contratos de API (OpenAPI/Swagger) y ADRs. Documenta supuestos en este archivo.
- **DRY y SRP**: sin duplicación de lógica de negocio; una responsabilidad por módulo/servicio.
- **Feature flags**: para funcionalidad experimental, desactivada por defecto.
- **Alcance**: implementa únicamente el elemento del backlog (`BL-xxx`, ver `docs/backlog.md`) que se te haya solicitado. No amplíes el alcance a elementos vecinos sin instrucción explícita, aunque queden cerca del código que estás tocando; si uno resulta ser prerrequisito, dilo antes de implementarlo.

---

## 8. Testing Strategy

- **Unitarias (Jest)**: lógica de comparación de inventarios (RF-03), evaluación de reglas de alertas (RF-06), validación de permisos por perfil, hashing/validación de contraseñas.
- **Integración**: endpoints contra una MongoDB de prueba (contenedor efímero/`mongodb-memory-server`), incluyendo autenticación y RBAC.
- **Contrato**: validación de esquemas JSON de ingesta (`/inventory`, `/access-events`) y respuestas de consulta.
- **E2E (Cypress)**: flujos del portal por perfil:
  - Login y redirección de no autenticado (CA-08).
  - Usuario consulta y exporta reportes (CA-13).
  - Auditor consulta eventos y actualiza estado de alerta, sin acceso a usuarios/reglas (CA-14).
  - Administrador crea usuario y ese usuario inicia sesión (CA-10); define/modifica alerta (CA-11).
  - Usuario recibe 403 en gestión de usuarios/config de alertas (CA-09).
- **Seguridad**: validación de autenticación/autorización, 401 sin token (CA-06), 403 por rol, rate limiting, resistencia a inyección NoSQL.
- **Carga**: simulación de cientos de nodos enviando inventarios y eventos; verificar ingesta < 500 ms por inventario.
- **Cobertura objetivo**: **≥ 80%** en lógica de negocio crítica. Los PR que reduzcan cobertura por debajo del umbral se rechazan.
- **Datos de prueba**: nunca uses datos reales sensibles; usa fixtures anonimizados.

---

## 9. DevOps Guidelines

- **Contenedores Docker**: uno para API (Node.js), uno para frontend (Angular servido por Nginx). Imágenes reproducibles, multi-stage, sin secretos embebidos.
- **MongoDB**: gestionado (Atlas) o autoalojado **con réplica**. Nunca instancia única en producción.
- **Reverse Proxy / API Gateway** con TLS delante de la API.
- **Entornos**: desarrollo, staging y producción, con configuración aislada por env vars.
- **CI/CD** (GitHub Actions o similar): pipeline `lint → build → test → security scan → deploy`. El deploy a producción requiere que todos los pasos pasen.
- **Variables de entorno**: credenciales, secreto JWT, API keys de nodos, cadena de conexión Mongo, zona horaria, umbrales por defecto.
- **Semilla inicial**: crea un usuario **Administrador** por defecto en el primer despliegue con **cambio de contraseña obligatorio** en el primer login. La contraseña semilla nunca queda fija en código.
- **Backups**: respaldos periódicos y verificados de MongoDB; documenta el procedimiento de restauración.
- **Rollback**: cada despliegue debe ser reversible (imágenes versionadas por tag inmutable).

---

## 10. Observability Standards

- **Logging estructurado en JSON** con niveles (`info`, `warn`, `error`). Incluye `requestId`, `actorId` (si aplica) y contexto. **Nunca** loguees contraseñas, tokens ni `passwordHash`.
- **Request-id**: genera/propaga un identificador de correlación en cada petición y respuesta.
- **Auditoría de acceso**: registra inicios de sesión (éxito/fallo), intentos fallidos y acciones administrativas en `audit_log`.
- **Métricas**: inventarios recibidos, latencia de API, alertas generadas por tipo, usuarios activos/sesiones, tasa de errores 4xx/5xx.
- **Health checks**: `GET /health` que verifique el proceso y la conexión a MongoDB.
- **Monitoreo**: integración con Prometheus/Grafana o equivalente.
- **Alertas operativas**: notificación ante caída de la API o de la base de datos.

---

## 11. Quality Gates

Un cambio NO se integra ni despliega si falla alguno de estos gates:

1. **Lint** sin errores (backend y frontend).
2. **Build** exitoso (API y Angular).
3. **Tests** unitarios, de integración y de contrato en verde.
4. **Cobertura** ≥ 80% en lógica de negocio crítica.
5. **RBAC verificado**: existen pruebas que demuestran 401/403 correctos por endpoint sensible.
6. **Security scan**: `npm audit` sin vulnerabilidades altas/críticas; escaneo de secretos limpio.
7. **Sin secretos** hardcodeados ni datos sensibles en logs.
8. **Contratos de API** actualizados (OpenAPI/Swagger) si cambian endpoints.
9. **ADR** registrado para toda decisión arquitectónica o cambio de contrato/modelo.
10. **Criterios de Aceptación** afectados (CA-xx) verificados.

---

## 12. AI Agent Behaviour

- **No inventes** endpoints, campos o roles fuera del spec. Usa exactamente los contratos y colecciones definidos.
- **Respeta el alcance del elemento solicitado**: implementa el `BL-xxx` pedido y nada más; marca claramente cualquier trabajo que exceda ese alcance y no lo actives por defecto.
- **Seguridad primero**: ante conflicto entre velocidad y seguridad/RBAC, elige seguridad.
- **Documenta supuestos**: cualquier decisión ante información faltante se registra en **Assumptions** y, si es arquitectónica, en un ADR.
- **Cambios mínimos y localizados**: no refactorices fuera del alcance de la tarea sin justificación.
- **Idempotencia y determinismo**: genera código reproducible; evita dependencias implícitas del entorno.
- **Trazabilidad**: vincula cada entrega a RF/UC/CA.
- **No degradar**: no reduzcas cobertura, no elimines validaciones, no bajes niveles de seguridad para “que funcione”.
- **Detente y marca** cuando una instrucción viole reglas de seguridad, RBAC, alcance o el modelo de datos; propón alternativa conforme.
- **Verifica antes de entregar**: ejecuta mentalmente los Quality Gates y los CA relevantes.

---

## 13. Decision Framework

Ante ambigüedad o información faltante, decide en este orden:

1. **¿Compromete la seguridad, la privacidad o el RBAC?** → Elige la opción más segura y restrictiva (menor privilegio, denegar por defecto).
2. **¿Contradice `spec.md`?** → Prevalece `spec.md`. No implementes lo contradictorio; documenta el conflicto.
3. **¿Está fuera del alcance de Fase 1?** → No lo implementes; regístralo para roadmap.
4. **¿Afecta contrato de API o modelo de datos?** → Mantén compatibilidad; si es inevitable romper, versiona y crea ADR.
5. **¿Existe estándar del proyecto o práctica moderna consolidada?** → Aplícala (TypeScript strict, JWT, bcrypt/argon2, esquemas de validación, Docker).
6. **¿Sigue habiendo ambigüedad?** → Toma la decisión razonable más simple, mantenible y reversible; documéntala en **Assumptions**.

Regla transversal: **denegar por defecto**, **validar siempre**, **preferir lo reversible**, **no ampliar el alcance**.

---

## 14. Project Constraints

- Stack **fijo**: Angular + Node.js + MongoDB. No sustituyas tecnologías del núcleo.
- **API pública en internet**: TLS y protección obligatorias.
- **Agentes de recolección fuera del alcance de desarrollo**: no construyas agentes en Fase 1.
- **Tres perfiles fijos**: Administrador, Usuario, Auditor. No crees roles personalizados en Fase 1.
- **Sin autoservicio de registro**, **sin MFA**, **sin SSO/OIDC/LDAP** en Fase 1.
- **Fuera del alcance**: remediación automática, gestión de parches, ITSM/SIEM, APM en tiempo real, gestión de licencias, facturación, DR de equipos monitoreados.
- **NFR objetivo**: uptime API ingesta 99.5%; ingesta de inventario < 500 ms; escalado horizontal para cientos de nodos.
- **Compatibilidad**: navegadores modernos (Chrome, Edge, Firefox); accesibilidad WCAG AA.
- **Persistencia**: exclusivamente MongoDB con las colecciones definidas.
- **Despliegue**: contenedores Docker; MongoDB con réplica en producción.

---

## 15. Assumptions

Supuestos adoptados ante información no especificada (revísense y ajústense si el spec evoluciona):

1. **Node.js LTS ≥ 20** y **TypeScript strict** para backend y frontend, por mantenibilidad y tipado.
2. **NestJS** como framework backend preferido por su soporte nativo de DI y guards RBAC; alternativa aceptable Express con arquitectura modular equivalente (confirmar vía ADR).
3. **Mongoose** como ODM para esquemas y consultas seguras.
4. **JWT de acceso corto (~15 min)** + **refresh token (~7 días)**; `logout` invalida vía rotación/lista de revocación. Valores exactos configurables por env.
5. **Argon2** como algoritmo preferido de hashing de contraseñas (bcrypt aceptable).
6. **Zona horaria de “horario habitual”** configurable por instalación; por defecto UTC hasta configuración explícita. Timestamps almacenados en UTC.
7. ~~`resource_change` se dispara cuando cambian los recursos habilitados en la regla (`cpu`, `ram`, `disks`); horario habitual por defecto `07:00–19:00` (configurable en `alert_rules`)~~ — **Resuelto (sub-fase 1.3):** estos ya no son solo valores asumidos: `AlertRulesService.onModuleInit()` siembra ambas reglas por defecto en el primer arranque si no existen (`resource_change` habilitada con `resources: [cpu, ram, disks]`; `off_hours_access` habilitada con `habitualHours: {from: "07:00", to: "19:00"}`), exactamente con estos valores. El motor (`AlertEngineService`) siempre lee la configuración de `alert_rules` en Mongo, nunca hardcodea los umbrales — la siembra es solo el punto de partida, el Administrador puede editarla vía `PATCH /alert-rules/:id`.
8. **`deviceId` = `_id` del host (host-uuid)**; los inventarios se vinculan por `deviceId` y se comparan contra el inventario previo más reciente.
9. **Paginación por defecto** de 20–50 ítems en listados de `devices` y `alerts`.
10. **OpenAPI/Swagger** como documentación de contrato de la API.
11. ~~RF-19 (exportación PDF/CSV) y RF-20 (notificaciones) se consideran recomendados~~ — **Resuelto (spec.md v1.3):** existía una contradicción entre CA-13 (exigía exportación en la entrega actual) y el Roadmap original (que ubicaba la exportación en Fase 2). Se resolvió incluyendo explícitamente la exportación de reportes (RF-19) en el alcance de Fase 1 en `spec.md`. Ya no es un supuesto, sino un requisito confirmado y entregado. RF-20 (notificaciones) sigue sin implementarse y hoy vive en el backlog como BL-005 a BL-008.
12. **Rate limiting** por IP y por credencial en login; umbrales configurables por env.
13. **Retención** de `inventories`, `access_events` y `audit_log` mediante política configurable (TTL/purga), con valor por defecto conservador documentado en configuración.
14. ~~API Keys de nodos almacenadas hasheadas y rotables; identificación del nodo por su clave~~ — **Resuelto (sub-fase 1.2):** el contrato autoritativo no define un endpoint REST para aprovisionar/rotar claves de nodo, así que esa operación se implementó como script de CLI (`npm run device:provision` / `device:rotate-key` en `backend/`), no como ruta HTTP — mismo enfoque de bootstrap fuera de banda que la semilla de Administrador (§9). Formato de clave `<deviceId>.<secret>`: el `deviceId` permite lookup O(1) en `devices`, el `secret` se valida con el hash argon2 en el campo adicional `devices.apiKeyHash` (no está en el ejemplo de `spec.md`, mismo patrón aditivo que `users.passwordHash`). `NodeApiKeyGuard` es una guard completamente separada de `JwtAuthGuard`/`RolesGuard` — nunca se mezclan en el mismo endpoint (regla de auth dual, §5.4).
15. **Idempotencia de ingesta** (`POST /inventory`, `POST /access-events`) implementada vía índice único de MongoDB sobre la clave natural del documento (`deviceId+timestamp` en `inventories`; `deviceId+level+user+timestamp+action` en `access_events`) en vez de un almacén de tokens de idempotencia separado. Un reenvío idéntico de un nodo (mismo `timestamp`) produce un error de clave duplicada que el repositorio interpreta como reintento exitoso (`201`, sin duplicar), no como error. Esto exige que el nodo reporte su propio `timestamp` de colección/evento — el servidor nunca lo sobrescribe con la hora de recepción.
16. **Un único `alert_rule` habilitado por `type`** (índice único sobre `type` en `alert_rules`): el motor lee "la regla de `resource_change`"/"la regla de `off_hours_access`" como conceptos singulares en todo `spec.md`/este documento, así que un segundo `POST /alert-rules` para un `type` ya existente responde `409` (usar `PATCH` para modificarla), en vez de permitir múltiples reglas ambiguas del mismo tipo.
17. **Zona horaria de horario habitual** (Assumption #6) implementada como variable de entorno `HABITUAL_HOURS_TZ` (IANA timezone, por defecto `UTC`), consumida por `AlertEngineService` vía `Intl.DateTimeFormat` (sin dependencia nueva). La comparación contra `habitualHours.from`/`to` maneja tanto rango del mismo día (`from <= to`) como rango que cruza medianoche (`from > to`, ej. turno nocturno `22:00–06:00`).
18. **`off_hours_access` solo aplica a dispositivos `category: infrastructure`** y solo a la acción `login` (nunca `logout`), conforme a CA-03 ("acceso a **servidor**" fuera de horario). Se evalúa igual para `level: os` y `level: database`, ya que `spec.md` no distingue por nivel al describir esta regla.
19. **Gestión de usuarios (sub-fase 1.4)**: `POST /users` recibe la contraseña inicial directamente del Administrador (misma política de complejidad que el cambio de contraseña propio); el usuario creado siempre queda con `mustChangePassword: true` sin importar el rol, igual que la semilla de Administrador. `PATCH /users/:id` permite `email`/`role`/`active`/`password` parciales: un reset de contraseña por el admin fuerza `mustChangePassword: true` e incrementa `tokenVersion` (mismo mecanismo de revocación que el cambio de contraseña propio); desactivar (`active:false`) también incrementa `tokenVersion` — el único acceso residual posible es el access token corto ya vigente, consistente con que esta API es intencionalmente stateless (`JwtAuthGuard` no consulta la base en cada petición). **Guardia anti-bloqueo propio**: un Administrador no puede desactivarse ni degradarse a sí mismo vía este endpoint (403) — riesgo operativo real, no validación especulativa; sí puede editar su propio email o resetear su propia contraseña. Las respuestas de `/users` siempre se mapean a través de `toUserResponse()` para garantizar que `passwordHash`/`tokenVersion` nunca salgan del proceso.
20. **Portal de consulta (sub-fase 1.5)**: `GET /devices`, `GET /stats/devices`, `GET /stats/os` son, a diferencia de `/users`/`/alert-rules` (solo Administrador) o `/alerts` (Administrador+Auditor), abiertos a **los tres roles autenticados** (Administrador, Usuario, Auditor) — declarado explícitamente vía `@Roles(ADMINISTRATOR, USER, AUDITOR)` en vez de omitir `RolesGuard`, para cumplir la regla de que ningún endpoint quede sin declaración de rol. `GET /devices` filtra por `category` (enum exacto) y `osName`/`hostname` (coincidencia parcial case-insensitive) — el input se escapa con `escapeRegex()` antes de construir el `$regex` de Mongo, para evitar inyección/ReDoS vía metacaracteres. `GET /stats/os` agrupa por `os.name`; los dispositivos que nunca enviaron un inventario (sin `os` aún) se agrupan bajo la etiqueta explícita `"unknown"` en vez de excluirse, para que la suma de conteos siempre iguale el total de dispositivos (sin truncado silencioso). La UI de alertas (tabla filtrable + cambio de estado) se incorporó a esta sub-fase ampliada — ver nota en el Roadmap (§17) — reutilizando el backend de `alerts`/`alert-rules` ya existente desde la 1.3, sin nuevos endpoints. El gráfico de distribución por SO se construyó sin dependencia de librería de charts (SVG/HTML+CSS a medida), siguiendo la paleta y especificación de marcas validada por el skill de dataviz, para minimizar dependencias y peso del bundle.
21. **Reportes y exportación (sub-fase 1.6)**: `GET /reports/export` recibe `reportType` (`devices|alerts`) y `format` (`csv|pdf`) — nombrado `reportType` en vez de `type` para no colisionar con el propio campo `type` de una alerta cuando `reportType=alerts` (filtro opcional `alertType`). Es el único endpoint con **RBAC mixto en una sola ruta**: el guard grueso permite los tres roles (Usuario puede exportar `devices` per RF-14/CA-13), pero `ReportsService.generate()` rechaza con `403` explícito a un Usuario que pida `reportType=alerts` (mismo alcance que `AlertsController`) — un guard declarativo no puede expresar "según el valor de un query param", así que la regla vive en el servicio, igual que el guard anti-bloqueo propio de `UsersService` (ítem 19). Columnas del reporte de equipos: `hostname, category, os.name, os.version, lastSeen`; de alertas: `type, deviceId, detail (aplanado a texto), createdAt, status`. Sin paginación (se exporta el conjunto filtrado completo; a escala de "cientos de nodos" esto es aceptable para una descarga puntual autenticada). CSV se construye a mano con escape correcto (comilla si el valor contiene coma/comilla/salto de línea, comilla interna duplicada) — nunca un `.join(',')` ingenuo. PDF usa **pdfkit** (nueva dependencia de producción, justificada: no hay forma de generar bytes PDF reales sin una librería, y es la opción liviana estándar en Node sin depender de un navegador headless); `npm audit --omit=dev` confirma 0 vulnerabilidades nuevas. La respuesta es el único endpoint de toda la API que no usa el sobre JSON estándar — es una descarga de archivo cruda (`Content-Type`/`Content-Disposition`, vía `@Res()` sin passthrough); `AllExceptionsFilter` sigue aplicando normalmente para los casos de error (401/403/400), que sí devuelven el sobre JSON de siempre.

Con la sub-fase 1.6 cerrado el conjunto de funcionalidad de portal/consulta de Fase 1; solo queda pendiente la 1.7 (endurecimiento y cierre de Quality Gates).

22. **Extensión EXT-1 — Auditoría de firewall AWS (ver ADR-0013/0014/0015, spec.md §21)**: extensión fuera del roadmap original de Fases 1-4 (ni `spec.md` ni este documento mencionaban AWS/nube/firewalls antes de esta extensión), confirmada explícitamente por el usuario y documentada como tal en vez de absorberse silenciosamente en Fase 1. `security_group_rules` (catálogo) y `aws_sync_runs` (log de corridas) son colecciones nuevas, no una extensión de `devices`/`inventories` — un Security Group de AWS no es un "dispositivo" en el sentido que `devices` asume (agente de recolección reportando un host físico/virtual). "Dispositivo asociado" se resuelve como el/los recurso(s) de AWS adjuntos al grupo (`attachedResources`, vía `DescribeNetworkInterfaces`+`DescribeInstances`), no como un cruce contra `devices`. Origen/destino se derivan honestamente según la direccionalidad AWS (ingress solo define origen, egress solo define destino; el lado local reutiliza `securityGroupId`, nunca un valor inventado). `createdAt` cae de vuelta a "primera vez observada por IT-MAS" porque la API de EC2 no expone timestamp de creación de reglas. RBAC: lectura del catálogo (`GET /`, `/groups`, `/export`) abierta a los tres roles; `PATCH /:id/review` solo Auditor; `PATCH /:id/authorize` solo Administrador — dos endpoints de rol fijo, no uno genérico, para que `@Roles()` sea el mecanismo real de separación de funciones (reforzado con una validación defensiva de mismo actor en el servicio, aunque hoy sea inalcanzable por disyunción de roles). El módulo de sincronización (`security-group-sync`: disparo manual, historial, resumen) es Administrador+Auditor únicamente. Una sola cuenta AWS, regiones auto-descubiertas vía `DescribeRegions` (nunca una lista fija). Ejecución síncrona en el mismo proceso, sin cola/worker. Sin infraestructura de notificación nueva — el indicador de "pendientes desde la última corrida" se deriva por consulta (`status=pendiente AND createdAt >= última corrida exitosa`), no por un concepto de notificación persistido.

**EXT-1 (EXT-1.0–1.4): funcionalmente completa** — catálogo, motor de sincronización, cron, y portal, todos con RBAC/auditoría/pruebas verificadas (backend: 114 unit + 65 e2e en verde; frontend: build/lint en verde, flujo revisar→autorizar verificado en navegador real). **Hallazgo operacional durante la verificación manual**: el entorno de pruebas usado tenía credenciales AWS reales ambientales (no configuradas por IT-MAS) que el botón "Sincronizar ahora" alcanzó una vez antes de detectarse; ver la nota de seguridad en `DEPLOYMENT.md` y la nota en `docs/ca-traceability.md` (CA-15/CA-20). No es un defecto del código — es un recordatorio de que este módulo, por diseño, puede leer todo el inventario de firewall de cualquier cuenta cuyas credenciales el proceso del backend alcance; el despliegue real debe usar un rol dedicado con exactamente la política mínima documentada, nunca credenciales ambientales de una máquina de desarrollo.

---

## 16. Acceptance Criteria

La entrega se considera conforme cuando se cumplen todos los criterios del spec, verificables mediante pruebas automatizadas:

- **CA-01**: Inventario válido enviado a la API se persiste en MongoDB y retorna **201**.
- **CA-02**: Segundo inventario con distinta RAM genera alerta `resource_change`.
- **CA-03**: Acceso a servidor fuera del rango configurado genera alerta `off_hours_access`.
- **CA-04**: El dashboard muestra el total de equipos separados por colaborador e infraestructura.
- **CA-05**: El dashboard muestra la distribución por sistema operativo actualizada.
- **CA-06**: Peticiones a la API sin token válido se rechazan con **401**.
- **CA-07**: Las alertas se filtran por tipo y rango de fechas.
- **CA-08**: Usuario no autenticado es redirigido al login.
- **CA-09**: Perfil **Usuario** recibe **403** en gestión de usuarios/config de alertas y no ve dichas opciones en el menú.
- **CA-10**: El **Administrador** crea un usuario con perfil y ese usuario puede iniciar sesión.
- **CA-11**: El **Administrador** define/modifica una alerta y el motor aplica la configuración actualizada.
- **CA-12**: Cada acción administrativa (crear usuario, modificar alerta, login) queda en `audit_log` con usuario y marca de tiempo.
- **CA-13**: Perfil **Usuario** consulta y exporta reportes correctamente.
- **CA-14**: Perfil **Auditor** consulta eventos de auditoría y actualiza el estado de una alerta, sin acceder a gestión de usuarios ni configuración de reglas.

Complementariamente, deben cumplirse los **NFR** (uptime 99.5% API ingesta, ingesta < 500 ms, escalado horizontal, RBAC en backend y frontend, WCAG AA) y todos los **Quality Gates** de la sección 11.

---

## 17. Sub-fases de Fase 1 (registro histórico)

> **Este apartado es registro histórico, no plan de trabajo.** La Fase 1 se entregó completa (1.0–1.7) y el proyecto ya no se planifica por fases: el trabajo pendiente son requisitos individuales `BL-xxx` en `docs/backlog.md`. Se conserva porque `docs/ca-traceability.md` mapea cada CA-01..14 a la sub-fase que lo implementó, y porque las notas de implementación de cada sub-fase siguen siendo la mejor referencia sobre por qué el código está como está.

La Fase 1 (MVP) se ejecutó de forma incremental en las siguientes sub-fases. El orden respetaba dependencias técnicas reales (qué bloquea a qué); no eran fases de negocio, sino únicamente secuenciación de implementación.

### Fase 1.0 — Cimientos
Sin valor funcional visible; desbloquea todo lo demás.
- Scaffolding backend (NestJS/TS strict) y frontend (Angular) según §5.
- Conexión a MongoDB, configuración por variables de entorno, logging estructurado JSON con `request-id`.
- `GET /health` (proceso + conexión Mongo).
- Dockerfiles multi-stage (API y frontend) y esqueleto de CI (`lint → build → test`).

### Fase 1.1 — Autenticación y RBAC base
Prerrequisito de RBAC en todos los endpoints posteriores (§4, §6.5).
- Colección `users`, seed de Administrador con cambio de contraseña obligatorio (§9).
- `POST /auth/login|logout|refresh|change-password`, emisión/validación JWT, guards de rol en backend.
- Interceptor Angular (JWT, manejo 401/403) y pantalla de login.
- Registro en `audit_log` de logins (éxito/fallo).
- **Cubre:** CA-06, CA-08.

### Fase 1.2 — Ingesta de inventarios y eventos
- `POST /inventory`, `POST /access-events` con autenticación por API Key de nodo.
- Colecciones `devices`, `inventories` (append-only), `access_events`.
- Lógica de comparación de inventarios consecutivos (RF-03).
- **Cubre:** CA-01, CA-02 (parcial; la alerta se genera en 1.3).

### Fase 1.3 — Motor de alertas
- Colección `alert_rules`, evaluación de reglas `resource_change` y `off_hours_access`.
- Colección `alerts`, `GET/POST/PATCH /alert-rules` (Administrador).
- `GET /alerts` (filtros, paginación), `PATCH /alerts/:id` (Administrador/Auditor).
- **Cubre:** CA-02 (completo), CA-03, CA-07, CA-11, CA-14 (parte de gestión de alertas).

### Fase 1.4 — Gestión de usuarios (Administrador)
- `GET/POST/PATCH /users` con validación y RBAC estricto.
- UI de administración en el portal (crear/editar/activar-desactivar usuario, asignar rol).
- `audit_log` de creación/edición de usuarios y cambios de reglas.
- **Cubre:** CA-09, CA-10, CA-12.

### Fase 1.5 — Portal de consulta: dashboard, equipos y alertas
> Ampliada respecto al plan original: la UI del módulo de alertas (tabla filtrable, gestión de estado) no tenía sub-fase asignada pese a que el backend de alertas ya existe desde la 1.3; se incorpora aquí porque dashboard/equipos/alertas son, los tres, pantallas de consulta del portal para los mismos roles.
- `GET /stats/os`, `GET /stats/devices`, `GET /devices` (filtros/paginación) — accesibles a Administrador, Usuario y Auditor (consulta, no administración).
- Dashboard Angular: tarjetas resumen, gráfico por SO.
- Lista de equipos (`/devices`) con filtros.
- Tabla de alertas filtrable (tipo, estado, rango de fechas) usando `GET /alerts` (fase 1.3), con cambio de estado (`PATCH /alerts/:id`) reservado a Administrador/Auditor en la UI (el backend ya lo exige).
- Menú/guards por rol (`AdministradorGuard`, `AuditorGuard`, `UsuarioGuard`).
- **Cubre:** CA-04, CA-05, CA-07, CA-09 (ocultamiento de menú), CA-14 (vista Auditor).

### Fase 1.6 — Reportes y exportación
Corresponde a RF-19, confirmado como parte de Fase 1 (ver Assumption #11).
- `GET /reports/export` (PDF/CSV).
- Vista de reportes para Usuario/Auditor/Administrador en el portal.
- **Cubre:** CA-13.

### Fase 1.7 — Endurecimiento y cierre de Quality Gates
Transversal a todas las anteriores; se cierra al final como gate de salida de Fase 1.
- Rate limiting y anti fuerza bruta en `/auth/login` y API pública.
- `npm audit` + escaneo de secretos, cifrado en reposo, políticas de retención (TTL) para `inventories`/`access_events`/`audit_log`.
- Pruebas de seguridad, de carga (ingesta < 500 ms), cobertura ≥ 80%, documentación OpenAPI/Swagger completa, ADRs consolidados.
- **Verifica:** todos los CA-xx restantes y los NFR (uptime, latencia, WCAG AA).

**Dependencias clave**: 1.1 debe completarse antes de exponer cualquier endpoint protegido de 1.2–1.6, porque RBAC en backend es la fuente de verdad. 1.2 y 1.3 pueden avanzar en paralelo si el equipo lo permite, pero la evaluación de reglas en 1.3 depende de que existan inventarios/eventos reales de 1.2. 1.7 corre de forma transversal pero no se declara "cerrada" hasta el final.

#### Fase 1.7 — completada. Notas de implementación (backend)

- **Rate limiting global + espec ífico de login, en un solo registro**: `@nestjs/throttler`'s `ThrottlerModule` es `@Global()` internamente — un segundo `forRootAsync()` en un módulo distinto (como existía antes solo en `AuthModule`) choca sobre los mismos tokens de DI y uno de los dos se pisa silenciosamente. Se consolidó en **un único registro** en `AppModule` con dos perfiles nombrados: `default` (límite generoso API-wide, env `API_RATE_LIMIT_MAX`/`API_RATE_LIMIT_WINDOW_SEC`, default 100/60s) y `login` (el límite estricto anti fuerza bruta ya existente, env `LOGIN_RATE_LIMIT_MAX`/`WINDOW_SEC`). `ThrottlerGuard` se aplica globalmente vía `APP_GUARD`, que por diseño de la librería evalúa **todos** los perfiles nombrados sobre cada ruta salvo que se excluyan explícitamente — por eso cada controlador (salvo `POST /auth/login`) lleva `@SkipThrottle({ login: true })`.
- **Retención (TTL)** configurable vía env (`INVENTORY_RETENTION_DAYS`, `ACCESS_EVENTS_RETENTION_DAYS`, `AUDIT_LOG_RETENTION_DAYS`; defaults 180/180/365 días) — implementada con índices TTL creados **programáticamente** en `onModuleInit()` de cada repositorio (mismo patrón que el seed de Administrador), no como `@Prop({expires})` estático, porque el valor debe leerse de `ConfigService` en runtime. `ensureTtlIndex()` (`src/common/mongo/`) detecta si el índice ya existe con un `expireAfterSeconds` distinto y lo recrea (Mongo no permite cambiarlo in-place), y trata el error "ns does not exist" (colección nueva) como "sin índice previo" en vez de fallar el arranque.
- **Body-size cap** explícito de 1MB (`main.ts`, `bodyParser: false` + `json({limit})` manual) — antes solo existía el límite implícito de Express (100kb) sin ser una decisión documentada.
- **`AllExceptionsFilter` ahora también maneja errores "crudos"** (no-`HttpException`) con `.status`/`.statusCode` propio — como el `PayloadTooLargeError` de `body-parser` — mapeándolos al código HTTP real (413) en vez de caer siempre en un 500 genérico.
- **OpenAPI/Swagger**: se optó por el **plugin de CLI de `@nestjs/swagger`** (`nest-cli.json` → `compilerOptions.plugins`) en vez de anotar manualmente `@ApiProperty()` en cada DTO — el plugin infiere tipos, enums, campos requeridos y anidados directamente de los decoradores de `class-validator` ya existentes, con resultado equivalente y muchísimo menos código. Se agregó `@ApiTags`/`@ApiBearerAuth`/`@ApiSecurity` a cada controlador. Sirve en `GET /api/docs` (Swagger UI) y `GET /api/docs-json`; `backend/openapi.json` se regenera en cada arranque (best-effort, no rompe el boot si el filesystem es de solo lectura).
- **Pruebas de seguridad** (`test/security.e2e-spec.ts`): rechazo de payloads con forma de inyección NoSQL (objetos donde se espera string), rechazo de payload > 1MB (413), y 429 del límite global en un endpoint no-login.
- **Benchmark de carga** (no gate duro, `scripts/load-smoke.ts`, `npm run load:smoke`): 50 `POST /inventory` concurrentes contra una instancia real midieron p95 ≈ 1096ms en este entorno sandboxed (objetivo NFR: p95 < 500ms). Se documenta la medición real en vez de omitirla, con la salvedad explícita de que es una sola instancia sin tuning de pool de conexiones en un entorno compartido — no una medición de producción con escalado horizontal.
- **Cobertura**: se reforzó `DevicesService` (33%→88%, incluyendo `verifyApiKey`'s rechazo timing-safe) y se agregó el caso faltante de `UsersService.findAllForAdmin`. La cobertura agregada del repo (~35%) se mantiene deliberadamente por debajo de 80% en controladores/DTOs/wiring de módulos — cubiertos por e2e, no por unit tests, mismo criterio aplicado desde la 1.1.
- **`npm audit`**: 0 vulnerabilidades (`@nestjs/swagger` se fijó en `11.4.5`, no la última `11.4.6`, porque esta última arrastra una versión vulnerable de `js-yaml`).

**Docker/CI** (agente paralelo): `backend/Dockerfile` y `frontend/Dockerfile` (multi-stage), `docker-compose.yml` en la raíz, `.github/workflows/ci.yml` (lint→build→test→audit), y `DEPLOYMENT.md` en la raíz con instrucciones de despliegue.

**Accesibilidad WCAG AA** (agente paralelo, frontend): se cerró la deuda señalada en la 1.5 — el gráfico de distribución de SO (construido a mano, sin librería) ahora tiene alternativa accesible (`aria-label`/tabla oculta visualmente), más `aria-label` en acciones de solo-ícono (tabla de usuarios, tabla de alertas, reportes) y reglas de accesibilidad de `angular-eslint` habilitadas como gate de regresión.

**Cifrado en reposo**: es una decisión de infraestructura (habilitar cifrado a nivel de clúster/almacenamiento de MongoDB), no de código de aplicación — documentado como requisito operativo en `DEPLOYMENT.md`, no implementado aquí.

**Fase 1 (1.0–1.7): funcionalmente completa.** Los 14 CA (CA-01 a CA-14) están cubiertos por pruebas automatizadas a través de las sub-fases; ver `docs/ca-traceability.md` para el mapeo detallado. Los ADRs de las decisiones arquitectónicas mayores viven en `docs/adr/`.

---

> Este documento es vivo: actualízalo con cada cambio de alcance de fase, decisión arquitectónica (ADR) o nuevo supuesto. Toda IA o desarrollador debe leerlo antes de contribuir.