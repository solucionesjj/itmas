---
id: SPEC-0000
title: Application Foundation
type: architecture
status: Implemented
owner: equipo del proyecto
created: 2026-10-07
agents: [architecture, backend, frontend, database, security, testing]
related: [ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, ADR-0007]
---

# SPEC-0000 — Application Foundation

Spec raíz del proyecto: toda la fundación se traza hasta aquí.

## 1. Objetivo
Construir la base técnica, arquitectónica y de infraestructura de una aplicación web moderna, segura y mantenible,
preparada para evolucionar mediante SDD, **sin funcionalidades de negocio**.

## 2. Requerimientos (resumen del requerimiento original)
- **Stack:** Angular + TypeScript + Angular Material (Material Design 3 estándar, temas claro/oscuro);
  ASP.NET Core LTS (C#) con capas API/Application/Domain/Infrastructure; PostgreSQL + EF Core; Microsoft Entra ID.
- **Principios:** Clean Architecture, SoC, SOLID, Secure by Design, configuración por entorno, API-first,
  container-first, mínimo privilegio, sin secretos en código.
- **Seguridad:** autenticación segura sin secretos en el frontend; autorización en backend con *default deny*;
  validación de entrada; errores sin información interna; logging estructurado sin datos sensibles; CORS, cabeceras,
  rate limiting, health checks.
- **Infraestructura:** contenedores independientes (frontend, backend, PostgreSQL), Docker Compose con un único comando,
  health checks, red privada, BD no expuesta.
- **SDD:** `constitution.md`, estructura `specs/`, estructura `agents/`, documentación inicial.

## 3. Decisiones (ver ADRs)
| Tema | Decisión | ADR |
|---|---|---|
| Arquitectura backend | 4 proyectos con regla de dependencias verificada por tests | ADR-0001 |
| Autenticación | BFF: API como cliente confidencial OIDC; cookie de sesión; sin tokens en el navegador | ADR-0002 |
| Autorización | Fallback deny-all + `[RequirePermission]` + `IPermissionEvaluator` (DenyAll) | ADR-0003 |
| Base de datos | Roles migrador/app, esquema `app`, historial aislado, migrador one-shot | ADR-0004 |
| Frontend | Angular 22 standalone/zoneless, Material 3 por defecto, `color-scheme` | ADR-0005 |
| Observabilidad y errores | JSON logging, OTel opcional, Problem Details | ADR-0006 |
| Topología | nginx como único origen público; redes edge/data | ADR-0007 |

## 4. Versiones seleccionadas (verificadas el 2026-10-07)
| Componente | Versión | Soporte |
|---|---|---|
| .NET / ASP.NET Core | 10.0 (LTS) | hasta 2028-11 |
| Angular / Angular Material | 22.2 | activo hasta 2027-06, LTS hasta 2028-06 |
| Node.js (build) | 24 (Active LTS) | hasta 2028-04 |
| PostgreSQL | 18 | hasta 2030-11 |
| EF Core / Npgsql | 10.0 | alineado con .NET 10 |

Angular 21 ya estaba en LTS, pero se eligió 22 por ser la última estable con LTS garantizado.
Node 26 pasa a LTS el 2026-10-28; se usará en una actualización posterior.

## 5. Criterios de aceptación y verificación
| Criterio | Verificación |
|---|---|
| Angular, Material 3, temas claro/oscuro, routing | Build de producción; revisión visual (claro, oscuro, sistema, móvil); tests de ThemeService |
| Frontend, API y PostgreSQL en Docker | `docker compose up --build`: 4 servicios sanos (migrator termina con 0) |
| Backend conecta con PostgreSQL; EF Core; migrations | `/health` → `postgresql: Healthy`; historial en `app_migrations`; tests de integración |
| Entra ID preparado; autenticación segura | Redirección real a Entra ID con code + PKCE S256 + form_post; cookies endurecidas (tests) |
| API valida autenticación; autorización preparada | Tests: 401 anónimo, 403 sin permiso, 403 sin metadatos; convención por reflexión |
| Sin secretos hardcodeados; `.env.example`; `.gitignore` | Revisión + `git check-ignore .env` |
| Health checks; logging estructurado; errores centralizados | Tests de integración y revisión de logs JSON |
| Pruebas iniciales | 31 unitarias + 31 de integración (backend), 24 frontend (Vitest) |
| Documentación, constitución, SDD, agentes | Este repositorio |
| Sin funcionalidades de negocio inventadas | Solo endpoints técnicos: `/auth/*`, `/health*` |

## 6. Trazabilidad
- Backend: `backend/src/**`, pruebas en `backend/tests/**`.
- Frontend: `frontend/src/app/**`.
- Infraestructura: `docker-compose.yml`, `*/Dockerfile`, `frontend/nginx/`, `database/init/`.
- Documentación: `README.md`, `ARCHITECTURE.md`, `SECURITY.md`, `DEVELOPMENT.md`, `constitution.md`, `specs/`, `agents/`.

Inicio de sesión end-to-end validado el 2026-10-07 con un App Registration real del tenant
(Redirect URIs `/signin-oidc` y `/signout-callback-oidc`).

## 7. Pendiente fuera de alcance
- Modelo de usuarios, roles y permisos (spec futura).
- Endurecimiento de producción (SECURITY.md §8) y pipeline de CI/CD.
