---
id: ADR-0018
title: TLS en un proxy inverso del host delante del frontend (servidor Linux único)
status: Accepted
date: 2026-10-24
deciders: [architecture, security]
related: [ADR-0007, ADR-0011, ADR-0002, SPEC-0002, SPEC-0009, ADR-0019]
---

# ADR-0018 — TLS en un proxy inverso del host delante del frontend

> **Aceptado por el solicitante el 2026-10-25.** **Revisión de `security` hecha el 2026-10-25** por un revisor independiente: sin hallazgos críticos ni altos y
> con cambios, que se recogen aquí (marcados «Revisión 2026-10-25») y en SPEC-0009 (§19.5). **La lectura de §8.1 se formalizó con una enmienda de la constitución:**
> [ADR-0019](adr-0019-host-edge-operations-outside-compose.md), `Accepted` el 2026-10-26 (versión 3.0.0). `ADR-0007` y `ADR-0011` ya remiten a este documento y no hace
> falta tocarlos de nuevo.

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
   certificado, redirige HTTP a HTTPS y reenvía todo a `127.0.0.1:${FRONTEND_PORT}`. La implementación de referencia es **Caddy**;
   el contrato de abajo no depende de ella. **Revisión 2026-10-25:** el emisor es **solo Let's Encrypt** (Caddy usa por defecto también ZeroSSL
   como respaldo, y una falla de Let's Encrypt emitiría de forma silenciosa con otra CA): se fijan el emisor y un correo de contacto.
2. **El frontend sigue siendo el único origen y el único puerto publicado del sistema, solo en loopback** (§8.4). Ningún otro
   servicio del stack publica puertos. Publicar el frontend en otra interfaz invalida esta decisión (ver Consecuencias).
3. **nginx confía en el proxy solo cuando la conexión llega desde la red `edge`**, donde el proxy aparece como la puerta de
   enlace de Docker:
   - `set_real_ip_from <subred de edge>` y `real_ip_header X-Forwarded-For`, con `real_ip_recursive` desactivado: se toma la
     **última** dirección de la cabecera, la que añadió el proxy.
   - `X-Forwarded-Proto` hacia la API es el del proxy **solo si vale exactamente `https`**; en cualquier otro caso es `$scheme`.
   - nginx sigue reconstruyendo `X-Forwarded-For`, `X-Forwarded-Proto` y `X-Forwarded-Host` hacia la API (nunca los reenvía tal
     cual) y la API solo los acepta desde la subred `edge` (`ReverseProxy:KnownNetworks`). La API no cambia.
   - Sin proxy, y mientras ningún cliente envíe esas cabeceras, el comportamiento es el de antes.
   - **Modelo de confianza real (revisión 2026-10-25).** Con el puerto publicado solo en loopback, la conexión de Caddy y la de **cualquier otro proceso del
     servidor con acceso a `127.0.0.1`** llegan a nginx con el mismo origen (la puerta de enlace de `edge`), y los contenedores de `edge` caen dentro del rango. nginx
     confía en `X-Forwarded-*` de todos ellos: un proceso local puede fijar su IP y el esquema. Se acepta como límite: quien ejecuta código en el servidor ya puede más que eso.
   - **Endurecimiento implementado** (SPEC-0009, RF-28, 2026-10-26): (a) `real_ip_recursive off` declarado explícitamente, no por defecto; (b) `X-Forwarded-Proto`
     solo se acepta si la conexión (`$realip_remote_addr`) está dentro del rango de confianza, no de cualquier origen; (c) `set_real_ip_from` se parametriza con `envsubst` (`EDGE_TRUSTED_PROXY_FROM`; por defecto, toda la subred de `edge`, `172.28.10.0/24`) y el `gateway` de `edge` queda fijado en el
     `ipam` de Compose (`EDGE_NETWORK_GATEWAY`); **se estrecha a la puerta de enlace (`/32`) solo después de comprobar en Linux** qué origen ve nginx (Seguimiento, SPEC-0009 CA-22);
     (d) `scripts/test-nginx-realip.sh` ejecuta la plantilla real y la tabla de abajo, más los casos de origen y de esquema.
4. **El proxy del host:** reenvía `Host` sin cambios, **elimina las cabeceras `Server` y `Via`** (`Via: 1.1 Caddy` la añade el propio proxy) y añade
   `Strict-Transport-Security` **sin** `includeSubDomains` ni `preload` (para no afectar a otros subdominios del dominio padre). **Revisión 2026-10-25:** el `max-age` es
   **escalonado**: 300 s al publicar, 86 400 s cuando el sitio esté confirmado y 31 536 000 s tras una renovación real del certificado (una renovación fallida con un HSTS largo bloquea el sitio, sin
   posibilidad de omitir el aviso). Sirve solo HTTP/1.1 y HTTP/2 (sin HTTP/3: solo se abren 80/tcp y 443/tcp). Su API de administración **no** escucha en `localhost:2019`: va por un
   socket Unix de acceso restringido o desactivada. No define CSP ni otras cabeceras: las fija nginx.
5. **El rango de confianza de nginx es una variable** (`EDGE_TRUSTED_PROXY_FROM`, por defecto la de `EDGE_NETWORK_SUBNET`, y `172.28.10.0/24` si la imagen se ejecuta sin Compose). Si se cambia la subred de `edge`, el rango la sigue sin tocar la plantilla (3c).
6. **Requisitos del host (revisión 2026-10-25):**
   - Docker Engine **28.0 o posterior**: según su documentación, antes de esa versión los equipos del mismo segmento L2 podían alcanzar puertos publicados en loopback. SPEC-0009 fija la versión
     concreta tras revisar los avisos de seguridad vigentes. No se usa `DOCKER_INSECURE_NO_IPTABLES_RAW`.
   - Caddy viene de un repositorio de terceros que `unattended-upgrades` no actualiza por defecto: se actualiza a mano, de forma periódica y con aviso, y la huella de su clave GPG se comprueba al instalarlo.
   - **Sin registro `AAAA`** salvo decisión expresa de `security`: con IPv6 los límites por IP particionan por /128 y un /64 permite rotar direcciones. Un registro CAA acota quién puede emitir el certificado.

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

**Alcance real de la prueba (revisión 2026-10-25):** se hizo con un servidor de eco, no con la API ni con Caddy, y entonces ninguna prueba automatizada del repositorio cubría la plantilla ni el rango
`172.28.10.0/24` (las de la API usan `127.0.0.1`). Desde SPEC-0009 (2026-10-26) `scripts/test-nginx-realip.sh` ejecuta la plantilla real de nginx con esta tabla y los casos de origen y de esquema; sigue sin cubrir a Caddy ni a la API,
por lo que la comprobación en la pila real queda en el Seguimiento.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| TLS en el nginx del contenedor `frontend`, con certbot en el host | Sin componente nuevo; IP y esquema correctos sin cambios | Hay que añadir 443, el desafío ACME, la renovación y la recarga a la plantilla, publicar 80/443 desde el contenedor y montar el volumen de certificados: más cambios y más operación |
| Caddy o Traefik como contenedor del `compose` | Todo en Compose | Un segundo servicio publicaría puertos (exige enmendar §8.4); el proxy de usuario de Docker puede ocultar la IP real en IPv6 |
| nginx del host con certbot | Conocido por el equipo | Dos nginx encadenados, renovación por *hook* y el mismo ajuste de `real_ip` |
| Balanceador o CDN del proveedor | Administrado, con alta disponibilidad, y retira la clave TLS del servidor | Fuera del alcance decidido (SPEC-0009: un solo servidor, sin balanceador ni servicios administrados; en AWS existirían ALB o CloudFront con ACM). Si se adopta, cambiarían los rangos de confianza y habría que valorar `real_ip_recursive on` con los rangos del CDN |
| Confiar en `X-Forwarded-*` de cualquier origen | Trivial | Cualquiera puede falsear su IP y el esquema |

## Consecuencias
- **Positivas:**
  - Certificados renovados automáticamente y un cambio mínimo en nginx.
  - El frontend sigue en loopback y la API no cambia.
  - La IP del cliente la ve un proceso del host, no el proxy de usuario de Docker.
  - La misma plantilla vale con y sin proxy.
- **Negativas / riesgos:**
  - **Un componente fuera de Compose.** `docker compose up --build` no lo levanta. Este ADR lo lee como infraestructura de red, igual que un balanceador, y no como
    componente de la aplicación. La constitución dice «componentes de **servidor**» (§8) y «todo el **sistema de servidor** DEBE poder levantarse con `docker compose up --build`» (§8.1).
    **Revisión 2026-10-25:** `security` considera que Caddy no equivale a un balanceador administrado (es software instalado y operado en el mismo host, expuesto a Internet, que custodia la
    clave privada TLS y queda fuera de §8.2 y §8.5) y que, por §1.3, esa lectura exige una **enmienda formal**. **Resuelto el 2026-10-26:** ADR-0019 enmienda la constitución (3.0.0) y esta decisión pasa a regirse por §8.7.
  - **El proxy custodia claves** (la privada TLS y la de la cuenta ACME, en `/var/lib/caddy`): acceso solo para su usuario, y ningún procedimiento de copia las incluye en claro.
  - **Confianza amplia en `X-Forwarded-*`** (ver 3): es de cualquier origen en la red `edge`, y `X-Forwarded-Proto` no se limita por origen hasta aplicar 3b. Si el frontend se publicara en otra interfaz
    o el Docker fuera anterior a 28.0, un cliente podría falsear el esquema y la IP.
  - **Rango fijo.** Si no coincide con la red `edge`, la IP registrada pasa a ser la de la puerta de enlace. Eso degrada ADR-0011 y **agrupa los límites por IP**: un anónimo puede agotar los 20/min de
    `/auth/login` y los 300/min anónimos para todos. No es un fallo «seguro» frente a denegación.
  - **IPv6.** Con un registro `AAAA`, cada cliente IPv6 tiene su propia partición por /128 y un /64 permite rotar direcciones para eludir los límites (decisión 6).
  - **Sin alta disponibilidad:** el proxy y el servidor son un punto único de fallo.
  - **Let's Encrypt (HTTP-01)** exige que el puerto 80 sea alcanzable desde Internet (el 443 sirve la aplicación); si no, hay que usar DNS-01.
  - **HSTS:** una renovación fallida con `max-age` largo bloquea el sitio hasta un año (decisión 4: escalonado).
  - **Lo comprobado no es el servidor real.** La IP de origen que ve nginx en Linux no está verificada: la prueba fue en Docker Desktop. Y el proxy sobrescribe `X-Forwarded-For` de los
    clientes no confiables por defecto (documentación de Caddy), pero eso no se probó aquí.
- **Seguimiento:**
  - **Comprobaciones en el servidor real**, antes de abrir el DNS o de enrolar equipos (SPEC-0009, CA-20 y CA-22 a CA-27):
    1. Origen que ve nginx: `curl -s http://127.0.0.1:8080/` y `docker compose logs --tail=3 frontend`; el primer campo debe ser la puerta de enlace de `edge`. Decide si se puede estrechar 3c.
    2. Suplantación a través del proxy con la API real: `POST https://<host>/api/v1/device-enrollments` con `Authorization: Enrollment <valor inválido>` y `X-Forwarded-For: 1.2.3.4`; debe dar 401 y el log de
       `api` («Device enrollment rejected … from …») debe traer la IP real del cliente, no `1.2.3.4`. Sustituye al `curl` a `/auth/session`, que no registra la IP.
    3. Contraste del modelo de confianza: un `X-Forwarded-For` falso contra `127.0.0.1:${FRONTEND_PORT}` desde el propio servidor se verá como falso: el loopback es de confianza total.
    4. Cabeceras de salida: un solo `Strict-Transport-Security`, ni `Server` ni `Via`, sin `Alt-Svc`. Emisor del certificado: Let's Encrypt.
    5. `ss -ltnp` y `ss -lunp`: solo 80 y 443 hacia fuera, 8080 en `127.0.0.1`, y la administración de Caddy fuera de TCP; con un escaneo externo.
    6. Rutas con `curl --path-as-is` (`/%68ealth`, `//health`, `/./health`, `/api/..%2fhealth`): ninguna debe devolver 200 de la API.
    7. Con la configuración de `AllowedHosts` de producción, `api` queda `healthy` (SPEC-0009, RF-26).
  - Pendiente de una spec de despliegue: copias de seguridad, endurecimiento del servidor, automatización y la desviación del
    checklist de SECURITY.md §8 sobre PostgreSQL (con `SslMode=VerifyFull` pensado para un servidor administrado; aquí es un
    contenedor en la red interna). **SPEC-0009 lo cubre.**
  - Parametrizar el rango de `set_real_ip_from` (3c): **hecho** en SPEC-0009 (RF-28). Falta estrecharlo a la puerta de enlace tras comprobar el origen en Linux (CA-22).

## Impacto en la constitución
**Enmendada por [ADR-0019](adr-0019-host-edge-operations-outside-compose.md) (`Accepted`, constitución 3.0.0, 2026-10-26).** La revisión de `security` (2026-10-25) concluyó que la lectura de §8.1 de este ADR (infraestructura de red) exigía una enmienda de §8, que se hizo: el proxy TLS del host es un componente
que admite el nuevo §8.7, que este ADR enumera con sus puertos (80 y 443) y revisión de `security`. §8.4 se cumple: solo el frontend publica un puerto con Docker, en `127.0.0.1` (§8.7 b). No cambian §3 ni §4: el proxy no guarda secretos **de la aplicación** (sí guarda su propia clave TLS, ver Consecuencias).
La confianza en `X-Forwarded-*` desde el loopback es un riesgo residual conocido y consta como límite aceptado (decisión 3; §8.7 c). Los controles de las decisiones 4 y 6 responden a §8.7 d.
