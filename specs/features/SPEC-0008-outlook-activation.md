---
id: SPEC-0008
title: Activación de Outlook para la agenda de infraestructura
type: feature
status: Draft
owner: jmartinez
created: 2026-10-21
approved-by:
agents: [architecture, security, backend, testing]
related: [SPEC-0006, ADR-0016]
---

# SPEC-0008 — Activación de Outlook para la agenda de infraestructura

> Nace de la **enmienda del 2026-10-21 de SPEC-0006** (§19.8, D-5): el solicitante trasladó aquí **CA-24** y la **prueba real con el
> tenant** de Microsoft 365 (tarea 11 de SPEC-0006), que dependen del tenant y no de la agenda, para que SPEC-0006 quede `Implemented` con
> Outlook desactivado. **Mientras esta spec no esté `Implemented`, `AGENDA_OUTLOOK_ENABLED` no se activa en producción** (SPEC-0006 §19.8,
> D-4, riesgo 2). Borrador inicial: las propuestas (P-n) y las preguntas (Q-n) de §19 esperan al solicitante.

## 1. Objetivo

Activar la publicación de la agenda en el calendario compartido de Outlook en producción **con el alcance verificado**: la aplicación de
Graph solo puede operar sobre el buzón de la agenda, la credencial cumple lo que pide ADR-0016 y lo que SPEC-0006 no pudo comprobar sin
el tenant queda comprobado o decidido.

## 2. Contexto

SPEC-0006 entregó la agenda con la publicación en Outlook **implementada y probada con dobles** (ninguna prueba llama a Microsoft,
constitución §9.4) y **desactivada por defecto** (`AGENDA_OUTLOOK_ENABLED=false`). ADR-0016 fija el diseño: una aplicación de Entra ID
propia, con un único permiso de calendario concedido por *RBAC for Applications* de Exchange Online sobre **un solo buzón**, sin
consentimiento global; MSAL más un `HttpClient` tipado; y la publicación después de guardar, sin cola.

Quedaron pendientes, por depender del tenant (SPEC-0006 §18 y §19.8):

- **CA-24**: que con el permiso acotado la aplicación opere sobre el buzón configurado y reciba `403` sobre otro buzón.
- La **prueba real** con un buzón de pruebas: publicar, actualizar y cancelar (runbook en DEVELOPMENT.md §11.3).
- La **credencial**: el código de la v1 solo admite **secreto de cliente**; pasar a certificado, que ADR-0016 recomienda para
  producción, exige un cambio de código. El solicitante aceptó el secreto **hasta producción**.
- Lo que SPEC-0006 dejó **no verificado** sobre el comportamiento real de Graph (§18, «Verificación del incremento C»): que el buzón
  acepte la zona IANA enviada; qué hace Graph con el `transactionId` de un evento ya borrado (decisión 11 de SPEC-0006); que `cancel`
  funcione en un evento sin más invitados que el responsable; el formato real de los ids y de `webLink` frente a las comprobaciones del
  adaptador; un `Retry-After` real; y el comportamiento tras un proxy de salida.

Los prerrequisitos del tenant están en SPEC-0006 §14.4 y en DEVELOPMENT.md §11.2; los realiza el equipo de infraestructura, que
administra el tenant (SPEC-0006 Q-1).

## 3. Alcance

- **Incluye:** la verificación del alcance con el tenant real (CA-24) y la prueba con un buzón de pruebas; la decisión y, si procede, la
  implementación de la credencial por certificado; la confirmación (o la corrección) de los supuestos de Graph que SPEC-0006 no pudo
  comprobar; la lista de verificación de producción de SECURITY.md cumplida; y el registro de todo ello sin direcciones ni identificadores
  del tenant.
- **Excluye:** cambios de funcionalidad de la agenda (siguen en SPEC-0006 o en specs nuevas); leer respuestas de asistencia (ADR-0016,
  seguimiento); el procesamiento en segundo plano (SPEC-0006 RNF-6); y, salvo que el solicitante lo decida (Q-3), un presupuesto de
  destinatarios por hora.

## 4. Actores

| Actor | Descripción |
|---|---|
| Equipo de infraestructura (administra el tenant) | Hace los prerrequisitos de Microsoft 365, entrega los datos de configuración y ejecuta la prueba con el buzón de pruebas. |
| Quien despliega ITMas | Configura las variables `AGENDA_OUTLOOK_*` en el entorno, sin pasar el secreto por la línea de comandos (constitución §4). |
| `security` | Revisa el alcance verificado, la credencial y la lista de verificación de producción. |

## 5. Requerimientos

### 5.1 Funcionales

- **RF-1** Antes de activar Outlook en producción, el alcance de la aplicación de Graph se verifica con el tenant real: opera sobre el
  buzón de la agenda y es rechazada sobre cualquier otro (CA-24 de SPEC-0006, aquí CA-1).
- **RF-2** La prueba del runbook (DEVELOPMENT.md §11.3) se ejecuta con un buzón de pruebas y su resultado queda en §18: publicar,
  actualizar sin duplicar, cancelar con el motivo y `graph_forbidden` fuera del ámbito.
- **RF-3** La credencial de producción es la que decida el solicitante (Q-1): certificado, con el cambio de código que exige, o secreto de
  caducidad corta aceptado de nuevo de forma explícita.
- **RF-4** Cada supuesto de Graph no verificado en SPEC-0006 (§2) queda **confirmado** con el buzón de pruebas o **corregido** en el
  adaptador, con su prueba.

### 5.2 No funcionales

- **RNF-1** Ninguna prueba automatizada llama a Microsoft (constitución §9.4): la verificación con el tenant es manual y documentada.
- **RNF-2** Ni la spec, ni el manual, ni los logs, ni los registros de la prueba contienen direcciones de buzón, identificadores del
  tenant o de la aplicación, ni secretos (constitución §4 y §10.4).

## 6. Reglas de negocio

Sin reglas nuevas: se aplican las de SPEC-0006.

## 7. Permisos

Sin permisos nuevos. Se aplican los `agenda.*` de SPEC-0006 (con la enmienda del 2026-10-21: modificar lo Programado exige
`agenda.publish`).

## 8. Comportamiento esperado

1. El equipo de infraestructura hace los prerrequisitos (DEVELOPMENT.md §11.2) y verifica el ámbito con `Test-ServicePrincipalAuthorization`.
2. En un entorno de pruebas, con un buzón de pruebas, se activa Outlook y se ejecuta la prueba de DEVELOPMENT.md §11.3.
3. Se comprueban los supuestos de Graph de §2 y se corrige el adaptador si alguno no se cumple.
4. Se cumple la lista de verificación de producción de SECURITY.md (sección de la agenda).
5. Solo entonces se activa Outlook en producción, con el buzón y la credencial de producción.

## 9. Criterios de aceptación

- [ ] **CA-1** (CA-24 de SPEC-0006) Verificación documentada en el tenant: con el permiso acotado, la aplicación opera sobre el buzón
  configurado y es rechazada (`403`, `graph_forbidden` en ITMas) sobre otro buzón.
- [ ] **CA-2** Con un buzón de pruebas: programar publica la actividad (estado *Publicado*, visible en el calendario del buzón y, en solo
  lectura, en el del equipo); modificarla actualiza la invitación sin duplicarla; cancelarla envía la cancelación con el motivo.
- [ ] **CA-3** En Entra ID no hay ninguna concesión de `Calendars.ReadWrite` para la aplicación y sus propietarios están limitados
  (SECURITY.md, lista de verificación).
- [ ] **CA-4** La credencial de producción es la decidida en Q-1 y su vencimiento está anotado en la propia agenda.
- [ ] **CA-5** Cada supuesto de Graph de §2 está confirmado con el buzón de pruebas o corregido en el adaptador con su prueba.

## 10. Análisis arquitectónico

- No cambia la arquitectura de ADR-0016. Si Q-1 elige certificado, cambia el adaptador (`GraphOutlookSettings`,
  `MsalGraphTokenProvider`) y la configuración (una variable nueva para el certificado o su ruta); requiere revisión de `security` y,
  si el certificado se monta como archivo, de `architecture` (Docker).
- Si la prueba muestra que Graph recuerda el `transactionId` de un evento borrado de otra forma de la supuesta (decisión 11 de SPEC-0006),
  se ajusta la recreación en el adaptador.

## 11. Cambios de frontend

Ninguno previsto.

## 12. Cambios de backend

Solo los que resulten de Q-1 (credencial) y de RF-4 (supuestos de Graph). Sin endpoints nuevos.

## 13. Cambios de base de datos

Ninguno previsto.

## 14. Consideraciones de seguridad

- Es la spec que cierra el riesgo 2 aceptado en SPEC-0006 §19.8 (acotamiento no verificado) y, según Q-1, el riesgo 1 (secreto en lugar
  de certificado).
- Un consentimiento global posterior o quien pueda añadir credenciales a la aplicación amplía o hereda el acceso: la verificación del
  alcance se repite de forma periódica (SECURITY.md).
- Revisión de `security` obligatoria del resultado de la prueba, de la credencial y de cualquier cambio del adaptador.

## 15. Pruebas

| Criterio | Tipo | Prueba |
|---|---|---|
| CA-1, CA-2, CA-3 | Verificación documentada (manual, con el tenant) | DEVELOPMENT.md §11.2 y §11.3; resultado en §18 sin direcciones ni identificadores. |
| CA-4 | Verificación documentada; unitarias si cambia el adaptador | Pruebas del proveedor de token con un manejador simulado (como `MsalGraphTokenProviderTests`). |
| CA-5 | Verificación documentada; unitarias del adaptador para lo que se corrija | `GraphOutlookCalendarGatewayTests`. |

## 16. Plan de implementación

Pendiente de las respuestas de §19. Borrador:

| # | Tarea | Agente | Archivos |
|---|---|---|---|
| 1 | Prerrequisitos del tenant y verificación del ámbito (§8, pasos 1 y 2) | equipo de infraestructura (security revisa) | — |
| 2 | Credencial según Q-1 | backend (security) | `Itmas3.Infrastructure/Agenda/Graph/**`, `Itmas3.Api/Agenda/**`, `.env.example`, README |
| 3 | Prueba con el buzón de pruebas y supuestos de Graph (CA-2, CA-5) | testing + equipo de infraestructura (security) | §18 de esta spec |
| 4 | Lista de verificación de producción y activación | security + architecture | SECURITY.md, DEVELOPMENT.md §11 |

## 17. Manual de uso

| Proceso (`id`) | Acción | Permiso | Pantallas (`routes`) |
|---|---|---|---|
| — | Ninguno previsto: el manual de SPEC-0006 ya describe la agenda con y sin Outlook (`programar-y-cancelar`). Si la prueba cambia algo visible, se actualiza `programar-y-cancelar`. | — | — |

## 18. Trazabilidad (al implementar)

- Commits/PRs: …
- Archivos principales: …
- Pruebas: …
- Manual de uso actualizado: …

## 19. Preguntas abiertas

- **Q-1** Credencial de producción: ¿certificado (recomendado por ADR-0016; exige cambiar el adaptador y decidir cómo llega el certificado
  al contenedor) o secreto de caducidad corta aceptado de nuevo? *Propuesta P-1: certificado.*
- **Q-2** ¿Quién ejecuta la prueba con el buzón de pruebas y en qué entorno? ¿Cuál es el buzón de pruebas y el que queda **fuera** del
  ámbito? (Sus direcciones no se escriben en el repositorio.)
- **Q-3** El límite de avisos cuenta peticiones, no destinatarios (SPEC-0006, hallazgo F-3, riesgo aceptado). ¿Se añade aquí un
  presupuesto de destinatarios por hora por debajo del límite de envío del buzón, o se mantiene el riesgo? *Propuesta P-2: mantenerlo y
  bajar `AGENDA_PUBLISH_REQUESTS_PER_HOUR` si Exchange limita el buzón.*
- **Q-4** Nombre de la lista de distribución del equipo para `AGENDA_OUTLOOK_TEAM_ADDRESS` (SPEC-0006 Q-2), si se usa.
- **Q-5** ¿Habrá varias réplicas de la API? El límite de avisos vive en la memoria de cada proceso (riesgo aceptado en SPEC-0006): con
  varias réplicas se multiplica.
