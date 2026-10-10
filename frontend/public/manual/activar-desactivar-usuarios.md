# Activar y desactivar usuarios

Desactivar un usuario le impide iniciar sesión y cierra la sesión que tenga abierta en su siguiente acción. Conserva
sus políticas, por si se vuelve a activar.

**Quién puede hacerlo:** `users.update`.

## Pasos

1. Abre **Administración → Usuarios** y pulsa el usuario.
2. Pulsa **Desactivar** (o **Activar** si está inactivo) y confirma.

## Resultado

El estado del usuario cambia a **Inactivo** o **Activo** y la operación queda registrada en la auditoría.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| No puedo desactivarme a mí mismo | Es una salvaguarda: otra persona con el permiso debe hacerlo. |
| No puedo desactivar a un usuario Root | Solo otro Root puede hacerlo, y siempre debe quedar al menos un Root activo. |
