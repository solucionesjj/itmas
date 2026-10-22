# Consultar la agenda del año

La agenda de infraestructura es el plan anual de las actividades del área: mantenimiento y parches, auditorías,
hardening, pruebas de vulnerabilidad, cambios de claves y más. Cada actividad tiene responsable, fecha, estado y, cuando
se ejecuta, su registro de ejecución.

**Quién puede hacerlo:** usuarios con el permiso `agenda.read` (*Consultar la agenda*).

## Las cuatro vistas

Elige cómo ver las actividades con el selector **Lista del año**, **Año**, **Mes** y **Semana**. Las cuatro comparten los
mismos filtros (ver [Etiquetar actividades y filtrar la agenda](etiquetar-y-filtrar)): al cambiar de vista, los filtros
se conservan.

| Vista | Qué muestra |
|---|---|
| **Lista del año** | Todas las actividades del año, agrupadas por mes y en orden de fecha, con el **Resumen** del año encima. Es la vista más completa: la única que permite seleccionar varias actividades. |
| **Año** | Los doce meses a la vez, como calendarios pequeños, con las actividades en los días en que ocurren. |
| **Mes** | Una cuadrícula con las semanas de lunes a domingo. Cada día muestra sus actividades con hora y título; las de varios días se dibujan abarcando sus días y, si no caben, aparece **+n más**. |
| **Semana** | Siete columnas con las horas de 00:00 a 24:00. Las actividades se colocan por hora y duración; las simultáneas van una junto a otra; las de todo el día o de varios días, en la franja superior; y una línea marca la hora actual. |

## Pasos

1. En el menú, abre **Agenda → Actividades**. La pantalla se titula **Agenda de infraestructura** y muestra la **Lista
   del año**.
2. Elige la vista que necesitas con el selector. Cada vista tiene sus flechas para moverte de periodo, con el nombre de
   lo que mueven: **Año anterior** y **Año siguiente**, **Mes anterior** y **Mes siguiente**, **Semana anterior** y
   **Semana siguiente** (en un celular, la vista Semana muestra un día por vez y las flechas se llaman **Día anterior** y
   **Día siguiente**). Pulsa **Hoy** para volver al periodo actual. Junto al título del periodo se indica la zona horaria
   de la organización: todas las horas se muestran en esa zona, estés donde estés.
3. En la **Lista del año**, revisa el **Resumen**: los doce meses con su cantidad de actividades y los totales **Por
   estado** y **Por tipo**. Pulsa un mes para ver solo sus actividades; púlsalo de nuevo para volver a todo el año.
4. Recorre la lista: las actividades aparecen agrupadas por mes y en orden de fecha. Cada fila indica:
   - **Fecha y hora** de la actividad (o "todo el día");
   - **Tipo**;
   - **Actividad** (el título, que abre su detalle), su impacto si lo tiene, sus etiquetas y el aviso **Advertencias**
     si se solapa con otras;
   - **Responsable** ("Responsable inactivo" si esa persona ya no tiene acceso a ITMas);
   - **Estado**;
   - **Outlook**: si la actividad se publicó en el calendario compartido. Mientras Outlook no esté configurado,
     aparece **No publicado** y arriba verás el aviso **Outlook no configurado**.
5. Pulsa el título de una actividad (o **Ver** en el menú de su fila) para abrir su **detalle**.

## Usar los calendarios

En las vistas **Año**, **Mes** y **Semana**:

- Cada actividad se dibuja con su **estado** como ícono y texto, nunca solo con color; encima del calendario está la
  leyenda **Estados de las actividades**. Una actividad **Vencida** lleva además su marca.
- Pulsa una actividad para abrir su detalle. También puedes llegar a ella con el teclado: con **Tab** pasas de un día o
  una actividad a otra y con **Intro** abres lo que tengas seleccionado.
- En la vista **Año**, pulsa un día para abrir la **Semana** de ese día y pulsa el nombre de un mes para abrir ese
  **Mes**. (En un celular, donde los meses van en una sola columna, el nombre del mes no se puede pulsar: usa el
  selector **Mes**.)
- En la vista **Mes**, pulsa el número de un día para abrir su semana y **+n más** para ver las actividades que no
  caben.
- En la vista **Semana**, la línea de la **hora actual** y la columna de hoy te ayudan a ubicarte.
- Los días de los meses vecinos que completan la cuadrícula se muestran atenuados.
- Quien tenga el permiso `agenda.create` puede pulsar un día vacío (en **Mes**) o una hora vacía (en **Semana**) para
  abrir **Nueva actividad** con esa fecha y hora ya escritas (ver [Planificar actividades y series](planificar-actividad)).
  Solo funciona en días y horas que todavía no han pasado: una actividad no puede empezar en el pasado.
- Los calendarios son solo de lectura: no se cambia una fecha arrastrando la actividad. Para reprogramarla, edítala.
- La dirección de la página guarda la vista, el periodo y los filtros: puedes guardarla en favoritos o compartirla y
  quien la abra verá lo mismo.

## Exportar a un archivo .ics

Con **Exportar .ics** descargas las actividades **Programadas** del periodo que estás viendo (el año, el mes o la
semana), con los filtros activos, en un archivo de calendario (`.ics`) que puedes abrir o importar en Outlook u otro
calendario. Sirve para quien no esté invitado o cuando Outlook no está configurado.

- Solo se exportan las **Programadas**: el filtro de **Estado** no se aplica.
- El archivo incluye el tipo y las etiquetas de cada actividad, y no incluye a las personas (ni el responsable ni los
  correos).
- El nombre del archivo dice el periodo: por ejemplo, `agenda-2026.ics`, `agenda-2026-10.ics` o
  `agenda-semana-2026-10-19.ics`.
- Si el periodo tiene más de 1000 actividades, ITMas lo avisa: acota con filtros.

## Estados

Cada estado se muestra siempre con un ícono y un texto; el significado no depende del color.

| Estado | Significado |
|---|---|
| **Borrador** | Solo existe en ITMas: todavía no se avisó a nadie. |
| **Programada** | Confirmada. |
| **Completada** | Ejecutada (o cerrada como no realizada), con su registro de ejecución. |
| **Cancelada** | Descartada antes de ejecutarse, con su motivo. |
| **Vencida** | Marca que aparece junto al estado cuando una actividad en Borrador o Programada ya terminó sin haberse cerrado. |

## El detalle de una actividad

El detalle reúne, en tarjetas:

- **Datos:** tipo, cuándo, responsable, participantes, si se avisa al equipo, impacto, alcance, lugar, recordatorio, la
  regla de la serie a la que pertenece (por ejemplo, "Cada mes, el segundo martes") y la descripción. Si la actividad se
  cancelló, también el motivo.
- **Etiquetas:** ver [Etiquetar actividades y filtrar la agenda](etiquetar-y-filtrar).
- **Outlook:** el estado de la publicación en el calendario compartido.
- **Ejecución:** el resultado, la fecha real, las notas y los enlaces de evidencia (solo si ya se registró; ver
  [Registrar la ejecución de una actividad](registrar-ejecucion)).
- **Registro:** quién creó la actividad y quién la modificó por última vez, y cuándo.

Todo lo que escribe una persona (títulos, descripciones, notas, etiquetas) se muestra como texto, nunca como
formato.

## Resultado

Tienes a la vista qué se hará, quién lo hará, cuándo y en qué estado está cada actividad del año, del mes o de la
semana, y si algo quedó vencido sin cerrarse.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "Todavía no hay actividades planificadas." | La agenda no tiene actividades (en el año o, en los calendarios, en el periodo elegido). Quien tenga `agenda.create` puede usar **Nueva actividad** o **Nueva serie** (ver [Planificar actividades y series](planificar-actividad)). |
| "No hay actividades en este periodo." | El periodo que ves (un año pasado o el año, mes o semana de un calendario) no tiene actividades. El calendario se muestra igualmente. |
| "Ninguna actividad coincide con los filtros." | Pulsa **Limpiar filtros** (ver [Etiquetar actividades y filtrar la agenda](etiquetar-y-filtrar)). |
| "Hay más actividades de las que se pueden mostrar. Acota con filtros." | La lista y los calendarios muestran como máximo 1000 actividades: filtra por tipo, estado, responsable o etiqueta. |
| "Cargando el calendario…" no desaparece | Recarga la página. Si continúa, avisa a quien administra ITMas. |
| En un día del **Mes** solo veo algunas actividades | Pulsa **+n más** o abre la **Semana** de ese día. |
| El calendario se ve vacío pero la lista no | Revisa el periodo que muestra el título: el calendario solo dibuja el año, el mes o la semana elegidos. |
| "No se pudo cargar la agenda." | Pulsa **Reintentar**. Si continúa, avisa a quien administra ITMas. |
| No veo la sección Agenda | Necesitas el permiso `agenda.read`. Ver [Qué puedo hacer: permisos y cómo solicitarlos](permisos-y-acceso). |
