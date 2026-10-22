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

Cuando Outlook está configurado, programar publica la actividad en el calendario compartido del área y los invitados
reciben una invitación:

- **Invitados:** el responsable, los participantes y, si la actividad tiene activado **Notificar al equipo**, la lista de
  distribución del equipo (como invitada opcional).
- **Asunto:** el tipo entre corchetes y el título, por ejemplo "[Auditoría interna] Revisión de accesos".
- **Contenido:** en texto plano, con la descripción, el alcance, el impacto y un enlace a la actividad en ITMas. Las
  etiquetas no se envían.
- **Recordatorio y categoría:** el recordatorio de la actividad y el tipo como categoría de Outlook.
- Si **modificas** una actividad programada, la invitación de Outlook se actualiza. Si la **cancelas**, los invitados
  reciben la cancelación con el motivo. Registrar la ejecución no cambia nada en Outlook.
- ITMas es la fuente de verdad: lo que se edite directamente en Outlook se sobrescribe la próxima vez que se publique.

En el detalle de la actividad, la tarjeta **Outlook** muestra el **Estado** de la publicación (siempre con ícono y texto):
**No publicado**, **Pendiente**, **Publicado** o **Error**, y, si se publicó, el botón **Abrir en Outlook**.

### Si la publicación falla

Un fallo de Outlook **nunca impide** programar, modificar ni cancelar: la actividad queda **Programada** y su estado en
Outlook pasa a **Error** con un texto que explica la causa en general ("No se pudo publicar en Outlook." y, por ejemplo,
"Outlook no está disponible."). Cuando se resuelva, usa **Sincronizar de nuevo** en el menú de la fila o en el detalle: es
seguro repetirlo, no crea invitaciones duplicadas. **Sincronizar pendientes con Outlook** (encima de la lista del año)
reintenta de una vez todas las programadas que aún no empezaron y están pendientes, con error o sin publicar. Si Outlook pide
esperar por demasiadas solicitudes, ITMas se detiene, deja el resto como **Pendiente** y puedes reintentar más tarde.

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
| Una actividad quedó **Pendiente** en Outlook | Una publicación se interrumpió (por ejemplo, por demasiadas solicitudes): pulsa **Sincronizar de nuevo**. |
| Me equivoqué y cancelé una actividad | Una actividad cancelada no se reactiva: crea una nueva con los mismos datos. |
| No veo **Programar** ni **Cancelar** | Necesitas el permiso `agenda.publish`; quien puede editar no necesariamente puede programar. Ver [Qué puedo hacer: permisos y cómo solicitarlos](permisos-y-acceso). |
| "Se alcanzó el límite de avisos por hora de la agenda. Inténtalo más tarde." o "Demasiadas solicitudes." | Programar, cancelar, sincronizar y modificar actividades programadas comparten un límite por hora para todo ITMas. Espera y vuelve a intentarlo. |
