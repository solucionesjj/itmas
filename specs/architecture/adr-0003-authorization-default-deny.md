---
id: ADR-0003
title: Autorización default deny basada en permisos
status: Accepted
date: 2026-10-07
deciders: [architecture, security]
related: [SPEC-0000, ADR-0008]
---

# ADR-0003 — Autorización *default deny* basada en permisos

## Contexto
Se exige "Default Deny": un usuario autenticado no obtiene privilegios automáticamente. La arquitectura debe
permitir más adelante un modelo granular (usuarios, roles, permisos, políticas, claims) similar al de plataformas
cloud, sin implementarlo ahora.

## Decisión
- **Fallback policy** = autenticado **y** `RequireAssertion(_ => false)`: un endpoint sin metadatos de autorización
  se deniega a todos (401 anónimo / 403 autenticado).
- Declaraciones permitidas por endpoint: `[AllowAnonymous]`, política `AuthenticatedUser` (solo identidad propia)
  o `[RequirePermission("<módulo>.<recurso>.<acción>")]`.
- `RequirePermissionAttribute` implementa `IAuthorizationRequirementData` (sin *policy provider* propio) y produce un
  `PermissionRequirement`; `PermissionAuthorizationHandler` delega en el puerto `IPermissionEvaluator`.
- Implementación actual: `DenyAllPermissionEvaluator` (no concede nada).
- Una prueba de convención por reflexión obliga a que todo endpoint declare autorización y prohíbe `[Authorize]` sin política.

## Evolución

> **Implementada por [ADR-0008](adr-0008-policy-based-authorization.md) / SPEC-0001** con políticas en lugar de
> roles. El texto siguiente es el planteamiento original.

### Planteamiento original
Sustituir el registro de `IPermissionEvaluator` por una implementación respaldada por PostgreSQL:
*role definitions* (conjuntos de permisos) + *role assignments* (principal, rol, ámbito), con caché por usuario.
Los permisos usan nombres jerárquicos (`billing.invoices.approve`), lo que permite comodines o ámbitos más adelante.
Los grupos/app roles de Entra ID pueden mapearse a asignaciones sin cambiar los endpoints.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Fallback = solo autenticado | Habitual | Un endpoint olvidado queda abierto a todo usuario autenticado |
| Roles de Entra ID (`[Authorize(Roles=…)]`) | Sin almacenamiento propio | Granularidad pobre; acoplado al proveedor; cambios requieren admin del tenant |
| Policy provider dinámico | Flexible | Más código que `IAuthorizationRequirementData` |

## Consecuencias
- Positivas: el modo de fallo es "denegar"; los endpoints documentan su permiso; el modelo de permisos es
  intercambiable sin tocar controllers.
- Negativas: hasta la spec de permisos ningún endpoint protegido por permiso es usable (intencional).

## Impacto en la constitución
Recogido en §3.3–§3.4.
