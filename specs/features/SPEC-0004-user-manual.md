---
id: SPEC-0004
title: Manual de uso del sistema
type: feature
status: Implemented
owner: jmartinez
created: 2026-10-10
approved-by: jmartinez (2026-10-10)
agents: [architecture, frontend, security, testing]
related: [ADR-0013, SPEC-0001, SPEC-0002, SPEC-0003]
---

# SPEC-0004 — Manual de uso del sistema

> Derivada de la petición del solicitante (2026-10-10) y de sus respuestas a las preguntas del mismo día (§18). La
> regla que obliga a mantener el manual ya está en la constitución 1.2.0 (§10.4, [ADR-0013](../architecture/adr-0013-user-manual.md), aceptado).
> Aprobada el 2026-10-10.

## 1. Objetivo

Que cualquier persona usuaria pueda consultar, de forma sencilla y desde la propia aplicación, un manual de uso con los
procesos del sistema, y que ese manual esté siempre actualizado porque cada funcionalidad nueva lo actualiza.

## 2. Contexto

### Definición original (solicitante)

> Quiero que cada vez que se desarrolle una funcionalidad se vaya actualizando el manual de uso del sistema, indicando
> los procesos que existen (por ejemplo: enrolamiento de dispositivos, creación de usuarios, asignación de privilegios),
> de tal manera que el usuario siempre pueda entrar y consultar de manera sencilla el manual y que este siempre esté
> actualizado. Como el contenido es estático y solo cambia cuando hay una nueva funcionalidad, coloca un mecanismo
> sencillo para consultarlo.

Hoy la documentación es técnica (README, ARCHITECTURE, SECURITY, DEVELOPMENT y `agent/README.md`). La regla que
obliga a mantener el manual se añade a la constitución con ADR-0013. Esta spec construye el mecanismo de consulta y
el contenido inicial.

## 3. Alcance

**Incluye**
- Manual en Markdown en `frontend/public/manual/` (un archivo por proceso) con un índice `index.json`.
- Sección "Manual de uso" en la SPA: índice por categorías, búsqueda por texto y página de cada proceso.
- Acceso **público**, sin iniciar sesión ni privilegios: enlace en la página de inicio de sesión, entrada en la
  navegación lateral y botón de ayuda en la barra superior de la aplicación (P-1).
- Contenido inicial con los procesos de SPEC-0001 a SPEC-0003 (§8.3).
- Prueba automática de coherencia del manual (RF-7).
- Enmienda de la constitución 1.2.0 y cambios derivados en plantilla, agentes y AGENTS.md (ADR-0013).

**Excluye**
- Edición del manual desde la interfaz (se edita en el repositorio, con la spec que cambia el proceso).
- Ayuda contextual en cada pantalla (enlaces "?" a la sección concreta) (P-6).
- Exportación a PDF: basta con imprimir desde el navegador (P-5).
- Documentación técnica o de operación (despliegue, variables de entorno, migraciones), que sigue en README,
  DEVELOPMENT y SECURITY.

## 4. Actores

| Actor | Descripción |
|---|---|
| Visitante | Cualquier persona, con o sin cuenta en ITMas (por ejemplo, quien instala el agente). Consulta el manual sin iniciar sesión. |
| Persona usuaria | Usuario de ITMas, con o sin permisos. Consulta el manual desde la aplicación. |
| Autor de una spec | Persona o agente de IA que desarrolla una funcionalidad y actualiza el manual en el mismo cambio. |

## 5. Requerimientos

### 5.1 Funcionales
- **RF-1** El manual está formado por procesos. Cada proceso tiene un archivo Markdown y una entrada en `index.json`
  con: identificador, título, resumen, categoría, permisos necesarios (puede no tener), specs de origen y fecha de la
  última actualización.
- **RF-2** La página "Manual de uso" muestra el índice agrupado por categoría, con el título y el resumen de cada
  proceso y los permisos que necesita.
- **RF-3** Un campo de búsqueda filtra los procesos por título, resumen y categoría, sin distinguir mayúsculas ni tildes.
- **RF-4** Cada proceso se abre en su propia página (`/manual/<id>`), con su título, los permisos necesarios, la fecha
  de la última actualización y la spec de origen, y un enlace para volver al índice. La URL se puede compartir.
- **RF-5** El manual es público (P-1): `/manual` y `/manual/<id>` se abren sin iniciar sesión y sin privilegios, con
  una barra superior propia (nombre de la aplicación, selector de tema y "Iniciar sesión" o "Volver a ITMas" según
  haya sesión). Se llega desde la página de inicio de sesión ("Consultar el manual de uso"), desde la navegación lateral
  ("Manual de uso") y desde un botón de ayuda en la barra superior de la aplicación.
- **RF-6** Se muestran todos los procesos, aunque la persona no tenga sus permisos; cada uno indica qué permiso hace
  falta y a quién pedirlo (P-2).
- **RF-7** Una prueba automática falla si una entrada del índice no tiene archivo, si un archivo no está en el índice,
  si hay identificadores repetidos o si una entrada de la navegación de la aplicación no tiene ningún proceso del
  manual asociado.

### 5.2 No funcionales
- **RNF-1** El manual no añade servicios, endpoints ni tablas: son archivos estáticos que sirve nginx.
- **RNF-2** Las pantallas cumplen la constitución §6: Angular Material estándar, temas claro y oscuro, accesibilidad
  (encabezados, navegación por teclado) y uso en pantallas estrechas.
- **RNF-3** El manual se carga solo al entrar en la sección (ruta diferida) y su contenido se puede cachear en el
  navegador, salvo `index.json`, que se revalida para mostrar siempre la última versión desplegada.

## 6. Reglas de negocio

| ID | Regla |
|---|---|
| RN-1 | El manual describe procesos para personas usuarias: qué es, quién puede hacerlo (permiso), pasos, resultado y problemas frecuentes. |
| RN-2 | El manual es público: NO DEBE contener secretos, códigos, datos personales reales ni detalles internos de infraestructura (nombres o IPs internos, versiones de componentes). |
| RN-3 | Toda spec que añada, cambie o retire un proceso visible para personas usuarias actualiza el manual en el mismo cambio (constitución §10.4, ADR-0013). |
| RN-4 | Cada proceso indica la spec de origen y la fecha de su última actualización. |

## 7. Permisos

No se añaden permisos. Las rutas del manual son públicas (sin `authGuard`), como `/login`; no usan la API, así que no
hay endpoints nuevos ni cambia la autorización del backend.

## 8. Comportamiento esperado

### 8.1 Estructura de archivos

```text
frontend/public/manual/
├── index.json                       Índice: categorías y procesos
├── iniciar-sesion.md
├── usuarios-alta-automatica.md
└── …                                Un archivo por proceso (identificador = nombre del archivo)
```

```json
{
  "categories": [{ "id": "inventario", "title": "Inventario de equipos" }],
  "processes": [
    {
      "id": "registrar-equipo",
      "title": "Registrar un equipo y obtener su código",
      "summary": "Da de alta un equipo en el inventario para poder instalar su agente.",
      "category": "inventario",
      "permissions": ["devices.create"],
      "specs": ["SPEC-0003"],
      "updated": "2026-10-10",
      "routes": ["/inventory"]
    }
  ]
}
```

`routes` relaciona el proceso con las pantallas que explica; la prueba de RF-7 lo usa para comprobar que toda entrada
de la navegación tiene su explicación.

### 8.2 Plantilla de cada proceso

```markdown
# <Título del proceso>

<Para qué sirve, en una o dos frases.>

**Quién puede hacerlo:** <permiso o "cualquier usuario">.

## Pasos
1. …

## Resultado
…

## Problemas frecuentes
| Situación | Qué hacer |
|---|---|
```

### 8.3 Contenido inicial

| Categoría | Proceso | Permiso | Spec |
|---|---|---|---|
| Primeros pasos | Iniciar y cerrar sesión; tema claro u oscuro | — | SPEC-0000 |
| Primeros pasos | Qué puedo hacer: permisos y cómo solicitarlos | — | SPEC-0001 |
| Usuarios y privilegios | Alta de usuarios (automática en el primer inicio de sesión) | `users.read` | SPEC-0001 |
| Usuarios y privilegios | Activar y desactivar usuarios | `users.update` | SPEC-0001 |
| Usuarios y privilegios | Crear, editar, activar y eliminar políticas | `policies.*` | SPEC-0001 |
| Usuarios y privilegios | Asignar y retirar políticas a un usuario (asignación de privilegios) | `users.policies.assign` / `revoke` | SPEC-0001 |
| Usuarios y privilegios | Consultar los permisos efectivos de un usuario | `users.read` | SPEC-0001 |
| Usuarios y privilegios | Administradores Root: promover y revocar | Root | SPEC-0001 |
| Usuarios y privilegios | Consultar el catálogo de permisos | `permissions.read` | SPEC-0001 |
| Auditoría | Consultar la auditoría | `audit.read` | SPEC-0001 |
| Inventario de equipos | Registrar un equipo y obtener su código | `devices.create` | SPEC-0003 |
| Inventario de equipos | Instalar el agente y enrolar el equipo (Windows, Linux, macOS) | Código del equipo | SPEC-0002, SPEC-0003 |
| Inventario de equipos | Editar un equipo, regenerar su código y eliminarlo | `devices.update` / `devices.delete` | SPEC-0003 |
| Inventario de equipos | Consultar el listado de equipos | `devices.read` | SPEC-0003 |
| Inventario de equipos | Consultar el historial y el informe de un equipo | `devices.reports.read` | SPEC-0003 |

El proceso del agente resume `agent/README.md` para quien instala y enlaza a la descarga; el detalle técnico sigue en
ese archivo.

### 8.4 Estados de la interfaz

| Situación | Comportamiento |
|---|---|
| Búsqueda sin resultados | "Ningún proceso coincide con la búsqueda." |
| Proceso inexistente (`/manual/<id>` desconocido) | "Este proceso no existe o se ha retirado del manual." con enlace al índice |
| Error al cargar | Mensaje de error estándar y botón "Reintentar" |

## 9. Criterios de aceptación

- [ ] **CA-1** Un visitante sin sesión abre `/manual` (o el enlace de la página de inicio de sesión) y ve todos los
  procesos agrupados por categoría, sin que se le pida iniciar sesión. Una persona con sesión llega también desde la
  navegación y el botón de ayuda.
- [ ] **CA-2** La búsqueda "enrolar" (o "Enrolar", "enrolár") muestra el proceso de instalación del agente.
- [ ] **CA-3** Cada proceso abre en `/manual/<id>`, muestra sus permisos, su spec de origen y su fecha, y la URL abre
  directamente esa página.
- [ ] **CA-4** El contenido inicial de §8.3 está publicado y describe los procesos tal como funcionan hoy.
- [ ] **CA-5** La prueba de coherencia (RF-7) falla si se añade una entrada de navegación sin proceso asociado o un
  proceso sin archivo, y pasa con el contenido inicial.
- [ ] **CA-6** El HTML generado a partir del Markdown se sanea: un `<script>` o un atributo `on*` en un archivo del
  manual no se ejecuta.
- [ ] **CA-7** La constitución está en la versión 1.2.0 con §10.4, y la plantilla de spec incluye la sección "Manual de uso".
- [ ] **CA-8** Las pantallas funcionan en tema claro y oscuro, con teclado y en pantalla estrecha.

## 10. Análisis arquitectónico

- **Sin backend:** nginx ya sirve los archivos de `public/`. No cambia la API, la base de datos ni Docker.
- **Ubicación:** `frontend/public/manual/` porque la imagen del frontend se construye con el contexto `./frontend`
  (ADR-0013, alternativas).
- **Renderizado:** el Markdown se convierte a HTML en el navegador con `marked` y se inserta con `[innerHTML]`, que
  Angular sanea (elimina scripts y atributos de evento). Es contenido propio y versionado, y la CSP
  (`script-src 'self'`) es una segunda barrera. Alternativa descartada: escribir el manual en HTML (más costoso de
  mantener).
- **Dependencia nueva (constitución §12.5):** `marked` (licencia MIT, sin dependencias, mantenida activamente). Se
  carga solo en la ruta del manual.
- **Ruta pública:** `/manual` se declara fuera del `Shell` (que exige sesión), como `/login`. Una barra superior propia
  (`layout/public-toolbar/`) reutiliza el selector de tema y consulta `AuthService` solo para elegir entre "Iniciar
  sesión" y "Volver a ITMas"; la sesión ya se carga al arrancar la aplicación, así que no hay llamadas nuevas.
- **Estructura frontend:** `features/manual/` con `manual.routes.ts`, `data/manual-api.ts` (lee `index.json` y los
  `.md` con `HttpClient`), `manual-index-page` y `manual-process-page`.
- **Caché:** nginx sirve `manual/index.json` con `Cache-Control: no-cache` para que el índice refleje siempre el último
  despliegue (cambio menor en `frontend/nginx/`, revisado por `security`).

### 10.1 Conformidad con la constitución

| Regla | Situación |
|---|---|
| §1.2 | La regla nueva se añade con la enmienda MINOR de ADR-0013 (1.2.0). |
| §6.2–6.4 | Componentes de Material; el HTML del manual solo usa estilos de layout y tokens `--mat-sys-*`. |
| §6.6 | Lectura con `HttpClient` y rutas relativas del mismo origen. |
| §3.5, RN-2 | El manual no es una frontera de seguridad ni contiene información confidencial. |
| §12.5 | `marked` justificada arriba. |

## 11. Cambios de frontend

| Ruta | Pantalla |
|---|---|
| `/manual` (pública) | Índice por categoría con búsqueda (`mat-form-field`, `mat-card` o `mat-list` por categoría, `mat-chip` para permisos) |
| `/manual/:id` (pública) | Proceso: título, chips de permisos, "Actualizado el … (SPEC-NNNN)", contenido y enlace al índice |

- Navegación de la aplicación: entrada "Manual de uso" (icono `menu_book`) en un grupo final "Ayuda", sin permiso.
- Barra superior de la aplicación: `mat-icon-button` con icono `help` y `aria-label="Manual de uso"`.
- Página de inicio de sesión: enlace "Consultar el manual de uso".
- Barra superior del manual: nombre de la aplicación, selector de tema y "Iniciar sesión" / "Volver a ITMas".

## 12. Cambios de backend

Ninguno.

## 13. Cambios de base de datos

Ninguno.

## 14. Consideraciones de seguridad

| Amenaza | Mitigación |
|---|---|
| **I** El manual es público y el servidor está en Internet: cualquiera lee los procesos y los nombres de los permisos | No da acceso a nada (los permisos se evalúan en la API, constitución §3.5). RN-2; revisión de `security` en cada cambio del manual; el contenido solo describe procesos visibles en la interfaz. |
| **I** Un visitante deduce si existe una sesión | La barra del manual usa el estado que la SPA ya obtiene de `/auth/session`, público desde SPEC-0000; no se expone nada nuevo. |
| **T/E** HTML o script inyectado en un archivo del manual | Contenido versionado y revisado; saneamiento de Angular; CSP `script-src 'self'` (CA-6). |
| **S** Manual desactualizado que lleva a un procedimiento inseguro (p. ej. compartir códigos por correo) | Regla §10.4 y prueba RF-7; el manual del enrolamiento repite las reglas de entrega segura del código. |

## 15. Pruebas

| Criterio | Tipo | Prueba prevista |
|---|---|---|
| CA-1, CA-2, CA-3 | Frontend | Rutas del manual sin `authGuard`; índice agrupado, búsqueda sin tildes ni mayúsculas, página de proceso y proceso inexistente |
| CA-5 | Frontend | Coherencia de `index.json`, archivos y `NAVIGATION` |
| CA-6 | Frontend | Markdown con `<script>` y `onerror` → no aparecen en el DOM |
| CA-4, CA-7 | Revisión | Contenido frente a las specs implementadas; versión de la constitución |
| CA-8 | Verificación documentada | Revisión visual en claro y oscuro, escritorio y móvil, teclado |

## 16. Plan de implementación (preliminar)

| # | Tarea | Agente | Archivos |
|---|---|---|---|
| 1 | ~~Enmienda 1.2.0, ADR-0013 → Accepted; plantilla, `agents/`, `AGENTS.md`, `specs/README.md`~~ Hecho el 2026-10-10 | architecture + security | `constitution.md`, `specs/**`, `agents/**`, `AGENTS.md` |
| 2 | Visor público del manual, barra propia, navegación, botón de ayuda y enlace en el login | frontend | `frontend/src/app/features/manual/**`, `layout/**`, `features/login/**`, `app.routes.ts` |
| 3 | Cabecera de caché de `index.json` | frontend + security | `frontend/nginx/**` |
| 4 | Contenido inicial (§8.3) | architecture | `frontend/public/manual/**` |
| 5 | Pruebas (RF-7, CA-6) y verificación visual | testing | `frontend/src/**/*.spec.ts` |
| 6 | Documentación: README (cómo consultar y mantener el manual), ARCHITECTURE, DEVELOPMENT | architecture | docs |

## 17. Trazabilidad

Implementada el 2026-10-10.

| Área | Archivos principales |
|---|---|
| Decisión y regla | [ADR-0013](../architecture/adr-0013-user-manual.md); constitución 1.2.0 (§10.3, §10.4); `specs/_templates/feature-spec.md` (§17 "Manual de uso"); `agents/README.md`, `agents/architecture.md`, `AGENTS.md`, `specs/README.md` |
| Contenido | `frontend/public/manual/index.json` y 16 procesos `*.md` |
| Visor | `frontend/src/app/features/manual/` (`manual-index-page`, `manual-process-page`, `data/`), `layout/public-layout/`, `app.routes.ts` (ruta pública), `layout/navigation/navigation-items.ts` (grupo "Ayuda"), `layout/shell/` (botón de ayuda), `features/login/login-page.ts` (enlace) |
| Servidor web | `frontend/nginx/default.conf.template` (`location = /manual` y `^~ /manual/`) |
| Dependencias | `marked` 18.0.14 (MIT, sin dependencias, solo en la ruta del manual); `@types/node` 24.13.6 (solo desarrollo, para la prueba de coherencia). Versiones exactas. |
| Documentación | README ("Manual de uso"), DEVELOPMENT §8.1 (cómo mantenerlo), ARCHITECTURE, SECURITY, `specs/security/threat-model-baseline.md` |

| Criterio | Prueba o verificación |
|---|---|
| CA-1 | `manual-pages.spec.ts` (ruta sin guard, índice agrupado con permisos); verificación en la pila local sin sesión: enlace desde `/login` → `/manual` |
| CA-2 | `manual-pages.spec.ts` (`ENROLÁR` → proceso de instalación); verificación manual con la misma búsqueda |
| CA-3 | `manual-pages.spec.ts` (metadatos, un solo `h1`, enlaces internos); verificación: `/manual/instalar-agente` abierto directamente y enlace a `registrar-equipo` sin recargar la página |
| CA-4 | Revisión del contenido frente a las pantallas actuales (textos de botones y mensajes tomados del código) |
| CA-5 | `manual-consistency.spec.ts` (archivos e índice, ids, categorías, enlaces, cobertura de `NAVIGATION`) |
| CA-6 | `manual-pages.spec.ts` (`<script>` y `onerror` eliminados); CSP `script-src 'self'` comprobada en las cabeceras de `/manual/` |
| CA-7 | Constitución 1.2.0; plantilla con la sección 17 |
| CA-8 | Verificación en el navegador: claro y oscuro, escritorio y móvil (375 px), tarjetas y enlaces navegables con teclado |

Totales: frontend 69 pruebas (11 nuevas del manual); backend sin cambios.

**Ajustes de implementación** (no cambian el comportamiento aprobado):
- Se añadió el proceso "Consultar este manual", necesario para que la entrada de navegación "Manual de uso" tenga su
  explicación (RF-7).
- RNF-3: todos los archivos del manual se sirven con `Cache-Control: no-cache` (no solo `index.json`). Son pequeños y
  se revalidan con `ETag`, así que un despliegue se ve al momento.
- `/manual/` combina estáticos y rutas de la SPA: un `.md` o `.json` inexistente responde `404`; cualquier otra ruta
  (`/manual/<id>`) sirve la aplicación. Solo admite `GET` y `HEAD`.
- La página de un proceso solo pide archivos cuyo `id` está en el índice: la URL nunca elige un archivo arbitrario.

## 18. Decisiones y preguntas abiertas

### 18.1 Primera ronda (2026-10-10)

| # | Tema | Decisión del solicitante |
|---|---|---|
| ADR-0013 | Enmienda de la constitución | Aprobada: constitución 1.2.0, §10.4. |
| P-1 | Acceso | **Público**: no hace falta iniciar sesión ni privilegios (cambia la propuesta inicial). |
| P-2 | Visibilidad | Todos los procesos, indicando el permiso necesario. |
| P-3 | Audiencia | Sin procesos de TI fuera de la interfaz; siguen en README y SECURITY. |
| P-4 | Capturas | No por ahora. |
| P-5 | PDF | No; se imprime desde el navegador. |
| P-6 | Ayuda contextual | No por ahora; el campo `routes` del índice lo deja preparado. |

### 18.2 Preguntas abiertas

Ninguna.
