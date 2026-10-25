# Planificar actividades y series

Para planificar el año se crean **actividades** puntuales (un parche, una auditoría) o **series** que se repiten (parches
cada mes, un escaneo cada 90 días). Todo lo que se crea nace en **Borrador**: no se avisa a nadie hasta que alguien con
permiso la **programa** (ver [Programar y cancelar actividades](programar-y-cancelar)).

**Quién puede hacerlo:** `agenda.create` (*Crear actividades*) para crear y `agenda.update` (*Modificar actividades*) para
modificar y eliminar borradores. Modificar una actividad ya **Programada**, o una serie que tenga actividades programadas,
exige además `agenda.publish` (*Programar y cancelar actividades*), porque el cambio avisa a sus invitados.

## Crear una actividad

1. Abre **Agenda → Actividades** y pulsa **Nueva actividad**. También puedes pulsar un día vacío de la vista **Mes** o
   una hora vacía de la vista **Semana** que todavía no haya pasado: se abre **Nueva actividad** con esa fecha (y hora)
   ya escritas, que puedes cambiar (ver [Consultar la agenda del año](consultar-agenda)). Si pulsas el día de **hoy**, la
   hora propuesta es la actual más una, para que no empiece en el pasado.
2. Elige el **Tipo de actividad**. Debajo se muestran la duración y el recordatorio por defecto de ese tipo, y si es un
   periodo de congelamiento (en el que no deben programarse cambios).
3. Escribe el **Título** (hasta 120 caracteres) y, si quieres, la **Descripción**. No escribas contraseñas, claves ni
   hallazgos de seguridad: la descripción es visible para todo el equipo.
4. Indica cuándo ocurre:
   - Una actividad con hora: la **Fecha de inicio** y la **Hora de inicio**. Si no pones fin, dura lo que indica el tipo.
     Para poner fin, usa **Fecha de fin** y **Hora de fin**.
   - Una actividad de todo el día: activa **Todo el día** y elige el **Primer día** y, si dura más de uno, el **Último
     día (inclusive)**.
   - Puedes escribir la fecha como día/mes/año (por ejemplo, 24/11/2026) o elegirla en el calendario.
5. Elige al **Responsable** entre las personas activas de ITMas (por defecto, tú).
6. Si quieres, añade **Participantes**: una dirección de correo por vez, pulsando **Intro**.
7. Ajusta **Notificar al equipo**, el **Impacto** (**Ninguno**, **Degradación** o **Indisponibilidad**) y el
   **Recordatorio**. Si no eliges recordatorio se usa el del tipo.
8. Si quieres, escribe el **Alcance** (qué sistemas o equipos abarca), el **Lugar** (por ejemplo, "Remoto") y las
   **Etiquetas** (ver [Etiquetar actividades y filtrar la agenda](etiquetar-y-filtrar)).
9. Pulsa **Crear actividad**. Se abre su detalle y aparece en la lista como **Borrador**.

### Reglas

- La fecha de inicio no puede estar en el pasado, el fin debe ser posterior al inicio y una actividad dura como máximo 60
  días.
- Los participantes deben ser direcciones válidas y de los dominios permitidos; hasta 30 por actividad.
- El responsable debe ser una persona activa. Si después se desactiva, la actividad lo muestra como "Responsable
  inactivo" y hay que reasignarla.

### Advertencias

Al crear o modificar, ITMas avisa (sin impedirte guardar) cuando la actividad:

- se solapa con un periodo de congelamiento: "Esta actividad se solapa con el congelamiento «…»";
- tiene impacto y se solapa con otra que también lo tiene: "Se solapa con «…», que también tiene impacto".

Las advertencias aparecen en el detalle y, como aviso **Advertencias**, en la lista.

## Crear una serie

1. Pulsa **Nueva serie**.
2. Rellena la **Primera actividad** como en el apartado anterior: es la plantilla de todas las demás.
3. En **Repetición**, indica **Repetir cada** un número (de 1 a 99) y la **Unidad** (días, semanas, meses o años). Por
   ejemplo: cada 1 mes es mensual, cada 3 meses es trimestral y cada 90 días es cada 90 días.
4. Si repites por meses o años, elige **Qué día se repite**: **Un día del mes** (déjalo vacío para usar el día de la
   primera actividad) o **El n-ésimo día de la semana** (por ejemplo, el segundo martes). Si el día elegido no existe en
   un mes (30 o 31), la actividad cae el último día de ese mes.
5. En **Hasta cuándo**, elige **Número de actividades** (hasta 60) o **Hasta una fecha**.
6. Pulsa **Vista previa**. Aparece la tabla con las fechas que se crearían y sus advertencias; todavía no se guarda nada.
7. Si todo está bien, pulsa **Crear serie**. Si cambias cualquier dato después de la vista previa, vuelve a pedirla:
   solo se crea lo que se mostró.

Cada actividad de la serie es independiente: tiene su propio estado, responsable y evidencia. Las fechas no se mueven por
fines de semana ni por feriados: si una cae en un día no laborable, edítala a mano.

## Modificar una actividad

1. Abre su detalle y pulsa **Editar** (o **Editar** en el menú de su fila).
2. Cambia lo que necesites y pulsa **Guardar cambios**.

Solo se pueden modificar actividades en **Borrador** o **Programada**. Para una **Programada** necesitas también
`agenda.publish`: si Outlook está configurado, sus invitados reciben la actualización. Una **Completada** solo admite
cambios en sus notas y enlaces (ver [Registrar la ejecución de una actividad](registrar-ejecucion)) y en sus etiquetas;
una **Cancelada** no admite cambios (salvo sus etiquetas).

Un **Borrador** puede eliminarse con **Eliminar borrador**. Una actividad ya programada no se elimina: se cancela.

## Modificar los campos comunes de una serie

Sirve, por ejemplo, para reasignar al responsable de todo el año.

1. Abre una actividad de la serie que todavía no haya empezado y pulsa **Editar la serie**.
2. Cambia el tipo, el título, la descripción, el responsable, los participantes, el impacto, el alcance, el lugar, el
   recordatorio o las etiquetas. En esta pantalla no hay fechas.
3. Pulsa **Aplicar a la serie** y confirma.

El cambio llega a las actividades de la serie que aún no han empezado y están en Borrador o Programada. Las fechas no
cambian y las demás actividades no se tocan. Si alguna de las que cambian está programada, necesitas también
`agenda.publish`.

## Resultado

Las actividades quedan en **Borrador**, visibles en la agenda para todo el equipo, listas para que alguien las programe.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "El inicio no puede estar en el pasado." | Elige una fecha y hora futuras. El histórico anterior a ITMas no se registra. |
| "El fin debe ser posterior al inicio." | Corrige la fecha o la hora de fin. |
| "Elige una persona de la lista." | El responsable se elige de la lista que aparece al escribir; no basta con teclear el nombre. |
| "Introduce un correo válido." | Revisa la dirección del participante. Si el dominio no está permitido, ITMas lo rechaza al guardar. |
| "Otro usuario modificó esta actividad. Recarga los datos e inténtalo de nuevo." | Alguien la cambió mientras la editabas. Pulsa **Recargar datos** y repite tu cambio. |
| "Indica un número entero de 1 a 60." o "Una serie admite como máximo 60 ocurrencias." | Una serie crea hasta 60 actividades: reduce el número de actividades o acorta la fecha final. |
| No veo **Nueva actividad** ni **Editar** | Necesitas los permisos `agenda.create` o `agenda.update`. Ver [Qué puedo hacer: permisos y cómo solicitarlos](permisos-y-acceso). |
| No veo **Editar** en una actividad programada, o "Esta actividad está programada. Para modificarla necesitas también el permiso para programar y cancelar actividades." | Modificarla avisa a sus invitados: necesitas también `agenda.publish`. Pide el cambio a quien programa las actividades o solicita el permiso. |
| "Modificar una actividad programada avisa a sus invitados: necesitas también el permiso agenda.publish." al aplicar un cambio a una serie | Alguna actividad de la serie está programada. Pide el cambio a quien programa las actividades o solicita el permiso. |
| "Se alcanzó el límite de avisos por hora de la agenda. Inténtalo más tarde." | Modificar actividades programadas comparte un límite por hora con programar y cancelar. Espera y vuelve a intentarlo. |
