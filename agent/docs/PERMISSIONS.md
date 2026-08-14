# Permisos

## Veredicto general

**Ningún dato recolectado en este alcance requiere permiso TCC** (Full Disk Access, Screen Recording, etc.) — se evitó deliberadamente todo recurso protegido (Mail, Fotos, Mensajes, Documentos/Escritorio/Descargas de otros usuarios) desde el diseño. El agente corre como **root** (LaunchDaemon), lo cual de por sí exime de la mayoría de chequeos POSIX de permisos, pero root **no** exime automáticamente de TCC en macOS moderno para categorías específicamente protegidas — de ahí la única excepción marcada abajo.

## Fuente por dato

| Categoría | Dato | Fuente | Privilegio |
|---|---|---|---|
| Hardware | Modelo, CPU, GPU, almacenamiento | `system_profiler SPHardwareDataType/SPDisplaysDataType/SPStorageDataType -json` | ninguno |
| Hardware | Serial del equipo | `system_profiler SPHardwareDataType -json` → `serial_number` | ninguno |
| OS | Nombre/versión/build/kernel/hostname | `platform` (stdlib) + `sysctl -n kern.osversion` | ninguno |
| Apps | Aplicaciones instaladas | Enumeración de `/Applications`, `/System/Applications`(`/Utilities`), `~/Applications` por usuario; `Info.plist` vía `plistlib` | **ver excepción abajo** |
| Storage | Volúmenes (`df`-like) | `psutil.disk_partitions()`+`disk_usage()` | ninguno |
| Users | Usuarios/grupos locales | `dscl`, `id -Gn` | ninguno |
| Users | Último login | `psutil.users()` (sesiones activas al momento de la corrida) | ninguno |
| Resources | CPU/memoria actual | `psutil.cpu_percent()`, `psutil.virtual_memory()` | ninguno |
| Red | IP local | truco de socket UDP (sin enviar paquetes) | ninguno |
| Red | IP pública | llamada HTTPS a un servicio externo configurable (best-effort) | ninguno — **ver aviso de privacidad** |

## Excepción a verificar empíricamente

Enumerar `~/Applications` de **otros** usuarios (no el que ejecuta el proceso) requiere que el LaunchDaemon, corriendo como root, lea dentro del home directory de otra cuenta. `~/Applications` **no** está en la lista de carpetas que Apple protege con Full Disk Access (a diferencia de Desktop/Documents/Downloads/Mail/Photos/Time Machine) — se espera que no dispare ningún prompt ni bloqueo, pero esto debe confirmarse en hardware real durante el despliegue piloto (no se pudo verificar en esta sesión de desarrollo sin privilegios de root reales). Si resultara necesario, el remedio es un perfil MDM de PPPC (Privacy Preferences Policy Control) otorgando Full Disk Access al binario del agente de forma silenciosa — no un prompt visible al usuario.

## Aviso de privacidad: lookup de IP pública

El agente hace, por defecto, una llamada HTTPS de solo lectura a un servicio de terceros (`api.ipify.org` por defecto, configurable vía `publicIpLookupUrl` en `config.json`) para determinar la IP pública del equipo. Esto revela, únicamente, la dirección IP del equipo a ese servicio externo — nada más se envía ni se recibe. Es un dato "best-effort": si la llamada falla o no hay conexión a internet, el campo queda `null`, nunca se inventa.

## Qué NO se recolecta (por diseño, no por omisión)

Contraseñas, contenido de archivos personales, historial de navegación, mensajes, fotos, contenido de disco, números de serie de discos individuales más allá del resumen de hardware, o cualquier dato fuera de las categorías arriba listadas.
