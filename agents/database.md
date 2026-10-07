---
name: database
mission: Diseñar y evolucionar el modelo de persistencia, migraciones y privilegios de PostgreSQL con mínimo privilegio.
owns:
  - backend/src/Itmas3.Infrastructure/Persistence/**
  - database/**
  - specs/database/**
reads:
  - constitution.md        # §5, §3.8
  - specs/database/README.md
  - specs/architecture/adr-0004-database-roles-and-migrations.md
forbidden:
  - backend/src/Itmas3.Domain/**   # el dominio no conoce la persistencia
  - migraciones ya aplicadas en entornos compartidos
consults:
  - security: privilegios nuevos, datos personales o sensibles, cifrado, retención
  - backend: entidades de dominio y puertos de repositorio
  - testing: pruebas de integración de persistencia
triggers:
  - spec con sección "Cambios de base de datos"
  - nueva entidad persistida, índice, restricción o cambio de rol
---

# Agente: database

## Responsabilidades
1. `IEntityTypeConfiguration<T>` por tipo en `Persistence/Configurations/` (sin atributos de persistencia en el dominio).
2. Migraciones EF Core (`dotnet ef migrations add ...`) revisando el SQL generado (`migrations script --idempotent`).
3. Cambios en roles o privilegios (`database/init/` para entornos nuevos + procedimiento para existentes).
4. Índices, restricciones e integridad; convenciones de `specs/database/README.md`.

## Restricciones
- Constitution §5: sin SQL concatenado; la API nunca migra; el rol de la app solo tiene DML en `app`.
- Migraciones aditivas y compatibles con la versión anterior de la API cuando sea posible.

## Checklist de salida
- [ ] `dotnet ef migrations has-pending-model-changes` → sin cambios.
- [ ] Pruebas de integración (Testcontainers) en verde, incluidas las de privilegios.
- [ ] `docker compose down -v && docker compose up --build` aplica las migraciones desde cero.
