# Gestionar los tipos de actividad

Cada actividad de la agenda pertenece a un **tipo** (parches del sistema operativo, auditoría de seguridad, hardening,
prueba de vulnerabilidades, cambio de claves…). El tipo define la duración y el recordatorio por defecto y si es un
**periodo de congelamiento**, en el que no deben programarse cambios. ITMas trae un catálogo inicial con los tipos
habituales y se puede ajustar.

**Quién puede hacerlo:** `agenda.types.manage` (*Gestionar tipos de actividad*).

## Consultar el catálogo

En **Agenda → Actividades**, pulsa **Tipos de actividad**. La tabla muestra cada tipo con su **Duración**, su
**Recordatorio**, si es de **Congelamiento** y su **Estado** (**Activo** o **Inactivo**).

## Crear un tipo

1. Pulsa **Nuevo tipo**.
2. Escribe el **Nombre** (único, sin distinguir mayúsculas, hasta 80 caracteres) y, si quieres, la **Descripción**.
3. Indica la **Duración por defecto (minutos)** (de 1 a 86400) y el **Recordatorio por defecto (minutos antes)** (de 0 a
   40320, que son cuatro semanas).
4. Activa **Periodo de congelamiento (no deben programarse cambios)** si corresponde. Las actividades de otros tipos que se
   solapen con una de este tipo muestran una advertencia.
5. Pulsa **Crear tipo**.

## Modificar un tipo

Pulsa el lápiz de su fila, cambia lo que necesites y pulsa **Guardar**. Los cambios en la duración o el recordatorio por
defecto afectan solo a las actividades que se creen después.

## Desactivar y reactivar

Un tipo no se elimina: se desactiva. Pulsa el botón **Desactivar el tipo …** de su fila y confirma. Un tipo inactivo ya no
se puede asignar a actividades nuevas, pero las actividades que ya lo tienen lo conservan. Para volver a usarlo, pulsa
**Activar el tipo …**.

## Resultado

Quien planifica elige entre los tipos activos al crear una actividad o una serie (ver [Planificar actividades y
series](planificar-actividad)).

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| El nombre ya existe | Los nombres no se repiten, tampoco con otras mayúsculas o minúsculas: elige otro o reactiva el tipo existente. |
| "Un número entero de 1 a 86400." | La duración se expresa en minutos enteros; el máximo son 60 días. |
| "Otro usuario modificó este tipo. Recarga los datos e inténtalo de nuevo." | Alguien lo cambió antes que tú: actualiza la pantalla y repite el cambio. |
| Un tipo no aparece al crear una actividad | Está **Inactivo**: reactívalo. |
| No veo **Tipos de actividad** | Necesitas el permiso `agenda.types.manage`. Ver [Qué puedo hacer: permisos y cómo solicitarlos](permisos-y-acceso). |
