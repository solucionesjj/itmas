# Crear, editar, activar y eliminar políticas

Una política es un conjunto de permisos con un nombre (por ejemplo, "Consulta del inventario"). Se crea una vez y se
asigna a todas las personas que necesitan esos permisos.

**Quién puede hacerlo:** `policies.read` para consultarlas; `policies.create`, `policies.update` y `policies.delete`
para crearlas, modificarlas y eliminarlas.

## Crear una política

1. Abre **Administración → Políticas** y pulsa **Nueva política**.
2. Escribe un **Nombre** (único) y, si quieres, una **Descripción**.
3. Marca los permisos que debe conceder. Están agrupados por categoría (Autorización, Inventario…).
4. Pulsa **Crear política**.

## Editar, activar o desactivar

1. Abre la política desde la lista y pulsa **Editar**.
2. Cambia el nombre, la descripción o los permisos, o usa el interruptor **Activa**. Una política inactiva conserva
   sus usuarios pero no les concede nada.
3. Pulsa **Guardar cambios**. Los cambios se aplican a todos sus usuarios en su siguiente acción.

## Eliminar

1. Abre la política y pulsa **Eliminar**. Confirma.
2. Solo se pueden eliminar políticas sin usuarios: retírala antes de todos ellos o desactívala.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "Ya existe una política llamada…" | Elige otro nombre (no distingue mayúsculas). |
| No puedo añadir un permiso | Solo puedes conceder permisos que tú mismo tienes; algunos permisos solo los ejerce un Root. |
| "La política fue modificada por otra persona" | Otra persona la cambió mientras la editabas. Recarga y repite el cambio. |
