# Editar un equipo, regenerar su código o eliminarlo

Las acciones de cada equipo están en el botón **Más acciones** (⋮) de su fila en **Inventario → Equipos**.

**Quién puede hacerlo:** `devices.update` para editar y regenerar el código; `devices.delete` para eliminar.

## Editar

1. Pulsa **Más acciones → Editar**.
2. Cambia el nombre, el tipo o el correo del propietario (puedes dejar el correo vacío) y pulsa **Guardar**.

## Regenerar el código

Úsalo para reinstalar el agente o si el código anterior se perdió o pudo filtrarse.

1. Pulsa **Más acciones → Regenerar código** y confirma.
2. Copia el nuevo código y entrégalo de forma segura a quien instalará el agente.

El código anterior deja de servir. Si el agente ya está instalado, sigue funcionando hasta que se reinstale con el
código nuevo.

## Eliminar

1. Pulsa **Más acciones → Eliminar** y confirma.
2. El equipo pasa a **Eliminado**: su código y la credencial de su agente dejan de funcionar al momento, y el agente ya
   no puede enviar datos.
3. Su historial se conserva y se puede consultar con el filtro **Estado → Eliminados**.

Eliminar no se puede deshacer. Para volver a inventariar esa máquina, regístrala como un equipo nuevo.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "Otro usuario modificó este equipo" | Alguien lo cambió mientras editabas. Vuelve a abrir **Editar** y repite el cambio. |
| No veo **Más acciones** | Te faltan los permisos, o el equipo está eliminado. |
