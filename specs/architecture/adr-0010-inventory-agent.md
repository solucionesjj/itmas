---
id: ADR-0010
title: Agente de inventario multiplataforma en .NET
status: Accepted
date: 2026-10-08
deciders: [architecture, security]
related: [SPEC-0002, ADR-0009, ADR-0011]
---

# ADR-0010 — Agente de inventario multiplataforma en .NET

## Contexto
SPEC-0002 requiere un servicio que se instala en los equipos de los colaboradores y que funciona en estas plataformas:

- Windows 11 x64.
- Windows Server 2019, 2022 y 2025.
- Ubuntu 22.04/24.04 y Debian 12, en x64 y arm64.
- macOS en Intel y Apple Silicon.

Tiene que ser fácil de mantener (un único código base) y necesita privilegios elevados para leer el cifrado, el
firewall y los grupos. Se instala a mano, sin firma de código y sin herramientas de distribución masiva. Ningún
componente previo del sistema se ejecuta fuera de Docker.

## Decisión
1. **Tecnología.** El agente es un *Worker Service* de .NET 10, publicado *self-contained* en un solo archivo por RID:
   `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64` y `osx-arm64`. No necesita *runtime* instalado. Sin *trimming*
   (WMI y COM no son compatibles); su tamaño, unos 70 MB, es aceptable para una instalación manual.
2. **Estructura** (`agent/`). Es una solución independiente que no referencia ningún proyecto de `backend/`:

   | Ruta | Contenido |
   |---|---|
   | `src/Itmas3.Agent` | Ejecutable con los comandos `install`, `uninstall`, `status`, `collect` y `run` (modo servicio), además del planificador, el cliente HTTP y el almacén de la API key. |
   | `src/Itmas3.Agent.Collectors` | Modelo del contrato `v1` y colectores por sistema operativo. Las salidas del SO se interpretan con *parsers* puros. |
   | `tests/Itmas3.Agent.Tests` | Pruebas unitarias con *fixtures* de salidas reales de cada SO y prueba de contrato. |
   | `packaging/` | Plantillas de los servicios (`systemd`, `launchd`, `newsyslog`). |

   Se aplican los estándares de §12 de la constitución: `TreatWarningsAsErrors`, analizadores, *nullable*, versiones
   centralizadas, NuGet Audit y `LoggerMessage`.
3. **Ejecución como servicio:**

   | Plataforma | Servicio | Logs |
   |---|---|---|
   | Windows | Servicio `ITMasAgent` (`LocalSystem`, arranque automático, reinicio ante fallo con `sc.exe failure`) | Visor de eventos, origen `ITMas Agent` |
   | Linux | Unidad `itmas-agent.service` (`Restart=on-failure`), con *sandbox*: `ProtectSystem=strict`, `ProtectHome=read-only`, `PrivateTmp`, `NoNewPrivileges`, `ReadWritePaths=/var/lib/itmas-agent` | `journald` |
   | macOS | `LaunchDaemon` `cloud.sacservices.itmas.agent` (`RunAtLoad`, `KeepAlive`) | `/Library/Logs/ITMas/agent.log`, rotado por `newsyslog` |

4. **Privilegios elevados** (`LocalSystem` o `root`), una excepción explícita a §3.8 de la constitución con estos
   controles compensatorios:
   - El agente solo lee el sistema.
   - No abre puertos de escucha.
   - No acepta órdenes del servidor ni descarga código.
   - Los comandos del SO se ejecutan con ruta absoluta, sin *shell*, con argumentos fijos, `LC_ALL=C`, un tiempo
     máximo de 30 s y la salida acotada.
   - Ningún dato externo se usa como argumento: solo nombres de cuentas locales que el propio sistema ha devuelto.
   - El directorio de instalación solo es escribible por administradores.
5. **Fuentes de datos por SO**, usando solo componentes incluidos en el sistema:
   - Windows: registro, WMI (`System.Management`) y `root\SecurityCenter2`.
   - Linux: `/proc`, `/etc`, `lsblk`, `apt-mark`, `ufw`, `firewall-cmd`, `nft` e `iptables`.
   - macOS: `sw_vers`, `sysctl`, `ioreg`, `diskutil`, `fdesetup`, `dscl`, `id`, `socketfilterfw` y `pfctl`.
   - Las IPs locales se obtienen en todas las plataformas con `System.Net.NetworkInformation`.
6. **Credenciales y estado en el equipo:**

   | Plataforma | Ubicación y protección |
   |---|---|
   | Windows | `%ProgramData%\ITMas\Agent` con ACL solo para `SYSTEM` y `Administradores`; API key cifrada con DPAPI (ámbito de máquina) |
   | Linux | `/var/lib/itmas-agent` (`0700`), con la configuración en `/etc/itmas-agent` |
   | macOS | `/Library/Application Support/ITMas/Agent` (`0700`, `root:wheel`) |

   En Linux y macOS los archivos son `0600`.
7. **Comunicación:**
   - Solo HTTPS, con validación del certificado contra el almacén del sistema y sin ninguna opción para desactivarla.
   - Usa el proxy del sistema.
   - `install` pide la URL del servidor y propone la configurada o `https://itmas.sacservices.cloud`. También se
     acepta `--server-url` o `ITMAS_SERVER_URL`: la URL no es un secreto. Solo se admite `https://`.
   - Antes de pedir el código, `install` comprueba con `GET /healthz` que el servidor es accesible y que su
     certificado es válido (SPEC-0002 P-19).
8. **Compilación y distribución:**
   - `scripts/build-agent.sh` compila dentro de un contenedor (`agent/Dockerfile`, sin SDK local), genera
     `itmas-agent-<rid>.zip` en Windows y `.tar.gz` en el resto, y un `SHA256SUMS`.
   - El resultado se publica en `/downloads/agent/<versión>/` y `/downloads/agent/latest/`, que nginx sirve desde un
     volumen de solo lectura.
   - No hay firma de código. Los binarios de macOS llevan la firma *ad hoc* que genera el SDK, obligatoria en Apple
     Silicon y que no requiere cuenta de Apple.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Go | Binarios pequeños y estáticos | Segundo lenguaje y otra cadena de herramientas para un equipo .NET |
| osquery + envío propio | Maduro, multiplataforma | Dependencia grande ejecutada como root, para datos que se obtienen directamente |
| Scripts por SO (PowerShell/bash) | Sin compilación | Tres implementaciones divergentes, difíciles de probar y de mantener |
| Instaladores nativos (MSI/.pkg/.deb) | Mejor experiencia | Requieren firma para ser útiles (decisión del solicitante: sin firma) y tres cadenas de empaquetado |
| Native AOT | Binario menor y arranque rápido | WMI y COM de Windows no son compatibles con AOT |

## Consecuencias
- **Positivas:** un solo código base y un solo lenguaje; las pruebas de los *parsers* no necesitan el SO real; la
  instalación es un único ejecutable autocontenido.
- **Negativas / riesgos:**
  - Al no estar firmados, SmartScreen avisa en Windows y Gatekeeper bloquea en macOS si el archivo llega con el atributo
    de cuarentena (descarga desde el navegador): hay que permitirlo en Ajustes → Privacidad y seguridad. También puede
    haber falsos positivos de antivirus. Mitigación: HTTPS y `SHA256SUMS`.
  - Los privilegios elevados amplían el impacto de una vulnerabilidad en el agente.
  - Sin actualización automática, cada nueva versión implica reinstalar.
- **Seguimiento:** evaluar la firma de código y los instaladores nativos si la flota crece; las versiones de macOS
  soportadas son las que admite .NET 10.

## Impacto en la constitución
Enmiendas en la versión 1.1.0:
- **MINOR**, nueva sección §13 "Agente": estructura, estándares y privilegios.
- **MINOR**, §3.8: excepción acotada para el agente.
- **PATCH**, §8: lo de Docker aplica a los componentes de servidor.
