---
id: ADR-0016
title: Integración con Outlook mediante Microsoft Graph con permisos de aplicación acotados
status: Proposed
date: 2026-10-17
deciders: [architecture, security]
related: [SPEC-0006, ADR-0002]
---

# ADR-0016 — Integración con Outlook mediante Microsoft Graph con permisos de aplicación acotados

## Contexto
SPEC-0006 (agenda anual de infraestructura) debe publicar las actividades programadas en un calendario compartido de
Outlook: invitaciones, actualizaciones, cancelaciones y recordatorios para el equipo. Es la primera integración saliente
de ITMas con Microsoft 365. El solicitante fijó el marco (SPEC-0006 §19):

- El correo está en **Exchange Online** y el equipo de infraestructura administra el tenant (Q-1).
- El destino es un **buzón compartido nuevo**, y "notificar al equipo" invita a una lista de distribución que ya existe
  (Q-2).
- Las invitaciones van solo al dominio corporativo (Q-4).
- **ITMas es la fuente de verdad** y Outlook, el canal de aviso, en un solo sentido (P-1, P-14).
- MVP simple: la sincronización ocurre en la propia petición, sin cola (P-2).

Fuerzas en juego:

- ADR-0002 prevé que, si una spec necesita llamar a APIs aguas abajo (como Graph), el token se adquiera en el servidor
  con la caché de `Microsoft.Identity.Web`, es decir, con permisos **delegados**. La API no guarda tokens
  (`SaveTokens=false`).
- Un permiso de **aplicación** de calendarios alcanza, por defecto, **todos los buzones** del tenant. Exchange Online
  permite acotarlo a buzones concretos con *RBAC for Applications*, que sustituye a las *application access policies*
  ([documentación de Exchange](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac)).
- Graph no admite permisos de aplicación para crear eventos en calendarios de grupo de Microsoft 365, solo delegados
  ([documentación de Graph](https://learn.microsoft.com/en-us/graph/api/group-post-events?view=graph-rest-1.0)), y
  Outlook no ofrece recordatorios para eventos de grupo
  ([recurso `event`](https://learn.microsoft.com/en-us/graph/api/resources/event?view=graph-rest-1.0)).
- Constitución: §3.8 (mínimo privilegio), §4 (secretos), §9.4 (pruebas sin servicios externos) y §12.5 (dependencias).

## Decisión
1. **Destino.** El calendario predeterminado de un **buzón compartido** de Exchange Online dedicado a la agenda
   (`AGENDA_OUTLOOK_MAILBOX`). Ese buzón es el organizador de todos los eventos, y el equipo tiene permiso de solo
   lectura sobre su calendario.
2. **Identidad y permiso.** Una **aplicación de Entra ID propia**, distinta de la del BFF, sin permisos delegados y con
   un solo permiso: `Calendars.ReadWrite` de aplicación, concedido **únicamente mediante RBAC for Applications**, con un
   ámbito de gestión que contiene solo el buzón de la agenda (rol `Application Calendars.ReadWrite`).
   - **No** se consiente `Calendars.ReadWrite` en Entra ID: los permisos de las dos fuentes se suman y el consentimiento
     global anularía el límite.
   - No se conceden otros permisos (`Mail.Send`, `MailboxSettings.ReadWrite`, etc.).
3. **Credencial.** Un secreto de cliente por variable de entorno (`AGENDA_OUTLOOK_CLIENT_SECRET`), como la aplicación del
   BFF, con caducidad corta. **Se recomienda certificado para producción**: MSAL lo admite con un cambio de
   configuración, y queda como seguimiento (abajo). El secreto solo existe en el contenedor `api` y nunca en el
   repositorio, en logs ni en la línea de comandos (§4).
4. **Adaptador.** `GraphOutlookCalendarGateway` en `Infrastructure/Agenda/`, que implementa el puerto
   `IOutlookCalendarGateway` de `Application`.
   - Usa **MSAL** (`Microsoft.Identity.Client`, ya presente como dependencia de `Microsoft.Identity.Web`; si hace falta,
     con referencia propia en `Directory.Packages.props`) para el token de aplicación y un `HttpClient` tipado con
     tiempo máximo de 10 s.
   - **Sin el SDK de Microsoft Graph**: arrastra muchas dependencias para cuatro operaciones.
5. **Operaciones y destinos.** Solo `login.microsoftonline.com` y `graph.microsoft.com`, con rutas fijas bajo
   `/users/{buzón}/calendar/events`. Ninguna URL procede de la entrada de las personas.
   - **Crear** (`POST`) con `transactionId` igual al id del evento de la agenda: un reintento tras agotar el tiempo no
     duplica el evento, y `transactionId` no se puede cambiar después de crearlo.
   - **Actualizar** (`PATCH`) con el id guardado. Ante `404`, se vuelve a crear.
   - **Cancelar** (`POST …/cancel`) con el motivo como comentario. Envía el aviso de cancelación a los invitados, solo
     lo puede hacer el organizador (el buzón) y responde `202`.
6. **Contenido.**
   - Asunto `[<Tipo>] <Título>` y cuerpo en **texto plano**.
   - Categoría con el nombre del tipo. Si no existe en la lista de categorías del buzón, Outlook la muestra sin color;
     darles color es una tarea manual de quien administra el buzón, porque no se concede `MailboxSettings.ReadWrite`.
   - Recordatorio del evento.
   - Invitados: responsable, participantes y, opcional, la lista del equipo. Los participantes se limitan a los dominios
     permitidos y a 30 por evento (Graph admite 500).
   - Las etiquetas de ITMas no se envían.
7. **Sincronización en línea, sin cola.** Se guarda el cambio, se llama a Graph en la misma petición y se guarda el
   resultado (`published` o `error` con un código normalizado). Un fallo de Graph nunca impide guardar, programar,
   cancelar ni cerrar. El reintento es manual e idempotente. Ante `429` se respeta `Retry-After` y se detiene el lote.
   El criterio para pasar a segundo plano está en SPEC-0006 RNF-6.
8. **Errores.** Solo se guarda y se muestra un código de un conjunto cerrado (`graph_unauthorized`, `graph_forbidden`,
   `graph_not_found`, `graph_throttled`, `graph_unavailable`, `graph_timeout`, `graph_rejected`, `not_configured`). El
   cuerpo de las respuestas de Graph no se registra ni se devuelve.
9. **Configuración y arranque.** Desactivado por defecto (`AGENDA_OUTLOOK_ENABLED=false`). Con `true`, las variables
   `AGENDA_OUTLOOK_*` son obligatorias y la API no arranca si faltan, sin imprimir valores.
10. **Prerrequisitos del tenant** (SPEC-0006 §14.4). Se documentan en SECURITY.md y DEVELOPMENT.md, con su verificación
    mediante `Test-ServicePrincipalAuthorization`. Los cambios de RBAC tardan entre 30 minutos y 2 horas en aplicarse
    por la caché de permisos.
11. **Pruebas** (§9.4). Un doble de `IOutlookCalendarGateway` y un `HttpMessageHandler` simulado para el contrato HTTP del
    adaptador. Ninguna prueba llama a Microsoft.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| **Calendario compartido (buzón compartido) + Graph con permisos de aplicación acotados a ese buzón** (elegida) | Invitaciones, recordatorios, actualizaciones y cancelaciones nativas; funciona sin una persona conectada; el equipo lo ve en Outlook de escritorio, web y móvil; el permiso se puede limitar a un solo buzón. | Requiere pasos de un administrador del tenant; hay una credencial de aplicación que proteger; dependencia externa. |
| Calendario de un grupo de Microsoft 365 | Modelo natural de un equipo. | Graph no admite permisos de aplicación para crear eventos de grupo y Outlook no ofrece recordatorios para ellos: exigiría tokens de personas guardados en el servidor. |
| Graph con permisos delegados (token de la persona, vía BFF) | Sin credencial de aplicación. | Los eventos irían al calendario de quien actúa o exigirían permisos de delegado sobre el buzón para cada persona; habría que guardar *refresh tokens* y no permitiría sincronizar en segundo plano; choca con `SaveTokens=false`. |
| Suscripción a un feed iCalendar (`webcal`) | Sin permisos en Microsoft 365. | Solo lectura y por sondeo: en Outlook web la actualización depende de la programación de Microsoft (horas) y no se configura; sin invitaciones ni recordatorios propios; la URL secreta hay que protegerla. |
| Invitaciones iCalendar por SMTP (`METHOD:REQUEST`) | No depende de Graph. | Necesita un relay SMTP y una cuenta emisora; actualizar o cancelar de forma fiable es frágil; sin estado de entrega. |
| Power Automate / Logic Apps | Poco código. | Queda fuera del repositorio y del flujo SDD; difícil de probar y versionar; credenciales fuera de ITMas. |
| SDK de Microsoft Graph en lugar de MSAL + `HttpClient` | Menos código propio. | Arrastra Kiota y muchas dependencias para cuatro operaciones (§12.5). |

## Consecuencias
- **Positivas:**
  - Invitaciones, actualizaciones, cancelaciones y recordatorios nativos de Outlook, sin personas conectadas.
  - Alcance mínimo y verificable: un solo buzón y un solo permiso.
  - ITMas no guarda tokens de personas; la API sigue sin conservar los del BFF.
  - Sin SDK de Graph y con un puerto que facilita las pruebas y un eventual cambio de proveedor.
- **Negativas / riesgos:**
  - Hay una credencial de aplicación nueva que proteger y rotar; su vencimiento se lleva en la propia agenda.
  - Depende de pasos manuales del administrador del tenant. Un consentimiento global de `Calendars.ReadWrite` en Entra ID,
    hecho después por error, anula el límite del buzón.
  - La caché de permisos de RBAC retrasa los cambios hasta 2 horas.
  - Es una dependencia externa: sin conexión o con límites de Graph, las publicaciones quedan en Error o Pendiente hasta
    el reintento manual.
  - Es unidireccional: lo que se edite en Outlook se sobrescribe y las respuestas de asistencia no se leen.
  - Las invitaciones salen del buzón corporativo: un abuso desde ITMas sería correo corporativo legítimo. Se mitiga con
    `agenda.publish` separado, dominios permitidos, límite de invitados y auditoría.
- **Seguimiento:**
  - Pasar a certificado (o a una identidad administrada si el despliegue lo permite) antes de la primera puesta en
    producción, o aceptar de forma explícita el secreto con caducidad corta (`security`).
  - Verificar periódicamente el alcance (SPEC-0006 CA-24): que la aplicación solo opere sobre el buzón de la agenda.
  - Guardar el `webLink` del evento (columna que concreta `database`) para el enlace "Abrir en Outlook" (SPEC-0006 RF-14).
  - Leer las respuestas de asistencia en una versión posterior, si el equipo lo pide.
  - Procesamiento en segundo plano según SPEC-0006 RNF-6.
  - Revisión de `security` pendiente de: el alcance con RBAC, la credencial, los dominios permitidos y los límites, y la
    lectura de la constitución (abajo).

## Impacto en la constitución
Ninguno. Se aplican §3.8 (mínimo privilegio: permiso acotado a un buzón), §4 (la credencial, por variable de entorno),
§9.4 (pruebas sin servicios externos) y §12.5 (dependencias). §3.2 regula la autenticación de personas y de
dispositivos; una credencial de aplicación saliente hacia Microsoft 365 no es ninguna de las dos. `security` debe
confirmar esta lectura: si no la compartiera, sería una enmienda MINOR a §3.
