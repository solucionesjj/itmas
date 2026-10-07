---
id: ADR-0007
title: Topología de contenedores con proxy de mismo origen
status: Accepted
date: 2026-10-07
deciders: [architecture, security]
related: [SPEC-0000, ADR-0002]
---

# ADR-0007 — Topología de contenedores con proxy de mismo origen

## Contexto
Contenedores independientes para frontend, backend y PostgreSQL; arranque con un comando; health checks; red privada;
BD no expuesta. El BFF (ADR-0002) necesita que la SPA y la API compartan origen.

## Decisión
- `frontend` (nginx *unprivileged*) sirve la SPA y hace proxy de `/api`, `/auth`, `/signin-oidc` y
  `/signout-callback-oidc` hacia `api`. Es el **único puerto publicado** (en `127.0.0.1`).
- Redes: `edge` (frontend ↔ api, con salida para Entra ID; subred fija confiable para `X-Forwarded-*`) y
  `data` (`internal: true`: api/migrator ↔ postgres, sin salida a Internet).
- nginx sobrescribe `X-Forwarded-For/Proto/Host`; la API solo los acepta desde la subred `edge`.
- `/health*` no se expone por el proxy (404); los health checks se ejecutan dentro de los contenedores.
- Contenedores no root, `cap_drop: ALL`, `no-new-privileges`, `read_only` en api/migrator.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| API publicada + CORS con credenciales | Despliegue independiente | Cookies cross-site, CORS con credenciales, más superficie |
| Proxy dedicado (Traefik/YARP) | Funciones avanzadas | Un servicio más; nginx ya sirve la SPA |
| Servir la SPA desde ASP.NET Core | Un contenedor menos | Viola "contenedores independientes"; acopla despliegues |

## Consecuencias
- Positivas: cookies first-party, CORS cerrado, superficie pública mínima.
- Negativas: nginx debe conocer las rutas del BFF (lista explícita en `default.conf.template`).
- Seguimiento: en producción, TLS en el borde (ingress) delante de `frontend` o en el propio nginx.

## Impacto en la constitución
Recogido en §8.
