# Consultar el catálogo de permisos

El catálogo lista todos los permisos que existen en ITMas, agrupados por categoría. Los permisos los define cada
funcionalidad; desde aquí solo se edita cómo se presentan.

**Quién puede hacerlo:** `permissions.read` para consultarlo y `permissions.update` para editar su presentación.

## Pasos

1. Abre **Administración → Permisos**.
2. Usa **Buscar** o filtra por **Categoría**. La columna **Políticas** indica en cuántas políticas está cada permiso.
3. Para cambiar el nombre visible, la descripción o la categoría de un permiso, pulsa **Editar** en su fila y guarda.

## Los permisos de la agenda

La categoría **Agenda** reúne los permisos de la agenda de infraestructura. Todas las acciones exigen además `agenda.read`.

| Permiso | Qué permite | Proceso |
|---|---|---|
| `agenda.read` | Consultar la agenda, el detalle, los tipos y las etiquetas | [Consultar la agenda del año](consultar-agenda) |
| `agenda.create` | Crear actividades y series (siempre en **Borrador**) | [Planificar actividades y series](planificar-actividad) |
| `agenda.update` | Modificar borradores y series, editar etiquetas y eliminar borradores | [Planificar actividades y series](planificar-actividad), [Etiquetar actividades y filtrar la agenda](etiquetar-y-filtrar) |
| `agenda.publish` | Programar y cancelar actividades; junto con `agenda.update`, modificar las ya programadas | [Programar y cancelar actividades](programar-y-cancelar) |
| `agenda.complete` | Registrar la ejecución, las notas y la evidencia | [Registrar la ejecución de una actividad](registrar-ejecucion) |
| `agenda.types.manage` | Crear, modificar y desactivar tipos de actividad | [Gestionar los tipos de actividad](gestionar-tipos-de-actividad) |

`agenda.update` y `agenda.publish` están separados a propósito: programar, cancelar y modificar lo ya programado notifican a
otras personas, así que quien prepara el plan no tiene por qué poder enviar esos avisos.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| Un permiso aparece como obsoleto | La funcionalidad que lo usaba ya no existe: no concede acceso y no se puede añadir a políticas. |
| No puedo cambiar el nombre técnico (`recurso.acción`) | Es fijo; solo se edita su presentación. |
