# Programar y cancelar actividades

Una actividad en **Borrador** solo existe en ITMas. **Programarla** la confirma; **cancelarla** la descarta antes de que
se ejecute. Son acciones que afectan a otras personas, por eso tienen su propio permiso, distinto del de quien prepara el
plan.

**Quién puede hacerlo:** `agenda.publish` (*Programar y cancelar actividades*). Para modificar una actividad ya
programada hacen falta `agenda.update` y `agenda.publish` (ver [Planificar actividades y series](planificar-actividad)).

## Programar

1. En **Agenda → Actividades**, programa una actividad de una de estas formas:
   - Abre el menú de su fila y elige **Programar**.
   - Abre su detalle y pulsa **Programar**.
   - Para varias a la vez, marca las casillas de los borradores (la casilla del encabezado marca todos los de la
     lista) y pulsa **Programar seleccionadas (n)**. Puedes programar hasta 50 por vez.
2. Confirma en el diálogo **Programar actividad** (o **Programar n actividades**).
3. ITMas informa el resultado: "Actividad programada." o "n actividades programadas."

Las actividades pasan a **Programada**. Si el aviso **Outlook no configurado** está visible, programar solo cambia el
estado de la actividad: no se envía ninguna invitación. Con Outlook configurado, el diálogo avisa "Se enviarán
invitaciones de Outlook a los invitados de cada actividad." (ver más abajo, **Qué recibe Outlook**).

Si en un lote alguna no se puede programar (por ejemplo, porque su inicio ya pasó), ITMas programa las demás y te dice
cuáles no pudo y por qué: "Se programaron n y m no se pudieron programar: …".

## Cancelar

1. Abre el menú de la fila de una actividad **Programada** y elige **Cancelar**, o abre su detalle y pulsa **Cancelar
   actividad**.
2. Escribe el **Motivo de la cancelación** (obligatorio, hasta 500 caracteres).
3. Pulsa **Cancelar actividad**. Si prefieres no cancelar, pulsa **Volver**.

La actividad pasa a **Cancelada** y muestra su motivo en el detalle. Una actividad cancelada no se puede volver a
programar ni modificar (solo se pueden editar sus etiquetas).

## Qué recibe Outlook

Cuando Outlook está configurado, programar envía una **invitación de Outlook por correo** desde la cuenta de la agenda, y la
reunión aparece en el calendario de cada invitado:

- **Invitados:** el responsable, los participantes y, si la actividad tiene activado **Notificar al equipo**, la lista de
  distribución del equipo (como invitada opcional).
- **Asunto:** el tipo entre corchetes y el título, por ejemplo "[Auditoría interna] Revisión de accesos".
- **Contenido:** en texto plano, con la descripción, el alcance, el impacto y un enlace a la actividad en ITMas. Las
  etiquetas no se envían.
- **Recordatorio y categoría:** el recordatorio de la actividad y el tipo como categoría de Outlook.
- Si **modificas** una actividad programada, los invitados reciben una actualización de la misma reunión, sin duplicarla.
  Si **quitas** a alguien (un participante, el responsable o la lista del equipo), esa persona recibe un aviso de que ya no
  está invitada, sin los datos de la actividad, y la reunión sale de su calendario.
- Si la **cancelas**, los invitados reciben la cancelación con el motivo. Registrar la ejecución no envía nada.
- ITMas es la fuente de verdad: lo que se edite directamente en Outlook no vuelve a ITMas y se sustituye en la próxima
  actualización.

En el detalle de la actividad, la tarjeta **Outlook** muestra el **Estado** del envío (siempre con ícono y texto):
**No publicado**, **Pendiente**, **Publicado** o **Error**. **Publicado** significa que el servidor de correo aceptó la
invitación; no confirma que haya llegado a cada buzón. Según cómo esté configurado ITMas, la tarjeta puede mostrar además
el botón **Abrir en Outlook**.

### Si el envío falla

Un fallo de Outlook **nunca impide** programar, modificar ni cancelar: la actividad queda **Programada** y su estado en
Outlook pasa a **Error** con un texto que explica la causa en general, por ejemplo "El servidor de correo rechazó las
credenciales de ITMas." o "Outlook no está disponible.". Cuando se resuelva, usa **Sincronizar de nuevo** en el menú de la
fila o en el detalle: es seguro repetirlo, no duplica la reunión. **Sincronizar pendientes con Outlook** (encima de la
lista del año) reintenta de una vez todas las programadas que aún no empezaron y están pendientes, con error o sin publicar.

Si programas o sincronizas muchas actividades a la vez, ITMas envía las que caben en ese momento y deja el resto como
**Pendiente**, sin error: vuelve a pulsar **Sincronizar pendientes con Outlook** pasado un minuto. Lo mismo ocurre si el
servidor de correo pide esperar.

## Resultado

El plan queda confirmado o descartado, y cada cambio queda registrado en la auditoría (ver [Consultar la
auditoría](consultar-auditoria)).

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "No se puede programar una actividad cuyo inicio ya pasó." | Edita la actividad y elige una fecha futura (ver [Planificar actividades y series](planificar-actividad)). |
| "Selecciona hasta 50 actividades a la vez." | Desmarca algunas y programa por tandas. |
| El botón **Programar seleccionadas** está desactivado | Marca al menos un borrador: solo los borradores se pueden programar. |
| "El motivo es obligatorio." | Escribe por qué se cancela; no basta con espacios. |
| El estado en Outlook dice **Error** | Pulsa **Sincronizar de nuevo**. Si sigue fallando, avisa a quien administra ITMas: el texto del detalle indica la causa en general. |
| Una actividad quedó **Pendiente** en Outlook | No cupo en el envío (programaste muchas a la vez) o el servidor de correo pidió esperar: pulsa **Sincronizar de nuevo** pasado un minuto. |
| "El servidor de correo rechazó las credenciales de ITMas." u otro error del servidor de correo | No depende de ti: avisa a quien administra ITMas. Las actividades siguen programadas y se envían con **Sincronizar de nuevo** cuando se resuelva. |
| Quité a alguien de una actividad y aún la ve en su calendario | Le llega una cancelación de esa reunión; si el estado en Outlook dice **Error** o **Pendiente**, pulsa **Sincronizar de nuevo**. |
| Me equivoqué y cancelé una actividad | Una actividad cancelada no se reactiva: crea una nueva con los mismos datos. |
| No veo **Programar** ni **Cancelar** | Necesitas el permiso `agenda.publish`; quien puede editar no necesariamente puede programar. Ver [Qué puedo hacer: permisos y cómo solicitarlos](permisos-y-acceso). |
| "Se alcanzó el límite de avisos por hora de la agenda. Inténtalo más tarde." o "Demasiadas solicitudes." | Programar, cancelar, sincronizar y modificar actividades programadas comparten un límite por hora para todo ITMas. Espera y vuelve a intentarlo. |
