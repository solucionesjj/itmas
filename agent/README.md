# Agente de inventario ITMas

Servicio que se instala en cada equipo (Windows, Linux, macOS), recopila una vez al día su inventario técnico y lo
envía a ITMas por HTTPS ([SPEC-0002](../specs/features/SPEC-0002-os-inventory-agent.md),
[ADR-0010](../specs/architecture/adr-0010-inventory-agent.md)). Solo **lee y envía**: no abre puertos, no acepta
órdenes del servidor y no se actualiza solo.

## Plataformas soportadas

| Sistema | Arquitectura | Paquete |
|---|---|---|
| Windows 11, Windows Server 2019 / 2022 / 2025 | x64 | `itmas-agent-win-x64.zip` |
| Ubuntu 22.04 / 24.04, Debian 12 | x64 | `itmas-agent-linux-x64.tar.gz` |
| Ubuntu 22.04 / 24.04, Debian 12 | arm64 | `itmas-agent-linux-arm64.tar.gz` |
| macOS (Intel) | x64 | `itmas-agent-osx-x64.tar.gz` |
| macOS (Apple Silicon) | arm64 | `itmas-agent-osx-arm64.tar.gz` |

Descarga: `https://itmas.sacservices.cloud/downloads/agent/latest/` (o `/downloads/agent/<versión>/`), junto con `SHA256SUMS`.

## Antes de instalar

1. Pide al área de TI el **código del equipo**. TI registra antes el equipo en ITMas (**Inventario → Equipos**), que
   genera un código de un solo uso con el formato `XXXXX-XXXXX-XXXXX-XXXXX` (SPEC-0003). Se entrega por un canal
   interno y nunca va en un correo público, un ticket ni la línea de comandos.
2. Descarga el paquete de tu plataforma y `SHA256SUMS` desde la misma URL, y **verifica la suma**. Los binarios no
   están firmados (decisión de SPEC-0002), así que la suma es la única forma de comprobar que el archivo es el
   publicado por ITMas.

En Linux:

```bash
sha256sum -c SHA256SUMS --ignore-missing
```

En macOS:

```bash
shasum -a 256 -c SHA256SUMS --ignore-missing
```

En Windows (PowerShell):

```powershell
Get-FileHash .\itmas-agent-win-x64.zip -Algorithm SHA256
```

Compara el resultado con la línea de `SHA256SUMS`.

## Instalación

El instalador hace estos pasos, en orden:

1. Muestra los **términos y condiciones**. Hay que escribir `ACEPTO` para continuar; si no se aceptan, no se realiza
   ningún cambio.
2. Pide la **URL del servidor ITMas** al que se enviará el inventario. Propone `https://itmas.sacservices.cloud` (o la
   ya configurada, si es una reinstalación) y basta pulsar Enter para aceptarla. Solo admite direcciones `https://`.
3. **Comprueba la conexión** con ese servidor y su certificado HTTPS. Si falla, explica por qué (certificado no válido,
   nombre que no resuelve, conexión rechazada) y cancela sin cambios, antes de pedir el código.
4. Pide el **código de autorización**: es el código del equipo. No se muestra en pantalla; se aceptan guiones, espacios
   y minúsculas.
5. Enrola el equipo e instala el servicio. El código queda consumido y no se guarda en el equipo.

Mensajes del enrolamiento:

| Mensaje | Causa y solución |
|---|---|
| `El código de autorización no es válido.` | El código no existe, ya se usó, se regeneró o el equipo se eliminó. Pide a TI que regenere el código del equipo. |
| `El servidor respondió 409.` | Esta máquina ya está registrada en ITMas como **otro** equipo. TI debe eliminar ese registro (o usar su código) antes de enrolarla con este. |

### Windows

1. Descomprime el zip.
2. Abre **PowerShell como administrador** en esa carpeta y ejecuta:

   ```powershell
   .\itmas-agent.exe install
   ```

3. Si aparece "Windows protegió su PC" (SmartScreen, porque el binario no está firmado), pulsa *Más información →
   Ejecutar de todas formas*, solo después de haber verificado la suma SHA-256.

Se instala en `C:\Program Files\ITMas\Agent` como servicio **ITMasAgent**, con arranque automático y reinicio ante fallo.
Los logs van al Visor de eventos (*Aplicación*, origen `ITMas Agent`).

### Linux

```bash
tar -xzf itmas-agent-linux-x64.tar.gz
```

```bash
sudo ./itmas-agent install
```

Se instala en `/opt/itmas-agent` como unidad `itmas-agent.service` de systemd, con aislamiento: `ProtectSystem=strict`,
`ProtectHome=read-only` y `NoNewPrivileges`. Logs:

```bash
journalctl -u itmas-agent
```

### macOS

```bash
tar -xzf itmas-agent-osx-arm64.tar.gz
```

```bash
sudo ./itmas-agent install
```

Si macOS bloquea el archivo porque se descargó con el navegador (Gatekeeper), permítelo en *Ajustes del Sistema →
Privacidad y seguridad → Abrir igualmente*, después de verificar la suma. Las descargas con `curl` no llevan atributo
de cuarentena.

Se instala en `/Library/Application Support/ITMas/Agent` como `LaunchDaemon` `cloud.sacservices.itmas.agent`.
Logs: `/Library/Logs/ITMas/agent.log`, rotados por `newsyslog`.

### Instalación no interactiva (pruebas, automatización)

La URL puede darse como argumento o en la variable `ITMAS_SERVER_URL`, porque no es un secreto. El código **no se
acepta como argumento**: puede leerse de un archivo protegido o de la variable de entorno `ITMAS_ENROLLMENT_CODE`.

```bash
sudo ./itmas-agent install --accept-terms --server-url https://itmas.ejemplo.com --enrollment-code-file /ruta/protegida/codigo.txt
```

Sin consola interactiva (por ejemplo, desde un script) no se hace ninguna pregunta y se usa la URL indicada o la
propuesta por defecto.

## Comandos

| Comando | Qué hace | Privilegios |
|---|---|---|
| `itmas-agent install` | Términos → código → enrolamiento → servicio | Administrador / `sudo` |
| `itmas-agent uninstall` | Detiene y elimina el servicio, la API key y los archivos del agente | Administrador / `sudo` |
| `itmas-agent status` | Estado del servicio, último y próximo envío, último error (nunca muestra la API key) | — |
| `itmas-agent collect` | Muestra en pantalla el inventario de este equipo **sin enviarlo** | Recomendado administrador |
| `itmas-agent version` | Versión del agente | — |

## Funcionamiento

- **Cadencia:** envía un inventario al arrancar si han pasado 24 h o más desde el último envío correcto, y después
  cada 24 h ± 30 min. Si falla, reintenta a los 1, 2, 4… hasta 60 minutos.
- **Datos parciales:** si una sección no se puede leer (permisos, herramienta ausente, tiempo agotado), se envía el
  resto y esa sección figura en `unavailable` con el motivo.
- **IP pública:** se consulta una vez al día a `https://ipinfo.io/json`, de forma anónima y sin enviar datos del
  equipo. Si ipinfo.io está bloqueado, esa sección queda como no disponible.
- **Credencial:** la API key del equipo se guarda solo legible por administradores:
  - Windows: `%ProgramData%\ITMas\Agent`, cifrada con DPAPI.
  - Linux: `/var/lib/itmas-agent`, con permisos `0600`.
  - macOS: `/Library/Application Support/ITMas/Agent`, con permisos `0600`.
- **Credencial rechazada (401):** ocurre cuando el equipo se elimina en ITMas o cuando el agente se reinstaló con otro
  código. El agente deja de enviar, lo registra y vuelve a comprobarlo cada 24 h. Para volver a enrolar el equipo,
  pide a TI que regenere su código (o que lo registre de nuevo si se eliminó) y reinstala el agente: la API key
  anterior queda invalidada.

## Desarrollo

| Acción | Comando |
|---|---|
| Pruebas | `cd agent && dotnet test` |
| Compilar los 5 paquetes en Docker (y probar) | `./scripts/build-agent.sh` |
| Inventario del agente Linux en un contenedor | `docker compose --profile agent run --rm agent-e2e` |
| Inventario de este equipo | `cd agent && dotnet run --project src/Itmas3.Agent -- collect` |

Estructura y reglas: [ADR-0010](../specs/architecture/adr-0010-inventory-agent.md), constitución §13 y el rol
[`device-agent`](../agents/device-agent.md). El contrato `v1` está en
[`tests/fixtures/contract/inventory-report.v1.json`](tests/fixtures/contract/inventory-report.v1.json) y lo usan tanto
las pruebas del agente como las del backend.
