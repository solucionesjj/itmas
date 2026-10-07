---
id: ADR-0001
title: Clean Architecture en cuatro proyectos
status: Accepted
date: 2026-10-07
deciders: [architecture]
related: [SPEC-0000]
---

# ADR-0001 — Clean Architecture en cuatro proyectos

## Contexto
El requerimiento exige separar API/Presentation, Application, Domain e Infrastructure, con controllers delgados,
y que futuras specs añadan módulos sin degradar la arquitectura.

## Decisión
Usamos cuatro proyectos .NET (`Itmas3.Domain`, `Itmas3.Application`, `Itmas3.Infrastructure`, `Itmas3.Api`) con
dependencias solo hacia dentro. `Application` define puertos y casos de uso (clases *handler* registradas en DI);
`Infrastructure` implementa los puertos; `Api` es el composition root. La regla se verifica con `LayerDependencyTests`.
Los módulos funcionales futuros se organizan como carpetas por módulo dentro de cada capa (*vertical slices* por
capa), no como proyectos nuevos, salvo que una spec lo justifique.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Proyecto único con carpetas | Simple | La regla de dependencias no se puede forzar por compilación |
| MediatR / mediator | Pipeline de comportamientos | Licencia comercial desde 2025; indirección innecesaria hoy |
| Minimal APIs | Menos código | El requerimiento pide controllers; los controllers facilitan convenciones por reflexión |
| Proyecto por módulo (modular monolith) | Aislamiento fuerte | Complejidad prematura sin módulos de negocio |

## Consecuencias
- Positivas: dependencias verificables, dominio testeable sin infraestructura, crecimiento ordenado.
- Negativas: algo más de ceremonia (DTOs y handlers) para operaciones triviales.
- Seguimiento: si aparecen preocupaciones transversales en casos de uso (auditoría, transacciones), evaluar
  decoradores sobre los handlers mediante un nuevo ADR.

## Impacto en la constitución
Recogido en §2.
