---
id: SPEC-NNNN
title: <Título corto>
type: feature            # feature | database | security
status: Draft            # Draft | Approved | In Progress | Implemented | Rejected | Superseded
owner: <persona responsable>
created: YYYY-MM-DD
approved-by:             # nombre y fecha de la aprobación humana
agents: [architecture, backend, frontend, database, security, testing]   # agentes que intervienen
related: []              # SPEC/ADR relacionados
---

# SPEC-NNNN — <Título>

## 1. Objetivo
<Qué resultado de negocio se busca, en una o dos frases.>

## 2. Contexto
<Situación actual, problema, motivación. Enlaces a material de origen.>

## 3. Alcance
- **Incluye:** …
- **Excluye:** …

## 4. Actores
| Actor | Descripción |
|---|---|

## 5. Requerimientos
### 5.1 Funcionales
- **RF-1** …
### 5.2 No funcionales
- **RNF-1** (rendimiento, disponibilidad, accesibilidad, auditoría…) …

## 6. Reglas de negocio
- **RN-1** … (se implementan en `Domain`; sus violaciones devuelven 422)

## 7. Permisos
| Permiso (`<módulo>.<recurso>.<acción>`) | Concede | Endpoints / pantallas |
|---|---|---|

## 8. Comportamiento esperado
<Flujos principales y alternativos, estados, mensajes al usuario. Diagramas si ayudan.>

## 9. Criterios de aceptación
- [ ] **CA-1** Dado … cuando … entonces …
- [ ] **CA-2** …

## 10. Análisis arquitectónico
<Impacto por capa, alternativas consideradas, ¿requiere ADR? ¿respeta la constitución?>

## 11. Cambios de frontend
<Rutas (`features/<x>/<x>.routes.ts`), componentes Material usados, navegación, estados de carga/error/vacío.>

## 12. Cambios de backend
| Método | Ruta | Permiso | Request | Response | Errores |
|---|---|---|---|---|---|
<Casos de uso (`Application/<Módulo>/<CasoDeUso>/`), entidades de dominio, puertos.>

## 13. Cambios de base de datos
<Tablas/columnas/índices, migración, datos existentes, reversibilidad. Si es complejo, spec `database` aparte.>

## 14. Consideraciones de seguridad
<Datos sensibles/personales, amenazas (STRIDE), validaciones, rate limiting, auditoría, logging permitido.>

## 15. Pruebas
| Criterio | Tipo (unit/integration/frontend) | Prueba |
|---|---|---|

## 16. Plan de implementación
| # | Tarea | Agente | Archivos |
|---|---|---|---|

## 17. Manual de uso
<Procesos del manual (`frontend/public/manual/`) que esta spec añade, cambia o retira (constitución §10.4).
"Ninguno" solo si no cambia nada visible para las personas usuarias.>
| Proceso (`id`) | Acción (nuevo / cambia / se retira) | Permiso | Pantallas (`routes`) |
|---|---|---|---|

## 18. Trazabilidad (al implementar)
- Commits/PRs: …
- Archivos principales: …
- Pruebas: …
- Manual de uso actualizado: …

## 19. Preguntas abiertas
- …
