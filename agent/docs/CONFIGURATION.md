# Configuración

Dos almacenes separados, deliberadamente: la API key (secreta) nunca toca el archivo de configuración en texto plano.

## `config.json` (no secreto)

Ruta: `/Library/Application Support/ITMasAgent/config.json` (root, `0700`/`0600`). Ver `Resources/config.example.json`. Se escribe con `itmas-agent configure` — nunca a mano en producción, aunque el formato es JSON simple si hace falta inspeccionarlo/editarlo manualmente.

| Campo | Tipo | Default | Descripción |
|---|---|---|---|
| `apiBaseUrl` | string | *(requerido)* | Raíz de la API de IT-MAS, ej. `https://itmas.example.com/api/v1`. |
| `category` | `"collaborator"` \| `"infrastructure"` | *(requerido)* | Debe coincidir con la categoría usada al aprovisionar el dispositivo. |
| `scheduledHour` | int | `9` | Hora local (0-23) de la corrida diaria. |
| `scheduledMinute` | int | `0` | Minuto local de la corrida diaria. |
| `publicIpLookupUrl` | string | `https://api.ipify.org?format=text` | Servicio externo para la IP pública (best-effort) — ver aviso de privacidad en `PERMISSIONS.md`. |
| `tlsCaBundlePath` | string \| null | `null` | Ruta a un bundle de CA personalizado, para un backend con certificado propio/autofirmado. `null` = validación TLS estándar del sistema. |

## API key del nodo (secreta)

Nunca en `config.json`. Se almacena en el **Keychain de Sistema** de macOS (`/Library/Keychains/System.keychain`), vía `itmas-agent configure --api-key '<deviceId>.<secret>'` (lo hace `install.sh` por ti). Recuperarla, cambiarla o borrarla:

```bash
sudo itmas-agent configure --api-key '<nuevo-deviceId>.<nuevo-secret>' --api-base-url ... --category ...
sudo itmas-agent service uninstall   # también borra la API key del Keychain
```

No existe un comando para *leer* la API key de vuelta en texto — por diseño, igual que el backend nunca la muestra tras el aprovisionamiento inicial. Si se perdió, hay que rotarla desde el backend (`npm run device:rotate-key`) y volver a configurar el agente con la nueva.

## Cambiar la hora programada

```bash
sudo itmas-agent configure --api-key '<la-misma-que-ya-tenías>' --api-base-url <la-misma> --category <la-misma> \
  --scheduled-hour 22 --scheduled-minute 30
```

`configure` sobrescribe `config.json` completo — hay que repetir todos los campos, no solo el que cambia (evita un `PATCH` parcial ambiguo en un archivo de configuración simple).
