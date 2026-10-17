---
id: SPEC-0006
title: Agenda anual de infraestructura
type: feature
status: Approved
owner: jmartinez
created: 2026-10-17
approved-by: jmartinez (2026-10-17)
agents: [architecture, security, database, backend, frontend, testing]
related: [SPEC-0001, SPEC-0003, SPEC-0004, SPEC-0005, ADR-0002, ADR-0008, ADR-0016]
---

# SPEC-0006 — Agenda anual de infraestructura

> Derivada de la definición funcional aportada por el solicitante (2026-10-17) y de la asesoría sobre prácticas del
> sector del Anexo A. Las propuestas de `architecture` se identifican con **P-n** y las preguntas pendientes con
> **Q-n** (§19). Rondas de respuestas (2026-10-17) en §19.1 a §19.3.
> **Aprobada el 2026-10-17** con las propuestas por defecto de §19.4 y §19.5 (§19.6). Se implementa por incrementos
> (§16), empezando por el ADR-0016.

## 1. Objetivo

1. Tener en ITMas un **plan anual** de las actividades del área de infraestructura (mantenimiento y parches, auditorías
   de seguridad, hardening, pruebas de vulnerabilidad, cambios de claves y secretos y otras), consultable por todo el
   equipo, con responsable, fecha, estado y evidencia de ejecución.
2. **Publicar** las actividades programadas en un **calendario compartido de Outlook**, de modo que el equipo reciba
   invitaciones, actualizaciones, cancelaciones y recordatorios de los eventos que están por suceder.
3. **Ubicar la información con rapidez**: cuatro vistas (lista del año, calendario del año, mes y semana), etiquetas
   libres en cada evento y filtros combinables (por etiqueta, tipo, estado y responsable).

## 2. Contexto

### Definición original (solicitante)

> Crear una agenda para el área de infraestructura. En esta agenda, mi equipo de trabajo debe poder ingresar a ver todas
> las actividades que se deben organizar durante todo el año. Algunas de las actividades que hacemos:
>
> - Mantenimiento de toda la infraestructura: coordinar espacios de trabajo para poder aplicar parches de sistema
>   operativo a todos los equipos.
> - Plan de auditorías de seguridad.
> - Procesos de hardening.
> - Pruebas de vulnerabilidad.
> - Cambios de claves y secretos.
> - Entre otros varios tipos de eventos.
>
> Esto lo quiero hacer en ITMas, pero dándole forma para que se pueda hacer de manera profesional. Me gustaría tener esta
> agenda como una agenda en mi Outlook, donde le pueda avisar a todo el equipo los eventos que están por suceder.

**Ampliación (solicitante, 2026-10-17):**

> Me gustaría que en los eventos se pueda colocar etiquetas, de tal manera que pueda filtrar por etiquetas y sea sencillo
> ubicar la información.

**Ampliación (solicitante, 2026-10-17, vistas):**

> Quiero que la agenda tenga varias formas de visualizar: anual, donde se ven todas las actividades creadas en formato de
> listado; semanal, en formato de calendario; mensual, en formato de calendario; y anual, donde se ven todos los meses en
> formato de calendario.

### Cómo lo hacen otras organizaciones (resumen; detalle y fuentes en el Anexo A)

1. **Separan el sistema de registro del canal de aviso.** El plan (qué, quién, cuándo, resultado) vive en una
   herramienta que guarda estado y evidencia; el calendario de correo es solo el medio por el que la gente se entera.
   Un calendario por sí solo no guarda responsable, evidencia ni estado de cumplimiento.
2. **Catálogo de tipos de actividad** con valores por defecto (duración, anticipación, si requiere ventana). Las tareas
   repetitivas y de bajo riesgo, como los parches mensuales, se tratan como **cambios estándar** preaprobados (ITIL 4).
3. **Calendario de cambios** (*change schedule*) con **detección de conflictos** y **periodos de congelamiento**
   (cierres contables, fin de año) en los que no se programan cambios salvo excepción.
4. **Grupos y ventanas de mantenimiento** acordados con anticipación (NIST SP 800-40 r4), en lugar de parchar "cuando se
   pueda".
5. **Evidencia por ocurrencia** de cada actividad del programa anual (ISO/IEC 27001, cláusula 9.2; PCI DSS; SOC 2), que
   es lo que piden las auditorías.
6. **Cadencias estándar**: parches mensuales, escaneos al menos trimestrales, prueba de penetración anual, auditoría
   interna anual, rotación de secretos y certificados según política (Anexo A).

### Qué cambia en ITMas

ITMas ya tiene identidad (Entra ID), permisos por políticas, auditoría append-only, inventario (SPEC-0003) y auditoría de
accesos SQL (SPEC-0005). Esta spec añade el primer módulo de **planificación** y la primera **integración saliente con
Microsoft 365**.

## 3. Alcance

**Incluye**
- Catálogo de tipos de actividad, sembrado con los tipos del Anexo A y administrable (P-5).
- Eventos puntuales y **series** recurrentes (cada N días, semanas, meses o años) que generan ocurrencias (P-4).
- Ciclo de vida **Borrador → Programada → Completada / Cancelada**, con marca calculada de **Vencida** (P-3).
- Registro de la ejecución: resultado, notas y enlaces de evidencia (P-9).
- **Advertencias** de solapamiento con congelamientos y con otras actividades con impacto (P-8).
- Pantallas: agenda con sus cuatro vistas, detalle y edición, creación de series con vista previa, tipos.
- **Cuatro vistas** de la agenda: lista del año, calendario del año (12 meses), calendario mensual y calendario semanal (P-10, P-20).
- **Publicación en un calendario compartido de Outlook** mediante Microsoft Graph: invitaciones, actualizaciones,
  cancelaciones y recordatorios; activable por configuración (P-1, P-2, P-6, P-7).
- **Etiquetas** libres en cada evento, con sugerencias, y filtro por etiquetas combinable con los demás filtros (P-17, P-18).
- Exportación de la agenda filtrada como archivo `.ics` (P-11).
- Permisos `agenda.*`, auditoría y manual de uso.

**Excluye**
- Sincronización de **Outlook hacia ITMas**: respuestas de asistencia, ediciones o borrados hechos en Outlook (P-14).
- Avisos propios por correo, Teams u otro canal distinto de las invitaciones y recordatorios de Outlook (P-6).
- Aprobación de cambios (CAB) y gestión de cambios completa: ITMas no pasa a ser una herramienta ITSM (Q-7).
- Vincular eventos con los equipos del inventario (SPEC-0003) o planificar campañas de parches por grupo de equipos (Q-5).
- Avisar a las personas usuarias finales (fuera del equipo de infraestructura) de las ventanas que les afectan (Q-5).
- Arrastrar y soltar actividades para reprogramarlas, y una vista de un solo día (Q-18).
- Archivos adjuntos: solo enlaces.
- Administrar etiquetas (renombrar, fusionar, eliminar, darles colores) y enviarlas a Outlook (P-19; Q-13).
- Importar un plan existente (Excel u otro) (Q-12).
- Informes de cumplimiento exportables (PDF, Excel) y validación de la evidencia por una segunda persona (Q-16).
- Registrar actividades ya ejecutadas antes de usar ITMas (histórico): solo se planifican fechas futuras (Q-14).
- Almacenar **secretos, claves, contraseñas o hallazgos** de seguridad: la agenda registra *cuándo* y *quién*, nunca el
  valor (§14).
- Varios calendarios de destino u organizaciones.

## 4. Actores

| Actor | Descripción |
|---|---|
| Planificador | Usuario de ITMas con `agenda.create` y `agenda.update`. Prepara el plan: crea eventos y series y los modifica. |
| Publicador | Usuario con `agenda.publish`. Programa los borradores (lo que dispara las invitaciones) y cancela eventos programados. |
| Responsable | Usuario de ITMas asignado a un evento. Con `agenda.complete` registra la ejecución. |
| Consultor | Integrante del equipo con `agenda.read`. Consulta la agenda. |
| Administrador del catálogo | Usuario con `agenda.types.manage`. Mantiene los tipos de actividad. |
| Equipo (destinatarios) | Personas que reciben las invitaciones en Outlook: responsable, participantes y, opcionalmente, la dirección del equipo. |
| Administrador del tenant de Microsoft 365 | El propio equipo de infraestructura (Q-1). Realiza fuera de ITMas los prerrequisitos de §14.4 (buzón, aplicación, permiso acotado). |

## 5. Requerimientos

### 5.1 Funcionales

**Catálogo de tipos**
- **RF-1** El sistema mantiene un catálogo de **tipos de actividad**. Cada tipo tiene: nombre (obligatorio, único sin
  distinguir mayúsculas), descripción, **duración por defecto** (minutos), **recordatorio por defecto** (minutos antes
  del inicio), si es de **congelamiento** (periodo en el que no deben programarse cambios) y si está activo. El
  catálogo se siembra con los tipos del Anexo A (P-5).
- **RF-2** Un usuario con `agenda.types.manage` crea, modifica y desactiva tipos. Un tipo no se elimina: se desactiva.
  Un tipo inactivo no se puede asignar a eventos nuevos, pero los eventos existentes lo conservan.

**Eventos y series**
- **RF-3** Crear un **evento** con: tipo (obligatorio), título (obligatorio), descripción, inicio y fin (fecha y hora, o
  día completo), **responsable** (obligatorio, usuario activo de ITMas), **participantes** (correos), "notificar al
  equipo" (sí/no; por defecto, sí), **impacto** (Ninguno, Degradación o Indisponibilidad; por defecto Ninguno), **alcance** (texto libre:
  qué sistemas o equipos abarca), **lugar** (texto libre, p. ej. "Remoto"), recordatorio (por defecto, el del tipo) y **etiquetas** (hasta 10,
  RF-23).
  Nace en estado **Borrador**. La respuesta incluye las advertencias de RN-11.
- **RF-4** Crear una **serie**: los datos de RF-3 más una regla de repetición: **cada N** días, semanas, meses o años
  (N de 1 a 99; "mensual" es cada 1 mes, "trimestral" cada 3, "semestral" cada 6, "anual" cada 1 año y "cada 90 días"
  es cada 90 días), ancla para meses y años (un día del mes, o el n-ésimo día de la semana del mes, p. ej. "segundo
  martes") y límite (fecha final o número de ocurrencias, máximo 60). Las ocurrencias parten de la fecha de inicio
  indicada y ninguna es anterior (Q-14). El sistema las genera en Borrador. Antes de confirmar, la interfaz muestra una
  **vista previa** con las fechas que se generarían y sus advertencias (P-4).
- **RF-5** Modificar un evento que no esté Completado ni Cancelado. Si está Programado, los cambios se reflejan en
  Outlook (RF-17).
- **RF-6** Modificar los **campos comunes de una serie** (tipo, título, descripción, responsable, participantes,
  impacto, alcance, lugar, recordatorio, etiquetas). El cambio se aplica a las ocurrencias de la serie que aún no han empezado y
  están en Borrador o Programada; **las fechas no cambian** y las demás ocurrencias no se tocan. Sirve, por ejemplo,
  para reasignar el responsable de todo el año.
- **RF-7** Eliminar un evento en Borrador. Los eventos Programados se cancelan (RF-10); los Completados y Cancelados se
  conservan.
- **RF-8** Estados y transiciones (RN-7):

  | Estado | Significado |
  |---|---|
  | Borrador | Solo existe en ITMas. No se ha avisado a nadie. |
  | Programada | Confirmada. Si Outlook está activo, el evento se ha publicado y el equipo lo ha recibido. |
  | Completada | Ejecutada (o cerrada como no realizada), con su registro de ejecución. |
  | Cancelada | Descartada antes de ejecutarse, con motivo. Es terminal. |

  **Vencida** no es un estado: es una marca calculada para un evento en Borrador o Programada cuyo fin ya pasó sin
  haberse cerrado.

**Programar, cancelar y cerrar**
- **RF-9** **Programar** (`agenda.publish`): Borrador → Programada. Admite varios eventos a la vez (hasta 50), con
  resultado individual por evento. Si Outlook está activo, cada evento programado se publica (RF-16).
- **RF-10** **Cancelar** (`agenda.publish`) un evento Programado, con motivo obligatorio. Si estaba publicado, se cancela
  en Outlook y los invitados reciben el aviso con el motivo.
- **RF-11** **Registrar la ejecución** (`agenda.complete`) de un evento Programado: resultado (Realizada con éxito,
  Realizada parcialmente, Fallida, No realizada), fecha y hora reales (por defecto, el momento del registro), notas y hasta
  10 **enlaces de evidencia** (etiqueta y URL). El evento pasa a Completada. Después solo se pueden modificar las
  notas y los enlaces (RN-9).

**Consulta**
- **RF-12** La agenda ofrece **cuatro vistas** de las actividades, que se cambian con un selector y comparten los mismos
  filtros (P-10):

  | Vista | Formato | Qué muestra |
  |---|---|---|
  | **Lista del año** | Listado | Todas las actividades del año elegido (por defecto, el actual), agrupadas por mes y en orden cronológico, con: fecha y hora, tipo, título, etiquetas, responsable (nombre; "Responsable inactivo" si corresponde), estado (con la marca Vencida), advertencias y estado en Outlook. Encima, el **resumen** (RF-13). |
  | **Año** | Calendario | Los doce meses del año a la vez, como calendarios pequeños. Los días con actividades quedan marcados. Elegir un día abre la semana de ese día y elegir el título de un mes abre el mes. |
  | **Mes** | Calendario | Una cuadrícula del mes, con las semanas de lunes a domingo. Cada día muestra sus actividades con hora, tipo y título, y "+n más" cuando no caben. Las de varios días se dibujan abarcando sus días. |
  | **Semana** | Calendario | Siete columnas (lunes a domingo) con un eje de horas de 00:00 a 24:00. Las actividades se colocan por hora y duración; las de día completo o de varios días van en una franja superior; las simultáneas se colocan en paralelo. Una línea marca la hora actual. |

  En las vistas de calendario cada actividad se dibuja con su **estado** (Borrador, Programada, Completada, Cancelada,
  Vencida) con ícono y texto, nunca solo con color, y abrirla lleva a su detalle. Filtros combinables: **etiquetas**,
  tipo, estado (incluye "Vencidas"), responsable, "Mis actividades" y búsqueda por título o etiqueta (P-18). Si se
  aplican etiquetas o una búsqueda sin haber elegido vista, se muestra la **lista del año**. La vista, el periodo (año,
  mes o semana) y los filtros se guardan en la dirección de la página, para poder guardarla o compartirla. Cada vista
  tiene "Anterior", "Hoy" y "Siguiente" y el título del periodo.
- **RF-12b** Con `agenda.create`, elegir un día vacío en la vista Mes o una hora vacía en la vista Semana abre "Nuevo
  evento" con esa fecha y hora ya rellenas (P-21). Las vistas solo leen: no se reprograma arrastrando (Q-18).
- **RF-13** El **resumen del año**, encabezado de la lista del año, muestra, por mes, la cantidad de eventos por estado y las vencidas, y el total por
  tipo. Respeta los filtros activos: con una etiqueta elegida, muestra en qué meses hay eventos que la tienen.
- **RF-14** El **detalle** de un evento muestra todos sus datos, el registro de ejecución, quién lo creó y modificó y
  cuándo, y el estado de su publicación en Outlook (con enlace para abrir el evento en Outlook si está publicado).

**Publicación en Outlook** (P-1, P-2, P-6, P-7)
- **RF-15** La publicación usa **un calendario compartido** (el de un buzón compartido de Exchange Online) indicado en la
  configuración. Es **opcional**: con `AGENDA_OUTLOOK_ENABLED=false` (valor por defecto) todo lo demás funciona.
- **RF-16** Al programar un evento con Outlook activo, ITMas crea el evento en ese calendario con estos **invitados**:
  el responsable, los participantes y, si "notificar al equipo" está activo, la dirección del equipo (como opcional).
  - Asunto: `[<Tipo>] <Título>`.
  - Cuerpo en **texto plano**: descripción, alcance, impacto y enlace a la actividad en ITMas.
  - Categoría de Outlook: el nombre del tipo.
  - Recordatorio: los minutos del evento.
  - Cada creación lleva un identificador de transacción propio del evento para que un reintento no duplique.
- **RF-17** Modificar un evento Programado actualiza el de Outlook (fechas, título, descripción, lugar, impacto,
  alcance, responsable y participantes). Cancelarlo lo cancela en Outlook con el motivo. Registrar la ejecución no
  modifica Outlook.
- **RF-18** Cada evento muestra su **estado en Outlook**, siempre con ícono y texto:

  | Estado | Cuándo |
  |---|---|
  | No publicado | Borrador, o Outlook no activo. |
  | Pendiente | Programado y aún sin confirmación de Outlook (p. ej. el proceso se interrumpió o un lote se detuvo por límite de peticiones). |
  | Publicado | Outlook confirmó la creación o la última actualización. |
  | Error | La última operación con Outlook falló. Se muestra un código normalizado y la fecha, nunca el detalle crudo. |

- **RF-19** **Sincronizar de nuevo** (`agenda.publish`): reintenta un evento, o todos los eventos Programados futuros que
  estén en Pendiente, Error o No publicado. La operación es idempotente: no crea eventos duplicados. Si el evento de
  Outlook ya no existe (se borró allí), se vuelve a crear (RN-14).
- **RF-20** Un fallo de Outlook **nunca** impide guardar, programar, cancelar ni cerrar un evento en ITMas: el evento
  queda en el estado pedido y su publicación en Error o Pendiente.

**Exportación**
- **RF-21** Exportar a `.ics` (RFC 5545) los eventos **Programados** del rango y filtros elegidos, para quien no esté
  invitado o cuando Outlook no esté configurado (P-11).

**Auditoría**
- **RF-22** Cada operación de escritura queda en la auditoría de SPEC-0001 (§12.2).

**Etiquetas** (P-17, P-18, P-19)
- **RF-23** Cada evento puede tener hasta 10 **etiquetas**: palabras o frases cortas de texto libre que cruzan los
  tipos (por ejemplo "windows", "producción", "iso-27001", "sql-server", "2026-t4"). Se indican al crear el evento o
  la serie y se crean al usarlas por primera vez, sin permiso aparte. Al escribir, el campo **sugiere** las etiquetas
  existentes con la cantidad de eventos que las usan, para reutilizarlas en lugar de duplicarlas.
- **RF-24** Las etiquetas de un evento se editan desde su detalle y desde la lista, **en cualquier estado** (también en
  eventos Completados y Cancelados), con `agenda.update`. Cambiarlas no modifica el `ETag` del evento, no actualiza
  Outlook ni envía avisos.
- **RF-25** **Filtrar por etiquetas**: se eligen una o varias y la lista y el resumen muestran solo los eventos que
  tienen **todas**, combinadas con el resto de filtros. Pulsar una etiqueta en la lista del año o en el detalle la añade al filtro.
- **RF-26** La lista de etiquetas existentes, con su cantidad de eventos, está disponible para sugerir y para filtrar.
  Las etiquetas viajan en el `.ics` como categorías (RF-21) y **no** se envían a Outlook (P-19).

### 5.2 No funcionales

- **RNF-1** Capacidad: hasta 2 000 eventos por año y 20 000 en total; las consultas de una semana o de un mes
  responden en menos de 500 ms y las del año (lista, resumen y calendario) en menos de 1 s.
- **RNF-2** Las pantallas cumplen la constitución §6: Angular Material estándar, temas claro y oscuro, textos en
  español, accesibilidad (etiquetas ARIA en botones de icono, navegación por teclado) y uso en pantallas estrechas. Ningún
  estado se comunica solo con color.
- **RNF-3** Las ediciones concurrentes no se pierden: `ETag`/`If-Match` en eventos y tipos, que responde `412`. Las etiquetas son la excepción: el último cambio gana (RF-24).
- **RNF-4** Zona horaria: la agenda trabaja en **una zona horaria de la organización**, configurable, con `America/Bogota` como valor inicial (Q-3). Se guardan
  la hora local, la zona y el instante UTC. Las ocurrencias de una serie conservan la **hora local** aunque cambie el
  desfase respecto a UTC.
- **RNF-5** Resiliencia: cada llamada a Microsoft Graph tiene un tiempo máximo de 10 s. Ante `429`, se respeta
  `Retry-After` y se detiene el lote: los eventos restantes quedan Pendientes. No hay reintentos automáticos en segundo
  plano en la v1 (P-2).
- **RNF-6** Criterio para pasar a procesamiento en segundo plano (P-2): se propondrá una spec nueva si el p95 de "Programar"
  supera 5 s, si se programan lotes de más de 50 con frecuencia o si el equipo necesita reintentos automáticos. El
  contrato (`outlook.status` en las respuestas) ya admite ese cambio.
- **RNF-7** Todo dato de la agenda se muestra como texto, nunca como HTML, y se envía a Outlook como texto plano.
- **RNF-8** Observabilidad: los logs contienen ids, nunca títulos, correos ni respuestas de Graph. Métrica
  `itmas.agenda.outlook.operations` (medidor `Itmas3.Agenda`) con el resultado de cada operación con Outlook.
- **RNF-9** Las vistas muestran las horas en la **zona horaria de la organización** aunque el navegador esté en otra, e
  indican la zona en el encabezado. La API entrega la hora local de esa zona (RNF-4) y las vistas la muestran tal cual, sin pasar por la zona del navegador.
- **RNF-10** Accesibilidad de los calendarios: cada actividad es enfocable y se abre con el teclado, tiene un nombre
  accesible con título, tipo, estado, fecha y hora, y el contraste sigue el tema claro y oscuro. Lo que FullCalendar no
  resuelva por sí mismo (por ejemplo, recorrer las celdas con flechas) se documenta como limitación (§10.4, prueba
  técnica previa). La **lista del año** es la alternativa completa y accesible a las vistas de calendario.

## 6. Reglas de negocio

| ID | Regla |
|---|---|
| RN-1 | El título es obligatorio, de 1 a 120 caracteres sin caracteres de control. El tipo debe existir y estar activo al crear el evento o cambiárselo. |
| RN-2 | El fin es posterior al inicio y la duración máxima es de 60 días. En los eventos de día completo se informan fechas y el último día es inclusivo. |
| RN-3 | No se puede programar un evento cuyo inicio ya pasó, ni mover al pasado el inicio de uno Programado. |
| RN-4 | El responsable debe ser un usuario **activo** de ITMas al crear el evento o cambiárselo. Si después se desactiva, el evento muestra "Responsable inactivo" y no se reasigna solo. |
| RN-5 | Los participantes son direcciones válidas de hasta 254 caracteres, únicas sin distinguir mayúsculas, hasta 30 por evento, y su dominio debe estar en la lista permitida (`AGENDA_ALLOWED_ATTENDEE_DOMAINS`; si está vacía, solo el dominio del buzón configurado). |
| RN-6 | Series: cada N (1 a 99) días, semanas, meses o años; máximo 60 ocurrencias, todas desde la fecha de inicio indicada. Con días o semanas el intervalo es fijo desde el inicio. Si el día elegido no existe en un mes (30 o 31), la ocurrencia cae el último día del mes. Las fechas no se mueven por fines de semana ni feriados (Q-3). |
| RN-7 | Transiciones permitidas: Borrador → Programada; Borrador → eliminado; Programada → Completada; Programada → Cancelada. Ninguna otra. |
| RN-8 | Cancelar exige un motivo de 1 a 500 caracteres. Completar solo es posible desde el inicio programado del evento (no antes; si se ejecutó antes, se reprograma primero). |
| RN-9 | Un evento Completado solo admite cambios de notas (hasta 4 000 caracteres) y de enlaces de evidencia. Uno Cancelado no admite cambios. Las etiquetas se editan aparte y en cualquier estado (RF-24). |
| RN-10 | Un enlace de evidencia es una URL `http` o `https` de hasta 500 caracteres, sin credenciales incrustadas (`usuario:clave@`), con una etiqueta de hasta 100. Máximo 10 por evento. |
| RN-11 | **Advertencias** (no bloquean, se devuelven y se muestran): (a) un evento que no es de congelamiento se solapa con un congelamiento Programado; (b) un evento con impacto distinto de Ninguno se solapa con otro evento con impacto distinto de Ninguno, ambos en Borrador o Programada. |
| RN-12 | Una serie guarda su regla solo para describirla ("cada mes, el segundo martes"); las ocurrencias existen como filas propias, cada una con su estado, responsable y evidencia (P-4). |
| RN-13 | Los eventos Programados, Completados y Cancelados no se eliminan nunca desde la interfaz; su historial se conserva (Q-9). |
| RN-14 | **ITMas es la fuente de verdad.** Los eventos publicados se administran desde ITMas: lo que se edite directamente en el evento de Outlook se sobrescribe en la siguiente actualización. |
| RN-15 | Etiquetas: hasta 10 por evento; cada una de 1 a 30 caracteres, sin caracteres de control, con los espacios recortados y los repetidos reducidos a uno. Dos etiquetas son la misma si solo difieren en mayúsculas o minúsculas; se conserva la escritura de la primera vez que se usó. Un evento no repite una etiqueta. |
| RN-16 | No se crean eventos ni series con inicio en el pasado, medido en la zona horaria de la organización. Una serie no genera ocurrencias anteriores a su fecha de inicio. El histórico anterior a ITMas queda fuera (Q-14). |

Las violaciones de RN-1 a RN-10, RN-15 y RN-16 devuelven `422`.

## 7. Permisos

Categoría "Agenda", declarados en un `IPermissionDefinitionProvider` del módulo. Todos otorgables. Ningún acceso implícito
por estar autenticado (constitución §3.4).

| Permiso | Concede | Endpoints / pantallas |
|---|---|---|
| `agenda.read` | Consultar la agenda, el resumen, el detalle, los tipos y las etiquetas; exportar `.ics` | Agenda, detalle; `GET /agenda-events…`, `GET /agenda-event-types`, `GET /agenda-tags`, `GET /agenda-events/export.ics` |
| `agenda.create` | Crear eventos y series (siempre en Borrador) | "Nuevo evento", "Nueva serie"; `POST /agenda-events`, `POST /agenda-series` |
| `agenda.update` | Modificar eventos y los campos comunes de una serie; editar etiquetas; eliminar borradores | Editar, Etiquetas; `PUT /agenda-events/{id}`, `PUT /agenda-events/{id}/tags`, `PUT /agenda-series/{id}`, `DELETE /agenda-events/{id}` |
| `agenda.publish` | Programar y cancelar eventos (esto envía invitaciones y cancelaciones) y sincronizar de nuevo con Outlook | Programar, Cancelar, Sincronizar; `POST /agenda-events/schedule`, `…/{id}/cancel`, `…/outlook-sync` |
| `agenda.complete` | Registrar la ejecución y modificar notas y enlaces de evidencia | Registrar ejecución; `PUT /agenda-events/{id}/completion` |
| `agenda.types.manage` | Crear, modificar y desactivar tipos de actividad | Tipos; `POST/PUT /agenda-event-types` |

Todas las acciones exigen además `agenda.read`, porque se ejecutan desde la agenda (como en SPEC-0001 y SPEC-0003). Se
separan `agenda.update` y `agenda.publish` porque programar y cancelar **notifican a otras personas**: quien prepara el
plan no tiene por qué poder enviar invitaciones (P-12).

## 8. Comportamiento esperado

### 8.1 Planificar el año y avisar al equipo

```text
Planificador ──▶ Agenda → "Nueva serie" → tipo, título, responsable, horario y repetición
                 (p. ej. "Parches de SO · mensual · segundo martes · hasta 2026-12-31")
API          ──▶ POST /agenda-series?dryRun=true → fechas propuestas y advertencias (sin guardar)
Planificador ──▶ revisa la vista previa → "Crear serie"
API          ──▶ POST /agenda-series → crea la serie y sus ocurrencias en Borrador   [auditar agenda.series.created]
Publicador   ──▶ marca los borradores a avisar → "Programar"
API          ──▶ POST /agenda-events/schedule { ids }
                 por cada evento: Borrador → Programada (transacción)   [auditar agenda.event.scheduled]
                 Outlook activo: crea el evento con su identificador de transacción → Publicado | Error | Pendiente
             ◀── 200 [{ id, status, warnings, outlook: { status, errorCode? } }]
Equipo       ──▶ recibe la invitación de Outlook (responsable, participantes y, opcional, el equipo) con su recordatorio
```

### 8.2 Ciclo de vida

```text
Borrador ──programar──▶ Programada ──registrar ejecución──▶ Completada
   │                        │
 eliminar               cancelar (motivo)
                            └────────────▶ Cancelada
Vencida = marca calculada (Borrador o Programada con fin pasado)
```

### 8.3 Cuando Outlook falla

```text
Publicador ──▶ Programar → evento Programada (ya guardado)
API        ──▶ Graph responde 503 / 429 / agotó el tiempo / 403
               el evento sigue Programada; Outlook: Error (código `graph_unavailable`) o Pendiente (lote detenido)
           ◀── 200 con el resultado por evento (no es un error HTTP: lo programado quedó programado)
SPA        ──▶ muestra "No se pudo publicar en Outlook" en el evento y el botón "Sincronizar de nuevo"
Publicador ──▶ Sincronizar de nuevo → misma operación con el mismo identificador de transacción → Publicado (sin duplicar)
```

### 8.4 Mensajes y estados de la interfaz

| Situación | Comportamiento |
|---|---|
| Agenda vacía | "Todavía no hay actividades planificadas." y, con `agenda.create`, "Nuevo evento" y "Nueva serie". |
| Periodo sin eventos | "No hay actividades en este periodo." (el calendario se muestra igualmente) |
| Más de 1 000 resultados | "Hay más actividades de las que se pueden mostrar. Acota con filtros." (lista del año y vista Año) |
| Búsqueda sin resultados | "Ninguna actividad coincide con los filtros." con el botón "Limpiar filtros". |
| Etiquetas | Campo con sugerencias: "Escribe una etiqueta y pulsa Intro". Al exceder el máximo: "Máximo 10 etiquetas por actividad." |
| Evento sin etiquetas | "Sin etiquetas" y, con `agenda.update`, la acción "Añadir etiquetas". |
| Advertencia de congelamiento | "Esta actividad se solapa con el congelamiento «{título}»." No impide guardar. |
| Advertencia de solapamiento | "Se solapa con «{título}», que también tiene impacto." No impide guardar. |
| Programar | Confirmación: "Se enviarán invitaciones de Outlook a los invitados de cada actividad." (si Outlook está activo). |
| Cancelar | Diálogo con motivo obligatorio: "Se cancelará la actividad y se avisará a los invitados." |
| Edición de una serie | Confirmación: "El cambio se aplicará a las {n} ocurrencias que aún no han empezado. Las fechas no cambian." |
| Edición concurrente (`412`) | "Otro usuario modificó esta actividad. Recarga los datos e inténtalo de nuevo." |
| Outlook no configurado | Indicador "Outlook no configurado": programar solo cambia el estado. |
| Error de publicación | "No se pudo publicar en Outlook." con el código normalizado y "Sincronizar de nuevo". |
| Descripción | Ayuda en el campo: "No escribas contraseñas, claves ni hallazgos de seguridad: el calendario llega a Outlook." |
| Error de carga | Mensaje estándar del interceptor y botón "Reintentar". |

## 9. Criterios de aceptación

- [ ] **CA-1** El catálogo contiene los tipos sembrados del Anexo A. Con `agenda.types.manage` se crea, modifica y desactiva
  un tipo; un nombre repetido (sin distinguir mayúsculas) devuelve `422`; un tipo inactivo no se puede asignar a un
  evento nuevo y los eventos existentes lo conservan.
- [ ] **CA-2** Un usuario con `agenda.create` crea un evento que queda en Borrador con los datos informados y el
  recordatorio del tipo por defecto. Las violaciones de RN-1 a RN-5 devuelven `422` y un cuerpo con propiedades
  desconocidas, `400`.
- [ ] **CA-3** La vista previa de una serie no guarda nada. Al confirmar se generan las ocurrencias esperadas: "segundo
  martes de cada mes", "cada 90 días", un día 31 que cae el último día de los meses cortos, y la **hora local constante** en un cambio de
  horario. Más de 60 ocurrencias devuelve `422`.
- [ ] **CA-4** Modificar un evento con `If-Match` desactualizado devuelve `412`. Un evento Completado solo admite notas y
  enlaces; uno Cancelado no admite cambios (`422`).
- [ ] **CA-5** Modificar los campos comunes de una serie actualiza solo las ocurrencias futuras en Borrador o Programada,
  sin cambiar fechas y sin tocar las Completadas ni las Canceladas.
- [ ] **CA-6** Eliminar un Borrador funciona. Eliminar un evento Programado, Completado o Cancelado devuelve `422`.
- [ ] **CA-7** Programar varios borradores a la vez pasa a Programada los válidos y devuelve un resultado por evento: el
  de inicio pasado se rechaza (RN-3) sin afectar a los demás. Más de 50 por solicitud devuelve `400`.
- [ ] **CA-8** Cancelar exige motivo, deja el evento en Cancelada y no admite más cambios.
- [ ] **CA-9** Registrar la ejecución guarda resultado, notas y enlaces; rechaza enlaces que no sean `http` o `https` y los
  que lleven credenciales; no se puede registrar antes del inicio programado; después solo permite cambiar notas y
  enlaces.
- [ ] **CA-10** Un evento Programado o en Borrador con fin pasado se muestra como Vencido. Los filtros por tipo, estado
  (incluido "Vencidas"), responsable, "Mis actividades" y título funcionan, y el resumen del año coincide con los datos.
- [ ] **CA-11** Las advertencias de congelamiento y de solapamiento con impacto se devuelven al crear, modificar y en la
  vista previa de una serie, se muestran con ícono y texto y **no** impiden guardar.
- [ ] **CA-12** Un responsable inactivo no se puede asignar (`422`); si se desactiva después, el evento lo muestra como
  "Responsable inactivo".
- [ ] **CA-13** Un participante de un dominio fuera de la lista permitida, repetido, con formato inválido, o más de 30
  participantes, devuelve `422`.
- [ ] **CA-14** Con Outlook activo, programar un evento crea en el calendario configurado un evento con el asunto, el
  cuerpo en texto plano, la categoría, el recordatorio y los invitados de RF-16, y el estado en Outlook pasa a
  Publicado.
- [ ] **CA-15** Modificar un evento Programado actualiza el de Outlook; cancelarlo lo cancela con el motivo.
- [ ] **CA-16** Si Graph falla (`401`, `403`, `429`, `5xx`, agotamiento del tiempo), el evento queda en el estado pedido y su
  estado en Outlook es Error o Pendiente, con un código normalizado y sin detalle crudo. "Sincronizar de nuevo" lo
  publica sin duplicar. Si el evento de Outlook ya no existe, se vuelve a crear.
- [ ] **CA-17** Con Outlook desactivado, toda la funcionalidad opera sin hacer llamadas externas y los eventos muestran "No
  publicado". Al activarlo, "Sincronizar de nuevo" publica los eventos Programados futuros.
- [ ] **CA-18** Con `AGENDA_OUTLOOK_ENABLED=true` y configuración incompleta, la API no arranca y el mensaje no incluye
  valores secretos.
- [ ] **CA-19** El `.ics` exportado es válido (RFC 5545), contiene solo eventos Programados del rango, usa un `UID`
  estable, incluye el tipo y las etiquetas como `CATEGORIES`, escapa saltos de línea, `;` y `,`, y un título con saltos de línea no inyecta propiedades.
- [ ] **CA-20** Cada endpoint de §12.1 devuelve `401` sin sesión y `403` sin su permiso; con `agenda.read` pero sin
  `agenda.create` no se puede crear, y sin `agenda.publish` no se puede programar ni cancelar.
- [ ] **CA-21** Cada operación de escritura genera su evento de auditoría, sin secretos, sin tokens y sin la respuesta cruda de Graph.
- [ ] **CA-22** Un título o una descripción con marcado HTML se muestra como texto literal en la interfaz y se envía como texto
  plano a Outlook.
- [ ] **CA-23** Las pantallas funcionan en tema claro y oscuro, con teclado y en pantalla estrecha, con estados de carga,
  vacío y error.
- [ ] **CA-24** Verificación documentada en el tenant: con el permiso acotado, la aplicación opera sobre el buzón
  configurado y es rechazada (`403`) sobre otro buzón.
- [ ] **CA-25** Un evento admite hasta 10 etiquetas de 1 a 30 caracteres; los espacios se recortan, "Windows" y "windows" son
  la misma etiqueta (se conserva la primera escritura) y una etiqueta repetida en el evento se unifica. Más de 10 o una
  de longitud inválida devuelve `422`. El autocompletado devuelve las etiquetas existentes con su cantidad.
- [ ] **CA-26** Filtrar por una o varias etiquetas devuelve solo los eventos que tienen todas, combinado con tipo, estado,
  responsable y "Mis actividades"; la búsqueda por texto también encuentra por etiqueta. El resumen del año respeta el
  filtro y los calendarios también, sin elegir vista se muestra la lista del año y los filtros quedan en la dirección de la página.
- [ ] **CA-27** Con `agenda.update` se editan las etiquetas de un evento en cualquier estado, también Completado y Cancelado; sin
  ese permiso, `403`. Cambiarlas no modifica el `ETag`, no llama a Outlook y queda auditado.
- [ ] **CA-28** Las ocurrencias de una serie heredan las etiquetas de la serie; la edición de los campos comunes las
  reemplaza en las ocurrencias futuras en Borrador o Programada.
- [ ] **CA-29** Crear un evento o una serie con inicio en el pasado devuelve `422`. Una serie no genera ocurrencias
  anteriores a su fecha de inicio.
- [ ] **CA-30** La **lista del año** muestra todas las actividades del año agrupadas por mes y en orden cronológico, con los
  datos de RF-12, y su resumen coincide con ellas.
- [ ] **CA-31** La vista **Año** muestra los doce meses como calendarios con los días que tienen actividades marcados;
  elegir un día abre su semana y elegir el título de un mes abre el mes. Con más de 1 000 resultados avisa que hay que
  acotar con filtros.
- [ ] **CA-32** La vista **Mes** muestra la cuadrícula con las semanas de lunes a domingo, los días de los meses vecinos
  diferenciados, las actividades de cada día con hora, tipo y título, "+n más" cuando no caben, las actividades de
  varios días abarcando sus días y el día de hoy señalado.
- [ ] **CA-33** La vista **Semana** muestra siete columnas con el eje de horas, las actividades colocadas por hora y
  duración, las simultáneas en paralelo, las de día completo y de varios días en la franja superior, las que cruzan la
  medianoche repartidas entre los dos días y la línea de la hora actual.
- [ ] **CA-34** En las cuatro vistas, los filtros se conservan al cambiar de vista; la vista, el periodo y los filtros quedan
  en la dirección de la página; "Anterior", "Hoy" y "Siguiente" funcionan; el estado se dibuja con ícono y texto; las
  horas están en la zona de la organización; abrir una actividad lleva a su detalle; con `agenda.create`, un día u hora
  vacíos abren "Nuevo evento" con esa fecha; y todo funciona con teclado, en tema claro y oscuro y en pantalla estrecha.

## 10. Análisis arquitectónico

### 10.1 Modelo: serie y ocurrencias materializadas (P-4)

| Opción | Valoración |
|---|---|
| **Ocurrencias materializadas**: la serie genera filas `agenda_events`, una por fecha | **Propuesta.** Cada ocurrencia tiene su propio estado, responsable, evidencia e identificador en Outlook, que es lo que piden las auditorías ("¿se hizo el trimestre 2?"). Las consultas son simples (rango de fechas, estado). Coste: las series tienen un tope (60) y la edición de la serie se limita a campos comunes. |
| Regla de recurrencia (RRULE) guardada y ocurrencias calculadas al consultar | Más compacta y flexible, pero el estado y la evidencia por ocurrencia obligan a crear filas de excepción igualmente, y complica las consultas, el solapamiento y la sincronización con Outlook. |
| Sin series: cada evento se crea a mano | Más simple, pero un plan anual con parches mensuales exige crear a mano decenas de eventos. |

### 10.2 Integración con Outlook (P-1)

ITMas es la **fuente de verdad** y Outlook, el **canal de aviso** (unidireccional ITMas → Outlook, P-14). Opciones evaluadas:

| Opción | A favor | En contra |
|---|---|---|
| **A. Calendario compartido (buzón compartido de Exchange Online) + Microsoft Graph con permisos de aplicación acotados a ese buzón** | **Propuesta.** Invitaciones, recordatorios, actualizaciones y cancelaciones nativas; funciona sin una persona conectada; el equipo ve el calendario en Outlook de escritorio, web y móvil; el permiso se puede limitar a un solo buzón. | Exige acciones de un administrador del tenant (§14.4); hay una credencial de aplicación que proteger; dependencia externa (Graph). |
| B. Calendario de un grupo de Microsoft 365 | Es el modelo natural de un equipo. | Graph no admite permisos de aplicación para crear eventos en calendarios de grupo, solo delegados ([documentación de Graph](https://learn.microsoft.com/en-us/graph/api/group-post-events?view=graph-rest-1.0)): exigiría guardar tokens de personas en el servidor. |
| C. Graph con permisos delegados (token de la persona, vía BFF) | Sin credencial de aplicación. | Los eventos irían al calendario de quien actúa; exigiría guardar *refresh tokens* y no permite sincronizar en segundo plano; choca con ADR-0002 (`SaveTokens=false`). |
| D. Suscripción a un feed iCalendar (`webcal`) | No necesita permisos en Microsoft 365. | Solo lectura y por sondeo: en Outlook web la actualización depende de la programación de Microsoft (horas) y no se configura; no hay invitaciones ni recordatorios propios; la URL secreta hay que protegerla y, en general, Outlook web exige que sea alcanzable desde Internet (Q-1b). |
| E. Invitaciones iCalendar por correo (SMTP, `METHOD:REQUEST`) | No depende de Graph. | Necesita un relay SMTP y una cuenta emisora; actualizar o cancelar de forma fiable es frágil; sin estado de entrega. |
| F. Power Automate / Logic Apps | Poco código. | Queda fuera del repositorio y del flujo SDD; difícil de probar y versionar; credenciales fuera de ITMas. |
| G. Descarga manual de `.ics` | Cero dependencias. | Manual; no avisa ni se actualiza. |

**Recomendación:** A como integración principal y G (RF-21) como contingencia y para quien no esté invitado. Requisitos
de A (los detalla §14.4): un buzón compartido, una aplicación de Entra ID propia (distinta de la del BFF) y el permiso
`Calendars.ReadWrite` de aplicación **acotado a ese buzón** con *RBAC for Applications* de Exchange Online, que sustituye
a las *application access policies* ([documentación de Exchange](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac)).
Un permiso de aplicación de calendarios sin acotar alcanza **todos los buzones del tenant**.

La invitación es el mecanismo de aviso: el evento queda en el calendario de cada invitado y allí dispara su
recordatorio. El calendario compartido, con permiso de lectura para el equipo, es la **vista de conjunto**.

### 10.3 Sincronización en línea, sin cola (P-2)

Siguiendo la preferencia de MVP simple (como en SPEC-0005), no hay cola ni proceso en segundo plano:

1. El caso de uso guarda el cambio en una transacción (estado Programada, `outlook_status = pending`).
2. **Después** de confirmar la transacción, llama a Graph en la misma petición (puerto `IOutlookCalendarGateway`).
3. Guarda el resultado (`synced` o `failed` con un código normalizado) en una segunda escritura.
4. Una interrupción entre 1 y 3 deja el evento en `pending`; "Sincronizar de nuevo" lo resuelve.
5. La creación usa el `transactionId` de Graph (el id del evento) para que un reintento tras agotar el tiempo no cree un
   duplicado. La actualización usa el id guardado; si responde `404`, se vuelve a crear.

El contrato ya devuelve `outlook.status` por evento, de modo que mover la publicación a segundo plano (RNF-6) no
cambiaría la API. El criterio para hacerlo está en RNF-6.

### 10.4 Vistas de calendario con FullCalendar (P-10, P-20)

Las vistas de calendario (RF-12) necesitan cuadrículas de mes, semana y año que Angular Material no incluye:
`mat-calendar` es un selector de fechas, no una agenda de eventos. **El solicitante decidió usar FullCalendar
(2026-10-17).** Opciones consideradas:

| Opción | Valoración |
|---|---|
| **A. FullCalendar**: paquete `fullcalendar` y conector `@fullcalendar/angular`, con los complementos `daygrid`, `timegrid`, `multimonth` e `interaction` | **Decidida.** Cubre las tres vistas de calendario ya hechas: Mes = `dayGridMonth`, Semana = `timeGridWeek`, Año = `multiMonthYear`. Esos complementos no son *premium* ([índice de complementos](https://fullcalendar.io/docs/plugin-index)) y el conector declara soporte de Angular 16 a 22 ([documentación](https://fullcalendar.io/docs/angular)). Coste: una dependencia nueva, cuya apariencia debe someterse al tema Material, y varios puntos por verificar (prueba técnica previa, abajo). |
| B. Componentes propios con CSS grid y `--mat-sys-*` | Sin dependencias y con control total de la accesibilidad y de las pruebas, pero la vista Semana (colocación por hora y actividades simultáneas) es laboriosa. **Plan B** si la prueba técnica previa de A falla. |
| C. angular-calendar u otra biblioteca de menor alcance | No cubre un año de doce meses y su ritmo de mantenimiento hay que comprobarlo. Descartada. |

**Justificación de la dependencia** (constitución §12.5): Angular Material no ofrece un calendario de eventos (§6.2 pide
el componente oficial "siempre que exista uno adecuado"); el paquete `fullcalendar` y los complementos no *premium* son
de licencia MIT ([licencia](https://fullcalendar.io/license)); la versión 7 está publicada y vigente (se confirma la
última al instalarla). No se usan los complementos *premium* (*scheduler*, recursos, líneas de tiempo).

**Reglas de uso**
- **Alcance:** FullCalendar dibuja solo las vistas Mes, Semana y Año. La **lista del año** sigue siendo una tabla de
  Angular Material (`mat-table`), no la vista `listYear`, porque necesita selección múltiple para programar, columnas
  propias (etiquetas, responsable, estado, Outlook, advertencias) y un menú de acciones.
- **Tema (§6.3):** la apariencia de FullCalendar se somete al tema: sus variables CSS se asignan a `--mat-sys-*` en la
  hoja de estilos de la feature (solo mapeo, sin sobrescribir tokens ni componentes de Material), así el tema claro y
  oscuro siguen funcionando. El estado de cada actividad se expresa con clases de roles de color de Material 3 y
  siempre con ícono y texto (`eventContent`); el tipo, con su nombre e ícono, no con un color propio.
- **Solo lectura:** `editable` apagado, sin arrastrar y soltar (Q-18). El complemento `interaction` se usa solo para
  `dateClick` (P-21).
- **Datos:** el calendario pide a `GET /agenda-events` el rango visible, con los mismos filtros que las demás vistas, y
  su estado se sincroniza con la ruta (RF-12).
- **Configuración:** idioma español, semana desde el lunes, eje de 00:00 a 24:00 abierto en las 06:00, indicador de la
  hora actual, "+n más" por día, actividades de varios días abarcando sus días y las de día completo en la franja
  superior.
- **Pantalla estrecha** (menos de 600 px): la vista Semana se presenta como un día por vez (`timeGridDay`) y la vista
  Año con los meses en una columna (`multiMonthMaxColumns` en 1).
- **Carga diferida:** el componente de calendario se carga al elegir una vista de calendario, para que la lista del año
  no pague su tamaño.
- **CSP:** FullCalendar inyecta etiquetas `<style>` y la CSP actual ya permite estilos en línea
  (`style-src 'unsafe-inline'`). Si se endurece con *nonce*, FullCalendar lo toma de `<meta name="csp-nonce">`
  ([documentación](https://fullcalendar.io/docs/content-security-policy)).

**Prueba técnica previa** (tarea 13, antes de construir las vistas). Con la versión elegida se verifica: (1) que
funciona con Angular 22 *zoneless* y con signals, algo que la documentación del conector no menciona; (2) que muestra las
horas en la zona de la organización, con la opción de zona de FullCalendar o entregándole la hora local con una zona
fija (RNF-9); (3) si hace falta el *polyfill* opcional `temporal-polyfill` según los navegadores objetivo; (4) la
accesibilidad con teclado y los nombres accesibles (RNF-10); (5) las pruebas con Vitest y jsdom; (6) el tamaño del
paquete; y (7) el mapeo de variables al tema claro y oscuro. Si algo lo impide, se adopta el plan B (opción B) y se
actualiza esta spec.

### 10.5 Tiempo y zonas horarias (RNF-4)

Se guardan la hora local (`timestamp` sin zona), el identificador IANA de la zona de la organización y el instante UTC
(`timestamptz`). El generador de series trabaja sobre la hora local y calcula el UTC con la zona, de modo que
"09:00" sigue siendo "09:00" tras un cambio de horario. Una hora local inexistente (salto de primavera) se desplaza a la
siguiente hora válida. Al llamar a Graph se envía la hora local con el nombre de zona. Las vistas de calendario muestran esa hora local con la opción de zona de FullCalendar, o entregándole la hora local con una zona fija; se decide en la prueba técnica previa (§10.4).

### 10.6 Encaje por capa

| Capa | Cambios |
|---|---|
| Domain | `Agenda/`: `EventType`, `AgendaEvent`, `EventSeries`, `EventStatus`, `EventImpact`, `CompletionResult`, `RecurrencePattern` y su generador de ocurrencias (puro, sin zona del sistema: recibe la zona), `EvidenceLink`, `TagName` (normalización de etiquetas), reglas RN-1 a RN-15 y detección de advertencias (RN-11). |
| Application | `Agenda/`: casos de uso `CreateEvent`, `CreateSeries` (con *dry run*), `UpdateEvent`, `UpdateSeries`, `DeleteDraftEvent`, `ScheduleEvents`, `CancelEvent`, `CompleteEvent`, `ManageEventTypes`, `SetEventTags`, `SyncWithOutlook`; consultas (`ListEvents`, `GetEvent`, `GetSummary`, `ListTags`, `ExportCalendar`); puerto `IOutlookCalendarGateway` y `IAgendaQueries`; catálogo de permisos y acciones de auditoría. |
| Infrastructure | `Agenda/`: repositorios y consultas EF Core, unidad de trabajo auditada (como `Inventory`), adaptador `GraphOutlookCalendarGateway` (§10.7); configuraciones en `Persistence/Configurations/`; migración (§13). |
| Api | `Controllers/Agenda/` (eventos, series, tipos, exportación), contratos en `Contracts/Agenda/`, `AgendaOptions` con validación al arrancar, `ETag`/`If-Match`, `AgendaModuleSetup`. |
| Frontend | Nueva feature `features/agenda/` (§11). |

### 10.7 Adaptador de Graph y dependencias

- Se propone **no** añadir el SDK de Microsoft Graph (arrastra Kiota y muchas dependencias para cuatro operaciones:
  crear, actualizar, cancelar y consultar un evento). El adaptador usa **MSAL** (`Microsoft.Identity.Client`, ya presente
  como dependencia de `Microsoft.Identity.Web`; `architecture` confirma si requiere una referencia propia en
  `Directory.Packages.props`) para el token de aplicación y un `HttpClient` tipado con tiempo máximo y manejo de
  `429`/`Retry-After`.
- El token de aplicación se adquiere en el servidor con la credencial configurada y se guarda en la caché de MSAL en
  memoria; **nunca** llega al navegador.
- La aplicación de Entra ID de Graph es **distinta** de la del BFF: la del BFF no tiene permisos de aplicación sobre
  Microsoft 365 y así sigue.
- Las pruebas usan un doble de `IOutlookCalendarGateway` y un `HttpMessageHandler` simulado para el contrato HTTP del
  adaptador; ninguna prueba llama a Microsoft (constitución §9.4).

### 10.8 Decisiones que requieren ADR

- **ADR-0016** Integración con Outlook mediante Microsoft Graph con permisos de aplicación acotados a un buzón:
  alternativas de §10.2, credencial, alcance mínimo y acotamiento obligatorio en el tenant, sincronización en línea y
  criterio para pasar a segundo plano, dependencias (MSAL en lugar del SDK). Amplía ADR-0002, que preveía llamar a
  Graph desde el servidor con tokens delegados.
- **ADR-0017** Biblioteca de calendario (FullCalendar) y su sujeción al tema Material: alternativas de §10.4, versión,
  complementos permitidos, regla de mapear sus variables a `--mat-sys-*` (§6.3) y resultado de la prueba técnica previa.

### 10.9 Conformidad con la constitución y documentos vigentes

| # | Regla | Observación | Resolución propuesta |
|---|---|---|---|
| 1 | ADR-0002 | Preveía llamadas a Graph con la caché de tokens de `Microsoft.Identity.Web` (delegados). Esta spec necesita permisos de **aplicación** para sincronizar sin una persona conectada. | **Sin enmienda de la constitución.** ADR-0016 lo amplía. `security` debe confirmarlo. |
| 2 | §3.8 (mínimo privilegio) | `Calendars.ReadWrite` de aplicación alcanza por defecto todos los buzones del tenant. | **Obligatorio** acotarlo a un buzón con RBAC for Applications y **no** conceder el permiso en Entra ID: los permisos se suman y el consentimiento global anularía el límite (§14.4). Se documenta en SECURITY.md y se verifica (CA-24). |
| 3 | §4 (secretos) | Hay una credencial nueva de aplicación. | Variable de entorno `AGENDA_OUTLOOK_CLIENT_SECRET` (o certificado), documentada en `.env.example` y README, solo en el contenedor `api`, nunca en logs ni en la línea de comandos. Se recomienda certificado y una caducidad corta; el vencimiento se añade a esta misma agenda. |
| 4 | §8 y ARCHITECTURE §6 (redes) | La API sale por la red `edge` hacia Entra ID. Graph requiere salida a `graph.microsoft.com` y `login.microsoftonline.com`. | Sin cambios de topología si la salida ya está permitida. La CSP del frontend no cambia: la llamada es del servidor. |
| 5 | §6.2, §6.3 y §12.5 | Angular Material no incluye vistas de calendario de eventos y FullCalendar es una dependencia nueva con su propia apariencia. | **Sin enmienda.** §6.2 pide el componente oficial "siempre que exista uno adecuado" y no existe; la dependencia se justifica en §10.4 (§12.5); su apariencia se somete al tema con variables `--mat-sys-*` (§6.3). ADR-0017 fija esa regla. |
| 6 | §10.4 | El manual es público. | No incluye nombres de buzón, identificadores del tenant ni direcciones internas. |
| 7 | §11.4 (no inventar requisitos) | La definición no precisa varias reglas del negocio. | Se formulan como propuestas P-n y preguntas Q-n (§19). |
| 8 | §9.4 | Las pruebas no dependen de servicios externos. | Doble del puerto y manejador HTTP simulado (§10.7). |

### 10.10 Etiquetas (P-17, P-18, P-19)

Los **tipos** y las **etiquetas** cubren cosas distintas. El tipo dice qué *clase* de actividad es (uno por evento) y trae
comportamiento: duración y recordatorio por defecto, congelamiento. Las etiquetas son dimensiones libres que cruzan los
tipos (sistema, entorno, norma, proyecto, trimestre) y existen para localizar información.

| Opción | Valoración |
|---|---|
| **Etiquetas libres, en tabla propia, creadas al usarlas, con sugerencias** | **Propuesta.** No frena a quien planifica; las sugerencias y el conteo evitan duplicados ("windows" y "Windows" son la misma). Una tabla propia hace sencillos el autocompletado, el conteo, el filtro y un futuro renombrado o fusión. |
| Lista cerrada, administrada con un permiso | Más consistente, pero obliga a pedir cada etiqueta nueva y añade un permiso y una pantalla. Se puede adoptar después si hay desorden. |
| Texto o `jsonb` dentro del evento, sin tabla | Lo más simple de guardar, pero el conteo y el autocompletado exigen recorrer todos los eventos y renombrar toca todas las filas. |

- **Filtro** (P-18): varias etiquetas se combinan con "todas" (Y), la forma habitual de acotar una búsqueda. La búsqueda
  por texto también mira las etiquetas. Al filtrar sin haber elegido una vista, se muestra la lista del año, porque una etiqueta rara vez se
  concentra en un mes; el resumen y los calendarios respetan el filtro. Vista, periodo y filtros van en
  la dirección de la página.
- **Outlook** (P-19): las etiquetas **no** se envían a Outlook. Un cambio de etiquetas no debe generar avisos al equipo, y
  añadirlas como categorías llenaría la lista de categorías del buzón compartido. Solo se envía el tipo (como categoría
  de Outlook, RF-16) y el `.ics` lleva tipo y etiquetas.
- Las etiquetas nunca se tratan como HTML (RNF-7). El filtro viaja con parámetros repetidos (`tags=a&tags=b`), no como
  una lista separada por comas, para admitir cualquier carácter.

## 11. Cambios de frontend

Feature `features/agenda/`, con carga diferida, y un grupo de navegación "Agenda" (ícono `calendar_month`), visible con
`agenda.read` (solo UX).

| Ruta | Pantalla | Permiso |
|---|---|---|
| `/agenda` | Agenda con sus cuatro vistas: lista del año, año, mes y semana (RF-12, RF-13); vista, periodo y filtros en los parámetros de la ruta | `agenda.read` |
| `/agenda/events/new` | Nuevo evento | `agenda.read` + `agenda.create` |
| `/agenda/series/new` | Nueva serie con vista previa | `agenda.read` + `agenda.create` |
| `/agenda/events/:id` | Detalle, estado en Outlook y acciones | `agenda.read` |
| `/agenda/events/:id/edit` | Editar evento y, si pertenece a una serie, sus campos comunes | `agenda.read` + `agenda.update` |
| `/agenda/types` | Catálogo de tipos | `agenda.read` + `agenda.types.manage` |

Componentes de Angular Material y CDK, sin dependencias nuevas:
- Selector de vista: `mat-button-toggle-group` (Lista del año, Año, Mes, Semana), con "Anterior", "Hoy" y "Siguiente"
  (`mat-icon-button` con `aria-label` y `matTooltip`) y el título del periodo ("octubre de 2026", "Semana del 12 al 18 de
  octubre de 2026"). Requiere `provideNativeDateAdapter` y el idioma español con la semana desde el lunes.
- Resumen (en la lista del año): selector de año (`mat-select`) y doce meses como `mat-chip-listbox` con su cantidad, más
  `mat-card` con totales por estado y tipo.
- Lista del año: `mat-table` agrupada por mes, con selección (`mat-checkbox`) para programar varios, `mat-chip` de tipo y estado (roles estándar
  de Material 3, siempre con ícono y texto), ícono de advertencia con `matTooltip` y `aria-label`, `mat-menu` de acciones
  (Ver, Editar, Programar, Cancelar, Registrar ejecución, Sincronizar de nuevo) según permisos.
- Calendarios (FullCalendar, §10.4): un componente `agenda-calendar` en `features/agenda/calendar/` que envuelve
  `@fullcalendar/angular` y se carga de forma diferida al elegir Año, Mes o Semana. Usa `dayGridMonth`, `timeGridWeek` y
  `multiMonthYear`, en español, con la semana desde el lunes, el eje de 00:00 a 24:00 abierto en las 06:00, el indicador
  de la hora actual y "+n más" por día; `editable` apagado. Cada actividad se dibuja con `eventContent`: ícono y texto de
  estado, y nombre accesible ("{título}, {tipo}, {estado}, {fecha y hora}"), con clases mapeadas a roles de color de
  Material 3. La hoja de estilos de la feature solo asigna las variables de FullCalendar a `--mat-sys-*`. En pantalla
  estrecha, la semana se presenta como un día por vez y el año con los meses en una columna.
- Con `agenda.create`, elegir un día vacío (mes) o una hora vacía (semana) abre "Nuevo evento" con esa fecha y hora (P-21).
- Etiquetas: en la lista, `mat-chip-set` con las tres primeras etiquetas y "+n"; pulsar una la añade al filtro
  (con `aria-label` "Filtrar por la etiqueta {x}"). El filtro usa `mat-chip-grid` con `mat-autocomplete` (patrón estándar
  de Material), y un botón "Limpiar filtros", el mismo en las cuatro vistas. Los meses del resumen y los calendarios
  reflejan lo que cumple el filtro. Vista, periodo y filtros se guardan en los parámetros de la ruta. En el detalle,
  una tarjeta "Etiquetas" con el mismo editor, que guarda al instante.
- Formularios: formulario reactivo con `mat-form-field`, `mat-datepicker` y campo de hora, `mat-slide-toggle` de día
  completo, `mat-select` (tipo, impacto, unidad de repetición), un campo numérico (cada N), `mat-radio-group` (ancla de la serie), `mat-chip-grid` para los
  participantes y las etiquetas (esta con `mat-autocomplete`) y validación en línea.
- Vista previa de la serie: tabla de fechas con sus advertencias antes de crear.
- Diálogos (`mat-dialog`): cancelar con motivo, registrar ejecución, confirmaciones de programar y de editar una serie.
- Estado en Outlook: ícono y texto en la lista y el detalle; "Outlook no configurado" cuando está desactivado.
- Estados de carga (`mat-progress-bar`), vacío y error en todas las pantallas (§8.4).
- Nada de `innerHTML` ni `bypassSecurityTrust*`; los enlaces de evidencia se abren con `rel="noopener noreferrer"`.

## 12. Cambios de backend

### 12.1 Endpoints

Todos bajo `/api/v1`, con Problem Details en los errores.

| Método | Ruta | Autorización | Request | Response | Errores |
|---|---|---|---|---|---|
| GET | `/agenda-events?from=&to=&type=&status=&responsible=&mine=&tags=&search=` | `agenda.read` | — | `200 { items: [evento resumido], truncated, timeZone }` (rango máximo de 366 días; hasta 1 000 resultados) | 400, 401, 403 |
| GET | `/agenda-events/summary?year=&type=&responsible=&mine=&tags=&search=` | `agenda.read` | — | `200 { year, months: [{ month, draft, scheduled, completed, cancelled, overdue }], byType: [{ typeId, count }] }` | 400, 401, 403 |
| GET | `/agenda-events/{id}` | `agenda.read` | — | `200` evento completo + `ETag` | 401, 403, 404 |
| POST | `/agenda-events` | `agenda.create` | datos de RF-3 | `201` evento + `ETag` + `warnings` | 400, 401, 403, 422 |
| PUT | `/agenda-events/{id}` | `agenda.update` | datos de RF-3 + `If-Match` | `200` evento + `ETag` + `warnings` (y `outlook` si estaba Programado) | 400, 401, 403, 404, 412, 422, 428 |
| DELETE | `/agenda-events/{id}` | `agenda.update` | — | `204` (solo Borrador) | 401, 403, 404, 422 |
| PUT | `/agenda-events/{id}/tags` | `agenda.update` | `{ tags: [≤ 10] }` | `200 { tags }` (cualquier estado; sin `ETag` ni cambios en Outlook) | 400, 401, 403, 404, 422 |
| GET | `/agenda-tags?search=&limit=` | `agenda.read` | — | `200 { items: [{ name, count }] }` (por cantidad y nombre; `limit` máximo 50) | 400, 401, 403 |
| POST | `/agenda-events/schedule` | `agenda.publish` | `{ ids: [≤ 50] }` | `200 { results: [{ id, status, warnings, outlook: { status, errorCode? }, error? }] }` | 400, 401, 403 |
| POST | `/agenda-events/{id}/cancel` | `agenda.publish` | `{ reason }` | `200` evento + `outlook` | 400, 401, 403, 404, 422 |
| PUT | `/agenda-events/{id}/completion` | `agenda.complete` | `{ result, executedAt?, notes?, evidenceLinks[] }` | `200` evento | 400, 401, 403, 404, 422 |
| POST | `/agenda-events/{id}/outlook-sync` | `agenda.publish` | — | `200 { outlook: { status, errorCode? } }` | 401, 403, 404, 422 |
| POST | `/agenda-events/outlook-sync` | `agenda.publish` | — (todos los Programados futuros pendientes, en error o no publicados; hasta 50) | `200 { results: […] }` | 401, 403 |
| GET | `/agenda-events/export.ics?from=&to=&type=&responsible=&tags=` | `agenda.read` | — | `200 text/calendar` (solo Programados) | 400, 401, 403 |
| POST | `/agenda-series?dryRun=` | `agenda.create` | datos de RF-3 + regla de repetición | `200 { occurrences: [{ start, end, warnings }] }` si `dryRun=true`; `201 { seriesId, eventIds }` si no | 400, 401, 403, 422 |
| PUT | `/agenda-series/{id}` | `agenda.update` | campos comunes (RF-6) | `200 { updated }` | 400, 401, 403, 404, 422 |
| GET | `/agenda-event-types` | `agenda.read` | — | `200 { items: […] }` | 401, 403 |
| POST | `/agenda-event-types` | `agenda.types.manage` | datos de RF-1 | `201` tipo + `ETag` | 400, 401, 403, 422 |
| PUT | `/agenda-event-types/{id}` | `agenda.types.manage` | datos de RF-1 + `If-Match` | `200` tipo + `ETag` | 400, 401, 403, 404, 412, 422, 428 |

- El evento resumido incluye: `id`, `seriesId`, `type`, `title`, `start`, `end`, `isAllDay`, `status`, `isOverdue`,
  `responsible { id, name, isActive }`, `impact`, `tags`, `hasWarnings` y `outlook { status }`.
- Las fechas se devuelven con la hora local, la zona y el instante UTC (RNF-4).
- `outlook.status` es `not-published`, `pending`, `published` o `error`; `errorCode` es uno de un conjunto cerrado
  (`graph_unauthorized`, `graph_forbidden`, `graph_not_found`, `graph_throttled`, `graph_unavailable`, `graph_timeout`,
  `graph_rejected`, `not_configured`).
- El parámetro `tags` se repite (`tags=a&tags=b`) y exige que el evento tenga todas.
- Las vistas de calendario usan `GET /agenda-events` con el rango visible: la semana, la cuadrícula del mes con sus
  días vecinos o el año. `timeZone` es la zona de la organización (RNF-9).
- Los eventos de una misma solicitud de programación se procesan en orden; ante `429` se detiene el lote.

### 12.2 Auditoría

Acciones nuevas en `audit_events` (tipos de recurso `agenda-event`, `agenda-series`, `agenda-event-type`):
`agenda.event.created`, `agenda.event.updated`, `agenda.event.deleted`, `agenda.event.scheduled`,
`agenda.event.cancelled`, `agenda.event.completed`, `agenda.event.completion-updated`, `agenda.event.tags-updated`, `agenda.series.created`,
`agenda.series.updated`, `agenda.event-type.created`, `agenda.event-type.updated`, `agenda.event.outlook-published`,
`agenda.event.outlook-failed`. Los valores anterior y posterior incluyen los campos del evento y **nunca** secretos,
tokens ni la respuesta cruda de Graph. La pantalla de auditoría de SPEC-0001 muestra en español las acciones nuevas y
permite filtrar por esos tipos de recurso.

### 12.3 Configuración

Variables nuevas, todas documentadas en `.env.example` y README (constitución §4.3). Solo `AGENDA_OUTLOOK_CLIENT_SECRET`
es un secreto.

| Variable | Uso |
|---|---|
| `AGENDA_TIME_ZONE` | Zona horaria IANA de la organización. Valor inicial: `America/Bogota` (RNF-4; Q-3). |
| `AGENDA_ALLOWED_ATTENDEE_DOMAINS` | Dominios permitidos para los participantes, separados por comas. Vacía = el dominio del buzón, que es el corporativo (Q-4). |
| `AGENDA_PUBLIC_URL` | URL pública de ITMas, para el enlace del cuerpo de la invitación. |
| `AGENDA_OUTLOOK_ENABLED` | `false` por defecto. Con `true`, el resto de las variables `AGENDA_OUTLOOK_*` son obligatorias y la API no arranca si faltan (CA-18). |
| `AGENDA_OUTLOOK_TENANT_ID`, `AGENDA_OUTLOOK_CLIENT_ID` | Identificadores de la aplicación de Entra ID de Graph (públicos, no secretos). |
| `AGENDA_OUTLOOK_CLIENT_SECRET` | **Secreto.** Credencial de la aplicación. Se prefiere certificado (a decidir en ADR-0016). |
| `AGENDA_OUTLOOK_MAILBOX` | Dirección del buzón compartido nuevo cuyo calendario se usa (Q-2). |
| `AGENDA_OUTLOOK_TEAM_ADDRESS` | Opcional. Dirección de la lista de distribución del equipo, que ya existe (Q-2), invitada como opcional cuando "notificar al equipo" está activo. Debe pertenecer a un dominio permitido. Si está vacía, la opción "notificar al equipo" no se muestra. |

## 13. Cambios de base de datos (preliminar; los concreta `database`)

Esquema `app`, migración `AddInfrastructureAgenda`:

| Tabla | Columnas principales |
|---|---|
| `agenda_event_types` | `id`, `name varchar(80)` (único `lower(name)`), `description varchar(500)`, `default_duration_minutes`, `default_reminder_minutes`, `is_freeze`, `is_active`, `created_at`, `updated_at`, `revision`; `xmin` como token de concurrencia. |
| `agenda_series` | `id`, `type_id`, `pattern jsonb` (descripción de la regla, RN-12), `created_at`, `created_by`. |
| `agenda_events` | `id`, `series_id` (nulo), `type_id`, `title varchar(120)`, `description varchar(4000)`, `status`, `is_all_day`, `starts_at_local`, `ends_at_local`, `time_zone`, `starts_at_utc`, `ends_at_utc`, `responsible_user_id`, `participants jsonb`, `notify_team`, `impact`, `scope varchar(500)`, `location varchar(200)`, `reminder_minutes`, `cancel_reason`, `completion_result`, `executed_at`, `completed_at`, `completed_by`, `completion_notes varchar(4000)`, `evidence_links jsonb`, `outlook_status`, `outlook_event_id`, `outlook_synced_revision`, `outlook_error_code`, `outlook_synced_at`, `created_at`, `created_by`, `updated_at`, `updated_by`, `revision`; `xmin`. |
| `agenda_tags` | `id`, `name varchar(30)` (único `lower(name)`), `created_at`, `created_by`. |
| `agenda_event_tags` | `event_id`, `tag_id` (clave primaria compuesta; `event_id` con `ON DELETE CASCADE` porque solo se eliminan borradores, `tag_id` con `ON DELETE RESTRICT`). |

| Cambio | Detalle |
|---|---|
| Índices | `agenda_events(starts_at_utc)`, `(status, ends_at_utc)` (vencidas y solapamientos), `(responsible_user_id, starts_at_utc)`, `(series_id)`, `(type_id)`; único parcial `outlook_event_id` donde no sea nulo; `agenda_event_tags(tag_id, event_id)` para filtrar por etiqueta y `agenda_tags(lower(name))` para las sugerencias. |
| Claves foráneas | `type_id` y `series_id` con `ON DELETE RESTRICT`. `responsible_user_id` referencia `users(id)` sin cascada; el dominio `Agenda` solo guarda el `Guid` y no depende del dominio `Authorization`. Los nombres se resuelven en las consultas, como en SPEC-0003 (RF-4b). |
| Datos iniciales | Los tipos del Anexo A (identificadores fijos) se insertan en la migración, con duración por defecto de 60 minutos (1 día para el congelamiento) y recordatorio por defecto de 1 día antes, editables en el catálogo (P-16). |
| Retención | Sin purga: los eventos se conservan (Q-9). |
| Roles | Sin cambios: la API ya tiene `SELECT/INSERT/UPDATE/DELETE` sobre `app` por privilegios por defecto. |
| Reversibilidad | `Down` elimina las cinco tablas; en un entorno compartido se corrige con otra migración (constitución §5.4). |

## 14. Consideraciones de seguridad

### 14.1 Amenazas (STRIDE)

| Amenaza | Mitigación |
|---|---|
| **S/E** Usar ITMas para enviar invitaciones a destinatarios arbitrarios (spam o *phishing* desde el buzón corporativo) | `agenda.publish` separado de `agenda.update`; participantes limitados a dominios permitidos (RN-5) y a 30 por evento; el cuerpo solo incluye campos del evento y un enlace a ITMas; texto plano; límite de frecuencia global; auditoría de cada programación. |
| **S/E** Robo o abuso de la credencial de aplicación de Graph | Permiso acotado a un solo buzón (§14.4), sin consentimiento global en Entra ID; la credencial solo existe en el contenedor `api`, por variable de entorno, sin pasar por línea de comandos ni logs; certificado y caducidad corta recomendados; su vencimiento figura en esta agenda; la aplicación solo tiene permisos de calendario. |
| **T** Alterar el plan sin autorización | Permiso por acción, `ETag`/`If-Match`, auditoría *append-only*. |
| **T** Alterar eventos publicados desde Outlook | Solo la aplicación y los administradores del buzón tienen escritura en el calendario; el equipo tiene lectura (§14.4). Lo que se edite allí se sobrescribe (RN-14). |
| **R** Cambios de fechas o cancelaciones sin rastro | Auditoría de todas las operaciones (§12.2). |
| **I** La agenda revela la postura de seguridad (fechas de pruebas de penetración, rotaciones, congelamientos) | Acceso con `agenda.read`; invitaciones solo a dominios permitidos; calendario compartido solo con lectura para el equipo; guía de títulos neutros (Q-8) en la interfaz y el manual. |
| **I** Secretos o hallazgos escritos por error en un título, una descripción o una etiqueta | La agenda **no almacena valores** de claves ni hallazgos: solo *cuándo* y *quién*. Ayuda visible en el campo y en el manual. No se puede impedir técnicamente; se registra en el modelo de amenazas. |
| **I** Datos personales (correos del responsable y de los participantes) | Son direcciones corporativas, limitadas a los dominios permitidos; los logs registran ids, nunca correos ni títulos. |
| **I** Un error de Graph expone nombres de buzón o detalles del tenant | Solo se guarda y muestra un código normalizado; el cuerpo de la respuesta no se registra ni se devuelve. |
| **D** Límites de Graph o indisponibilidad | No bloquea el uso (RF-20); tiempo máximo de 10 s; lote de hasta 50 y detenido ante `429`; sin reintentos automáticos. |
| **D** Series o consultas enormes | Máximo de 60 ocurrencias, 50 eventos por lote, rango máximo de 366 días y 1 000 resultados. |
| **T/E** XSS en título, descripción o enlaces | Angular escapa la interpolación; prohibidos `innerHTML` y `bypassSecurityTrust*`; enlaces `http`/`https` validados en el backend y con `rel="noopener noreferrer"`; texto plano hacia Outlook. |
| **T** Inyección de propiedades en el `.ics` (saltos de línea) | Escape RFC 5545 y plegado de líneas, con pruebas (CA-19). |
| **T** SSRF o llamadas a destinos controlados por el usuario | El adaptador solo llama a `login.microsoftonline.com` y `graph.microsoft.com`, con rutas fijas; ninguna URL procede de la entrada. |
| **T** Una vulnerabilidad o un cambio malicioso en la cadena de suministro de FullCalendar | Versión fija y `package-lock.json`; solo los complementos estándar necesarios; se empaqueta con el build, sin CDN, y la CSP mantiene `script-src 'self'`; revisión de `npm audit` al añadirla y en cada actualización. |

### 14.2 Datos personales

Se guardan el responsable (usuario de ITMas) y las direcciones de los participantes, todas corporativas. No hay datos de
personas usuarias finales. La agenda se comparte con Microsoft 365 solo mediante invitaciones a esas direcciones.

### 14.3 Revisión de `security`

Obligatoria para: ADR-0016, el alcance y la credencial de la aplicación de Graph, la lista de dominios permitidos, la
generación del `.ics`, el manejo de enlaces de evidencia, la incorporación de FullCalendar (cadena de suministro y CSP) y la actualización de SECURITY.md y del modelo de amenazas.

### 14.4 Prerrequisitos en Microsoft 365 (los realiza el equipo de infraestructura, que administra el tenant; no los hace ITMas)

1. Crear un **buzón compartido** nuevo que aloje el calendario, por ejemplo "Agenda de Infraestructura" (Q-2), y
   compartir su calendario con el equipo con permiso de **solo lectura**. Confirmar que la lista de distribución del
   equipo existe en Exchange Online y pertenece a un dominio permitido.
2. Registrar una **aplicación en Entra ID distinta** de la del BFF, con credencial por certificado (preferible) o
   secreto con caducidad, sin permisos delegados.
3. Acotar el permiso de calendario con **RBAC for Applications de Exchange Online**: crear el *service principal* en
   Exchange, un ámbito de gestión que contenga **solo ese buzón** y asignar el rol `Application Calendars.ReadWrite`.
   **No** consentir `Calendars.ReadWrite` en Entra ID: los permisos se suman y el consentimiento global anula el límite.
4. Verificar con `Test-ServicePrincipalAuthorization` que solo el buzón está dentro del ámbito. Los cambios tardan entre
   30 minutos y 2 horas en aplicarse (caché de permisos).
5. Entregar a quien despliega ITMas el identificador del tenant, el de la aplicación, la credencial y la dirección del
   buzón, para las variables de §12.3.

## 15. Pruebas

| Criterio | Tipo | Prueba prevista |
|---|---|---|
| CA-1 | Unit (dominio) + Integración | Catálogo sembrado, alta, modificación y desactivación de tipos; nombre repetido; tipo inactivo. |
| CA-2, CA-12, CA-13 | Unit (dominio) + Integración | Reglas RN-1 a RN-5; propiedades desconocidas → `400`; responsable inactivo; dominios y límites de participantes. |
| CA-3 | Unit (dominio) | Generador: segundo martes, día 31, año bisiesto, anual, cada N días y semanas, límite de 60, hora local constante en un cambio de horario; vista previa sin persistencia (integración). |
| CA-4 a CA-9 | Unit (dominio) + Integración | Transiciones y RN-7 a RN-10; `412` con `If-Match` desactualizado; edición de serie; programar por lotes con resultado por evento; enlaces rechazados; completar antes del inicio. |
| CA-10 | Integración + Frontend | Vencidas, filtros y resumen contra datos conocidos; componente de la lista con las marcas. |
| CA-11 | Unit (dominio) + Integración + Frontend | Solapamientos con congelamientos y con eventos con impacto; las advertencias no bloquean. |
| CA-14, CA-15 | Integración (doble del puerto) + Unit (adaptador) | Qué recibe el puerto al programar, modificar y cancelar; mapeo HTTP del adaptador (asunto, cuerpo plano, categoría, recordatorio, invitados, `transactionId`) con un manejador simulado. |
| CA-16 | Integración + Unit (adaptador) | `401`, `403`, `404`, `429`, `5xx` y agotamiento del tiempo → códigos normalizados; el evento queda en el estado pedido; reintento sin duplicar; recreación ante `404`; lote detenido ante `429`. |
| CA-17, CA-18 | Integración | Con Outlook desactivado no hay llamadas; con configuración incompleta la API no arranca y no filtra valores. |
| CA-19 | Unit | `.ics` contra *fixtures*; escape de saltos de línea, `;` y `,`; `UID` estable; `CATEGORIES` con el tipo y las etiquetas. |
| CA-20 | Integración | `401` y `403` en cada endpoint; combinaciones de permisos; pruebas de convención existentes (catálogo y autorización). |
| CA-21 | Integración | Un evento de auditoría por operación, sin secretos ni respuestas de Graph. |
| CA-22 | Frontend + Unit | Título con `<script>` como texto literal; cuerpo de Outlook en texto plano. |
| CA-23 | Verificación documentada | Revisión visual en claro y oscuro, escritorio y móvil, y recorrido con teclado. |
| CA-24 | Verificación documentada | Con el tenant real: operación sobre el buzón configurado y `403` sobre otro buzón (§14.4). |
| CA-25 | Unit (dominio) + Integración | `TagName`: recorte, espacios repetidos, mayúsculas, longitud y máximo de 10; la misma etiqueta en dos eventos es una sola fila; sugerencias con su cantidad. |
| CA-26 | Integración + Frontend | Filtro por una y por varias etiquetas (todas), combinado con tipo, estado y responsable; búsqueda por etiqueta; resumen y calendarios con filtro; lista del año por defecto; filtros en la ruta. |
| CA-27 | Integración | Edición de etiquetas en cualquier estado; `403` sin `agenda.update`; sin cambio de `ETag`, sin llamada a Outlook y con auditoría. |
| CA-28 | Unit (dominio) + Integración | Las ocurrencias de una serie heredan las etiquetas; la edición común de la serie las reemplaza en las futuras. |
| CA-29 | Unit (dominio) + Integración | Inicio en el pasado en eventos y series → `422`; la serie no genera fechas anteriores a su inicio. |
| CA-30 | Integración + Frontend | Lista del año agrupada por mes y en orden; resumen coherente con los datos. |
| CA-31 | Frontend (adaptador y configuración) + verificación documentada | Año: configuración de `multiMonthYear`; elegir un día abre la semana y un mes abre el mes; aviso con más de 1 000 resultados; dibujo verificado en el navegador. |
| CA-32 | Frontend (adaptador) + verificación documentada | Mes: mapeo de las actividades (día completo y varios días), "+n más", clases de estado y contenido accesible; dibujo verificado en el navegador. |
| CA-33 | Frontend (adaptador) + verificación documentada | Semana: mapeo de hora y duración, actividades que cruzan la medianoche, simultáneas y franja de día completo; hora actual con reloj simulado; dibujo verificado en el navegador. |
| CA-34 | Frontend + verificación documentada | Filtros compartidos entre vistas y en la ruta; Anterior, Hoy y Siguiente; estado con ícono y texto; horas en la zona de la organización; abrir la actividad; nuevo evento desde un día u hora vacíos; teclado y lectores de pantalla; claro y oscuro; pantalla estrecha. |

## 16. Plan de implementación (preliminar)

| # | Tarea | Agente | Archivos principales |
|---|---|---|---|
| 1 | ADR-0016 y nota en ADR-0002 | architecture + security | `specs/architecture/` |
| 2 | Migración `AddInfrastructureAgenda` con los tipos iniciales | database | `Infrastructure/Persistence/**` |
| 3 | Dominio: tipos, eventos, series, etiquetas, generador, reglas y advertencias | backend | `Itmas3.Domain/Agenda/**` |
| 4 | Casos de uso (incluye etiquetas), consultas, permisos, auditoría y puerto `IOutlookCalendarGateway` | backend | `Itmas3.Application/Agenda/**` |
| 5 | Repositorios y consultas EF Core | backend | `Itmas3.Infrastructure/Agenda/**` |
| 6 | Controllers, contratos, `AgendaOptions` y su validación | backend + security | `Itmas3.Api/**` |
| 7 | Variables de entorno: `.env.example`, `init-env.sh`, `docker-compose.yml`, README | architecture | raíz y `scripts/` |
| — | **Incremento A**: API verificable sin Outlook | testing | `backend/tests/**` |
| 8 | Agenda: selector de vista, lista del año con resumen, filtros y etiquetas, formularios, serie con vista previa; navegación; adaptador de fechas y español | frontend | `frontend/src/app/features/agenda/**`, `layout/navigation/**`, `app.routes.ts` |
| 9 | Detalle, editor de etiquetas, cancelar, registrar ejecución, tipos, estado de Outlook | frontend | `features/agenda/**` |
| — | **Incremento B**: lista del año, formularios y detalle | testing | `*.spec.ts` |
| 10 | Adaptador `GraphOutlookCalendarGateway` (MSAL + `HttpClient`), caso de uso `SyncWithOutlook`, métrica | backend + security | `Itmas3.Infrastructure/Agenda/**`, `Itmas3.Application/Agenda/**` |
| 11 | Runbook del tenant (§14.4) en SECURITY.md y DEVELOPMENT.md; prueba con un buzón de pruebas | architecture + security | docs |
| — | **Incremento C**: publicación en Outlook | testing | `backend/tests/**` |
| 12 | Exportación `.ics` y botón de descarga | backend + frontend | `Itmas3.Application/Agenda/**`, `features/agenda/**` |
| — | **Incremento D**: exportación | testing | tests |
| 13 | **Prueba técnica previa** de FullCalendar (§10.4: Angular 22 *zoneless*, zona horaria, accesibilidad, jsdom, tamaño, tema) y ADR-0017 | frontend + architecture + security | `specs/architecture/`, `features/agenda/calendar/**` |
| 14 | Componente `agenda-calendar` con las vistas Mes, Semana y Año; tema, estados, filtros y carga diferida | frontend | `features/agenda/calendar/**`, `package.json`, `angular.json` |
| — | **Incremento E**: vistas de calendario | testing | `*.spec.ts` |
| 15 | Revisión de seguridad; documentación (README, ARCHITECTURE, SECURITY, DEVELOPMENT, modelo de amenazas, `specs/database/README.md`); manual de uso | security + architecture | docs, `frontend/public/manual/**` |
| — | **Cierre**: validación con el tenant; spec → Implemented | testing + security | — |

## 17. Manual de uso

| Proceso (`id`) | Acción | Permiso | Pantallas |
|---|---|---|---|
| `consultar-agenda` | Nuevo (las cuatro vistas: lista del año, año, mes y semana) | `agenda.read` | `/agenda`, `/agenda/events/:id` |
| `etiquetar-y-filtrar` | Nuevo (etiquetas, filtros combinables, vista de todo el año, filtros guardados en la dirección) | `agenda.read`, `agenda.update` | `/agenda`, `/agenda/events/:id` |
| `planificar-actividad` | Nuevo (eventos, series, vista previa, advertencias) | `agenda.create`, `agenda.update` | `/agenda/events/new`, `/agenda/series/new`, `/agenda/events/:id/edit` |
| `programar-y-cancelar` | Nuevo (qué reciben los invitados en Outlook, reintentos) | `agenda.publish` | `/agenda` |
| `registrar-ejecucion` | Nuevo (resultado, notas y evidencia) | `agenda.complete` | `/agenda/events/:id` |
| `gestionar-tipos-de-actividad` | Nuevo | `agenda.types.manage` | `/agenda/types` |
| `consultar-auditoria` | Cambia: incluye los eventos de la agenda | `audit.read` | `/admin/audit` |
| `catalogo-de-permisos` | Cambia: permisos `agenda.*` | `permissions.read` | `/admin/permissions` |

Categoría nueva del manual: "Agenda de infraestructura". El manual **no** incluye nombres de buzón, identificadores del
tenant ni direcciones internas (constitución §10.4).

## 18. Trazabilidad (al implementar)

- Decisiones: [ADR-0016](../architecture/adr-0016-outlook-graph-application-permissions.md) (propuesto el 2026-10-17);
  ADR-0017 al cerrar la prueba técnica previa de FullCalendar (tarea 13).
- Commits/PRs: —
- Archivos principales: —
- Pruebas: —
- Manual de uso actualizado: —

## 19. Decisiones y preguntas abiertas

### 19.1 Primera ronda (2026-10-17)

| # | Tema | Decisión del solicitante | Efecto en la spec |
|---|---|---|---|
| Q-1 | Microsoft 365 | El correo está en **Exchange Online** y el equipo de infraestructura **administra el tenant**. | La publicación con Graph (P-1) es viable. El propio equipo hace los prerrequisitos de §14.4 y la verificación de CA-24 con el tenant real. |
| Q-2 | Calendario de destino | **Buzón compartido nuevo** (por ejemplo "Agenda de Infraestructura") y **lista de distribución del equipo ya existente** para "notificar al equipo". | `AGENDA_OUTLOOK_MAILBOX` y `AGENDA_OUTLOOK_TEAM_ADDRESS` apuntan a ellos (§12.3). La lista debe pertenecer a un dominio permitido. Falta indicar su nombre al configurar. |
| Q-3 | Zona horaria y feriados | `America/Bogota` (UTC-5, sin horario de verano). Los feriados quedan fuera de la v1: las series no se mueven por días no laborables y se corrigen a mano. | Valor inicial de `AGENDA_TIME_ZONE` (RNF-4). RN-6 sin ajuste por días no laborables. Confirma P-13. |
| Q-4 | Equipo | Todo el equipo tiene correo corporativo y usará ITMas. No se previeron personas externas ni integrantes sin ITMas. El tamaño del equipo no se indicó. | Participantes solo del dominio corporativo (RN-5). El responsable debe haber iniciado sesión en ITMas al menos una vez (RN-4). Si más adelante hay externos, se decide de forma explícita. |

### 19.2 Segunda ronda (2026-10-17)

| # | Tema | Decisión del solicitante | Efecto en la spec |
|---|---|---|---|
| Q-5 | Parches a todos los equipos | Un evento por ventana, con el alcance en texto libre. | Sin vínculo con el inventario ni grupos de mantenimiento en la v1 (§3). Se retoma en una spec posterior si hace falta. |
| Q-6 | Avisos con anticipación | Basta la invitación al programar y el recordatorio de Outlook. No se indicaron días de anticipación. | Sin correos propios (P-6). El recordatorio inicial de cada tipo es de 1 día antes y se ajusta en el catálogo (P-16). |
| Q-7 | Aprobación previa | Sin aprobación formal. | `agenda.publish` basta para programar y queda auditado. Sin estado ni permiso de aprobación (§3, P-3). |
| Q-8 | Visibilidad de eventos de seguridad | Visibles para todo el equipo, con títulos neutros. | Sin indicador de privacidad en la v1. Guía de títulos neutros en la interfaz y el manual (P-15, §14.1). |

### 19.3 Tercera ronda (2026-10-17)

| # | Tema | Decisión del solicitante | Efecto en la spec |
|---|---|---|---|
| Q-14 | Arranque del plan | Solo fechas futuras: se planifica lo que queda de 2026 y 2027; no se registra el histórico. | Nuevo RN-16 y CA-29: no se crean eventos ni series con inicio en el pasado, y una serie no genera ocurrencias anteriores a su inicio. El histórico queda fuera (§3). |
| Q-15 | Frecuencias de repetición | Además de mensual, trimestral, semestral y anual, "cada N días o semanas". | La regla es "cada N días, semanas, meses o años" (RF-4, RN-6, CA-3). |
| Q-16 | Auditorías y gerencia | Basta con la pantalla (resumen y detalle). | Sin reporte exportable ni validación de la evidencia por otra persona; se confirman las exclusiones de §3. |
| Q-17 | "Notificar al equipo" | Activo por defecto. | RF-3: por defecto, sí. Sin dirección del equipo configurada, la opción no se muestra (§12.3). |

### 19.4 Preguntas resueltas con su propuesta por defecto

Cada pregunta indica la propuesta por defecto que el solicitante aceptó al aprobar (§19.6).

| # | Pregunta | Por qué importa | Propuesta por defecto |
|---|---|---|---|
| Q-1b | ¿ITMas es accesible solo desde la red corporativa (o VPN) o también desde Internet? | Los invitados abren desde su red el enlace del cuerpo de la invitación. También condiciona la opción D (feed `webcal`) si algún día se quisiera. | Solo la red corporativa o VPN: basta para la v1. |
| Q-9 | ¿Cuánto tiempo se conservan los eventos completados (evidencia)? | RN-13 y §13. | Sin límite: sin purga automática. |
| Q-10 | ¿Qué **normas o marcos** les aplican (ISO 27001, PCI DSS, SOC 2, otros)? ¿Qué cadencias tienen comprometidas? | Ajusta los tipos sembrados, las cadencias de ejemplo y los datos de evidencia. | Los tipos y cadencias orientativas del Anexo A. |
| Q-11 | ¿Cuántas actividades al año calculan (decenas, cientos)? | Valida que la sincronización en línea y sin cola (P-2) es suficiente. | Hasta 2 000 al año (RNF-1). |
| Q-12 | ¿Hay un **plan actual** (Excel, Outlook) que quieran cargar al empezar? | Una importación sería otra tarea y otra spec. | Fuera de la v1: se carga con las series y a mano. |
| Q-13 | ¿Necesitan **administrar etiquetas** (renombrar, fusionar, eliminar, darles colores) o basta con crearlas al usarlas? | Una pantalla de administración añadiría un permiso (`agenda.tags.manage`) y más casos de uso. | Basta con crearlas al usarlas y reutilizarlas con las sugerencias; la administración, si hace falta, en una spec posterior. |
| Q-18 | ¿Necesitan **arrastrar y soltar** actividades en el calendario para reprogramarlas, o una vista de **un solo día**? | Un arrastre accidental reprogramaría una actividad Programada y avisaría a todo el equipo; exigiría confirmación, advertencias y manejo de `ETag`. | No en la v1: se reprograma con el formulario; la semana ya cubre el detalle de un día. |

### 19.5 Propuestas (aceptadas al aprobar)

| # | Propuesta |
|---|---|
| P-1 | ITMas es la fuente de verdad y Outlook el canal de aviso: publicación unidireccional a un calendario compartido con Microsoft Graph (§10.2). |
| P-2 | Sincronización en línea, sin cola ni reintentos automáticos; con estado y reintento manual; criterio para pasar a segundo plano en RNF-6 (§10.3). |
| P-3 | Ciclo de vida Borrador → Programada → Completada / Cancelada; Vencida como marca calculada; el aviso a Outlook ocurre al programar. |
| P-4 | Series con ocurrencias materializadas, tope de 60; edición de la serie limitada a campos comunes (§10.1). |
| P-5 | Catálogo de tipos administrable, sembrado con los del Anexo A. |
| P-6 | El aviso es la invitación de Outlook y su recordatorio; sin correos propios en la v1. |
| P-7 | Invitados: responsable, participantes y, opcional, la lista de distribución del equipo (Q-2); dominios permitidos (solo el corporativo, Q-4). |
| P-8 | Solapamientos con congelamientos y con actividades con impacto: advertencia, no bloqueo. |
| P-9 | Cierre con resultado, notas y enlaces de evidencia (sin archivos). |
| P-10 | Cuatro vistas: lista del año, calendario del año (12 meses), calendario mensual y calendario semanal (§10.4). |
| P-11 | Exportación `.ics` como contingencia; se puede retirar sin afectar al resto. |
| P-12 | Permisos `agenda.*` con `agenda.update` separado de `agenda.publish`. |
| P-13 | Una sola zona horaria de la organización. **Confirmada** (Q-3: `America/Bogota`). |
| P-14 | Sin retroalimentación desde Outlook (asistencia, ediciones); la v2 podría leer las respuestas de asistencia. |
| P-15 | La agenda no almacena secretos ni hallazgos; títulos neutros por guía, no por control técnico. **Confirmada** (Q-8). |
| P-16 | Valores iniciales de los tipos: duración de 60 minutos (1 día para el congelamiento) y recordatorio de 1 día antes, ajustables en el catálogo (§13). En Q-6 no se indicaron días de anticipación. |
| P-17 | Etiquetas libres, creadas al usarlas, con sugerencias y sin permiso propio: crear con `agenda.create`, editar con `agenda.update` (§10.10). |
| P-18 | Filtro por varias etiquetas = todas (Y); la búsqueda por texto mira las etiquetas; al filtrar sin elegir vista se muestra la lista del año; vista, periodo y filtros en la dirección de la página. |
| P-19 | Las etiquetas viven en ITMas y en el `.ics`; no se envían a Outlook para no generar avisos ni llenar las categorías del buzón. Editables en cualquier estado sin cambiar el `ETag`. |
| P-20 | **Decidida por el solicitante (2026-10-17):** las vistas de calendario (Mes, Semana y Año) usan FullCalendar sujeto al tema Material; la lista del año sigue siendo una tabla de Angular Material. Si la prueba técnica previa falla, se pasa a componentes propios (§10.4). |
| P-21 | Elegir un día vacío (mes) o una hora vacía (semana) abre "Nuevo evento" con esa fecha y hora. |
| P-22 | La semana va de lunes a domingo; el eje de horas, de 00:00 a 24:00, y se abre en las 06:00. |

### 19.6 Aprobación (2026-10-17)

El solicitante aprobó la spec completa. Aceptó expresamente FullCalendar (P-20), las propuestas por defecto de §19.4
(Q-1b, Q-9 a Q-13 y Q-18), P-16 y P-21 a P-22. El resto de las propuestas (P-1 a P-15 y P-17 a P-19) forman parte de la
spec aprobada. **No quedan preguntas abiertas.**

Quedan como verificaciones durante la implementación:
- **ADR-0016** (propuesto el 2026-10-17): lo revisa `security` y lo acepta el solicitante antes de la tarea 10.
- **Prueba técnica previa de FullCalendar** (tarea 13) y su **ADR-0017**; si falla, se pasa a componentes propios (§10.4).
- **Prerrequisitos del tenant** (§14.4): los realiza el equipo del solicitante antes de la tarea 11. Hace falta el nombre
  de la lista de distribución del equipo (Q-2).
- **Valores iniciales de los tipos** (P-16): se ajustan en el catálogo si los avisos habituales del equipo son otros.

## Anexo A. Prácticas del sector y cadencias de referencia

Información **orientativa** recogida el 2026-10-17 para dar forma al catálogo. Las cadencias exactas dependen de los marcos
que apliquen a la organización (Q-10): conviene validarlas con el responsable de cumplimiento antes de adoptarlas.

### A.1 Cómo se gestiona el calendario de cambios

- ITIL 4 llama a este artefacto *change schedule* (ITIL v2 y v3 lo llamaban *forward schedule of change*). Lista los
  cambios planificados y suele incluir eventos del negocio que condicionan las fechas
  ([ITIL Tattle, Universidad de Edimburgo](https://blogs.ed.ac.uk/itiltattle/?p=672)).
- Sirve para ver conflictos entre cambios que caen en la misma ventana y para marcar los **periodos de congelamiento**
  alrededor de picos del negocio ([Znuny](https://community.znuny.org/viewtopic.php?p=40315)).
- Un congelamiento tiene fecha de inicio y fin y una vía para los cambios urgentes mediante el proceso de cambio de
  emergencia ([ejemplo de Houston Methodist](https://it.houstonmethodist.org/?p=33644)).
- Las tareas repetitivas, de bajo riesgo y conocidas (parches mensuales) se gestionan como **cambios estándar**
  preaprobados; los de emergencia (parche crítico fuera de ciclo) no pasan por el calendario y se registran después.
- NIST SP 800-40 r4 (gestión de parches empresarial) recomienda **grupos de mantenimiento** con ventanas acordadas y
  planes de mantenimiento por grupo, en lugar de aplicar parches de forma ad hoc.

### A.2 Tipos y cadencias propuestos como semilla (P-5)

| Tipo | Cadencia habitual | Referencias |
|---|---|---|
| Mantenimiento y parches de sistema operativo | Mensual, tras el segundo martes del mes (*Patch Tuesday*), con piloto, estaciones y servidores en ventanas distintas. Los parches críticos fuera de ciclo, como cambio de emergencia. | CIS Controls v8, 7.3 (parches del SO, mensual o más frecuente); NIST SP 800-40 r4 |
| Pruebas de vulnerabilidad (escaneo) | Interno: como mínimo trimestral. Activos expuestos: mensual. Remediación: mensual o más frecuente. | CIS Controls v8, 7.5 a 7.7; PCI DSS 4.0, 11.3 (trimestral) |
| Prueba de penetración | Anual y tras cambios significativos. | PCI DSS 4.0, 11.4; ISO/IEC 27001:2022, A.8.8 |
| Auditoría de seguridad (interna) | Programa anual planificado; revisión por la dirección a intervalos planificados. | ISO/IEC 27001:2022, cláusulas 9.2 y 9.3 |
| Hardening (revisión de líneas base) | Semestral o anual, y al cambiar de versión de sistema operativo o de plataforma. | ISO/IEC 27001:2022, A.8.9; CIS Benchmarks |
| Rotación de claves y secretos | Según política y criptoperiodo (por ejemplo, secretos de aplicación y cuentas de servicio cada 90 días, claves de cifrado anuales). Los secretos de las aplicaciones de Entra ID caducan: llevar su vencimiento en la agenda evita caídas (incluido el de ITMas). | ISO/IEC 27001:2022, A.5.17 y A.8.24; NIST SP 800-57 |
| Renovación de certificados TLS | La vigencia máxima de los certificados públicos es de **200 días desde el 2026-03-15**, **100 desde 2027-03-15** y **47 desde 2029-03-15** ([resumen del ballot SC-081v3](https://www.appviewx.com/blogs/certificate-validity-period-47-days/)): las renovaciones serán mucho más frecuentes; conviene automatizarlas y llevar en la agenda las que no lo estén. | CA/Browser Forum, SC-081v3 |
| Pruebas de respaldo y restauración | Trimestral o semestral. | ISO/IEC 27001:2022, A.8.13 |
| Revisión de accesos (cuentas privilegiadas, motores SQL) | Trimestral. Se relaciona con SPEC-0005. | ISO/IEC 27001:2022, A.5.18 |
| Simulacro de continuidad y recuperación | Anual. | ISO/IEC 27001:2022, A.5.29 y A.5.30 |
| Congelamiento de cambios (tipo de congelamiento) | Cierres contables o fiscales, fin de año y campañas del negocio. | ITIL 4, change schedule |
| Otro | Libre. | — |

### A.3 Dónde viven estos planes en otras organizaciones

- Herramientas **ITSM** (ServiceNow, Jira Service Management y similares) ofrecen calendario de cambios con detección de
  conflictos y congelamientos, y publican hacia el correo del equipo. ITMas no pretende reemplazarlas (Q-7).
- Plataformas **GRC** y de cumplimiento continuo programan tareas recurrentes con **recolección de evidencia** por
  ocurrencia. Esta spec toma de ellas la idea de evidencia por ocurrencia (P-9).
- El calendario compartido de correo es el **canal** que usa casi todo el mundo para avisar al equipo; casi nunca es el
  sistema de registro (§2).
