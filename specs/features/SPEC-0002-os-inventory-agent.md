---
id: SPEC-0002
title: Agente de inventario de sistema operativo
type: feature
status: Implemented
owner: jmartinez
created: 2026-10-07
approved-by: jmartinez (2026-10-08)
agents: [architecture, security, backend, database, device-agent, testing]
related: [SPEC-0000, SPEC-0001, ADR-0002, ADR-0003, ADR-0007, ADR-0008]
---

# SPEC-0002 — Agente de inventario de sistema operativo

> Derivada de la definición funcional aportada por el solicitante (2026-10-07) y de sus respuestas a dos rondas de
> preguntas (2026-10-07 y 2026-10-08, dos el día 8), recogidas en §18. Las propuestas de `architecture` que el solicitante aceptó se
> conservan con su referencia (P-n) para que se puedan rastrear. Aprobada el 2026-10-08.

## 1. Objetivo

Saber qué equipos (Windows, Linux y macOS) hay en la organización y con qué configuración de hardware, software,
usuarios y seguridad. Para ello, un agente instalado como servicio en cada equipo recopila el inventario una vez al día
y lo envía a la API de ITMas.

## 2. Contexto

Hoy ITMas tiene la fundación (SPEC-0000) y la autorización por políticas (SPEC-0001). Su único cliente es el navegador,
que se autentica con Entra ID mediante BFF. Esta spec introduce:

1. Un **componente que se instala en los equipos de los colaboradores**: se ejecuta fuera de Docker y con privilegios elevados.
2. Un **cliente no humano** de la API, que se autentica con una API key propia de cada dispositivo.
3. **Datos procedentes de equipos de terceros** que llegan desde Internet y se tratan como entrada no confiable.
4. Una **dependencia externa en tiempo de ejecución** en el agente: ipinfo.io, para obtener la IP pública y su ubicación.

Ninguna de estas cuatro novedades está prevista en la constitución actual. El solicitante acepta que la constitución
evolucione con el producto. Las enmiendas necesarias están en §10.6.

La entrega se divide en dos etapas (§18.1, P-4/P-9):
- **SPEC-0002 (esta):** el agente, el enrolamiento, la recepción y el almacenamiento del inventario.
- **SPEC-0003 (siguiente etapa):** la interfaz web para consultar el inventario, gestionar los códigos de enrolamiento
  y revocar o eliminar dispositivos, con sus permisos `devices.*`.

### Definición original (solicitante)

El inventario debe contener:

- Nombre del equipo
- Sistema operativo y versión
- Aplicaciones instaladas (no importa la versión, solo el nombre)
- Usuarios creados en el sistema con los grupos a los que pertenecen (para saber cuáles son administradores)
- IP pública con la que se conecta a Internet, con su ubicación (país y ciudad)
- IPs locales
- Capacidad de memoria RAM
- Capacidad de CPU (tipo/modelo, núcleos)
- Capacidad de los discos duros, tecnología y espacio libre
- Si el disco duro está cifrado
- Si el firewall está activo

El inventario se recopila una vez al día y se envía a la API de ITMas.

## 3. Alcance

**Incluye**
- Agente multiplataforma que se ejecuta como servicio: Windows Service, unidad `systemd` en Linux y `LaunchDaemon` en macOS.
- Plataformas (P-1):
  - Windows 11 x64.
  - Windows Server 2019, 2022 y 2025 x64.
  - Ubuntu 22.04 y 24.04 y Debian 12, en x64 y arm64.
  - macOS en Intel y Apple Silicon, en versiones recientes. Los Mac Intel tienen la última versión que soporta su hardware.
- Instalación manual por el usuario desde un paquete descargado de Internet. Sin firma de código y sin herramienta de
  distribución masiva (P-11).
- Aceptación de términos y condiciones durante la instalación (P-16).
- Enrolamiento con un código de autorización, a cambio del cual el servidor emite una API key única por dispositivo (P-3).
- Recopilación de los 11 datos de la definición, con el significado concreto por sistema operativo de §8.3.
- Ejecución diaria, reintentos y envío por HTTPS a `https://itmas.sacservices.cloud` (P-15).
- Endpoints de la API para el enrolamiento y la recepción del inventario, con contrato estricto, validación y almacenamiento.
- IP pública y ubicación obtenidas por el agente con ipinfo.io, y además la IP de conexión registrada por el servidor (P-5c).
- Publicación de los paquetes del agente en `https://itmas.sacservices.cloud/downloads/agent/` (P-17).

**Excluye**
- Interfaz web y permisos de usuario para consultar el inventario, gestionar códigos y revocar dispositivos (SPEC-0003).
- Versiones de las aplicaciones (excluido por la definición).
- Usuarios de dominio o Entra ID: solo se inventarían las cuentas locales (P-7).
- Ejecución remota de comandos o cualquier acción del servidor sobre el equipo: el agente solo **lee y envía**.
- Actualización automática del agente (P-12).
- Firma de código, instaladores nativos (MSI, .pkg, .deb/.rpm) y distribución por GPO, Intune o MDM.
- Alertas, reportes y exportaciones.
- Multi-organización: hoy hay una sola organización (ECS), aunque el modelo no debe impedirla (§10.4).
- El despliegue de producción (servidor expuesto a Internet, puertos 80/443, certificado de Let's Encrypt y su
  renovación). Queda para una spec de infraestructura aparte. Esta spec solo fija lo que necesita de ella (§10.4).

## 4. Actores

| Actor | Descripción |
|---|---|
| Colaborador / instalador | Persona con permisos de administrador en el equipo. Descarga el paquete, acepta los términos, introduce el código de autorización e instala el agente. |
| Agente | Servicio instalado en el equipo. Recopila y envía. Se autentica con su API key, no como usuario. |
| Operador de ITMas | Persona con acceso al despliegue. Genera y rota el código de autorización (configuración del servidor) y publica los paquetes del agente. |

## 5. Requerimientos

### 5.1 Funcionales

**Instalación y enrolamiento**
- **RF-1** El agente es un único ejecutable por plataforma con los comandos `install`, `uninstall` y `status`.
  `install` debe ejecutarse con privilegios de administrador o `root`.
- **RF-2** Durante `install`, el agente:
  1. Muestra los términos y condiciones (Anexo A) y exige que el usuario los acepte expresamente. Si no los acepta,
     cancela la instalación sin dejar cambios.
  2. Pide la URL del servidor ITMas proponiendo un valor que se acepta con Enter: la URL ya configurada si es una
     reinstalación o, si no, `https://itmas.sacservices.cloud`. Solo admite `https://`. Después comprueba la conexión
     HTTPS con el servidor (`/healthz`, certificado válido) **antes** de pedir el código; si falla, explica la causa
     (certificado, DNS, conexión) y cancela sin cambios (P-19).
  3. Pide el código de autorización con entrada oculta, sin eco en pantalla.
  4. Se enrola en la API, guarda la URL y la API key del dispositivo de forma protegida (§14.2) y descarta el código.
  5. Se copia en el directorio de instalación, registra el servicio, lo arranca y envía el primer inventario.
- **RF-3** Para pruebas automatizadas, `install` puede recibir la aceptación con `--accept-terms`, la URL con
  `--server-url` o con la variable `ITMAS_SERVER_URL` (no es un secreto), y el código desde un archivo
  (`--enrollment-code-file`) o desde la variable de entorno `ITMAS_ENROLLMENT_CODE`, nunca como argumento
  (constitución §4.5). Sin consola interactiva no se hacen preguntas: se usa la URL propuesta.
- **RF-4** El servidor registra con el dispositivo la versión de los términos aceptados, la fecha de aceptación y la IP
  de origen del enrolamiento.
- **RF-5** `uninstall` detiene y elimina el servicio y borra la API key y los archivos del agente. Los datos ya enviados
  se conservan en el servidor.
- **RF-6** `status` muestra si el servicio está activo, la fecha del último envío correcto y el último error. Nunca muestra la API key.

**Ejecución**
- **RF-7** El servicio arranca con el equipo y el sistema operativo lo reinicia si falla.
- **RF-8** Cadencia de envío (P-6):
  1. Al arrancar, si han pasado 24 h o más desde el último envío correcto.
  2. Después, cada 24 h desde el último envío correcto, con una variación aleatoria de ±30 min.
  3. Si un envío falla, reintenta con espera exponencial (1, 2, 4… hasta 60 min). No acumula inventarios: en cada
     reintento vuelve a recopilar.
- **RF-9** Si un dato no se puede obtener (falta de permiso, herramienta ausente, tiempo agotado), el inventario se envía
  igualmente con ese dato marcado como no disponible y con el motivo (RN-6).
- **RF-10** Si la API rechaza la API key (`401`), el agente deja de enviar y vuelve a comprobarla cada 24 h. Lo registra en el log local.
- **RF-11** El agente usa el proxy del sistema si está configurado y valida siempre el certificado TLS con el almacén de
  confianza del sistema. No existe ninguna opción para desactivar esa validación.
- **RF-12** El agente escribe un log local sin secretos ni contenido del inventario: Visor de eventos en Windows,
  `journald` en Linux y archivo con rotación en `/Library/Logs/ITMas/` en macOS.
- **RF-13** Cada envío incluye la versión del agente y la fecha y hora de recopilación (UTC).

**Servidor**
- **RF-14** La API enrola dispositivos que presentan un código de autorización válido (§8.1) y emite una API key por dispositivo.
- **RF-15** La API recibe inventarios de dispositivos enrolados, los valida contra un contrato estricto (§12.2) y los almacena.
- **RF-16** El inventario registra dos IPs distintas (P-5c):
  - **IP pública declarada:** la obtiene el agente en cada recopilación con `GET https://ipinfo.io/json`, que devuelve
    la IP con la que el equipo sale a Internet y su ubicación. Se envía en el inventario con país, región, ciudad y
    organización/ASN. Si ipinfo.io no responde, la sección se marca como no disponible (RF-9).
  - **IP de conexión:** la IP de origen de la petición HTTP tal como la ve el servidor. La registra la API y el agente
    no puede falsearla. Normalmente coincide con la declarada; si difieren, puede indicar un proxy, una VPN o un dato
    manipulado. La interfaz de SPEC-0003 mostrará ambas.
- **RF-17** Se conservan el último inventario de cada dispositivo y un histórico de 90 días (P-8).

**Distribución**
- **RF-18** nginx sirve los paquetes del agente sin autenticación, porque no contienen secretos:
  - Ruta: `/downloads/agent/<versión>/` y `/downloads/agent/latest/`.
  - Un archivo por plataforma: `itmas-agent-<rid>.zip` en Windows y `.tar.gz` en Linux y macOS.
  - Un `SHA256SUMS` por versión.
  - Sin listado de directorios.
  - Los paquetes se leen de un volumen de solo lectura y no forman parte de la imagen.

### 5.2 No funcionales

- **RNF-1** La recopilación no debe afectar al uso del equipo: prioridad baja, menos de 2 minutos en total y 30 s como
  máximo por comando externo. En reposo, el agente usa como máximo 50 MB de RAM y no consume CPU.
- **RNF-2** El agente no abre puertos de escucha. Solo establece conexiones HTTPS salientes a la URL de ITMas y a `ipinfo.io`.
- **RNF-3** El inventario ocupa menos de 1 MB, que es el límite actual del proxy.
- **RNF-4** Capacidad: unos 80 dispositivos en la v1. El diseño debe soportar al menos 10 veces esa cifra sin cambios.
- **RNF-5** Contrato estricto (constitución §3.6): la API rechaza cualquier campo desconocido. El agente y la API se
  versionan por separado y la API siempre se despliega antes que una versión del agente que use campos nuevos (§10.6, punto 7).
- **RNF-6** El agente debe ser fácil de mantener en todas las plataformas: un solo código base y un solo lenguaje,
  sin componentes nativos por plataforma, salvo las llamadas al sistema operativo.

## 6. Reglas de negocio

| ID | Regla |
|---|---|
| RN-1 | Un dispositivo solo puede enviar el inventario **de sí mismo**: su identidad la determina la API key, nunca el contenido del inventario. |
| RN-2 | Un código de autorización incorrecto no enrola el dispositivo (`401`, sin dar el motivo). |
| RN-3 | Sin la aceptación de los términos no hay enrolamiento: la API exige `termsVersion` y que coincida con una versión vigente. |
| RN-4 | Enrolar un dispositivo cuyo identificador de hardware ya existe (reinstalación) lo vincula al dispositivo existente, invalida su API key anterior y queda auditado (P-10). |
| RN-5 | Un inventario con fecha de recopilación anterior al último almacenado se guarda en el histórico, pero no sustituye al último. |
| RN-6 | Un dato no disponible se informa como tal y nunca se rellena con valores supuestos. |
| RN-7 | Ser "administrador" es una propiedad que calcula el agente según las reglas de cada sistema operativo (§8.3.4). |

## 7. Permisos

Esta spec no añade permisos de usuario: no expone endpoints para personas. Los permisos `devices.*` para consultar y
gestionar el inventario se definirán en SPEC-0003.

El agente no recibe permisos ni políticas. Puede hacer exactamente dos operaciones (enrolarse y enviar su propio
inventario), cada una protegida por un esquema de autenticación y una política propios (§10.2). Esto requiere la
enmienda del punto 1 de §10.6.

## 8. Comportamiento esperado

### 8.1 Instalación y enrolamiento

```text
Operador    ──▶ genera el código de autorización (aleatorio, ≥ 32 caracteres) en la configuración del servidor
                (INVENTORY_ENROLLMENT_CODE) y lo comparte con los colaboradores por un canal interno
Colaborador ──▶ descarga el paquete de su plataforma + SHA256SUMS (P-17) ──▶ verifica la suma
            ──▶ Windows: consola como administrador → itmas-agent.exe install
                Linux/macOS: sudo ./itmas-agent install
Agente      ──▶ muestra los términos → "¿Acepta los términos y condiciones? (escriba ACEPTO)"
            ──▶ "Código de autorización:" (oculto)
            ──▶ POST /api/v1/device-enrollments
                  Authorization: Enrollment <código>  ·  X-CSRF: 1
                  { hardwareId, hostname, osFamily, agentVersion, termsVersion, termsAcceptedAt }
API         ──▶ código válido (comparación en tiempo constante) → crea o vincula el dispositivo (RN-4)
            ──▶ genera la API key (256 bits aleatorios), guarda solo su hash [auditar device.enrolled]
            ◀── 201 { deviceId, apiKey }
Agente      ──▶ guarda la API key protegida (§14.2) → instala y arranca el servicio → primer inventario
```

Rotar el código de autorización consiste en cambiar la variable y reiniciar la API. No afecta a los dispositivos ya
enrolados, porque cada uno usa su propia API key. Los códigos por dispositivo o con caducidad, gestionados desde la UI,
llegarán con SPEC-0003.

### 8.2 Envío diario

```text
Agente ──▶ recopila (§8.3), incluida GET https://ipinfo.io/json (tiempo máximo 10 s; si falla, "no disponible")
       ──▶ POST /api/v1/inventory-reports   (Authorization: Device <apiKey>, X-CSRF: 1)
API    ──▶ autentica el dispositivo ──▶ valida el contrato y los límites (§12.2)
       ──▶ IP de conexión = IP de origen (X-Forwarded-For solo desde proxies de confianza)
       ──▶ guarda el inventario (con la IP declarada y la de conexión) y actualiza el último inventario y la última conexión
       ◀── 202 Accepted
Errores: 400 contrato inválido · 401 API key inválida · 413 demasiado grande · 429 límite de frecuencia
```

### 8.3 Datos recopilados por sistema operativo

Se usan APIs del sistema y comandos incluidos en el propio sistema operativo, invocados con ruta absoluta, sin *shell* y
con argumentos fijos. No se instalan herramientas de terceros. La única llamada externa es a ipinfo.io (§8.3.5).
ADR-0010 confirmará las fuentes concretas.

#### 8.3.1 Identificación y sistema operativo

| Dato | Windows 11 / Server | Linux | macOS |
|---|---|---|---|
| Nombre del equipo | Nombre del host | `hostname` | `LocalHostName` / `ComputerName` |
| SO y versión | Edición, versión (p. ej. 24H2) y compilación (registro `CurrentVersion`; Windows 11 se detecta con compilación ≥ 22000) | `/etc/os-release` (`NAME`, `VERSION_ID`) y versión del *kernel* | `sw_vers` (nombre, versión, compilación) |
| Arquitectura | x64 | x64 / arm64 | x64 / arm64 |
| Identificador de hardware (técnico, para RN-4) | UUID SMBIOS | `/etc/machine-id` | `IOPlatformUUID` |

#### 8.3.2 Hardware

| Dato | Contenido |
|---|---|
| RAM | Memoria física total en bytes. |
| CPU | Modelo, procesadores físicos (*sockets*), núcleos físicos y procesadores lógicos (hilos). |
| Discos físicos | Modelo, capacidad, **tecnología** (HDD, SSD) y **bus** (NVMe, SATA, SAS, USB, virtual). Si el sistema operativo no lo informa: "desconocido". En máquinas virtuales suele ser "virtual" o "desconocido". |
| Volúmenes | Punto de montaje o letra, sistema de archivos, capacidad, **espacio libre**, disco al que pertenece, si es el volumen de arranque y **estado de cifrado**. |

Se excluyen las unidades extraíbles, de red, ópticas y los sistemas de archivos virtuales (`tmpfs`, `overlay`, `loop`,
`squashfs`) (P-13).

#### 8.3.3 Aplicaciones instaladas (solo nombre, sin duplicados)

| SO | Fuente |
|---|---|
| Windows | Claves `Uninstall` de HKLM (64 y 32 bits) y de los perfiles de usuario cargados (HKU), sin componentes de sistema (`SystemComponent=1`) ni actualizaciones (`ParentKeyName`). No incluye aplicaciones de Microsoft Store (P-7, P-18). |
| Linux | Solo los paquetes **instalados manualmente**: `apt-mark showmanual` (Ubuntu y Debian). El software instalado fuera del gestor de paquetes (tarballs, `/opt`, binarios sueltos) no se detecta. |
| macOS | Bundles `.app` de `/Applications`, incluidas sus subcarpetas (P-7). |

#### 8.3.4 Usuarios locales y grupos

Solo cuentas locales (§18.1, P-7). Los usuarios de dominio y de Entra ID no se inventarían.

| SO | Usuarios | Grupos | Es administrador si… |
|---|---|---|---|
| Windows | Cuentas locales | Grupos locales de los que es miembro | Es miembro (directo o por grupo local anidado) de `Administradores` (SID `S-1-5-32-544`) |
| Linux | `/etc/passwd`: UID 0 y UID ≥ `UID_MIN` (las cuentas de servicio se excluyen) | `/etc/group`, incluido el grupo primario | UID 0, o miembro de `sudo`, `wheel` o `admin` (no se analiza `sudoers`) |
| macOS | `dscl`: UID ≥ 500 y sin prefijo `_` | Grupos de `dscl` | Miembro de `admin` |

Por cada cuenta se informa también si está **habilitada** (P-7). Así se evita un falso positivo, por ejemplo
la cuenta `Administrador` integrada de Windows, que suele estar deshabilitada.

#### 8.3.5 Red

| Dato | Contenido |
|---|---|
| IPs locales | Por interfaz activa: nombre, IPv4 e IPv6. Se excluyen *loopback* y *link-local*. Las interfaces virtuales (Docker, Hyper-V) se incluyen con su nombre (P-13). |
| IP pública y ubicación | `GET https://ipinfo.io/json`, sin token, una vez por recopilación: campos `ip`, `country` (ISO 3166-1 alfa-2), `region`, `city` y `org`. El resto de campos se ignora. La respuesta es de un tercero y no es confiable: se valida el formato de la IP y la longitud de cada campo. Usa el proxy del sistema si existe, valida TLS, no envía ningún dato del equipo y no reintenta dentro de la misma recopilación. |
| IP de conexión | No la envía el agente: la registra el servidor (RF-16). |

#### 8.3.6 Seguridad

| Dato | Windows 11 / Server | Linux | macOS |
|---|---|---|---|
| Cifrado por volumen | BitLocker / Cifrado de dispositivo (`Win32_EncryptableVolume`): protegido, cifrando, suspendido, sin cifrar | El volumen está sobre `dm-crypt`/LUKS (cadena de `lsblk`) | FileVault (`fdesetup status`) y cifrado APFS por volumen |
| Firewall | Firewall de Windows por perfil (dominio, privado, público). En Windows 11, también los firewalls de terceros registrados en el Centro de seguridad, que no existe en Windows Server | Activo si alguno lo está: `ufw`, `firewalld`, un *ruleset* de `nftables` no vacío o `iptables` con reglas o una política distinta de `ACCEPT` | Firewall de aplicaciones (`socketfilterfw --getglobalstate`); `pf` como dato adicional |

Valores normalizados:
- Cifrado: `encrypted`, `encrypting`, `suspended`, `not-encrypted`, `unknown`.
- Firewall: `active`, `inactive`, `unknown`, con el detalle por perfil o producto.

Los cifrados de terceros (VeraCrypt, etc.) aparecen como `unknown` o `not-encrypted`.

**Resumen por dispositivo**, calculado en el servidor:
- **Disco del sistema cifrado:** estado del volumen de arranque.
- **Firewall activo:** en Windows, los tres perfiles activos o un firewall de terceros activo; en Linux y macOS, el
  resultado de la tabla (P-14).

## 9. Criterios de aceptación

- [ ] **CA-1** En cada plataforma soportada, `install` instala el agente como servicio, el servicio arranca con el sistema y se reinicia tras un fallo.
- [ ] **CA-2** Si no se aceptan los términos, la instalación se cancela sin enrolar el dispositivo ni dejar archivos ni servicio.
- [ ] **CA-3** El código de autorización no se muestra al teclearlo, no se acepta como argumento y no queda guardado en el equipo tras el enrolamiento.
- [ ] **CA-4** Un código incorrecto devuelve `401` y no crea ningún dispositivo. El enrolamiento queda auditado con la versión de los términos y la IP de origen.
- [ ] **CA-5** El agente envía el inventario al arrancar si han pasado ≥ 24 h, y después cada 24 h ± 30 min.
- [ ] **CA-6** Sin conexión, el agente reintenta con espera exponencial y envía en cuanto se recupera la conexión.
- [ ] **CA-7** El inventario contiene los 11 datos de la definición en cada plataforma, o los marca como no disponibles con su motivo.
- [ ] **CA-8** Las aplicaciones aparecen solo por nombre, sin duplicados ni versiones. En Linux solo aparecen los paquetes instalados manualmente.
- [ ] **CA-9** Cada usuario local aparece con sus grupos, su estado (habilitado o no) y la marca de administrador según §8.3.4.
- [ ] **CA-10** Un volumen cifrado con BitLocker, FileVault o LUKS se informa como `encrypted`; uno sin cifrar, como `not-encrypted`.
- [ ] **CA-11** Activar o desactivar el firewall se refleja en el siguiente inventario.
- [ ] **CA-12** El inventario incluye la IP pública, el país, la región y la ciudad obtenidos de ipinfo.io. Si ipinfo.io no responde o devuelve datos inválidos, la sección se marca como no disponible y el inventario se envía igualmente.
- [ ] **CA-13** Un dispositivo no puede enviar inventario en nombre de otro (RN-1).
- [ ] **CA-14** Tras reinstalar el agente en el mismo equipo, la API key anterior deja de funcionar (RN-4).
- [ ] **CA-15** Un inventario con propiedades desconocidas, fuera de los límites o mal formado se rechaza con `400`/`413` y Problem Details.
- [ ] **CA-16** Los endpoints del agente rechazan una sesión de usuario, y los endpoints de usuario rechazan una API key de dispositivo.
- [ ] **CA-17** El código de autorización y las API keys no aparecen en logs (agente ni API), ni en la base de datos en claro, ni en el repositorio.
- [ ] **CA-18** El agente no abre puertos de escucha.
- [ ] **CA-19** Los paquetes y el `SHA256SUMS` se descargan de `/downloads/agent/` sin sesión, con tipo `application/octet-stream`, sin listado de directorios y sin exponer otras rutas del volumen.
- [ ] **CA-21** `install` pide la URL del servidor (Enter acepta la propuesta), rechaza las que no son `https://` y,
  si el servidor no es accesible o su certificado no es válido, cancela antes de pedir el código y sin cambios.
- [ ] **CA-20** El servidor guarda con cada inventario la IP de conexión. Un `X-Forwarded-For` enviado por el cliente no la altera.

## 10. Análisis arquitectónico (preliminar)

### 10.1 Componentes

```text
 Equipo (Win/Linux/macOS)                       itmas.sacservices.cloud
┌──────────────────────────┐  HTTPS saliente   ┌────────────────────────────────────────────────────────────┐
│ itmas-agent (servicio)   │ ────────────────▶ │ borde TLS ─▶ nginx ─▶ api ─▶ postgres                       │
│  install/uninstall/status│                   │   POST /api/v1/device-enrollments  (esquema Enrollment)    │
│  Scheduler ─▶ Collectors │                   │   POST /api/v1/inventory-reports   (esquema Device)        │
│  ─▶ ReportSender         │                   │   IP de conexión = IP de origen (nginx es el borde)        │
│  API key protegida       │                   └────────────────────────────────────────────────────────────┘
└───────────┬──────────────┘
            │ HTTPS saliente, sin token: GET /json → IP pública + país/región/ciudad
            ▼
        ipinfo.io
```

### 10.2 Autenticación del agente (ADR-0009)

Decisión del solicitante: enrolamiento mediante un código de autorización, del que sale una API key por dispositivo
(§18.1, puntos 1 y 5).

- **Código de autorización (v1):** un único valor secreto en la configuración del servidor
  (`INVENTORY_ENROLLMENT_CODE`, generado por `scripts/init-env.sh`). Si no está configurado, el endpoint de enrolamiento
  responde `404`. Rotarlo no afecta a los dispositivos ya enrolados. SPEC-0003 lo sustituirá por códigos gestionados desde la UI.
- **API key del dispositivo:** 256 bits aleatorios. Solo se entrega una vez. En la BD se guarda su hash SHA-256, que
  basta por la entropía de la clave, y se compara en tiempo constante.
- Dos esquemas de autenticación (`Enrollment`, `Device`), separados de la cookie BFF, y dos políticas (`DeviceEnrollment`,
  `EnrolledDevice`) que solo aceptan su propio esquema. La política de usuario rechaza a los dispositivos y viceversa (CA-16).
- El agente envía `X-CSRF: 1` y no envía `Origin`, así que `CsrfProtectionMiddleware` no se modifica ni se relaja.
- Revocación en la v1: reinstalar el agente invalida la API key anterior (RN-4). La revocación desde la UI llega con
  SPEC-0003. Hasta entonces, revocar un dispositivo comprometido es un procedimiento operativo documentado (§14.1).
- Alternativas descartadas:
  - Una API key compartida por toda la flota: si se filtra, compromete todos los dispositivos y no se puede revocar uno solo.
  - mTLS: exige una PKI propia; queda como evolución.
  - *Client credentials* de Entra ID: equivalen a un secreto compartido.

### 10.3 Agente (ADR-0010)

Requisito del solicitante: una tecnología fácil de mantener en todas las plataformas (RNF-6).

| Opción | Valoración |
|---|---|
| **.NET 10 Worker Service**, *self-contained*, un solo archivo por RID (`win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`) | **Propuesta.** Un único código base en el mismo lenguaje que el backend y con los mismos estándares (§12). Soporte de Microsoft para Windows Service y systemd (`Microsoft.Extensions.Hosting.WindowsServices` / `.Systemd`); en macOS se usa `launchd` mediante un *plist*. No necesita *runtime* instalado en el equipo. Ocupa unos 60–80 MB por binario; *trimming* y Native AOT se evalúan en el ADR. |
| Go | Binarios pequeños, pero añade un segundo lenguaje y otra cadena de herramientas al equipo. |
| osquery + envío propio | Es una dependencia grande ejecutada como root, para datos que se obtienen fácilmente de forma directa. |

```text
agent/
├── Itmas3.Agent.slnx
├── src/
│   ├── Itmas3.Agent/                Host, comandos install/uninstall/status, scheduler, cliente HTTP, almacén de la API key
│   └── Itmas3.Agent.Collectors/     Interfaces de colectores + implementaciones Windows / Linux / macOS
├── tests/Itmas3.Agent.Tests/        Parsers con salidas reales de cada SO (fixtures), scheduler, cliente HTTP
├── terms/                           Términos y condiciones versionados (Anexo A)
└── packaging/                       Plantillas de servicio (systemd unit, launchd plist), empaquetado zip/tar.gz + SHA256SUMS
```

- Es una solución separada del backend, sin referencias a `Itmas3.*`. El contrato JSON `v1` (§12.2) se comprueba con
  *fixtures* compartidos y con pruebas de contrato en ambos lados.
- Los colectores son independientes entre sí: si uno falla, los demás siguen (RF-9). Los parsers son funciones puras y
  se prueban con *fixtures*, sin necesitar el sistema operativo real.
- Compilación: `dotnet publish` dentro de un contenedor (sin SDK local), que genera los cinco artefactos y `SHA256SUMS`.
  Los binarios de Apple Silicon necesitan al menos una firma *ad hoc*: no es una firma de desarrollador, la genera el SDK
  y no requiere cuenta de Apple. Hay que verificarlo en el ADR al compilar desde Linux.
- **Sin firma de código** (decisión del solicitante): ver las consecuencias en §14.1 y el procedimiento de instalación en DEVELOPMENT/README.

### 10.4 Encaje con el servidor

- **Clean Architecture:** nuevo módulo `Inventory`.
  - `Domain`: `Device`, `InventoryReport`.
  - `Application`: casos de uso `EnrollDevice` y `SubmitInventoryReport`, y los puertos `IDeviceRepository`,
    `ISecretHasher`, `IEnrollmentCodeValidator`.
  - `Infrastructure`: EF Core. El servidor no llama a servicios externos.
  - `Api`: esquemas de autenticación, políticas, controllers y contratos.
- **IP de conexión** (P-15, P-15c):
  - En esta etapa, el servidor de `itmas.sacservices.cloud` está directamente expuesto a Internet, con certificado de
    Let's Encrypt. nginx ya **sobrescribe** `X-Forwarded-For` con `$remote_addr`, y la API solo acepta esa cabecera
    desde la red del proxy (`UseForwardedHeaders`).
  - Si el certificado se instala en el propio nginx del contenedor `frontend`, la IP de conexión es la real sin cambios.
  - Si TLS lo termina otro proxy del host (por ejemplo certbot con un nginx del host, Caddy o Traefik), o cuando llegue
    el balanceador, el nginx del contenedor deberá configurar `set_real_ip_from` y `real_ip_header` con la red de ese
    componente. La API no cambia. La spec de infraestructura de producción lo concretará y quedará en el checklist de SECURITY.md.
  - Si la IP no es fiable, se registra como desconocida: nunca se inventa.
- **IP pública y ubicación con ipinfo.io (ADR-0011):**
  - Las obtiene el agente: es el único que ve la red del equipo y hace una sola consulta al día. Se usa sin token, así
    que no hay ningún secreto que distribuir.
  - El uso anónimo tiene un límite de solicitudes por IP de origen. Con una consulta diaria por equipo, aunque varios
    salgan por la misma IP, el volumen queda lejos de ese límite (decisión del solicitante, §18.3).
  - Es un dato declarado por el equipo. El servidor lo valida como el resto del contrato y lo contrasta con la IP de conexión.
  - Las pruebas del agente sustituyen el `HttpMessageHandler` (§9.4). El servidor no depende de ipinfo.io.
- **Tamaño:** se mantiene el límite de 1 MB de nginx. No se acepta compresión del cuerpo, para evitar bombas de descompresión.
- **Rate limiting:** por dispositivo, 12 inventarios por hora; por IP en el enrolamiento, 10 por minuto, lo que también
  frena la fuerza bruta sobre el código.
- **Almacenamiento:** columnas resumen en `devices` (para filtrar en SPEC-0003) y el inventario completo en `jsonb` por informe.
- **Multi-organización futura:** el dispositivo no lleva organización en la v1. Cuando llegue la multi-organización, el
  código de enrolamiento la determinará y `devices` recibirá `organization_id` con una migración. Nada en esta spec lo impide.

### 10.5 Decisiones que requieren ADR

- **ADR-0009** Autenticación de dispositivos: código de autorización → API key por dispositivo; esquemas y políticas.
- **ADR-0010** Agente: tecnología, estructura, comandos, privilegios, compilación, empaquetado y distribución sin firma.
- **ADR-0011** IP pública y ubicación con ipinfo.io desde el agente: dependencia externa, datos que ve el tercero, uso anónimo, validación y fallos; IP de conexión en el servidor.

### 10.6 Conflictos con la constitución y su resolución

| # | Regla | Conflicto | Resolución (aceptada por el solicitante en §18.1) |
|---|---|---|---|
| 1 | §3.2, §3.3 | La autenticación es solo de personas con Entra ID + BFF, y un endpoint solo admite tres formas de autorización. | **Enmienda MINOR**: autenticación de dispositivos (ADR-0009) y las políticas `DeviceEnrollment`/`EnrolledDevice` como cuarta forma, limitada a endpoints de dispositivos. |
| 2 | §2, §12 | La constitución no prevé componentes instalados en los equipos. | **Enmienda MINOR**: nueva sección "Agente" (estructura, estándares de §12 aplicables, privilegios, sin dependencia de `backend/`). |
| 3 | §8.1 | Todo el sistema debe levantarse con `docker compose`. | **Enmienda PATCH**: §8 aplica a los componentes de servidor. El agente se compila en un contenedor y un perfil `agent` lo ejecuta en Linux para pruebas de extremo a extremo. |
| 4 | §3.8, §8.2 | Mínimo privilegio y "no root". | **Enmienda MINOR**: excepción explícita, porque el agente necesita privilegios elevados (decisión del solicitante). Controles compensatorios: solo lectura, sin puertos, sin ejecución remota, comandos con ruta absoluta y sin *shell*, y *sandbox* de systemd (`ProtectSystem=strict`, `ProtectHome=read-only`, `NoNewPrivileges`, `PrivateTmp`). `security` debe confirmar que se trata de una excepción acotada y no de una relajación general (MAJOR). |
| 5 | §4.5 | Los secretos no pueden pasar por línea de comandos. | Sin enmienda. El código se pide de forma interactiva y oculta, o se lee de un archivo o de una variable de entorno (RF-2, RF-3). |
| 6 | §9.4 | Las pruebas no pueden depender de Internet, y el agente consulta ipinfo.io. | Sin enmienda. La regla aplica a las pruebas: el cliente de ipinfo del agente se sustituye por un doble de prueba. En tiempo de ejecución, la dependencia externa se justifica en ADR-0011 (§2.5). |
| 7 | §3.6 | El contrato debe ser estricto, y un agente más nuevo que la API sería rechazado. | Sin enmienda. Contrato estricto (decisión del solicitante), con regla de despliegue: la API va siempre antes que los agentes. Un cambio incompatible es `v2` (§7.4). |
| 8 | §11.5 | Ningún rol es propietario de `agent/**`. | Nuevo rol `agents/device-agent.md` y actualización de la matriz de `agents/README.md`. |
| 9 | §8.4 | En local solo se publica el frontend, en `127.0.0.1`. | Sin conflicto. En producción, los agentes usan `https://itmas.sacservices.cloud`, la misma URL pública que la web. |

## 11. Cambios de frontend

No hay cambios en la SPA: la interfaz de inventario es SPEC-0003.

Hay cambios en nginx (`frontend/nginx/default.conf.template`), para RF-18:
- `location ^~ /downloads/agent/` con `alias` al volumen de paquetes, de solo lectura.
- `autoindex off`.
- `Content-Type: application/octet-stream` (salvo `SHA256SUMS`, que es `text/plain`) y `X-Content-Type-Options: nosniff`.
- `Content-Disposition: attachment`.
- `Cache-Control` corto para `latest/`.
- Se mantienen las cabeceras de seguridad existentes y el límite de frecuencia global de nginx, si existe.

nginx sigue siendo el único servicio con puerto publicado (constitución §8.4).

## 12. Cambios de backend

### 12.1 Endpoints

| Método | Ruta | Autorización | Request | Response | Errores |
|---|---|---|---|---|---|
| POST | `/api/v1/device-enrollments` | `DeviceEnrollment` | `{ hardwareId, hostname, osFamily, agentVersion, termsVersion, termsAcceptedAt }` | `201 { deviceId, apiKey }` | 400, 401, 404 (enrolamiento desactivado), 422 (términos no vigentes), 429 |
| POST | `/api/v1/inventory-reports` | `EnrolledDevice` | Inventario `v1` (§12.2) | `202` | 400, 401, 413, 429 |

### 12.2 Contrato del inventario `v1` (estricto) y límites

```json
{
  "schemaVersion": 1,
  "collectedAt": "2026-10-07T12:00:00Z",
  "agentVersion": "1.0.0",
  "system": { "hostname": "…", "osFamily": "windows|linux|macos", "osName": "…", "osVersion": "…", "osBuild": "…", "architecture": "x64|arm64" },
  "hardware": {
    "memoryBytes": 0,
    "cpus": [{ "model": "…", "physicalCores": 0, "logicalProcessors": 0 }],
    "disks": [{ "id": "…", "model": "…", "sizeBytes": 0, "mediaType": "ssd|hdd|unknown", "busType": "nvme|sata|sas|usb|virtual|unknown" }],
    "volumes": [{ "mountPoint": "C:\\", "fileSystem": "NTFS", "sizeBytes": 0, "freeBytes": 0, "diskId": "…", "isSystem": true,
                  "encryption": { "status": "encrypted|encrypting|suspended|not-encrypted|unknown", "method": "bitlocker|filevault|apfs|luks|null" } }]
  },
  "network": {
    "interfaces": [{ "name": "…", "ipv4": ["…"], "ipv6": ["…"] }],
    "publicNetwork": { "ip": "172.96.143.36", "country": "US", "region": "Florida", "city": "Miami", "org": "AS23470 ReliableSite.Net LLC" }
  },
  "software": { "applications": ["…"] },
  "accounts": { "users": [{ "name": "…", "enabled": true, "isAdmin": true, "groups": ["…"] }] },
  "security": { "firewall": { "status": "active|inactive|unknown", "details": [{ "name": "…", "enabled": true }] } },
  "unavailable": [{ "section": "security.firewall", "reason": "permission-denied|not-supported|timeout|error" }]
}
```

- Propiedades desconocidas → `400`. Enumeraciones cerradas. Todos los campos son obligatorios salvo los anulables declarados.
- `network.publicNetwork` es `null` cuando no está disponible (y figura en `unavailable`). `ip` debe ser una IPv4 o IPv6
  válida; `country`, dos letras mayúsculas; `region`, `city` y `org`, anulables.
- Límites: cadenas ≤ 256 caracteres y sin caracteres de control; aplicaciones ≤ 5 000; usuarios ≤ 1 000; grupos por
  usuario ≤ 200; interfaces ≤ 64; discos y volúmenes ≤ 64 cada uno; cuerpo < 1 MB; `collectedAt` no posterior a ahora + 5 min.
- Todo dato del equipo es no confiable: nunca se interpreta como HTML ni se concatena en SQL.

### 12.3 Otros cambios

- Casos de uso `Inventory/EnrollDevice` e `Inventory/SubmitInventoryReport`, más una tarea en segundo plano que purga el
  histórico vencido (RF-17).
- Configuración nueva, que se documenta en `.env.example` y en README:

  | Variable | Contenido |
  |---|---|
  | `INVENTORY_ENROLLMENT_CODE` | Secreto; lo genera `init-env.sh`. |
  | `Inventory__HistoryRetentionDays` | Retención del histórico, en días. |
  | `Inventory__TermsVersions` | Versiones de los términos vigentes. |

## 13. Cambios de base de datos (preliminar; los concreta `database`)

Esquema `app`, migración `AddDeviceInventory`:

| Tabla | Contenido principal |
|---|---|
| `devices` | PK `id`; `hardware_id` (único); `api_key_hash` (único); `enrolled_at`, `enrolled_from_ip`, `terms_version`, `terms_accepted_at`, `last_seen_at`, `last_connection_ip`, `last_report_id`; columnas resumen: `hostname`, `os_family`, `os_name`, `os_version`, `agent_version`, `public_ip`, `country_code`, `region`, `city`, `system_volume_encryption`, `firewall_status`, `has_local_admins` |
| `inventory_reports` | PK `id`; FK `device_id` (cascade); `collected_at`, `received_at`, `connection_ip`, `public_ip`, `country_code`, `region`, `city`, `payload jsonb`; índice `(device_id, collected_at desc)` |

No hay cambios de roles. La purga usa `ExecuteDelete` de EF Core, que genera SQL parametrizado.

## 14. Consideraciones de seguridad

### 14.1 Amenazas (STRIDE) y riesgos aceptados

| Amenaza | Mitigación |
|---|---|
| **S** Filtración del código de autorización, que permitiría enrolar equipos falsos y contaminar el inventario | Se comparte por canal interno. Rate limiting por IP. Cada enrolamiento queda auditado (`device.enrolled`, con IP y hostname). Rotación cambiando la variable, sin afectar a los dispositivos existentes. **Riesgo residual:** es un único código compartido hasta SPEC-0003. |
| **S** Robo de la API key de un dispositivo, que permitiría enviar inventarios falsos de ese dispositivo | Clave única y protegida en el equipo (§14.2). Se invalida reinstalando el agente. **Riesgo residual:** hasta SPEC-0003 no hay revocación desde la UI; existe un procedimiento operativo documentado. |
| **S** Suplantación de la API (MITM) | HTTPS obligatorio con validación del certificado, sin opción de desactivarla. |
| **T** Binario del agente alterado (no hay firma de código) | Descarga solo por HTTPS desde un origen controlado por ECS y publicación de `SHA256SUMS`; las instrucciones piden verificar la suma. **Riesgo aceptado por el solicitante.** Consecuencias: aviso de SmartScreen en Windows, bloqueo de Gatekeeper en macOS (hay que permitirlo en Ajustes → Privacidad y seguridad), posibles falsos positivos de antivirus. |
| **T** Un equipo declara datos falsos (p. ej. "cifrado: sí" o una IP pública inventada) | Inherente a un agente: el inventario refleja lo que declara el equipo. La IP de conexión la registra el servidor y sirve para contrastar la IP pública declarada. |
| **T** Respuesta de ipinfo.io manipulada o inesperada | TLS validado; solo se extraen 5 campos con formato y longitud validados; el servidor vuelve a validarlos. |
| **T** Secuestro del identificador de hardware al enrolar (RN-4) | Requiere el código de autorización. Invalida la clave anterior y queda auditado. |
| **R** Enrolamientos sin rastro | Eventos `device.enrolled` y `device.relinked` en la auditoría existente (SPEC-0001). Los envíos diarios quedan en `inventory_reports`. |
| **I** Datos personales (nombres de cuenta, IP, ciudad) | Términos aceptados en la instalación (Anexo A). No se escriben en logs. Retención limitada. El acceso en la UI llegará con SPEC-0003 y sus permisos. |
| **I** Exposición a ipinfo.io | El tercero solo ve la IP de salida del equipo (inherente a cualquier conexión) y una petición diaria sin datos del equipo. Se informa en los términos (Anexo A) y en ADR-0011. |
| **I** Secretos en logs, BD o repositorio | Solo se guardan hashes. El logging HTTP no registra cabeceras. El código solo existe en una variable de entorno del servidor. |
| **D** Avalancha de envíos o inventarios enormes | Rate limiting, límite de 1 MB, límites por colección y variación aleatoria de la hora de envío. |
| **D** ipinfo.io caído, bloqueado por el firewall corporativo o con la cuota agotada | Tiempo máximo de 10 s; la sección se marca como no disponible y el inventario se envía igualmente. La IP de conexión sigue registrándose. |
| **E** El agente, que corre con privilegios elevados, usado como vector (binario de sistema suplantado, comando inyectado) | Rutas absolutas, sin *shell*, argumentos fijos y nunca datos externos como argumento. El directorio de instalación solo es escribible por administradores. |
| **E** Compromiso del servidor que se propaga a los equipos | El agente no acepta órdenes ni descarga código: no hay ejecución remota ni autoactualización. |

### 14.2 Protección de la API key en el equipo

| SO | Ubicación |
|---|---|
| Windows | `%ProgramData%\ITMas\Agent\`, ACL solo para `SYSTEM` y `Administradores`, cifrada con DPAPI (ámbito de máquina) |
| Linux | `/var/lib/itmas-agent/`, `root:root`, `0600` |
| macOS | `/Library/Application Support/ITMas/Agent/`, `root:wheel`, `0600` |

### 14.3 Datos personales

El inventario contiene nombres de cuentas locales, la IP pública y la ubicación aproximada. El tratamiento se basa en la
aceptación de los términos y condiciones al instalar (Anexo A, texto general a petición del solicitante). Se recomienda
que el área legal revise el texto antes del despliegue. Esta spec no sustituye esa revisión.

## 15. Pruebas

| Criterio | Tipo | Prueba prevista |
|---|---|---|
| CA-1 | E2E Linux en contenedor + verificación documentada | Instalación, reinicio y recuperación tras fallo. Lista de verificación manual en Windows 11, Windows Server y macOS (Intel y Apple Silicon). |
| CA-2, CA-3 | Unit (agente) | Rechazo de los términos sin efectos; entrada oculta; ausencia de argumento para el código; borrado del código. |
| CA-4, CA-14 | Integración (API) | Código válido o incorrecto; auditoría; reenrolamiento con el mismo `hardwareId` → la clave anterior da `401`. |
| CA-5, CA-6 | Unit (agente, reloj simulado) | Próxima ejecución, variación aleatoria, espera exponencial, envío tras recuperar la conexión. |
| CA-7 a CA-11 | Unit (parsers con *fixtures* reales) + verificación manual por plataforma | Salidas de `lsblk`, `apt-mark`, `dscl`, `fdesetup`, `ufw`, `nft`, registro, WMI… → modelo normalizado. |
| CA-12 | Unit (agente) | Doble de ipinfo con respuesta válida, campos inválidos, error HTTP y tiempo agotado → `publicNetwork` o `unavailable`. |
| CA-20 | Integración | IP de conexión registrada; `X-Forwarded-For` enviado por el cliente → ignorado. |
| CA-13, CA-16 | Integración | La clave de A no escribe en B; sesión de usuario en endpoints de agente y API key en endpoints de usuario → 401/403. |
| CA-15 | Integración | Propiedad desconocida, colecciones fuera de límite, cuerpo > 1 MB. |
| CA-17 | Unit + revisión | Logs capturados sin secretos; solo hashes en la BD; búsqueda de secretos en el repositorio. |
| CA-18 | Verificación documentada | `netstat`/`lsof` con el agente en ejecución. |
| CA-19 | E2E (compose) | `curl` a un paquete, a `SHA256SUMS`, al directorio (→ 403/404) y con *path traversal* (→ 400/404). |

## 16. Plan de implementación (preliminar)

| # | Tarea | Agente | Archivos principales |
|---|---|---|---|
| 1 | ADR-0009/0010/0011; enmiendas (§10.6, puntos 1–4) → constitución 1.1.0; rol `device-agent` | architecture + security | `specs/architecture/`, `constitution.md`, `agents/` |
| 2 | Migración `AddDeviceInventory` | database | `Infrastructure/Persistence/**` |
| 3 | Dominio y casos de uso `Inventory` | backend | `Itmas3.Domain/Inventory/**`, `Itmas3.Application/Inventory/**` |
| 4 | Esquemas y políticas de dispositivo, rate limiting, controllers y contratos | backend + security | `Itmas3.Api/**` |
| 5 | Configuración (`.env.example`, `init-env.sh`, compose) | architecture | `scripts/`, `.env.example`, `docker-compose.yml` |
| — | **Incremento A**: API verificable con un cliente de prueba | testing | `backend/tests/**` |
| 6 | Agente: comandos, términos, enrolamiento, scheduler, cliente HTTP, almacén de la API key | device-agent | `agent/src/Itmas3.Agent/**`, `agent/terms/**` |
| 7 | Colectores Linux, Windows y macOS, cliente de ipinfo.io + *fixtures* | device-agent | `agent/src/Itmas3.Agent.Collectors/**`, `agent/tests/**` |
| 8 | Compilación en contenedor, empaquetado + `SHA256SUMS`, perfil `agent` en Compose | device-agent + architecture | `agent/packaging/**`, `docker-compose*.yml` |
| 8b | Ruta `/downloads/agent/` en nginx y volumen de paquetes | frontend + security | `frontend/nginx/**`, `docker-compose.yml` |
| — | **Incremento B**: agente funcional en las 5 plataformas contra la pila local | testing | — |
| 9 | Revisión de seguridad; documentación (README, ARCHITECTURE, SECURITY, DEVELOPMENT, threat model, guía de instalación del agente) | security + architecture | docs |
| — | **Cierre**: spec → Implemented | testing + security | — |

## 17. Trazabilidad

Implementada el 2026-10-08.

| Área | Archivos principales |
|---|---|
| Decisiones | ADR-0009 (autenticación de dispositivos), ADR-0010 (agente), ADR-0011 (IP pública con ipinfo.io); constitución 1.1.0 (§3.2, §3.3, §3.8, §8, nueva §13); ADR-0003 (nota); rol `agents/device-agent.md` |
| Dominio | `backend/src/Itmas3.Domain/Inventory/` (`Device`, `InventoryReport`) |
| Aplicación | `backend/src/Itmas3.Application/Inventory/` (`EnrollDevice`, `SubmitInventoryReport`, `AuthenticateDevice`, `PurgeHistory`, `DeviceSecrets`, puertos) |
| Infraestructura | `backend/src/Itmas3.Infrastructure/Inventory/`, `Persistence/Configurations/{Device,InventoryReport}Configuration.cs`, migración `AddDeviceInventory` |
| API | `Itmas3.Api/Inventory/` (esquemas `Enrollment`/`Device`, políticas, *rate limiting*, purga), `Controllers/Inventory/`, `Contracts/Inventory/`, `Security/ConnectionIp.cs` |
| Agente | `agent/` (`Itmas3.Agent`, `Itmas3.Agent.Collectors`, pruebas y *fixtures*, `packaging/`, `terms/`, `Dockerfile`, `README.md`) |
| Distribución | `scripts/build-agent.sh`, `frontend/nginx/default.conf.template` (`/downloads/agent/`), `docker-compose.yml` (volumen, variables, perfil `agent`) |
| Configuración | `INVENTORY_ENROLLMENT_CODE`, `INVENTORY_HISTORY_RETENTION_DAYS`, `AGENT_DOWNLOADS_DIR` (`.env.example`, `init-env.sh`, README) |

| Criterio | Prueba o verificación |
|---|---|
| CA-1 | Linux arm64: `install` real en Ubuntu 24.04 con systemd (servicio activo con el *sandbox* completo, `Type=notify`, reinicio tras `kill -9`, `uninstall` limpio). Linux x64: `install` + `run` contra la pila local por HTTPS, sin systemd. **Pendiente (manual):** Windows 11, Windows Server y macOS (Intel y Apple Silicon) |
| CA-2, CA-3 | `InstallCommandTests` (términos rechazados sin efectos, entrada oculta, archivo y variable de entorno, argumento rechazado en `Program`) |
| CA-4, CA-14 | `InventoryTests.CA04_*`, `CA14_*`, `Enrollment_is_disabled_without_a_configured_code`, `RN03_*`; `EnrollDeviceHandlerTests` |
| CA-5, CA-6 | `ScheduleTests`, `InventoryCycleTests` |
| CA-7 a CA-11 | `LinuxParsersTests`, `LinuxSourceTests`, `MacOsParsersTests`, `WindowsParsersTests`, `InventoryCollectorTests`; `collect` real en macOS 27 (Apple Silicon) y en Ubuntu 24.04 x64 (contenedor) |
| CA-12 | `NetworkTests`, `InventoryCollectorTests.CA12_*` |
| CA-13, CA-16 | `InventoryTests.CA13_and_CA20_*`, `CA16_*`; `EndpointAuthorizationConventionTests.Device_policies_are_only_used_by_device_endpoints` |
| CA-15 | `InventoryTests.CA15_*` (el 413 con Kestrel real), `ContractValidationTests` |
| CA-17 | `DeviceSecretsTests`, `ApiClientTests`, `InstallCommandTests.CA03_*`; revisión: sin registro de cabeceras, solo hashes |
| CA-18 | Verificación documentada: el agente solo usa `HttpClient` saliente; ningún *listener* en el código |
| CA-19 | Verificación con `curl` contra la pila local: paquete → 200 `application/octet-stream` + `attachment`; `SHA256SUMS` → `text/plain`; listado → 404; *traversal* → 400; `POST` → 403 |
| CA-20 | `InventoryTests.CA20_*`; verificación con `curl` (`X-Forwarded-For` falsificado ignorado) |
| CA-21 | `InstallCommandTests` (URL por defecto, reintento ante URL inválida, reinstalación, variable de entorno, servidor no confiable antes del código), `ApiClientTests` (`/healthz`, errores de certificado y DNS); binario real contra el dominio actual: rechazo por certificado |
| Contrato | `ContractTests` (agente) e `InventoryTests.Contract_fixture_shared_with_the_agent_is_accepted` (API) con el mismo *fixture* |

Totales al cerrar: backend 184 pruebas (112 unitarias + 72 de integración), agente 129. Los paquetes de las 5
plataformas se generan con `./scripts/build-agent.sh`; el binario de macOS arm64 compilado en Linux lleva firma *ad hoc*
y se ejecuta en macOS.

**Ajustes de implementación** (no cambian el comportamiento aprobado):
- Comandos adicionales `collect` (inventario sin envío, para verificar) y `version`; `run` es el modo servicio.
- Si el SO no expone identificador de hardware (p. ej. contenedores sin `/etc/machine-id`), se usa un identificador de
  instalación aleatorio y persistente.
- El límite de envíos se aplica por credencial (hash de la cabecera), porque el limitador corre antes de autenticar.
- macOS no informa los volúmenes auxiliares del sistema (Recovery, Preboot, VM, Update). Linux no informa montajes
  sin sistema de archivos (*bind mounts* de archivos).
- Linux: el firewall de `iptables`/`nftables` se evalúa solo sobre la cadena de entrada, para no confundir las reglas
  de Docker con un firewall.

**Validación manual pendiente del solicitante:**
1. `install`, reinicio del equipo y `status` en Windows 11, Windows Server (2019/2022/2025) y macOS (Intel y Apple
   Silicon), con los paquetes de `/downloads/agent/latest/`.
2. Despliegue en `itmas.sacservices.cloud` con Let's Encrypt, revisando la IP real del cliente (SECURITY.md §8).

## 18. Decisiones y preguntas abiertas

### 18.1 Primera ronda (2026-10-07)

| # | Tema | Decisión |
|---|---|---|
| P-2 | Organizaciones y volumen | Será multi-organización en el futuro; hoy solo ECS. Unos 80 dispositivos. |
| P-3 | Autenticación del agente | Enrolamiento con un código de autorización introducido al instalar, del que sale una API key por dispositivo. |
| P-4/P-9 | Alcance | La interfaz de consulta y la gestión de códigos se dejan para la siguiente etapa (SPEC-0003). |
| P-5 | Geolocalización | Usar ipinfo.io. |
| P-7 | Alcance de los datos | Linux: paquetes instalados manualmente (son mayoritariamente servidores). Usuarios: solo cuentas locales. |
| P-11 | Distribución | Instalación manual, sin herramienta de distribución y sin firma de código. |
| P-16 | Datos personales | El usuario acepta términos y condiciones generales al instalar. |
| §10.6 | Constitución | Se aceptan las enmiendas, los privilegios elevados del agente y un contrato estricto. |

### 18.2 Segunda ronda (2026-10-08)

| # | Tema | Decisión |
|---|---|---|
| P-1 | Plataformas | Windows 11 x64; Windows Server 2019, 2022 y 2025 x64; Ubuntu 22.04/24.04 y Debian 12 (x64 y arm64); macOS Intel y Apple Silicon (versiones soportadas por .NET 10). |
| P-3b | Código de autorización | Un código único en la configuración del servidor. SPEC-0003 cambiará el mecanismo. |
| P-5b | ipinfo.io | Uso libre, sin token. (Ver P-5c: la consulta la hace el agente.) |
| P-15 | Publicación | `itmas.sacservices.cloud`, con el servidor expuesto directamente a Internet. Más adelante, detrás de un balanceador. |
| P-17 | Descarga | `https://itmas.sacservices.cloud/downloads/agent/` servido por nginx, con `SHA256SUMS`. |
| P-18 | Aplicaciones en Windows | HKLM + HKU de los perfiles cargados, sin Store. |
| P-6, P-7b, P-8, P-10, P-12, P-13, P-14 | Resto | Se aceptan las propuestas: cadencia de 24 h ± 30 min; apps de `/Applications` y estado habilitado de las cuentas; 90 días de histórico; reinstalación vinculada al dispositivo existente; actualización manual; filtros de discos y redes; firewall de Windows activo = tres perfiles activos. |

### 18.3 Tercera ronda (2026-10-08)

| # | Tema | Decisión |
|---|---|---|
| P-1c | macOS | Versiones recientes. Los Mac Intel tienen la última versión de macOS que soporta su hardware. ADR-0010 verifica que .NET 10 las soporta. |
| P-3c | Código de autorización | Confirmado: un código general hasta SPEC-0003. |
| P-5c | IP pública | La obtiene el **agente** con ipinfo.io (una vez al día, sin token) y la envía en el inventario. El servidor guarda **además** la IP de conexión HTTP. Sustituye a la propuesta de geolocalizar en el servidor. |
| P-15c | TLS | Certificado de Let's Encrypt. El despliegue de producción se concretará en una spec de infraestructura. |
| P-17, P-18, resto | — | Confirmados. |

### 18.4 Cuarta ronda (2026-10-08, tras la primera prueba de instalación)

| # | Tema | Decisión |
|---|---|---|
| P-19 | URL del servidor | La instalación falló contra `itmas.sacservices.cloud`, que todavía responde con un certificado de otro servicio (Azure Bastion). El agente rechazó la conexión, como debía. A petición del solicitante, `install` pide la URL (con valor por defecto y validación `https://`) y comprueba la conexión antes de pedir el código. También se acepta `--server-url` / `ITMAS_SERVER_URL`. Agente 1.1.0. |

### 18.5 Preguntas abiertas

Ninguna.

## Anexo A — Términos y condiciones (texto general, versión 1)

> Se recomienda revisión legal antes del despliegue. El agente muestra el texto vigente, que se versiona en `agent/terms/`.

```text
TÉRMINOS Y CONDICIONES DE USO DEL AGENTE DE INVENTARIO ITMas (versión 1)

Al instalar este agente, usted declara estar autorizado para instalar software en este equipo y acepta que:

1. El agente recopilará una vez al día información técnica del equipo: nombre del equipo, sistema operativo y
   versión, aplicaciones instaladas, cuentas de usuario locales y los grupos a los que pertenecen, direcciones IP
   locales, características de memoria, procesador y discos, estado de cifrado de los discos y estado del firewall.
2. Para conocer la dirección IP pública del equipo y su ubicación aproximada (país, región y ciudad), el agente
   consultará una vez al día un proveedor externo (ipinfo.io), sin enviarle ningún otro dato del equipo.
   La información se enviará de forma cifrada a la plataforma ITMas, que registrará además la dirección IP desde la
   que recibe la conexión.
3. La finalidad es exclusivamente la gestión del inventario tecnológico y de la seguridad de la información de la
   organización. La información no se utilizará para otros fines.
4. El agente no accede al contenido de sus archivos, correos, navegación ni actividad personal, y no permite controlar
   el equipo de forma remota.
5. La información se conservará durante el tiempo necesario para la finalidad indicada y será accesible solo por el
   personal autorizado.
6. Puede desinstalar el agente en cualquier momento con el comando "itmas-agent uninstall". Para consultar, actualizar
   o solicitar la supresión de sus datos, contacte con el área de TI de su organización.
```
