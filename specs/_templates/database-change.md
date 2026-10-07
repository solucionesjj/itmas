---
id: SPEC-NNNN
title: <Cambio de base de datos>
type: database
status: Draft
owner: <persona>
created: YYYY-MM-DD
agents: [database, backend, security, testing]
related: []
---

# SPEC-NNNN — <Cambio de base de datos>

## 1. Motivo
## 2. Cambio de esquema
| Objeto | Operación | Detalle |
|---|---|---|
## 3. Datos existentes y migración de datos
<Volumen, backfill, valores por defecto, compatibilidad con la versión anterior de la API (despliegue sin downtime).>
## 4. Privilegios
<¿El rol de la app necesita algo más que DML en `app`? Justificar (constitution §5.6).>
## 5. Índices y rendimiento
## 6. Reversibilidad
<Cómo se revierte; si no es reversible, por qué es aceptable.>
## 7. Seguridad y datos personales
<Clasificación de datos, retención, cifrado.>
## 8. Validación
- [ ] `dotnet ef migrations has-pending-model-changes` sin cambios pendientes.
- [ ] SQL generado revisado (`dotnet ef migrations script --idempotent`).
- [ ] Pruebas de integración contra PostgreSQL (Testcontainers) en verde.
