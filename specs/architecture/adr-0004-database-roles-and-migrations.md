---
id: ADR-0004
title: Roles de base de datos de mínimo privilegio y migrador dedicado
status: Accepted
date: 2026-10-07
deciders: [architecture, database, security]
related: [SPEC-0000]
---

# ADR-0004 — Roles de BD de mínimo privilegio y migrador dedicado

## Contexto
Se requieren migraciones, creación controlada del esquema, credenciales por variables de entorno y mínimo privilegio.
No se debe asumir que PostgreSQL está listo porque su contenedor arrancó.

## Decisión
- Tres roles: superusuario de arranque (solo el contenedor `postgres`), `itmas3_migrator` (dueño de `app` y
  `app_migrations`, DDL) e `itmas3_app` (DML en `app` mediante *default privileges*). Creados por
  `database/init/01-create-roles.sh`, que lee contraseñas con `\getenv`.
- El historial de EF Core vive en `app_migrations`, inaccesible para la API.
- Las migraciones se aplican con un **EF Core migration bundle** en el servicio one-shot `migrator`
  (`depends_on: service_healthy` de postgres); la API espera `service_completed_successfully`.
- El health check de PostgreSQL usa `pg_isready -h 127.0.0.1` (TCP), que no responde durante la inicialización.
- `DesignTimeDbContextFactory` lee las mismas variables `Database__*` que la API: sin contraseñas en línea de comandos.
- Convención `snake_case` (EFCore.NamingConventions), esquema por defecto `app`, `IncludeErrorDetail=false`.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| `Database.Migrate()` al arrancar la API | Simple | La API necesitaría DDL; carreras con varias réplicas |
| Un único rol para todo | Simple | Viola mínimo privilegio |
| Scripts SQL manuales | Control total | Duplica el modelo de EF; propenso a errores |
| Herramienta externa (Flyway/Liquibase) | Madura | Segundo sistema de migraciones; el requerimiento sugiere EF Core |

## Consecuencias
- Positivas: una API comprometida no puede alterar el esquema ni el historial; migraciones serializadas y auditables.
- Negativas: el script de roles solo corre con volumen vacío; cambios de roles posteriores requieren spec y procedimiento.

## Impacto en la constitución
Recogido en §5.
