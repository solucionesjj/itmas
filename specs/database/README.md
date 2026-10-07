# Specs de base de datos

Cambios de esquema, roles o datos que no forman parte de una spec de funcionalidad (o que por su riesgo
merecen una spec propia). Plantilla: [`_templates/database-change.md`](../_templates/database-change.md).

## Estado actual del esquema

| Esquema | Dueño | Acceso de la API | Contenido |
|---|---|---|---|
| `app` | `itmas3_migrator` | DML (default privileges) | `data_protection_keys` (técnica); autorización: `users`, `user_external_identities`, `policies`, `policy_statements`, `policy_assignments`, `permissions`, `authorization_state` |
| `app` → `audit_events` | `itmas3_migrator` | **Solo INSERT/SELECT** | Auditoría de autorización (*append-only*, SPEC-0001) |
| `app_migrations` | `itmas3_migrator` | Ninguno | `__ef_migrations_history` |
| `public` | — | Ninguno (`REVOKE ALL FROM PUBLIC`) | — |

Migraciones: `backend/src/Itmas3.Infrastructure/Persistence/Migrations/`. Roles: `database/init/01-create-roles.sh`.
Decisión: [ADR-0004](../architecture/adr-0004-database-roles-and-migrations.md).

## Convenciones

- Nombres en `snake_case` (aplicado automáticamente por la convención de EF Core).
- Claves primarias `id`; claves foráneas `<entidad>_id`; índices `ix_<tabla>_<columnas>`.
- Fechas en `timestamp with time zone` (UTC). Identificadores públicos preferentemente `uuid`.
- Una configuración `IEntityTypeConfiguration<T>` por tipo.
