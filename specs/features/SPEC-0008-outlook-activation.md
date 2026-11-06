---
id: SPEC-0008
title: Invitaciones de la agenda por correo SMTP de Microsoft 365
type: feature
status: Approved
owner: jmartinez
created: 2026-10-21
approved-by: jmartinez (2026-11-06)
agents: [architecture, security, backend, database, frontend, testing]
related: [SPEC-0006, ADR-0016, ADR-0023, SPEC-0009]
---

# SPEC-0008 — Invitaciones de la agenda por correo SMTP de Microsoft 365

> Nace de la **enmienda del 2026-10-21 de SPEC-0006** (§19.8, D-5): el solicitante trasladó aquí la activación de Outlook, que
> SPEC-0006 dejó implementada con Graph, probada con dobles y desactivada.
> **Reescrita el 2026-11-05.** El solicitante decidió no usar Microsoft Graph y enviar las invitaciones por **SMTP de Microsoft 365
> con usuario y contraseña** (§19.1). El diseño está en **ADR-0023**.
>
> El tenant **no tiene acceso condicional** (Q-11), así que la contraseña es una identidad del directorio sin MFA. El solicitante
> descartó expresamente las alternativas que la acotarían y eligió una **enmienda de la constitución §3.8** (Q-12, MAJOR 6.0.0).
>
> **Dos rondas de revisión independiente y una de verificación** (`architecture`, `security` y `database`, 2026-11-05), todas
> «aceptable con cambios». Todos los hallazgos están incorporados (§19.3).
>
> **Aprobada por el solicitante el 2026-11-06** (Q-13). Ese día:
> - confirmó que los valores predeterminados de seguridad ya estaban desactivados;
> - aceptó R-1 y R-5;
> - aceptó ADR-0023 con su enmienda, y la constitución pasó a la 6.0.0.
>
> **Mientras esta spec no esté `Implemented`, `AGENDA_OUTLOOK_ENABLED` no se activa en producción** (SPEC-0006 §19.8, D-4).

## 1. Objetivo

Que las actividades Programadas de la agenda lleguen como **reuniones de Outlook** al responsable, a los participantes y a la
lista del equipo, y que puedan actualizarse y cancelarse, también para quien deje de estar invitado. Se envían por SMTP de
Microsoft 365 con una cuenta de servicio dedicada, sin aplicación de Entra ID.

## 2. Contexto

SPEC-0006 entregó la publicación en Outlook detrás del puerto `IOutlookCalendarGateway`, con un adaptador de Graph (ADR-0016)
desactivado por defecto. El 2026-11-05 el solicitante decidió no usar Graph y usar una **cuenta de correo SMTP de Microsoft 365**,
«por ahora».

SMTP solo envía correo, así que el calendario se alimenta con **invitaciones iCalendar** (ADR-0023). Frente a SPEC-0006 cambia:

- La reunión aparece en el calendario **de cada destinatario**. El buzón emisor no guarda los eventos y no hay enlace «Abrir en
  Outlook».
- **Publicado** pasa a significar *enviado* (aceptado por el servidor), no *entregado*.
- ITMas tiene que **recordar quién puede tener la reunión**, para cancelar a quien se quite.

Hechos externos (ADR-0023, contexto):

- Desde **finales de diciembre de 2026** Exchange Online desactiva por defecto la autenticación básica de SMTP (MC786329). Se puede
  reactivar de forma temporal, y la retirada definitiva se anunciará en 2027.
- Sin MFA y sin acceso condicional, la contraseña da una **identidad del directorio**. Abre el portal, ITMas y las aplicaciones sin
  asignación; permite leer el directorio y el propio buzón y pedir tokens de Graph; y, si el tenant no lo impide, consentir y
  registrar aplicaciones, registrar métodos de autenticación e invitar a personas externas.
- Entra ID Free guarda los inicios de sesión 7 días. Exchange admite 30 mensajes por minuto y 3 conexiones simultáneas por buzón, y
  nginx corta las peticiones a los 60 s.

## 3. Alcance

- **Incluye:**
  - el adaptador SMTP de `IOutlookCalendarGateway` y un escritor puro de invitaciones iCalendar (`REQUEST` y `CANCEL`);
  - el cambio del puerto (entrada y resultado) y de `OutlookPublisher` (escritura anticipada, lotes, presupuesto, códigos
    neutros);
  - la lista de quienes pueden tener la reunión, con su migración (Q-7);
  - la regla estricta de direcciones, también al guardar los participantes;
  - la caducidad técnica de la aceptación;
  - la selección del adaptador por configuración y las variables nuevas;
  - los códigos `smtp_*` y `outlook_*` en la API y en la pantalla;
  - los controles de la cuenta y del tenant (§14.2);
  - el procedimiento ante un compromiso;
  - la prueba con la cuenta real;
  - la enmienda de SPEC-0006 (§10.1);
  - la documentación y el manual.
- **Excluye:**
  - cambios de funcionalidad de la agenda;
  - leer respuestas, rebotes o el buzón emisor;
  - el procesamiento en segundo plano (SPEC-0006 RNF-6);
  - OAuth 2.0 para SMTP y el *relay* por IP (descartados; seguimiento de ADR-0023);
  - limitar la cuenta por IP (Q-9);
  - un presupuesto de destinatarios por hora (Q-3);
  - borrar el adaptador de Graph (Q-6);
  - un reinicio de las columnas de Outlook al cambiar de transporte.

## 4. Actores

| Actor | Descripción |
|---|---|
| Quien administra el tenant de Microsoft 365 | Confirma la condición previa (valores predeterminados de seguridad ya desactivados), prepara la cuenta y los ajustes del tenant (§14.2), hace las revisiones periódicas y ejecuta el procedimiento ante un compromiso. |
| Quien despliega ITMas | Genera la contraseña nueva y la escribe directamente en el `.env`, sin pasarla por la línea de comandos (constitución §4). Es quien la custodia, y se anota su rol. |
| Personas con `agenda.publish` | Programan, modifican lo Programado, cancelan y reintentan: son las que hacen salir las invitaciones (SPEC-0006). |
| Responsable, participantes y lista del equipo | Reciben las invitaciones, actualizaciones y cancelaciones en Outlook. |
| `security` | Revisa la implementación, la configuración y el resultado de la prueba. |

## 5. Requerimientos

### 5.1 Funcionales

- **RF-1** Con `AGENDA_OUTLOOK_ENABLED=true` y `AGENDA_OUTLOOK_TRANSPORT=smtp` (valor por defecto), programar, modificar lo
  Programado, cancelar y «Sincronizar de nuevo» envían correo a `smtp.office365.com:587` en lugar de llamar a Graph. Con `graph` se
  usa el adaptador de ADR-0016, y con Outlook desactivado no hay ninguna conexión externa.
- **RF-2** Publicar o actualizar envía **un mensaje** con una invitación `METHOD:REQUEST` (ADR-0023, decisión 4):
  - destinatarios: responsable y participantes como obligatorios y la lista del equipo como opcional, con los dominios permitidos y
    el máximo de 30 participantes de SPEC-0006;
  - `UID` derivado del id del evento y `SEQUENCE = 2·revisión + 1`;
  - organizador: la cuenta;
  - asunto, cuerpo de texto plano, lugar, recordatorio y categoría como en SPEC-0006 RF-16.
- **RF-2b** **Regla estricta de direcciones** (ADR-0023, decisión 4): la parte local solo admite `[a-z0-9._+-]` (sin punto al
  principio, al final ni dos seguidos), el dominio son etiquetas LDH en ASCII, y las direcciones se guardan recortadas y en
  minúsculas. Se aplica:
  - al **guardar los participantes**, que se rechazan con un mensaje que nombra la dirección. Antes de activar el envío, una
    consulta de solo lectura cuenta los participantes guardados que no la cumplen, y se corrigen editando cada actividad (CA-6);
  - al arrancar, a la lista del equipo y a la cuenta;
  - en el escritor, también al responsable.
  Una dirección que el escritor omite no rompe el envío y se cuenta en la auditoría.
- **RF-3** Cancelar la actividad envía un `METHOD:CANCEL` con el mismo `UID`, `SEQUENCE = 2·revisión actual + 1` (cancelar
  incrementa la revisión) y el motivo, a **quienes pueden tener la reunión** (RF-4), filtrados con los dominios permitidos actuales.
  - Con la escritura anticipada, el `UID` ya está guardado cuando alguien cancela, así que la cancelación la envía siempre el
    propio caso de uso. **No hay cancelación de seguimiento con `smtp`**; se mantiene para `graph`.
  - Alguien cuyo `REQUEST` nunca se aceptó puede recibir el `CANCEL` (R-3).
- **RF-3b** (Q-7) Al actualizar una actividad ya enviada, quienes puedan tener la reunión y ya no estén entre los destinatarios
  reciben **un único** `METHOD:CANCEL`, con todos ellos como `ATTENDEE` y en `To` y `SEQUENCE = 2·revisión`, antes de la
  actualización. También se filtra con los dominios actuales. Esto incluye a un participante quitado, a un responsable cambiado y a
  la lista del equipo si se desmarca «notificar al equipo». Una actualización son como mucho dos mensajes.
  - La comparación no distingue mayúsculas, y nadie es a la vez destinatario directo de la cancelación y de la actualización.
  - Si alguien recibe la cancelación por la lista y la actualización directamente, gana la actualización por su secuencia mayor.
- **RF-4** **Quienes pueden tener la reunión** se guardan en el evento (ADR-0023, decisión 5; solo con `smtp` y con el envío activo,
  RF-7):
  - **en la primera escritura**, la misma que guarda el cambio, se fija `outlook_event_id = UID` y se añaden los destinatarios del
    `REQUEST` previsto, ya pasados por RF-2b. El trabajo lleva el conjunto que había antes;
  - **la segunda escritura solo quita**:
    - a quien recibió un `CANCEL` aceptado;
    - a quien el servidor rechazó de forma permanente en `RCPT`, solo si este trabajo lo añadió;
    - solo si la revisión del trabajo es igual a la revisión actual del evento;
    - ante un conflicto, con un único reintento;
  - `MarkOutlookPublished` es monótono: nunca baja la revisión sincronizada ni deshace una cancelación confirmada;
  - una cancelación confirmada o completar la actividad **vacía** la lista, y desde entonces nunca vuelve a llenarse;
  - lista vacía = `NULL`, con las direcciones ordenadas.
- **RF-5** Estado del resultado, como hoy:
  - **Publicado** si el servidor aceptó todos los mensajes de la operación;
  - **Error** con un código del conjunto cerrado (ADR-0023, decisión 8);
  - **Pendiente** para lo que no se intentó o se envió a medias: lote detenido, presupuesto agotado, pausa tras un fallo de
    autenticación, espera del *gate* que no cabe, o la cancelación a quienes se quitan aceptada y la actualización sin enviar. El
    puerto lo devuelve como `NotAttempted`, sin código de error.
  `webLink` queda vacío.
- **RF-6** Lotes (ADR-0023, decisión 7):
  - plazo propio, no el de la petición del navegador, también en la segunda escritura. Si el cliente se va, el lote sigue hasta su
    presupuesto;
  - una conexión y un `AUTH` por lote;
  - presupuesto de 45 s **desde el inicio de la petición**, con la primera escritura y las esperas del *gate* dentro. Un mensaje
    solo empieza si caben la conexión (15 s, si no hay una abierta), el mensaje (15 s) y un margen de 5 s. El instante límite viaja
    al adaptador, que aplica esta regla;
  - en todo el proceso, 25 mensajes por minuto y 2 conexiones simultáneas;
  - el lote se detiene ante `smtp_auth_failed`, `smtp_basic_auth_disabled`, `smtp_sender_rejected` y `smtp_throttled`;
  - tras un fallo de autenticación, una pausa de 15 minutos para todo el proceso.
- **RF-7** **Caducidad técnica.** Una fecha en el código de composición (`SmtpExceptionAcceptedUntil`) igual a la revisión de la
  aceptación de R-1 (2027-05-05, o la que fije Q-13), que pasa a `AgendaSettings`.
  - `Application` calcula si el envío está **activo** (`OutlookEnabled` y, con `smtp`, la fecha no vencida según `TimeProvider`).
    Sin envío activo no hay escritura anticipada ni envío, y las consultas devuelven Outlook como no configurado, así que la
    pantalla muestra «Outlook no configurado».
  - Renovarla exige un commit que remita a la nueva aceptación.
  - **Al vencer**, quien administra el tenant bloquea además el inicio de sesión de la cuenta (o restablece la contraseña) y
    deshabilita SMTP AUTH en el buzón (DEVELOPMENT.md §11).
- **RF-8** Al arrancar con Outlook activo y `smtp`, la API no arranca si falta `AGENDA_SMTP_USERNAME`, `AGENDA_SMTP_PASSWORD` o
  `AGENDA_PUBLIC_URL`, o si la cuenta o la lista del equipo no cumplen RF-2b. No imprime valores. Si
  `AGENDA_ALLOWED_ATTENDEE_DOMAINS` está vacío, se permite el dominio de la cuenta.
- **RF-9** La pantalla muestra un texto en español para cada código `smtp_*` y `outlook_*`. El enlace «Abrir en Outlook» no aparece
  sin `webLink`, como ya ocurre hoy.

### 5.2 No funcionales

- **RNF-1** TLS obligatorio:
  - STARTTLS estricto, TLS 1.2 o 1.3;
  - validación del certificado, del nombre y de la revocación, que necesita salida HTTP a OCSP/CRL;
  - ninguna opción de configuración para desactivarlo, y `AUTH` solo después de TLS;
  - host y puerto fijos;
  - solo el código de pruebas puede inyectar una CA de prueba.
- **RNF-2** Ni la contraseña, ni el diálogo SMTP, ni el texto del servidor, ni las excepciones de MailKit, ni las direcciones
  aparecen en logs, métricas, Problem Details o auditoría.
  - El registro de protocolo y la telemetría de MailKit están desactivados, y `SmtpOutlookSettings` no imprime la contraseña.
  - La auditoría guarda el código normalizado y recuentos (destinatarios, quitados y omitidos).
  - La lista nunca sale en la API, en la exportación `.ics` ni en las instantáneas de la auditoría.
- **RNF-3** Ninguna prueba automatizada llama a Microsoft (constitución §9.4). La prueba con la cuenta real es manual y queda en
  §18 sin direcciones, identificadores ni secretos.
- **RNF-4** Se mantienen el límite de 60 operaciones por hora, los lotes de 50 y el máximo de 30 participantes. Con una sola
  réplica de la API (SPEC-0009, Q-5), los límites en memoria son los del servidor.

## 6. Reglas de negocio

Sin reglas nuevas de la agenda. Se aplican las de SPEC-0006: ITMas es la fuente de verdad, un fallo del envío nunca impide guardar,
programar ni cancelar, y el reintento es manual. La regla de direcciones de RF-2b endurece la validación existente de los
participantes.

## 7. Permisos

Sin permisos nuevos. Se aplican los `agenda.*` de SPEC-0006: enviar invitaciones, actualizaciones y cancelaciones exige
`agenda.publish`. La cuenta de la agenda no recibe ninguna política en ITMas.

## 8. Comportamiento esperado

1. Quien administra el tenant confirma la **condición previa**: los valores predeterminados de seguridad ya estaban desactivados por
   motivos ajenos a ITMas, y el tenant no tiene acceso condicional. Después prepara la cuenta y los ajustes del tenant (§14.2) y
   evalúa el MFA por usuario con contraseña de aplicación.
2. Quien despliega genera la contraseña nueva y la escribe en el `.env` del equipo de trabajo.
3. Con `docker compose up --build` se activa Outlook con `smtp` y se ejecuta la prueba de DEVELOPMENT.md §11.3, que se reescribe
   (CA-1 a CA-4). Las invitaciones van a buzones de quien hace la prueba.
4. Se cumple la lista de verificación de producción de SECURITY.md (agenda, reescrita).
5. Se activa en producción **con la misma cuenta** (Q-8): una actividad de prueba con la lista del equipo llega y se cancela (CA-5).

Lo que ve una persona con `agenda.publish`:

- Al programar, la actividad queda **Publicado** si los correos salieron, o **Error** con un texto como «El servidor de correo
  rechazó las credenciales de ITMas.».
- Si programa muchas a la vez, algunas quedan **Pendiente**: «Sincronizar de nuevo» las envía después.
- Un participante con una dirección que no cumple la regla se rechaza al guardar, con el mensaje actual de correo no válido.
- Los destinatarios reciben una reunión de la cuenta de la agenda. Al modificarla, reciben una actualización, y quien se quita, la
  cancelación. Al cancelarla, todos reciben la cancelación con el motivo.

## 9. Criterios de aceptación

- [ ] **CA-1** Con la cuenta real, se programa una actividad con responsable, un participante y la lista del equipo:
  - queda **Publicado**;
  - los tres reciben una reunión de Outlook con el asunto `[<Tipo>] <Título>`, la hora correcta en su zona y el recordatorio;
  - la reunión queda en sus calendarios.
  Se anota si el calendario del buzón emisor muestra o no el evento.
- [ ] **CA-2** Al modificar la hora, los destinatarios reciben una **actualización** de la misma reunión: su calendario muestra una
  sola, con la hora nueva. «Sincronizar de nuevo» sin cambios no la duplica.
- [ ] **CA-3** Al cancelarla con un motivo, los destinatarios reciben la **cancelación** con el motivo y Outlook ofrece quitarla del
  calendario. Ocurre lo mismo con una actividad de día completo.
- [ ] **CA-3b** (RF-3b):
  - al quitar al participante de una actividad enviada, esa persona recibe la cancelación y la reunión sale de su calendario,
    mientras el responsable y la lista reciben la actualización;
  - al desmarcar «notificar al equipo», un miembro de la lista que también es participante **conserva** la reunión actualizada;
  - al cancelar después la actividad, la cancelación llega a quienes seguían invitados.
- [ ] **CA-4** Errores con la cuenta real o simulados en el equipo de trabajo, sin que la operación de la agenda falle:
  - con una contraseña incorrecta: **Error** con `smtp_auth_failed`, un solo intento de autenticación, el resto del lote Pendiente
    y la pausa de 15 minutos;
  - sin salida a `smtp.office365.com`: `smtp_unavailable`;
  - ni la contraseña, ni el diálogo SMTP, ni direcciones aparecen en los logs de `api` (búsqueda documentada).
- [ ] **CA-5** En producción, tras CA-1 a CA-4:
  - una actividad de prueba con la lista del equipo llega y se cancela;
  - `docker compose logs api` no contiene la contraseña, `AUTH` ni direcciones;
  - la próxima rotación de la contraseña y la fecha de revisión de R-1 están anotadas en la agenda.
- [ ] **CA-6** Controles de §14.2, verificados por quien administra el tenant y anotados en §18 sin identificadores:
  - **condición previa:** valores predeterminados ya desactivados antes de esta decisión y sin acceso condicional;
  - **cuenta de servicio:** buzón dedicado, UPN igual a la dirección principal, sin derechos sobre otros buzones y usada solo por
    ITMas;
  - **contraseña:** nueva y generada al configurar, rotada otra vez tras la prueba en el equipo de trabajo, con el rol de quien la
    custodia y la copia sellada del `.env`;
  - **licencia y pertenencia:** solo Exchange Online, sin roles ni equipos, y como mucho el grupo de exclusión de métodos (o la
    persistencia por métodos aceptada en R-1);
  - **participantes guardados:** el resultado de la consulta de RF-2b, con cero direcciones que no cumplan;
  - **buzón:** solo SMTP AUTH, habilitado en el buzón y no en la organización;
  - **tenant:** sin consentimiento de usuarios, sin registro de aplicaciones por usuarios, invitaciones externas limitadas y
    reenvío externo automático desactivado;
  - **métodos:** fuera de SSPR, con la contraseña como único método;
  - **MFA por usuario con contraseña de aplicación:** el resultado de la evaluación;
  - **detección:** directivas de alerta de envío dirigidas al equipo, y la revisión (cada 6 días como máximo en Entra ID, o mensual
    en Purview si muestra el inicio de sesión SMTP de CA-1, anotando cuál) programada en la agenda;
  - **al vencer la aceptación:** el bloqueo de la cuenta y de SMTP AUTH está en DEVELOPMENT.md §11 y en la agenda;
  - **buzón emisor:** retención de 30 días de los Elementos enviados y alerta de cuota;
  - **ITMas:** la cuenta no tiene ninguna política.
- [ ] **CA-7** Antes de finales de diciembre de 2026, y de nuevo tras el cambio de Microsoft, se confirma que el envío sigue
  funcionando (o la excepción temporal del tenant).
- [ ] **CA-8** Pruebas automatizadas en verde:
  - **escritor de invitaciones:** `REQUEST`/`CANCEL`, `UID`, paridad de `SEQUENCE`, asistentes y roles, un solo `ATTENDEE` en la
    cancelación a quien se quita, día completo, recordatorio y `DTSTAMP`; inyección de cabeceras (también en `To`/`Cc`), de
    propiedades y de `CAL-ADDRESS`;
  - **regla de direcciones** (RF-2b): al guardar participantes (rechazo con la dirección), al arrancar, en el escritor (omisión
    contada) y los caracteres de `mailto:` (`?`, `%`, `&`, `#`);
  - **lista** (`AgendaEventTests` y la función de quienes se quitaron):
    - escritura anticipada, sin distinguir mayúsculas y conjuntos disjuntos;
    - un único `CANCEL` con varios quitados;
    - quitar solo con la revisión igual, rechazos `RCPT` solo de lo añadido, `ConfirmedRemovals` en un envío parcial y un reintento
      ante un conflicto;
    - `MarkOutlookPublished` monótono y estados finales que nunca añaden;
    - vacío = `NULL` y orden;
  - **`OutlookPublisher`:**
    - instante límite, `NotAttempted`, envío parcial y `StopBatch`;
    - pausa tras `AUTH`;
    - plazo propio frente a la petición abortada, también en la segunda escritura;
    - códigos neutros;
    - sin cancelación de seguimiento con `smtp`, y la carrera entre una cancelación confirmada y un resultado tardío;
  - **envío activo** (RF-7): la caducidad con `TimeProvider` en los manejadores y en las consultas;
  - **adaptador** (doble de `ISmtpClient`):
    - mapeo por código extendido;
    - una conexión y un `AUTH` por lote;
    - 2 conexiones y 25 mensajes por minuto;
    - STARTTLS estricto y validación por defecto en producción;
    - ninguna excepción ni protocolo en los logs;
    - `ToString` de los ajustes sin la contraseña;
    - caducidad (RF-7);
  - **persistencia:** migración, `CHECK`, lectura y escritura de `NULL` y de la lista con PostgreSQL real, y su ausencia en la
    auditoría, en el detalle y en el `.ics`;
  - **configuración:** validación al arrancar y selección `smtp`/`graph`/desactivado;
  - **frontend:** textos de los códigos;
  - **integración** con un servidor SMTP de pruebas en Testcontainers.
- [ ] **CA-9** La documentación está al día en el mismo cambio (§16, tarea 7), `docker compose up --build` sigue sano y con
  Outlook desactivado no hay ninguna conexión SMTP.

## 10. Análisis arquitectónico

- **ADR-0023 (`Proposed`)** decide transporte, cuenta y controles, contenido, lista, puerto, lotes, errores, librería,
  configuración, caducidad y la excepción de §3.8. ADR-0016 sigue vigente para `graph` y recibe una nota al aceptar.
- **Capas** (constitución §2):
  - `Domain`: la regla de direcciones (también en `ParticipantList`) y `AgendaEvent` con la lista (`OutlookSentRecipients`) y sus
    reglas (RF-4). El dominio garantiza los invariantes del `CHECK`.
  - `Application`: el puerto (entrada con el instante límite, y resultado con `NotAttempted`), el escritor de invitaciones junto a
    `IcalendarWriter`, la función de quienes se quitaron, `OutlookEventContent.ForCancel(evento, tipo, ajustes)`, los códigos neutros
    y `AgendaSettings` con `Transport`, la fecha de caducidad y el cálculo del envío activo. Revisión obligatoria de `architecture`.
    - `OutlookPublisher` asume el plazo propio, el instante límite, la segunda escritura que solo quita y `StopBatch`. Con `smtp` no
      hay cancelación de seguimiento.
    - Cambian `ScheduleAndCancelHandlers`, `SyncWithOutlookHandler`, `SeriesHandlers` y `EventHandlers`.
  - `Infrastructure/Agenda/Smtp/`: el adaptador *scoped* (MIME, MailKit, conexión perezosa por lote), `SmtpThrottleGate` (ritmo,
    conexiones y pausa) y `SmtpOutlookSettings`.
  - `Api/Agenda`: opciones, validación y selección.
- **Base de datos:** una migración (§13).
- **Red:** `api` ya tiene salida por `edge`. Destinos nuevos: `smtp.office365.com:587` y los OCSP/CRL del certificado por HTTP.
- **Dependencias nuevas:** MailKit/MimeKit (MIT, mantenida) y la imagen del servidor SMTP de pruebas, fijada por *digest* (§12.5).
- **Cambio de transporte:** solo sin envíos vivos, es decir, sin Programadas con `outlook_event_id` ni Canceladas con la
  cancelación sin confirmar (ADR-0023, decisión 10). Se documenta en DEVELOPMENT.md §11.

### 10.1 Enmienda de SPEC-0006

Con `AGENDA_OUTLOOK_TRANSPORT=smtp` cambian estas partes de SPEC-0006 (`Implemented`). Se enmiendan al aceptar ADR-0023 y se
registran en SPEC-0006 §19:

- **RF-14:** el enlace «Abrir en Outlook» solo existe con `graph`.
- **RF-15 y RF-16:** sin «calendario compartido». Con `smtp` son invitaciones a los calendarios de los destinatarios, sin
  `transactionId`: la idempotencia viene del `UID` y de la `SEQUENCE`.
- **RF-17:** quien se quita recibe la cancelación.
- **RF-18:** Publicado significa enviado.
- **RF-19 y RN-14:** con `smtp` no hay «se vuelve a crear si se borró», sino que la actualización se reenvía con el mismo `UID`.
- **RNF-5:** con `smtp`, 15 s por mensaje, un presupuesto de 45 s y detención también ante fallos de autenticación o del remitente,
  no solo ante un `429`.
- **§12.1:** el conjunto cerrado de códigos gana los `smtp_*` y los `outlook_*`.
- **§14.4:** los prerrequisitos del tenant (aplicación y RBAC) solo valen para `graph`.
- **Modelo de amenazas:** la fila de «el equipo tiene lectura del calendario» solo vale para `graph`.
- **`outlook_event_id`:** con `smtp` guarda el `UID`.

## 11. Cambios de frontend

- `features/agenda/data/agenda-labels.ts`: textos de `smtp_auth_failed`, `smtp_basic_auth_disabled`, `smtp_sender_rejected`,
  `smtp_rejected`, `smtp_throttled`, `smtp_unavailable`, `smtp_timeout`, `outlook_unavailable`, `outlook_timeout` y
  `outlook_rejected`, con sus pruebas.
- El formulario de participantes muestra el error de validación actual para las direcciones que no cumplen RF-2b.
- Sin pantallas nuevas. Si algún texto dice «calendario compartido», pasa a «invitación de Outlook».

## 12. Cambios de backend

Sin endpoints nuevos ni cambios de contrato HTTP.

- `Domain/Agenda/EventDetails.cs` (`ParticipantList`): la regla estricta de direcciones.
- `Domain/Agenda/AgendaEvent.cs`: `OutlookSentRecipients` y sus reglas (RF-4).
- `Application/Agenda/Outlook/`:
  - `IOutlookCalendarGateway.cs`: entrada (`Sequence`, `StartUtc`/`EndUtc`, `RemovedRecipients`, datos en `CancelAsync`) y
    resultado (`StopBatch`, id en el fallo, `ConfirmedRemovals`, `RejectedRecipients`);
  - `OutlookPublisher.cs`: plazo propio, presupuesto, segunda escritura que solo quita y reintento ante un conflicto;
  - la función de quienes se quitaron;
  - `OutlookEventContent.ForCancel`.
- `Application/Agenda/`: el escritor de invitaciones (junto a `IcalendarWriter`), en `AgendaCodes.cs` los códigos `smtp_*` y
  `outlook_*`, y en `AgendaSettings.cs` el transporte, la fecha de caducidad y el envío activo.
- `Application/Agenda/Events/` y `Series/`: `ScheduleAndCancelHandlers`, `EventHandlers`, `SeriesHandlers` y
  `SyncWithOutlookHandler` (escritura anticipada, tipo para la cancelación y firma nueva).
- `Infrastructure/Agenda/Smtp/`:
  - `SmtpOutlookSettings` (no es un `record` y su `ToString` no imprime la contraseña);
  - `SmtpOutlookCalendarGateway` (*scoped*, conexión perezosa por lote y caducidad);
  - `SmtpThrottleGate`.
- `Infrastructure/Agenda/Graph/` y `NotConfiguredOutlookCalendarGateway.cs`: solo la firma nueva.
- `Api/Agenda/AgendaOptions.cs` y `AgendaModuleSetup.cs`: `Transport` y la sección `Smtp`. Sin telemetría de MailKit en
  `ObservabilitySetup`.
- `docker-compose.yml` (`Agenda__Outlook__Transport`, `Agenda__Smtp__Username`, `Agenda__Smtp__Password`) y `.env.example`:
  - sin valores;
  - con el aviso de las comillas simples para una contraseña con `$`;
  - con el comentario de los dominios corregido.

## 13. Cambios de base de datos

Una migración EF Core, `AddAgendaOutlookSentRecipients` (constitución §5.4), con revisión de `database` y `security` (datos
personales):

- **Columna:** `app.agenda_events.outlook_sent_recipients`, de tipo **`jsonb` nulable**, con las direcciones normalizadas y ordenadas
  (`Ordinal`) de quienes pueden tener la reunión (RF-4). Solo la mantiene el transporte `smtp`.
  - Usa la convención de las listas pequeñas del evento: el mismo convertidor y comparador que `participants`
    (`AgendaConverters.Json`, `JsonComparer`), configurado de forma explícita, sin `IsRequired()`.
- **`CHECK` `ck_agenda_events_outlook_sent_recipients`:**
  `outlook_sent_recipients IS NULL OR (outlook_event_id IS NOT NULL AND jsonb_typeof(outlook_sent_recipients) = 'array' AND
  jsonb_array_length(outlook_sent_recipients) > 0)`.
  - El dominio garantiza el invariante (lista vacía = `NULL`, `UID` fijado en la primera escritura), porque `AgendaUnitOfWork` no
    traduce el 23514.
- **Migración:**
  - `ADD COLUMN` nulable y sin valor por defecto solo toca el catálogo. `ADD CONSTRAINT` recorre la tabla con un bloqueo
    `ACCESS EXCLUSIVE`, despreciable con su tamaño actual.
  - `NULL` en las filas existentes. En producción no hay ninguna enviada, porque Outlook nunca se activó, y la API anterior ignora la
    columna.
  - **`Down`** elimina la restricción y la columna. No afecta a otros datos, pero pierde las listas: solo se revierte si no hubo
    envíos o si se acepta que quien se quite después no reciba la cancelación.
- **Datos personales:**
  - La columna lleva direcciones que la fila **no** tiene en otra forma: el correo del responsable, que vive en `users`, y la lista
    del equipo, que solo está en el `.env`.
  - Se minimiza vaciándola al confirmar la cancelación o al completar la actividad. No tiene límite si una actividad vencida nunca
    se cierra o su cancelación queda en Error, porque la agenda no purga.
  - No sale en la API, en el `.ics` ni en las instantáneas de la auditoría (RNF-2).
  - Las copias de seguridad la conservan durante su retención (SPEC-0009), también después de vaciarla.
- **Privilegios:** el rol de la API ya tiene DML por los privilegios por defecto. No se conceden privilegios nuevos.
- **Documentación y salida:** se actualiza `specs/database/README.md` y se sigue la lista de salida de `agents/database.md`:
  `has-pending-model-changes`, revisión del script idempotente y migraciones aplicadas desde cero.

## 14. Consideraciones de seguridad

### 14.1 Amenazas (STRIDE)

| Amenaza | Escenario | Control |
|---|---|---|
| Suplantación | La contraseña se filtra (`.env`, `docker inspect` por `root` o el grupo `docker`, una copia en claro) o cae por *password spraying* (la dirección es visible para cualquier invitado). Con ella, alguien envía correo corporativo como la agenda, inicia sesión en el portal, en ITMas y en aplicaciones sin asignación, lee el directorio y el propio buzón y pide tokens de Graph, sin MFA y desde cualquier IP. | Los de §14.2: cuenta de servicio con solo Exchange Online, sin roles ni equipos (como mucho, el grupo de exclusión de métodos); ajustes del tenant sin P1; fuera de SSPR; alertas de envío y revisión periódica; contraseña nueva y rotada; procedimiento ante un compromiso. El `.env` es de `root` y legible solo por él (SPEC-0009). **Sin control preventivo del inicio de sesión** (Q-9, Q-11). **R-1, excepción de §3.8.** |
| Elevación / persistencia | Con la cuenta, alguien consiente o registra una aplicación, registra un método de autenticación o crea reglas de reenvío para mantener el acceso tras un cambio de contraseña. | Sin consentimiento ni registro de aplicaciones por usuarios; fuera de SSPR y con la contraseña como único método, comprobado en cada revisión; reenvío externo automático desactivado; el procedimiento ante un compromiso quita consentimientos, aplicaciones, métodos y reglas. |
| Manipulación | Inyección de cabeceras MIME, de propiedades iCalendar o de parámetros `CAL-ADDRESS` desde el título, la descripción, el motivo o una dirección. | Regla estricta de direcciones al guardar, al arrancar y en el escritor (RF-2b), sin `CN`; MimeKit codifica las cabeceras; el escape de `IcalendarWriter`; pruebas de inyección (CA-8). |
| Manipulación / revelación en tránsito | Degradación a texto claro, un servidor falso o un host mal escrito que recibe la contraseña. | Host y puerto fijos; STARTTLS estricto; validación del certificado, del nombre y de la revocación; sin opción para desactivarlo (RNF-1). |
| Revelación | La contraseña, las direcciones o el texto del servidor acaban en logs, en excepciones registradas o en la auditoría. | RNF-2; pruebas de que ninguna excepción de MailKit llega a un *logger*. |
| Revelación | Una reunión que describe una actividad de seguridad se queda en el calendario de quien ya no está invitado, o la cancelación llega a quien no debía. Los Elementos enviados del buzón acumulan el contenido. | Escritura anticipada de la lista y reglas de RF-4; conjuntos disjuntos y secuencias separadas (RF-3b); refiltrado con los dominios actuales; retención de 30 días de los Elementos enviados. |
| Repudio | No saber quién hizo salir un correo. | Cada operación ya genera su evento de auditoría con la persona (SPEC-0006), con recuentos de destinatarios. |
| Denegación de servicio | Envío masivo hasta que Exchange limita el buzón, autenticaciones fallidas repetidas que bloquean la cuenta, o un ataque de *password spraying* que la bloquea y deja la agenda sin envío. | 60 operaciones por hora, lotes de 50, 30 participantes, 25 mensajes por minuto, 2 conexiones, una autenticación por lote, detención del lote y pausa de 15 minutos tras un fallo de autenticación (RF-6). El bloqueo por un ataque externo se acepta en R-1. |
| Elevación | Una persona con `agenda.publish` usa la agenda para enviar correo arbitrario. | Contenido fijo (asunto, cuerpo y enlace), dominios permitidos y límites de SPEC-0006. |

### 14.2 Cuenta y tenant (los prepara quien administra el tenant)

- **Condición previa:** los **valores predeterminados de seguridad ya estaban desactivados** por motivos ajenos a ITMas. Si
  estuvieran activos, **no** se desactivan por esta cuenta y el envío no se activa (ADR-0023, compuerta 2). El tenant no tiene
  acceso condicional (Q-11).
- **Cuenta de servicio:**
  - buzón dedicado a la agenda, nunca el de una persona, que solo usa ITMas;
  - su UPN coincide con su dirección SMTP principal;
  - envía solo como sí misma, sin *Send As*, *Send on Behalf* ni derechos sobre otros buzones.
- **Contraseña:**
  - **nueva**, larga y generada al configurar ITMas, y la anterior deja de valer. Tras la prueba en el equipo de trabajo se rota otra
    vez, de modo que la de producción solo esté en el `.env` del servidor y en su copia sellada fuera de línea (SECURITY.md §8);
  - se escribe directamente en el `.env`, entre comillas simples si contiene `$`;
  - **no se escribe en el chat, en tickets ni en el repositorio**;
  - solo la conoce quien la configura (se anota su rol);
  - se rota cada seis meses como máximo, y la rotación se anota en la agenda.
- **Licencia y pertenencia:** solo una licencia de **Exchange Online**, sin Teams, SharePoint ni OneDrive; sin roles de
  administración ni equipos. Como mucho, un único grupo de seguridad sin permisos para excluirla de los métodos registrables en la
  directiva de métodos de autenticación. Sin él, la persistencia por métodos se acepta en R-1.
- **Buzón:** SMTP AUTH habilitado **solo en él** (`Set-CASMailbox -SmtpClientAuthenticationDisabled $false`), nunca en toda la
  organización. POP, IMAP, EWS, MAPI, ActiveSync y Outlook en la web, desactivados.
- **Ajustes del tenant** (sin P1):
  - no permitir el consentimiento de usuarios a aplicaciones;
  - que los usuarios no puedan registrar aplicaciones;
  - invitaciones de personas externas limitadas a roles concretos;
  - reenvío automático externo desactivado en la directiva de *spam* saliente.
- **Métodos de autenticación:** fuera de SSPR, con la contraseña como único método.
- **MFA por usuario con contraseña de aplicación:** se evalúa. Si SMTP AUTH funciona con una contraseña de aplicación y la cuenta
  queda con MFA para el inicio de sesión interactivo, se adopta y R-1 se reduce. El resultado se anota en §18. Si se adopta:
  - el segundo factor lo custodia quien custodia la contraseña;
  - la cuenta tiene exactamente ese método y la contraseña de aplicación de ITMas;
  - el procedimiento ante un compromiso borra las contraseñas de aplicación, que no se revocan al restablecer la contraseña.
- **Detección:**
  - las directivas de alerta predeterminadas de Microsoft 365 sobre el envío («patrones sospechosos de envío», «usuario restringido
    para enviar»), dirigidas al equipo;
  - una revisión del registro de inicios de sesión **cada 6 días como máximo** en Entra ID (guarda 7), o **mensual** en el registro
    de auditoría unificado de Purview (Audit Standard, 180 días) si muestra el inicio de sesión SMTP de la prueba CA-1. Se anota cuál
    se usa, y se programa en la agenda;
  - cada revisión comprueba también los métodos, los consentimientos, las aplicaciones, las reglas de bandeja y los reenvíos. La
    alerta de reglas de reenvío no ve las creadas por Graph, así que esta comprobación es manual.
- **Buzón emisor:** retención de **30 días** de los Elementos enviados y alerta de cuota.
- **ITMas:** la cuenta no recibe ninguna política (§3.4). No se exige asignación en la aplicación empresarial de ITMas, para no
  cambiar el acceso de las demás personas.
- **Procedimiento ante un compromiso** (DEVELOPMENT.md §11):
  1. `AGENDA_OUTLOOK_ENABLED=false`;
  2. restablecer la contraseña y revocar las sesiones (`Revoke-MgUserSignInSession`);
  3. quitar consentimientos, aplicaciones, métodos registrados y contraseñas de aplicación;
  4. revisar reglas de bandeja, reenvíos y Elementos enviados;
  5. avisar al equipo.
- **Retirada de la autenticación básica:** SMTP AUTH con autenticación básica permitido para el buzón también después del cambio de
  diciembre de 2026 (CA-7).

### 14.3 Datos personales y sensibles

- Las direcciones del responsable, de los participantes y de la lista salen hacia Microsoft 365, como ya preveía SPEC-0006.
- La lista guardada (§13) las conserva en ITMas hasta que se cancela o se completa la actividad, y en las copias durante su
  retención.
- El buzón emisor guarda lo enviado 30 días, más las respuestas y los rebotes, que ITMas no lee.
- Al vaciar la lista tras una cancelación confirmada, las direcciones que el filtro de dominios actual excluyó no recibieron el
  `CANCEL` (R-3).

### 14.4 Riesgos residuales

R-2 a R-4 los aceptó el solicitante el 2026-11-05. R-1 y R-5, reformulados tras las revisiones de `security` y la respuesta a Q-11,
los **aceptó expresamente el 2026-11-06** (Q-13). Todos tienen revisión no posterior al 2027-05-05. R-1 es la excepción de
§3.8 (constitución 6.0.0) que declara ADR-0023, con los seis elementos que exige (ADR-0023, decisión 12):

- **R-1** Una identidad del directorio **sin MFA, sin acceso condicional y sin límite por IP** (excepción de §3.8):
  - **Alcance real:**
    - enviar correo corporativo como la agenda desde cualquier lugar;
    - iniciar sesión en el portal, en ITMas (sin permisos) y en aplicaciones sin asignación;
    - leer el directorio y el propio buzón, con sus Elementos enviados de 30 días, y pedir tokens de Graph con los permisos por
      defecto.
    - registrar métodos de autenticación (passkey, Authenticator) para mantener el acceso, salvo que el grupo de exclusión de §14.2
      lo impida.
    Sin Teams, SharePoint ni OneDrive, por la licencia. Sin consentimiento, registro de aplicaciones ni invitaciones, por los ajustes
    del tenant.
  - **Controles que se aplican:** los de §14.2. **Controles que faltan:** MFA (salvo que se adopte la contraseña de aplicación),
    acceso condicional y límite por IP. No hay control preventivo del inicio de sesión, y la detección llega con hasta una semana de
    retraso.
  - **Alternativas descartadas:** SMTP con OAuth 2.0 (`SMTP.SendAsApp` acotado al buzón), que solo exigía registrar una aplicación,
    y el *relay* por IP, que exigía un conector y liberar el puerto 25 en AWS. Descartadas expresamente el 2026-11-05 (Q-12).
  - **Custodia:** la contraseña de producción solo está en el `.env` del servidor y en su copia sellada fuera de línea. La conoce
    quien configura el `.env` (se anota su rol), que custodia también el segundo factor si se adopta. ITMas es la única usuaria.
  - **Retirada:** OAuth 2.0 para SMTP, o una directiva de acceso condicional si el tenant llega a tenerla.
  - **Aceptación:** expresa del solicitante el 2026-11-06 (Q-13), con revisión no posterior al 2027-05-05. Al vencer, RF-7 desactiva
    el envío y el tenant bloquea la cuenta.
  - Un ataque de *password spraying* puede además bloquear la cuenta y dejar la agenda sin envío.
- **R-2** La autenticación básica de SMTP sale desactivada por defecto desde finales de diciembre de 2026, y Microsoft la retirará en
  2027 (desde entonces, `550 5.7.30`, visible como `smtp_basic_auth_disabled`). El envío depende de que el tenant la mantenga, y
  deberá migrarse antes de la fecha definitiva.
- **R-3** Publicado significa enviado, no entregado, e ITMas no lee los rebotes. Las direcciones que el filtro de dominios actual
  excluye no reciben la cancelación. Alguien cuyo `REQUEST` nunca se aceptó puede recibir un `CANCEL`, por la escritura anticipada.
- **R-4** Una dependencia nueva en la cadena de suministro (MailKit/MimeKit).
- **R-5** El envío y sus controles dependen de la configuración del tenant:
  - SMTP AUTH en el buzón, y una directiva de autenticación de Exchange que no bloquee la autenticación básica de SMTP
    (`AllowBasicAuthSmtp`);
  - los valores predeterminados de seguridad desactivados para todo el tenant, por una decisión previa y ajena a ITMas que esta spec
    no toma ni justifica;
  - los ajustes de §14.2.
  Un cambio del tenant puede romper el envío o debilitar los controles sin que ITMas lo sepa, y la revisión periódica es la que lo
  detecta.

Si una revisión posterior cambia alguno de estos riesgos, se vuelve a pedir la aceptación.

### 14.5 Revisión de `security`

Hecha en dos rondas sobre ADR-0023 y esta spec el 2026-11-05 (§19.3). Vuelve a ser obligatoria sobre el adaptador, la
configuración, la migración y el resultado de la prueba (constitución §1.2, `agents/README.md`).

## 15. Pruebas

| Criterio | Tipo | Prueba |
|---|---|---|
| CA-1 a CA-5 (con CA-3b) | Verificación documentada (manual, con la cuenta real) | DEVELOPMENT.md §11.3 reescrita; resultado en §18 sin direcciones, identificadores ni secretos. |
| CA-6, CA-7 | Verificación documentada (tenant) | Lista de §14.2 y fechas en la agenda. |
| CA-8 | Unitarias, de persistencia e integración | `InvitationWriterTests`, pruebas de la regla de direcciones (`ParticipantList`, opciones y escritor), `AgendaEventTests` y `RemovedRecipientsTests`, `OutlookPublisherTests` y casos de uso, `SmtpOutlookCalendarGatewayTests` y `SmtpThrottleGateTests` (doble de `ISmtpClient`), `AgendaOptionsValidator` y la selección del adaptador, persistencia con Testcontainers, integración con un servidor SMTP de pruebas, y `agenda-labels` en el frontend. |
| CA-9 | Verificación documentada | `docker compose up --build` y la suite completa (`dotnet test`, `npm test`). |

## 16. Plan de implementación

Aprobado con la spec el 2026-11-06. La tarea 1 se hizo en el mismo cambio que la aprobación:

| # | Tarea | Agente | Archivos |
|---|---|---|---|
| 1 | Aceptación de ADR-0023: constitución 6.0.0 (§3.8, cabecera e historial), nota en ADR-0016 y enmienda de SPEC-0006 (§10.1) | architecture + security (+ aprobación humana) | `constitution.md`, `specs/architecture/adr-0023-*.md`, `adr-0016-*.md`, `specs/features/SPEC-0006-*.md`, `specs/features/SPEC-0008-*.md`, `specs/README.md`, `specs/features/README.md` |
| 2 | Regla de direcciones, `AgendaEvent`, puerto, escritor de invitaciones, quienes se quitaron, `OutlookPublisher` y casos de uso, códigos | backend (architecture revisa) | `Itmas3.Domain/Agenda/**`, `Itmas3.Application/Agenda/**` |
| 2b | Columna `outlook_sent_recipients`, `CHECK` y migración `AddAgendaOutlookSentRecipients` | database (backend, security) | `Itmas3.Infrastructure/Persistence/**`, `specs/database/README.md` |
| 3 | Adaptador SMTP, `SmtpThrottleGate`, caducidad, MailKit | backend (security revisa) | `Itmas3.Infrastructure/Agenda/Smtp/**`, `Directory.Packages.props` |
| 4 | Opciones, validación y selección; Compose y `.env.example` | backend + architecture (security) | `Itmas3.Api/Agenda/**`, `docker-compose.yml`, `.env.example` |
| 5 | Pruebas unitarias, de persistencia y de integración | testing | `backend/tests/**` |
| 6 | Textos de los códigos y del error de participantes | frontend | `frontend/src/app/features/agenda/**` |
| 7 | Documentación y manual | architecture (security) | README; SECURITY (agenda, «Abuso», lista de producción y riesgos); DEVELOPMENT §11 (prueba, diagnóstico de códigos, cambio de transporte, procedimiento ante un compromiso, revisiones) y §12 (salida a `smtp.office365.com:587` y OCSP/CRL); ARCHITECTURE (topología y adaptador); `specs/security/threat-model-baseline.md` (credencial SMTP, suplantación, persistencia, inyección `CAL-ADDRESS`, cancelación equivocada); `frontend/public/manual/**` |
| 8 | Cuenta, tenant y prueba real (CA-1 a CA-7) | quien administra el tenant + testing (security) | §18 de esta spec |

## 17. Manual de uso

| Proceso (`id`) | Acción | Permiso | Pantallas (`routes`) |
|---|---|---|---|
| `programar-y-cancelar` | Cambia: los invitados reciben una reunión de Outlook por correo, con actualizaciones y cancelaciones; quien se quita recibe la cancelación; Publicado significa enviado; con muchas a la vez, algunas quedan Pendiente; mensajes de error nuevos; ya no hay enlace a Outlook. | `agenda.publish` | `/agenda`, `/agenda/events/:id` |
| `planificar-actividad` | Cambia: un participante con un correo que no cumple la regla se rechaza al guardar, con un mensaje que nombra la dirección; las actividades guardadas antes con una dirección así se corrigen editándolas. | `agenda.create`, `agenda.update` | `/agenda/events/new`, `/agenda/events/:id/edit`, `/agenda/series/new` |
| `consultar-agenda` | Cambia: la columna Outlook y el aviso «Outlook no configurado» se describen sin calendario compartido, y el aviso también aparece si la aceptación venció. | `agenda.read` | `/agenda` |

## 18. Trazabilidad (al implementar)

- Commits/PRs: …
- Archivos principales: …
- Pruebas: …
- Manual de uso actualizado: …

## 19. Decisiones y preguntas abiertas

### 19.1 Respuestas del solicitante (2026-11-05)

| # | Pregunta | Respuesta |
|---|---|---|
| Q-1 | Credencial de Graph: certificado o secreto | **No se usará Microsoft Graph.** El envío será por SMTP de Microsoft 365 con una cuenta, su usuario y su contraseña, «por ahora». Elegido frente al *relay* por IP, sabiendo que la autenticación básica sale desactivada por defecto desde diciembre de 2026 (ADR-0023). |
| Q-2 | Quién y dónde se prueba | La prueba se hace con esa cuenta SMTP de Microsoft 365 (§8). |
| Q-3 | El límite de 60 por hora cuenta operaciones, no personas | **Dejarlo así.** Si Exchange limita el buzón, se baja `AGENDA_PUBLISH_REQUESTS_PER_HOUR`. |
| Q-4 | Lista de distribución del equipo | **Se usará.** Su dirección se configura solo en el `.env` (`AGENDA_OUTLOOK_TEAM_ADDRESS`). Además queda en la lista guardada de cada actividad enviada hasta que se cancela o se completa (§13). |
| Q-5 | Varias réplicas de la API | No: producción es un único servidor con una sola instancia de `api` (SPEC-0009). |
| Q-6 | Qué hacer con el adaptador de Graph | **Conservarlo desactivado**, seleccionable con `AGENDA_OUTLOOK_TRANSPORT=graph`. Solo se cambia de transporte sin envíos vivos (§10). |
| Q-7 | Quien se quita de una actividad ya enviada | **Enviarle la cancelación**: se guardan quienes pueden tener la reunión, con una migración (RF-3b, RF-4, §13). |
| Q-8 | ¿La cuenta de la prueba es la de producción? | **Sí, la misma.** En producción basta CA-5. La contraseña, en cambio, se genera de nuevo al configurar (§14.2). |
| Q-9 | Acceso condicional por IP | **No por ahora:** la cuenta puede enviar desde cualquier IP. El mecanismo se cambiará más adelante. |
| Q-10 | Aceptación de los riesgos residuales | **Acepta R-1 a R-4** en su redacción inicial, con revisión no posterior al 2027-05-05. Las revisiones cambiaron R-1 y añadieron R-5, cuya aceptación está pendiente (Q-13). |
| Q-11 | ¿El tenant tiene acceso condicional? | **No.** La contraseña queda como identidad del directorio sin MFA (R-1). |
| Q-12 | Cómo seguir sin acceso condicional | **Enmienda de §3.8 y usuario y contraseña.** Descarta expresamente SMTP con OAuth 2.0 (recomendado) y el *relay* por IP. ADR-0023 propone el texto (MAJOR 6.0.0). |
| Q-13 (2026-11-06) | Condición previa, aceptación de R-1 y R-5 y de ADR-0023 | Confirma que los **valores predeterminados de seguridad ya estaban desactivados** en el tenant. **Acepta R-1 y R-5** con revisión no posterior al 2027-05-05. **Acepta ADR-0023** con su enmienda (constitución 6.0.0) y **aprueba esta spec**. |

### 19.2 Preguntas abiertas

Ninguna.

### 19.3 Revisiones independientes (2026-11-05)

Revisores de solo lectura, uno por rol, sin conocer las conclusiones de quien redactó, revisaron ADR-0023 y esta spec contra el
código en dos rondas. Todos los hallazgos están incorporados.

- **Primera ronda** (sobre la primera redacción): las tres «aceptable con cambios».
  - **`security`:**
    - altos: los valores predeterminados no admiten exclusiones y la contraseña es una identidad completa (R-1 reformulado, que con
      Q-11 llevó a la enmienda);
    - medios: validación estricta frente a la inyección en `CAL-ADDRESS`, semántica de la lista, host fijo y TLS estricto, una
      autenticación por lote y alertas;
    - bajos: retención, `ToString`, excepciones de MailKit, comillas en el `.env` y el modelo de amenazas.
  - **`architecture`:**
    - altos: la cancelación a la lista del equipo borraba la reunión a quien seguía invitado (secuencias separadas) y la lista
      dejaba reuniones huérfanas;
    - medios: el alcance real del puerto, el escritor en `Application`, los límites de Exchange y el corte de nginx, los respaldos de
      error, la CA de prueba, la enmienda de SPEC-0006 y la documentación;
    - bajos: sin el puerto 465, el UPN igual a la dirección y la trazabilidad.
  - **`database`:**
    - medios: `jsonb` en lugar de `text[]`, el fallo parcial, la normalización, los datos personales nuevos y el cambio de
      transporte;
    - bajos: fuera de la auditoría, la documentación del esquema, el `CHECK` y la reversión.
- **Segunda ronda** (sobre la versión con la enmienda): las tres «aceptable con cambios».
  - **`architecture`:**
    - altos: la condición de la enmienda no amparaba un descarte por preferencia, faltaba un suelo y §3.8 decía «única excepción»;
      además, la lista se escribía después del envío y el puerto no modelaba el lote;
    - medios: el presupuesto por mensaje y por evento, 3 conexiones y `432 4.3.2`, la pausa tras `AUTH`, el responsable en la
      cancelación, los códigos neutros y la enmienda de SPEC-0006 incompleta;
    - bajos: el filtro de RF-3b, `5.1.8` y `5.7.30`, OCSP/CRL y la tabla de §19.1.
  - **`security`:**
    - altos: la condición y las exclusiones de la enmienda; el alcance real incompleto (buzón por Graph, consentimientos,
      métodos, invitaciones, inicio de sesión en ITMas); la revisión mensual imposible con 7 días de retención; y los valores
      predeterminados como condición previa;
    - medios: el MFA por usuario con contraseña de aplicación, la caducidad técnica, `StopBatch` y la pausa, y la validación al
      guardar;
    - bajo: la contraseña nueva.
  - **`database`:**
    - altos: la escritura anticipada, quitar solo con la revisión igual y los estados finales que no añaden;
    - medios: solo `smtp` mantiene la lista, `ConfirmedRemovals` y los rechazos `RCPT`, y el cambio de transporte;
    - bajos: vacío = `NULL` y orden, el bloqueo del `CHECK` y el `Down`, los datos personales en copias y en actividades sin
      cerrar, y el responsable en la cancelación.
  - **Decisiones de redacción** (ADR-0023, §«Revisiones»): no se exige asignación en la aplicación empresarial de ITMas. No se
    añade un `CHECK` de estado final.
- **Verificación** (sobre la versión anterior): `architecture` (con la lente de `database`) y `security`, «aceptable con cambios».
  Confirmaron resueltos los altos de las dos rondas.
  - **`architecture`:**
    - altos: la regla por evento no cabía con dos o más quitados (un único `CANCEL` y solo la regla por mensaje); el puerto no
      expresaba Pendiente (`NotAttempted` e instante límite); la cancelación de seguimiento sobraba con `smtp` y estropeaba el estado
      (sin seguimiento y `MarkOutlookPublished` monótono);
    - medios: rechazos `RCPT` solo de lo añadido, el envío activo en `Application`, el presupuesto desde el inicio de la petición y
      la exclusión de la enmienda;
    - bajos: `Transport` en `AgendaSettings`, la regla de direcciones antes de la escritura anticipada, el `CANCEL` de más en R-3, el
      token propio en la segunda escritura, el cambio de transporte y la tarea 1.
  - **`security`:**
    - alto: la exclusión del proveedor de identidad abarcaba esta credencial (precisada, y ahora se exige un ADR `Accepted`);
    - medios: la custodia (rotación tras la prueba y copia sellada), el bloqueo de la cuenta al vencer, la contraseña de aplicación,
      la persistencia por métodos (grupo de exclusión) y los participantes ya guardados;
    - bajos: `5.7.139`, `AllowBasicAuthSmtp`, `mailto:`, la revisión cada 6 días y Purview.
