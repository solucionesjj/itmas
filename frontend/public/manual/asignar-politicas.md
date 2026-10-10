# Asignar y retirar políticas a un usuario

Así se conceden privilegios en ITMas: asignando a la persona las políticas que contienen los permisos que necesita.

**Quién puede hacerlo:** `users.policies.assign` para asignar y `users.policies.revoke` para retirar.

## Asignar una política

1. Abre **Administración → Usuarios** y pulsa el usuario.
2. En la pestaña **Políticas**, pulsa **Asignar política**.
3. Elige la **Política** y confirma.

## Retirar una política

1. En la pestaña **Políticas** del usuario, pulsa el botón **Retirar** junto a la política y confirma.
2. La persona pierde los permisos que solo concedía esa política; conserva los que le dan otras.

## Resultado

Los cambios se aplican en la siguiente acción de la persona, sin que tenga que cerrar sesión, y quedan en la auditoría.
Para ver en su menú una sección nueva, la persona debe recargar la página.
Revisa el resultado en la pestaña **Permisos efectivos** (ver [Consultar los permisos efectivos de un usuario](permisos-efectivos)).

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| No puedo cambiar mis propias políticas | Es una salvaguarda contra la autoconcesión de privilegios: otra persona debe hacerlo. |
| No puedo asignar una política | Contiene permisos que tú no tienes, o el usuario es Root. |
| "No hay políticas activas disponibles para asignar" | Crea o activa antes la política (ver [Crear, editar, activar y eliminar políticas](gestionar-politicas)). |
