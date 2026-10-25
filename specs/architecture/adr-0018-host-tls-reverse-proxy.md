---
id: ADR-0018
title: TLS en un proxy inverso del host delante del frontend (servidor Linux único)
status: Proposed
date: 2026-10-24
deciders: [architecture, security]
related: [ADR-0007, ADR-0011, ADR-0002, SPEC-0002]
---

# ADR-0018 — TLS en un proxy inverso del host delante del frontend

> **Pendiente de aceptación del solicitante y de revisión de `security`.** Hasta entonces el estado es `Proposed`. Cuando se acepte,
> `ADR-0007` y `ADR-0011` ya remiten a este documento y no hace falta tocarlos de nuevo.

## Contexto
Producción es **un único servidor Ubuntu con IP pública**, sin balanceador ni servicios administrados, que sirve
`itmas.sacservices.cloud` (SPEC-0002, P-15 y P-17). La aplicación corre con `docker compose` en ese servidor.

- ADR-0007 dejó el TLS de producción abierto: "en el borde (ingress) delante de `frontend` o en el propio nginx".
- ADR-0011 y SPEC-0002 §10.4 describieron nginx como borde (sobrescribe `X-Forwarded-For` con `$remote_addr`) y anticiparon que,
  si otro proxy del host terminaba el TLS, nginx tendría que usar `set_real_ip_from` y `real_ip_header`.

Poner un proxy TLS delante **sin tocar nginx** rompe dos cosas:

1. **La IP de conexión.** nginx vería siempre la puerta de enlace de Docker. Los inventarios guardarían esa IP (ADR-0011 pierde su
   sentido) y los límites por IP de la API (anónimos 300/min, `/auth/login` 20/min, enrolamiento 10/min) contarían a todos los
   clientes como uno solo.
2. **El esquema.** nginx envía `X-Forwarded-Proto: http` (`$scheme`), así que la API construye el `redirect_uri` de OIDC con
   `http://`: Entra ID lo rechaza (`AADSTS50011`).

La constitución §8.4 exige que solo el frontend publique un puerto. Esta decisión debe respetarlo.

## Decisión
1. **El TLS se termina en un proxy inverso instalado en el host, fuera de Docker.** Escucha en 80 y 443, obtiene y renueva el
   certificado de Let's Encrypt, redirige HTTP a HTTPS y reenvía todo a `127.0.0.1:${FRONTEND_PORT}`. La implementación de
   referencia es **Caddy**; el contrato de abajo no depende de ella.
2. **El frontend sigue siendo el único origen y el único puerto publicado del sistema, solo en loopback** (§8.4). Ningún otro
   servicio del stack publica puertos. Publicar el frontend en otra interfaz invalida esta decisión (ver Consecuencias).
3. **nginx confía en el proxy solo cuando la conexión llega desde la red `edge`**, donde el proxy aparece como la puerta de
   enlace de Docker:
   - `set_real_ip_from <subred de edge>` y `real_ip_header X-Forwarded-For`, con `real_ip_recursive` desactivado: se toma la
     **última** dirección de la cabecera, la que añadió el proxy.
   - `X-Forwarded-Proto` hacia la API es el del proxy **solo si vale exactamente `https`**; en cualquier otro caso es `$scheme`.
   - nginx sigue reconstruyendo `X-Forwarded-For`, `X-Forwarded-Proto` y `X-Forwarded-Host` hacia la API (nunca los reenvía tal
     cual) y la API solo los acepta desde la subred `edge` (`ReverseProxy:KnownNetworks`). La API no cambia.
   - Sin proxy (desarrollo local) las cabeceras no existen y el comportamiento es el de antes.
4. **El proxy del host:** reenvía `Host` sin cambios, añade `Strict-Transport-Security: max-age=31536000` **sin**
   `includeSubDomains` ni `preload` (para no afectar a otros subdominios del dominio padre) y elimina la cabecera `Server`. No
   define CSP ni otras cabeceras: las fija nginx.
5. **La subred está escrita en la plantilla de nginx** (`172.28.10.0/24`, el valor por defecto de `EDGE_NETWORK_SUBNET`). Si se
   cambia esa variable, hay que cambiar también la plantilla.

### Comprobaciones del contrato
Hechas el 2026-10-24 con `nginxinc/nginx-unprivileged:stable-alpine` en Docker Desktop (macOS), con un servidor que devuelve las
cabeceras recibidas y una copia de la plantilla:

| Entrada | Resultado hacia la API |
|---|---|
| `X-Forwarded-For: 203.0.113.9`, `X-Forwarded-Proto: https` | `203.0.113.9` y `https`, también en la ruta de 16 MB |
| Sin cabeceras | IP de la puerta de enlace y `http` |
| `X-Forwarded-For: 1.2.3.4, 203.0.113.9` | `203.0.113.9` (se usa la última) |
| `X-Forwarded-Proto: javascript` | `http` |
| Conexión fuera del rango de `set_real_ip_from` | `X-Forwarded-For` ignorado |

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| TLS en el nginx del contenedor `frontend`, con certbot en el host | Sin componente nuevo; IP y esquema correctos sin cambios | Hay que añadir 443, el desafío ACME, la renovación y la recarga a la plantilla, publicar 80/443 desde el contenedor y montar el volumen de certificados: más cambios y más operación |
| Caddy o Traefik como contenedor del `compose` | Todo en Compose | Un segundo servicio publicaría puertos (exige enmendar §8.4); el proxy de usuario de Docker puede ocultar la IP real en IPv6 |
| nginx del host con certbot | Conocido por el equipo | Dos nginx encadenados, renovación por *hook* y el mismo ajuste de `real_ip` |
| Balanceador o CDN del proveedor | Administrado, con alta disponibilidad | No disponible: solo hay un servidor. Si se adopta, el mecanismo de `real_ip` sería el mismo |
| Confiar en `X-Forwarded-*` de cualquier origen | Trivial | Cualquiera puede falsear su IP y el esquema |

## Consecuencias
- **Positivas:**
  - Certificados renovados automáticamente y un cambio mínimo en nginx.
  - El frontend sigue en loopback y la API no cambia.
  - La IP del cliente la ve un proceso del host, no el proxy de usuario de Docker.
  - La misma plantilla vale con y sin proxy.
- **Negativas / riesgos:**
  - **Un componente fuera de Compose.** `docker compose up --build` no lo levanta. Este ADR lee §8.1 como aplicable a los
    componentes de la aplicación y trata el proxy como infraestructura de red, igual que un balanceador. Si `security` o
    `architecture` no comparten esa lectura, hace falta una enmienda de §8.1.
  - **`X-Forwarded-Proto` no se limita por origen.** Es seguro mientras el frontend solo escuche en `127.0.0.1`. Si se publicara
    en otra interfaz, un cliente podría falsear el esquema, y con el proxy de usuario de Docker también la IP.
  - **Rango fijo.** Si no coincide con la red `edge`, la IP registrada pasa a ser la de la puerta de enlace: falla de forma segura,
    pero degrada ADR-0011 y agrupa los límites por IP.
  - **Sin alta disponibilidad:** el proxy y el servidor son un punto único de fallo.
  - **Let's Encrypt (HTTP-01)** exige que 80 y 443 sean alcanzables desde Internet; si no, hay que usar DNS-01.
  - **Lo comprobado no es el servidor real.** La IP de origen que ve nginx en Linux no está verificada: la prueba fue en Docker
    Desktop. Y el proxy sobrescribe `X-Forwarded-For` de los clientes no confiables por defecto, pero eso no se probó aquí.
- **Seguimiento:**
  - Tras el primer despliegue, comprobar en **Inventario** que la IP de conexión es la del cliente y repetir con `curl` desde
    fuera con un `X-Forwarded-For` falsificado (SPEC-0002 CA-20): debe ignorarse.
  - Pendiente de una spec de despliegue: copias de seguridad, endurecimiento del servidor, automatización y la desviación del
    checklist de SECURITY.md §8 sobre PostgreSQL (con `SslMode=VerifyFull` pensado para un servidor administrado; aquí es un
    contenedor en la red interna).
  - Valorar parametrizar el rango de `set_real_ip_from` con `EDGE_NETWORK_SUBNET`.

## Impacto en la constitución
Ninguno si se acepta la lectura de §8.1 indicada arriba. §8.4 se cumple: solo el frontend publica un puerto, en `127.0.0.1`. No
cambian §3 ni §4: el proxy no guarda secretos de la aplicación.
