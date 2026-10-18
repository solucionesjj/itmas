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
| Activar los hooks de git (una vez por clon) | `./scripts/install-git-hooks.sh` |
| Levantar todo | `docker compose up --build` |
| Pruebas backend | `cd backend && dotnet test` (requiere Docker) |
| Pruebas frontend | `cd frontend && npm test -- --watch=false` |
| Build frontend | `cd frontend && npm run build` |
| Pruebas del agente (unitarias) | `cd agent && dotnet test tests/Itmas3.Agent.Tests` |
| Pruebas del agente con motores SQL reales | `cd agent && dotnet test tests/Itmas3.Agent.IntegrationTests` (requiere Docker) |
| Paquetes del agente | `./scripts/build-agent.sh` |
| Pruebas del hook de commit | `./scripts/test-commit-msg-hook.sh` |
| Validar Compose sin mostrar valores | `docker compose config -q` (sin `-q` imprime los valores de `.env`) |
| Nueva migración | ver [DEVELOPMENT.md §5](DEVELOPMENT.md#5-migraciones-ef-core) |

## Reglas que más se olvidan
- Todo endpoint declara autorización: `[AllowAnonymous]`, `[RequirePermission("recurso.accion")]`, la política `AuthenticatedUser`
  o, solo en endpoints de dispositivos, `DeviceEnrollment` / `EnrolledDevice` (ADR-0009).
  Todo permiso usado se declara en el catálogo (`IPermissionDefinitionProvider`). No se usan roles.
- Nunca secretos en el repositorio, en el frontend, en logs ni en la línea de comandos. Nunca leer ni modificar `.env`.
- Nada de lógica de negocio en controllers; nada de EF Core fuera de `Infrastructure`.
- Angular Material estándar: no sobrescribir tokens, colores ni tipografías.
- La documentación se actualiza en el mismo cambio. Commits con el ID de la spec (`SPEC-NNNN: …`); `minor: <motivo>`
  solo para las correcciones menores de §11.6. El hook `commit-msg` lo comprueba; actívalo con
  `./scripts/install-git-hooks.sh`.
- Si cambia algo que ve una persona usuaria, se actualiza el manual de uso (`frontend/public/manual/`, constitución §10.4):
  procesos, permisos y pasos. Es público: nada confidencial.

## Skills de proyecto (Claude Code, SPEC-0007)

Procedimientos en `.claude/skills/`; solo se ejecutan cuando una persona los invoca. Leen las reglas del repositorio en
cada ejecución y, ante cualquier conflicto, prevalecen la constitución y este archivo. Detalle y límites en
[DEVELOPMENT.md §10](DEVELOPMENT.md#10-skills-y-guardas-de-desarrollo-claude-code).

| Skill | Para qué |
|---|---|
| `/sdd-new-spec <idea>` | Crea una spec en `Draft` con su plantilla, el siguiente ID y los índices. |
| `/sdd-implement SPEC-NNNN` | Implementa una spec `Approved` según su plan, sin commit ni push. |
| `/sdd-validate SPEC-NNNN` | Ejecuta las comprobaciones, relaciona criterios con pruebas y señala lo no verificado. |
| `/update-manual SPEC-NNNN` | Actualiza el manual de uso según la sección "Manual de uso" de la spec. |
