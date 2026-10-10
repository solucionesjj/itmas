---
id: SPEC-0003
title: Registro de equipos e inventario recolectado
type: feature
status: Implemented
owner: jmartinez
created: 2026-10-09
approved-by: jmartinez (2026-10-09)
agents: [architecture, security, database, backend, frontend, testing]
related: [SPEC-0001, SPEC-0002, ADR-0008, ADR-0009, ADR-0010]
---

# SPEC-0003 — Registro de equipos e inventario recolectado

> Derivada de la definición funcional aportada por el solicitante (2026-10-09) y de sus respuestas a dos rondas de
> preguntas (2026-10-09), recogidas en §18. Las propuestas de `architecture` se identifican con P-n.
> Aprobada el 2026-10-09.

## 1. Objetivo

1. Mantener un **registro de equipos**, clasificados como **estación de trabajo** o **servidor**, en el que cada equipo
   recibe un **código único**. El agente instalado en el equipo usa ese código para enrolarse y enviar su inventario.
   Ningún agente puede enrolarse sin que su equipo se haya registrado antes.
2. Asociar a cada equipo el **correo de su propietario** y, si coincide con un usuario de ITMas, mostrar también su
   nombre, para poder trazar la relación entre colaboradores y equipos.
3. **Consultar el inventario recolectado** por los agentes: un listado resumido por equipo, su historial de registros y
   un informe del último registro, organizado por secciones.

## 2. Contexto

SPEC-0002 dejó en producción el agente, el enrolamiento y la recepción del inventario. Hoy:

- Un equipo aparece en el sistema cuando su agente se enrola con el **código de autorización general**
  (`INVENTORY_ENROLLMENT_CODE`), que es un único secreto compartido por todos los equipos (ADR-0009, punto 1).
- No hay interfaz web: el inventario solo se puede consultar en la base de datos.
- Revocar un equipo requiere un procedimiento operativo con SQL (SECURITY.md §8).

SPEC-0002 y ADR-0009 ya anticipaban esta etapa: "SPEC-0003 sustituirá el código único por códigos gestionados
(caducidad, usos, revocación) y añadirá la revocación de dispositivos", junto con la interfaz de consulta y los
permisos `devices.*`.

### Definición original (solicitante)

> Necesitamos crear un inventario de equipos, compuesto por los equipos de los colaboradores y los diferentes
> servidores de nuestra red. En estos equipos está instalado el agente de SPEC-0002. El objetivo es asignar un código
> único a cada equipo, que luego cada agente usará para interactuar con la API.
>
> Al crear el equipo se le puede asociar el correo del propietario, para que más adelante se pueda trazar la relación
> entre propietarios y equipos.
>
> Se deben poder agregar, actualizar y eliminar equipos. Al eliminar un equipo, su código queda inactivo y el agente
> instalado en el equipo ya no podrá entregar los datos a la API.
>
> Datos de cada equipo: nombre y correo del colaborador asociado. Los dos se pueden actualizar.
>
> Además, se necesita consultar los datos capturados: un listado resumido o agrupado por equipo que muestre de un
> vistazo el nombre del equipo, el usuario asociado (si no tiene: "Sin usuario asociado"), la fecha del último
> registro capturado y la cantidad de registros del historial. Con un botón para ver los registros en detalle y otro
> para ver el último registro como un informe de la máquina agrupado por secciones: características del equipo, datos
> de red, almacenamiento, software instalado, etc.

### Cómo encaja el "código único" con la autenticación actual

En la definición, el agente "usa el código para interactuar con la API". Usar el código directamente como credencial en
cada envío contradice ADR-0009: el código solo sirve para el enrolamiento, y a cambio el servidor emite una API key
por dispositivo, de la que solo guarda el hash. Esta spec **mantiene ese mecanismo** y solo cambia el origen del código:

```text
Hoy (SPEC-0002):  código general (uno para todos) ──enrolamiento──▶ API key del dispositivo ──▶ envíos diarios
SPEC-0003:        código del equipo (uno por equipo) ──enrolamiento──▶ API key del dispositivo ──▶ envíos diarios
```

Para el usuario el resultado es el esperado: cada equipo tiene su código, el agente lo introduce al instalarse y, al
eliminar el equipo, el código y la credencial dejan de funcionar. **El agente no cambia**: ya pide un "código de
autorización" en `install`, y ahora será el código del equipo (§10.3).

El solicitante confirmó (§18.1) que el "código" de la definición equivale a la API key del dispositivo y que se mantiene
el mecanismo de ADR-0009: el código del equipo solo sirve para obtener esa API key.

## 3. Alcance

**Incluye**
- Alta, modificación y eliminación (lógica) de equipos, con nombre, tipo (estación de trabajo o servidor) y correo del
  propietario.
- Nombre del colaborador obtenido de los usuarios de ITMas cuando su correo coincide con el del propietario (P-5).
- Un código de enrolamiento por equipo, generado por el servidor y mostrado una sola vez (P-2, P-3).
- Enrolamiento del agente con el código del equipo, en sustitución del código general (P-1).
- Incorporación al registro de los dispositivos ya enrolados con el código general (P-6).
- Listado resumido de equipos con búsqueda, filtro por estado, orden y paginación.
- Historial de registros de un equipo.
- Informe de un registro (el último u otro del historial) organizado por secciones.
- Permisos `devices.*` y auditoría de las operaciones.

**Excluye**
- Cambios en el agente (§10.3).
- Una relación persistida (clave foránea) entre el equipo y un usuario de ITMas: la coincidencia por correo se calcula
  al consultar (P-5). Tampoco se incluye una vista "equipos de un colaborador" más allá de la búsqueda por nombre o correo.
- Alta de colaboradores: solo existen como usuarios de ITMas los que han iniciado sesión alguna vez (SPEC-0001 RF-1).
- Exportación del informe (PDF, Excel) e impresión con formato propio (P-10).
- Comparación entre registros (qué cambió de un día a otro), alertas y paneles de cumplimiento.
- Reactivación de un equipo eliminado (P-4).
- Eliminación física de equipos o de su historial desde la interfaz.
- Multi-organización (el modelo sigue sin impedirla, SPEC-0002 §10.4).

## 4. Actores

| Actor | Descripción |
|---|---|
| Administrador de inventario | Usuario de ITMas con permisos `devices.*` (§7). Da de alta, modifica y elimina equipos, y entrega el código al instalador. |
| Consultor de inventario | Usuario de ITMas con `devices.read` y, si se le concede, `devices.reports.read`. Consulta el listado, el historial y los informes. |
| Instalador | Persona que instala el agente en el equipo (SPEC-0002) e introduce el código del equipo. |
| Agente | Servicio instalado en el equipo. Se enrola con el código y después envía el inventario con su API key. |

## 5. Requerimientos

### 5.1 Funcionales

**Registro de equipos**
- **RF-1** Crear un equipo con **nombre** (obligatorio), **tipo** (obligatorio: Estación de trabajo o Servidor) y
  **correo del propietario** (opcional). Al crearlo, el servidor genera su código de enrolamiento y lo devuelve en la
  respuesta. El equipo queda en estado **Pendiente**.
- **RF-2** El código se muestra al usuario **una sola vez**, en un diálogo con botón de copiar. El servidor solo
  guarda su hash y no puede volver a mostrarlo (P-2).
- **RF-3** Regenerar el código de un equipo que no está eliminado: se emite un código nuevo y el anterior deja de
  servir. Se usa cuando el código se perdió, se filtró o hay que reinstalar el agente (P-3).
- **RF-4** Modificar el nombre, el tipo y el correo del propietario. El correo se puede quitar.
- **RF-4b** **Colaborador asociado** (P-5). Cuando el correo del propietario coincide, sin distinguir mayúsculas, con el
  correo de un usuario de ITMas (SPEC-0001), la interfaz muestra el nombre de ese usuario junto al correo. La
  coincidencia se calcula en cada consulta, así que refleja los cambios de nombre y los usuarios que se den de alta
  después. Si no hay coincidencia, solo se muestra el correo. Si varios usuarios comparten el correo, se usa el que
  inició sesión más recientemente.
- **RF-5** Eliminar un equipo, con confirmación. El equipo pasa a estado **Eliminado**:
  1. su código deja de servir;
  2. la API key del agente deja de servir: el siguiente envío recibe `401` y el agente deja de enviar (SPEC-0002 RF-10);
  3. su historial se conserva con la retención normal de 90 días (SPEC-0002 RF-17) y se puede consultar (P-4).
- **RF-6** Los estados de un equipo son:

  | Estado | Significado |
  |---|---|
  | Pendiente | Creado; ningún agente se ha enrolado todavía con su código. |
  | Activo | Un agente se ha enrolado con su código y puede enviar inventario. |
  | Eliminado | Eliminado por un usuario. Ni el código ni la API key sirven. |

**Enrolamiento con el código del equipo**
- **RF-7** El agente se enrola con `POST /api/v1/device-enrollments` presentando el código del equipo, igual que hoy
  con el código general. Si el código pertenece a un equipo Pendiente, el equipo se vincula al identificador de
  hardware del agente, pasa a Activo y el servidor emite la API key. El código queda **consumido** (P-3).
- **RF-8** Un código incorrecto, consumido, regenerado o de un equipo Eliminado se rechaza con `401`, sin dar el
  motivo (SPEC-0002 RN-2). El rechazo se registra en el log sin el código.
- **RF-9** Reinstalación: con un código nuevo (RF-3) del mismo equipo Activo, el agente se vuelve a enrolar, el equipo
  se vincula al identificador de hardware que presente y la API key anterior deja de servir (equivale a RN-4 de
  SPEC-0002). Queda auditado como `device.relinked`.
- **RF-10** Si el identificador de hardware que presenta el agente ya pertenece a **otro** equipo Activo, el
  enrolamiento se rechaza con `409` y un mensaje que pide eliminar primero ese equipo. No se "mueve" un equipo
  automáticamente (RN-4).
- **RF-11** El código de enrolamiento general (`INVENTORY_ENROLLMENT_CODE`) se retira: la configuración, el código y la
  documentación que lo usan se eliminan (P-1).

**Listado de equipos**
- **RF-12** Listado paginado con una fila por equipo y estas columnas (definición):

  | Columna | Contenido |
  |---|---|
  | Nombre del equipo | Nombre dado en el registro. Debajo, en texto secundario, el *hostname* que informó el agente si es distinto (P-7). |
  | Tipo | Estación de trabajo o Servidor. |
  | Usuario asociado | Nombre del colaborador (RF-4b) y debajo su correo; solo el correo si no coincide con ningún usuario; "Sin usuario asociado" si no tiene correo. |
  | Último registro | Fecha y hora de recopilación (`collectedAt`) del último inventario, en la zona horaria del navegador. Si no hay ninguno: "Sin registros". |
  | Registros en el historial | Cantidad de inventarios conservados (los últimos 90 días). |
  | Estado | Pendiente, Activo o Eliminado (P-8). |
  | Acciones | **Ver registros** (historial), **Ver último informe**, Editar, Regenerar código y Eliminar, según los permisos del usuario. |

- **RF-13** El listado permite buscar por nombre del equipo, *hostname*, correo o nombre del colaborador; filtrar por
  estado (por defecto, Pendientes y Activos) y por tipo; y ordenar por nombre, usuario asociado o fecha del último
  registro (P-8).
- **RF-14** "Ver último informe" se desactiva si el equipo no tiene registros.

**Historial de registros ("Ver registros en detalle")**
- **RF-15** El historial de un equipo es una tabla paginada, del más reciente al más antiguo, con una fila por
  inventario recibido: fecha de recopilación, fecha de recepción, IP de conexión, IP pública declarada con su ubicación
  (ciudad, región, país), versión del agente y número de secciones no disponibles (P-9).
- **RF-16** Cada fila abre el informe de ese registro (RF-17).

**Informe del registro ("Ver último informe")**
- **RF-17** El informe muestra un registro completo, organizado por secciones, con la cabecera del equipo (nombre, tipo,
  estado y colaborador asociado con su nombre y correo, RF-4b) y la fecha de recopilación y recepción del registro.
  La pantalla de historial lleva la misma cabecera. Cuando el registro no es el último,
  lo indica y ofrece ir al último.

  | Sección | Contenido (contrato `v1` de SPEC-0002 §12.2) |
  |---|---|
  | Características del equipo | *Hostname*, sistema operativo, versión, compilación, arquitectura, procesadores (modelo, núcleos físicos, procesadores lógicos), memoria RAM, versión del agente. |
  | Seguridad | Cifrado del disco del sistema; cifrado por volumen; estado del firewall con el detalle por perfil o producto. |
  | Red | Interfaces con sus IPv4 e IPv6; IP pública declarada con país, región, ciudad y organización; IP de conexión registrada por el servidor. Si la IP pública declarada y la de conexión difieren, se indica, porque puede deberse a un proxy, una VPN o un dato manipulado (SPEC-0002 RF-16). |
  | Almacenamiento | Discos (modelo, capacidad, tecnología, bus) y volúmenes (punto de montaje, sistema de archivos, capacidad, espacio libre y porcentaje usado, disco, si es el de arranque, cifrado). |
  | Usuarios locales | Cuentas con su estado (habilitada o no), la marca de administrador y sus grupos. |
  | Software instalado | Lista de aplicaciones ordenada alfabéticamente, con el total y un campo para filtrarla. |
  | Datos no disponibles | Secciones que el agente no pudo obtener y su motivo. Solo aparece si hay alguna. |

- **RF-18** Las capacidades se muestran en unidades legibles (GB, TB) y los estados normalizados en español
  ("Cifrado", "Sin cifrar", "Activo", "Inactivo", "Desconocido"…). Los valores no disponibles se muestran como
  "No disponible", nunca como un valor supuesto (SPEC-0002 RN-6).

### 5.2 No funcionales

- **RNF-1** Capacidad: el listado responde en menos de 1 s con 800 equipos y 90 registros por equipo (10 veces el
  volumen previsto en SPEC-0002 RNF-4).
- **RNF-2** Las pantallas cumplen la constitución §6: Angular Material estándar, temas claro y oscuro, textos en
  español, accesibilidad (etiquetas ARIA en botones de icono, navegación por teclado) y uso en pantallas estrechas.
- **RNF-3** Las ediciones concurrentes de un equipo no se pierden: control optimista con `ETag`/`If-Match`, que
  responde `412` si el equipo cambió (como las políticas en SPEC-0001).
- **RNF-4** Todo dato que procede de un equipo es no confiable: la interfaz lo muestra siempre como texto, nunca como
  HTML (SPEC-0002 §12.2).

## 6. Reglas de negocio

| ID | Regla |
|---|---|
| RN-1 | El nombre del equipo es obligatorio, de 1 a 100 caracteres sin caracteres de control, y **único sin distinguir mayúsculas** entre los equipos no eliminados (P-7). |
| RN-2 | El correo del propietario es opcional. Si se informa, debe ser una dirección válida de hasta 254 caracteres; se guarda recortado y en minúsculas. No tiene que ser un usuario de ITMas ni pertenecer a un dominio concreto (P-5). |
| RN-2b | El tipo es obligatorio y solo admite `workstation` (Estación de trabajo) o `server` (Servidor) (P-14). |
| RN-3 | Cada equipo tiene como máximo **un código vigente**. Es de un solo uso: se consume al enrolar y deja de servir al regenerarlo o al eliminar el equipo (P-3). |
| RN-4 | Un identificador de hardware solo puede pertenecer a un equipo no eliminado. Un equipo eliminado libera su identificador: ese hardware se puede volver a registrar como equipo nuevo. |
| RN-5 | Un equipo eliminado no puede modificarse, regenerar su código ni volver a activarse (P-4). |
| RN-6 | Un equipo eliminado no recibe inventarios: su API key se invalida en la misma transacción que la eliminación. |
| RN-7 | La cantidad de registros del historial es la de inventarios conservados; la purga de 90 días la reduce (SPEC-0002 RF-17). |

Las violaciones de RN-1, RN-2, RN-2b y RN-5 devuelven `422`; RN-4 en el enrolamiento devuelve `409` (RF-10).

## 7. Permisos

Categoría "Inventario", declarados en un `IPermissionDefinitionProvider` del módulo. Todos otorgables (P-11).

| Permiso | Concede | Endpoints / pantallas |
|---|---|---|
| `devices.read` | Consultar el listado de equipos y los datos de un equipo (nombre, tipo, colaborador asociado, estado, fechas, cantidad de registros) | Listado; `GET /devices`, `GET /devices/{id}` |
| `devices.create` | Crear equipos y recibir su código | Botón "Nuevo equipo"; `POST /devices` |
| `devices.update` | Modificar el nombre, el tipo y el correo; **regenerar el código** | Editar, Regenerar código; `PUT /devices/{id}`, `POST /devices/{id}/enrollment-code` |
| `devices.delete` | Eliminar equipos | Eliminar; `DELETE /devices/{id}` |
| `devices.reports.read` | Consultar el historial y los informes, que contienen cuentas locales, IPs y ubicación | Ver registros, Ver último informe; `GET /devices/{id}/reports…` |

La pantalla de historial e informe exige `devices.read` **y** `devices.reports.read`. Las demás acciones exigen
`devices.read` además del permiso propio, porque se ejecutan desde el listado (como en SPEC-0001).

Ver el nombre del colaborador asociado (RF-4b) no exige `users.read`: solo se muestra el nombre del usuario cuyo correo
ya figura en el equipo, nunca el listado de usuarios ni otros datos suyos.

## 8. Comportamiento esperado

### 8.1 Alta de un equipo y enrolamiento

```text
Administrador ──▶ Inventario → "Nuevo equipo" → nombre + tipo + correo (opcional)
API           ──▶ valida RN-1/RN-2/RN-2b → crea el equipo (Pendiente) → genera el código, guarda su hash
                  [auditar device.created]
              ◀── 201 { id, …, enrollmentCode }      (Cache-Control: no-store)
SPA           ──▶ diálogo "Código del equipo" (solo esta vez) · Copiar · aviso de que no se podrá volver a ver
Administrador ──▶ entrega el código al instalador por un canal interno
Instalador    ──▶ itmas-agent install → acepta términos → URL → código (oculto)   [agente sin cambios]
Agente        ──▶ POST /api/v1/device-enrollments   Authorization: Enrollment <código>
API           ──▶ busca el equipo por el hash del código (Pendiente o Activo, código vigente)
                  ├─ no existe / consumido / eliminado ──▶ 401
                  ├─ hardwareId de otro equipo no eliminado ──▶ 409 [auditar device.enrollment.rejected]
                  └─ ok ──▶ vincula hardwareId, emite la API key, consume el código, estado Activo
                           [auditar device.enrolled o device.relinked]
              ◀── 201 { deviceId, apiKey }
```

### 8.2 Eliminación

```text
Administrador ──▶ Eliminar → confirmación ("El agente del equipo dejará de enviar datos. Esta acción no se puede deshacer.")
API           ──▶ estado Eliminado, borra el hash del código y el de la API key, libera el hardwareId
                  [auditar device.deleted]
Agente        ──▶ siguiente envío → 401 → deja de enviar y lo vuelve a intentar cada 24 h (SPEC-0002 RF-10)
```

### 8.3 Mensajes y estados de la interfaz

| Situación | Comportamiento |
|---|---|
| Listado vacío (sin equipos) | "Todavía no hay equipos registrados." y, con `devices.create`, el botón "Nuevo equipo". |
| Búsqueda sin resultados | "Ningún equipo coincide con la búsqueda." |
| Equipo sin registros | "Sin registros" en la columna; "Ver último informe" desactivado; historial con "Este equipo todavía no ha enviado inventario." |
| Nombre duplicado | Error en el campo: "Ya existe un equipo con este nombre." |
| Edición concurrente (`412`) | "Otro usuario modificó este equipo. Recarga los datos e inténtalo de nuevo." |
| Regenerar código | Confirmación: "El código anterior dejará de servir. Si el agente ya está instalado, seguirá funcionando hasta que se reinstale." y diálogo con el código nuevo. |
| Error de carga | Mensaje de error estándar del interceptor y botón "Reintentar". |

## 9. Criterios de aceptación

- [ ] **CA-1** Un usuario con `devices.create` crea un equipo con nombre, tipo y sin correo; aparece en el listado como
  Pendiente, con su tipo, "Sin usuario asociado", "Sin registros" y 0 registros. Sin tipo o con un tipo distinto de
  `workstation`/`server`, el alta devuelve `400` (contrato; ver ajustes en §17).
- [ ] **CA-2** La respuesta de alta incluye el código una sola vez; ningún otro endpoint lo devuelve y en la base de
  datos solo está su hash.
- [ ] **CA-3** Un agente que se enrola con el código de un equipo Pendiente lo deja Activo y vinculado a su hardware;
  sus inventarios aparecen en el historial de ese equipo.
- [ ] **CA-4** Un código ya consumido, regenerado, incorrecto o de un equipo eliminado devuelve `401` y no crea ni
  modifica ningún equipo.
- [ ] **CA-5** Tras regenerar el código y reinstalar el agente, la API key anterior devuelve `401` y la nueva funciona.
- [ ] **CA-6** Un enrolamiento cuyo identificador de hardware pertenece a otro equipo no eliminado devuelve `409`.
- [ ] **CA-7** Tras eliminar un equipo, su agente recibe `401` al enviar el siguiente inventario y su código ya no
  enrola; su historial sigue consultable con el filtro "Eliminados".
- [ ] **CA-8** El nombre, el tipo y el correo se pueden modificar; un nombre duplicado (sin distinguir mayúsculas) o un correo
  inválido devuelven `422`; un `If-Match` desactualizado devuelve `412`.
- [ ] **CA-9** El listado muestra, por equipo, nombre, usuario asociado (o "Sin usuario asociado"), fecha del último
  registro y cantidad de registros, que coinciden con los inventarios almacenados.
- [ ] **CA-10** La búsqueda por nombre del equipo, *hostname*, correo o nombre del colaborador, los filtros por estado y
  por tipo, el orden y la paginación funcionan.
- [ ] **CA-10b** Si el correo del propietario coincide (sin distinguir mayúsculas) con el de un usuario de ITMas, el
  listado, el historial y el informe muestran su nombre y su correo; si no coincide, solo el correo. Si el usuario
  cambia de nombre, o inicia sesión por primera vez después de registrar el equipo, la siguiente consulta lo refleja.
- [ ] **CA-11** "Ver registros" muestra el historial del equipo, del más reciente al más antiguo, y cada fila abre su informe.
- [ ] **CA-12** "Ver último informe" muestra todas las secciones de RF-17 con los datos del último registro, marca los
  datos no disponibles y señala la diferencia entre la IP pública declarada y la de conexión.
- [ ] **CA-13** Un dato del equipo con marcado HTML se muestra como texto literal.
- [ ] **CA-14** Cada endpoint de §12.1 devuelve `401` sin sesión y `403` sin su permiso; con `devices.read` pero sin
  `devices.reports.read` no se puede consultar el historial ni los informes.
- [ ] **CA-15** Crear, modificar, regenerar el código, eliminar, enrolar y rechazar un enrolamiento por `409` generan
  su evento de auditoría, sin el código ni la API key.
- [ ] **CA-16** Tras la migración, los dispositivos enrolados con el código general aparecen como equipos Activos, con
  su historial y con el tipo asignado según P-15, y siguen enviando inventario sin reinstalar el agente.
- [ ] **CA-17** El código general ya no enrola ningún equipo y su variable no existe en la configuración.
- [ ] **CA-18** Las pantallas funcionan en tema claro y oscuro, con teclado y en pantalla estrecha.

## 10. Análisis arquitectónico

### 10.1 Modelo: el equipo es el `Device` existente

| Opción | Valoración |
|---|---|
| **Ampliar `Device`** con nombre, correo, estado y código | **Propuesta.** Un equipo y su dispositivo enrolado son la misma cosa en esta etapa (1:1). Un solo agregado mantiene las reglas RN-3 a RN-6 en una transacción, y el inventario recibido ya cuelga de `Device`. |
| Nueva entidad `Asset` (equipo) vinculada 1:1 a `Device` | Separa "lo que registra una persona" de "lo que declara el agente", pero duplica estados y obliga a sincronizar dos agregados para cada regla. Tendría sentido si un equipo pudiera cambiar de dispositivo o existir sin agente; la definición no lo pide. |

`Device` (Domain) pasa a tener:
- `Name`, `Type` (`Workstation | Server`), `OwnerEmail?`, `Status` (`Pending | Active | Deleted`),
  `EnrollmentCodeHash?`, `EnrollmentCodeIssuedAt?`, `CreatedAt`, `DeletedAt?`.
- El nombre del colaborador **no** forma parte de `Device`: es un dato de lectura que resuelven las consultas (§10.4).
- `HardwareId?`, `ApiKeyHash?` y los datos de enrolamiento pasan a ser anulables, porque un equipo Pendiente todavía no
  los tiene.
- Métodos: `Register(name, type, ownerEmail, codeHash, now)`, `Update(name, type, ownerEmail)`, `IssueEnrollmentCode(codeHash, now)`,
  `Enroll(hardwareId, apiKeyHash, data, now)` (consume el código, Pendiente/Activo → Activo), `Delete(now)`.
  `ApplyReport` no cambia.

### 10.2 Autenticación de dispositivos: cambio de ADR-0009

- **Nuevo ADR-0012** "Códigos de enrolamiento por equipo", que **sustituye el punto 1 de ADR-0009** (código general).
  El resto de ADR-0009 sigue vigente: API key por dispositivo, esquemas `Enrollment` y `Device`, políticas, CSRF,
  límites de frecuencia y auditoría.
- Código: 20 caracteres del alfabeto Base32 de Crockford (100 bits de un CSPRNG), presentado en grupos
  `XXXXX-XXXXX-XXXXX-XXXXX` para facilitar copiarlo y dictarlo. El servidor ignora guiones, espacios y mayúsculas al
  validarlo. 100 bits, con el límite de 10 intentos por minuto y por IP, hacen inviable la fuerza bruta.
- Se guarda su SHA-256 (índice único) y la búsqueda se hace por hash, igual que la API key. No se necesita una
  comparación en tiempo constante adicional: la búsqueda por hash no filtra información del código.
- `EnrollmentCodeAuthenticationHandler` deja de leer la configuración: calcula el hash, busca el equipo con un puerto
  de `Application` (`IDeviceRepository.FindByEnrollmentCodeHashAsync`) y emite un principal con el id del equipo. El
  caso de uso `EnrollDevice` recibe ese id en lugar de buscar solo por hardware.
- El endpoint de enrolamiento deja de responder `404` cuando no hay código configurado: siempre está habilitado y sin
  código válido responde `401`.
- `DeviceAuthenticator` rechaza también los dispositivos que no estén Activos (defensa en profundidad además de RN-6).

### 10.3 El agente no cambia

El agente envía el código que el usuario introduce, sin validar su formato más allá de no estar vacío
(`agent/src/Itmas3.Agent/Cli/InstallCommand.cs`). Por eso funciona igual con el código del equipo y no requiere una
versión nueva. Solo cambia la documentación de instalación (de dónde sale el código).

Limitación conocida: el agente 1.1.0 traduce `401`, `404`, `422` y `429` a mensajes propios
(`agent/src/Itmas3.Agent/Api/ItmasApiClient.cs`), pero el `409` de RF-10 lo mostrará como "El servidor respondió 409.".
La guía de instalación explicará qué significa. Un mensaje específico, y cambiar "código de autorización" por "código
del equipo", serían un cambio menor del agente (P-12).

### 10.4 Consultas y rendimiento

- **Listado:** una consulta paginada sobre `devices` con un `LEFT JOIN` a un agregado de `inventory_reports` por
  `device_id` (`count(*)`), apoyado en el índice existente `(device_id, collected_at desc)`. La fecha del último
  registro ya está en `devices.last_collected_at`. Con 800 equipos y 72 000 informes, se resuelve en milisegundos.
  Alternativa descartada: un contador desnormalizado en `devices`, que la purga obligaría a recalcular.
- **Colaborador asociado (RF-4b):** las consultas del listado, del equipo y del informe hacen un `LEFT JOIN` lateral a
  `users` por `lower(email) = devices.owner_email` (índice existente `lower(email)` de SPEC-0001) y toman el usuario
  con el inicio de sesión más reciente. Es una lectura entre módulos dentro de las consultas de `Infrastructure`: el
  dominio `Inventory` no depende de `Authorization`. Alternativa descartada: copiar el nombre en `devices` al guardar,
  porque quedaría desactualizado al cambiar el nombre en Entra ID o al darse de alta el usuario después.
- **Búsqueda:** `ILIKE` parametrizado sobre `name`, `hostname`, `owner_email` y el nombre del colaborador mediante LINQ
  (EF Core), sin SQL explícito (constitución §5.2). Con este volumen no hace falta un índice de trigramas.
- **Historial:** paginado por `(device_id, collected_at desc)`; no lee el `payload`.
- **Informe:** lee un solo `payload jsonb` y lo deserializa en el modelo del contrato `v1`, que se devuelve como DTO
  propio de lectura (`Api/Contracts/Inventory/`), no como entidad (§7.2).

### 10.5 Encaje por capa

| Capa | Cambios |
|---|---|
| Domain | `Device` ampliado (§10.1), `DeviceStatus`, `DeviceType`, reglas RN-1 a RN-6. |
| Application | Casos de uso `RegisterDevice`, `UpdateDevice`, `RegenerateEnrollmentCode`, `DeleteDevice`; consultas `ListDevices`, `GetDevice`, `ListDeviceReports`, `GetDeviceReport`; `EnrollDevice` adaptado; `DeviceSecrets.NewEnrollmentCode()`; catálogo de permisos del módulo; acciones de auditoría nuevas. |
| Infrastructure | Repositorio y consultas EF Core; configuración de `Device` actualizada; migración (§13). |
| Api | `DevicesController` y `DeviceReportsController` con `[RequirePermission]`; contratos; `EnrollmentCodeAuthenticationHandler` adaptado; `ETag`/`If-Match`; `Cache-Control: no-store` en las respuestas con código. Se retira `InventoryOptions.EnrollmentCode`. |
| Frontend | Nueva feature `features/inventory/` (§11). |

### 10.6 Decisiones que requieren ADR

- **ADR-0012** Códigos de enrolamiento por equipo: formato, entropía, un solo uso, regeneración, almacenamiento,
  retirada del código general y transición de los dispositivos existentes. Actualiza ADR-0009 (nota en su punto 1 y en
  "Seguimiento").

### 10.7 Contradicciones con la constitución y documentos vigentes

| # | Regla | Conflicto | Resolución propuesta |
|---|---|---|---|
| 1 | ADR-0009 §1 (constitución §1.3: los ADR prevalecen sobre las specs) | La definición dice que el agente "usa el código para interactuar con la API". Usarlo como credencial permanente contradice ADR-0009, que limita el código al enrolamiento y emite una API key por dispositivo, de la que solo se guarda el hash. | **Sin enmienda de la constitución.** Se mantiene el intercambio código → API key; solo cambia el origen del código (§2). ADR-0012 sustituye el punto 1 de ADR-0009. |
| 2 | §3.2 (el navegador no recibe tokens), §4.4 (sin secretos en el frontend), §3.1 | El código del equipo es una credencial de enrolamiento y llega al navegador para que el administrador lo entregue. | **Sin conflicto, con controles.** §3.2 se refiere a los tokens de la sesión de la persona y §4.4 al código y la configuración de la SPA, no a un dato que el usuario solicita. Controles: se muestra una sola vez, `Cache-Control: no-store`, no se guarda en el estado de la SPA ni en almacenamiento del navegador, no se registra en logs y es de un solo uso. `security` debe confirmarlo. |
| 3 | §11.4 (no inventar requisitos) | La definición no precisaba el ciclo de vida del código, el destino de los dispositivos ya enrolados, la unicidad del nombre, el tipo de equipo ni la exportación del informe. | Resuelto por el solicitante en dos rondas de preguntas (§18.1 y §18.2). |
| 4 | §3.4 (sin privilegios por estar autenticado) | La definición no dice quién puede gestionar ni consultar el inventario. | Permisos `devices.*` (§7) concedidos por políticas. Ningún acceso implícito. |
| 5 | §5.4 (migraciones revisadas, no modificar las aplicadas) | `devices.hardware_id` y `devices.api_key_hash` son hoy obligatorios y únicos, y hay filas en producción. | Migración nueva que los hace anulables, sustituye los índices únicos por índices parciales (equipos no eliminados) y rellena las filas existentes (§13). Se revisa el SQL generado. |
| 6 | §10.1 | El código general aparece en README, SECURITY (procedimiento de revocación), DEVELOPMENT, `.env.example`, `init-env.sh`, `docker-compose.yml` y la guía del agente. | Se actualizan en el mismo cambio. El procedimiento SQL de revocación se sustituye por "Eliminar equipo". |

## 11. Cambios de frontend

Feature `features/inventory/`, con carga diferida, y una entrada de navegación "Inventario" (icono `devices`) en un
grupo nuevo "Inventario", visible con `devices.read` (solo UX).

| Ruta | Pantalla | Permiso |
|---|---|---|
| `/inventory` | Listado de equipos (RF-12, RF-13) | `devices.read` |
| `/inventory/:id/reports` | Historial de registros (RF-15) | `devices.read` + `devices.reports.read` |
| `/inventory/:id/reports/latest` | Informe del último registro (RF-17) | `devices.read` + `devices.reports.read` |
| `/inventory/:id/reports/:reportId` | Informe de un registro del historial | `devices.read` + `devices.reports.read` |

Componentes de Angular Material y CDK, sin dependencias nuevas:
- Listado: `mat-table`, `mat-sort`, `mat-paginator`, `mat-form-field` de búsqueda, `mat-chip-listbox` o `mat-select`
  para el estado, `mat-icon-button` con `matTooltip` y `aria-label` para las acciones, `mat-menu` para las acciones
  secundarias en pantallas estrechas.
- Alta y edición: `mat-dialog` con formulario reactivo: nombre, tipo (`mat-button-toggle-group` o `mat-select` con
  "Estación de trabajo" y "Servidor") y correo, con validación.
- Colaborador asociado: nombre y, debajo, el correo en texto secundario; solo el correo si no hay coincidencia.
- Código: `mat-dialog` con el código en tipografía monoespaciada del tema, botón de copiar (`@angular/cdk/clipboard`)
  y aviso de que no se volverá a mostrar.
- Confirmaciones: `mat-dialog` para eliminar y regenerar.
- Informe: `mat-card` por sección con un índice de secciones (`mat-chip` o enlaces de anclaje); `mat-table` o
  `mat-list` para discos, volúmenes, interfaces y usuarios; `mat-progress-bar` para el uso de cada volumen;
  `mat-form-field` para filtrar el software.
- Estados de carga (`mat-progress-bar`), vacío y error en todas las pantallas (§8.3).
- `shared/`: pipe de tamaños (bytes → GB/TB) y pipe de etiquetas de estados normalizados, reutilizables.

## 12. Cambios de backend

### 12.1 Endpoints

Todos bajo `/api/v1`, con Problem Details en los errores.

| Método | Ruta | Autorización | Request | Response | Errores |
|---|---|---|---|---|---|
| GET | `/devices?search=&status=&type=&sort=&page=&pageSize=` | `devices.read` | — | `200 { items: [{ id, name, type, hostname, ownerEmail, ownerName, status, lastCollectedAt, reportCount }], total }` | 400, 401, 403 |
| POST | `/devices` | `devices.create` | `{ name, type, ownerEmail? }` | `201 { id, name, type, ownerEmail, ownerName, status, createdAt, enrollmentCode }` + `ETag` | 400, 401, 403, 422 |
| GET | `/devices/{id}` | `devices.read` | — | `200 { id, name, type, ownerEmail, ownerName, status, hostname, osName, osVersion, createdAt, enrolledAt, lastSeenAt, lastCollectedAt, reportCount, enrollmentCodeIssuedAt }` + `ETag` | 401, 403, 404 |
| PUT | `/devices/{id}` | `devices.update` | `{ name, type, ownerEmail? }` + `If-Match` | `200` (como GET) + `ETag` | 400, 401, 403, 404, 412, 422, 428 |
| POST | `/devices/{id}/enrollment-code` | `devices.update` | — | `201 { enrollmentCode, issuedAt }` | 401, 403, 404, 422 (eliminado) |
| DELETE | `/devices/{id}` | `devices.delete` | — | `204` (idempotente si ya está eliminado) | 401, 403, 404 |
| GET | `/devices/{id}/reports?page=&pageSize=` | `devices.reports.read` | — | `200 { items: [{ id, collectedAt, receivedAt, connectionIp, publicIp, countryCode, region, city, agentVersion, unavailableCount }], total }` | 401, 403, 404 |
| GET | `/devices/{id}/reports/latest` | `devices.reports.read` | — | `200` informe (abajo) | 401, 403, 404 (sin registros) |
| GET | `/devices/{id}/reports/{reportId}` | `devices.reports.read` | — | `200` informe | 401, 403, 404 |
| POST | `/device-enrollments` (existente) | `DeviceEnrollment` | sin cambios | sin cambios | 400, 401, **409**, 422, 429 (desaparece el 404 de enrolamiento desactivado) |

- Informe: `{ id, device: { id, name, type, status, ownerEmail, ownerName }, collectedAt, receivedAt, connectionIp,
  isLatest, inventory }`, donde `inventory` sigue el contrato `v1` de SPEC-0002 §12.2 (mismas propiedades, como DTO de lectura).
- `type` es `workstation` o `server`. `ownerName` es `null` si no hay correo o si no coincide con ningún usuario (RF-4b).
- Las respuestas que incluyen `enrollmentCode` llevan `Cache-Control: no-store`.
- `pageSize` máximo 100. `sort` admite `name`, `owner` y `lastCollectedAt`, con `-` para descendente.
- El contrato `v1` de envío de inventario y `POST /inventory-reports` no cambian.

### 12.2 Auditoría

Acciones nuevas en `audit_events` (tipo de recurso `device`): `device.created`, `device.updated`,
`device.enrollment-code.regenerated`, `device.deleted`, `device.enrollment.rejected` (solo el `409` de RF-10).
Se mantienen `device.enrolled` y `device.relinked`, ahora con el id del equipo. Los valores anterior y posterior
incluyen nombre, tipo, correo y estado, **nunca** el código ni la API key ni sus hashes.

### 12.3 Configuración

Se elimina `INVENTORY_ENROLLMENT_CODE` (`Inventory__EnrollmentCode`) de `.env.example`, `init-env.sh`,
`docker-compose.yml`, README y DEVELOPMENT. No hay variables nuevas.

## 13. Cambios de base de datos (preliminar; los concreta `database`)

Esquema `app`, migración `AddDeviceRegistry`:

| Cambio | Detalle |
|---|---|
| Columnas nuevas en `devices` | `name varchar(100) not null`, `device_type varchar(16) not null` (`workstation`/`server`), `owner_email varchar(254) null`, `status varchar(16) not null`, `enrollment_code_hash char(64) null`, `enrollment_code_issued_at timestamptz null`, `created_at timestamptz not null`, `deleted_at timestamptz null`; token de concurrencia `xmin`. |
| Columnas que pasan a anulables | `hardware_id`, `api_key_hash`, `enrolled_at`, `terms_version`, `terms_accepted_at`, `hostname`, `os_family`, `agent_version`. |
| Índices | Único parcial `lower(name)` donde `status <> 'deleted'`; único parcial `hardware_id` donde `status <> 'deleted'` (sustituye al único actual); único `api_key_hash` y único `enrollment_code_hash` (admiten nulos); índice `lower(owner_email)`; índice `status`. |
| Datos existentes (P-6, P-15) | Cada fila pasa a `status = 'active'`, `created_at = enrolled_at`, `name = hostname` y `device_type` según P-15. Si dos dispositivos comparten *hostname*, se añade un sufijo ` (2)`, ` (3)`… en orden de enrolamiento. Se revisa el SQL generado (constitución §5.4). |
| Reversibilidad | El `Down` solo es posible si no hay equipos Pendientes ni Eliminados (no tienen `hardware_id`). Se documenta; en un entorno compartido se corrige con otra migración. |

No hay cambios de roles: la API ya tiene `SELECT`, `INSERT`, `UPDATE` y `DELETE` sobre las tablas de `app` (privilegios
por defecto de `database/init/01-create-roles.sh`). La eliminación de equipos es lógica (`UPDATE`).

## 14. Consideraciones de seguridad

### 14.1 Amenazas (STRIDE)

| Amenaza | Mitigación |
|---|---|
| **S** Filtración de un código de equipo | Un solo uso (RN-3): un código consumido no sirve. Un código no usado se invalida regenerándolo o eliminando el equipo. 100 bits de entropía y 10 intentos por minuto por IP. Riesgo mucho menor que el código general actual, que enrola cualquier equipo. |
| **S** Un usuario con `devices.update` regenera el código de un equipo Activo y enrola un equipo falso en su lugar | Requiere el permiso, queda auditado (`device.enrollment-code.regenerated`, `device.relinked` con IP de origen y *hostname*) y el equipo legítimo deja de poder enviar (`401`), lo que se nota. Se puede separar en un permiso propio si el solicitante lo prefiere (P-11). |
| **S** Robo de la API key de un dispositivo | Sin cambios respecto a SPEC-0002, pero ahora con revocación desde la interfaz: eliminar el equipo (RF-5). |
| **T** Un dispositivo se apropia del registro de otro presentando su identificador de hardware | Necesita un código vigente; si el hardware ya pertenece a otro equipo, `409` y auditoría (RF-10). |
| **R** Cambios en el registro sin rastro | Auditoría de todas las operaciones (§12.2), en la tabla *append-only* de SPEC-0001. |
| **I** El código queda en el navegador (historial, caché, extensiones) | Solo en la respuesta de alta o regeneración, con `no-store`; la SPA no lo guarda fuera del diálogo; un solo uso. |
| **I** Exposición de datos personales: correo del propietario, cuentas locales, IPs, ubicación | Acceso por permisos; el historial y el informe exigen `devices.reports.read`, separado de `devices.read`. Sin datos personales en logs: se registran ids. |
| **I** Descubrir el nombre de usuarios de ITMas probando correos en el alta o la edición de equipos (RF-4b) | Requiere `devices.create` o `devices.update` y cada intento queda auditado. Solo revela el nombre visible, que ya es conocido dentro de la organización. Riesgo bajo, aceptado salvo que `security` indique lo contrario. |
| **I** Enumeración de equipos o de correos | Endpoints protegidos por permisos; el enrolamiento no distingue entre código inexistente, consumido o eliminado (`401`). |
| **T/E** Datos del equipo con contenido malicioso (XSS) | Angular escapa la interpolación; prohibido `innerHTML` y `bypassSecurityTrust*` en la feature; la CSP existente se mantiene (CA-13). |
| **D** Listado o informe costosos | Paginación con máximo 100; el listado no lee `payload`; el informe lee uno solo; *rate limiting* global existente. |

### 14.2 Datos personales

El registro añade el **correo del propietario**, que un administrador introduce, y muestra el **nombre del
colaborador** cuando es usuario de ITMas. Relaciona a una persona con un equipo y con su inventario (cuentas locales,
IP pública, ubicación aproximada). Los términos de SPEC-0002 (Anexo A,
punto 5) ya indican que la información "será accesible solo por el personal autorizado", lo que esta spec materializa
con permisos. Los términos no mencionan la asociación con el correo del propietario: se recomienda que el área legal
valore si hace falta una versión 2 de los términos (P-13). Esta spec no cambia los términos.

### 14.3 Revisión de `security`

Obligatoria para: ADR-0012, el manejo del código en el navegador (§10.7, punto 2), el handler de enrolamiento, la
separación de permisos y la actualización de SECURITY.md y del modelo de amenazas.

## 15. Pruebas

| Criterio | Tipo | Prueba prevista |
|---|---|---|
| CA-1, CA-8 | Unit (dominio) + Integración | Alta, edición, reglas RN-1/RN-2/RN-2b/RN-5, `412` con `If-Match` desactualizado. |
| CA-10b | Integración + Frontend | Correo con y sin usuario coincidente, en mayúsculas distintas, con dos usuarios del mismo correo y tras cambiar el nombre del usuario; componentes con y sin `ownerName`. |
| CA-2 | Integración | El código solo aparece en `POST /devices` y en la regeneración; la fila solo tiene el hash; `Cache-Control: no-store`. |
| CA-3 a CA-7 | Integración (API) | Enrolamiento con código de equipo Pendiente; código consumido, regenerado, incorrecto y de equipo eliminado → `401`; reinstalación con código regenerado; hardware de otro equipo → `409`; eliminación → `401` en el envío. |
| CA-9, CA-10 | Integración + Frontend | Conteo y fecha con varios informes y tras la purga; búsqueda, filtro, orden y paginación; componente del listado con "Sin usuario asociado" y "Sin registros". |
| CA-11, CA-12 | Integración + Frontend | Historial ordenado y paginado; informe con todas las secciones a partir del *fixture* del contrato `v1` compartido con el agente; IP declarada distinta de la de conexión; datos no disponibles. |
| CA-13 | Frontend | Un *hostname* y un nombre de aplicación con `<script>` se muestran como texto. |
| CA-14 | Integración | `401` y `403` en cada endpoint; `devices.read` sin `devices.reports.read` → `403` en `/reports`. Pruebas de convención existentes (catálogo y autorización). |
| CA-15 | Integración | Un evento por operación, sin código ni hashes en `before`/`after`. |
| CA-16 | Integración (migración) | Base de datos con dispositivos de SPEC-0002 (incluidos *hostnames* repetidos y de cada familia de SO) → migración → equipos Activos con nombre único, tipo según P-15 y su API key sigue funcionando. |
| CA-17 | Integración + revisión | La cabecera `Enrollment` con el valor del antiguo código general → `401`; sin referencias a `INVENTORY_ENROLLMENT_CODE` en el repositorio. |
| CA-18 | Verificación documentada | Revisión visual en claro y oscuro, escritorio y móvil, y recorrido con teclado. |
| Agente | Pruebas existentes del agente | `cd agent && dotnet test` sin cambios; prueba de extremo a extremo con el perfil `agent` usando un código de equipo. |

## 16. Plan de implementación (preliminar)

| # | Tarea | Agente | Archivos principales |
|---|---|---|---|
| 1 | ADR-0012; nota en ADR-0009 | architecture + security | `specs/architecture/` |
| 2 | Migración `AddDeviceRegistry` con la transición de datos | database | `Infrastructure/Persistence/**` |
| 3 | Dominio `Device` ampliado y reglas | backend | `Itmas3.Domain/Inventory/**` |
| 4 | Casos de uso, consultas, permisos y auditoría; `EnrollDevice` adaptado | backend | `Itmas3.Application/Inventory/**` |
| 5 | Repositorio y consultas EF Core | backend | `Itmas3.Infrastructure/Inventory/**` |
| 6 | Controllers, contratos, handler de enrolamiento; retirada del código general | backend + security | `Itmas3.Api/**` |
| 7 | Configuración: retirar `INVENTORY_ENROLLMENT_CODE` | architecture | `.env.example`, `scripts/init-env.sh`, `docker-compose.yml` |
| — | **Incremento A**: API verificable sin UI | testing | `backend/tests/**` |
| 8 | Listado, alta, edición, código, eliminación; navegación | frontend | `frontend/src/app/features/inventory/**`, `layout/navigation/**`, `app.routes.ts` |
| 9 | Historial e informe; pipes en `shared/` | frontend | `features/inventory/**`, `shared/**` |
| — | **Incremento B**: interfaz completa | testing | `*.spec.ts` |
| 10 | Revisión de seguridad; documentación (README, ARCHITECTURE, SECURITY, DEVELOPMENT, `agent/README.md`, modelo de amenazas) | security + architecture | docs |
| — | **Cierre**: E2E con el agente; spec → Implemented | testing + security | — |

## 17. Trazabilidad

Implementada el 2026-10-10 (incrementos A y B, revisada por el solicitante). Queda una validación manual del agente, indicada al final.

| Área | Archivos principales |
|---|---|
| Decisiones | [ADR-0012](../architecture/adr-0012-per-device-enrollment-codes.md) (códigos por equipo; sustituye el punto 1 de ADR-0009, que queda anotado) |
| Dominio | `backend/src/Itmas3.Domain/Inventory/Device.cs` (`DeviceStatus`, `DeviceType`, `Register`, `Update`, `IssueEnrollmentCode`, `Enroll`, `Delete`), `InventoryReport.cs` (`AgentVersion`, `UnavailableCount`) |
| Aplicación | `Itmas3.Application/Inventory/ManageDevices/` (registro, edición, regeneración, eliminación), `Queries/IInventoryQueries.cs`, `EnrollDevice/` (código por equipo, `409` auditado), `AuthenticateDevice/` (`EnrollmentCodeAuthenticator`; solo equipos activos), `DeviceSecrets.cs` (códigos Crockford), `InventoryPermissions.cs`, `InventoryAuditActions.cs`; `Common/Exceptions/CredentialRejectedException.cs` |
| Infraestructura | `Itmas3.Infrastructure/Inventory/` (`InventoryQueries`, repositorio, unidad de trabajo con actor del usuario y conflictos `409`), `Persistence/Configurations/DeviceConfiguration.cs`, migración `AddDeviceRegistry` |
| API | `Controllers/Inventory/DevicesController.cs`, `DeviceReportsController.cs`, `DeviceEnrollmentsController.cs`; `Contracts/Inventory/DeviceContracts.cs`; `Inventory/EnrollmentCodeAuthenticationHandler.cs`; `Controllers/EntityTags.cs`; `GlobalExceptionHandler` (`401` sin detalle) |
| Frontend | `frontend/src/app/features/inventory/` (`devices/`, `history/`, `report/`, `data/`), `shared/pipes/bytes-pipe.ts`, `auth/guards/permission-guard.ts` (lista de permisos), `layout/navigation/navigation-items.ts`, `app.routes.ts`; auditoría: `features/authorization/data/authorization.models.ts` (etiquetas `device.*`) y filtro "Equipo" en `audit/audit-page.html` |
| Configuración | Retirados `INVENTORY_ENROLLMENT_CODE` / `Inventory__EnrollmentCode` (`.env.example`, `scripts/init-env.sh`, `docker-compose.yml`, `InventoryOptions`) |
| Documentación | README, ARCHITECTURE, SECURITY, DEVELOPMENT, `agent/README.md`, `specs/security/threat-model-baseline.md`, `specs/database/README.md` |

| Criterio | Prueba o verificación |
|---|---|
| CA-1, CA-2 | `DeviceRegistryTests.CA01_CA02_*`, `CA01_type_is_required_and_closed`; `ManageDevicesHandlerTests.CA02_*`; `DeviceTests.RF01_*`, `RN01_*`, `RN02_*`, `RN02b_*` |
| CA-3 | `DeviceRegistryTests.CA03_*`; `EnrollDeviceHandlerTests.RF07_*`; `InventoryTests.CA04_valid_code_*` |
| CA-4 | `DeviceRegistryTests.CA04_*`; `EnrollDeviceHandlerTests.RF08_*`, `Deleted_devices_cannot_enroll`; `InventoryTests.CA04_invalid_or_missing_code_*` |
| CA-5 | `DeviceRegistryTests.CA05_*`; `EnrollDeviceHandlerTests.RF09_*`; `InventoryTests.CA14_*`; `ManageDevicesHandlerTests.RF03_*` |
| CA-6 | `DeviceRegistryTests.CA06_*`; `EnrollDeviceHandlerTests.RF10_*`, `RN04_*` |
| CA-7 | `DeviceRegistryTests.CA07_*`; `ManageDevicesHandlerTests.RF05_*`; `DeviceTests.RF05_RN06_*`, `RN05_*` |
| CA-8 | `DeviceRegistryTests.CA08_*`; `ManageDevicesHandlerTests.RF04_*`, `RN01_*`; `device-form-dialog.spec.ts` |
| CA-9, CA-10 | `DeviceRegistryTests.CA09_CA10_*`; `devices-page.spec.ts` |
| CA-10b | `DeviceRegistryTests.CA10b_*`; `inventory-labels.spec.ts`, `devices-page.spec.ts` |
| CA-11, CA-12 | `DeviceRegistryTests.CA11_CA12_*`; `device-report-page.spec.ts` |
| CA-13 | `devices-page.spec.ts` y `device-report-page.spec.ts` (`CA-13`); revisión: la feature no usa `innerHTML` ni `bypassSecurityTrust*` |
| CA-14 | `DeviceRegistryTests.CA14_*`, `Device_api_keys_cannot_use_the_registry`; `EndpointAuthorizationConventionTests`, `PermissionCatalogConventionTests`; `permission-guard.spec.ts`, `shell.spec.ts` |
| CA-15 | `DeviceRegistryTests.CA15_*`, `CA06_*`; `ManageDevicesHandlerTests.CA02_*` |
| CA-16 | `DeviceRegistryMigrationTests.CA16_*`; verificación en la pila local: los 5 dispositivos de SPEC-0002 quedaron activos (Linux → Servidor, macOS → Estación de trabajo) y conservan su API key |
| CA-17 | `InventoryTests.CA17_*`; búsqueda: no quedan referencias a `INVENTORY_ENROLLMENT_CODE` fuera de specs y ADR |
| CA-18 | Revisión del solicitante en la pila local (2026-10-10): aprobada |

Totales al cerrar los incrementos A y B: backend 251 pruebas (161 unitarias + 90 de integración), frontend 58, agente sin
cambios.

**Ajustes de implementación** (no cambian el comportamiento aprobado):
- Contrato: el colaborador se devuelve como objeto `owner: { email, name }` (o `null`) en lugar de `ownerEmail` y
  `ownerName` sueltos. Un tipo ausente o desconocido es un error de contrato (`400`), como el resto de enumeraciones.
- `inventory_reports` gana `agent_version` y `unavailable_count` (rellenados por la migración desde el `payload`), para
  que el historial no tenga que leer el `jsonb` de cada registro. `users` gana el índice `lower(email)` para la
  coincidencia del colaborador.
- Concurrencia: `revision` (columna) es el `ETag` que ve el usuario y solo cambia con sus ediciones; `xmin` protege
  además cualquier escritura (por ejemplo, el mismo código usado dos veces a la vez). Así, los envíos diarios del
  agente no invalidan el `ETag` de una edición en curso.
- Navegación: grupo "Inventario" con la entrada "Equipos". `permissionGuard` acepta una lista de permisos (historial e
  informe exigen `devices.read` y `devices.reports.read`).
- El diálogo del código solo se cierra con "Listo", para no perder el código con un clic accidental.
- Auditoría (2026-10-10): la pantalla de SPEC-0001 muestra en español las acciones `device.*` y permite filtrar por el
  tipo de recurso "Equipo". Antes los eventos de equipos aparecían con su código técnico y no se podían filtrar.

**Manual de uso** (constitución §10.4, publicado con SPEC-0004):

| Proceso (`id`) | Acción | Permiso | Pantallas |
|---|---|---|---|
| `registrar-equipo` | Nuevo | `devices.create` | `/inventory` |
| `instalar-agente` | Nuevo (resume `agent/README.md` con el código del equipo) | Código del equipo | — |
| `gestionar-equipo` | Nuevo | `devices.update`, `devices.delete` | `/inventory` |
| `listado-de-equipos` | Nuevo | `devices.read` | `/inventory` |
| `historial-e-informe` | Nuevo | `devices.read`, `devices.reports.read` | `/inventory` |
| `consultar-auditoria` | Incluye los eventos de equipos | `audit.read` | `/admin/audit` |

**Validación manual:**
1. CA-18: revisada y aprobada por el solicitante el 2026-10-10.
2. Pendiente (el solicitante la hará más adelante; la spec se cierra a petición suya): instalar el agente en un equipo con un código generado desde la interfaz, comprobar que aparece como Activo y que,
   al eliminarlo, el agente recibe `401`.

## 18. Decisiones y preguntas abiertas

### 18.1 Primera ronda (2026-10-09)

| # | Tema | Decisión del solicitante |
|---|---|---|
| ADR-0009 | "Código" de la definición | Es la API key del dispositivo. Se mantiene el mecanismo de ADR-0009: el código del equipo solo sirve para obtenerla (§2). |
| §10.7-2 | Código en el navegador | De acuerdo: un solo uso; el administrador lo copia y lo entrega de forma segura a quien instala el agente. |
| §10.7-5 | Registro previo | Antes de usar el agente en un equipo, el equipo se registra en el inventario para poder obtener su API key. |
| P-1 | Código general | Se retira. |
| P-2, P-3 | Visibilidad y uso del código | Solo se ve al crearlo o regenerarlo; un solo uso; para reinstalar el agente se regenera. |
| P-3b | Caducidad | Sin caducidad en la v1 (propuesta aceptada). |
| P-4 | Eliminación | Lógica: estado Eliminado, historial conservado 90 días, sin reactivación. |
| P-5 | Correo del propietario | Libre, con formato válido. **Además**, se busca un usuario de ITMas con ese correo para mostrar el nombre del colaborador junto al correo (RF-4b). |
| P-6 | Dispositivos ya enrolados | Se incorporan como Activos (nombre = *hostname*) sin reinstalar. |
| P-7 | Nombre | Libre y único. |
| P-8, P-9 | Listado e historial | Propuestas aceptadas: columna y filtro de estado; historial con una fila por envío. |
| P-10 | Informe | Solo en pantalla en la v1. |
| P-11 | Permisos | Se separan `devices.read` y `devices.reports.read`; regenerar va en `devices.update`. |
| P-12, P-13 | Agente y términos | Propuestas aceptadas: el agente no cambia; la revisión legal de los términos no bloquea. |
| P-14 | Tipo de equipo | Sí: Estación de trabajo o Servidor (RN-2b). |

### 18.2 Segunda ronda (2026-10-09)

| # | Tema | Decisión del solicitante |
|---|---|---|
| P-15 | Tipo de los equipos ya enrolados | Se deduce del sistema operativo informado: Windows Server y Linux → Servidor; Windows 11 y macOS → Estación de trabajo. Se puede corregir después editando el equipo. |
| P-16 | Nombre del colaborador | Se muestra en el listado, el historial y el informe; la búsqueda del listado incluye el nombre. |

### 18.3 Preguntas abiertas

Ninguna.
