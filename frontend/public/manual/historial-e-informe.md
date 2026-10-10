# Consultar el historial y el informe de un equipo

Cada día el agente envía un inventario del equipo. El **historial** lista esos registros y el **informe** muestra uno
de ellos completo, organizado por secciones.

**Quién puede hacerlo:** `devices.read` y `devices.reports.read`.

## Ver el historial

1. En **Inventario → Equipos**, pulsa **Ver registros** (icono de historial) en la fila del equipo.
2. Cada fila es un registro: fecha de recopilación y de recepción, IP pública declarada y ubicación, IP de conexión,
   versión del agente y cantidad de datos no disponibles. El último lleva la marca **Último**.
3. Pulsa un registro para abrir su informe.

## Ver el último informe

1. En **Inventario → Equipos**, pulsa **Ver último informe** en la fila del equipo (o en la página de registros).
2. El informe tiene estas secciones; puedes saltar a cada una con los botones de la parte superior:
   - **Características del equipo:** nombre, sistema operativo, versión, arquitectura, memoria RAM y procesadores.
   - **Seguridad:** cifrado del disco del sistema y de cada volumen, y estado del firewall.
   - **Red:** IP pública declarada con su ubicación, IP de conexión e interfaces de red.
   - **Almacenamiento:** discos (capacidad, tecnología, bus) y volúmenes (espacio libre y cifrado).
   - **Usuarios locales:** cuentas del equipo, si están habilitadas, si son administradoras y sus grupos.
   - **Software instalado:** lista de aplicaciones con un filtro.
   - **Datos no disponibles:** lo que el agente no pudo obtener y el motivo.

## Cómo interpretar algunos datos

- **No disponible (motivo):** el agente no pudo leer ese dato (por ejemplo, por falta de permisos). ITMas nunca
  rellena un dato con un valor supuesto.
- **La IP pública declarada y la IP de conexión no coinciden:** puede deberse a un proxy o una VPN, o a un dato
  manipulado. La IP de conexión la registra ITMas y el equipo no puede alterarla.
- Si abres un registro antiguo, el informe lo indica y ofrece **Ver el último informe**.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| **Ver último informe** está desactivado | El equipo todavía no ha enviado ningún inventario. |
| No veo los botones de registros | Te falta el permiso `devices.reports.read`. |
