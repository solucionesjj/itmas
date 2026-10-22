# Etiquetar actividades y filtrar la agenda

Las etiquetas son palabras o frases cortas, de texto libre, que cruzan los tipos de actividad: por ejemplo "windows",
"producción", "iso-27001", "sql-server" o "2026-t4". Sirven para ubicar la información con rapidez, combinadas con los
demás filtros de la agenda.

**Quién puede hacerlo:** `agenda.read` para filtrar y `agenda.update` (*Modificar actividades*) para editar etiquetas.

## Etiquetar una actividad

1. Al crear una actividad o una serie, escribe en el campo **Etiquetas** y pulsa **Intro**. Al escribir, ITMas sugiere
   las etiquetas que ya existen, con la cantidad de actividades que usa cada una: elígelas en lugar de crear variantes.
2. En una actividad existente, abre su detalle y, en la tarjeta **Etiquetas**, pulsa **Añadir etiquetas** (o **Editar
   etiquetas** si ya tiene). Los cambios se guardan al instante; pulsa **Listo** cuando termines.
3. Para quitar una etiqueta, pulsa la **x** de su ficha.

Reglas:

- Hasta 10 etiquetas por actividad, de 1 a 30 caracteres cada una.
- "Windows" y "windows" son la misma etiqueta: se conserva la escritura de la primera vez que se usó.
- Las etiquetas se pueden editar en cualquier estado, también en una actividad **Completada** o **Cancelada**.
- Cambiar las etiquetas no avisa a nadie ni modifica la actividad.
- No necesitas un permiso aparte para crear una etiqueta nueva: nace al usarla.

## Filtrar la agenda

En **Agenda → Actividades**, los filtros se combinan entre sí:

- **Buscar por título o etiqueta:** escribe parte del título o de una etiqueta.
- **Tipo** y **Estado** (incluye **Vencidas**).
- **Responsable** y **Mis actividades** (solo las tuyas).
- **Filtrar por etiquetas:** elige una o varias. Se muestran las actividades que tienen **todas** las etiquetas elegidas.

Atajos:

- Pulsa una etiqueta en la lista (o en el detalle de una actividad) para añadirla al filtro.
- Pulsa **Limpiar filtros** para quitarlos todos.

Los filtros valen para las cuatro vistas (**Lista del año**, **Año**, **Mes** y **Semana**) y se conservan al cambiar de
vista o de periodo (ver [Consultar la agenda del año](consultar-agenda)). Los calendarios solo dibujan las actividades que
cumplen los filtros.

El **Resumen** de la lista del año respeta los filtros: con una etiqueta elegida, los meses indican en cuáles hay
actividades que la tienen. Si aplicas etiquetas o una búsqueda sin haber elegido una vista, la agenda muestra la lista del
año completo, porque una etiqueta rara vez se concentra en un mes.

Los filtros, la vista y el periodo (año, mes o semana) quedan en la dirección de la página: puedes guardarla en
favoritos o compartirla y quien la abra verá la misma búsqueda.

## Resultado

Encuentras en segundos, por ejemplo, las actividades de Windows en producción que están vencidas, o todo lo que
corresponde a una auditoría.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "Máximo 10 etiquetas por actividad." | Quita alguna antes de añadir otra. |
| "Una etiqueta admite como máximo 30 caracteres." | Acórtala. |
| No veo el botón **Añadir etiquetas** | Necesitas el permiso `agenda.update`. Ver [Qué puedo hacer: permisos y cómo solicitarlos](permisos-y-acceso). |
| "Ninguna actividad coincide con los filtros." | Quitaste o combinaste filtros que no tienen actividades en común: pulsa **Limpiar filtros**. Recuerda que con varias etiquetas se piden todas a la vez. |
