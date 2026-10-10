# Consultar el listado de equipos

El listado muestra todos los equipos del inventario, una fila por equipo.

**Quién puede hacerlo:** `devices.read`.

## Pasos

1. Abre **Inventario → Equipos**.
2. Cada fila muestra:
   - **Nombre del equipo** (y debajo el nombre que informa el propio equipo, si es distinto).
   - **Tipo**: Estación de trabajo o Servidor.
   - **Usuario asociado**: el nombre del colaborador y su correo, solo el correo si no es usuario de ITMas, o
     "Sin usuario asociado".
   - **Último registro**: fecha del último inventario recibido, o "Sin registros".
   - **Registros**: cantidad de inventarios guardados (se conservan 90 días).
   - **Estado**: Pendiente, Activo o Eliminado.
3. Usa **Buscar por equipo, colaborador o correo**, los filtros **Estado** y **Tipo**, y pulsa los encabezados para
   ordenar por nombre, usuario asociado o último registro.

## Resultado

Desde cada fila puedes abrir sus registros o su último informe (ver
[Consultar el historial y el informe de un equipo](historial-e-informe)).

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| No aparece un equipo eliminado | Por defecto se muestran pendientes y activos. Cambia **Estado** a **Eliminados** o **Todos**. |
| Un equipo sigue **Pendiente** | Su agente aún no se ha instalado con el código. |
| Un equipo activo no tiene registros recientes | El agente envía una vez al día; si pasan más de dos días, revisa el equipo. |
