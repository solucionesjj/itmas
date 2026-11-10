# Forzar un nuevo envío del inventario

El agente envía el inventario del equipo una vez al día y, justo después, la auditoría de los motores SQL que tenga
configurados. Si necesitas que vuelva a enviarlo sin esperar al día siguiente, puedes forzar un envío. Por ejemplo,
porque acabas de añadir una conexión SQL y quieres probarla, o porque cambiaste algo en el equipo y quieres verlo ya en
ITMas.

**Quién puede hacerlo:** una persona con permisos de administrador en el equipo donde está instalado el agente.

## Cómo decide el agente cuándo enviar

- El agente guarda la fecha del último envío correcto en el archivo `state.json`. Al arrancar, si ese archivo no existe
  o han pasado 24 horas o más, envía el inventario en ese momento; si no, espera a su hora.
- La auditoría SQL se envía siempre a continuación del inventario. Incluye cada conexión que no se ha enviado nunca o
  cuyo último envío correcto tiene más de 20 horas.
- Por eso, para forzar un envío basta con borrar `state.json` y reiniciar el servicio. Ese archivo solo guarda fechas
  y el último error: borrarlo **no** borra la credencial del equipo, la dirección del servidor ni las conexiones SQL.

## Antes de empezar

1. Si quieres probar una conexión SQL nueva, créala antes con `itmas-agent sql add` (ver
   [Configurar las conexiones SQL del agente](configurar-conexiones-sql)) y comprueba con `itmas-agent sql list` que
   aparece.
2. Abre una consola como administrador: PowerShell como administrador en Windows, o una terminal usando `sudo` en Linux
   y macOS.

## Pasos

1. Detén el servicio, borra el estado y vuelve a arrancarlo:
   - **Windows (PowerShell como administrador):**

     ```
     Stop-Service ITMasAgent
     Remove-Item "$env:ProgramData\ITMas\Agent\state.json"
     Start-Service ITMasAgent
     ```

   - **Linux:**

     ```
     sudo systemctl stop itmas-agent
     sudo rm /var/lib/itmas-agent/state.json
     sudo systemctl start itmas-agent
     ```

   - **macOS:**

     ```
     sudo rm "/Library/Application Support/ITMas/Agent/state.json"
     sudo launchctl kickstart -k system/cloud.sacservices.itmas.agent
     ```

2. Espera uno o dos minutos y comprueba el resultado en el equipo:
   - `itmas-agent status`: **Último envío** muestra la hora actual.
   - `itmas-agent sql list` (como administrador): cada conexión muestra la hora actual con el resultado **correcta** o
     **parcial**. Si dice **fallida** o **motor no disponible**, aparece el motivo.

## Resultado

- En **Inventario → Equipos**, el equipo tiene un registro nuevo con la fecha de hoy (ver
  [Consultar el historial y el informe de un equipo](historial-e-informe)). El registro anterior del mismo día se
  conserva.
- Si el equipo tiene conexiones SQL, sus instancias se actualizan en **Auditoría SQL → Instancias** (ver
  [Consultar los accesos de los motores SQL](consultar-accesos-sql)).
- A partir de ese envío, el agente vuelve a su ritmo normal: el siguiente será unas 24 horas después.

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| Quiero ver qué datos lee el agente sin enviar nada | Ejecuta `itmas-agent collect` para el inventario o `itmas-agent sql test <alias>` para una conexión SQL. |
| Se envió el inventario, pero no la conexión SQL | La conexión se envió correctamente hace menos de 20 horas, o no estaba creada al reiniciar. Revisa `itmas-agent sql list` y, si hace falta, repite los pasos más tarde. |
| La conexión SQL aparece como **fallida** con un reintento | No hace falta repetir los pasos: el agente reintenta solo a los 1, 2, 4… hasta 60 minutos. Corrige la causa que indica (ver [Configurar las conexiones SQL del agente](configurar-conexiones-sql)). |
| **Último envío** no cambia y `status` muestra un error | Revisa el error: suele ser la conexión con el servidor de ITMas. El agente reintenta solo. |
| "La API rechazó la credencial del dispositivo" | El código del equipo ya no es válido. Pide a TI un código nuevo y reinstala el agente (ver [Instalar el agente y enrolar el equipo](instalar-agente)). |
