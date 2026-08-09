# Instalación (macOS)

## Requisitos previos

1. El dispositivo debe estar **aprovisionado** contra el backend antes de instalar el agente — no hay endpoint REST para esto, es un script de CLI que corre quien administra el backend (ver `backend/scripts/provision-device.ts` en la raíz del repo):

   ```bash
   # Desde backend/, contra el backend en ejecución:
   npm run device:provision -- --hostname <nombre-del-equipo> --category <collaborator|infrastructure>
   ```

   Esto imprime `deviceId: <uuid>` y `apiKey: <deviceId>.<secret>` **una sola vez** — guárdalo, no se puede recuperar después (solo rotar con `device:rotate-key`, lo que invalida el anterior).

2. Rosetta 2 disponible si vas a construir en un Mac Apple Silicon (`softwareupdate --install-rosetta` si no lo tienes) — necesario para el binario universal2.

## Build

```bash
cd agent
./Scripts/macos/build.sh
```

Produce `dist/itmas-agent` (binario universal2, sin firmar en esta versión — ver `docs/ARCHITECTURE.md` sobre firma de código). `lipo -info dist/itmas-agent` debe mostrar `x86_64 arm64`.

## Instalar

```bash
sudo ./Scripts/macos/install.sh \
  --api-key '<deviceId>.<secret>' \
  --api-base-url https://itmas.example.com/api/v1 \
  --category <collaborator|infrastructure> \
  [--scheduled-hour 9] [--scheduled-minute 0]
```

Esto:
1. Copia el binario a `/usr/local/itmas-agent/bin/itmas-agent`.
2. Guarda la API key en el Keychain de Sistema y la configuración no-secreta en `/Library/Application Support/ITMasAgent/config.json`.
3. Registra el LaunchDaemon (`/Library/LaunchDaemons/com.ecs-la.itmas.agent.plist`) vía `launchctl bootstrap`.

## Verificar

```bash
sudo /usr/local/itmas-agent/bin/itmas-agent status
sudo /usr/local/itmas-agent/bin/itmas-agent service status   # "loaded" o "not loaded"
sudo /usr/local/itmas-agent/bin/itmas-agent run --now --dry-run   # corre sin enviar nada
sudo /usr/local/itmas-agent/bin/itmas-agent run --now             # corre y transmite de verdad
```

Logs: `/Library/Logs/ITMasAgent/agent.log` (JSON-lines, del agente mismo) y `/Library/Logs/ITMasAgent/launchd.{out,err}.log` (stdout/stderr crudos de launchd).

## Desinstalar

```bash
sudo ./Scripts/macos/uninstall.sh
```

Pide confirmación escrita ("eliminar") salvo que se pase `--yes`. Desregistra el LaunchDaemon, borra la API key del Keychain de Sistema, y elimina el binario instalado y todo el estado local (config, caché, cola de reintento).

## Actualizar (v1 — manual)

No hay auto-actualización en esta versión (decisión explícita, ver ADR-0011/`ARCHITECTURE.md`). Para actualizar: construir un binario nuevo y volver a ejecutar `install.sh` — sobreescribe el binario instalado sin tocar la configuración/API key ya guardadas (`configure` es idempotente).
