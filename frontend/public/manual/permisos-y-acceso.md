# Qué puedo hacer: permisos y cómo solicitarlos

En ITMas, estar dentro del sistema no da acceso a nada por sí mismo. Cada acción necesita un **permiso**, y los
permisos se conceden mediante **políticas**.

**Quién puede hacerlo:** cualquier usuario (para consultar este proceso).

## Cómo funciona

- Un **permiso** autoriza una acción concreta, por ejemplo `devices.create` (registrar equipos) o `users.read`
  (consultar usuarios). Cada proceso de este manual indica el permiso que necesita.
- Una **política** agrupa varios permisos con un nombre, por ejemplo "Gestión del inventario".
- A cada usuario se le asignan una o varias políticas. Sus **permisos efectivos** son la suma de los permisos de sus
  políticas activas.
- Un usuario **Root** puede hacerlo todo sin necesidad de políticas (ver [Administradores Root](administradores-root)).
- No hay roles fijos: el acceso de cada persona se ajusta combinando políticas.

## Pasos para pedir acceso

1. Busca en este manual el proceso que necesitas y anota el permiso que indica.
2. Pide al área de TI (o a la persona que administra ITMas en tu equipo) que te asigne una política con ese permiso.
3. En cuanto te la asignen, el permiso funciona desde tu siguiente acción, sin cerrar sesión. Para que la nueva
   sección aparezca en el menú, recarga la página.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| Solo veo "Inicio" y "Manual de uso" | Tu usuario no tiene políticas. Pide acceso como se indica arriba. |
| Veo una sección pero un botón no aparece | Ese botón necesita otro permiso (por ejemplo, ver equipos y eliminarlos son permisos distintos). |
| "No tienes permisos para realizar esta acción" | Te falta el permiso de esa acción, o te lo retiraron. Pide acceso de nuevo si lo necesitas. |
