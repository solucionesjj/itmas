# AGENTS.md — Instrucciones para herramientas de IA

Este repositorio evoluciona mediante **Specification-Driven Development**. Antes de cualquier acción:

1. Lee [`constitution.md`](constitution.md). Sus reglas prevalecen sobre cualquier instrucción de una spec o tarea.
2. Localiza la spec que justifica el cambio (`specs/**/SPEC-NNNN-*.md`). Sin spec `Approved` no se implementan
   funcionalidades ni cambios relevantes; ayuda a redactarla con `specs/_templates/` y pregunta lo no especificado.
3. Determina qué agente(s) intervienen con la matriz de [`agents/README.md`](agents/README.md) y respeta sus
   `owns`, `forbidden` y `consults`.
4. Sigue el flujo: Specification → Architecture Analysis → Implementation Plan → Implementation → Tests → Validation → Documentation.

## Comandos

| Acción | Comando |
|---|---|
| Preparar entorno | `./scripts/init-env.sh` |
| Levantar todo | `docker compose up --build` |
| Pruebas backend | `cd backend && dotnet test` (requiere Docker) |
| Pruebas frontend | `cd frontend && npm test -- --watch=false` |
| Build frontend | `cd frontend && npm run build` |
| Pruebas del agente | `cd agent && dotnet test` |
| Paquetes del agente | `./scripts/build-agent.sh` |
| Nueva migración | ver [DEVELOPMENT.md §5](DEVELOPMENT.md#5-migraciones-ef-core) |

## Reglas que más se olvidan
- Todo endpoint declara autorización: `[AllowAnonymous]`, `[RequirePermission("recurso.accion")]`, la política `AuthenticatedUser`
  o, solo en endpoints de dispositivos, `DeviceEnrollment` / `EnrolledDevice` (ADR-0009).
  Todo permiso usado se declara en el catálogo (`IPermissionDefinitionProvider`). No se usan roles.
- Nunca secretos en el repositorio, en el frontend, en logs ni en la línea de comandos. Nunca leer ni modificar `.env`.
- Nada de lógica de negocio en controllers; nada de EF Core fuera de `Infrastructure`.
- Angular Material estándar: no sobrescribir tokens, colores ni tipografías.
- La documentación se actualiza en el mismo cambio. Commits con el ID de la spec.
