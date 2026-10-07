# database/

Artefactos de PostgreSQL que no pertenecen al código .NET.

| Ruta | Uso |
|---|---|
| `init/01-create-roles.sh` | Bootstrap de un volumen vacío: roles `migrator` y `app` (mínimo privilegio), esquemas `app` y `app_migrations`. Lo ejecuta la imagen oficial de PostgreSQL y también las pruebas de integración. |

El esquema de la aplicación se gestiona con **migraciones EF Core** en
`backend/src/Itmas3.Infrastructure/Persistence/Migrations/` (ver [DEVELOPMENT.md §5](../DEVELOPMENT.md#5-migraciones-ef-core)).
Decisión y detalles: [ADR-0004](../specs/architecture/adr-0004-database-roles-and-migrations.md) y
[specs/database/README.md](../specs/database/README.md).
