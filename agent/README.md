# Agente de inventario ITMas

Servicio que se instala en cada equipo (Windows, Linux, macOS), recopila una vez al día su inventario técnico y lo
envía a ITMas por HTTPS ([SPEC-0002](../specs/features/SPEC-0002-os-inventory-agent.md),
[ADR-0010](../specs/architecture/adr-0010-inventory-agent.md)). Opcionalmente audita, en solo lectura, las cuentas y los
permisos de los motores SQL que configure el administrador del equipo ([SPEC-0005](../specs/features/SPEC-0005-sql-engine-access-audit.md),
[ADR-0014](../specs/architecture/adr-0014-database-engine-audit-agent.md), [ADR-0015](../specs/architecture/adr-0015-database-connections-without-verified-tls.md)).
Solo **lee y envía**: no abre puertos, no acepta órdenes del servidor y no se actualiza solo.

## Plataformas soportadas

| Sistema | Arquitectura | Paquete |
|---|---|---|
| Windows 11, Windows Server 2019 / 2022 / 2025 | x64 | `itmas-agent-win-x64.zip` |
| Ubuntu 22.04 / 24.04, Debian 12 | x64 | `itmas-agent-linux-x64.tar.gz` |
| Ubuntu 22.04 / 24.04, Debian 12 | arm64 | `itmas-agent-linux-arm64.tar.gz` |
| macOS (Intel) | x64 | `itmas-agent-osx-x64.tar.gz` |
| macOS (Apple Silicon) | arm64 | `itmas-agent-osx-arm64.tar.gz` |

Descarga: `https://itmas.sacservices.cloud/downloads/agent/latest/` (o `/downloads/agent/<versión>/`), junto con `SHA256SUMS`.

Desde la versión 1.2.0, en Linux el agente necesita la biblioteca ICU de la distribución (`libicu`), que ya traen las
instalaciones estándar de Ubuntu y Debian. Si falta, el agente no arranca y lo indica; se instala con
`sudo apt install libicu74` (Ubuntu 24.04), `libicu70` (Ubuntu 22.04) o `libicu72` (Debian 12). La necesita el
controlador de SQL Server.

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

1. Muestra los **términos y condiciones** (versión 2, que incluye la auditoría de motores SQL). Hay que escribir
   `ACEPTO` para continuar; si no se aceptan, no se realiza ningún cambio. Las instalaciones existentes no vuelven a
   aceptar términos.
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
| `itmas-agent sql …` | Conexiones de la auditoría SQL: `add`, `update`, `list`, `test`, `remove` (ver abajo) | Administrador / `sudo` |
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

## Auditoría SQL

Módulo desactivado por defecto: un agente sin conexiones no se conecta a ningún motor. Con conexiones configuradas,
cada día, junto con el inventario, envía **una instantánea por conexión** con las cuentas, roles, permisos (servidor,
base de datos, esquema, objeto y columna) y propietarios, a `POST /api/v1/sql-audit-snapshots`. Nunca lee contraseñas,
hashes, datos de tablas, texto de procedimientos ni sesiones; solo consultas fijas de catálogo, una conexión por base de
datos (nunca `USE`), 30 s por consulta y 5 minutos por conexión.

Motores: SQL Server 2019, 2022 y 2025; PostgreSQL 18; MySQL 9 LTS; MariaDB 12 LTS. Los controladores van dentro del
ejecutable: no hay que instalar clientes.

### Comandos

Todos requieren privilegios de administrador (`sudo` en Linux y macOS). Hasta 50 conexiones por agente; el alias es único.

| Comando | Qué hace |
|---|---|
| `itmas-agent sql add` | Pide alias, motor, host, puerto o instancia con nombre (SQL Server), base de datos inicial (PostgreSQL), usuario, contraseña (oculta) y modo TLS. **Prueba la conexión, el certificado y la lectura del catálogo antes de guardar**; si falla, explica la causa (DNS, conexión, motor sin TLS, certificado, credenciales, permisos) y no guarda nada. |
| `itmas-agent sql update <alias>` | Cambia la conexión (por ejemplo, rota la contraseña; Enter mantiene cada valor). Vuelve a probarla antes de guardar. |
| `itmas-agent sql list` | Lista las conexiones con motor, destino, usuario, TLS y resultado de la última recolección. Nunca muestra la contraseña. |
| `itmas-agent sql test <alias>` | Prueba la conexión y avisa si la cuenta tiene más privilegios de los necesarios. |
| `itmas-agent sql remove <alias>` | Borra la conexión y su contraseña. |

Sin consola interactiva (automatización) se usan opciones; la contraseña **nunca** se acepta como argumento, solo de un
archivo protegido o de una variable de entorno:

```bash
sudo ./itmas-agent sql add --alias ventas-prod --engine sqlserver --host sql01.example.internal --port 1433 \
  --user itmas_agent --password-file /ruta/protegida/clave.txt --tls pinned --certificate-file /ruta/servidor.cer
```

Opciones: `--alias`, `--engine sqlserver|postgresql|mysql|mariadb`, `--host`, `--port`, `--instance` (SQL Server),
`--database` (PostgreSQL, por defecto `postgres`), `--user`, `--password-file <ruta>` o `--password-env <VARIABLE>`,
`--tls verify|pinned|unverified`, `--certificate-file <ruta>` o `--certificate-sha256 <huella>`. No existen cadenas de
conexión libres: el agente compone la conexión con el *builder* de cada controlador.

### Modos TLS

Toda conexión va cifrada, también al propio equipo. Un motor que no acepte TLS no se puede auditar.

| Modo | Comportamiento |
|---|---|
| `verify` (por defecto) | Valida el certificado con el almacén de confianza del sistema y el nombre del host. |
| `pinned` | Acepta solo el certificado fijado (archivo PEM/DER o huella SHA-256). Recomendado para certificados autofirmados. En SQL Server con huella, el agente obtiene el certificado del servidor sin iniciar sesión y comprueba que coincide. |
| `unverified` | Cifrado, pero sin validar el certificado: no protege frente a la suplantación del motor. Nunca se aplica solo, ni aunque `verify` falle: en consola hay que escribir `ACEPTO EL RIESGO`; sin consola, indicar `--tls unverified`. ITMas señala estas instancias. |

Implementación: SQL Server con `Encrypt=Mandatory` (`TrustServerCertificate` solo en `unverified`, `ServerCertificate` en
`pinned`); PostgreSQL y MySQL/MariaDB con `SslMode=Require(d)` y una validación propia del agente durante el *handshake*
(antes de enviar credenciales). En MySQL nunca se pide la clave RSA del servidor (`AllowPublicKeyRetrieval=false`).

### Cuenta del agente en cada motor (mínimo privilegio)

| Motor | Permisos |
|---|---|
| SQL Server | Login SQL dedicado con `CONNECT ANY DATABASE` y `VIEW ANY DEFINITION`. Sin roles de servidor. |
| PostgreSQL | Rol con `LOGIN`. Basta el `CONNECT` que `PUBLIC` tiene por defecto; las bases que lo revoquen requieren `GRANT CONNECT` o aparecen como no disponibles. |
| MySQL | `SHOW DATABASES` global; `SELECT` sobre `mysql.role_edges`, `mysql.global_grants`, `mysql.db`, `mysql.tables_priv`, `mysql.columns_priv`, `mysql.procs_priv` y, en `mysql.user`, solo sobre las columnas que no son de autenticación (`Host`, `User`, las `*_priv`, `account_locked`, `password_expired`, `password_last_changed`, `password_lifetime`). |
| MariaDB | `SHOW DATABASES` global; `SELECT` sobre `mysql.global_priv`, `mysql.roles_mapping`, `mysql.db`, `mysql.tables_priv`, `mysql.columns_priv` y `mysql.procs_priv`. `global_priv` no permite excluir la autenticación: el agente nunca la consulta (riesgo documentado en ADR-0014). |

### Credenciales y estado

En `<datos>/sql/`, junto a la API key y con la misma protección (DPAPI de máquina en Windows; `0600` en un directorio
`0700` de `root` en Linux y macOS): `connections.json` (sin secretos), `credentials/<id>` (una contraseña por conexión),
`certificates/<id>.cer` (certificado fijado de SQL Server) y `results.json` (último resultado de cada conexión).

### Envío y reintentos

Cada instantánea lleva un `submissionId` nuevo; los reintentos HTTP inmediatos de esa misma instantánea lo reutilizan.
Si el envío falla, la conexión se reintenta con espera exponencial (1, 2, 4… 60 min) y se vuelve a recolectar. Una
conexión o base de datos que no se puede leer se envía como no disponible con su motivo. Un fallo de la auditoría SQL
nunca afecta al inventario. Los logs no contienen contraseñas ni nombres de cuentas, bases de datos u objetos.

## Desarrollo

| Acción | Comando |
|---|---|
| Pruebas unitarias (sin Docker ni red; las que ejecuta el `Dockerfile`) | `cd agent && dotnet test tests/Itmas3.Agent.Tests` |
| Pruebas de integración de la auditoría SQL (requieren Docker; sin Docker se omiten) | `cd agent && dotnet test tests/Itmas3.Agent.IntegrationTests` |
| Todas | `cd agent && dotnet test` |
| Compilar los 5 paquetes en Docker (y probar) | `./scripts/build-agent.sh` |
| Inventario del agente Linux en un contenedor | `docker compose --profile agent run --rm agent-e2e` |
| Inventario de este equipo | `cd agent && dotnet run --project src/Itmas3.Agent -- collect` |

Estructura y reglas: [ADR-0010](../specs/architecture/adr-0010-inventory-agent.md), constitución §13 y el rol
[`device-agent`](../agents/device-agent.md). El contrato `v1` está en
[`tests/fixtures/contract/inventory-report.v1.json`](tests/fixtures/contract/inventory-report.v1.json) y el de la
auditoría SQL en [`tests/fixtures/contract/sql-audit.v1.json`](tests/fixtures/contract/sql-audit.v1.json); los usan tanto
las pruebas del agente como las del backend.

Pruebas de la auditoría SQL:

- **Unitarias** (`tests/Itmas3.Agent.Tests/SqlAudit/`): *mappers* por motor con filas de catálogo de ejemplo
  (`fixtures/sqlaudit/*.rows.json`, con nombres que contienen `'`, `"`, `]`, `;` y `<script>`), reglas del contrato,
  modos TLS y *builders*, comandos `sql` (contraseña nunca como argumento, nada guardado si la prueba falla,
  `unverified` solo con confirmación), almacén protegido, ciclo diario y términos v2.
- **Integración** (`tests/Itmas3.Agent.IntegrationTests/`, Testcontainers): PostgreSQL 18, MySQL 9.7 y MariaDB 12.3
  reales, con y sin TLS, con la cuenta de mínimo privilegio, objetos con nombres conflictivos y permisos en todos los
  niveles. SQL Server 2019/2022/2025 solo existen para amd64: en Apple Silicon requieren la emulación Rosetta de Docker
  Desktop y, si no está disponible, se omiten indicando el motivo. Azure SQL Edge (arm64, motor 15) ejecuta el mismo
  código de SQL Server como sustituto, pero no es una de las versiones soportadas.
