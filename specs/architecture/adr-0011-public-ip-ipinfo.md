---
id: ADR-0011
title: IP pública y ubicación con ipinfo.io desde el agente; IP de conexión en el servidor
status: Accepted
date: 2026-10-08
deciders: [architecture, security]
related: [SPEC-0002, ADR-0010, ADR-0007]
---

# ADR-0011 — IP pública y ubicación

## Contexto
SPEC-0002 pide la IP pública con la que el equipo sale a Internet y su ubicación (país y ciudad). El solicitante eligió
ipinfo.io, que se usa de forma libre y sin token, y quiere distinguir dos IPs:

- La **IP pública declarada**, que obtiene el agente.
- La **IP de conexión**, la IP de origen de la petición HTTP tal como la ve el servidor.

Por ahora el servidor está expuesto directamente a Internet, con nginx como borde y un certificado de Let's Encrypt.
Más adelante estará detrás de un balanceador. La constitución §9.4 prohíbe que las pruebas dependan de Internet, y
§2.5 exige justificar una dependencia de impacto transversal.

## Decisión
1. **IP pública declarada (agente).**
   - En cada recopilación el agente hace `GET https://ipinfo.io/json`, sin token y sin enviar ningún dato del equipo.
   - Usa el proxy del sistema, valida TLS y tiene un tiempo máximo de 10 s.
   - Solo extrae `ip`, `country`, `region`, `city` y `org`, y valida el formato y la longitud de cada uno. La respuesta
     es de un tercero y no se considera confiable.
   - Si falla, `network.publicNetwork` se envía como `null` y la sección figura en `unavailable`. No se reintenta dentro
     de la misma recopilación.
2. **IP de conexión (servidor).**
   - La API guarda con cada inventario `HttpContext.Connection.RemoteIpAddress`, después de `UseForwardedHeaders`.
   - nginx sobrescribe `X-Forwarded-For` con `$remote_addr` y la API solo confía en esa cabecera desde la red del proxy
     (`ReverseProxy:KnownNetworks`), así que un cliente no puede falsearla.
   - Si TLS se termina en otro proxy delante de nginx, o cuando llegue el balanceador, el nginx del contenedor tendrá que
     usar `set_real_ip_from` y `real_ip_header` con la red de ese componente. Está en el checklist de producción de
     SECURITY.md.
3. El **servidor no llama a ipinfo.io** ni a ningún otro servicio externo para el inventario.
4. **Pruebas:** el cliente de ipinfo del agente se sustituye por un `HttpMessageHandler` de prueba. Las pruebas del
   servidor no necesitan red.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Geolocalizar en el servidor la IP de conexión (ipinfo o base local) | El dispositivo no puede falsear la ubicación | Descartada por el solicitante; añade una dependencia externa a la API |
| Base de datos GeoIP local (DB-IP, MaxMind) | Sin llamadas externas | Licencias, atribución y actualización mensual; descartada por el solicitante |
| Usar solo la IP de conexión | Un único dato fiable | Pierde la ubicación pedida, y con un proxy o una VPN no refleja la salida real del equipo |

## Consecuencias
- **Positivas:**
  - Ningún secreto que distribuir.
  - La API no depende de terceros.
  - Tener dos IPs permite detectar proxies, VPN o datos manipulados.
- **Negativas / riesgos:**
  - La IP pública declarada y la ubicación son datos del dispositivo y se pueden falsear. Se contrastan con la IP de conexión.
  - ipinfo.io ve la IP de salida de cada equipo una vez al día; se informa en los términos.
  - El uso anónimo tiene un límite de frecuencia por IP de origen. Con una consulta diaria por equipo no se alcanza.
  - Si un firewall bloquea ipinfo.io, la sección queda como no disponible.
- **Seguimiento:** si se contrata un plan, el token no debe distribuirse a los equipos: habría que mover la consulta al servidor.

## Impacto en la constitución
Ninguno. Es una dependencia externa justificada (§2.5), §9.4 se cumple con dobles de prueba y no hay secretos (§4).
