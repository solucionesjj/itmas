---
id: ADR-0023
title: Invitaciones de la agenda por SMTP de Microsoft 365 con usuario y contraseña (enmienda de §3.8, MAJOR 6.0.0)
status: Accepted
date: 2026-11-05
deciders: [architecture, security]
related: [SPEC-0008, SPEC-0006, ADR-0016, ADR-0022, SPEC-0009]
---

# ADR-0023 — Invitaciones de la agenda por SMTP de Microsoft 365 con usuario y contraseña (enmienda de §3.8, MAJOR 6.0.0)

> **Propuesto el 2026-11-05** a partir de la decisión del solicitante en SPEC-0008 §19.1: **no se usará Microsoft Graph** y la
> agenda avisará enviando correo por **SMTP de Microsoft 365 con una cuenta, su usuario y su contraseña**, «por ahora».
>
> **El tenant no tiene acceso condicional** (SPEC-0008 Q-11). La contraseña queda como identidad completa de Entra ID sin MFA, y
> el solicitante **descartó expresamente** las alternativas que la acotarían (SMTP con OAuth 2.0, recomendada, y el *relay* por
> IP): eligió mantener usuario y contraseña con una **enmienda de §3.8** (Q-12).
>
> **Dos rondas de revisión independiente y una de verificación** (`architecture`, `security` y `database`, 2026-11-05), todas
> «aceptable con cambios». Todos los hallazgos están incorporados (§«Revisiones»).
>
> **Aceptado por el solicitante el 2026-11-06**, con las tres compuertas de §«Compuertas de aceptación» cumplidas:
> - confirmó que los valores predeterminados de seguridad ya estaban desactivados;
> - aceptó R-1 y R-5 con revisión no posterior al 2027-05-05;
> - la constitución pasa a la 6.0.0 en el mismo cambio.
>
> **Enmienda del 2026-11-10** (SPEC-0008 §19.4): la detección queda solo con las alertas de envío, sin MFA con contraseña de
> aplicación y sin grupo de exclusión de métodos. Pasan a los controles que faltan de la decisión 12, con revisión de `security`.
> El solicitante **aceptó de nuevo R-1 y R-5 de forma expresa el 2026-11-10** (SPEC-0008 Q-14), con revisión no posterior al
> 2027-05-05.

## Contexto

SPEC-0006 publica las actividades Programadas en Outlook a través del puerto `IOutlookCalendarGateway`. El único adaptador real es
el de Graph (ADR-0016), que **nunca se ha activado**. El 2026-11-05 el solicitante decidió no usar Graph y enviar correo por SMTP
con una cuenta de Microsoft 365.

Fuerzas en juego:

- **Qué ofrece SMTP.** Solo envía correo. Un calendario de Outlook se alimenta con **invitaciones iCalendar** (RFC 5545 y el modelo
  iTIP de RFC 5546): `METHOD:REQUEST` crea o actualiza la reunión en el calendario de quien la recibe y `METHOD:CANCEL` la cancela,
  y Outlook las relaciona por `UID` y `SEQUENCE`. A diferencia de Graph:
  - el calendario del remitente no guarda los eventos;
  - no hay `webLink` ni un identificador del servidor;
  - aceptar el mensaje no confirma la entrega;
  - nadie cancela por sí solo a quien deja de estar invitado: ITMas debe recordar a quién envió.
- **La autenticación básica de SMTP está en retirada.** Según Message Center MC786329 (actualizado el 2026-01-27), Exchange Online
  la **desactiva por defecto a finales de diciembre de 2026**. Un administrador puede reactivarla de forma temporal, y la retirada
  definitiva se anunciará en 2027; desde entonces el envío fallará con `550 5.7.30`.
- **La cuenta es una identidad del directorio sin MFA**, porque la autenticación básica no la admite. **Sin acceso condicional**
  (Entra ID P1, que el tenant no tiene), su contraseña permite, además de enviar correo:
  - iniciar sesión en el portal y en cualquier aplicación empresarial que no exija asignación, ITMas incluida, que da de alta a
    cualquier cuenta del tenant sin concederle nada (SECURITY.md §2, constitución §3.4);
  - leer el directorio y pedir tokens de Graph con los permisos por defecto, con clientes de primera parte ya preautorizados;
  - leer y modificar el propio buzón por Graph (los Elementos enviados y las reglas de bandeja), aunque POP, IMAP, EWS, MAPI y Outlook
    en la web estén desactivados;
  - consentir y registrar aplicaciones, cuyas concesiones sobreviven a un cambio de contraseña;
  - registrar métodos de autenticación, que dan persistencia;
  - invitar a personas externas, si la configuración del tenant lo permite.
- **Valores predeterminados de seguridad.** No admiten exclusiones por usuario y bloquean la autenticación heredada en todo el
  tenant. Si estuvieran activos, desactivarlos para esta cuenta quitaría el MFA a todas las personas, un riesgo mucho mayor que el
  de la propia cuenta.
- **Registros sin P1.** Entra ID Free conserva los registros de inicio de sesión y de auditoría **7 días**, y exportarlos exige P1.
- **Límites de Exchange Online:** 30 mensajes por minuto, 3 conexiones simultáneas y 10 000 destinatarios al día por buzón.
  Algunos códigos al superarlos: `432 4.3.2`, `421 4.4.2`, `554 5.2.0 …SubmissionQuotaExceeded` y `5.1.90`.
- **Tiempo y concurrencia de las peticiones.**
  - La publicación ocurre dentro de la petición, sin cola (SPEC-0006), y nginx corta a los 60 s (`proxy_read_timeout`).
  - Hoy `OutlookPublisher` guarda el resultado en una **segunda escritura** después de la llamada. Si esa escritura choca con otra
    (`ConflictException`) o la petición se aborta, no guarda nada.
- **El servidor tiene salida.** El contenedor `api` está en la red `edge`, `ufw` permite la salida (SPEC-0009) y AWS solo limita
  por defecto el puerto 25.
- **Constitución.** §3.8 exige mínimo privilegio en todo, con excepciones declaradas solo para el agente (§13.4) y, desde la 5.0.0,
  para las credenciales de copias (§8.7(e), ADR-0022). Rigen además §4 (secretos), §9.4 (pruebas sin servicios externos), §12.2
  (logging) y §12.5 (dependencias), y las protecciones de contenido de SPEC-0006.

## Decisión

1. **Transporte.** Envío SMTP autenticado **solo a `smtp.office365.com:587`**. No hay variables para el host ni el puerto: una
   errata o un dominio parecido entregaría la contraseña a un tercero.
   - MailKit con `SecureSocketOptions.StartTls` (nunca `Auto` ni `StartTlsWhenAvailable`), `SslProtocols` explícito (TLS 1.2 y
     1.3), `CheckCertificateRevocation` activo y ninguna `ServerCertificateValidationCallback` permisiva. `AUTH` solo después de
     TLS.
   - La comprobación de revocación necesita salida HTTP a los servicios OCSP y CRL del certificado, que `ufw` permite.
   - La prueba de integración confía en una CA de prueba mediante un punto de inyección **interno, solo desde el código de
     pruebas** (DI, nunca configuración). Una prueba comprueba que la composición de producción usa la validación por defecto.
2. **Cuenta emisora y controles** (los aplica quien administra el tenant, sin P1):
   - **Cuenta de servicio:** un buzón dedicado a la agenda, nunca el de una persona, que **solo usa ITMas**. Su UPN coincide con su
     dirección SMTP principal (si no, `5.2.252 SendAsDenied`). Envía solo como sí mismo, sin *Send As* ni *Send on Behalf* y sin
     derechos sobre otros buzones.
   - **Contraseña nueva**, larga, generada al configurar ITMas y escrita directamente en el `.env`. La que exista hoy deja de valer.
     - Tras la prueba en el equipo de trabajo **se rota otra vez**, de modo que la de producción solo esté en el `.env` del servidor
       y en su copia sellada fuera de línea (SECURITY.md §8).
     - Solo la conoce quien la configura, que se anota por su rol.
     - Se rota cada seis meses como máximo, y la rotación se anota en la agenda.
   - **Mínimo de licencia y de pertenencia:** solo una licencia de Exchange Online, sin Teams, SharePoint ni OneDrive; sin roles de
     administración ni equipos. **Sin grupo de exclusión** de los métodos registrables (2026-11-10, SPEC-0008 §19.4): la persistencia
     por métodos se acepta en R-1. Un único grupo de seguridad sin permisos que la excluyera de la directiva de métodos sería la
     forma de cerrarla.
   - **En el buzón:** SMTP AUTH habilitado **solo en él** (`Set-CASMailbox -SmtpClientAuthenticationDisabled $false`), nunca en la
     organización. POP, IMAP, EWS, MAPI, ActiveSync y Outlook en la web desactivados.
   - **En el tenant**, ajustes que no exigen P1:
     - no permitir el consentimiento de usuarios a aplicaciones;
     - que los usuarios no puedan registrar aplicaciones;
     - limitar las invitaciones de personas externas a roles concretos;
     - el reenvío automático externo, desactivado en la directiva de *spam* saliente.
   - **Métodos de autenticación:** la cuenta queda fuera del autoservicio de restablecimiento (SSPR) y solo tiene la contraseña como
     método.
   - **MFA por usuario con contraseña de aplicación:** se evaluó y **no se adopta** (2026-11-10, SPEC-0008 §18). Queda como opción
     futura: la credencial que guardara ITMas solo serviría para la autenticación heredada y no para iniciar sesión de forma
     interactiva, y R-1 se reduciría. Si se adoptara:
     - el segundo factor lo custodia la misma persona que custodia la contraseña;
     - la cuenta tiene exactamente ese método y la contraseña de aplicación de ITMas;
     - las contraseñas de aplicación no se revocan al restablecer la contraseña, así que el procedimiento ante un compromiso las
       borra.
   - **Detección:** las directivas de alerta predeterminadas de Microsoft 365 sobre el envío («patrones sospechosos de envío»,
     «usuario restringido para enviar»), dirigidas al equipo.
     - **Enmienda del 2026-11-10** (SPEC-0008 §19.4): el solicitante retiró la revisión periódica del registro de inicios de sesión
       (cada 6 días en Entra ID, o mensual en Purview) y, con ella, la de los métodos, los consentimientos, las aplicaciones, las
       reglas de bandeja y los reenvíos. Pasa a los controles que faltan (decisión 12). Los registros se consultan solo ante una
       alerta, un `smtp_auth_failed` inesperado o una sospecha, y en el procedimiento ante un compromiso.
   - **Retención** de los Elementos enviados limitada a **30 días** (lo que se conserva es lo que se expone), y una alerta de cuota.
   - **ITMas** no concede nada a la cuenta: no se le asigna ninguna política (§3.4). No se exige asignación en la aplicación
     empresarial de ITMas, para no cambiar el acceso de las demás personas.
   - **Procedimiento ante un compromiso**, documentado en DEVELOPMENT.md §11:
     1. poner `AGENDA_OUTLOOK_ENABLED=false`;
     2. restablecer la contraseña y revocar las sesiones (`Revoke-MgUserSignInSession`);
     3. quitar consentimientos, aplicaciones, métodos registrados y contraseñas de aplicación;
     4. revisar reglas de bandeja, reenvíos y Elementos enviados;
     5. avisar al equipo.
   - **Condición previa:** los **valores predeterminados de seguridad ya estaban desactivados** en el tenant antes de esta decisión,
     por motivos ajenos a ITMas. Si estaban activos, este ADR **no se aplica** y el envío no se activa: no se desactivan por esta
     cuenta.
3. **Credencial.** `AGENDA_SMTP_PASSWORD`, variable de entorno solo del contenedor `api`. Nunca va en el repositorio, en logs, en
   la línea de comandos ni en imágenes (§4). En producción vive en el `.env` del servidor, bajo la desviación ya aceptada en
   SECURITY.md §8, entre comillas simples si contiene `$`, porque Compose interpola.
4. **Contenido: invitaciones iCalendar.**
   - **Un escritor puro de invitaciones en `Application/Agenda`**, junto a `IcalendarWriter`, cuyo escape `TEXT` y plegado comparte.
     Infrastructure solo construye el MIME.
   - Cada mensaje es `multipart/alternative`, con una parte `text/plain` (el cuerpo de SPEC-0006 RF-16) y otra `text/calendar;
     method=<METHOD>; charset=UTF-8`, con **un solo** `VEVENT`. `DTSTAMP` es el instante del envío (`TimeProvider`).
   - **Publicar o actualizar:** `METHOD:REQUEST`. **Cancelar:** `METHOD:CANCEL`, con el motivo en el cuerpo y en `COMMENT`, y
     `STATUS:CANCELLED`.
   - **`UID`:** `<id del evento>@invite.agenda.itmas`. Se deriva siempre del id del evento, nunca del valor guardado, y es distinto
     del de la exportación `.ics` (que no cambia).
   - **`SEQUENCE`:**
     - `2·revisión` en las cancelaciones a quien se quita, y `2·revisión + 1` en las actualizaciones. Si alguien recibe las dos (por
       ejemplo, a través de la lista del equipo que se quitó), gana la actualización, llegue en el orden que llegue.
     - Cancelar la actividad incrementa la revisión (`AgendaEvent.Cancel`), así que su `2·revisión + 1` supera cualquier envío
       anterior.
   - **`ORGANIZER`:** la cuenta emisora. **`ATTENDEE`:** el responsable y los participantes como `REQ-PARTICIPANT` y la lista del
     equipo como `OPT-PARTICIPANT`, con `RSVP=FALSE`, sin `CN`.
   - **Quienes se quitan reciben un único `CANCEL`**, con todos ellos como `ATTENDEE` y en `To`. Así una actualización son como
     mucho dos mensajes: la cancelación y la actualización.
   - **Esa cancelación es neutra** (precisión de la revisión de `security` de la implementación, 2026-11-10): asunto y `SUMMARY`
     «Ya no estás invitado a una actividad de la agenda», y solo `UID`, `SEQUENCE`, `DTSTAMP`, las fechas, `ORGANIZER`, sus
     `ATTENDEE` y `STATUS:CANCELLED`. Nunca el título, la descripción, el lugar, la categoría ni el enlace de la actividad tal como
     quedó, que la persona quitada no debe ver.
   - **Direcciones.** Una regla estricta única, en `Domain`:
     - la parte local solo admite `[a-z0-9._+-]`, sin punto al principio, al final ni dos seguidos. Es un subconjunto de
       *dot-atom* (RFC 5321) sin `?`, `%`, `&` ni `#`, que tienen significado en un URI `mailto:` (RFC 6068);
     - el dominio, etiquetas LDH en ASCII;
     - las direcciones se normalizan recortadas y en minúsculas.
     Se aplica:
     - **al guardar los participantes** (hoy `ParticipantList.EmailPattern` acepta más), que se rechazan con un mensaje que nombra
       la dirección. Antes de activar el envío, una consulta de solo lectura cuenta los participantes guardados que no la cumplen, y
       se corrigen editando cada actividad (SPEC-0008 CA-6);
     - al arrancar, a la lista del equipo y a la cuenta;
     - en el escritor, como defensa en profundidad, también al correo del responsable (que viene del directorio) y a `To`/`Cc`.
     Una dirección que el escritor omite no rompe el envío, y la auditoría cuenta las omitidas.
   - Horas en UTC (el puerto lleva `StartUtc`/`EndUtc`), días completos con `VALUE=DATE` y fin exclusivo, `VALARM` con el
     recordatorio y `CATEGORIES` con el tipo. Asunto `[<Tipo>] <Título>`.
5. **Destinatarios que pueden tener la reunión** (`app.agenda_events.outlook_sent_recipients`, `jsonb`, una migración). Solo los
   mantiene el transporte `smtp`.
   - **Escritura anticipada.** En la **primera escritura** (la unidad de trabajo protegida por `xmin` que guarda el cambio y
     construye el trabajo de Outlook; los cinco manejadores ya la hacen dentro de `unitOfWork.ExecuteAsync`):
     - se fija `outlook_event_id = UID`;
     - se **añaden** a la lista los destinatarios del `REQUEST` que se va a enviar, ya pasados por la regla de direcciones;
     - el trabajo lleva el conjunto que había antes.
     Así, ni un conflicto en la segunda escritura ni una petición abortada dejan a nadie fuera. Como contrapartida, alguien cuyo
     `REQUEST` nunca se aceptó puede recibir después un `CANCEL` (R-3). `Application` sabe qué transporte hay (`AgendaSettings`
     gana `Transport`) y solo hace esto con `smtp` y con el envío activo (decisión 11).
   - **La segunda escritura solo quita**, o vacía, y usa el plazo propio del lote (decisión 7):
     - quita a quien recibió un `CANCEL` **aceptado**;
     - quita a quien el servidor **rechazó de forma permanente** en `RCPT` (`5xx`), pero **solo si este trabajo lo añadió** en su
       escritura anticipada, porque un rechazo de ahora no prueba que no recibiera un `REQUEST` anterior;
     - solo quita si la revisión del trabajo es **igual** a la revisión actual del evento, porque un trabajo más nuevo puede estar en
       curso;
     - ante un `ConflictException`, se reintenta **una vez**, porque quitar es idempotente.
     - **Precisión de la implementación (2026-11-10, revisión de `architecture`):** también se olvida a quien el servidor rechazó para
       siempre en el `CANCEL` a quien se quita, porque no tiene un buzón que pueda guardar la reunión; si no, cada actualización
       repetiría ese `CANCEL` y bloquearía la actividad. Un `CANCEL` que falla sin detener el lote no impide la actualización de los
       demás: el evento queda en Error y el `CANCEL` se reintenta. Una cancelación de la actividad que el servidor acepta, aunque
       rechace a todos, se da por hecha.
   - **`MarkOutlookPublished` es monótono:** nunca baja `OutlookSyncedRevision` ni deshace una cancelación confirmada. Un resultado
     tardío de una revisión anterior no cambia el estado.
   - **Estados finales.** Una cancelación confirmada de la actividad, o completarla, **vacía** la lista (`NULL`). Una actividad
     completada, o cancelada con la cancelación confirmada, nunca vuelve a añadir.
   - **Cancelar la actividad** va a la lista guardada, que ya incluye a todos los que pudieron recibir algo, filtrada con los
     dominios permitidos actuales, con `SEQUENCE = 2·revisión actual + 1`. Las direcciones que ese filtro excluye se pierden: se
     vacían sin recibir el `CANCEL` (riesgo aceptado, SPEC-0008 R-3).
   - **Sin cancelación de seguimiento con `smtp`.** Con la escritura anticipada, el `UID` ya está guardado cuando alguien cancela,
     así que `CancelEventHandler` envía su propio `CANCEL`. La cancelación de seguimiento de `OutlookPublisher` (un evento cancelado
     mientras Graph lo creaba) solo se mantiene para `graph`.
   - **Invariantes** (el `CHECK` de la migración):
     - lista vacía = `NULL`;
     - direcciones ordenadas con `Ordinal`, porque el comparador distingue el orden;
     - sin lista si no hay `UID`.
     Las garantiza el dominio, con pruebas, porque `AgendaUnitOfWork` no traduce una violación de `CHECK` (23514) y haría fallar la
     operación.
   - La lista nunca sale en la API, en la exportación ni en las instantáneas de la auditoría. La auditoría solo guarda recuentos
     (destinatarios, quitados y omitidos).
6. **Cambio del puerto `IOutlookCalendarGateway`** (`Application`).
   - **Entrada:** `OutlookEventData` gana la `Sequence`, `StartUtc`/`EndUtc` y los `RemovedRecipients`.
     - A quien se quitó lo calcula `Application` con una función pura: la lista guardada menos los destinatarios actuales, sin
       distinguir mayúsculas y filtrado con los dominios actuales.
     - El conjunto de `CANCEL` nunca se cruza con el de `REQUEST`.
     - `CancelAsync` recibe los datos del evento y la lista. `OutlookEventContent.ForCancel(evento, tipo, ajustes)` los construye, y
       `CancelEventHandler` necesita el repositorio de tipos.
     - La entrada lleva además el **instante límite** del lote (decisión 7).
   - **Resultado:** `OutlookGatewayResult` gana:
     - `StopBatch`, que generaliza `Throttled`;
     - **`NotAttempted`**: no se envió nada, o solo una parte (por ejemplo, la cancelación sí y la actualización no), sin código de
       error. `OutlookPublisher` lo guarda como **Pendiente**. Lo devuelven el límite de tiempo, la pausa tras un fallo de `AUTH` y la
       espera del *gate* que no cabe;
     - el id también en un fallo;
     - `ConfirmedRemovals`: las direcciones del `CANCEL` aceptado;
     - `RejectedRecipients`: los rechazos permanentes en `RCPT`.
   - **Lote.** El adaptador SMTP es *scoped*. Abre la conexión de forma perezosa en el primer envío, la reutiliza para todo el lote
     y la cierra al liberar el ámbito. Recuerda un fallo de `AUTH` durante todo el lote.
     - El adaptador aplica la regla por mensaje con el instante límite, porque sabe si hay una conexión abierta.
     - `OutlookPublisher` fija el instante límite, no empieza un evento nuevo pasado ese instante y aplica la detención.
     - Lo prueban `OutlookPublisherTests` y las pruebas del adaptador.
   - El adaptador de Graph y `NotConfiguredOutlookCalendarGateway` reciben los campos nuevos y los ignoran.
   - Cambian `OutlookPublisher`, `ScheduleAndCancelHandlers`, `SyncWithOutlookHandler`, `SeriesHandlers` y `EventHandlers`, por la
     escritura anticipada, la firma nueva, el tipo en la cancelación y el envío activo (decisión 11).
7. **Lotes, ritmo y tiempo.**
   - **Plazo propio:** el lote usa su propio token de plazo, no el `RequestAborted` de la petición, también en la segunda escritura.
     Si el cliente se va, el lote sigue hasta su presupuesto y guarda el resultado.
   - **Presupuesto de 45 s desde el inicio de la petición**, por debajo del corte de nginx (60 s). Incluye la primera escritura, las
     esperas del *gate* y los envíos:
     - un mensaje solo empieza si el tiempo transcurrido, más 15 s de conexión (si no hay una abierta), más 15 s del mensaje y un
       margen de 5 s, cabe en el presupuesto;
     - una espera del *gate* que no cabe devuelve `NotAttempted`;
     - pasado el presupuesto quedan la segunda escritura (con un solo reintento) y la respuesta, dentro del corte de nginx. La
       cancelación propia vence 12 s después del presupuesto; si vence en la segunda escritura, el evento queda Pendiente y la operación
       no falla.
   - **Conexión** (precisión de la implementación): un rechazo del servidor deja la sesión abierta; solo un error de transporte, de TLS
     o de autenticación obliga a conectar de nuevo. Un mensaje que llegó al servidor cuenta en el ritmo, lo acepte o no.
     Lo que no cabe queda **Pendiente**. Un evento con envío parcial (la cancelación aceptada y la actualización sin enviar) queda
     Pendiente, con la cancelación aceptada ya quitada de la lista.
   - **Tiempos máximos:** 15 s por mensaje, y otros 15 s para conectar, cifrar y autenticar.
   - **`SmtpThrottleGate`**, de todo el proceso:
     - como máximo **25 mensajes por minuto** y **2 conexiones simultáneas**;
     - tras un `smtp_auth_failed` o un `smtp_basic_auth_disabled`, una **pausa de 15 minutos** para todo el proceso, en la que todo
       queda Pendiente sin autenticar. Así, un `.env` desactualizado o un ataque no disparan el bloqueo de la cuenta.
   - **El lote se detiene** (`StopBatch`) ante `smtp_auth_failed`, `smtp_basic_auth_disabled`, `smtp_sender_rejected` y
     `smtp_throttled`.
8. **Errores.** Solo se guarda un código de un conjunto cerrado, decidido por el **código de estado extendido**:
   - `smtp_auth_failed`: `535 5.7.3` (credenciales no válidas) u otro `535`;
   - `smtp_basic_auth_disabled`: `5.7.30` (retirada de la autenticación básica) y `5.7.139` (SMTP AUTH desactivado en el tenant o
     en el buzón, o bloqueado por los valores predeterminados de seguridad). Hace visibles R-2 y R-5;
   - `smtp_sender_rejected`: `5.1.8`, `5.2.252` u otra restricción del remitente;
   - `smtp_rejected`: un `5xx` sobre los datos, o sobre todos los destinatarios;
   - `smtp_throttled`: `432 4.3.2`, `421 4.4.2`, `5.2.0 …SubmissionQuotaExceeded`, `5.1.90` u otro `4xx` de límite;
   - `smtp_unavailable`: no conecta, o no hay STARTTLS o TLS válido;
   - `smtp_timeout`;
   - `not_configured`, también con la aceptación vencida (decisión 11).
   `OutlookPublisher` deja de usar los `graph_*` como respaldo y pasa a **códigos neutros** para los dos transportes
   (`outlook_unavailable`, `outlook_timeout` y `outlook_rejected`) ante una excepción inesperada, un tiempo agotado o un resultado
   sin id.
   - Ni el texto de las respuestas del servidor, ni el protocolo, ni las excepciones de MailKit llegan a un *logger*.
     `SmtpCommandException` y `AuthenticationException` llevan texto del servidor y direcciones.
   - El registro de protocolo y la telemetría de MailKit quedan desactivados.
   - `SmtpOutlookSettings` no es un `record` y sobrescribe `ToString`, como `GraphOutlookSettings`.
9. **Librería: MailKit/MimeKit** (licencia MIT, mantenidas activamente y la alternativa que recomienda la documentación de .NET).
   `System.Net.Mail.SmtpClient` está marcada como no recomendada para desarrollos nuevos. Se fija la versión en
   `Directory.Packages.props`, bajo NuGet Audit, y solo se usa en `Infrastructure`. La imagen del servidor SMTP de pruebas se fija por
   *digest* como dependencia de prueba (§12.5).
10. **Selección y configuración.**
    - `AGENDA_OUTLOOK_ENABLED` sigue siendo el interruptor general, y `AGENDA_OUTLOOK_TRANSPORT` elige `smtp` (por defecto) o
      `graph`.
    - Variables nuevas: `AGENDA_SMTP_USERNAME` (también la dirección emisora) y `AGENDA_SMTP_PASSWORD`. Con `smtp`, la API no
      arranca si faltan o si no cumplen la regla de direcciones, y no imprime valores.
    - Si `AGENDA_ALLOWED_ATTENDEE_DOMAINS` está vacío, se permite el dominio de la cuenta.
    - El adaptador de Graph se conserva desactivado (SPEC-0008 Q-6). **Solo se cambia de transporte sin envíos vivos:** antes, las
      actividades Programadas con `outlook_event_id` se cancelan o se completan, y las Canceladas con la cancelación sin confirmar
      se sincronizan hasta confirmarla. No se ofrece un reinicio de columnas. Si aun así queda una actividad publicada con el otro
      transporte, sus cambios se guardan, el adaptador responde `outlook_rejected` para ella sola y el resto del lote sigue.
11. **Caducidad técnica de la aceptación.**
    - Una constante en el código de composición (`Api/Agenda`), `SmtpExceptionAcceptedUntil`, guarda la fecha de revisión de la
      aceptación (decisión 12) y pasa a `AgendaSettings`.
    - `Application` calcula si el envío está **activo**: `OutlookEnabled` y, con `smtp`, la fecha no vencida según `TimeProvider`.
      Lo usan los manejadores (sin escritura anticipada ni envío si no está activo) y las consultas, de modo que la pantalla muestra
      «Outlook no configurado».
    - Renovarla exige un commit que remita a la nueva aceptación expresa. Tiene su prueba.
    - **Al vencer, además**, quien administra el tenant bloquea el inicio de sesión de la cuenta (o restablece la contraseña) y
      deshabilita SMTP AUTH en el buzón, porque apagar el envío no invalida la credencial (DEVELOPMENT.md §11).
12. **Excepción de §3.8 para esta credencial** (enmienda de la última sección):
    - **alcance real:** el del contexto, con los controles de la decisión 2;
    - **controles que faltan:** MFA (la contraseña de aplicación no se adopta), el grupo de exclusión de métodos, acceso
      condicional, límite por IP y, desde el 2026-11-10, la revisión periódica de los inicios de sesión (SPEC-0008 §19.4). No hay
      control preventivo del inicio de sesión. Las alertas solo detectan un envío anómalo en volumen o patrón, no uno dirigido de
      pocos mensajes. Un inicio de sesión (y con él la lectura del buzón o del directorio), un *password spraying* sin bloqueo, o un
      método o una regla añadidos para persistir pasan inadvertidos, y una persistencia por métodos sobrevive a la rotación;
    - **alternativas descartadas:** SMTP con OAuth 2.0 (`SMTP.SendAsApp` acotado al buzón), que solo exigía que el administrador
      registrara una aplicación, y el *relay* por IP, que exigía un conector y liberar el puerto 25 en AWS. Ambas se descartaron
      expresamente el 2026-11-05 (SPEC-0008 Q-12);
    - **custodia:** la contraseña de producción solo está en el `.env` del servidor y en su copia sellada fuera de línea. La conoce
      quien configura el `.env` (se anota su rol). ITMas es la única usuaria de la cuenta;
    - **retirada:** OAuth 2.0 para SMTP, o una directiva de acceso condicional si el tenant llega a tenerla;
    - **aceptación:** expresa del solicitante el **2026-11-06** (SPEC-0008 Q-13), con revisión no posterior al **2027-05-05**. Sin
      la revisión periódica, sin MFA y sin grupo de exclusión, **aceptada de nuevo de forma expresa el 2026-11-10** (SPEC-0008 Q-14
      y §19.4), con revisión no posterior al 2027-05-05. Al vencer sin revisión, la decisión 11 desactiva el envío.
13. **Pruebas** (§9.4). Ninguna prueba llama a Microsoft:
    - **escritor de invitaciones:** métodos, `UID`, `SEQUENCE` (paridad), asistentes y roles, día completo, recordatorio, inyección
      de cabeceras, de propiedades y de `CAL-ADDRESS`, y la regla de direcciones;
    - **lista y quienes se quitaron:** escritura anticipada, sin distinguir mayúsculas, conjuntos disjuntos, un único `CANCEL` con
      varios quitados, quitar solo con la revisión igual, rechazos `RCPT` solo de lo añadido, conflicto en la segunda escritura,
      estados finales, `MarkOutlookPublished` monótono y los invariantes del `CHECK`;
    - **`OutlookPublisher`:** instante límite, `NotAttempted`, envío parcial, `StopBatch`, pausa tras `AUTH`, plazo propio frente a
      la petición abortada, códigos neutros, sin seguimiento con `smtp` y la carrera entre una cancelación confirmada y un resultado
      tardío;
    - **envío activo:** caducidad con `TimeProvider` en los manejadores y en las consultas;
    - **adaptador** (doble de `ISmtpClient`): mapeo por código extendido, una conexión y un `AUTH` por lote, STARTTLS estricto y la
      validación por defecto en producción, ningún protocolo ni excepción en los logs, `ToString` de los ajustes y la caducidad
      (decisión 11);
    - **integración** con un servidor SMTP de pruebas en Testcontainers y la CA de prueba inyectada.

## Alternativas consideradas

| Alternativa | A favor | En contra |
|---|---|---|
| **SMTP AUTH con usuario y contraseña de un buzón dedicado, sin acceso condicional, con enmienda de §3.8** (elegida) | La eligió el solicitante; no necesita una aplicación de Entra ID; el puerto de SPEC-0006 lo admite. | Autenticación básica **desactivada por defecto desde diciembre de 2026** y en retirada; una identidad del directorio sin MFA ni control preventivo; exige enmendar la constitución; sin confirmación de entrega; ITMas debe recordar a quién envió. |
| SMTP AUTH con OAuth 2.0 (`XOAUTH2`, credenciales de cliente, `SMTP.SendAsApp` acotado al buzón) | Sin contraseña ni identidad sin MFA; no necesita P1; no lo afecta la retirada; cumple §3.8 sin enmienda. | Necesita que el administrador registre una aplicación de Entra ID y conceda el permiso sobre el buzón. **Recomendada; descartada expresamente por el solicitante el 2026-11-05.** Es la retirada natural de la excepción. |
| SMTP AUTH con usuario y contraseña, acotado con acceso condicional | Cumple §3.8 sin enmienda. | El tenant no tiene Entra ID P1 (SPEC-0008 Q-11). |
| *Relay* SMTP por IP (conector de entrada en Exchange) | Sin credencial; no lo afecta la retirada. | Exige un conector para la IP del servidor y que AWS libere el puerto 25; cualquier proceso del servidor podría enviar como el dominio. Descartada por el solicitante. |
| Microsoft Graph con permisos de aplicación acotados (ADR-0016) | Calendario compartido real, `webLink`, cancelación nativa, sin contraseñas. | Descartada por el solicitante: necesita una aplicación de Entra ID y RBAC en el tenant. |
| `System.Net.Mail.SmtpClient` | Sin dependencia nueva. | Microsoft no lo recomienda para desarrollos nuevos. |
| Correo de aviso sin invitación (solo texto) | Más simple. | No actualiza ni cancela nada en los calendarios: pierde RF-16 a RF-19 de SPEC-0006. |
| No cancelar a quien se quita | Sin columna nueva. | El solicitante eligió que se le cancele (SPEC-0008 Q-7). |

## Consecuencias

- **Positivas:**
  - Outlook se activa sin aplicación de Entra ID.
  - Los invitados y la lista del equipo reciben la reunión, con actualizaciones, cancelaciones y recordatorio. Quien se quita recibe
    la cancelación.
  - Estados, límites, auditoría y reintento de la agenda no cambian.
  - La validación estricta de direcciones mejora también lo que se guarda.
- **Negativas / riesgos:**
  - **Fecha límite externa (R-2).** Desde diciembre de 2026, sin acción del tenant, `smtp_basic_auth_disabled`. Con la retirada
    definitiva (2027), este adaptador deja de funcionar.
  - **Una identidad del directorio sin MFA ni control preventivo (R-1).**
    - Con la contraseña, alguien puede suplantar a la agenda, iniciar sesión, leer el directorio y el propio buzón, y pedir tokens de
      Graph.
    - Los ajustes del tenant (decisión 2) cierran el consentimiento, el registro de aplicaciones y las invitaciones. La detección
      llega con hasta una semana de retraso.
    - Es un blanco visible de *password spraying*, que además puede bloquear la cuenta y dejar la agenda sin envío.
  - **Una excepción a la constitución** con aceptación caducable, que se aplica técnicamente (decisión 11).
  - **Dependencia del tenant (R-5):** SMTP AUTH en el buzón, valores predeterminados ya desactivados y los ajustes de la decisión 2.
  - **Publicado no significa entregado (R-3)**, y las direcciones que el filtro de dominios actual excluye no reciben la cancelación.
  - Una lista más de direcciones por evento, hasta que se cancela o se completa. Sin límite si una actividad vencida nunca se cierra
    o su cancelación queda en Error. Las copias la conservan durante su retención.
  - Una dependencia nueva, MailKit/MimeKit (R-4).
  - Un lote grande tarda más y deja parte Pendiente.
- **Seguimiento:**
  - Pasar a OAuth 2.0 para SMTP (o a Graph, ADR-0016) **antes de la retirada definitiva**. Se revisa al publicarse, y no más tarde
    del **2027-05-05**.
  - Comprobar, antes de finales de diciembre de 2026, que SMTP AUTH sigue permitido para la cuenta (SPEC-0008 CA-7).
  - Limitar la cuenta por IP o cambiar el mecanismo, como prevé el solicitante (SPEC-0008 Q-9).
  - Revisar la aceptación de la excepción no más tarde del 2027-05-05 y renovar `SmtpExceptionAcceptedUntil` solo con una nueva
    aceptación.

## Compuertas de aceptación

El solicitante acepta este ADR solo cuando se cumplan las tres compuertas:

1. **Aceptación de los riesgos.** Acepta expresamente R-1 (la excepción de §3.8, con el alcance del contexto) y R-5, tal como los
   describe SPEC-0008 §14.4, con revisión no posterior al 2027-05-05. Consta que descartó expresamente OAuth 2.0 y el *relay*
   (Q-12). **Cumplido el 2026-11-06.**
2. **Condición previa del tenant**, confirmada por quien lo administra y anotada en SPEC-0008 §18 sin identificadores (en
   §19.1 queda la respuesta):
   - los valores predeterminados de seguridad estaban desactivados antes de esta decisión, por motivos ajenos a ITMas;
   - el tenant no tiene acceso condicional.
   Los demás controles de la decisión 2 son criterios de aceptación de la spec (CA-6), que se verifican antes de activar el envío.
   **Cumplido el 2026-11-06:** el solicitante confirma las dos cosas (SPEC-0008 Q-11 y Q-13). Quien administra el tenant lo
   anotará también en SPEC-0008 §18 al verificar CA-6.
3. **Un único cambio** con todo lo de §«Cambios al aceptar»: la constitución, los estados de `specs/README.md`, ADR-0016 y la
   enmienda de SPEC-0006 cambian a la vez (constitución §10.1). **Cumplido el 2026-11-06.**

## Cambios al aceptar

- **Constitución:** §3.8 con el texto de abajo, la cabecera en 6.0.0 y la fila del historial.
- **ADR-0016:** una nota en la cabecera: solo se aplica con `AGENDA_OUTLOOK_TRANSPORT=graph`; con `smtp` rige ADR-0023.
- **SPEC-0006:** la enmienda que enumera SPEC-0008 §10.1, registrada en su §19.
- **SPEC-0008:** estado y respuesta a Q-13.
- **`specs/README.md`** y **`specs/features/README.md`:** los estados de este ADR y de SPEC-0008.
- La documentación operativa (README, SECURITY, DEVELOPMENT, ARCHITECTURE, modelo de amenazas, manual) cambia con la
  implementación de SPEC-0008 (su tarea 7), porque describe un comportamiento que aún no existe. Hasta entonces, Outlook sigue
  desactivado en producción.

## Texto de la constitución (versión 6.0.0)

La cabecera pasa a `**Versión:** 6.0.0 · **Vigente desde:** 2026-11-06` y §3.8 queda así:

> 8. Mínimo privilegio en todo: roles de base de datos, contenedores no root, capacidades eliminadas, redes internas, puertos no
>    publicados salvo el del frontend, y credenciales con el alcance justo para su función. Solo se admiten estas excepciones, cada una
>    con sus propios controles:
>    - **(a)** el agente de dispositivo (§13) se ejecuta con privilegios elevados, porque los necesita para leer el estado del
>      sistema, con los controles compensatorios de §13.4;
>    - **(b)** las credenciales de copias, en los términos de §8.7(e);
>    - **(c)** una **credencial saliente**, que ITMas usa para llamar a un servicio externo, que no esté acotada a su función, porque
>      la alternativa acotada no está disponible o porque la persona responsable la descarta de forma expresa. Es admisible solo si:
>      - es de una **cuenta de servicio**: no es de una persona, solo la usa ITMas y no se comparte con otros sistemas;
>      - **no** tiene roles de administración ni privilegiados en el servicio externo, ni derechos sobre recursos de otros (por
>        ejemplo, enviar como otro buzón);
>      - si es una cuenta de usuario del directorio de la organización, no recibe ninguna política en ITMas (§3.4);
>      - un **ADR `Accepted` con revisión de `security`** declara:
>        1. su alcance real: a qué sistemas y datos da acceso además de su función;
>        2. los controles compensatorios que se aplican y los que faltan;
>        3. las alternativas acotadas que se descartan, su coste y el motivo;
>        4. quién conoce o custodia la credencial;
>        5. cómo se retiraría la excepción; y
>        6. la aceptación expresa del riesgo por una persona responsable, con su fecha y una fecha de revisión no posterior a seis
>           meses desde la aceptación.
>      - Vencida esa fecha sin revisión, la aceptación caduca y la funcionalidad que usa la credencial DEBE desactivarse,
>        preferiblemente de forma automática, hasta una nueva aceptación expresa.
>
>      Esta excepción NO DEBE aplicarse a:
>      - credenciales que den acceso al servidor, a la base de datos, al despliegue, al registro o a las copias (estas últimas se rigen
>        por §8.7(e));
>      - credenciales con privilegios de administración en cualquier sistema;
>      - credenciales de la aplicación de ITMas en el proveedor de identidad, y credenciales con privilegios sobre la configuración
>        del proveedor de identidad o de la nube que da soporte a ITMas. Una cuenta de usuario del directorio sin roles no queda
>        excluida por esto;
>      - la autenticación de personas o de dispositivos en ITMas (§3.2).

Fila del historial:

> | 6.0.0 | 2026-11-06 | MAJOR: §3.8 enumera sus excepciones y admite una nueva para una credencial saliente que no esté acotada a su función, porque la alternativa acotada no está disponible o se descarta de forma expresa. Solo para una cuenta de servicio exclusiva de ITMas, sin roles de administración ni derechos sobre recursos de otros, y sin políticas en ITMas si es una cuenta del directorio. Lo exige declarado en un ADR aceptado con revisión de `security`: su alcance real, los controles que se aplican y los que faltan, las alternativas descartadas con su coste y su motivo, quién la custodia, cómo se retiraría, y la aceptación expresa con revisión a seis meses como máximo; al caducar, la funcionalidad se desactiva. No se aplica a credenciales del servidor, la base de datos, el despliegue, el registro, las copias o la administración, ni a las de la aplicación de ITMas o con privilegios sobre la configuración del proveedor de identidad o de la nube, ni a la autenticación de personas o dispositivos (ADR-0023). |

## Versión: MAJOR 6.0.0

Este cambio relaja una regla, y §1.2(c) exige MAJOR:

- §3.8 admite una excepción nueva al mínimo privilegio para credenciales salientes.
- La frase «Única excepción: el agente de dispositivo», que ya no era exacta desde la 5.0.0 (§8.7(e)), pasa a ser una lista de las
  tres excepciones. No cambia lo que ya admitían.

## Impacto en la constitución

**Requiere la enmienda de §3.8 de la sección anterior** (§1.2: este ADR, revisión de `architecture` y `security`, versión MAJOR e
historial).

- **Por qué:** sin acceso condicional, la contraseña es una identidad del directorio sin MFA que permite más de lo que su función
  necesita. Además, la alternativa acotada (OAuth 2.0) existe y se descartó, así que la credencial no es de mínimo privilegio.
- **Precedente:** la 5.0.0 resolvió un caso equivalente para las copias con una excepción declarada, aceptada y caducable
  (§8.7(e), ADR-0022). Esta enmienda sigue el mismo patrón, con un suelo absoluto y una lista de exclusiones para que no se reutilice
  con credenciales débiles de otra clase.
- **Sin cambio:** §4 (la credencial, por variable de entorno), §9.4 (dobles y Testcontainers) y §12.5 (MailKit justificado).

## Revisiones (2026-11-05, independientes y de solo lectura, un agente por rol)

- **Primera ronda** (sobre la primera redacción; detalle en SPEC-0008 §19.3). `architecture`, `security` y `database`: «aceptable
  con cambios». Llevó a:
  - las secuencias separadas;
  - la lista de quienes pueden tener la reunión;
  - el alcance real del puerto;
  - host fijo y TLS estricto;
  - una autenticación por lote;
  - `jsonb`;
  - el alcance real de la credencial, que con la respuesta a Q-11 llevó a esta enmienda.
- **Segunda ronda** (sobre la versión con la enmienda). Las tres «aceptable con cambios», con los altos resueltos así:
  - **`architecture`:**
    - A-1, la condición de la enmienda no amparaba un descarte por preferencia: ahora la condición es declarativa e incluye las
      alternativas descartadas y su coste;
    - A-2, faltaba un suelo y §3.8 decía «única excepción»: hay suelo, lista de excepciones, cabecera, historial y compuertas;
    - A-3, la lista se escribía después del envío: escritura anticipada, plazo propio y quitar solo con la revisión igual;
    - A-4, el puerto no modelaba el lote: `StopBatch`, id en el fallo, `ConfirmedRemovals`, `RejectedRecipients` y un adaptador
      *scoped*;
    - medios y bajos incorporados: presupuesto por mensaje y por evento, 2 conexiones y `432 4.3.2`, la cancelación de seguimiento
      con `item.Data`, códigos neutros con nombre, la enmienda de SPEC-0006 completa, `5.1.8` y `5.7.30`, OCSP/CRL y la tabla de
      §19.1.
  - **`security`:**
    - S-1, la condición de la enmienda: igual que A-1, con custodia y exclusiones ampliadas;
    - S-2, el alcance real estaba incompleto: contexto y R-1 completos, ajustes del tenant sin P1, SSPR, reenvío externo y
      procedimiento ante un compromiso;
    - S-3, la revisión mensual no era posible con 7 días de retención: semanal, o mensual con Purview;
    - S-4, los valores predeterminados son ahora una condición previa (compuerta 2);
    - medios y bajos incorporados:
      - evaluar el MFA por usuario con contraseña de aplicación;
      - caducidad técnica;
      - `StopBatch` y pausa de 15 minutos tras un fallo de `AUTH`;
      - validación estricta al guardar;
      - contraseña nueva;
      - retención de 30 días.
  - **`database`:**
    - D-1, añadir en la segunda escritura perdía destinatarios: escritura anticipada;
    - D-2, la regla de la revisión: quitar solo con la revisión igual;
    - D-3, los estados finales: nunca añaden, y el dominio garantiza el `CHECK`;
    - medios y bajos incorporados: solo `smtp` mantiene la lista, `ConfirmedRemovals` y los rechazos `RCPT`, el cambio de
      transporte sin reinicio de columnas, vacío igual a `NULL` y orden `Ordinal`, el bloqueo del `CHECK` y el `Down`, y los datos
      personales en copias y en actividades sin cerrar.
  - **Decisiones de redacción:** no se exige asignación en la aplicación empresarial de ITMas, porque cambiaría el acceso de todas las
    personas y la cuenta no recibe permisos (§3.4). No se añade un `CHECK` de estado final, porque el dominio ya lo garantiza y un
    23514 sin traducir rompería la regla de que un fallo del envío no impide la operación.
- **Verificación** (sobre la versión anterior). `architecture` (con la lente de `database`) y `security`: «aceptable con cambios».
  Confirmaron resueltos los altos de las dos rondas y comprobaron en Microsoft Learn que los ajustes del tenant sin P1 existen.
  Lo nuevo se resolvió así:
  - **`architecture`:**
    - V-A1, la regla por evento no cabía con dos o más quitados: un único `CANCEL` para todos y solo la regla por mensaje, con envío
      parcial;
    - V-A2, el puerto no expresaba Pendiente: `NotAttempted` y el instante límite en la entrada;
    - V-A3, la cancelación de seguimiento sobraba y estropeaba el estado: sin seguimiento con `smtp` y `MarkOutlookPublished`
      monótono;
    - medios:
      - rechazos `RCPT` solo de lo añadido por el trabajo;
      - envío activo calculado en `Application`, para que el aviso aparezca;
      - presupuesto desde el inicio de la petición, con la conexión y el *gate*;
      - exclusión de la enmienda precisada;
    - bajos: `Transport` en `AgendaSettings`, la regla de direcciones antes de la escritura anticipada, el `CANCEL` a quien quizá no
      recibió el `REQUEST` (R-3), el token propio en la segunda escritura, las Canceladas sin confirmar en el cambio de transporte, y
      la tarea 1 del plan.
  - **`security`:**
    - V-S1, la exclusión del proveedor de identidad abarcaba esta credencial: se limita a la aplicación de ITMas y a quien tenga
      privilegios de configuración, se admite la cuenta del directorio sin políticas y se exige un ADR `Accepted` con revisión de
      `security`;
    - medios:
      - custodia: rotación tras la prueba y copia sellada;
      - al vencer, el tenant bloquea la cuenta, además de apagar el envío;
      - contraseña de aplicación: custodia del segundo factor, el control de métodos y borrarlas en el procedimiento;
      - persistencia por métodos: grupo de exclusión único, o el riesgo aceptado en R-1;
      - participantes ya guardados: consulta previa y corrección;
    - bajos: `5.7.139` como `smtp_basic_auth_disabled`, la parte local limitada por `mailto:`, la revisión cada 6 días y la
      comprobación de Purview.
- **Revisión de la implementación** (2026-11-10, SPEC-0008 §18): `architecture`, `security`, `database`, `testing` y `frontend`,
  todas «aceptable con cambios». Precisaron esta decisión en tres puntos, recogidos arriba:
  - la cancelación neutra a quien se quita (decisión 4);
  - olvidar a quien fue rechazado para siempre en el `CANCEL`, sin que un `CANCEL` fallido bloquee la actualización (decisión 5);
  - la conexión que se conserva ante un rechazo, y el margen del plazo propio (decisión 7).
  Además, una actividad de Graph responde `outlook_rejected` sin abortar el lote (decisión 10). Ninguno cambia el alcance de la
  credencial ni los riesgos aceptados.
- **Revisión de `security` de la enmienda del 2026-11-10** (SPEC-0008 §19.4), independiente y de solo lectura: «aceptable con
  cambios». La retirada cabe en §3.8 (c), que exige declarar los controles que faltan, no un mínimo de detección, pero no es válida
  sin la nueva aceptación expresa. Precisó lo que detectan las alertas, la persistencia que sobrevive a la rotación y que el MFA y
  el grupo de exclusión constaran como decididos. Todo incorporado en las decisiones 2 y 12.
