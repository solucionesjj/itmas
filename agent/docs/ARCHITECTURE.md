# Arquitectura

Ver `docs/adr/0011-agent-multiplatform-python-architecture.md` (raíz del repo) para el razonamiento completo de cada decisión. Este documento es la referencia práctica de "cómo está armado" y "cómo extenderlo".

## El principio: núcleo compartido + `PlatformBackend`

Todo lo que `psutil`/stdlib ya resuelven de forma uniforme en macOS/Windows/Linux (almacenamiento, CPU/memoria, sesiones activas para `last_login`, IP local, IP pública) vive directamente en `collectors/`, sin pasar por ningún backend. Solo lo que genuinamente difiere por sistema operativo (hardware/serial, aplicaciones instaladas, usuarios/grupos locales, almacén de credenciales, instalación del servicio) pasa por la interfaz `PlatformBackend` (`platforms/base.py`):

```python
class PlatformBackend(Protocol):
    platform_name: str
    def hardware_info(self) -> HardwareRawInfo: ...
    def hardware_serial(self) -> Optional[str]: ...
    def os_info(self) -> OSRawInfo: ...
    def installed_applications(self) -> list[RawAppInfo]: ...
    def local_users(self) -> list[RawUserInfo]: ...
    def credential_store(self) -> CredentialStore: ...
    def service_installer(self) -> ServiceInstaller: ...
```

`platforms/factory.py` elige la implementación según `platform.system()`. Los collectors reciben el backend por inyección de constructor y nunca saben en qué sistema operativo corren.

## Patrón de fallo parcial: `Measured<T>`

Cada dato recolectado se envuelve en `Measured` (`models.py`) — `.value` (o `None`) + `.reason` (diagnóstico interno, nunca sale al JSON de red). Un dato faltante nunca aborta la recolección del resto; ver `collectors/*.py` para el patrón repetido en cada uno.

## Flujo de una corrida (`composition.AgentRunner.run_due_work`)

1. `scheduling.is_daily_run_due()` decide si corresponde correr (salvo `--now`).
2. Se ejecutan los siete collectors, construyendo un `InventorySnapshot` rico.
3. Se cachea el JSON extendido completo localmente (`normalization/extended_schema.py`) — listo para transmitirse íntegro el día que el backend lo acepte (ver ADR-0012).
4. `normalization/inventory_mapper.py` mapea al subconjunto que el contrato **actual** de `/api/v1/inventory` acepta. Si falta un campo requerido por ese contrato, no se envía nada ese ciclo (se reintenta solo, no se inventa el dato).
5. Se intenta transmitir; fallos retryable (429/5xx/red) van a `RetryQueueStore` (acotada, máx. 500 items/7 días); fallos no-retryable (4xx) se registran como anomalía real, sin reintentar.
6. Antes de enviar lo nuevo, se intenta vaciar la cola de reintento pendiente.

## Por qué LaunchDaemon (macOS)

Un LaunchAgent solo existe dentro de una sesión con login — no sirve para "corre a las 9am aunque nadie haya iniciado sesión" ni para ver los datos de todos los usuarios locales, no solo el de una sesión. `LaunchdServiceInstaller` (`platforms/macos/launchd_service_installer.py`) registra `/Library/LaunchDaemons/com.ecs-la.itmas.agent.plist` vía `launchctl bootstrap system`.

## Por qué tres mecanismos de programación, no uno solo

- **`StartCalendarInterval: {Hour: 9, Minute: 0}`** — corrida normal diaria; launchd tiene comportamiento anacron nativo (si el equipo estaba dormido a las 9am, corre al despertar).
- **`RunAtLoad: true`** — cubre lo que el anacron no cubre: una instalación/reinicio que ocurre después de las 9am del mismo día.
- **`scheduling.is_daily_run_due()`** — el filtro real, agnóstico de SO y testeable con un reloj inyectado: sin importar qué disparó la invocación, solo corre si hoy (fecha local) no tiene una corrida exitosa registrada Y ya son ≥ la hora configurada.

`itmas-agent run --now` (bypassa el filtro) y `launchctl kickstart -k system/com.ecs-la.itmas.agent` cubren la ejecución a demanda.

Sin `KeepAlive`: cada invocación es efímera por diseño — corre, termina, sin proceso residente.

## Por qué el Keychain de Sistema, no `keyring`

Al arrancar un LaunchDaemon no hay sesión de usuario, así que un keychain de *login* nunca estaría desbloqueado para él. El Keychain de *Sistema* (`/Library/Keychains/System.keychain`) sí está desbloqueado sin sesión. El paquete `keyring` genérico resuelve por defecto contra el keychain de login del usuario actual — el objetivo equivocado para un daemon root sin sesión — por eso `KeychainCredentialStore` llama a `security` directamente, apuntando al Keychain de Sistema de forma explícita.

## Binario universal2: por qué se construye en dos pasos

Un Python universal2 (el de python.org) puede compilar hacia `universal2`, pero las dependencias con extensión C instaladas vía pip (`psutil`) llegan como binarios de una sola arquitectura — PyInstaller no puede fusionar un `.so` de una sola arquitectura en un ejecutable universal2 (verificado empíricamente, no asumido). `Scripts/macos/build.sh` construye un venv arm64 y otro x86_64 (este último vía Rosetta 2), compila cada binario por separado, y los fusiona con `lipo -create`.

## Cómo implementar Windows o Linux

1. Escribe la clase `WindowsBackend`/`LinuxBackend` completa (`platforms/{windows,linux}/backend.py` ya tienen cada método documentado con la API/herramienta prevista en su docstring — úsalos como especificación, no como sugerencia vaga).
2. Escribe el `ServiceInstaller` correspondiente (Task Scheduler con `<StartWhenAvailable>` en Windows; systemd timer con `Persistent=true` en Linux — ambos son el equivalente directo del catch-up de `StartCalendarInterval`, así que `scheduling.py` no cambia).
3. Decide y documenta (como ADR) el mecanismo de `CredentialStore` — ninguno de los dos SO tiene un almacén siempre-disponible sin sesión tan directo como el Keychain de Sistema de macOS.
4. Agrega `Scripts/{windows,linux}/{build,install,uninstall}.*` siguiendo la estructura de `Scripts/macos/`.
5. **Nada en `collectors/`, `normalization/`, `networking/`, `persistence/`, `scheduling.py` ni `cli.py` debería cambiar** — si algo ahí necesita cambiar para soportar un SO nuevo, es una señal de que el seno `PlatformBackend` está mal diseñado, no que sea normal tocarlo.
