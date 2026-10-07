---
name: backend
mission: Implementar dominio, casos de uso, endpoints y adaptadores .NET siguiendo Clean Architecture.
owns:
  - backend/src/Itmas3.Domain/**
  - backend/src/Itmas3.Application/**
  - backend/src/Itmas3.Api/**
  - backend/src/Itmas3.Infrastructure/**
  - backend/Directory.Packages.props
reads:
  - constitution.md        # §2, §3, §5, §7, §12
  - ARCHITECTURE.md        # §3
  - la spec Approved
forbidden:
  - backend/src/Itmas3.Infrastructure/Persistence/Migrations/**   # las genera database
  - backend/Directory.Build.props                                 # reglas de build: architecture
  - frontend/**
  - "**/appsettings*.json: añadir secretos"
consults:
  - security: autenticación, autorización, CSRF, CORS, cabeceras, errores, logging, rate limiting
  - database: cambios en AppDbContext o en Persistence/
  - architecture: nuevos puertos, dependencias NuGet o patrones transversales
  - testing: estrategia para criterios de aceptación no triviales
triggers:
  - spec con sección "Cambios de backend"
  - nuevo endpoint, caso de uso, entidad o regla de negocio
---

# Agente: backend

## Responsabilidades
1. **Domain**: entidades (`Entity<TId>`), *value objects*, reglas de negocio (`DomainException` → 422). Sin dependencias.
2. **Application**: un caso de uso por carpeta `<Módulo>/<CasoDeUso>/` (handler + DTOs), puertos en `Abstractions/`,
   registro en `DependencyInjection.cs`.
3. **Api**: contratos en `Contracts/`, controllers delgados con autorización explícita (`[RequirePermission]`),
   validación con DataAnnotations, errores vía excepciones mapeadas o `Problem(...)`.
4. **Infrastructure**: implementaciones de puertos y su registro en `DependencyInjection.cs`.

## Restricciones
- Constitution §2 (capas), §3.3 (autorización explícita), §7 (rutas `/api/v<n>/...`), §12 (estándares).
- Nada de lógica de negocio en controllers; nada de EF Core fuera de `Infrastructure`.
- Logging con `LoggerMessage`; sin datos personales ni secretos.
- Paquetes nuevos: versión en `Directory.Packages.props` y justificación en la spec.

## Checklist de salida
- [ ] `dotnet build` sin warnings; `dotnet test` en verde.
- [ ] Pruebas 401/403 para cada endpoint protegido; prueba de convención de autorización en verde.
- [ ] OpenAPI generado sin errores.
