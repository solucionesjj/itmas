---
id: ADR-0008
title: Modelo de autorización basado en políticas
status: Accepted
date: 2026-10-07
deciders: [architecture, security]
related: [SPEC-0001, ADR-0003, ADR-0002]
---

# ADR-0008 — Modelo de autorización basado en políticas

## Contexto
SPEC-0001 pide autorización inspirada en AWS IAM: `Usuario → Políticas → Permisos`, sin roles, con Root irrestricto,
denegación por defecto, auditoría y salvaguardas. ADR-0003 dejó el contrato (`[RequirePermission]` +
`IPermissionEvaluator`) y un evaluador que lo deniega todo.

## Decisión
1. **Usuarios de la aplicación** aprovisionados en el primer inicio de sesión (`OnTokenValidated`) y vinculados a una
   identidad externa `(provider, issuer, subject)`. La sesión lleva el id interno (`itmas3_uid`) y una marca de
   seguridad (`itmas3_sstamp`). El middleware `SessionValidationMiddleware` anula, en cada solicitud, la sesión de
   usuarios inexistentes, desactivados o con marca rotada.
2. **Catálogo de permisos en código** (`IPermissionDefinitionProvider`), sincronizado al arrancar. Los permisos que
   desaparecen se marcan obsoletos. Los no otorgables (`authorization.root.manage`) no pueden estar en políticas.
3. **Evaluación** (`PolicyPermissionEvaluator`): usuario activo → Root permite todo → si no, unión de permisos de
   políticas activas (sin obsoletos ni no otorgables).
4. **Caché por versión**: una instantánea por usuario (`AuthorizationSnapshot`: activo, root, marca, permisos con su
   política de origen) en memoria, con clave `(version, userId)`. `authorization_state.version` se incrementa en la misma
   transacción que cualquier cambio. Cada solicitud lee la versión una vez, así que la propagación es inmediata incluso
   con varias réplicas.
5. **Transacción de administración** (`IAuthorizationUnitOfWork`): bloquea la fila de versión (serializa los cambios de
   autorización y evita carreras como "revocar al último Root"), ejecuta el caso de uso, escribe el evento de auditoría y
   confirma. Si el caso de uso se rechaza por regla de negocio o salvaguarda, revierte y registra un evento `rejected`
   en una transacción aparte.
6. **Salvaguardas** contra la escalada de administradores delegados (RN-9 a RN-12), en `Application`.
7. **Auditoría** *append-only*: la migración revoca `UPDATE/DELETE/TRUNCATE` sobre `audit_events` a todo rol que no sea su dueño.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Permisos como claims en la cookie | Sin consultas por solicitud | Los cambios no se aplican hasta un nuevo login; cookie grande |
| Caché con TTL fijo | Simple | Privilegios revocados vigentes durante el TTL |
| Roles/grupos de Entra ID | Sin almacenamiento propio | Prohibido por SPEC-0001 (sin roles; el IdP no decide permisos) |
| Motor externo (OPA, Cedar, OpenFGA) | Potente (ABAC, ReBAC) | Infraestructura y lenguaje adicionales injustificados para v1 |

## Consecuencias
- Positivas: revocación inmediata, trazabilidad permiso → política, extensible (Deny, recursos, condiciones) en
  `PolicyStatement` y en el evaluador sin tocar los endpoints.
- Negativas: una consulta ligera por solicitud autenticada (versión) y una consulta a la BD por cada (usuario, versión)
  que no esté en caché. Todas las mutaciones de autorización se serializan, lo cual es aceptable dado su bajo volumen.

## Impacto en la constitución
Enmienda PATCH 1.0.1 de §3.3 (convención de nombres de permisos `<recurso>.<acción>`).
