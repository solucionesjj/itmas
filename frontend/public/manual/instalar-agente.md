# Instalar el agente y enrolar el equipo

El agente de inventario se instala en cada equipo y envía una vez al día sus datos técnicos a ITMas (sistema
operativo, hardware, red, discos, software instalado, cuentas locales, cifrado y firewall). Solo lee y envía: no
permite controlar el equipo. Opcionalmente puede auditar los accesos de motores SQL, solo si se configura después de
instalarlo (ver [Configurar las conexiones SQL del agente](configurar-conexiones-sql)).

**Quién puede hacerlo:** una persona con permisos de administrador en el equipo y el **código del equipo** que le
entregó el área de TI (ver [Registrar un equipo y obtener su código](registrar-equipo)).

## Antes de empezar

1. Ten a mano el código del equipo (`XXXXX-XXXXX-XXXXX-XXXXX`).
2. Descarga el paquete de tu sistema y el archivo `SHA256SUMS`. Los enlaces llevan siempre a la última versión
   publicada en esta misma dirección:

   | Sistema | Paquete |
   |---|---|
   | Windows (64 bits) | [itmas-agent-win-x64.zip](/downloads/agent/latest/itmas-agent-win-x64.zip) |
   | Linux (x64) | [itmas-agent-linux-x64.tar.gz](/downloads/agent/latest/itmas-agent-linux-x64.tar.gz) |
   | Linux (ARM64) | [itmas-agent-linux-arm64.tar.gz](/downloads/agent/latest/itmas-agent-linux-arm64.tar.gz) |
   | macOS con Apple Silicon (M1 o posterior) | [itmas-agent-osx-arm64.tar.gz](/downloads/agent/latest/itmas-agent-osx-arm64.tar.gz) |
   | macOS con Intel | [itmas-agent-osx-x64.tar.gz](/downloads/agent/latest/itmas-agent-osx-x64.tar.gz) |
   | Sumas de comprobación (todos) | [SHA256SUMS](/downloads/agent/latest/SHA256SUMS) |

   La carpeta de descargas no muestra su contenido: abre directamente el enlace del archivo.
3. Comprueba la suma del paquete con `SHA256SUMS`, desde la carpeta donde descargaste ambos archivos. Los paquetes no
   están firmados, así que esta comprobación garantiza que el archivo es el publicado por ITMas.
   - **Windows (PowerShell):** `(Get-FileHash .\itmas-agent-win-x64.zip -Algorithm SHA256).Hash` y compara el
     resultado con la línea de `itmas-agent-win-x64.zip` en `SHA256SUMS` (no importan mayúsculas o minúsculas).
   - **Linux:** `sha256sum -c --ignore-missing SHA256SUMS` debe mostrar `OK` junto al paquete.
   - **macOS:** `shasum -a 256 -c --ignore-missing SHA256SUMS` debe mostrar `OK` junto al paquete.

   Si la suma no coincide, no instales el paquete: descárgalo de nuevo y, si sigue sin coincidir, avisa a TI.

## Pasos

1. Descomprime el paquete.
2. Ejecuta el instalador como administrador:
   - **Windows:** abre PowerShell como administrador en la carpeta y ejecuta `.\itmas-agent.exe install`. Si aparece
     "Windows protegió su PC", pulsa *Más información → Ejecutar de todas formas* (solo tras comprobar la suma).
   - **Linux:** `sudo ./itmas-agent install`
   - **macOS:** `sudo ./itmas-agent install`. Si macOS lo bloquea, permítelo en *Ajustes del Sistema → Privacidad y
     seguridad → Abrir igualmente* (solo tras comprobar la suma).
3. Lee los términos y condiciones (versión 2: incluyen la auditoría opcional de motores SQL) y escribe `ACEPTO` para
   continuar. Los equipos que ya tenían el agente instalado no tienen que volver a aceptarlos.
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
| Al abrir la carpeta de descargas aparece "no encontrado" | La carpeta no muestra su contenido. Usa los enlaces de cada paquete de "Antes de empezar". |
| "El código de autorización no es válido." | El código ya se usó, se regeneró o el equipo se eliminó. Pide a TI un código nuevo. |
| "El servidor respondió 409." | Esta máquina ya está registrada en ITMas como otro equipo. TI debe eliminar ese registro antes. |
| Error de certificado o de conexión | Revisa la dirección del servidor y la conexión a Internet; el instalador no continúa sin una conexión segura. |
| En Linux, el agente no arranca y menciona "ICU" | Instala la biblioteca ICU de tu distribución (paquete `libicu`; viene de serie en Ubuntu y Debian). |
| Quiero desinstalarlo | Ejecuta `itmas-agent uninstall` como administrador. Los datos ya enviados se conservan en ITMas. |
