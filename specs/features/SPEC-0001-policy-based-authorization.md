---
id: SPEC-0001
title: Sistema de autorización basado en políticas
type: feature
status: Implemented
owner: jmartinez
created: 2026-10-07
approved-by: jmartinez (2026-10-07)
agents: [architecture, security, database, backend, frontend, testing]
related: [SPEC-0000, ADR-0002, ADR-0003, ADR-0004]
---

# SPEC-0001 — Sistema de autorización basado en políticas

> Derivada de la definición funcional aportada por el solicitante y de las decisiones del 2026-10-07
> (alta automática de usuarios, Root por *bootstrap* + marca en BD, catálogo de permisos definido en código,
> administración delegable con salvaguardas). Aprobada el 2026-10-07 aceptando todas las propuestas de §18.

## 1. Objetivo

Implementar un control de acceso centralizado, granular y flexible, inspirado conceptualmente en AWS IAM, donde los
privilegios se otorgan exclusivamente mediante **políticas** reutilizables que agrupan **permisos**:

```text
Usuario → Políticas → Permisos → Recurso/Acción
```

Todo usuario empieza sin privilegios. Un nivel especial **Root / Super Administrador** tiene acceso irrestricto.

## 2. Contexto

La fundación (SPEC-0000) dejó preparado el punto de extensión: los endpoints declaran `[RequirePermission("...")]`
y la decisión la toma el puerto `IPermissionEvaluator`, hoy implementado por `DenyAllPermissionEvaluator`
(nadie tiene permisos). Esta spec sustituye ese evaluador por el modelo de políticas, añade la gestión de usuarios
de la aplicación y la interfaz de administración.

La identidad la aporta Microsoft Entra ID (autenticación). Usuarios, políticas, permisos y estado activo se guardan
en la base de datos de la aplicación (autorización), de modo que cambiar de proveedor de identidad no altere el modelo.

## 3. Alcance

**Incluye**
- Registro automático del usuario en su primer inicio de sesión, sin políticas.
- Catálogo de permisos definido en código y sincronizado con la base de datos.
- CRUD de políticas, asignación y retiro de políticas a usuarios, activación/desactivación de usuarios y políticas.
- Root / Super Administrador: *bootstrap* por configuración, promoción/revocación desde la UI con protección anti-bloqueo.
- Evaluación de permisos efectivos (unión de políticas activas) y trazabilidad permiso → política.
- Administración delegable mediante permisos, con salvaguardas contra escalada de privilegios.
- Auditoría inmutable de todas las operaciones de administración de autorización.
- Interfaz de administración (Usuarios, Políticas, Permisos, Auditoría) y uso de permisos en el frontend para UX.

**Excluye (versión 1)**
- Roles tradicionales.
- Comodines (`customers.*`), permisos sobre recursos concretos, condiciones, *scopes*, reglas de denegación
  explícita, ABAC, restricciones por horario o IP, multi-organización. La arquitectura no debe impedirlos (§10.4).
- Alta manual de usuarios por el Root (ver §18, P1).
- Sincronización de grupos o *app roles* de Entra ID.

## 4. Actores

| Actor | Descripción |
|---|---|
| Usuario | Persona autenticada con Entra ID. Sin políticas no accede a ninguna funcionalidad protegida. |
| Administrador delegado | Usuario no Root con permisos de administración de autorización otorgados por políticas. Sujeto a salvaguardas (RN-9 a RN-12). |
| Root / Super Administrador | Usuario marcado como Root. Acceso irrestricto; único que gestiona el estado Root. |
| Sistema | Proceso que sincroniza el catálogo de permisos y aplica el *bootstrap* del Root. |

## 5. Requerimientos

### 5.1 Funcionales

**Usuarios**
- **RF-1** En el primer inicio de sesión se crea el usuario de la aplicación, vinculado a su identidad externa
  (proveedor + tenant + `oid`), activo y sin políticas. En inicios posteriores se actualizan nombre y correo.
- **RF-2** Un usuario desactivado no puede iniciar sesión y sus sesiones abiertas dejan de ser válidas en su siguiente solicitud.
- **RF-3** Consultar usuarios (lista paginada con búsqueda por nombre o correo, filtro por estado) y su detalle.
- **RF-4** Activar y desactivar usuarios.
- **RF-5** Consultar las políticas asignadas a un usuario y sus **permisos efectivos**, indicando para cada permiso
  la(s) política(s) que lo conceden.

**Permisos**
- **RF-6** Los permisos se declaran en código (cada spec declara los suyos) con: nombre, nombre visible, descripción,
  categoría y si es *otorgable*. Al desplegar se sincronizan con la base de datos.
- **RF-7** Un permiso que desaparece del código se marca como **obsoleto** (no se borra): deja de poder añadirse a
  políticas y deja de conceder acceso, pero se conserva para historial y auditoría.
- **RF-8** Consultar el catálogo de permisos (agrupado por categoría) y las políticas que contienen cada permiso.
- **RF-9** Editar la descripción y la categoría visibles de un permiso. El nombre no es editable.

**Políticas**
- **RF-10** Crear, consultar, modificar y eliminar políticas (nombre único, descripción, estado activo, permisos).
- **RF-11** Añadir y retirar permisos de una política. Solo se admiten permisos existentes, no obsoletos y otorgables.
- **RF-12** Activar y desactivar políticas. Una política inactiva no concede permisos y se conserva con sus asignaciones.
- **RF-13** Consultar los usuarios asociados a una política.
- **RF-14** Una política con usuarios asignados no se puede eliminar: hay que retirarla antes de
  todos los usuarios o desactivarla.

**Asignaciones**
- **RF-15** Asignar una o varias políticas a un usuario y retirarlas.
- **RF-16** Los cambios en una política o en las asignaciones se aplican a todos los usuarios afectados en su
  **siguiente solicitud**, sin cerrar sesión.

**Root / Super Administrador**
- **RF-17** *Bootstrap*: la configuración `Authorization__BootstrapRootObjectIds` lista identificadores de Entra ID
  (`oid`). **Solo si no existe ningún Root activo**, el inicio de sesión de una cuenta listada la marca como Root.
  Así se cubre la instalación inicial y la recuperación de emergencia, pero la configuración no anula una revocación hecha desde la UI.
- **RF-18** Un Root puede promover a otro usuario a Root y revocar el estado Root.
- **RF-19** Siempre debe existir al menos un Root activo: no se puede revocar, desactivar ni hacer que el último
  Root activo se revoque a sí mismo.

**Auditoría**
- **RF-20** Toda operación de administración de autorización (exitosa o rechazada por regla de negocio o salvaguarda)
  genera un evento de auditoría (§14.3).
- **RF-21** Consultar la auditoría con filtros por fecha, actor, acción y recurso.

**Frontend**
- **RF-22** El frontend conoce el estado Root y los permisos efectivos del usuario para mostrar u ocultar menús,
  rutas y botones. Solo es UX: la API valida siempre.

### 5.2 No funcionales
- **RNF-1** La evaluación de autorización añade como máximo una consulta ligera a la base de datos por solicitud;
  los permisos efectivos se guardan en caché e invalidan por versión (§10.3).
- **RNF-2** La propagación de cambios es inmediata: se aplican en la siguiente solicitud, también con varias réplicas.
- **RNF-3** La auditoría es inmutable a nivel de base de datos: el rol de la API no puede modificarla ni borrarla.
- **RNF-4** Las ediciones concurrentes de una misma política no se pierden: control optimista que responde `409` si cambió.
- **RNF-5** Las pantallas de administración cumplen §6 de la constitución (Material 3, temas claro/oscuro, accesibilidad).

## 6. Reglas de negocio

| ID | Regla |
|---|---|
| RN-1 | **Deny by default:** sin permiso explícito no hay acceso. |
| RN-2 | Permisos efectivos = unión de los permisos no obsoletos de todas las políticas **activas** asignadas a un usuario **activo**. |
| RN-3 | Un usuario Root **activo** está autorizado para cualquier permiso, sin necesidad de políticas. |
| RN-4 | Un usuario inactivo no tiene ningún permiso, aunque sea Root o tenga políticas. |
| RN-5 | Un usuario nuevo no tiene políticas ni permisos. |
| RN-6 | Nombre de permiso: `<recurso>.<acción>` o `<recurso>.<subrecurso>.<acción>`, en minúsculas y con al menos dos segmentos (p. ej. `customers.delete`). |
| RN-7 | El nombre de una política es único sin distinguir mayúsculas y minúsculas, y obligatorio (máx. 100 caracteres). |
| RN-8 | Los permisos **no otorgables** (p. ej. `authorization.root.manage`) no pueden incluirse en políticas: solo los ejerce el Root. |
| RN-9 | **Salvaguarda:** un no Root no puede modificar sus propias asignaciones de políticas ni su propio estado. |
| RN-10 | **Salvaguarda:** un no Root no puede añadir a una política un permiso que él mismo no tenga. |
| RN-11 | **Salvaguarda:** un no Root no puede asignar a otro usuario una política que contenga permisos que él no tenga. |
| RN-12 | **Salvaguarda:** un no Root no puede desactivar a un Root ni modificar sus asignaciones. |
| RN-13 | Siempre existe al menos un Root activo (RF-19). |

Las violaciones devuelven `422` (regla de negocio) o `403` (salvaguarda), y en ambos casos se auditan.

## 7. Permisos

Permisos de este módulo (declarados en código, categoría "Autorización"):

| Permiso | Otorgable | Concede |
|---|---|---|
| `users.read` | Sí | Consultar usuarios, sus políticas y sus permisos efectivos |
| `users.update` | Sí | Activar y desactivar usuarios (sujeto a RN-9 y RN-12) |
| `users.policies.assign` | Sí | Asignar políticas a usuarios (sujeto a RN-9, RN-11 y RN-12) |
| `users.policies.revoke` | Sí | Retirar políticas de usuarios (sujeto a RN-9 y RN-12) |
| `policies.read` | Sí | Consultar políticas y sus usuarios |
| `policies.create` | Sí | Crear políticas (sujeto a RN-10) |
| `policies.update` | Sí | Modificar, activar y desactivar políticas y sus permisos (sujeto a RN-10) |
| `policies.delete` | Sí | Eliminar políticas sin asignaciones |
| `permissions.read` | Sí | Consultar el catálogo de permisos |
| `permissions.update` | Sí | Editar la descripción y la categoría de los permisos |
| `audit.read` | Sí | Consultar la auditoría |
| `authorization.root.manage` | **No** | Promover y revocar Root (solo Root) |

## 8. Comportamiento esperado

### 8.1 Evaluación de autorización (`authorize(usuario, permiso)`)

```text
1. Identificar al usuario autenticado (sesión BFF) → usuario de la aplicación.
2. Si no existe o está inactivo                    → DENEGAR (y terminar la sesión).
3. Si es Root                                      → PERMITIR.
4. Si el permiso requerido es no otorgable         → DENEGAR.
5. Obtener los permisos efectivos (caché por versión): políticas activas → permisos no obsoletos.
6. Si contiene el permiso requerido                → PERMITIR; si no → DENEGAR (403, registro Warning).
```

### 8.2 Primer inicio de sesión y *bootstrap* del Root

```text
Login Entra ID OK → ¿existe identidad (proveedor, tenant, oid)?
   no → crear usuario (activo, sin políticas) [auditar user.provisioned]
   sí → actualizar nombre/correo y último acceso
¿usuario inactivo? → rechazar el login (/login?error=disabled)
¿no hay ningún Root activo y oid ∈ BootstrapRootObjectIds? → marcar Root [auditar root.bootstrapped]
```

### 8.3 Ejemplo (de la definición)

Juan tiene "Gestión de Clientes" (`customers.read/create/update`) y "Reportes" (`reports.read/export`):
`customers.delete` → **denegado**. Si se retira "Reportes", `reports.*` deja de estar disponible en su siguiente
solicitud. Si se añade `customers.delete` a "Gestión de Clientes", todos sus usuarios lo obtienen sin modificar a Juan.

## 9. Criterios de aceptación

Los CA-1 a CA-17 provienen de la definición original; los CA-18 a CA-24 los añaden las decisiones del 2026-10-07.

- [ ] **CA-1** Un usuario nuevo tiene cero privilegios: cualquier endpoint protegido por permiso devuelve 403.
- [ ] **CA-2** Los permisos existen en el catálogo independientemente de usuarios y políticas.
- [ ] **CA-3** Una política solo puede crearse con permisos existentes del catálogo (no obsoletos y otorgables).
- [ ] **CA-4** Una política puede contener múltiples permisos.
- [ ] **CA-5** Una política puede asignarse a múltiples usuarios.
- [ ] **CA-6** Un usuario puede tener múltiples políticas.
- [ ] **CA-7** Los permisos efectivos son la unión de los permisos de sus políticas activas.
- [ ] **CA-8** Al retirar una política se pierden los permisos que solo ella concedía y se conservan los que también concede otra.
- [ ] **CA-9** Modificar una política afecta a todos sus usuarios en su siguiente solicitud.
- [ ] **CA-10** Toda operación protegida se valida en el backend (prueba de convención + pruebas 401/403 por endpoint).
- [ ] **CA-11** Denegación por defecto: un permiso no concedido o desconocido se deniega.
- [ ] **CA-12** Mínimo privilegio: ningún privilegio implícito al crear un usuario o una política.
- [ ] **CA-13** El Root accede a cualquier endpoint protegido sin políticas asignadas.
- [ ] **CA-14** Cada operación de administración genera un evento de auditoría con actor, acción, recurso, id, fecha, resultado y valores anterior y posterior.
- [ ] **CA-15** Manipular el frontend o llamar directamente a la API no permite obtener privilegios (pruebas de API sin UI).
- [ ] **CA-16** Para un usuario se puede ver qué política(s) conceden cada permiso efectivo.
- [ ] **CA-17** No existen roles como mecanismo de autorización.
- [ ] **CA-18** Desactivar un usuario invalida su sesión en su siguiente solicitud e impide nuevos inicios de sesión.
- [ ] **CA-19** No se puede dejar el sistema sin un Root activo.
- [ ] **CA-20** El *bootstrap* solo actúa cuando no hay ningún Root activo.
- [ ] **CA-21** Las salvaguardas RN-9 a RN-12 impiden la escalada de privilegios de administradores delegados (403 y evento auditado).
- [ ] **CA-22** Un permiso obsoleto deja de conceder acceso y no puede añadirse a políticas.
- [ ] **CA-23** El rol de base de datos de la API no puede modificar ni borrar eventos de auditoría.
- [ ] **CA-24** Ediciones concurrentes de una política responden `409` en lugar de sobrescribirse.

## 10. Análisis arquitectónico

### 10.1 Encaje con la fundación
- Se conserva el contrato de los endpoints (`[RequirePermission]`) y el puerto `IPermissionEvaluator`. Solo cambia la
  implementación registrada. Los controllers existentes y futuros no se modifican.
- La autenticación (ADR-0002) no cambia. Al validar el login (`OnTokenValidated`) se añade el aprovisionamiento del
  usuario. La sesión incorpora el identificador interno del usuario y una **marca de seguridad** para invalidarla al desactivarlo.
- El nombre de permiso de dos segmentos (`customers.read`) ya es válido para `PermissionName`. Hay que corregir el texto
  de la constitución §3.3, que menciona tres segmentos (enmienda PATCH).

### 10.2 Modelo de dominio (nuevo módulo `Authorization`)

```text
User (agregado)            Id (uuid), DisplayName, Email, IsActive, IsRoot, SecurityStamp, CreatedAt, LastSignInAt
 └─ ExternalIdentity       Provider ("entra-id"), Issuer/TenantId, Subject (oid)   ← permite otros IdP (§17 de la definición)
Policy (agregado)          Id, Name, Description, IsActive, Version (concurrencia)
 └─ PolicyStatement        PermissionName, Effect = Allow   ← punto de extensión: Effect=Deny, Resource, Conditions
PermissionDefinition       Name (PK), DisplayName, Description, Category, IsGrantable, IsDeprecated
PolicyAssignment           UserId, PolicyId, AssignedAt, AssignedBy
AuditEvent                 Id, OccurredAt, ActorUserId, ActorDisplayName, Action, ResourceType, ResourceId,
                           Result, Before (jsonb), After (jsonb), TraceId
```

Las reglas RN-* viven en el dominio y en los casos de uso de `Application`. Las salvaguardas necesitan conocer al actor
(`ICurrentUser`) y sus permisos efectivos.

### 10.3 Evaluación y caché
- `PolicyPermissionEvaluator` (Infrastructure) implementa `IPermissionEvaluator` según §8.1.
- La caché en memoria guarda los permisos efectivos por usuario, con clave `(userId, authzVersion)`. `authzVersion` es un
  contador global en la BD que se incrementa en la misma transacción que cualquier cambio de políticas, asignaciones,
  usuarios o catálogo. Cada solicitud lee ese contador (1 consulta trivial), así que funciona con varias réplicas y
  cumple RNF-1 y RNF-2.
- Alternativa descartada: TTL fijo (simple, pero deja privilegios revocados vigentes durante el TTL).

### 10.4 Extensibilidad (sin implementarla)

| Capacidad futura | Dónde encaja |
|---|---|
| Denegación explícita | `PolicyStatement.Effect = Deny`; el evaluador prioriza Deny sobre Allow |
| Permisos sobre recursos concretos / *scopes* | `PolicyStatement.Resource` (patrón) + `RequirePermission` con proveedor de recurso |
| Condiciones (horario, IP, atributos) / ABAC | `PolicyStatement.Conditions` (jsonb) + evaluadores de condición encadenados |
| Multi-organización | `TenantId` en `PolicyAssignment` y en el contexto de evaluación |
| Comodines | Coincidencia jerárquica de nombres (`customers.*`) en el evaluador |
| Otro proveedor de identidad | Nueva fila `ExternalIdentity.Provider`; el modelo de autorización no cambia |

### 10.5 Decisiones que requieren ADR
- **ADR-0008** (nuevo): modelo de autorización por políticas (sustituye la parte de implementación de ADR-0003),
  caché por versión, salvaguardas anti-escalada, Root por *bootstrap*.
- ADR-0004 no cambia: la auditoría *append-only* se consigue revocando privilegios desde la migración (§13).

### 10.6 Conformidad con la constitución
Cumple §2, §3, §5 y §7. Requiere una enmienda **PATCH** en §3.3 (convención de nombres de permisos). No relaja ningún control.

## 11. Cambios de frontend

- **Sesión:** `GET /auth/session` añade `isRoot` y `permissions: string[]` (efectivos). `AuthService` expone
  `hasPermission(p)` (true si es Root o contiene `p`).
- **Guards y navegación:** `permissionGuard` (datos de ruta `permission`), `NAVIGATION_ITEMS` con `permission`
  opcional, y una directiva estructural `*appIfPermission` para mostrar u ocultar acciones. Ninguno es una frontera de seguridad.
- **Login:** mensaje para `/login?error=disabled` ("Tu cuenta está desactivada").
- **Área "Administración"** (`features/authorization/`, *lazy*), con componentes de Angular Material
  (`mat-table`, `mat-paginator`, `mat-sort`, `mat-dialog`, `mat-chips`, `mat-slide-toggle`, `mat-autocomplete`, `mat-tabs`):

| Pantalla | Contenido | Permiso |
|---|---|---|
| Usuarios | Lista con búsqueda y filtro de estado | `users.read` |
| Detalle de usuario | Pestañas: Políticas (asignar/retirar), Permisos efectivos (permiso → políticas), Estado, Root (solo Root) | `users.read` (+ acciones) |
| Políticas | Lista, crear | `policies.read` / `policies.create` |
| Detalle de política | Datos, permisos por categoría (selector), usuarios asignados, activar/eliminar | `policies.read` (+ acciones) |
| Permisos | Catálogo por categoría, políticas que lo contienen, editar descripción | `permissions.read` |
| Auditoría | Tabla filtrable; detalle con diferencias anterior/posterior | `audit.read` |

- Estados de carga, vacío y error, y confirmaciones (`mat-dialog`) para retirar, desactivar, eliminar y gestionar Root.

## 12. Cambios de backend

Todos los endpoints bajo `/api/v1`, con autorización explícita (§3.3) y errores en Problem Details.

| Método | Ruta | Permiso | Notas |
|---|---|---|---|
| GET | `/users?search=&status=&page=&pageSize=` | `users.read` | Paginado |
| GET | `/users/{id}` | `users.read` | Incluye políticas asignadas |
| GET | `/users/{id}/effective-permissions` | `users.read` | `[{ permission, grantedBy: [{policyId, name}] }]`; Root → `isRoot: true` |
| PATCH | `/users/{id}/status` | `users.update` | `{ isActive }` · RN-4, RN-9, RN-12, RN-13 |
| PUT | `/users/{id}/policies/{policyId}` | `users.policies.assign` | Idempotente · RN-9, RN-11, RN-12 |
| DELETE | `/users/{id}/policies/{policyId}` | `users.policies.revoke` | Idempotente · RN-9, RN-12 |
| PUT / DELETE | `/users/{id}/root` | `authorization.root.manage` | Solo Root · RN-13 |
| GET | `/policies?search=&status=&page=` | `policies.read` | |
| POST | `/policies` | `policies.create` | `{ name, description, permissions[] }` · RN-7, RN-10 |
| GET | `/policies/{id}` | `policies.read` | Devuelve `ETag` (versión) |
| PUT | `/policies/{id}` | `policies.update` | `If-Match` obligatorio → `412`/`409` si cambió · RN-10 |
| DELETE | `/policies/{id}` | `policies.delete` | `409` si tiene asignaciones (RF-14) |
| GET | `/policies/{id}/users` | `policies.read` | |
| GET | `/permissions` | `permissions.read` | Incluye obsoletos (marcados) |
| GET | `/permissions/{name}/policies` | `permissions.read` | |
| PATCH | `/permissions/{name}` | `permissions.update` | `{ displayName, description, category }` |
| GET | `/audit-events?from=&to=&actorId=&action=&resourceType=&resourceId=&page=` | `audit.read` | Solo lectura |

Otros cambios:
- **Application:** casos de uso por operación (`Authorization/<CasoDeUso>/`), puertos `IUserRepository`,
  `IPolicyRepository`, `IPermissionCatalog`, `IAuditLog`, `IAuthorizationVersion`.
- **Infrastructure:** repositorios EF Core, `PolicyPermissionEvaluator` con caché, sincronización del catálogo al
  arrancar (idempotente y segura con réplicas), escritor de auditoría en la misma transacción que el cambio.
- **Api:** aprovisionamiento en `OnTokenValidated`. En `OnValidatePrincipal`, validación de usuario activo y marca de
  seguridad (la sesión se cierra si no coinciden). `/auth/session` extendido.
- **Catálogo en código:** cada módulo aporta una clase `IPermissionDefinitionProvider`. Una prueba verifica que todo
  `[RequirePermission]` usado en el código existe en el catálogo.
- **Configuración nueva:** `Authorization__BootstrapRootObjectIds__0..n` (`.env`: `AUTHZ_BOOTSTRAP_ROOT_OIDS`, separados por comas).

## 13. Cambios de base de datos

Esquema `app` (migración `AddPolicyBasedAuthorization`):

| Tabla | Claves e índices principales |
|---|---|
| `users` | PK `id` (uuid); `is_active`, `is_root`, `security_stamp`; índice en `lower(email)` |
| `user_external_identities` | PK `id`; **único** `(provider, issuer, subject)`; FK `user_id` |
| `permissions` | PK `name`; `category`, `is_grantable`, `is_deprecated` |
| `policies` | PK `id` (uuid); **único** `lower(name)`; `is_active`; `xmin` como token de concurrencia |
| `policy_statements` | PK `(policy_id, permission_name)`; `effect` (`allow`); FK a `policies` (cascade) y `permissions` (restrict) |
| `policy_assignments` | PK `(user_id, policy_id)`; `assigned_at`, `assigned_by`; índice `policy_id` |
| `authorization_state` | Fila única con `version bigint` (contador de invalidación) |
| `audit_events` | PK `id`; índices `(occurred_at desc)`, `(resource_type, resource_id)`, `(actor_user_id)`; `before`/`after` `jsonb` |

**Privilegios:** la migración que crea `audit_events` revoca `UPDATE`, `DELETE` y `TRUNCATE` sobre esa tabla a todos
los roles distintos de su dueño (el migrador puede hacerlo porque es el dueño y quien concedió los *default privileges*).
Así el rol de la API solo puede insertar y leer (CA-23). Funciona igual en volúmenes nuevos y existentes, sin crear
roles nuevos (el migrador no tiene `CREATEROLE`). *Ajuste aprobado durante la implementación: sustituye la propuesta
inicial de un rol de grupo.*

## 14. Consideraciones de seguridad

### 14.1 Amenazas (STRIDE)

| Amenaza | Mitigación |
|---|---|
| Escalada por administrador delegado (auto-asignarse políticas, ampliar una política propia) | RN-9 a RN-12; pruebas CA-21; auditoría de intentos rechazados |
| Bloqueo total del sistema (sin Root) | RN-13; *bootstrap* solo con cero Root activos |
| Uso de configuración para recuperar Root indebidamente | El *bootstrap* exige acceso al despliegue y queda auditado (`root.bootstrapped`) |
| Privilegios revocados que siguen vigentes | Invalidación por versión en cada solicitud; marca de seguridad para usuarios desactivados |
| Manipulación del frontend o de las solicitudes | Toda decisión en el servidor; el cliente solo recibe la lista de permisos para UX |
| Suplantación por cambio de correo o UPN en Entra ID | La clave es `(provider, tenant, oid)` inmutable; el correo es solo informativo |
| Alteración de la auditoría | Tabla *append-only* a nivel de privilegios de BD |
| Permisos "fantasma" sin efecto | Catálogo en código + prueba de cobertura de `[RequirePermission]` |
| Pérdida de actualizaciones concurrentes | Concurrencia optimista (`ETag`/`If-Match`) |
| Enumeración de usuarios | Endpoints de usuarios protegidos por `users.read` |

### 14.2 Datos personales
Se almacenan nombre visible, correo/UPN y `oid` (necesarios para administrar). No se registran en logs (solo el `id`
interno o el `oid`). La auditoría contiene datos personales en `before`/`after`, por lo que su acceso se restringe
con `audit.read` y su retención se define en P4.

### 14.3 Eventos de auditoría

`user.provisioned`, `user.activated`, `user.deactivated`, `user.policy.assigned`, `user.policy.revoked`,
`root.bootstrapped`, `root.granted`, `root.revoked`, `policy.created`, `policy.updated`, `policy.activated`,
`policy.deactivated`, `policy.deleted`, `permission.updated`, `permission.synced` (altas y obsolescencias del catálogo)
Los intentos fallidos se registran con la acción intentada y resultado `rejected` (regla de negocio o conflicto) o
`denied` (salvaguardas RN-9 a RN-12), con el motivo en `after`. *Ajuste de implementación: sustituye la acción separada
`authorization.denied.safeguard` por el campo resultado, que permite filtrar cualquier acción por resultado.*
Campos: actor, acción, tipo e id de recurso, fecha UTC, resultado (`success` / `rejected` / `denied`), valores anterior y
posterior, `traceId`.

## 15. Pruebas

| Criterio | Tipo | Prueba prevista |
|---|---|---|
| CA-1, CA-11, CA-12 | Integración | Usuario aprovisionado sin políticas → 403 en endpoint con permiso; permiso desconocido → 403 |
| CA-2, CA-3, CA-22 | Unit + Integración | Sincronización del catálogo; crear política con permiso inexistente, obsoleto o no otorgable → 422 |
| CA-4 a CA-9 | Unit (dominio y evaluador) + Integración | Unión de políticas; retirar o modificar una política → la siguiente solicitud refleja el cambio |
| CA-10, CA-15 | Unit (convención) + Integración | Todo `[RequirePermission]` está en el catálogo; llamadas directas a la API sin permiso → 403 |
| CA-13 | Integración | Root sin políticas → 200 en cualquier endpoint protegido |
| CA-14, CA-23 | Integración | Cada operación crea su evento; `UPDATE/DELETE` sobre `audit_events` con el rol de la app → `42501` |
| CA-16 | Integración | `effective-permissions` devuelve `grantedBy` correcto con permisos solapados |
| CA-17 | Revisión + convención | No existe ninguna tabla, atributo ni política basada en roles |
| CA-18 | Integración | Desactivar → la siguiente solicitud con la misma sesión devuelve 401 |
| CA-19, CA-20 | Unit + Integración | Revocar o desactivar el último Root → 422; *bootstrap* con y sin Root existente |
| CA-21 | Integración | Cada salvaguarda con un administrador delegado → 403 + evento `authorization.denied.safeguard` |
| CA-24 | Integración | Dos `PUT` con el mismo `ETag` → el segundo devuelve 412/409 |
| UI | Frontend | `hasPermission`, `permissionGuard`, `*appIfPermission`, pantallas con estados vacío y error |

## 16. Plan de implementación

Entrega en tres incrementos verificables:

| # | Tarea | Agente | Archivos principales |
|---|---|---|---|
| 1 | ADR-0008, enmienda PATCH de la constitución §3.3, actualizar ADR-0003 | architecture | `specs/architecture/`, `constitution.md` |
| 2 | Migración del modelo + auditoría *append-only* | database | `Infrastructure/Persistence/**` |
| 3 | Dominio `Authorization` (User, Policy, PermissionDefinition, reglas RN-*) | backend | `Itmas3.Domain/Authorization/**` |
| 4 | Casos de uso, puertos y salvaguardas | backend | `Itmas3.Application/Authorization/**` |
| 5 | Repositorios, evaluador con caché por versión, sincronización del catálogo, auditoría | backend | `Itmas3.Infrastructure/Authorization/**` |
| 6 | Aprovisionamiento al iniciar sesión, *bootstrap* Root, marca de seguridad, `/auth/session` | backend + security | `Itmas3.Api/Authentication/**` |
| 7 | Controllers y contratos `/api/v1/...` | backend | `Itmas3.Api/Controllers/**`, `Contracts/**` |
| — | **Incremento A** (API completa y verificable sin UI) | testing | `backend/tests/**` |
| 8 | `hasPermission`, `permissionGuard`, `*appIfPermission`, navegación por permiso | frontend | `frontend/src/app/auth/**`, `layout/**` |
| 9 | Pantallas de Usuarios y Políticas | frontend | `features/authorization/**` |
| — | **Incremento B** (administración operativa) | testing | `*.spec.ts` |
| 10 | Pantallas de Permisos y Auditoría | frontend | `features/authorization/**` |
| 11 | Revisión de seguridad, documentación (SECURITY, ARCHITECTURE, README, `.env.example`) | security + architecture | docs |
| — | **Incremento C** (cierre, spec → Implemented) | testing + security | — |

## 17. Trazabilidad

Implementada el 2026-10-07.

| Área | Archivos principales |
|---|---|
| Decisión | `specs/architecture/adr-0008-policy-based-authorization.md`; constitución 1.0.1 (§3.3, §3.4) |
| Dominio | `backend/src/Itmas3.Domain/Authorization/` (User, Policy, PolicyStatement, PermissionDefinition…) |
| Aplicación | `backend/src/Itmas3.Application/Authorization/` (catálogo, evaluador, salvaguardas, casos de uso, consultas) |
| Infraestructura | `backend/src/Itmas3.Infrastructure/Authorization/`, `Persistence/Configurations/`, migración `AddPolicyBasedAuthorization` |
| API | `Itmas3.Api/Authentication/{SignInProvisioning,SessionValidationMiddleware}.cs`, `Controllers/Authorization/`, `Authorization/AuthorizationModuleSetup.cs`, `PermissionCatalogSynchronizer.cs` |
| Frontend | `frontend/src/app/features/authorization/`, `auth/guards/permission-guard.ts`, `auth/directives/if-permission.ts`, `layout/navigation/` |
| Configuración | `AUTHZ_BOOTSTRAP_ROOT_OIDS`, `AUTHZ_ALLOW_GUEST_USERS` (`.env.example`, `docker-compose.yml`) |

| Criterio | Prueba |
|---|---|
| CA-1, CA-11, CA-12 | `PolicyAuthorizationTests.CA01_*`; `AuthorizationTests` (sin metadatos / sin permiso) |
| CA-2, CA-3 | `PolicyAuthorizationTests.CA03_*` (inexistente y no otorgable → 422); `PolicyTests` |
| CA-4 a CA-9, CA-16 | `PolicyAuthorizationTests.CA04_to_CA09_and_CA16_*`, `RF12_inactive_policies_grant_nothing` |
| CA-10, CA-15 | `EndpointAuthorizationConventionTests`, `PermissionCatalogConventionTests`; todas las pruebas de integración llaman a la API sin UI |
| CA-13 | `PolicyAuthorizationTests.CA13_*`, `AuthorizationSnapshotTests` |
| CA-14 | `PolicyAuthorizationTests.CA14_*`, `CA21_*` (eventos `denied`) |
| CA-17 | Revisión: no existe ninguna tabla, atributo ni política de roles; constitución §3.4 |
| CA-18 | `PolicyAuthorizationTests.CA18_*`, `RootProtectionTests.Deactivation_rotates_the_security_stamp` |
| CA-19 | `RootProtectionTests` |
| CA-20 | `ProvisionUserHandlerTests.CA20_*` |
| CA-21 | `PolicyAuthorizationTests.CA21_*`, `AdministrationSafeguardsTests` |
| CA-22 | `PermissionCatalogTests.CA22_*` |
| CA-23 | `PolicyAuthorizationTests.CA23_*` |
| CA-24 | `PolicyAuthorizationTests.CA24_*` |
| UI | `permission-guard.spec.ts`, `if-permission.spec.ts`, `shell.spec.ts`, `auth-service.spec.ts`; revisión visual (claro/oscuro, escritorio/móvil) |

Totales al cerrar: backend 126 pruebas (77 unitarias + 49 de integración), frontend 35.

**Validación manual pendiente del solicitante:** primer Root mediante `AUTHZ_BOOTSTRAP_ROOT_OIDS` con una cuenta real
del tenant (README → "Primer administrador").

## 18. Decisiones sobre las preguntas abiertas (resueltas el 2026-10-07)

| # | Pregunta | Decisión |
|---|---|---|
| P1 | Alta manual de usuarios en v1 | Excluida. Alta solo automática en el primer inicio de sesión. |
| P2 | Invitados B2B del tenant | Rechazados salvo `Authorization__AllowGuestUsers=true`. |
| P3 | Identificador del *bootstrap* Root | `oid` de Entra ID. |
| P4 | Retención de auditoría / IP | Retención indefinida en v1; sin IP. |
| P5 | Eliminar política con usuarios asignados | Bloquear (409); se puede desactivar. |
| P6 | Comodines | No en v1. |
| P7 | Políticas predefinidas | Ninguna; solo el catálogo de permisos. |
| P8 | Notificaciones al usuario | No en v1. |
| P9 | Usuarios sin políticas en la lista | Sí, con filtro "sin políticas". |
