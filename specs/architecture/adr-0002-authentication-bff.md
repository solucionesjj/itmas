---
id: ADR-0002
title: Autenticación BFF con Microsoft Entra ID
status: Accepted
date: 2026-10-07
deciders: [architecture, security]
related: [SPEC-0000, ADR-0007]
---

# ADR-0002 — Autenticación Backend-for-Frontend con Microsoft Entra ID

## Contexto
Se requiere autenticar con Microsoft Entra ID una SPA Angular + API ASP.NET Core, sin secretos ni tokens persistentes
en el frontend, con validación y autorización en el backend y manejo correcto de la expiración.

## Decisión
Usamos el patrón **Backend-for-Frontend**:
- La API es un **cliente confidencial** OIDC (Microsoft.Identity.Web): *authorization code* + PKCE (S256),
  `response_mode=form_post`, credencial de cliente en el servidor.
- Tras validar el *id token*, la API emite una cookie de sesión `__Host-itmas3-session`
  (`HttpOnly; Secure; SameSite=Strict`), cifrada con Data Protection (claves persistidas en PostgreSQL).
- No se guardan tokens (`SaveTokens=false`). Si una spec futura necesita llamar a APIs aguas abajo (p. ej. Graph),
  se adquirirán en el servidor (token cache de Microsoft.Identity.Web), nunca en el navegador. *Ampliado por [ADR-0016](adr-0016-outlook-graph-application-permissions.md): la agenda llama a Graph con permisos de **aplicación** acotados a un buzón, no con tokens de las personas.*
- La SPA usa `/auth/session`, `/auth/login` (navegación) y `/auth/logout` (form POST). No contiene configuración de Entra ID.
- El esquema de *challenge* por defecto es la cookie, que responde `401`/`403`; solo `/auth/login` inicia OIDC.
- Expiración: inactividad deslizante (60 min) + absoluta (8 h); `401` → la SPA redirige a `/login?reason=expired`.
- CSRF: SameSite=Strict + cabecera `X-CSRF` en `/api` + validación de `Origin`.
- Fuera de `Development` la API no arranca sin configuración completa de Entra ID.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| MSAL Angular (SPA pública) + bearer tokens | Patrón muy difundido; API sin estado | Tokens accesibles a JavaScript (XSS); refresh tokens en el navegador; configuración de Entra ID en el frontend |
| MSAL Angular + BFF híbrido | — | Dos superficies de autenticación |
| Cookie + antiforgery token de ASP.NET Core | Estándar en MVC | Más piezas; la cabecera personalizada + SameSite=Strict + Origin cubre el mismo riesgo para una SPA same-origin |
| Duende BFF | Implementación completa del patrón | Licencia comercial; dependencia adicional |

Referencia: IETF *OAuth 2.0 for Browser-Based Applications* recomienda el BFF como opción más segura.

## Consecuencias
- Positivas: XSS no puede exfiltrar tokens; sin secretos en el frontend; revocación centralizada.
- Negativas: la API mantiene estado de sesión (cookie cifrada) y requiere key ring compartido entre réplicas
  (resuelto con PostgreSQL); el frontend y la API deben compartir origen (resuelto con el proxy, ADR-0007).
- Seguimiento: credencial por certificado o identidad administrada en producción; cifrado del key ring.

## Impacto en la constitución
Recogido en §3.2 y §4.4.
