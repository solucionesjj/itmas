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
| Documentation | Docs y **manual de uso** actualizados (constitución §10.4), spec → `Implemented` con trazabilidad | `architecture` |

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

- Toma el siguiente número libre a partir de los **nombres de archivo**, no del texto, que también menciona IDs de
  ejemplo (`ls specs/*/SPEC-*.md | grep -o 'SPEC-[0-9]\{4\}' | sort | tail -1`; la skill `/sdd-new-spec` lo hace).
- Commits y PRs referencian el ID: `SPEC-0007: add invoice approval endpoint`.

## Estados

`Draft` → `Approved` → `In Progress` → `Implemented`; alternativas: `Rejected`, `Superseded by SPEC-NNNN`.

## Índice

| ID | Título | Estado |
|---|---|---|
| [SPEC-0000](architecture/SPEC-0000-application-foundation.md) | Application Foundation | Implemented |
| [SPEC-0001](features/SPEC-0001-policy-based-authorization.md) | Sistema de autorización basado en políticas | Implemented |
| [SPEC-0002](features/SPEC-0002-os-inventory-agent.md) | Agente de inventario de sistema operativo | Implemented |
| [SPEC-0003](features/SPEC-0003-device-registry-and-inventory-viewer.md) | Registro de equipos e inventario recolectado | Implemented |
| [SPEC-0004](features/SPEC-0004-user-manual.md) | Manual de uso del sistema | Implemented |
| [SPEC-0005](features/SPEC-0005-sql-engine-access-audit.md) | Auditoría de accesos a motores SQL | Implemented |
| [SPEC-0006](features/SPEC-0006-infrastructure-agenda.md) | Agenda anual de infraestructura | Approved |
| [SPEC-0007](features/SPEC-0007-sdd-tooling.md) | Skills y guardas para el flujo SDD | Implemented |
| [ADR-0001](architecture/adr-0001-clean-architecture.md) | Clean Architecture en 4 proyectos | Accepted |
| [ADR-0002](architecture/adr-0002-authentication-bff.md) | Autenticación BFF con Microsoft Entra ID | Accepted |
| [ADR-0003](architecture/adr-0003-authorization-default-deny.md) | Autorización *default deny* basada en permisos | Accepted |
| [ADR-0004](architecture/adr-0004-database-roles-and-migrations.md) | Roles de BD de mínimo privilegio y migrador dedicado | Accepted |
| [ADR-0005](architecture/adr-0005-frontend-material3-theming.md) | Frontend Angular + Material 3 sin sistema de diseño propio | Accepted |
| [ADR-0006](architecture/adr-0006-observability-and-errors.md) | Logging estructurado, OpenTelemetry opcional y Problem Details | Accepted |
| [ADR-0007](architecture/adr-0007-container-topology.md) | Topología de contenedores con proxy de mismo origen | Accepted |
| [ADR-0008](architecture/adr-0008-policy-based-authorization.md) | Modelo de autorización basado en políticas | Accepted |
| [ADR-0009](architecture/adr-0009-device-authentication.md) | Autenticación de dispositivos | Accepted |
| [ADR-0010](architecture/adr-0010-inventory-agent.md) | Agente de inventario multiplataforma en .NET | Accepted |
| [ADR-0011](architecture/adr-0011-public-ip-ipinfo.md) | IP pública con ipinfo.io desde el agente; IP de conexión en el servidor | Accepted |
| [ADR-0012](architecture/adr-0012-per-device-enrollment-codes.md) | Códigos de enrolamiento por equipo | Accepted |
| [ADR-0013](architecture/adr-0013-user-manual.md) | Manual de uso actualizado con cada funcionalidad (enmienda 1.2.0) | Accepted |
| [ADR-0014](architecture/adr-0014-database-engine-audit-agent.md) | Auditoría de motores de bases de datos desde el agente (enmienda 1.3.0) | Accepted |
| [ADR-0015](architecture/adr-0015-database-connections-without-verified-tls.md) | Conexiones a motores con certificados no válidos (enmienda 2.0.0) | Accepted |
| [ADR-0016](architecture/adr-0016-outlook-graph-application-permissions.md) | Integración con Outlook mediante Microsoft Graph con permisos de aplicación acotados | Proposed |
