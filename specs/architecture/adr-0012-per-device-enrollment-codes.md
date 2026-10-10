---
id: ADR-0012
title: Códigos de enrolamiento por equipo
status: Accepted
date: 2026-10-09
deciders: [architecture, security]
related: [SPEC-0003, SPEC-0002, ADR-0009]
---

# ADR-0012 — Códigos de enrolamiento por equipo

## Contexto
ADR-0009 estableció que los agentes se enrolan con un **código de autorización único**, compartido por todos los
equipos y guardado en la configuración del servidor (`INVENTORY_ENROLLMENT_CODE`), a cambio del cual el servidor emite
una API key por dispositivo. Si ese código se filtra, cualquiera puede enrolar equipos falsos hasta que se rote.

SPEC-0003 introduce el **registro de equipos**: un equipo se da de alta en ITMas antes de instalar su agente, y el
solicitante pide un código único por equipo. También pide que eliminar un equipo impida que su agente siga enviando
datos. El solicitante confirmó que se mantiene el intercambio código → API key de ADR-0009 (SPEC-0003 §18.1).

## Decisión
Sustituye el **punto 1** de ADR-0009. El resto de ADR-0009 (API key por dispositivo, esquemas `Enrollment` y `Device`,
políticas, CSRF, límites de frecuencia y auditoría) sigue vigente.

1. **Un código por equipo.** Al crear un equipo (o al regenerar su código), el servidor genera un código de
   20 caracteres del alfabeto Base32 de Crockford (100 bits de un CSPRNG), presentado como `XXXXX-XXXXX-XXXXX-XXXXX`.
   - Se normaliza antes de validarlo: sin guiones ni espacios, en mayúsculas, con `I`/`L` → `1` y `O` → `0`.
     Un valor que no tenga 20 caracteres válidos se rechaza sin consultar la base de datos.
   - Se guarda solo su SHA-256 (índice único) y se busca por hash. 100 bits, con el límite de 10 intentos por minuto y
     por IP, hacen inviable la fuerza bruta. La búsqueda por hash no necesita una comparación en tiempo constante.
   - Se devuelve una sola vez, en la respuesta de alta o de regeneración (`Cache-Control: no-store`). No se puede
     volver a consultar.
2. **Un solo uso.** El código se consume al enrolar y deja de servir al regenerarlo o al eliminar el equipo. Para
   reinstalar el agente se regenera el código; el nuevo enrolamiento sustituye la API key anterior (`device.relinked`).
3. **El equipo determina la identidad.** El esquema `Enrollment` produce un principal con el id del equipo dueño del
   código. Un equipo es único por identificador de hardware entre los equipos no eliminados: si el hardware ya
   pertenece a otro equipo, el enrolamiento responde `409` y queda auditado (`device.enrollment.rejected`).
4. **Eliminar revoca.** Un equipo eliminado pierde el hash de su código y el de su API key en la misma transacción;
   el autenticador de dispositivos solo acepta equipos activos.
5. **Sin código general.** Se retiran `INVENTORY_ENROLLMENT_CODE` / `Inventory__EnrollmentCode`. El endpoint de
   enrolamiento ya no responde `404`: sin un código válido responde `401`.
6. **Transición.** La migración `AddDeviceRegistry` convierte los dispositivos enrolados con el código general en
   equipos activos; conservan su API key y no hay que reinstalarlos.
7. **Concurrencia.** El equipo usa `xmin` como token de concurrencia, de modo que dos enrolamientos simultáneos con el
   mismo código no pueden consumirlo dos veces (el segundo responde `409`).

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Mantener el código general junto a los códigos por equipo | Transición sin pasos manuales | Deja una vía para enrolar equipos sin registro previo; anula el control que pide SPEC-0003. |
| Código permanente y reutilizable por equipo | Reinstalar sin intervención del administrador | Si se filtra, permite suplantar el equipo en cualquier momento. |
| Guardar el código cifrado para poder volver a mostrarlo | Comodidad | Exige una clave de cifrado más y amplía la exposición; regenerar cubre el mismo caso. |
| Usar el código como credencial permanente en cada envío | Un único secreto | Contradice ADR-0009: el código circula por más canales (UI, personas) que la API key, que solo conoce el equipo. |

## Consecuencias
- **Positivas:** la filtración de un código afecta como mucho a un equipo y solo hasta que se usa; la revocación de un
  dispositivo pasa a ser una operación de la interfaz (eliminar el equipo), auditada; desaparece un secreto de la
  configuración del servidor.
- **Negativas / riesgos:**
  - Instalar el agente exige registrar antes el equipo y entregar su código al instalador.
  - Un usuario con `devices.update` puede regenerar el código de un equipo activo y enrolar otro equipo en su lugar.
    Mitigación: permiso explícito, auditoría con IP de origen y *hostname*, y el equipo legítimo deja de poder enviar.
  - El código pasa por el navegador del administrador. Mitigación: un solo uso, `no-store` y la SPA no lo guarda.
- **Seguimiento:** el agente 1.1.0 no cambia; el texto "código de autorización" y un mensaje propio para el `409`
  pueden llegar en una versión menor (SPEC-0003 P-12).

## Impacto en la constitución
Ninguno. §3.2 ya remite a ADR-0009 para la autenticación de dispositivos y esta decisión no relaja ningún control.
