# itmas-agent

Agente de inventario y monitoreo de IT-MAS. Un solo código Python para macOS, Windows y Linux — el núcleo (collectors, normalización, red, persistencia, CLI) es 100% compartido; lo específico de cada sistema operativo vive detrás de una única interfaz (`PlatformBackend`).

**Estado actual**: macOS completamente implementado y probado. Windows/Linux son stubs documentados (misma interfaz, `NotImplementedError` con el enfoque previsto en cada docstring) — ver `Scripts/windows/README.md` / `Scripts/linux/README.md`.

Ver `docs/` para arquitectura, permisos, instalación y configuración en detalle. Ver `docs/ARCHITECTURE.md` §Decisiones para el porqué de cada elección (Python, LaunchDaemon, cadencia diaria, Keychain de Sistema).

## Quick start (macOS)

```bash
cd agent
python3 -m venv .venv && source .venv/bin/activate
pip install -e ".[dev]"
pytest tests/                                    # suite unitaria, rápida y sin red/root
pytest integration_smoke/ -m macos_integration   # contra este Mac real, solo lecturas

./Scripts/macos/build.sh                          # binario universal2 en dist/itmas-agent

# Aprovisiona el dispositivo una vez contra el backend (fuera de este repo, ver raíz):
#   npm run device:provision -- --hostname <nombre> --category <collaborator|infrastructure>

sudo ./Scripts/macos/install.sh \
  --api-key '<deviceId>.<secret>' \
  --api-base-url https://itmas.example.com/api/v1 \
  --category collaborator

sudo itmas-agent status
sudo itmas-agent run --now --dry-run    # verifica sin enviar nada
```

## Estructura

```
agent/
├── src/itmas_agent/       # el paquete — ver docs/ARCHITECTURE.md
├── tests/                 # unitarias, sin tocar el SO real
├── integration_smoke/     # requieren un Mac real, solo lecturas
├── Resources/             # plist de referencia, config.example.json
├── Scripts/               # build/install/uninstall/smoke-test por plataforma
└── docs/                  # este índice de documentación
```
