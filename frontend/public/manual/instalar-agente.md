# Instalar el agente y enrolar el equipo

El agente de inventario se instala en cada equipo y envía una vez al día sus datos técnicos a ITMas (sistema
operativo, hardware, red, discos, software instalado, cuentas locales, cifrado y firewall). Solo lee y envía: no
permite controlar el equipo.

**Quién puede hacerlo:** una persona con permisos de administrador en el equipo y el **código del equipo** que le
entregó el área de TI (ver [Registrar un equipo y obtener su código](registrar-equipo)).

## Antes de empezar

1. Ten a mano el código del equipo (`XXXXX-XXXXX-XXXXX-XXXXX`).
2. Descarga el paquete de tu sistema desde `/downloads/agent/latest/` de esta misma dirección, junto con el archivo
   `SHA256SUMS`.
3. Comprueba la suma del paquete con `SHA256SUMS` (el área de TI te indicará cómo si lo necesitas). Los paquetes no
   están firmados, así que esta comprobación garantiza que el archivo es el publicado por ITMas.

## Pasos

1. Descomprime el paquete.
2. Ejecuta el instalador como administrador:
   - **Windows:** abre PowerShell como administrador en la carpeta y ejecuta `.\itmas-agent.exe install`. Si aparece
     "Windows protegió su PC", pulsa *Más información → Ejecutar de todas formas* (solo tras comprobar la suma).
   - **Linux:** `sudo ./itmas-agent install`
   - **macOS:** `sudo ./itmas-agent install`. Si macOS lo bloquea, permítelo en *Ajustes del Sistema → Privacidad y
     seguridad → Abrir igualmente* (solo tras comprobar la suma).
3. Lee los términos y condiciones y escribe `ACEPTO` para continuar.
4. Pulsa Enter para aceptar la dirección del servidor que propone el instalador (o escribe la que te indique TI).
5. Cuando pida el **código de autorización**, escribe el código del equipo. No se ve al escribirlo; puedes usar
   guiones o espacios.
6. El instalador enrola el equipo, instala el servicio y envía el primer inventario.

## Resultado

En ITMas, el equipo pasa a **Activo** y aparece su primer registro. Para ver el estado en el equipo, ejecuta
`itmas-agent status` (como administrador).

## Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "El código de autorización no es válido." | El código ya se usó, se regeneró o el equipo se eliminó. Pide a TI un código nuevo. |
| "El servidor respondió 409." | Esta máquina ya está registrada en ITMas como otro equipo. TI debe eliminar ese registro antes. |
| Error de certificado o de conexión | Revisa la dirección del servidor y la conexión a Internet; el instalador no continúa sin una conexión segura. |
| Quiero desinstalarlo | Ejecuta `itmas-agent uninstall` como administrador. Los datos ya enviados se conservan en ITMas. |
