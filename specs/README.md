# Especificaciones (SDD)

Toda evolución del sistema se deriva de una especificación. Este directorio es la **fuente de verdad** de
*qué* se construye y *por qué*; el código es la consecuencia. Reglas: [constitution.md §11](../constitution.md).

## Flujo

```text
Idea → Specification → Architecture Analysis → Implementation Plan → Implementation → Tests → Validation → Documentation
```

| Paso | Resultado | Responsable (ver `agents/`) |
|---|---|---|
| Idea | Problema descrito por negocio | Persona solicitante |
| Specification | `SPEC-NNNN-*.md` en estado `Draft` con objetivo, reglas, criterios de aceptación | Autor + `architecture` |
| Architecture Analysis | Sección "Análisis arquitectónico" completa; ADR si hay decisión transversal | `architecture` (+ `security`, `database`) |
| Implementation Plan | Sección "Plan de implementación": tareas por agente y archivos afectados | `architecture` |
| Implementation | Código en las capas indicadas | `backend`, `frontend`, `database` |
| Tests | Pruebas que cubren cada criterio de aceptación | `testing` + implementadores |
| Validation | Suite verde, `docker compose up` sano, revisión de seguridad | `testing`, `security` |
| Documentation | Docs actualizadas, spec → `Implemented` con trazabilidad | `architecture` |

La spec pasa a `Approved` solo con aprobación humana explícita. Nada se implementa en estado `Draft`.

## Estructura

```text
specs/
├── _templates/          Plantillas obligatorias
├── architecture/        SPEC-0000 (fundación) y ADRs (decisiones arquitectónicas)
├── features/            Specs funcionales: SPEC-NNNN-<slug>.md
├── database/            Specs de cambios de esquema/roles que no nacen de una feature
└── security/            Specs y revisiones de seguridad, modelo de amenazas
```

## Identificadores y nombres

| Tipo | ID | Archivo | Plantilla |
|---|---|---|---|
| Especificación (feature, BD, seguridad) | `SPEC-NNNN` (secuencia única global) | `<carpeta>/SPEC-NNNN-<slug>.md` | `feature-spec.md`, `database-change.md`, `security-review.md` |
| Decisión arquitectónica | `ADR-NNNN` | `architecture/adr-NNNN-<slug>.md` | `adr.md` |

- Toma el siguiente número libre (`grep -rho "SPEC-[0-9]\{4\}" specs | sort | tail -1`).
- Commits y PRs referencian el ID: `SPEC-0007: add invoice approval endpoint`.

## Estados

`Draft` → `Approved` → `In Progress` → `Implemented`; alternativas: `Rejected`, `Superseded by SPEC-NNNN`.

## Índice

| ID | Título | Estado |
|---|---|---|
| [SPEC-0000](architecture/SPEC-0000-application-foundation.md) | Application Foundation | Implemented |
| [SPEC-0001](features/SPEC-0001-policy-based-authorization.md) | Sistema de autorización basado en políticas | Implemented |
| [ADR-0001](architecture/adr-0001-clean-architecture.md) | Clean Architecture en 4 proyectos | Accepted |
| [ADR-0002](architecture/adr-0002-authentication-bff.md) | Autenticación BFF con Microsoft Entra ID | Accepted |
| [ADR-0003](architecture/adr-0003-authorization-default-deny.md) | Autorización *default deny* basada en permisos | Accepted |
| [ADR-0004](architecture/adr-0004-database-roles-and-migrations.md) | Roles de BD de mínimo privilegio y migrador dedicado | Accepted |
| [ADR-0005](architecture/adr-0005-frontend-material3-theming.md) | Frontend Angular + Material 3 sin sistema de diseño propio | Accepted |
| [ADR-0006](architecture/adr-0006-observability-and-errors.md) | Logging estructurado, OpenTelemetry opcional y Problem Details | Accepted |
| [ADR-0007](architecture/adr-0007-container-topology.md) | Topología de contenedores con proxy de mismo origen | Accepted |
| [ADR-0008](architecture/adr-0008-policy-based-authorization.md) | Modelo de autorización basado en políticas | Accepted |
