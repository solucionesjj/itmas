---
id: ADR-0013
title: Manual de uso del sistema, actualizado con cada funcionalidad
status: Accepted
date: 2026-10-10
deciders: [architecture, security]
related: [SPEC-0004]
---

# ADR-0013 — Manual de uso del sistema, actualizado con cada funcionalidad

## Contexto
La documentación actual (README, ARCHITECTURE, SECURITY, DEVELOPMENT, `agent/README.md`) está escrita para quien
desarrolla u opera ITMas. No hay un manual para las personas que **usan** el sistema: cómo se da de alta un usuario,
cómo se conceden privilegios, cómo se registra un equipo y se enrola su agente, cómo se consulta el inventario.

El solicitante pide (2026-10-10) que exista ese manual, que describa los procesos del sistema, que se actualice cada vez
que se desarrolla una funcionalidad y que se pueda consultar de forma sencilla. El contenido es estático: solo cambia
cuando cambia una funcionalidad.

La constitución §10 obliga a actualizar la documentación técnica en el mismo cambio, pero no menciona un manual de
uso. Sin una regla explícita, el manual quedaría desactualizado en cuanto llegue la siguiente spec.

## Decisión
1. **Fuente única del manual:** archivos Markdown versionados en `frontend/public/manual/`, uno por proceso, más un
   índice `index.json` (título, resumen, categoría, permisos necesarios, spec de origen y fecha de la última
   actualización). Viven dentro de `frontend/` porque la imagen del frontend se construye con esa carpeta, y el build
   de Angular los copia tal cual.
2. **Consulta pública dentro de la aplicación:** una sección "Manual de uso" de la SPA (SPEC-0004), **accesible sin
   iniciar sesión y sin privilegios** (decisión del solicitante, 2026-10-10), muestra el índice y cada proceso. No hay
   servicio, base de datos ni endpoint nuevos: nginx sirve los archivos como estáticos.
3. **Regla de proceso (enmienda):** toda spec que añada, cambie o retire un proceso visible para personas usuarias
   DEBE actualizar el manual en el mismo cambio, y no pasa a `Implemented` sin ello. La plantilla de spec incluye una
   sección "Manual de uso" para identificar esos procesos desde el borrador.
4. **Contenido:** procesos para personas usuarias (qué es, quién puede hacerlo y con qué permiso, pasos, resultado y
   problemas frecuentes). Como es público, nunca secretos, datos personales reales, datos de infraestructura (nombres
   o IPs internos, versiones de componentes) ni detalles internos que no necesite el usuario.
5. **Verificación automática:** una prueba del frontend comprueba que cada entrada del índice tiene su archivo y que
   cada entrada de la navegación de la aplicación tiene al menos un proceso del manual que la explica.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Manual en un servicio externo (wiki, SharePoint, Confluence) | Edición sin desarrolladores | Se desincroniza del código; no se puede exigir en el mismo cambio ni verificar en las pruebas |
| Manual en Markdown en la raíz (`docs/manual/`) | Separado del frontend | La imagen del frontend no lo ve (contexto de build `./frontend`); obligaría a ampliar el contexto de Docker |
| Páginas del manual como componentes Angular (HTML) | Sin dependencias | Escribir y revisar el manual en HTML es más costoso y propenso a errores que en Markdown |
| Generar un sitio estático aparte (MkDocs, Docusaurus) | Buscador y navegación incluidos | Otra cadena de herramientas y otro servicio que construir y publicar, para unas pocas páginas |
| Manual dentro de la base de datos, editable desde la UI | Edición sin despliegue | El contenido es estático y ligado a las versiones; añade esquema, permisos y API sin necesidad |

## Consecuencias
- **Positivas:** el manual se revisa en el mismo PR que la funcionalidad, queda versionado con el código y la prueba
  impide que una pantalla nueva llegue sin su explicación.
- **Negativas / riesgos:**
  - Cada spec tiene un poco más de trabajo de documentación.
  - Renderizar Markdown en la SPA requiere una dependencia (`marked`) y mostrar HTML generado. Mitigación: el
    contenido es propio y versionado, Angular lo sanea antes de insertarlo y la CSP prohíbe scripts en línea.
  - El manual es público y el servidor está expuesto a Internet: cualquiera puede leer los procesos y los nombres de
    los permisos. No da acceso a nada (los permisos se evalúan en la API), pero facilita el reconocimiento. Por eso el
    manual no debe contener nada confidencial (punto 4) y `security` revisa cada cambio.
- **Seguimiento:** SPEC-0004 implementa el visor, la prueba y el contenido inicial con los procesos de SPEC-0001 a
  SPEC-0003.

## Impacto en la constitución
Enmienda **MINOR** (añade una regla) → versión **1.2.0**:

- **§10.4 (nueva) — Manual de uso.** "El sistema DEBE tener un manual de uso para personas usuarias en
  `frontend/public/manual/`, consultable desde la aplicación sin iniciar sesión. Toda spec que añada, cambie o retire un proceso visible
  para personas usuarias DEBE actualizar el manual en el mismo cambio; la spec no pasa a `Implemented` sin ello. El
  manual describe procesos (qué, quién y con qué permiso, pasos, resultado y problemas frecuentes) y NO DEBE contener
  secretos, datos personales reales ni detalles internos de infraestructura, porque es público."
  *Aplicada el 2026-10-10 (constitución 1.2.0), aprobada por el solicitante.*
- **§10.3:** añadir "y el manual de uso afectado" a lo que actualiza una spec implementada.

Cambios derivados (sin enmienda): sección "Manual de uso" en `specs/_templates/feature-spec.md`, fila
`frontend/public/manual/**` en la matriz de `agents/README.md` (y en `owns` de `architecture`), regla en `AGENTS.md`
y paso "Documentation" de `specs/README.md`.
