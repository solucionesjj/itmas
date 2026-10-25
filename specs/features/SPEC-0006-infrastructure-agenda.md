---
id: SPEC-0006
title: Agenda anual de infraestructura
type: feature
status: Implemented
owner: jmartinez
created: 2026-10-17
approved-by: jmartinez (2026-10-17)
agents: [architecture, security, database, backend, frontend, testing]
related: [SPEC-0001, SPEC-0003, SPEC-0004, SPEC-0005, SPEC-0008, ADR-0002, ADR-0008, ADR-0016, ADR-0017]
---

# SPEC-0006 — Agenda anual de infraestructura

> Derivada de la definición funcional aportada por el solicitante (2026-10-17) y de la asesoría sobre prácticas del
> sector del Anexo A. Las propuestas de `architecture` se identifican con **P-n** y las preguntas pendientes con
> **Q-n** (§19). Rondas de respuestas (2026-10-17) en §19.1 a §19.3.
> **Aprobada el 2026-10-17** con las propuestas por defecto de §19.4 y §19.5 (§19.6). Se implementa por incrementos
> (§16), empezando por el ADR-0016.
> **Concreción de §13 y §16** redactada a petición del solicitante y **confirmada por él el 2026-10-17**, con las
> propuestas P-23 a P-30 de §19.7. Ambas secciones son definitivas.
> **Implementada el 2026-10-19** por el solicitante, con la trazabilidad de §18.
> **Enmienda del 2026-10-21** (§19.8), tras las revisiones obligatorias de `architecture`, `security`, `database`/`backend` y
> `testing`: modificar lo ya Programado exige también `agenda.publish` y gasta el mismo límite de avisos (RF-5, RF-6, §7, §14.1);
> el límite se cuenta después de autorizar; y **CA-24** y la prueba real con el tenant de Microsoft 365 (tarea 11) pasan a
> [SPEC-0008](SPEC-0008-outlook-activation.md), que debe cumplirse antes de activar Outlook en producción.

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
  Outlook (RF-17) y, como avisan a sus invitados, exigen además `agenda.publish` y gastan el límite de avisos de §12.3
  (enmienda del 2026-10-21, §19.8).
- **RF-6** Modificar los **campos comunes de una serie** (tipo, título, descripción, responsable, participantes,
  impacto, alcance, lugar, recordatorio, etiquetas). El cambio se aplica a las ocurrencias de la serie que aún no han empezado y
  están en Borrador o Programada; **las fechas no cambian** y las demás ocurrencias no se tocan. Sirve, por ejemplo,
  para reasignar el responsable de todo el año. Si alguna de las ocurrencias que cambian está Programada, la operación exige
  además `agenda.publish` y gasta **un** aviso del límite, cualquiera que sea el número de ocurrencias (enmienda del 2026-10-21).
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
  evento" con esa fecha y hora ya rellenas (P-21). Si el día elegido es **hoy**, la hora propuesta es la actual más una, en la
  zona de la organización (D-8, 2026-10-24). Las vistas solo leen: no se reprograma arrastrando (Q-18).
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
| `agenda.read` | Consultar la agenda, el resumen, el detalle, los tipos y las etiquetas; exportar `.ics` | Agenda, detalle; `GET /agenda-events…`, `GET /agenda-event-types`, `GET /agenda-tags`, `GET /agenda-people`, `GET /agenda-events/export.ics` |
| `agenda.create` | Crear eventos y series (siempre en Borrador) | "Nuevo evento", "Nueva serie"; `POST /agenda-events`, `POST /agenda-series` |
| `agenda.update` | Modificar borradores y los campos comunes de una serie; editar etiquetas; eliminar borradores | Editar, Etiquetas; `PUT /agenda-events/{id}`, `PUT /agenda-events/{id}/tags`, `PUT /agenda-series/{id}`, `DELETE /agenda-events/{id}` |
| `agenda.publish` | Programar y cancelar eventos (esto envía invitaciones y cancelaciones) y sincronizar de nuevo con Outlook. **Junto con `agenda.update`**, modificar eventos Programados o una serie con ocurrencias Programadas (envía actualizaciones) | Programar, Cancelar, Sincronizar, Editar lo Programado; `POST /agenda-events/schedule`, `…/{id}/cancel`, `…/outlook-sync`; `PUT /agenda-events/{id}` y `PUT /agenda-series/{id}` cuando llegan a algo Programado |
| `agenda.complete` | Registrar la ejecución y modificar notas y enlaces de evidencia | Registrar ejecución; `PUT /agenda-events/{id}/completion` |
| `agenda.types.manage` | Crear, modificar y desactivar tipos de actividad | Tipos; `POST/PUT /agenda-event-types` |

Todas las acciones exigen además `agenda.read`, porque se ejecutan desde la agenda (como en SPEC-0001 y SPEC-0003). Se
separan `agenda.update` y `agenda.publish` porque programar y cancelar **notifican a otras personas**: quien prepara el
plan no tiene por qué poder enviar invitaciones (P-12). Por la misma razón, **modificar lo ya Programado** también exige
`agenda.publish` (enmienda del 2026-10-21, hallazgo F-2 de `security`): la comprobación se hace en el caso de uso, que lee el
estado en la misma transacción que el cambio, y responde `403` con un mensaje que nombra el permiso. Un borrador sigue
necesitando solo `agenda.update`. Esto vale aunque Outlook esté desactivado: el permiso no depende del despliegue.

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

- [x] **CA-1** El catálogo contiene los tipos sembrados del Anexo A. Con `agenda.types.manage` se crea, modifica y desactiva
  un tipo; un nombre repetido (sin distinguir mayúsculas) devuelve `422`; un tipo inactivo no se puede asignar a un
  evento nuevo y los eventos existentes lo conservan.
- [x] **CA-2** Un usuario con `agenda.create` crea un evento que queda en Borrador con los datos informados y el
  recordatorio del tipo por defecto. Las violaciones de RN-1 a RN-5 devuelven `422` y un cuerpo con propiedades
  desconocidas, `400`.
- [x] **CA-3** La vista previa de una serie no guarda nada. Al confirmar se generan las ocurrencias esperadas: "segundo
  martes de cada mes", "cada 90 días", un día 31 que cae el último día de los meses cortos, y la **hora local constante** en un cambio de
  horario. Más de 60 ocurrencias devuelve `422`.
- [x] **CA-4** Modificar un evento con `If-Match` desactualizado devuelve `412`. Un evento Completado solo admite notas y
  enlaces; uno Cancelado no admite cambios (`422`).
- [x] **CA-5** Modificar los campos comunes de una serie actualiza solo las ocurrencias futuras en Borrador o Programada,
  sin cambiar fechas y sin tocar las Completadas ni las Canceladas.
- [x] **CA-6** Eliminar un Borrador funciona. Eliminar un evento Programado, Completado o Cancelado devuelve `422`.
- [x] **CA-7** Programar varios borradores a la vez pasa a Programada los válidos y devuelve un resultado por evento: el
  de inicio pasado se rechaza (RN-3) sin afectar a los demás. Más de 50 por solicitud devuelve `400`.
- [x] **CA-8** Cancelar exige motivo, deja el evento en Cancelada y no admite más cambios.
- [x] **CA-9** Registrar la ejecución guarda resultado, notas y enlaces; rechaza enlaces que no sean `http` o `https` y los
  que lleven credenciales; no se puede registrar antes del inicio programado; después solo permite cambiar notas y
  enlaces.
- [x] **CA-10** Un evento Programado o en Borrador con fin pasado se muestra como Vencido. Los filtros por tipo, estado
  (incluido "Vencidas"), responsable, "Mis actividades" y título funcionan, y el resumen del año coincide con los datos.
- [x] **CA-11** Las advertencias de congelamiento y de solapamiento con impacto se devuelven al crear, modificar y en la
  vista previa de una serie, se muestran con ícono y texto y **no** impiden guardar.
- [x] **CA-12** Un responsable inactivo no se puede asignar (`422`); si se desactiva después, el evento lo muestra como
  "Responsable inactivo".
- [x] **CA-13** Un participante de un dominio fuera de la lista permitida, repetido, con formato inválido, o más de 30
  participantes, devuelve `422`.
- [x] **CA-14** Con Outlook activo, programar un evento crea en el calendario configurado un evento con el asunto, el
  cuerpo en texto plano, la categoría, el recordatorio y los invitados de RF-16, y el estado en Outlook pasa a
  Publicado.
- [x] **CA-15** Modificar un evento Programado actualiza el de Outlook; cancelarlo lo cancela con el motivo.
- [x] **CA-16** Si Graph falla (`401`, `403`, `429`, `5xx`, agotamiento del tiempo), el evento queda en el estado pedido y su
  estado en Outlook es Error o Pendiente, con un código normalizado y sin detalle crudo. "Sincronizar de nuevo" lo
  publica sin duplicar. Si el evento de Outlook ya no existe, se vuelve a crear.
- [x] **CA-17** Con Outlook desactivado, toda la funcionalidad opera sin hacer llamadas externas y los eventos muestran "No
  publicado". Al activarlo, "Sincronizar de nuevo" publica los eventos Programados futuros.
- [x] **CA-18** Con `AGENDA_OUTLOOK_ENABLED=true` y configuración incompleta, la API no arranca y el mensaje no incluye
  valores secretos.
- [x] **CA-19** El `.ics` exportado es válido (RFC 5545), contiene solo eventos Programados del rango, usa un `UID`
  estable, incluye el tipo y las etiquetas como `CATEGORIES`, escapa saltos de línea, `;` y `,`, y un título con saltos de línea no inyecta propiedades.
- [x] **CA-20** Cada endpoint de §12.1 devuelve `401` sin sesión y `403` sin su permiso; con `agenda.read` pero sin
  `agenda.create` no se puede crear, y sin `agenda.publish` no se puede programar ni cancelar.
- [x] **CA-21** Cada operación de escritura genera su evento de auditoría, sin secretos, sin tokens y sin la respuesta cruda de Graph.
- [x] **CA-22** Un título o una descripción con marcado HTML se muestra como texto literal en la interfaz y se envía como texto
  plano a Outlook.
- [x] **CA-23** Las pantallas funcionan en tema claro y oscuro, con teclado y en pantalla estrecha, con estados de carga,
  vacío y error.
- ~~**CA-24** Verificación documentada en el tenant: con el permiso acotado, la aplicación opera sobre el buzón
  configurado y es rechazada (`403`) sobre otro buzón.~~ **Trasladado** a [SPEC-0008](SPEC-0008-outlook-activation.md)
  por la enmienda del 2026-10-21 (§19.8): depende del tenant y es condición para activar Outlook en producción, no para
  entregar la agenda con Outlook desactivado.
- [x] **CA-25** Un evento admite hasta 10 etiquetas de 1 a 30 caracteres; los espacios se recortan, "Windows" y "windows" son
  la misma etiqueta (se conserva la primera escritura) y una etiqueta repetida en el evento se unifica. Más de 10 o una
  de longitud inválida devuelve `422`. El autocompletado devuelve las etiquetas existentes con su cantidad.
- [x] **CA-26** Filtrar por una o varias etiquetas devuelve solo los eventos que tienen todas, combinado con tipo, estado,
  responsable y "Mis actividades"; la búsqueda por texto también encuentra por etiqueta. El resumen del año respeta el
  filtro y los calendarios también, sin elegir vista se muestra la lista del año y los filtros quedan en la dirección de la página.
- [x] **CA-27** Con `agenda.update` se editan las etiquetas de un evento en cualquier estado, también Completado y Cancelado; sin
  ese permiso, `403`. Cambiarlas no modifica el `ETag`, no llama a Outlook y queda auditado.
- [x] **CA-28** Las ocurrencias de una serie heredan las etiquetas de la serie; la edición de los campos comunes las
  reemplaza en las ocurrencias futuras en Borrador o Programada.
- [x] **CA-29** Crear un evento o una serie con inicio en el pasado devuelve `422`. Una serie no genera ocurrencias
  anteriores a su fecha de inicio.
- [x] **CA-30** La **lista del año** muestra todas las actividades del año agrupadas por mes y en orden cronológico, con los
  datos de RF-12, y su resumen coincide con ellas.
- [x] **CA-31** La vista **Año** muestra los doce meses como calendarios con los días que tienen actividades marcados;
  elegir un día abre su semana y elegir el título de un mes abre el mes. Con más de 1 000 resultados avisa que hay que
  acotar con filtros.
- [x] **CA-32** La vista **Mes** muestra la cuadrícula con las semanas de lunes a domingo, los días de los meses vecinos
  diferenciados, las actividades de cada día con hora, tipo y título, "+n más" cuando no caben, las actividades de
  varios días abarcando sus días y el día de hoy señalado.
- [x] **CA-33** La vista **Semana** muestra siete columnas con el eje de horas, las actividades colocadas por hora y
  duración, las simultáneas en paralelo, las de día completo y de varios días en la franja superior, las que cruzan la
  medianoche repartidas entre los dos días y la línea de la hora actual.
- [x] **CA-34** En las cuatro vistas, los filtros se conservan al cambiar de vista; la vista, el periodo y los filtros quedan
  en la dirección de la página; "Anterior", "Hoy" y "Siguiente" funcionan; el estado se dibuja con ícono y texto; las
  horas están en la zona de la organización; abrir una actividad lleva a su detalle; con `agenda.create`, un día u hora
  vacíos abren "Nuevo evento" con esa fecha; y todo funciona con teclado, en tema claro y oscuro y en pantalla estrecha.
- [x] **CA-35** (enmienda del 2026-10-21) Modificar un evento Programado, o una serie con ocurrencias Programadas, sin
  `agenda.publish` devuelve `403` y no cambia nada; con él, gasta un aviso del límite de §12.3 y, agotado el límite, responde
  `429` con `Retry-After` sin cambiar nada. Un borrador solo necesita `agenda.update` y no gasta el límite. Las peticiones
  rechazadas por falta de sesión, de la cabecera anti-CSRF o de permisos no gastan el límite. La interfaz no ofrece "Editar"
  sobre lo Programado a quien no tiene `agenda.publish`.

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
- **CSP:** se esperaba que FullCalendar inyectara etiquetas `<style>`; la prueba técnica previa comprobó que la 7.1.3 no lo
  hace: usa atributos `style`, que la CSP actual ya permite (`style-src 'unsafe-inline'`), y sus hojas van en los estilos
  globales (ADR-0017). Si la CSP se endurece con *nonce*, FullCalendar lo toma de `<meta name="csp-nonce">`
  ([documentación](https://fullcalendar.io/docs/content-security-policy)).

**Prueba técnica previa** (tarea 13, antes de construir las vistas). Con la versión elegida se verifica: (1) que
funciona con Angular 22 *zoneless* y con signals, algo que la documentación del conector no menciona; (2) que muestra las
horas en la zona de la organización, con la opción de zona de FullCalendar o entregándole la hora local con una zona
fija (RNF-9); (3) si hace falta el *polyfill* opcional `temporal-polyfill` según los navegadores objetivo; (4) la
accesibilidad con teclado y los nombres accesibles (RNF-10); (5) las pruebas con Vitest y jsdom; (6) el tamaño del
paquete; y (7) el mapeo de variables al tema claro y oscuro. Si algo lo impide, se adopta el plan B (opción B) y se
actualiza esta spec.

**Resultado (2026-10-18).** Las siete verificaciones pasaron con FullCalendar **7.1.3** (con una limitación en pantalla estrecha: ver ADR-0017 y §18);
se adopta la opción A; el solicitante aceptó el ADR-0017 el 2026-10-19.

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
- **Creación simultánea.** Dos personas que usan a la vez la misma etiqueta nueva no reciben error: el índice único
  (`ux_agenda_tags_name_lower`) deja pasar a una y la otra operación se repite entera, ya con la etiqueta guardada, y la reutiliza
  (RN-15: se conserva la escritura de la primera). Es la única violación de unicidad que se reintenta; las demás (por ejemplo, el nombre
  de un tipo) siguen siendo `409`.

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
- **Exportar `.ics`** (RF-21): botón "Exportar .ics" en el encabezado de la agenda, visible con `agenda.read` (solo UX). Descarga, con
  `HttpClient` y como `Blob`, las actividades **programadas** del periodo que muestra la lista (el año o, si la lista está acotada a un
  mes, ese mes) con los filtros activos; el filtro de estado no se aplica y el aviso del botón lo dice. Mientras descarga queda
  desactivado y con `aria-busy`; al terminar o fallar avisa con el aviso estándar (más de 1 000 actividades: "Acota el periodo o los
  filtros"). Su nombre accesible dice qué exporta ("Exportar .ics de las actividades programadas de 2026").
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
| PUT | `/agenda-events/{id}` | `agenda.update` (+ `agenda.publish` si está Programado) | datos de RF-3 + `If-Match` | `200` evento + `ETag` + `warnings` (y `outlook` si estaba Programado) | 400, 401, 403, 404, 412, 422, 428, 429 |
| DELETE | `/agenda-events/{id}` | `agenda.update` | — | `204` (solo Borrador) | 401, 403, 404, 422 |
| PUT | `/agenda-events/{id}/tags` | `agenda.update` | `{ tags: [≤ 10] }` | `200 { tags }` (cualquier estado; sin `ETag` ni cambios en Outlook) | 400, 401, 403, 404, 422 |
| GET | `/agenda-tags?search=&limit=` | `agenda.read` | — | `200 { items: [{ name, count }] }` (por cantidad y nombre; `limit` máximo 50) | 400, 401, 403 |
| POST | `/agenda-events/schedule` | `agenda.publish` | `{ ids: [≤ 50] }` | `200 { results: [{ id, status, warnings, outlook: { status, errorCode? }, error? }] }` | 400, 401, 403, 409, 429 |
| POST | `/agenda-events/{id}/cancel` | `agenda.publish` | `{ reason }` | `200` evento + `outlook` | 400, 401, 403, 404, 422, 429 |
| PUT | `/agenda-events/{id}/completion` | `agenda.complete` | `{ result, executedAt?, notes?, evidenceLinks[] }` | `200` evento | 400, 401, 403, 404, 422 |
| POST | `/agenda-events/{id}/outlook-sync` | `agenda.publish` | — | `200 { outlook: { status, errorCode? } }` | 401, 403, 404, 422, 429 |
| POST | `/agenda-events/outlook-sync` | `agenda.publish` | — (todos los Programados futuros pendientes, en error o no publicados; hasta 50) | `200 { results: […] }` | 401, 403, 429 |
| GET | `/agenda-events/export.ics?from=&to=&type=&responsible=&mine=&tags=&search=` | `agenda.read` | — | `200 text/calendar; charset=utf-8` como adjunto (solo Programados; mismo rango y filtros que la lista, sin `status`; hasta 1 000 eventos) | 400, 401, 403 |
| POST | `/agenda-series?dryRun=` | `agenda.create` | datos de RF-3 + regla de repetición | `200 { occurrences: [{ start, end, warnings }] }` si `dryRun=true`; `201 { seriesId, eventIds }` si no | 400, 401, 403, 422 |
| PUT | `/agenda-series/{id}` | `agenda.update` (+ `agenda.publish` si cambia alguna ocurrencia Programada) | campos comunes (RF-6) | `200 { updated }` | 400, 401, 403, 404, 422, 429 |
| GET | `/agenda-event-types` | `agenda.read` | — | `200 { items: […] }` | 401, 403 |
| GET | `/agenda-people?search=&limit=` | `agenda.read` | — | `200 { items: [{ id, name, isMe }] }` (usuarios **activos** por nombre, sin correos; `limit` máximo 50; sin búsqueda siempre incluye a quien consulta; P-31) | 400, 401, 403 |
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
- **Límites conocidos** (revisiones del 2026-10-21, aceptados en §19.8; no cambian ningún criterio):
  - El **resumen** cuenta cada evento en el mes y el año de su **inicio** (hora local), mientras la lista y los calendarios
    muestran lo que **comparte tiempo** con el rango: un evento del 31 de diciembre al 2 de enero sale en la lista de ambos
    años y solo en el resumen del primero (H3).
  - La **programación por lotes** guarda todo el lote en una transacción: si otro usuario cambia a la vez uno de los eventos,
    el lote entero responde `409` sin resultado por evento y se repite (H4). Las advertencias del lote no tienen en cuenta los
    congelamientos que se programan en esa misma petición.
  - Dos cambios **simultáneos de etiquetas** del mismo evento pueden terminar uno de ellos en `409` en lugar de "el último gana"
    (RNF-3); basta con repetirlo (H5).
  - El documento **OpenAPI** declara las rutas y sus permisos, pero las acciones que devuelven `IActionResult` (modificar un
    evento o un tipo, cancelar, registrar la ejecución, eliminar) no declaran el esquema de la respuesta `200` ni los Problem
    Details `409`, `412`, `422`, `428` y `429`; el contrato está en esta tabla.
- **`.ics`** (RF-21, CA-19). Solo los eventos **Programados** que comparten tiempo con el rango (`to` exclusivo, de 1 a 366 días, como la
  lista) y cumplen los filtros; `status` no se admite porque siempre es Programado. Más de 1 000 eventos es un `400` ("Acota el periodo o los
  filtros"): un archivo truncado parecería completo. Un rango sin eventos da un calendario sin componentes. Es un adjunto
  (`agenda-<desde>_<hasta>.ics`, solo con fechas), en UTF-8 sin BOM y con `CRLF`. Cada evento es un `VEVENT` con: `UID` =
  `<id>@agenda.itmas` (estable); `DTSTAMP` (el momento de la exportación), `LAST-MODIFIED` y `SEQUENCE` (= revisión − 1, para que un
  cliente aplique la actualización); `SUMMARY` = `[<Tipo>] <Título>` (como el asunto de Outlook); `DESCRIPTION` con descripción, alcance e
  impacto (como el cuerpo de Outlook, sin el enlace); `LOCATION`; `CATEGORIES` con el tipo y las etiquetas, cada una escapada por
  separado (RF-26); `URL` solo con `AGENDA_PUBLIC_URL` (P-27); `STATUS:CONFIRMED`. Las horas van en UTC; los eventos de día completo, como
  fechas (`VALUE=DATE`) con `DTEND` exclusivo (P-24). El archivo **no lleva personas**: ni organizador, ni invitados, ni el nombre del
  responsable. Todo texto de una persona se escapa (`\\`, `\;`, `\,`, saltos de línea como `\n`, el resto de caracteres de control se
  descarta) y las líneas se pliegan a 75 octetos sin partir una secuencia UTF-8, de modo que ningún valor puede iniciar otra propiedad.
- `GET /agenda-events` y `GET /agenda-events/{id}` incluyen `outlookEnabled` (si programar publica en Outlook): la interfaz
  lo necesita para la confirmación de "Programar" y el indicador "Outlook no configurado" de §8.4 (P-31).

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
| `AGENDA_PUBLIC_URL` | URL pública de ITMas, para el enlace del cuerpo de la invitación. **Obligatoria** (URL absoluta `https`) con `AGENDA_OUTLOOK_ENABLED=true`; sin Outlook es opcional y, si falta, el `.ics` omite la propiedad `URL` (P-27). |
| `AGENDA_PUBLISH_REQUESTS_PER_HOUR` | Operaciones por hora que avisan por Outlook (`schedule`, `cancel`, `outlook-sync` y, desde la enmienda del 2026-10-21, modificar un evento Programado o una serie con ocurrencias Programadas), contadas de forma global en el proceso de la API y **solo después** de comprobar la sesión, la cabecera anti-CSRF y los permisos, para que nadie sin permiso agote el límite de los demás (hallazgo F-1); al excederlas responde `429` con `Retry-After`. 60 por defecto (P-26). Protege contra el envío masivo de invitaciones (§14.1). Cuenta peticiones, no destinatarios (riesgo aceptado, §19.8). |
| `AGENDA_OUTLOOK_ENABLED` | `false` por defecto. Con `true`, el resto de las variables `AGENDA_OUTLOOK_*` son obligatorias y la API no arranca si faltan (CA-18). |
| `AGENDA_OUTLOOK_TENANT_ID`, `AGENDA_OUTLOOK_CLIENT_ID` | Identificadores de la aplicación de Entra ID de Graph (públicos, no secretos). |
| `AGENDA_OUTLOOK_CLIENT_SECRET` | **Secreto.** Credencial de la aplicación. Se prefiere certificado (ADR-0016), pero el código de la v1 solo admite secreto: pasar a certificado exige un cambio de código, previsto en SPEC-0008 (riesgo aceptado, §19.8). |
| `AGENDA_OUTLOOK_MAILBOX` | Dirección del buzón compartido nuevo cuyo calendario se usa (Q-2). |
| `AGENDA_OUTLOOK_TEAM_ADDRESS` | Opcional. Dirección de la lista de distribución del equipo, que ya existe (Q-2), invitada como opcional cuando "notificar al equipo" está activo. Debe pertenecer a un dominio permitido. Si está vacía, la opción "notificar al equipo" no se muestra. |

## 13. Cambios de base de datos

Esquema `app`, migración `AddInfrastructureAgenda` (la genera `database` con `dotnet ef migrations add` después de que
`backend` entregue el dominio, tarea 2; revisando el SQL con `migrations script --idempotent`). Convenciones de
`specs/database/README.md`: `snake_case`, claves `id` y `<entidad>_id`, fechas en `timestamptz` (UTC), uuid v7.
Las enumeraciones se guardan como **códigos de texto** con su conversor y su `CHECK`, como `devices.status`.

### 13.1 Tablas

**`agenda_event_types`**

| Columna | Tipo | Notas |
|---|---|---|
| `id` | `uuid` PK | Fijo en los tipos sembrados; v7 en los nuevos. |
| `name` | `varchar(80)` NOT NULL | Único sin distinguir mayúsculas: `ux_agenda_event_types_name_lower` (único, sobre `lower(name)`). |
| `description` | `varchar(500)` | |
| `default_duration_minutes` | `int` NOT NULL | `CHECK` entre 1 y 86 400 (60 días, RN-2). |
| `default_reminder_minutes` | `int` NOT NULL | `CHECK` entre 0 y 40 320 (P-25). |
| `is_freeze`, `is_active` | `boolean` NOT NULL | |
| `created_at`, `updated_at` | `timestamptz` NOT NULL | |
| `revision` | `int` NOT NULL | ETag visible; empieza en 1. `xmin` como token de concurrencia (propiedad sombra `RowVersion`, como `devices`). |

**`agenda_series`** (sin `revision`: `PUT /agenda-series/{id}` no usa `If-Match`, §12.1)

| Columna | Tipo | Notas |
|---|---|---|
| `id` | `uuid` PK | |
| `type_id` | `uuid` NOT NULL | FK → `agenda_event_types`, `ON DELETE RESTRICT`. |
| `pattern` | `jsonb` NOT NULL | Solo describe la regla (RN-12): `{ unit, interval, anchor, startsOn, endsOn?, count? }`. Las ocurrencias no se calculan desde aquí. |
| `created_at`, `created_by` | `timestamptz`, `uuid` NOT NULL | `created_by` = id del usuario (sin FK, como `audit_events.actor_user_id`). |

**`agenda_events`**

| Columna | Tipo | Notas |
|---|---|---|
| `id` | `uuid` PK | |
| `series_id` | `uuid` NULL | FK → `agenda_series`, `ON DELETE RESTRICT`. |
| `type_id` | `uuid` NOT NULL | FK → `agenda_event_types`, `ON DELETE RESTRICT`. |
| `title` | `varchar(120)` NOT NULL | |
| `description` | `varchar(4000)` | |
| `status` | `varchar(16)` NOT NULL | `CHECK`: `draft`, `scheduled`, `completed`, `cancelled`. |
| `is_all_day` | `boolean` NOT NULL | En los eventos de día completo se guarda inicio a las 00:00 y fin a las 00:00 **del día siguiente al último día** (fin exclusivo; P-24). |
| `starts_at_local`, `ends_at_local` | `timestamp` (sin zona) NOT NULL | Hora local de la zona de la organización (RNF-4). `CHECK` fin > inicio. |
| `time_zone` | `varchar(64)` NOT NULL | Identificador IANA con que se guardó (P-28). |
| `starts_at_utc`, `ends_at_utc` | `timestamptz` NOT NULL | Calculados con la zona. `CHECK` fin > inicio. |
| `responsible_user_id` | `uuid` NOT NULL | FK → `users(id)` sin cascada. El dominio solo guarda el `Guid` (no depende de `Authorization`). |
| `participants` | `jsonb` NOT NULL, por defecto `[]` | Arreglo de correos en minúsculas. Mismo patrón que `sql_instances.databases`. |
| `notify_team` | `boolean` NOT NULL | |
| `impact` | `varchar(16)` NOT NULL | `CHECK`: `none`, `degradation`, `outage`. |
| `scope` | `varchar(500)` | |
| `location` | `varchar(200)` | |
| `reminder_minutes` | `int` NOT NULL | `CHECK` entre 0 y 40 320 (P-25). |
| `cancel_reason` | `varchar(500)` | `CHECK`: no nulo si `status = 'cancelled'`. |
| `completion_result` | `varchar(16)` | `CHECK`: `succeeded`, `partial`, `failed`, `not-performed`; no nulo si y solo si `status = 'completed'`. |
| `executed_at`, `completed_at` | `timestamptz` | |
| `completed_by` | `uuid` | Id del usuario, sin FK. |
| `completion_notes` | `varchar(4000)` | |
| `evidence_links` | `jsonb` NOT NULL, por defecto `[]` | Arreglo de `{ label, url }`. |
| `outlook_status` | `varchar(16)` NOT NULL, por defecto `not-published` | `CHECK`: `not-published`, `pending`, `published`, `error`. |
| `outlook_event_id` | `varchar(300)` | Id del evento en Graph. |
| `outlook_web_link` | `varchar(1000)` | Enlace "Abrir en Outlook" (RF-14; seguimiento de ADR-0016). |
| `outlook_synced_revision` | `int` | `revision` que Outlook confirmó por última vez; si `revision` es mayor, el estado es Pendiente. |
| `outlook_error_code` | `varchar(30)` | Uno de los códigos cerrados de §12.1. |
| `outlook_status_at` | `timestamptz` | Fecha de la última operación con Outlook, confirmada o fallida (RF-18). Sustituye a `outlook_synced_at` del borrador. |
| `created_at`, `created_by`, `updated_at`, `updated_by` | `timestamptz`, `uuid` | `created_*` NOT NULL; `updated_*` nulos hasta el primer cambio. Los ids de usuario no llevan FK. |
| `revision` | `int` NOT NULL | ETag; empieza en 1 y solo sube con los cambios de RF-5, RF-6, RF-9, RF-10 y RF-11. **No** sube al editar etiquetas (RF-24) ni por la respuesta de Outlook. `xmin` como token de concurrencia (`RowVersion`). |

**`agenda_tags`**: `id uuid` PK, `name varchar(30)` NOT NULL con `ux_agenda_tags_name_lower` (único, sobre `lower(name)`), `created_at`, `created_by`.

**`agenda_event_tags`**: PK compuesta (`event_id`, `tag_id`). `event_id` → `agenda_events` con `ON DELETE CASCADE` (solo se eliminan
borradores, RN-13); `tag_id` → `agenda_tags` con `ON DELETE RESTRICT`. Editar etiquetas toca solo esta tabla y
`agenda_tags`, de modo que `xmin` y `revision` del evento no cambian (RF-24).

### 13.2 Índices y consultas que sirven

| Índice | Consulta que sirve |
|---|---|
| `ix_agenda_events_starts_at_utc` | Rangos de la lista, el resumen y los calendarios (RF-12, RF-13). La API convierte el rango local pedido a UTC con la zona de la organización. |
| `ix_agenda_events_status_ends_at_utc` | Vencidas (`status` en Borrador o Programada y `ends_at_utc` pasado) y solapamientos (RN-11). |
| `ix_agenda_events_responsible_user_id_starts_at_utc` | Filtros por responsable y "Mis actividades". |
| `ix_agenda_events_series_id`, `ix_agenda_events_type_id` | Edición de serie (RF-6) y filtro por tipo. |
| `ix_agenda_events_outlook_event_id` | Único parcial donde `outlook_event_id IS NOT NULL`. |
| `ix_agenda_event_tags_tag_id_event_id` | Filtro "todas las etiquetas" (RF-25). |
| `ux_agenda_tags_name_lower` | Búsqueda y sugerencias. |

Los índices sobre `lower(name)` no se pueden expresar solo con el modelo de EF Core: se crean en la migración con
`migrationBuilder.Sql` de **texto constante, sin datos** (justificación del SQL explícito, constitución §5.2), como los de
`devices`. Las consultas de la aplicación usan `ToLower()`/`EF.Functions.ILike` con parámetros, nunca concatenación.
La búsqueda por texto escapa `%`, `_` y `\`. Las etiquetas que se quedan sin eventos al eliminar un borrador permanecen
en la tabla, pero las sugerencias y el conteo (RF-23, RF-26) solo listan las que tienen al menos un evento.

### 13.3 Otros cambios

| Cambio | Detalle |
|---|---|
| Datos iniciales | Los 12 tipos del Anexo A.2 (Mantenimiento y parches de sistema operativo, Pruebas de vulnerabilidad, Prueba de penetración, Auditoría de seguridad, Hardening, Rotación de claves y secretos, Renovación de certificados TLS, Pruebas de respaldo y restauración, Revisión de accesos, Simulacro de continuidad y recuperación, Congelamiento de cambios, Otro), con identificadores fijos generados una sola vez al crear la migración y copiados como constantes. Duración por defecto de 60 minutos (1 440 para el congelamiento, `is_freeze = true`) y recordatorio de 1 440 minutos (P-16). Fechas de creación fijas, no `now()`. |
| Retención | Sin purga: los eventos se conservan (Q-9). Volumen de referencia (RNF-1): 20 000 eventos y hasta 200 000 filas en `agenda_event_tags`. |
| Auditoría | Sin tablas nuevas: `audit_events` (append-only) recibe los tipos de recurso `agenda-event`, `agenda-series` y `agenda-event-type` (caben en `varchar(50)`). |
| Roles | Sin cambios: la API ya tiene `SELECT/INSERT/UPDATE/DELETE` sobre `app` por privilegios por defecto (ADR-0004). |
| Reversibilidad | `Down` elimina las cinco tablas (primero `agenda_event_tags`); en un entorno compartido se corrige con otra migración (constitución §5.4). |
| Configuración EF Core | `AgendaEventTypeConfiguration`, `AgendaSeriesConfiguration`, `AgendaEventConfiguration` y `AgendaTagConfiguration` en `Persistence/Configurations/`; `DbSet`s en `AppDbContext`. Sin atributos de persistencia en el dominio (§5.3). |

**Concreción en la implementación** (tarea 4, migración `AddInfrastructureAgenda`,
`Persistence/Configurations/AgendaConfiguration.cs`): lo que se desvía de este diseño o lo concreta.

- Los índices ordinarios y los de clave foránea siguen la convención de nombres de EF Core (`ix_<tabla>_<columnas>`),
  también el único parcial de `outlook_event_id`; los dos únicos sobre `lower(name)` se llaman `ux_…_name_lower`, como el de
  `devices`. Las claves foráneas (`fk_<tabla>_<tabla destino>_<columna>`) y las restricciones `CHECK` (`ck_agenda_…`) están
  nombradas en la migración. El `CHECK` de `agenda_events` cubre estado, impacto, estado de Outlook, resultado, fin posterior
  al inicio (local y UTC), recordatorio, motivo de cancelación y la regla "resultado solo si está completado".
- `participants`, `evidence_links` y `agenda_series.pattern` son `jsonb` con el conversor de `System.Text.Json` (enumeraciones
  en `kebab-case`), como `sql_instances.databases`. No tienen valor por defecto en la base: el dominio siempre los informa.
  Tampoco `outlook_status` tiene el valor por defecto `not-published` de la tabla de arriba, por la misma razón: el dominio crea
  cada evento con ese estado (revisión de `database`, 2026-10-21; no hace falta otra migración).
- Las etiquetas se mapean como relación muchos a muchos (`agenda_event_tags`) con acceso por campo; EF Core solo toca esa
  tabla al cambiarlas, de modo que `xmin` y `revision` del evento no se alteran (RF-24).
- Los 12 tipos del Anexo A.2 se siembran con `InsertData` en la migración, con identificadores `019ba5c0-0000-7000-8000-0000000000NN`
  (`NN` de `01` a `0c`) y fechas fijas de 2026-10-17. Los identificadores y los nombres no deben cambiar.

## 14. Consideraciones de seguridad

### 14.1 Amenazas (STRIDE)

| Amenaza | Mitigación |
|---|---|
| **S/E** Usar ITMas para enviar invitaciones a destinatarios arbitrarios (spam o *phishing* desde el buzón corporativo) | `agenda.publish` separado de `agenda.update`, también para modificar lo ya Programado (enmienda del 2026-10-21); participantes y responsable invitados solo de dominios permitidos (RN-5) y hasta 30 participantes por evento; el cuerpo solo incluye campos del evento y un enlace a ITMas; texto plano; límite de frecuencia global contado después de autorizar; auditoría de cada programación. Riesgo aceptado: el límite cuenta peticiones, no destinatarios, y vive en la memoria de cada proceso (§19.8). |
| **S/E** Robo o abuso de la credencial de aplicación de Graph | Permiso acotado a un solo buzón (§14.4), sin consentimiento global en Entra ID; la credencial solo existe en el contenedor `api`, por variable de entorno, sin pasar por línea de comandos ni logs; certificado y caducidad corta recomendados; su vencimiento figura en esta agenda; la aplicación solo tiene permisos de calendario. |
| **T** Alterar el plan sin autorización | Permiso por acción, `ETag`/`If-Match`, auditoría *append-only*. |
| **T** Alterar eventos publicados desde Outlook | Solo la aplicación y los administradores del buzón tienen escritura en el calendario; el equipo tiene lectura (§14.4). Lo que se edite allí se sobrescribe (RN-14). |
| **R** Cambios de fechas o cancelaciones sin rastro | Auditoría de todas las operaciones (§12.2). |
| **I** La agenda revela la postura de seguridad (fechas de pruebas de penetración, rotaciones, congelamientos) | Acceso con `agenda.read`; invitaciones solo a dominios permitidos; calendario compartido solo con lectura para el equipo; guía de títulos neutros (Q-8) en la interfaz y el manual. |
| **I** Secretos o hallazgos escritos por error en un título, una descripción o una etiqueta | La agenda **no almacena valores** de claves ni hallazgos: solo *cuándo* y *quién*. Ayuda visible en el campo y en el manual. No se puede impedir técnicamente; se registra en el modelo de amenazas. |
| **I** Datos personales (correos del responsable y de los participantes) | Son direcciones corporativas, limitadas a los dominios permitidos; los logs registran ids, nunca correos ni títulos. |
| **I** Un error de Graph expone nombres de buzón o detalles del tenant | Solo se guarda y muestra un código normalizado; el cuerpo de la respuesta no se registra ni se devuelve. |
| **D** Límites de Graph o indisponibilidad | No bloquea el uso (RF-20); tiempo máximo de 10 s; lote de hasta 50 y detenido ante `429`; sin reintentos automáticos. |
| **D** Agotar el límite de avisos de los demás con peticiones sin permiso | El límite se gasta después de la autenticación, la cabecera anti-CSRF y la autorización (hallazgo F-1, 2026-10-21); una petición rechazada no lo toca. |
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
**Hecha el 2026-10-21** (§18, «Revisiones obligatorias del cierre»): sin hallazgos críticos ni altos; F-1, F-2, F-4 y F-6 corregidos y el resto
aceptado por el solicitante (§19.8). La verificación con el tenant (CA-24) es de SPEC-0008.

### 14.4 Prerrequisitos en Microsoft 365 (los realiza el equipo de infraestructura, que administra el tenant; no los hace ITMas)

1. Crear un **buzón compartido** nuevo que aloje el calendario, por ejemplo "Agenda de Infraestructura" (Q-2), y
   compartir su calendario con el equipo con permiso de **solo lectura** (con un grupo de seguridad habilitado para correo: Exchange no admite una
   lista de distribución normal como destino de permisos de carpeta). Confirmar que la lista de distribución del
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
| CA-25 | Unit (dominio) + Integración | `TagName`: recorte, espacios repetidos, mayúsculas, longitud y máximo de 10; la misma etiqueta en dos eventos es una sola fila; sugerencias con su cantidad; la creación simultánea de una etiqueta nueva (una transacción que la retiene sin confirmar y varias peticiones a la vez) responde `201` a todas y deja una sola fila con la escritura de la primera. |
| CA-26 | Integración + Frontend | Filtro por una y por varias etiquetas (todas), combinado con tipo, estado y responsable; búsqueda por etiqueta; resumen y calendarios con filtro; lista del año por defecto; filtros en la ruta. |
| CA-27 | Integración | Edición de etiquetas en cualquier estado; `403` sin `agenda.update`; sin cambio de `ETag`, sin llamada a Outlook y con auditoría. |
| CA-28 | Unit (dominio) + Integración | Las ocurrencias de una serie heredan las etiquetas; la edición común de la serie las reemplaza en las futuras. |
| CA-29 | Unit (dominio) + Integración | Inicio en el pasado en eventos y series → `422`; la serie no genera fechas anteriores a su inicio. |
| CA-30 | Integración + Frontend | Lista del año agrupada por mes y en orden; resumen coherente con los datos. |
| CA-31 | Frontend (adaptador y configuración) + verificación documentada | Año: configuración de `multiMonthYear`; elegir un día abre la semana y un mes abre el mes; aviso con más de 1 000 resultados; dibujo verificado en el navegador. |
| CA-32 | Frontend (adaptador) + verificación documentada | Mes: mapeo de las actividades (día completo y varios días), "+n más", clases de estado y contenido accesible; dibujo verificado en el navegador. |
| CA-33 | Frontend (adaptador) + verificación documentada | Semana: mapeo de hora y duración, actividades que cruzan la medianoche, simultáneas y franja de día completo; hora actual con reloj simulado; dibujo verificado en el navegador. |
| CA-34 | Frontend + verificación documentada | Filtros compartidos entre vistas y en la ruta; Anterior, Hoy y Siguiente; estado con ícono y texto; horas en la zona de la organización; abrir la actividad; nuevo evento desde un día u hora vacíos; teclado y lectores de pantalla; claro y oscuro; pantalla estrecha. |

## 16. Plan de implementación

Cada tarea nombra al agente que **escribe** (columna "Agente") y, entre paréntesis, a quien debe **revisar** según
`agents/README.md`. Las rutas de `backend/src` se abrevian a `Itmas3.<Capa>/`. La numeración 1 a 15 se conserva porque otras
secciones la citan; el orden de ejecución es el de la tabla, con las compuertas de §16.2.

### 16.1 Tareas

| # | Tarea | Agente (revisa) | Archivos | Listo cuando |
|---|---|---|---|---|
| 1 | **ADR-0016** (`Accepted` por el solicitante el 2026-10-18, índices actualizados) y nota en ADR-0002 (hecha). Revisión de `security` hecha el 2026-10-21 (alcance con RBAC, credencial, dominios y límites, lectura de la constitución; ver §18). | architecture + security (+ aceptación humana) | `specs/architecture/adr-0016-*.md`, `specs/README.md` | ADR `Accepted` (hecho). No bloquea 2 a 9 ni 12; **bloqueaba la 10**, ya desbloqueada. |
| 2 | **Dominio** `Agenda/`: `EventType`, `AgendaEvent`, `EventSeries`, estados, impacto y resultado; `RecurrencePattern` y su generador de ocurrencias (puro: recibe la zona, hora local constante, hora inexistente → siguiente válida, §10.5); `TagName`; `EvidenceLink`; reglas RN-1 a RN-16; detección de advertencias (RN-11). Va **antes** de la migración porque `dotnet ef` necesita las entidades y `database` no puede tocar `Domain` (P-23). | backend (architecture) | `Itmas3.Domain/Agenda/**` | Pruebas unitarias del dominio en verde. |
| 3 | **Aplicación** `Agenda/`: permisos `agenda.*` y su `IPermissionDefinitionProvider`; acciones y vistas de auditoría (§12.2); casos de uso `CreateEvent`, `CreateSeries` (con *dry run*), `UpdateEvent`, `UpdateSeries`, `DeleteDraftEvent`, `ScheduleEvents`, `CancelEvent`, `CompleteEvent`, `ManageEventTypes`, `SetEventTags`, `SyncWithOutlook`; consultas `ListEvents`, `GetEvent`, `GetSummary`, `ListTags`; puertos (`IAgendaUnitOfWork`, repositorios, `IAgendaQueries`, `IOutlookCalendarGateway`); `AgendaSettings`; registro en `DependencyInjection.cs`. La orquestación de la publicación (guardar, llamar al puerto, guardar el resultado, §10.3) vive aquí y se prueba con un doble del puerto (P-23). | backend (architecture revisa los puertos nuevos) | `Itmas3.Application/Agenda/**`, `Itmas3.Application/DependencyInjection.cs` | Pruebas unitarias de los casos de uso en verde; `LayerDependencyTests` y el catálogo de permisos en verde. |
| 4 | **Persistencia** según §13: configuraciones EF Core, `DbSet`s en `AppDbContext`, migración `AddInfrastructureAgenda` con los 12 tipos sembrados; SQL generado revisado; `specs/database/README.md` actualizado. | database (backend, security) | `Itmas3.Infrastructure/Persistence/Configurations/Agenda*.cs`, `Persistence/AppDbContext.cs`, `Persistence/Migrations/**`, `specs/database/README.md` | `dotnet ef migrations has-pending-model-changes` limpio; migración desde cero con la pila Docker. |
| 5 | **Infraestructura** `Agenda/`: repositorios, unidad de trabajo auditada (como `InventoryUnitOfWork`: `ConflictException` ante `xmin` y unicidad, salvo la carrera de una etiqueta nueva, que repite la operación), consultas EF Core con filtros, resumen y etiquetas; registro en `DependencyInjection.cs`. | backend (architecture) | `Itmas3.Infrastructure/Agenda/**`, `Itmas3.Infrastructure/DependencyInjection.cs` | Consultas verificadas por las pruebas de integración del incremento A. |
| 6 | **API**: controllers (eventos, series, tipos, etiquetas, programar, cancelar, ejecución, sincronizar), contratos, `ETag`/`If-Match` (`EntityTags`), propiedades desconocidas → `400`; `AgendaOptions` con validación al arrancar (zona IANA resuelta, dominios, bloque Outlook completo si está activo, `AGENDA_PUBLIC_URL` según P-27, mensajes sin valores); límite de frecuencia de las rutas que notifican (P-26; desde la enmienda del 2026-10-21, un filtro de acción que cuenta después de autorizar); `AddAgendaModule` en `Composition/ServiceRegistration.cs`; mientras no exista la tarea 10, `IOutlookCalendarGateway` se registra como "no configurado". Define el contrato de los eventos de día completo (P-24). | backend (security) | `Itmas3.Api/Controllers/Agenda/**`, `Itmas3.Api/Contracts/Agenda/**`, `Itmas3.Api/Agenda/**`, `Itmas3.Api/Composition/ServiceRegistration.cs` | OpenAPI generado sin errores; convención de autorización en verde. |
| 7 | **Variables de entorno** de §12.3: `.env.example`, `docker-compose.yml` (`Agenda__*`, el secreto como `ENTRA_CLIENT_SECRET`: vacío por defecto), tabla de README. `init-env.sh` no cambia (ninguna variable nueva es `*_PASSWORD`). **Comprobar la zona horaria en la imagen `api`** (Alpine): la validación de arranque debe resolver `America/Bogota`; si falta la base de zonas, añadir `tzdata` al `Dockerfile` (P-28). | architecture (security) | `.env.example`, `docker-compose.yml`, `README.md`, `backend/Dockerfile` | `docker compose config -q` válido y `docker compose up --build` con la API sana. |
| — | **Incremento A**: API verificable sin Outlook. Unitarias: `Domain/Agenda/` (etiquetas, reglas, generador de series con segundo martes, día 31, bisiesto, cada N días, hora constante y límite de 60, advertencias, enlaces de evidencia), `Application/Agenda/` (transiciones, lote de programación con resultado por evento, cancelar, completar, edición de serie, etiquetas, sincronización con doble), `Api/Agenda/` (`AgendaOptions`, CA-18). Integración (PostgreSQL de Testcontainers, `ApiFactory` con doble del puerto): tipos y semilla (CA-1), eventos y series (CA-2 a CA-13, CA-25 a CA-29), `401`/`403` por endpoint (CA-20), auditoría sin secretos (CA-21), filtros, resumen y lista (CA-10, CA-26, CA-30), carga de RNF-1 con 20 000 eventos. | testing (backend, database, security) | `backend/tests/Itmas3.UnitTests/{Domain,Application,Api}/Agenda/**`, `backend/tests/Itmas3.IntegrationTests/Api/Agenda*Tests.cs` | `cd backend && dotnet test` en verde (requiere Docker). |
| 8 | **Frontend base**: feature `features/agenda/` con rutas diferidas y entrada "Agenda" en la navegación; `data/` (cliente de la API, modelos, etiquetas en español y estado de la ruta ↔ filtros); `provideNativeDateAdapter` y español con la semana desde el lunes; página `/agenda` con selector de vista, resumen, lista del año (`mat-table`), filtros con etiquetas y "Limpiar filtros"; formularios de evento y de serie con vista previa. Hasta la tarea 14 el selector solo ofrece "Lista del año" (P-30). | frontend (security, backend) | `frontend/src/app/features/agenda/**` (el adaptador de fechas se registra en `agenda.routes.ts`), `layout/navigation/navigation-items.ts`, `app.routes.ts` | `npm run build` sin avisos de presupuesto. |
| 9 | **Detalle y acciones**: detalle, editor de etiquetas, programar, cancelar y registrar ejecución (diálogos), edición de serie (P-29), tipos de actividad, estado de Outlook; en la **pantalla de auditoría** de SPEC-0001, las etiquetas en español de las acciones nuevas y el filtro por los tipos de recurso `agenda-*`. | frontend (backend) | `frontend/src/app/features/agenda/**`, `features/authorization/data/authorization.models.ts`, `features/authorization/audit/audit-page.html` | Pantallas con carga, vacío y error (§8.4). |
| — | **Incremento B**: lista del año, formularios y detalle. Servicio de la API, etiquetas, estado de la ruta ↔ filtros, lista con marcas de vencida y advertencias (CA-10, CA-11, CA-30), editor de etiquetas (CA-25 a CA-27), vista previa de la serie (CA-3), título con `<script>` como texto (CA-22), reglas de visibilidad por permisos; CA-23 como verificación documentada. | testing (frontend) | `frontend/src/app/features/agenda/**/*.spec.ts` | `cd frontend && npm test -- --watch=false` en verde. |
| 10 | **Adaptador de Graph**: `GraphOutlookCalendarGateway` (MSAL + `HttpClient` tipado, 10 s, `Retry-After`, códigos normalizados, texto plano, `transactionId`); registro condicionado a `AGENDA_OUTLOOK_ENABLED`; métrica `itmas.agenda.outlook.operations` (medidor `Itmas3.Agenda`, añadido en `ObservabilitySetup`); referencia propia a `Microsoft.Identity.Client` en `Directory.Packages.props` si hace falta. | backend (security; architecture por la dependencia) | `Itmas3.Infrastructure/Agenda/Graph/**`, `Itmas3.Api/Observability/ObservabilitySetup.cs`, `backend/Directory.Packages.props` | Compuerta: **ADR-0016 aceptado**. Pruebas de contrato HTTP con manejador simulado. |
| 11 | **Runbook del tenant** (§14.4): sección de la agenda en `SECURITY.md`, runbook en `DEVELOPMENT.md` (con `Test-ServicePrincipalAuthorization` y las esperas de la caché), amenazas de §14.1 en el modelo; **prueba con un buzón de pruebas** (publicar, actualizar, cancelar y `403` sobre otro buzón) y registro del resultado en §18. | architecture + security | `SECURITY.md`, `DEVELOPMENT.md`, `specs/security/threat-model-baseline.md` | Compuerta: **prerrequisitos de §14.4 cumplidos** y nombre de la lista de distribución (Q-2). La documentación se hizo; **la prueba real pasó a [SPEC-0008](SPEC-0008-outlook-activation.md)** (enmienda del 2026-10-21). |
| — | **Incremento C**: publicación en Outlook. Unitarias del adaptador con `HttpMessageHandler` simulado (CA-14 a CA-16: asunto, cuerpo plano, categoría, recordatorio, invitados, `transactionId`; `401`, `403`, `404`, `429`, `5xx` y tiempo agotado → códigos normalizados; recreación ante `404`); integración con el doble: programar, modificar, cancelar, fallo sin bloquear, "Sincronizar de nuevo" sin duplicar, lote detenido ante `429` (CA-14 a CA-17); CA-24 pasó a SPEC-0008 (enmienda del 2026-10-21). | testing (security) | `backend/tests/**` | `dotnet test` en verde. |
| 12 | **Exportación `.ics`**: escritor RFC 5545 puro (escape, plegado de líneas, `UID` estable, `CATEGORIES`), caso de uso `ExportCalendar`, endpoint `GET /agenda-events/export.ics` y botón de descarga. | backend + frontend (security: generación del `.ics`) | `Itmas3.Application/Agenda/ExportCalendar/**`, `Itmas3.Api/Controllers/Agenda/**`, `frontend/src/app/features/agenda/**` | Pruebas de CA-19 en verde. No depende de Graph. |
| — | **Incremento D**: exportación. Unitarias con *fixtures* de `.ics` (CA-19: `;`, `,`, saltos de línea, título que intenta inyectar propiedades); integración (solo Programados, rango, filtros, `401`/`403`); botón en el frontend. | testing | `backend/tests/**`, `*.spec.ts` | Suites en verde. |
| 13 | **Prueba técnica previa de FullCalendar** (§10.4, las siete verificaciones) con versión fija, solo complementos no *premium* y `npm audit`; **ADR-0017** con su resultado. Si falla, plan B y se actualiza la spec. | frontend + architecture + security (cadena de suministro y CSP) | `specs/architecture/adr-0017-*.md`, `frontend/package.json`, `package-lock.json`, `frontend/src/app/features/agenda/calendar/**` | ADR-0017 redactado; decisión A o B tomada por el solicitante. |
| 14 | **`agenda-calendar`**: vistas Mes, Semana y Año con el tema Material, estado con ícono y texto, nombre accesible, filtros compartidos, ruta, `dateClick` hacia "Nuevo evento", pantalla estrecha y carga diferida; el selector de la tarea 8 pasa a ofrecer las cuatro vistas. | frontend (security, architecture) | `frontend/src/app/features/agenda/calendar/**`, `features/agenda/**`, `angular.json` | `npm run build` sin avisos de presupuesto; sin violaciones de CSP. |
| — | **Incremento E**: vistas de calendario. Adaptador de eventos (día completo, varios días, medianoche, simultáneas), configuración (`multiMonthYear`, semana desde el lunes, "+n más", pantalla estrecha), reloj simulado de la línea de hora actual, clases y contenido accesible, filtros y ruta compartidos; CA-31 a CA-34 con verificación documentada en el navegador. | testing (frontend) | `frontend/src/app/features/agenda/calendar/**/*.spec.ts` | Suite del frontend en verde. |
| 15 | **Revisión de seguridad y documentación**: `README.md`, `ARCHITECTURE.md`, `SECURITY.md`, `DEVELOPMENT.md`, modelo de amenazas, `specs/database/README.md` y los índices de specs; **manual de uso** con `/update-manual SPEC-0006` (procesos de §17, categoría "Agenda de infraestructura", sin buzones ni identificadores del tenant). | security + architecture (agente de cada proceso; security por ser público) | docs, `frontend/public/manual/**` | Manual coherente (prueba de SPEC-0004) y documentación al día. |
| — | **Cierre**: `/sdd-validate SPEC-0006`; CA-23 verificado y documentado; revisiones obligatorias hechas (2026-10-21); spec → `Implemented` con la trazabilidad de §18. | testing + security | — | Sin criterios `FALLA` ni `NO VERIFICADO`. CA-24 y la prueba real de la tarea 11 están en SPEC-0008 (enmienda del 2026-10-21). |

### 16.2 Dependencias y compuertas

- **Cadena obligatoria:** 2 → 3 → 4 → 5 → 6 → 7 → incremento A. Las tareas 8 y 9 usan el contrato de la 6; la 12, las consultas de la 5.
- **Compuertas externas del incremento C** (tareas 10 y 11): ADR-0016 aceptado (tarea 1) y prerrequisitos del tenant (§14.4). **No bloquean** A, B, D ni E: si
  no están cumplidas al llegar a C, se continúa con 12 a 14 y se vuelve a C antes del cierre. Sin Outlook configurado todo lo demás funciona (RF-15).
- **Compuerta de las vistas de calendario:** la tarea 14 exige el resultado de la 13 (FullCalendar o plan B) y la tarea 8.
- **Compromisos:** cada incremento cierra con el mensaje de commit `SPEC-0006: <resumen>` que propone `/sdd-implement`; los ejecuta una persona.
- **Revisiones obligatorias** (`agents/README.md`; `/sdd-implement` las deja **pendientes**, no las da por hechas): `architecture` por los puertos
  nuevos de `Application` y por `Infrastructure`; `security` por la API (autorización, errores y límites de frecuencia), el adaptador de Graph, ADR-0016,
  el `.ics`, FullCalendar y los cambios de Docker y variables de entorno; `database` y `backend` entre sí por `Persistence/`; `testing` por las pruebas del
  código de cada agente; y, para el manual, el agente de cada proceso y `security` por ser público.

## 17. Manual de uso

| Proceso (`id`) | Acción | Permiso | Pantallas |
|---|---|---|---|
| `consultar-agenda` | Nuevo (las cuatro vistas: lista del año, año, mes y semana; exportar las actividades programadas a un archivo `.ics`, con qué se exporta y qué no) | `agenda.read` | `/agenda`, `/agenda/events/:id` |
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

- Decisiones: [ADR-0016](../architecture/adr-0016-outlook-graph-application-permissions.md) (propuesto el 2026-10-17, aceptado por el
  solicitante el 2026-10-18; revisión de `security` hecha el 2026-10-21) y
  [ADR-0017](../architecture/adr-0017-fullcalendar-calendar-views.md) (resultado de la prueba técnica previa de FullCalendar, tarea 13;
  aceptado el 2026-10-19; revisión de `security` hecha el 2026-10-21).
- **Avance (2026-10-18).** Hechos los **incrementos A a E** (tareas 2 a 10, 12 a 14, y la documentación de la 11 y de la 15): API
  verificable sin Outlook (A); frontend de la lista del año, formularios, detalle, tipos y auditoría (B); adaptador de Graph, su registro
  condicionado a `AGENDA_OUTLOOK_ENABLED`, la métrica, las pruebas con un manejador simulado y la integración HTTP con un doble del puerto (C,
  sin llamadas a Microsoft); exportación `.ics` (D); y las vistas de calendario Año, Mes y Semana con FullCalendar (E, ADR-0017). Tarea 1:
  ADR-0016 **aceptado** el 2026-10-18 (falta la revisión de `security`). **Fuera de este cierre, por decisión del solicitante
  (2026-10-18):** la **prueba con un buzón real de Microsoft 365** de la tarea 11 y, con ella, la verificación de **CA-24** (el runbook y el guion
  de la prueba están en DEVELOPMENT.md §11; los prerrequisitos del tenant de §14.4 no están cumplidos). La spec puede pasar a `Implemented` sin
  ellas; quedan como seguimiento explícito y **no se dan por verificadas**.
- **Confirmaciones del solicitante (2026-10-19).** Aceptó el ADR-0017 con el riesgo residual de la cadena de suministro de FullCalendar, confirmó las decisiones 1 a 29
  de esta sección y pasó la spec a `Implemented`. **No es una validación completa:** CA-24 y la prueba real de la tarea 11 no están verificados, las revisiones
  obligatorias de abajo siguen pendientes y lo no verificado de cada incremento sigue siéndolo.
- Commits/PRs: — (incrementos A a E sin confirmar en el repositorio)
- Archivos principales (incremento A):
  - Dominio: `backend/src/Itmas3.Domain/Agenda/**`. Aplicación: `backend/src/Itmas3.Application/Agenda/**` (permisos `agenda.*`,
    acciones de auditoría, casos de uso, orquestación de Outlook, puertos y consultas).
  - Persistencia: `Itmas3.Infrastructure/Persistence/Configurations/AgendaConfiguration.cs`,
    `Persistence/Migrations/20261018051526_AddInfrastructureAgenda*.cs` y el modelo; `specs/database/README.md`.
  - Infraestructura: `backend/src/Itmas3.Infrastructure/Agenda/**` (repositorios, unidad de trabajo, consultas, puerto de Outlook
    "no configurado").
  - API: `backend/src/Itmas3.Api/Agenda/**` (opciones y módulo), `Contracts/Agenda/**`, `Controllers/Agenda/**`,
    `Security/RateLimitingPolicies.cs`, `Composition/ServiceRegistration.cs`.
  - Entorno: `.env.example`, `docker-compose.yml`, `README.md` y `backend/Dockerfile` (`tzdata`).
- Pruebas (incremento A): unitarias en `backend/tests/Itmas3.UnitTests/{Domain,Application,Api}/Agenda/` (dominio, casos de uso con
  dobles del puerto de Outlook, opciones y contrato) e integración en `backend/tests/Itmas3.IntegrationTests/Api/Agenda/`
  (tipos y semilla, eventos y ciclo de vida, series, etiquetas, consultas, `401`/`403` por endpoint, auditoría, arranque
  y OpenAPI, y la carga de RNF-1 con 20 000 eventos). Con la API real cubren CA-1 a CA-13, CA-17, CA-18, CA-20, CA-21 y CA-25 a
  CA-30 (y el contrato de `GET /agenda-events` de CA-31 a CA-34); CA-14 a CA-16 y el texto plano de CA-22 se prueban con un doble
  del puerto en las unitarias, y quedaban para el incremento C la prueba del adaptador de Graph (hecha: ver «Pruebas (incremento C)») y CA-24 (pendiente).
- Archivos principales (incremento B): `frontend/src/app/features/agenda/**` (`agenda.routes.ts` diferido; `data/`: cliente de la API,
  modelos, etiquetas en español, estado de la ruta ↔ filtros, tiempo en la zona de la organización y adaptador de fechas del
  formulario; `page/` (lista del año), `components/` (resumen, filtros, etiquetas, estados), `events/` (formulario de actividad,
  diálogos de cancelar y de registrar ejecución, acciones compartidas), `series/` (regla y vista previa), `detail/` (detalle y editor
  de etiquetas), `types/` (catálogo)), `app.routes.ts`, `layout/navigation/navigation-items.ts` y, en la pantalla de auditoría de
  SPEC-0001, `features/authorization/data/authorization.models.ts` y `audit/audit-page.html`.
- Pruebas (incremento B): 311 pruebas en 22 archivos (`*.spec.ts` junto a cada pieza y `authorization/data/audit-labels.spec.ts`):
  CA-3 (la vista previa no guarda y la serie creada es la mostrada), CA-10 y CA-11 (marcas de vencida y advertencias con ícono y
  texto), CA-12, CA-22 (título, descripción, alcance, notas y etiquetas con marcado se muestran como texto literal), CA-25 a CA-27
  (etiquetas: normalización, máximo de 10, edición en cualquier estado sin tocar el `ETag` ni Outlook), CA-30 (lista agrupada por
  mes y en orden), filtros combinados y guardados en la dirección (CA-26 y la parte de lista de CA-34), `412`/`422` en los
  formularios, visibilidad de acciones por estado y permiso (CA-20, solo UX) y etiquetas en español de la auditoría (§12.2).
  CA-8 y CA-9 del lado de la interfaz (motivo obligatorio de la cancelación; enlaces de evidencia solo `http` o `https` y sin
  credenciales, hasta 10) y la reacción a `412`/`422` en cada diálogo. Al escribirlas aparecieron dos fallos reales de la parte ya
  hecha, corregidos: el diálogo de cancelar no llamaba a la API (su `<form>` carecía de `FormGroup` y de `ngSubmit`) y la página
  de la agenda pisaba su propio estado de carga y de error cuando la respuesta llegaba de inmediato.
  `cd frontend && npm test -- --watch=false`: 438 en verde (incluida `manual-consistency.spec.ts`), `npm run build` sin avisos de
  presupuesto y `prettier --check` limpio.
- Archivos principales (incremento D, tarea 12):
  - Aplicación: `backend/src/Itmas3.Application/Agenda/ExportCalendar/` (`IcalendarWriter`: escritor RFC 5545 puro; `ExportCalendarHandler`:
    caso de uso, que fija el estado en Programado), el método `ListExportEventsAsync` y los registros `AgendaExportEvent` y
    `AgendaExportList` en el puerto `IAgendaQueries`, el registro en `DependencyInjection.cs` y `OutlookEventContent.ImpactLabel`, ahora
    `internal` para compartir el texto del impacto con la invitación.
  - Infraestructura: `Itmas3.Infrastructure/Agenda/AgendaQueries.cs` (la consulta, sin unir usuarios, y `InRange`, que comparte con la
    lista). API: `Controllers/Agenda/AgendaEventsController.cs` (`GET /agenda-events/export.ics`).
  - Frontend: `core/services/file-download.ts` (descarga de un `Blob`), `features/agenda/data/agenda-api.ts` (`exportCalendar`),
    `data/agenda-state.ts` (`monthRange`, `exportTarget`), `events/agenda-actions.ts` (`exportCalendar`) y `page/agenda-page.*` (botón).
  - Documentación: `README.md` y `.env.example` (`AGENDA_PUBLIC_URL` también sirve al `.ics`); este §11, §12.1 y §17.
- Pruebas (incremento D): CA-19 con la API real y con *fixtures*. Unitarias (`Application/Agenda/IcalendarWriterTests.cs`, 53 casos, y
  `ExportCalendarHandlerTests.cs`): el texto completo de un evento con hora, de uno de día completo y de un calendario vacío; `;`, `,`, `\`
  y saltos de línea (`CRLF`, `LF`, `CR`, NEL, U+2028/U+2029) escapados; controles descartados; sustituto de un *surrogate* suelto; un valor que
  intenta inyectar `ATTENDEE`, `DTSTART`, `END:VEVENT` u `ORGANIZER` en cada campo (título, descripción, alcance, lugar, tipo y etiqueta)
  y se queda en su línea; `CATEGORIES` con el tipo y las etiquetas escapadas por separado y sin repetidos; `UID` estable aunque cambien
  título, revisión y exportación, y `SEQUENCE` creciente; `URL` con y sin `AGENDA_PUBLIC_URL` (P-27) y rechazo de direcciones no seguras;
  plegado a 75 octetos con ASCII, `ñ`, `€` y emoji sin partir una secuencia UTF-8; independencia de la cultura; el caso de uso fuerza
  Programado, pasa rango y filtros, nombra el archivo con fechas y devuelve «demasiados» sin escribir nada. Integración
  (`Api/Agenda/AgendaExportTests.cs`, 22 pruebas, y `export` en la matriz de `AgendaAuthorizationTests`): solo Programados (también uno
  vencido) del rango, con los eventos que lo cruzan y sin el día final; el filtro de estado no se aplica; tipo, responsable, etiquetas
  (todas), «mis actividades» y búsqueda (también por etiqueta); adjunto `text/calendar; charset=utf-8` sin BOM, `no-store`, `nosniff`; un
  evento con hora en UTC, el tipo y las etiquetas como categorías y las horas esperadas; uno de día completo con `DTEND` el día
  siguiente al último; texto escrito en la actividad con `;`, `,` y saltos de línea escapado (el título con saltos se rechaza al crear);
  `UID` estable y `SEQUENCE` creciente tras modificar; sin responsable, sin correos y sin `URL` sin dirección pública; `400` para rangos
  inválidos (faltante, vacío, invertido, 367 días, no fechas) y aceptado de 366; exactamente 1 000 eventos se exportan y 1 001 son un `400`;
  `401`/`403` y combinaciones de permisos de CA-20; exportar no audita ni cambia nada. Frontend (33 pruebas nuevas): descarga de un
  `Blob` (`file-download.spec.ts`), la API (`exportCalendar` con el rango y los filtros, sin `status`), rango y nombre del archivo
  (`agenda-state.spec.ts`), la acción (descarga y avisa; `400` con su consejo; `403`, `429`, `5xx` y sin red los explica el interceptor;
  el resto, mensaje general; falla del navegador) y el botón (visible con `agenda.read`, año o mes, filtros, nombre accesible, cargando,
  `aria-busy`, sin doble pulsación, reintento y aviso del filtro de estado).
- **Verificación del incremento D.** Backend: unitarias 667 y de integración 349, en verde; frontend: `npm test -- --watch=false` 471 en
  verde, `npm run build` sin avisos de presupuesto y `prettier --check` limpio. Se ejecutaron con los ejecutables de xunit v3
  (`tests/*/bin/Debug/net10.0/Itmas3.UnitTests` y `Itmas3.IntegrationTests`): en el entorno donde se implementó, `dotnet test` imprime
  «Zero tests ran» también sobre una copia limpia de `HEAD`. **No verificado:** el botón en el navegador (claro, oscuro y móvil), la
  descarga real de un archivo y su apertura en Outlook u otro calendario; ni el `.ics` contra un validador RFC 5545 externo.
- **Verificación documentada de CA-23 (parcial, 2026-10-18).** Con `ng serve` y una API **simulada** (no la real; no hay sesión de
  Entra ID en el entorno de desarrollo), en el navegador integrado: lista del año, detalle (programada con error de Outlook y
  completada con ejecución), nueva actividad con fecha y hora precargadas, nueva serie con vista previa y tipos, en tema claro y
  escritorio; detalle y formulario en tema oscuro; lista del año en pantalla estrecha (375 px) sin desplazamiento horizontal de la
  página. Encontró y corrigió: solapes de ayuda y error en los formularios, el nombre accesible de los meses del resumen, la
  interpretación de las fechas escritas a mano y un aviso de Angular por el adaptador de fechas. **No verificado:** recorrido
  completo con teclado, los diálogos de cancelar, registrar ejecución y tipo, las pantallas de edición y de edición de serie en el
  navegador, y todo contra la API real.
- Archivos principales (incremento C, tarea 10; solo backend):
  - Infraestructura: `backend/src/Itmas3.Infrastructure/Agenda/Graph/` (`GraphOutlookCalendarGateway`: el adaptador del puerto
    `IOutlookCalendarGateway`; `GraphEventPayload`: los cuerpos JSON, escritos a mano; `GraphTokenProvider`: `IGraphTokenProvider` y
    `MsalGraphTokenProvider`, el token de aplicación con MSAL; `GraphThrottleGate`: la espera de `Retry-After`; `GraphOutlookSettings`: la
    configuración, sin imprimir el secreto).
  - API: `Itmas3.Api/Agenda/AgendaModuleSetup.cs` (cliente HTTP tipado del adaptador y elección del puerto según `AGENDA_OUTLOOK_ENABLED`) y
    `Itmas3.Api/Observability/ObservabilitySetup.cs` (medidor `Itmas3.Agenda`, con la métrica `itmas.agenda.outlook.operations` que ya registra
    `OutlookPublisher`).
  - Dependencia nueva, **señalada para revisión de `architecture` y `security`**: `Microsoft.Identity.Client` 4.90.1 en
    `backend/Directory.Packages.props` y `Itmas3.Infrastructure.csproj`. Es la versión que `Microsoft.Identity.Web` ya resolvía en la API,
    así que el conjunto de paquetes que se distribuye no crece; hace falta la referencia propia porque `Infrastructure` no referencia
    `Microsoft.Identity.Web`. No se añadió el SDK de Graph. Los pines transitivos de `System.Security.Cryptography.*` de otra sesión no se tocaron.
  - Documentación: `ARCHITECTURE.md` (la salida a `graph.microsoft.com` con Outlook activo). Las variables de entorno ya estaban en
    `.env.example` y README; `SECURITY.md` y `DEVELOPMENT.md` (runbook) son de la tarea 11.
- Pruebas (incremento C): unitarias en `backend/tests/Itmas3.UnitTests/Infrastructure/Agenda/Graph/` y `Api/Agenda/AgendaOutlookCompositionTests.cs`.
  `GraphOutlookCalendarGatewayTests` (117 casos, con un `HttpMessageHandler` simulado y un doble del proveedor de token): CA-14 (asunto
  `[Tipo] Título`, cuerpo en texto plano con el marcado literal, categoría, recordatorio, invitados requeridos y opcionales, lugar, hora local
  con zona, `transactionId` = id del evento; las etiquetas no viajan: se comprueba el conjunto exacto de propiedades), P-24 (día completo a
  medianoche con fin exclusivo, aunque lleguen horas), CA-15 (`PATCH` con el estado completo y sin `transactionId`; `POST …/cancel` con el
  motivo; cancelar lo que ya no existe es éxito), CA-16 (cada estado → su código: 401, 403, 404, 408, 429, 5xx, 4xx y 3xx; recreación ante
  `404` con un `transactionId` estable ante reintentos; creación repetida tras un tiempo agotado con el mismo `transactionId`; tiempo
  agotado, también el del token; fallo de red y corte al leer la respuesta; la cancelación de quien llama no se confunde con un tiempo
  agotado), RNF-5 (`429` cierra la compuerta con `Retry-After` en segundos o fecha, valor por defecto, tope de 10 min, y mientras está cerrada
  no se envía nada ni se pide el token; `503` con `Retry-After` detiene el lote sin dejar de ser `graph_unavailable`), RNF-8 (los logs llevan
  operación, id y código, nunca título, correos, buzón, cuerpo de Graph, motivo, token ni secreto), ids y enlaces de Graph (solo caracteres
  de un id de evento y 300 como máximo; enlace solo `https`, sin credenciales y de hasta 1 000), el buzón escapado como un solo segmento de
  la ruta, que toda petición va a `graph.microsoft.com` por HTTPS, y, con los casos de uso reales y el adaptador sobre el manejador simulado:
  programar, el lote detenido ante `429` (el resto Pendiente y sin peticiones), un fallo que deja el evento Programado en Error y
  «Sincronizar de nuevo» sin duplicar, la recreación tras borrar el evento en Outlook y cancelar con el motivo. `MsalGraphTokenProviderTests`
  (14, MSAL contestando desde el manejador simulado): solo `login.microsoftonline.com`, ámbito `https://graph.microsoft.com/.default`, token en
  memoria reutilizado hasta que un `401` pide otro, credencial rechazada sin secreto ni detalle en los logs, `429` con su espera, el mapa de
  errores de la plataforma a los códigos cerrados y un tenant que MSAL no acepta → `not_configured` sin llamada. `AgendaOutlookCompositionTests`
  (5): con Outlook apagado el puerto es el «no configurado» y con Outlook activo el adaptador de Graph (un solo proveedor de token y una sola
  compuerta), con el límite de 10 s. Integración: `AgendaAuditAndConfigurationTests` ya no podía asumir que con Outlook activo no hay adaptador
  (llamaría a Microsoft): ahora comprueba la elección del puerto con el host real y, con un doble del puerto que falla, que programar no falla,
  que el estado es Error con `graph_unavailable` y que la auditoría no lleva secretos. `AgendaHarness` admite un puerto distinto del doble.
- **Verificación del incremento C (tarea 10).** `cd backend && dotnet test` (con Docker): 1 153 en verde (803 unitarias y 350 de integración;
  antes 667 y 349), sin avisos de compilación ni de NuGet Audit, e incluidas las de dependencias de capas, convención de autorización y
  catálogo de permisos. **No verificado (depende del tenant y es de la tarea 11 / CA-24):** ninguna llamada real a Graph ni a Entra ID; que
  el buzón acepte la zona IANA enviada (`America/Bogota` está en la lista de zonas de Graph, otra zona de `AGENDA_TIME_ZONE` fuera de ella
  daría `graph_rejected`); qué hace Graph con un `transactionId` de un evento ya borrado (de ahí la decisión 11); que `cancel` funcione en un
  evento sin invitados (no es una reunión y Graph puede responder `400`; hoy todo evento tiene al menos al responsable); el formato real de
  los ids y de `webLink` frente a las comprobaciones; el token con RBAC (`403` sobre otro buzón, CA-24); un `Retry-After` real; y el
  comportamiento tras un proxy de salida.
- **Defecto corregido: carrera al crear una etiqueta nueva (2026-10-18).** `AgendaSeriesTests.CA03_creating_the_series_saves_the_occurrences_in_draft_with_the_tags_of_the_series`
  falló de forma intermitente («Assert.Equal() Failure: Values differ») en 2 de unas 5 corridas de `cd backend && dotnet test` (solución
  completa, unitarias e integración a la vez) y nunca en 17 solo de integración o solo de esa clase.
  - **Causa.** No era un problema de aislamiento de las pruebas sino un defecto del producto. `AgendaTagResolver` busca y luego crea
    (`FindByKeysAsync` + `Add`) y `AgendaUnitOfWork` convertía en `409` la violación de `ux_agenda_tags_name_lower`. Tres pruebas de clases
    distintas, que xunit ejecuta en paralelo, usan la misma etiqueta fija `windows` (`AgendaEventsTests`, y dos en `AgendaSeriesTests`): si dos
    peticiones la creaban a la vez, la segunda recibía `409` en lugar de `201` y la aserción de `HttpStatusCode.Created` decía «Values differ».
    Dos personas que usan a la vez la misma etiqueta nueva no deben recibir error (RN-15).
  - **Evidencia.** El mensaje del fallo natural no se capturó: unas 50 corridas con el código anterior (12 de la solución completa, 20 de
    las tres clases de etiquetas y 12 de esas clases con la CPU saturada) no lo repitieron, y ningún registro mostró la violación del índice.
    La carrera sí se reprodujo de forma determinista contra el código anterior, con el mismo mensaje: una transacción retiene la etiqueta sin
    confirmar, la petición intenta crearla y espera, la transacción confirma, y la petición responde `Expected: Created / Actual: Conflict`;
    y 9 creaciones simultáneas de la misma etiqueta nueva: 8 respondieron `409`. Se descartaron el orden de `Order()`, la fecha relativa y los
    datos compartidos entre pruebas (cada prueba usa su propio responsable y cuenta solo sus eventos).
  - **Corrección** (`Itmas3.Infrastructure/Agenda/AgendaUnitOfWork.cs`). Si el `SaveChanges` falla por la violación de **ese** índice, la
    operación entera se repite (hasta 4 intentos en total) con una transacción nueva: ahora la etiqueta ya está confirmada, así que se
    encuentra y se reutiliza con la escritura de la primera vez (RN-15). Las demás violaciones de unicidad, y `xmin`, siguen siendo `409`.
    No hay cambios de esquema ni de contrato HTTP. La rama de reintento reejecuta el caso de uso con el `ChangeTracker` limpio, como ya lo
    hace `EnableRetryOnFailure`.
  - **Pruebas** (`backend/tests/Itmas3.IntegrationTests/Api/Agenda/AgendaTagConcurrencyTests.cs`, 3). La determinista: una conexión retiene una
    etiqueta sin confirmar con otra escritura, una serie con la misma etiqueta en mayúsculas espera (se comprueba con `pg_blocking_pids`),
    la conexión confirma, y la serie responde `201` con las ocurrencias etiquetadas con la escritura de la primera y una sola fila. Y 9
    creaciones simultáneas de eventos con tres escrituras de la misma etiqueta nueva: todas `201`, una sola fila y la misma escritura en todas.
    La tercera prueba que dos escrituras que PostgreSQL ve como la misma etiqueta («istanbul» e «İstanbul», con `TagName.Key` alineado con `lower()`) son una sola etiqueta. Sin la corrección fallan las dos primeras con `Expected: Created / Actual: Conflict`.
  - **Verificación.** 10 corridas completas seguidas de `cd backend && dotnet test` con la corrección: 1 155 pruebas (803 unitarias y 352 de
    integración) en verde las 10 veces.
  - **Observado y no corregido aquí** (fuera de esta corrección). Con la máquina muy cargada (dos suites a la vez) aparecieron fallos ajenos
    a las etiquetas: `SqlAuditPerformanceTests.RNF2_…` («First snapshot took 00:00:3x», SPEC-0005); `InventoryTests.CA15_…` («Failed to bind to
    address http://127.0.0.1:5000: address already in use», porque otra suite usaba el puerto); y un `RegexMatchTimeoutException` del
    `DomainPattern` de `AgendaOptionsValidator` (`AgendaOptions.cs`, `matchTimeoutMilliseconds: 100`), que con la CPU saturada hace fallar el
    arranque de la API de las pruebas. El último es un límite de 100 ms medido en tiempo real: conviene revisarlo (por ejemplo, con un
    límite mayor para una expresión sin retroceso) si las pruebas vuelven a ser intermitentes bajo carga.
- **Tarea 11, documentación (hecha) y prueba real (fuera de este cierre).** Sección de la agenda en `SECURITY.md` (controles, lista de verificación
  de producción), runbook en `DEVELOPMENT.md` §11 (prerrequisitos del tenant con sus comandos de RBAC for Applications y
  `Test-ServicePrincipalAuthorization`, guion de la prueba con un buzón de pruebas, diagnóstico de los códigos `graph_*` y rotación de la
  credencial) y las amenazas, activos y riesgos residuales en `specs/security/threat-model-baseline.md`. Los comandos de PowerShell son los de
  la documentación de Microsoft y **no se ejecutaron**: el equipo de infraestructura debe validarlos en el tenant. **No verificado (CA-24):**
  que el permiso acotado opere sobre el buzón configurado y responda `403` sobre otro; el solicitante decidió cerrar la spec sin esa prueba
  (2026-10-18) y queda como seguimiento, con el guion de DEVELOPMENT.md §11.3.
- **Pruebas (incremento C, integración HTTP).** `backend/tests/Itmas3.IntegrationTests/Api/Agenda/AgendaOutlookFlowTests.cs` (14 casos): la API real
  y PostgreSQL con Outlook activo y el puerto sustituido por un doble programable (ninguna llamada a Microsoft). CA-14: programar llama al puerto
  una vez con el contenido de RF-16 (asunto `[Tipo] Título`, categoría, hora local y zona, recordatorio, invitados requeridos y el equipo como
  opcional, cuerpo con descripción, alcance y enlace) y guarda el id y el enlace de Outlook; CA-15: modificar una programada actualiza el evento
  de Outlook con su id guardado y un borrador no llama a Outlook, cancelar una publicada la cancela con el motivo y una que nunca se publicó no
  llama; CA-16: cada uno de los ocho códigos de error deja el evento Programado con su código normalizado, un código ajeno al conjunto cerrado y
  una excepción del puerto no llegan al evento, a la respuesta ni a la auditoría, «Sincronizar de nuevo» tras un fallo publica con el mismo id de
  evento y después actualiza sin crear otra vez, y un lote detenido ante `429` deja el resto Pendiente y cada evento se publica una sola vez al
  sincronizar. «Sincronizar todo» no se usaba en integración porque recorrería también los eventos programados de las pruebas que corren en
  paralelo; desde el 2026-10-21 lo prueba un caso con el reloj del host en 2190 (ver «Pruebas añadidas tras las revisiones»).
- Archivos principales (incremento E, tareas 13 y 14; solo frontend, más el ADR):
  - ADR: `specs/architecture/adr-0017-fullcalendar-calendar-views.md` (resultado de la prueba técnica previa; `Accepted` desde el 2026-10-19) e índice en `specs/README.md`.
  - Dependencia: `frontend/package.json` y `package-lock.json` (FullCalendar 7.1.3, `@fullcalendar/angular` 7.1.3 y `temporal-polyfill` 1.0.6, versiones
    exactas; 8 paquetes nuevos, sin *scripts* de instalación, `npm audit` sin vulnerabilidades), `frontend/angular.json` (las dos hojas de FullCalendar
    en los estilos globales) y `README.md` (tabla de tecnologías).
  - `frontend/src/app/features/agenda/calendar/`: `agenda-calendar.ts/.html/.scss` (el componente, con el mapeo del tema a `--mat-sys-*`) y
    `calendar-model.ts` (vistas, ancla, adaptador de eventos), con sus pruebas.
  - `features/agenda/data/agenda-state.ts` (vista, día de la semana, rango visible, navegación y título del periodo; destino de la exportación de la
    semana) y `features/agenda/page/agenda-page.ts/.html` (selector de cuatro vistas, navegación por vista, `@defer` del calendario, aviso de periodo
    vacío, resumen solo en la lista).
  - `core/error-handling/global-error-handler.ts` (ignora el aviso benigno de `ResizeObserver` que provoca el calendario del año) y, por los solapes que
    la verificación en pantalla de 375 px encontró, `features/agenda/events/event-fields.ts` y `complete-dialog.ts` (`subscriptSizing="dynamic"` y
    separación entre campos).
  - Documentación: `ARCHITECTURE.md` (estructura de la agenda y de sus calendarios), `README.md`, `SECURITY.md`, `DEVELOPMENT.md` §11, el modelo de
    amenazas y el manual de uso (`consultar-agenda`, `etiquetar-y-filtrar`, `planificar-actividad` y `programar-y-cancelar`, con las vistas, la
    exportación `.ics` y lo que recibe Outlook).
- Pruebas (incremento E): frontend de 471 a **556** en verde. `calendar-model.spec.ts` (13: vistas, ancla, fechas locales sin desplazamiento, fin exclusivo
  del día completo, estado con clase, ícono y texto, colores como roles de Material, nombre accesible, texto sin marcado),
  `agenda-calendar.spec.ts` (27, con FullCalendar real en jsdom, sin zona: cuadrícula del mes de lunes a domingo con los días vecinos, actividad con hora, tipo,
  título y estado, varios días, «+n más», semana con siete columnas y la franja de todo el día, simultáneas, la que cruza la medianoche partida en
  dos, línea de la hora actual con reloj simulado, los doce meses, elegir un día abre su semana con ratón y con teclado, abrir el detalle, el estado manda
  la página y no se responde si ya está donde debe, planificar desde un día u hora vacíos solo con `agenda.create`, las horas de la organización con la
  zona del proceso cambiada a `Pacific/Auckland`, `Asia/Tokyo` y `America/Los_Angeles`, una columna en pantalla estrecha, solo lectura, texto sin marcado y
  nombres accesibles), `agenda-state.spec.ts` (32 en total: las cuatro vistas, el día de la semana, el rango visible de cada vista, Anterior, Hoy y Siguiente con
  cruces de año, cambio de vista que conserva lo posible y títulos), `agenda-page.spec.ts` (64 en total, 20 nuevas: selector, rango pedido a la API, resumen que no se pide,
  navegación con los nombres de cada vista, filtros que se conservan, la página sigue al calendario, detalle y «Nueva actividad» con día y hora, aviso de más de
  1 000, periodo vacío, exportar la semana y el mes) `global-error-handler.spec.ts` (5), `agenda-api.spec.ts` (los ids de la dirección, codificados) y `agenda-safety.spec.ts` (4, prueba estática: sin HTML de datos ni navegación por sí sola).
- **Verificación del incremento E (CA-23, CA-31 a CA-34).** Con `ng serve` y una API **simulada** (no la real), y con la compilación de producción servida con la CSP de
  nginx (`script-src 'self'`, `connect-src 'self'`) en el navegador integrado: Año, Mes y Semana en esquema **oscuro** (los tres) y **claro** (Mes en
  escritorio y Semana en 375 px); en 375 px, Semana como un día, Año en una columna y Mes sin desplazamiento horizontal; navegación Anterior, Hoy y
  Siguiente de cada vista, elegir un día del año (abre su semana) y el título de un mes (abre el mes), la dirección y el título siempre de acuerdo;
  recorrido con **Tab** (anillo de foco visible) y **Intro**; actividades simultáneas en paralelo, la partida por la medianoche, las de varios días como
  barras, la de todo el día en la franja superior, «+n más», hoy señalado, el estado con ícono y texto y su leyenda; **cero violaciones de CSP**. En CA-23
  también: detalle y los diálogos **Programar**, **Cancelar actividad**, **Registrar ejecución** y **Nuevo tipo de actividad** (foco inicial, trampa de foco
  y **Esc**), la pantalla de tipos, **Editar actividad** y **Nueva serie** en 375 px (sin desplazamiento horizontal). Encontró y corrigió tres defectos: el
  aviso de `ResizeObserver` se mostraba como «Se produjo un error inesperado»; el mensaje de periodo vacío del mes decía «Ninguna actividad coincide con los
  filtros» sin haberlos; y las ayudas de dos líneas se solapaban con el campo siguiente en 375 px. **No verificado:** la línea de la hora actual a
  simple vista (solo con reloj simulado), el contenido de la ventana de «+n más», lector de pantalla (NVDA, VoiceOver), Firefox y Safari, los estados de
  carga y de error de cada pantalla en la verificación de esta ronda y **todo contra la API real** (no hay sesión de Entra ID en el entorno de desarrollo).
- **Verificación de cierre.** `cd backend && dotnet test` (con Docker): **1 171** en verde (804 unitarias y 367 de integración) en dos corridas completas seguidas tras las
  correcciones de las revisiones, y tres corridas de 1 169 antes de ellas. En una corrida hecha mientras corría la suite del frontend falló solo
  `SqlAuditPerformanceTests.RNF2_…` («First snapshot took 00:00:32», SPEC-0005), que depende de la carga de la máquina; pasó en las otras. Frontend: `npm test -- --watch=false` **556** en
  verde (incluida `manual-consistency.spec.ts`), `npm run build` sin avisos de presupuesto (total inicial 534,4 kB; aviso en 550) y `prettier --check` limpio.
- **Decisiones tomadas al implementar que el solicitante debe confirmar** (§11.4; ninguna cambia un criterio de aceptación, pero
  matizan el texto de la spec):
  1. **Edición de serie (P-29).** La API no ofrece una consulta de las ocurrencias de una serie, así que la acción «Editar la
     serie» se ofrece **desde una ocurrencia abierta** (Borrador o Programada que no ha empezado) y precarga los datos de **esa**
     ocurrencia, no de «la próxima ocurrencia abierta». Desde una completada, cancelada o ya iniciada no se ofrece. Cubrir el caso
     completo pide un cambio en la API (por ejemplo, el identificador de la próxima ocurrencia abierta en el detalle).
  2. **Confirmación de la edición de una serie (§8.4).** El texto no lleva «{n}» ocurrencias porque la API solo informa cuántas
     actualizó **después** (`updated`): la confirmación dice «las ocurrencias de la serie que aún no han empezado» y el aviso
     posterior da el número.
  3. **«Notificar al equipo».** Se muestra siempre: la API no informa si `AGENDA_OUTLOOK_TEAM_ADDRESS` está configurada, que es lo
     que §12.3 pide para ocultarla.
  4. **Fechas escritas.** El formulario lee «24/11/2026» día primero (el adaptador nativo la rechazaba) y la semana empieza el lunes
     aunque el navegador diga otra cosa (P-22).
  5. **Botones de creación.** En un año pasado vacío no se ofrecen «Nueva actividad» ni «Nueva serie» (RN-16 impide crear en el
     pasado); la spec solo los pedía en la agenda vacía.
  6. **Precarga desde un día u hora vacíos (P-21).** «Nueva actividad» acepta `?date=aaaa-mm-dd&time=hh:mm`, listo para las vistas de
     calendario de la tarea 14.
  7. **Filtros del `.ics` (tarea 12).** §12.1 listaba `from`, `to`, `type`, `responsible` y `tags`; el endpoint admite además `mine` y
     `search` para que el archivo contenga lo que la persona está viendo (la tarea pedía «los mismos filtros que `GET /agenda-events`»).
     `status` no se admite: siempre son Programados, y si la lista tiene un filtro de estado distinto de Programada el botón avisa de
     que no se aplica.
  8. **Límite del `.ics`.** §14.1 fija 1 000 resultados para las consultas pero no dice qué pasa al exportar más: más de 1 000 eventos es
     un `400` («Acota el periodo o los filtros»), no un archivo cortado que parecería completo.
  9. **Contenido del `.ics`.** Horas en UTC (sin `VTIMEZONE`) y fechas con `DTEND` exclusivo en los de día completo; `UID` =
     `<id>@agenda.itmas` (dominio fijo, para que reimportar actualice); `DTSTAMP`, `LAST-MODIFIED` y `SEQUENCE`; `METHOD:PUBLISH`; `SUMMARY`
     como el asunto de Outlook (`[Tipo] Título`); `DESCRIPTION` con descripción, alcance e impacto; sin recordatorio (`VALARM`), sin
     organizador ni invitados, ni el nombre del responsable (RF-21 es para quien no está invitado). Un rango sin eventos descarga un
     calendario sin componentes, que RFC 5545 estrictamente no admite pero los clientes aceptan; se prefirió a un error.
  10. **Periodo del botón.** Exporta el periodo que muestra la lista: el año o, si está acotada a un mes (cuadro del resumen), ese mes. La
      tarea decía «el año de la lista».
  11. **Recrear un evento borrado en Outlook (tarea 10).** El ADR-0016 y §10.3 dicen que crear lleva el id del evento como `transactionId`.
      Así se hace en la primera creación y en sus reintentos. Al **recrear** tras un `404`, el `transactionId` se deriva (SHA-256) del id del
      evento y del id de Outlook que ya no existe: es estable ante reintentos de esa recreación (no duplica) y distinto del del evento borrado,
      porque no está documentado si Graph recuerda el de un evento eliminado (podría devolver el borrado). A confirmar con el buzón de
      pruebas de la tarea 11; si Graph no lo recuerda, se puede volver al id del evento.
  12. **Zona horaria.** Las horas viajan como hora local con el identificador IANA del evento (§10.5). Graph acepta una lista de zonas IANA,
      con `America/Bogota`, y advierte que crear o actualizar puede no admitirlas todas; una zona de `AGENDA_TIME_ZONE` fuera de la lista
      daría `graph_rejected`. No se hizo conversión a UTC para respetar §10.5.
  13. **`Retry-After` (RNF-5).** Una compuerta única por proceso recuerda la espera pedida por `429` y se aplica también a las siguientes
      peticiones: mientras dura no se llama a Graph y se contesta `graph_throttled` con el lote detenido. Sin cabecera, 30 s; tope de 10 min.
      Un `503` con `Retry-After` se trata igual (detiene el lote) pero conserva el código `graph_unavailable`. Sin reintentos automáticos.
  14. **Lo que se acepta de Graph.** Del cuerpo de una respuesta correcta solo se leen `id` y `webLink`, con un tope de 1 MiB; el `id` debe
      tener solo letras, dígitos, `-`, `_`, `=` y `+` y hasta 300 caracteres (la columna), pues se vuelve a usar en una ruta; un `webLink` que no
      sea `https`, lleve credenciales o pase de 1 000 caracteres no se guarda (el evento se publica igual).
  15. **Registro y trazas (RNF-8).** El cliente HTTP del adaptador es un cliente tipado registrado en el módulo de la API (`Infrastructure` no
      referencia `Microsoft.Extensions.Http`), sin redirecciones ni cookies y sin el registro de URL de `IHttpClientFactory`, porque la ruta
      de cada llamada lleva la dirección del buzón. Por lo mismo, los *spans* de Graph no se exportan (`ObservabilitySetup`); la métrica
      `itmas.agenda.outlook.operations` y las métricas HTTP siguen. Los logs del adaptador incluyen, además de operación, id y código, el
      `request-id` de Graph (solo si es un GUID) para el soporte de Microsoft.
  16. **MSAL.** El descubrimiento de instancia está desactivado: la autoridad es el host público fijo `login.microsoftonline.com`, no algo por
      validar, y así se ahorra una llamada. El token vive en la memoria de MSAL; un `401` de Graph hace que el siguiente pida uno nuevo. Un
      tenant o credencial que MSAL no acepta da `not_configured`, no un error de la petición.
  17. **Elección del puerto.** El registro condicionado a `AGENDA_OUTLOOK_ENABLED` se resuelve al pedir el puerto, con las opciones ya
      validadas (no al leer la configuración al componer); con Outlook apagado no se construye nada de Graph ni de MSAL.
  18. **La semana se dirige por su día.** `view=week&date=aaaa-mm-dd`, sin `year` ni `month` (el año sale del día); en pantalla estrecha, la semana
      se ve de un día y el parámetro es ese día, y «Anterior» y «Siguiente» mueven un día. El mes de la vista Mes y el del filtro de la lista comparten el
      parámetro `month`.
  19. **Año en pantalla estrecha.** Con los meses en una sola columna, FullCalendar no convierte el título del mes en enlace; se llega al mes con el
      selector. En pantalla ancha, el título abre el mes y el día abre su semana (CA-31).
  20. **Planificar desde un día pasado.** ~~Un día u hora vacíos ya pasados abren «Nueva actividad» y el formulario responde con RN-16.~~ **Cambiada por el
      solicitante el 2026-10-21:** en los días ya pasados (y en las horas ya pasadas de hoy, en la zona de la organización) la acción no se ofrece; el
      resto de días y horas sigue abriendo «Nueva actividad» con la fecha precargada. RN-16 sigue protegiendo en el servidor.
  21. **Hojas de FullCalendar en los estilos globales** (+3,1 kB transferidos para toda la aplicación), porque el presupuesto de estilos de un componente
      (8 kB) no las admite; el código del calendario sí se carga diferido (ADR-0017).
  22. **Exportar la semana.** En la vista Semana, «Exportar .ics» exporta de lunes a domingo y el archivo se llama `agenda-semana-aaaa-mm-dd.ics`.
  23. **El resumen del año solo acompaña a la lista del año** (RF-13); los calendarios no piden `/agenda-events/summary`.
  24. **Aviso de `ResizeObserver`.** El manejador global de errores lo ignora (solo ese aviso, con prueba), en vez de mostrar «Se produjo un error inesperado».
  25. **Cierre sin CA-24.** El solicitante decidió (2026-10-18) cerrar sin la prueba real con el tenant (tarea 11 y CA-24). **El 2026-10-21** decidió
      trasladarlas a [SPEC-0008](SPEC-0008-outlook-activation.md) por enmienda (§19.8), para que esta spec quede `Implemented` sin excepciones a la
      constitución §9.2; Outlook no se activa en producción sin cumplir SPEC-0008.
  26. **Texto del estado en los calendarios.** En cada actividad dibujada, el estado va como ícono y color; su texto está en la leyenda visible de encima, en
      el recuadro que aparece al pasar el puntero (título, tipo, estado y cuándo) y, oculto a la vista, en el nombre accesible que lee el lector de pantalla.
      No se escribe en cada actividad porque no cabe en la cuadrícula; la lista del año sí lo muestra en una columna.
  27. **Teclado en los calendarios.** Los días y las actividades se recorren con **Tab** y se abren con **Intro**; planificar desde un día u hora vacíos (RF-12b)
      solo existe con el puntero: con teclado se usa **Nueva actividad** y se escribe la fecha. La vista Año tiene un punto de tabulación por día (365 o más): la
      lista del año es la alternativa completa y accesible (RNF-10).
  28. **La lista del año siempre se abre con todo el año:** al pasar del Mes a la lista no se arrastra el mes como filtro (CA-30).
  29. **El año por defecto es el actual en la zona de la organización**, no en la del navegador (RNF-9): el día del cambio de año no depende de dónde esté la persona.
- **Revisiones obligatorias pendientes** (*atendidas el 2026-10-21: ver «Revisiones obligatorias del cierre» más abajo*): `architecture` (puertos nuevos de
  `Application`, `Infrastructure/Agenda`, `Dockerfile`, variables de entorno); `security` (API: autorización, límite de frecuencia,
  validación y errores; ADR-0016; `tzdata` en la imagen; variables de entorno; y, del frontend, enlaces de evidencia y de Outlook,
  texto sin HTML y rutas); `backend` (contratos HTTP que usa el frontend); `database` y `backend` entre sí por `Persistence/` y la
  migración; `testing` (pruebas de los incrementos A, B y D). Del incremento D: **`security` sobre la generación del `.ics`** (§14.3: escape y
  plegado, límites de rango y de cantidad, que no lleve personas, la propiedad `URL`, la cabecera de adjunto y la descarga por `Blob`
  desde el navegador); `architecture` (el método nuevo del puerto `IAgendaQueries` y `FileDownload` en `core/`). De la corrección de la
  carrera de etiquetas (arriba): **`database` y `backend`** sobre el reintento de la unidad de trabajo ante `ux_agenda_tags_name_lower`
  (que solo se reintente ese índice, que `MaxAttempts` = 4 sea suficiente y que repetir el caso de uso con el `ChangeTracker` limpio sea
  seguro) y `testing` sobre `AgendaTagConcurrencyTests`.
- **Revisiones obligatorias pendientes del incremento C (tarea 10)** (*atendidas el 2026-10-21*): **`security`** sobre el adaptador
  de Graph (solo `login.microsoftonline.com` y `graph.microsoft.com`, rutas fijas, escape del buzón y del id, sin redirecciones; qué se registra
  y qué no; las decisiones 11 a 16), la credencial (secreto con caducidad corta frente a certificado, ADR-0016 «Seguimiento»), el alcance con
  RBAC (CA-24) y los límites; **`architecture`** sobre la dependencia nueva `Microsoft.Identity.Client` 4.90.1 (constitución §12.5), el cliente
  HTTP tipado en el módulo de la API y `Infrastructure/Agenda/Graph`; **`testing`** sobre las pruebas del adaptador y lo que queda del incremento
  C (integración HTTP con el doble del puerto: programar, modificar, cancelar, sincronizar de nuevo; CA-24). Sigue pendiente la revisión de
  `security` del ADR-0016.
- **Revisiones obligatorias pendientes del incremento E y de la tarea 15** (*atendidas el 2026-10-21*): **`security`** sobre la cadena de suministro de
  FullCalendar (8 paquetes nuevos, versión de 2 días de publicada) y la CSP, y sobre `SECURITY.md`, el modelo de amenazas y el runbook de DEVELOPMENT.md §11
  (los comandos de Exchange Online no se ejecutaron); **`architecture`** sobre la dependencia nueva, el `@defer` y las hojas globales (el ADR-0017 está `Accepted` desde el
  2026-10-19, con el riesgo residual de la cadena de suministro); **`frontend`** sobre los calendarios y el estado en la dirección; **`testing`** sobre las pruebas del incremento E y de
  `AgendaOutlookFlowTests`. El manual público (cuatro procesos cambiados) también lo revisa `security`: se buscaron direcciones IP, correos y `@`, GUID y números de
  versión en los seis archivos de la categoría y **no hubo coincidencias**; se revisaron a mano nombres de buzón, del tenant, secretos y datos personales:
  **ninguno**.
- **Revisiones independientes asistidas del cierre (2026-10-18).** Dos agentes de solo lectura revisaron lo nuevo sin conocer las conclusiones de quien lo implementó: uno
  con la lente de `security` y otro con las de `testing`, `frontend` y `backend`. **No sustituyen** las revisiones obligatorias de arriba, que siguen pendientes.
  - *Seguridad:* ningún hallazgo crítico ni alto. Se corrigió: el encabezado `## 2. Autorización` de `SECURITY.md`, que mi edición había borrado; el runbook
    (el calendario se comparte con un **grupo de seguridad habilitado para correo**, no con una lista de distribución; el consentimiento en Entra ID no lo ve
    `Test-ServicePrincipalAuthorization` y hay que revisarlo y limitar los propietarios de la aplicación); la colisión de la «İ» (U+0130) entre `TagName.Key` y
    `lower()` de PostgreSQL, que habría repetido cuatro veces una petición que acaba en `409`; los ids de la dirección en las rutas del cliente (`encodeURIComponent` y
    sin segmentos `.` ni `..`); y se añadió una prueba estática que prohíbe `innerHTML`, `bypassSecurityTrust*` y la forma `html:` de FullCalendar en la feature. **Queda
    para una persona:** aceptar el riesgo residual de la cadena de suministro de FullCalendar (7 de 8 paquetes con un solo mantenedor y sin atestaciones; ver ADR-0017).
  - *Corrección y pruebas:* se corrigieron una prueba mía que no ejercitaba lo que decía (el delegado que lanzaba sobrescribía al del código ajeno), la dependencia de la
    hora de `Future(10)` tras la petición, pruebas débiles del calendario (el salto de año, la espera fija, la zona de la organización con un reloj en UTC, que ahora falla
    si se quita la opción `timeZone`), el contraste del texto de una actividad cancelada, el clic con Ctrl, Cmd o Shift (ya no se impide: se abre en otra pestaña),
    los enlaces de día de la vista Semana (ya no se ofrecen: reescribían la dirección sin cambiar lo que se ve), la lista del año que arrastraba el mes, el año por
    defecto en la zona del navegador, las opciones que se reconstruían en cada cambio y tres componentes en un mismo archivo (`status-indicator.ts`, que viola «un
    componente por archivo», §12.3 de la constitución). Se anotaron como decisiones 26 a 29 el texto del estado, el teclado, la lista del año y el año por defecto.
    **No se hizo:** probar a nivel de página las etiquetas «Día anterior» y «Día siguiente» de pantalla estrecha (jsdom no tiene `matchMedia`; se vieron en el
    navegador), y aislar con días únicos las pruebas de integración que miran advertencias de solapamiento, que comparten base de datos y hoy pasan porque nadie más
    usa esas fechas (`AgendaEventsTests`, `AgendaSeriesTests`): un caso futuro que programe un congelamiento en esos días las rompería.
- **Manual de uso (adelantado a petición del solicitante, 2026-10-18).** Procesos nuevos en la categoría «Agenda de
  infraestructura»: `consultar-agenda`, `etiquetar-y-filtrar`, `planificar-actividad`, `programar-y-cancelar`,
  `registrar-ejecucion` y `gestionar-tipos-de-actividad`; cambian `consultar-auditoria` y `catalogo-de-permisos` (permisos
  `agenda.*`). Solo explican lo que existe tras el incremento B: la **lista del año**, los formularios, el detalle y los tipos.
  Quedan para la tarea 15 las vistas de calendario, la publicación en Outlook (incremento C) y la exportación `.ics`
  (incremento D); `programar-y-cancelar` solo dice que, con el aviso «Outlook no configurado», programar cambia el estado.
  Revisión de contenido público: se buscaron direcciones IP, correos y `@`, GUID y números de versión en los ocho archivos y en
  las líneas nuevas de `index.json`: **sin coincidencias**; se revisaron a mano nombres de buzón, del tenant, secretos y
  datos personales: **ninguno**. Pendiente de `security` por ser público.
- **Revisiones obligatorias del cierre (2026-10-21).** Cuatro agentes de solo lectura, independientes entre sí y de quien implementó, cada uno con
  la lista de `agents/` de su rol: `architecture`; `security`; `database` y `backend` entre sí; y `testing`. Ninguno encontró hallazgos críticos. Lo
  que se hizo con cada hallazgo (las decisiones del solicitante están en §19.8):
  - *`security`* (ningún hallazgo crítico ni alto). **F-1** (media), el límite de avisos se gastaba antes de autorizar y 60 peticiones anónimas
    bloqueaban a todos: **corregido**, el límite es un `FixedWindowRateLimiter` único del proceso (`Itmas3.Api/Agenda/AgendaPublishLimit.cs`) que
    gasta un filtro de acción, después de la sesión, la cabecera anti-CSRF y los permisos, y responde `429` con `Retry-After`
    (`TooManyRequestsException` en `Application/Common/Exceptions` y su traducción en `GlobalExceptionHandler`); desaparece la política de la
    canalización. **F-2** (media), con solo `agenda.update` se modificaba lo Programado y se avisaba a sus invitados: **corregido por enmienda**
    (§7, RF-5, RF-6, CA-35) con `Application/Agenda/Outlook/AgendaNotificationGuard.cs`, que los casos de uso de modificar evento y serie llaman
    dentro de la transacción; en la interfaz, "Editar" sobre lo Programado solo se ofrece con `agenda.publish`. **F-3** (baja), el límite cuenta
    peticiones y no destinatarios: **riesgo aceptado** (§19.8). **F-4** (baja), el responsable se invitaba aunque su dirección no fuera de un
    dominio permitido (cuenta invitada B2B): **corregido**, `OutlookEventContent` solo lo invita si su dominio está en la lista. **F-5** (baja), el
    cambio de nginx (`real_ip` y `X-Forwarded-Proto`) es **ajeno a esta spec**: no viaja en su commit y queda para quien lo hizo. **F-6**
    (documental): **corregido** en el modelo de amenazas (cadena de suministro de FullCalendar), ADR-0016 (pasar a certificado exige código) y
    ADR-0017 (`@fullcalendar/core` sí se instala). Lista de salida: `npm audit` daba una vulnerabilidad alta de `vite` 8.3.0 anidado bajo
    `@angular/build`, solo de desarrollo: **corregida** con un `override` en un commit `minor:` aparte. La lectura de la constitución que hace
    ADR-0016 es correcta y no requiere enmienda.
  - *`architecture`*. **A-1** (nginx) y **A-2** (fijaciones transitivas de NuGet y su párrafo de DEVELOPMENT.md §7): ajenos a esta spec; las
    fijaciones van en un commit `minor:` aparte y nginx queda fuera. **A-3** (la spec estaba `Implemented` con revisiones pendientes): atendido
    con este registro y con `/sdd-validate`. **A-4** (el adaptador de Graph se registra en la API y obliga a hacer públicos tipos de
    `Infrastructure`): **documentado** como desviación aceptada en ARCHITECTURE.md §8. **A-5** (`outlookEnabled` sale del puerto de lectura
    `IAgendaQueries`, que así depende de la configuración): **aceptado** como deuda menor; se moverá al controlador si el puerto cambia. **A-6**:
    el filtro de etiquetas **corregido** (normaliza con `TagName`, también espacios repetidos y la «İ»); la regla de «vencida» sigue escrita en las
    consultas porque EF Core tiene que traducirla a SQL (`AgendaEvent.IsOverdue` no es traducible), con la misma definición. **A-7** (FullCalendar
    fija tamaños de letra y sombras fuera del tema): **documentado** como limitación aceptada en ADR-0017. La documentación desactualizada que
    señaló (README, `.env.example`, `docker-compose.yml`, ARCHITECTURE.md y estas líneas de la spec) se **corrigió**.
  - *`database` ↔ `backend`*. **H1** (media), una cancelación o una edición mientras Outlook crea el evento dejaba la invitación viva o la creaba
    sin el cambio: **corregido**, si al guardar la creación el evento ya está Cancelado se envía la cancelación con el id recién recibido, y una
    edición no lanza una segunda creación mientras la primera está en camino (queda Pendiente y la sincroniza «Sincronizar de nuevo»). **H2** (las
    consultas por rango recorrían todo el histórico anterior): **corregido** con una cota inferior de `starts_at_utc` (duración máxima + 1 día).
    **H3**, **H4** y **H5**: **documentados** como límites conocidos en §12.1. **H6**: corregido con A-6. **H7** (unicidad sin mayúsculas con
    acentos): **probado** con PostgreSQL 18 de la pila («Producción»/«PRODUCCIÓN» al unificar, filtrar y buscar). **H8** (`outlook_status` sin valor
    por defecto): **documentado** en §13. **H9** (un `PUT` de tipo sin `isActive` lo reactivaba): **corregido**, conserva el valor actual. **H10**
    (la auditoría de la serie no guardaba el antes): **corregido**, `before` lleva los valores anteriores de cada ocurrencia cambiada. **H11** (una
    relectura nula daba `500`): **corregido**, responde `404`. Lista de salida: `has-pending-model-changes` limpio; `docker compose down -v` **no
    se ejecutó** por decisión del solicitante (solo `up --build`).
  - *`testing`* (sus hallazgos se citan aquí como T-H*n*). **T-H1** (el `.ics` de 366 días dependía del orden): filtra por un responsable propio.
    **T-H2** (conteos de advertencias globales): las pruebas miran solo las advertencias de sus propios eventos o usan días propios en 2181.
    **T-H3** («Sincronizar todo» nunca ejecutaba la consulta SQL real): prueba nueva con Outlook activo, el doble del puerto y el reloj del host en
    2190 (pendiente, error, no publicado, publicado desactualizado, al día, borrador, ya empezado y 51 eventos en dos tandas). **T-H4** (las de
    rendimiento medían en paralelo): corren solas en `PerformanceCollection`. **T-H5**: el tiempo máximo de la expresión regular del arranque pasó de
    100 ms a 1 s. **T-H6**: se comprueban el `200`, `updated` y el título. **T-H7**: renombrada. **T-H8** (las de RNF-9 pasaban por casualidad en una
    máquina de Bogotá): toda la suite del frontend corre en `Asia/Tokyo` (`src/test-setup.ts`) y las que cambian la zona comprueban que el cambio surtió
    efecto. **T-H9** y **T-H10** (dependían de la hora del reloj): la actividad aceptada empieza en 5 minutos y la serie sembrada tiene su ocurrencia
    empezada una hora antes de ahora, más un borrador futuro. **T-H11**: prueba nueva del resumen con etiquetas, «Mis actividades» y búsqueda.
    **T-H12**: pruebas del dominio del buzón como lista por defecto. **T-H13**: pruebas de RN-7 entre Completada y Cancelada. CA-24: trasladado a
    SPEC-0008.
- **Pruebas añadidas tras las revisiones (2026-10-21).** Unitarias: `Application/Agenda/NotificationRuleTests.cs` (F-2 en evento y serie, con y sin
  `agenda.publish`, con el límite agotado y en borradores; F-4; H1 con un doble del puerto que retiene la creación; H9),
  `Api/Agenda/AgendaOutlookCompositionTests.cs` (+3: dominio del buzón, dominios explícitos y presupuesto único con `Retry-After`) y
  `Domain/Agenda/AgendaEventTests.cs` (+1, RN-7). Integración: `AgendaAuthorizationTests` (+3: F-1 con peticiones anónimas, sin permiso y sin
  cabecera anti-CSRF; F-2 con `403`/`200` y su mensaje; F-2 gastando el límite), `AgendaTagsTests` (+1, H7), `AgendaOutlookFlowTests` (+1, T-H3),
  `AgendaQueryTests` (+1, T-H11) y los ajustes de T-H1, T-H2, T-H4, T-H6, T-H7, T-H9 y T-H10. Frontend: F-2 en la lista, el detalle y la página de
  edición (+3), la zona de la suite y las guardas de zona del calendario, y la regla de los días pasados (decisión 20).
- **Verificación tras las revisiones (2026-10-21).** `cd backend && dotnet build`: sin avisos. `dotnet test` (con Docker): **1 193** en verde
  (819 unitarias y 374 de integración), con las de rendimiento corriendo solas; las 256 de integración de la agenda, en verde también por
  separado. Frontend (en `Asia/Tokyo`): `npm test -- --watch=false` **560** en verde en diez corridas (tres de ellas con la máquina cargada) y,
  en otra, **un fallo que no se repitió ni se identificó** (no se guardó su salida); `prettier --check` limpio; `npm run build` sin avisos de presupuesto (inicial
  534,4 kB); `npm audit` sin vulnerabilidades tras el `override` de `vite` (commit `minor:` aparte). `docker compose config -q` válido y
  `docker compose up --build`: migrador terminado con 0, `api` y `frontend` sanos, y las rutas de la agenda responden `401` sin sesión a
  través de nginx (la imagen del frontend incluye el cambio ajeno de nginx del árbol de trabajo). **No verificado:** `docker compose down -v`
  (por decisión del solicitante); las pantallas de la regla F-2 y de la decisión 20 en el navegador (solo con pruebas unitarias; verificadas el 2026-10-23, abajo); y todo
  contra la API real con sesión de Entra ID, que no existe en el entorno de desarrollo. Queda para la persona: `/sdd-validate SPEC-0006`.
- **`/sdd-validate SPEC-0006` (2026-10-22).** Comandos de la tabla de AGENTS.md en verde (backend 1 193, frontend 560 y build, Compose y
  `docker compose up --build` sanos). Resultado: **validación incompleta**, por CA-23 (la verificación documentada dejaba fuera los estados de
  carga y error) y porque no se habían visto en el navegador las pantallas de F-2 ni de la decisión 20; además, `npm audit` y
  `has-pending-model-changes` no están en la tabla de comandos, y `docker compose down -v` no se ejecuta por decisión del solicitante. Lo
  primero se atendió en la verificación siguiente.
- **Verificación de CA-23, F-2 y la decisión 20 (2026-10-23).** Con `ng serve` y una API **simulada** (no la real: no hay sesión de Entra ID en
  el entorno de desarrollo) que puede responder lento (5 s), con error (`500`), vacío y con una sesión sin `agenda.publish`, en el navegador
  integrado, con el reloj del equipo (23-oct-2026, 12:22 en Bogotá):
  - *Carga (CA-23):* barra de progreso con nombre accesible en la lista del año y los calendarios («Cargando actividades»), el detalle («Cargando
    la actividad»), la edición, la nueva serie y los tipos («Cargando los tipos»).
  - *Error (CA-23):* «No se pudo cargar la agenda.» en la lista y en Mes, «No se pudo cargar la actividad.» en el detalle y en la edición, «No se
    pudieron cargar los tipos de actividad.» en nueva actividad, nueva serie y tipos, cada uno con **Reintentar**; el aviso general del
    servidor con su referencia; y **Reintentar** recupera la pantalla de tipos al volver la API.
  - *Vacío (CA-23):* «Todavía no hay actividades planificadas.» con «Nueva actividad» y «Nueva serie» en la lista; «No hay actividades en este
    periodo.» en Mes; «Todavía no hay tipos de actividad.» en tipos. En tema **oscuro** y a **375 px**, el error de la lista y el aviso de F-2 sin
    desplazamiento horizontal. En la consola, solo los `500` provocados.
  - *F-2:* sin `agenda.publish`, en una actividad Programada el detalle no ofrece «Editar», «Editar la serie», «Cancelar» ni «Sincronizar de
    nuevo» (sí «Editar etiquetas», que es de `agenda.update`); su menú en la lista solo tiene «Ver» y «Registrar ejecución»; y la dirección de
    edición muestra «Esta actividad está programada. Para modificarla necesitas también el permiso para programar y cancelar actividades.» con
    «Volver al detalle», sin formulario. Un borrador ofrece «Editar», «Editar la serie» y «Eliminar borrador» (sin «Programar»). Con el permiso,
    la Programada vuelve a ofrecer «Editar».
  - *Decisión 20:* en Mes, un día pasado vacío (15-oct) no hace nada; hoy (23-oct) y un día futuro (27-oct) abren «Nueva actividad» con la fecha.
    En Semana, una hora ya pasada de hoy y una de ayer no hacen nada; las 14:00 de hoy abren «Nueva actividad» con fecha y hora.
  - **Encontró y corrigió dos defectos:** el **resumen del año** mostraba ceros y «Sin actividades en este año.» mientras cargaba o si fallaba, y
    los números de la consulta anterior mientras llegaba la nueva: ahora se vacía en cada consulta y dice «Cargando el resumen…» o «No se pudo
    cargar el resumen del año.», con los meses sin números (`components/year-summary.ts`, `page/agenda-page.*`; pruebas en
    `year-summary.spec.ts` y `agenda-page.spec.ts`). Y el **ícono de los avisos** de la agenda se encogía hasta casi desaparecer cuando el texto
    ocupaba varias líneas en pantalla estrecha (`agenda-page.scss`). Frontend: `npm test -- --watch=false` **562** en verde, `prettier --check`
    limpio y `npm run build` sin avisos de presupuesto.
  - **Observación para el solicitante:** elegir **hoy** en la vista Mes precargaba las 09:00 aunque ya hubieran pasado; al guardar, la API
    respondía con RN-16. **Decidida el 2026-10-24 (§19.8, D-8) y corregida:** se propone la hora actual más una, en la zona de la organización
    (`plannedStart` en `data/agenda-time.ts`, usada por la página de la agenda; pruebas en `agenda-time.spec.ts` y `agenda-page.spec.ts`).
    «Nueva actividad» abierta desde el botón, sin día elegido, sigue proponiendo las 09:00 del día que se escriba: el formulario no conoce
    la zona de la organización.
  - **No verificado:** todo contra la API real; la CSP en esta ronda (`ng serve` no la aplica; se comprobó con nginx en el incremento E); lector
    de pantalla, Firefox y Safari.

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
spec aprobada. **No quedan preguntas abiertas.** La concreción posterior de §13 y §16 añade las propuestas P-23 a P-30
(§19.7), que el solicitante aceptó el 2026-10-17.

Quedan como verificaciones durante la implementación:
- **ADR-0016** (aceptado por el solicitante el 2026-10-18): falta la revisión de `security`; la tarea 10 ya no espera al ADR.
- **Prueba técnica previa de FullCalendar** (tarea 13) y su **ADR-0017**; si falla, se pasa a componentes propios (§10.4).
- **Prerrequisitos del tenant** (§14.4): los realiza el equipo del solicitante antes de la tarea 11. Hace falta el nombre
  de la lista de distribución del equipo (Q-2).
- **Valores iniciales de los tipos** (P-16): se ajustan en el catálogo si los avisos habituales del equipo son otros.

### 19.7 Concreción de §13 y §16 (2026-10-17): propuestas aceptadas

El solicitante pidió completar §13 y §16 antes de implementar. Las decisiones técnicas que se derivan de la spec y del
código existente quedan escritas en esas secciones. Las que **añaden una regla, una variable o un límite que la spec no
tenía** (constitución §11.4) se listan aquí. **El solicitante las aceptó todas (P-23 a P-30) el 2026-10-17** y con ello
§13 y §16 son definitivas.

| # | Propuesta | Por qué |
|---|---|---|
| P-23 | **Orden de la cadena backend**: dominio (2) → casos de uso (3) → migración (4) → repositorios (5) → API (6) → entorno (7). La publicación a Outlook se orquesta en `Application` desde la tarea 3 con un doble del puerto; la tarea 10 solo aporta el adaptador, la métrica y el registro. | El borrador ponía la migración antes del dominio, pero `dotnet ef` necesita las entidades y `database` no puede modificar `Domain` (`agents/database.md`). Así el incremento A prueba la lógica de publicación sin Graph. |
| P-24 | **Eventos de día completo**: en la base se guardan con fin exclusivo (00:00 del día siguiente al último); la API los recibe y los devuelve con el **último día inclusivo** (RN-2) y cada consumidor (Graph, `.ics`, FullCalendar) convierte al fin exclusivo que esperan. | Graph, iCalendar (`DTEND`) y FullCalendar usan fin exclusivo; RN-2 pide inclusivo hacia la persona usuaria. Hay que fijar el contrato una vez, en la tarea 6. |
| P-25 | **Recordatorio**: de 0 a 40 320 minutos (4 semanas) en eventos y tipos; fuera de ese rango, `422`. | La spec no acota el valor. El máximo se confirma contra Outlook en la prueba de la tarea 11 y se ajusta si el buzón de pruebas lo rechaza. |
| P-26 | **Límite de frecuencia** de `schedule`, `cancel` y `outlook-sync`: variable nueva `AGENDA_PUBLISH_REQUESTS_PER_HOUR` (`Agenda__PublishPermitLimitPerHour`), **60 por hora** y cuenta global del proceso, `429` al excederlo. Se añade a §12.3, `.env.example` y README en la tarea 7. | §14.1 exige un "límite de frecuencia global" contra el envío masivo de invitaciones, pero §12.3 no define valor ni variable. Con 50 eventos por solicitud, 60 solicitudes por hora bastan para un uso normal. |
| P-27 | **`AGENDA_PUBLIC_URL`** es obligatoria (URL absoluta `https`) cuando `AGENDA_OUTLOOK_ENABLED=true`, porque el cuerpo de la invitación lleva el enlace a la actividad (RF-16); sin Outlook es opcional y, si falta, el `.ics` omite la propiedad `URL`. | §12.3 la declara fuera del grupo `AGENDA_OUTLOOK_*`, así que CA-18 no dice si su ausencia impide arrancar. |
| P-28 | **Zona horaria**: la API valida al arrancar que `AGENDA_TIME_ZONE` se resuelve en la imagen (Alpine; si falta la base de zonas se añade `tzdata` al `Dockerfile`). Cambiar la zona con eventos existentes **no se soporta en la v1**: cada evento conserva la zona con la que se guardó y se documenta en `DEVELOPMENT.md`. | La spec dice "configurable" (RNF-4) sin decir qué pasa con lo ya guardado; recalcular fechas de eventos ya enviados a Outlook sería un cambio de plan para todo el equipo. |
| P-29 | **Edición de serie** (RF-6): el formulario se precarga con los datos de la **próxima ocurrencia abierta** (Borrador o Programada que no ha empezado); si no hay ninguna, la acción no se ofrece. | RN-12 hace que la serie guarde solo su regla, de modo que los campos comunes viven en las ocurrencias. |
| P-30 | **Incremento B** sin las vistas de calendario: hasta la tarea 14 el selector solo ofrece "Lista del año"; las otras tres aparecen al integrar FullCalendar. | Permite entregar y probar B sin depender del resultado de la prueba técnica previa (tarea 13). |

Aplicado al aceptarlas: se quitó de los encabezados de §13 y §16 la marca de concreción pendiente y se incorporaron a
§12.3 la variable de P-26 y la nota de P-27. La implementación sigue con `/sdd-implement SPEC-0006`.

**Surgida al implementar la tarea 8 (2026-10-18):**

| # | Propuesta | Por qué |
|---|---|---|
| P-31 | **Selector del responsable**: ruta nueva `GET /agenda-people` (§12.1), abierta por `agenda.read`, que lista usuarios activos por nombre (sin correos) y marca a quien consulta; y la bandera `outlookEnabled` en la lista y el detalle de eventos. **El solicitante aceptó la ruta el 2026-10-18** (opción "Endpoint de agenda"). | RF-3 exige un responsable, pero la spec no decía de dónde salen los usuarios, y `GET /users` exige `users.read`, que quien planifica no tiene por qué tener. §8.4 pide confirmar "Programar" y mostrar "Outlook no configurado" sin que la interfaz pudiera saber si Outlook está activo. |

### 19.8 Revisiones del cierre y enmienda (2026-10-21)

Tras las revisiones obligatorias (§18, «Revisiones obligatorias del cierre»), el solicitante decidió:

| # | Tema | Decisión del solicitante | Efecto en la spec |
|---|---|---|---|
| D-1 | Decisiones 1 a 29 de §18 | Las confirma todas, con un cambio en la 20. | Ninguno, salvo la decisión 20. |
| D-2 | Planificar desde un día pasado (decisión 20) | **Ocultar** la acción en los días y las horas que ya pasaron. | Decisión 20 cambiada; manual (`consultar-agenda`, `planificar-actividad`). |
| D-3 | Modificar lo ya Programado (hallazgo F-2) | **Exigir `agenda.publish`** al modificar un evento Programado o una serie con ocurrencias Programadas, y contarlo en el mismo límite de avisos. Los borradores siguen con `agenda.update`. | RF-5, RF-6, §7, §12.1, §12.3, §14.1, CA-35; manual (`planificar-actividad`, `programar-y-cancelar`, `catalogo-de-permisos`) y descripciones de los permisos. |
| D-4 | Riesgos residuales señalados por `security` | **Los acepta todos:** (1) secreto de cliente en lugar de certificado hasta producción, sabiendo que pasar a certificado exige cambiar código; (2) el acotamiento a un buzón depende del tenant y no está verificado: **Outlook no se activa en producción sin pasar CA-24** (SPEC-0008); (3) la cadena de suministro de FullCalendar; (4) las invitaciones y el calendario quedan en Microsoft 365, fuera de `agenda.read`, y cancelar no borra lo enviado; (5) secretos o enlaces con *tokens* escritos por error en el texto o en la evidencia, que quedan también en la auditoría; (6) el abuso de personas con permisos dentro de los dominios permitidos, con un límite que vive en la memoria de cada proceso; (7) un límite que cuenta peticiones y no destinatarios (F-3). | §12.3, §14.1, SECURITY.md, modelo de amenazas, ADR-0016 y ADR-0017. |
| D-5 | CA-24 y la prueba real con el tenant (tarea 11) | **Trasladarlos a una spec nueva** de activación de Outlook, en `Draft`: [SPEC-0008](SPEC-0008-outlook-activation.md). Esta spec queda `Implemented` sin excepciones a la constitución §9.2. | §9 (CA-24 trasladado), §16.1 (tareas 11 y cierre), §18. |
| D-6 | Cambios ajenos a esta spec en el mismo árbol | Autoriza commits `minor:` aparte para las fijaciones transitivas de NuGet, la vulnerabilidad de `vite` y la estabilización de la prueba de rendimiento de SPEC-0005. El cambio de nginx no es de esta spec y no viaja con ella. | Ninguno; trazabilidad (constitución §11.2 y §11.6). |
| D-7 | Revisiones y entorno | Revisiones con agentes independientes por rol; en Docker, solo `docker compose up --build` (sin `down -v`). | §18. |
| D-8 | Hora propuesta al planificar hoy desde el calendario (2026-10-24) | Si el día elegido en un calendario es **hoy**, el formulario propone una hora futura: la actual **más una hora**, en lugar de las 09:00. | RF-12b; manual (`consultar-agenda`, `planificar-actividad`). |

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
