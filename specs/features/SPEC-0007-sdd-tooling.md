---
id: SPEC-0007
title: Skills y guardas para el flujo SDD
type: feature
status: Implemented
owner: jmartinez
created: 2026-10-17
approved-by: jmartinez (2026-10-17)
agents: [architecture, security, testing]
related: [SPEC-0004, ADR-0013]
---

# SPEC-0007 — Skills y guardas para el flujo SDD

> Derivada de la consulta del solicitante (2026-10-17) sobre cómo mejorar el desarrollo de ITMas con skills de
> Claude Code. Las propuestas de `architecture` se identifican con **P-n** y las preguntas con **Q-n**, ya
> respondidas el 2026-10-17 (§19.2). **Aprobada el 2026-10-17** con las propuestas de §19.1 (§19.3).

## 1. Objetivo

Que las reglas del flujo SDD que hoy dependen de que alguien las recuerde (implementar solo con spec `Approved`,
commits con el ID de la spec, manual de uso al día, no tocar secretos) se ejecuten con **procedimientos repetibles**
(skills de proyecto de Claude Code) y **guardas mecánicas**, sin cambiar ninguna regla de la constitución ni el
código de la aplicación.

## 2. Contexto

Las reglas viven en prosa: [`constitution.md`](../../constitution.md), [`AGENTS.md`](../../AGENTS.md) y
[`agents/`](../../agents/README.md). El repositorio no tiene carpeta `.claude/`, ni hooks de git, ni CI. Cumplirlas
depende de la memoria de la persona o de la herramienta de IA que trabaje, y ya hay señales de que eso falla:

1. **El comando documentado para calcular el siguiente ID da un falso positivo.** `specs/README.md` indica
   `grep -rho "SPEC-[0-9]\{4\}" specs | sort | tail -1`. Hoy devuelve `SPEC-0007`, porque el propio README usa
   `SPEC-0007` como ejemplo de mensaje de commit, aunque la última spec real es SPEC-0006. Aplicado al pie de la
   letra, se salta un número.
2. **Los índices se mantienen a mano y ya divergen.** `specs/features/README.md` marca SPEC-0005 como `Approved`;
   la spec y `specs/README.md` dicen `Implemented`.
3. **"Nunca leer ni modificar `.env`"** (AGENTS.md) es una instrucción, no un control. Hay un `.env` real en el
   directorio de trabajo.
4. **El prefijo `SPEC-NNNN:` en los commits se cumple** (los 8 commits actuales lo llevan) **pero nada lo verifica.**
5. **AGENTS.md lista el manual de uso** entre las reglas que más se olvidan (constitución §10.4).

## 3. Alcance

**Incluye**
- Cuatro skills de proyecto en `.claude/skills/`: `sdd-new-spec`, `sdd-implement`, `sdd-validate` y `update-manual`.
- `.claude/settings.json` versionado, con reglas que impiden a Claude Code leer o editar archivos de secretos.
- Un hook `commit-msg` de git versionado en `.githooks/`, su script de activación y su prueba automatizada.
- Documentación: DEVELOPMENT.md, AGENTS.md, `agents/README.md`, `agents/architecture.md` y `.gitignore`.
- Corrección del comando de siguiente ID en `specs/README.md` y de la fila de SPEC-0005 en
  `specs/features/README.md` (erratas; constitución §11.6).

**Excluye**
- Cambios en `backend/`, `frontend/`, `agent/`, `database/`, Docker o en el manual de uso.
- Cualquier enmienda de la constitución: las skills y guardas automatizan reglas existentes, no añaden ni relajan ninguna.
- Subagentes de Claude Code (`.claude/agents/`) que envuelvan los roles de `agents/` (diferido, P-7).
- Integración continua (diferido, P-7).
- Skills adicionales como `/add-permission` o `/new-migration` (candidatas para una spec posterior).
- Instalar o modificar skills y plugins de terceros o globales de cada persona.

## 4. Actores

| Actor | Descripción |
|---|---|
| Persona desarrolladora | Invoca las skills y hace los commits. |
| Solicitante / aprobador | Única persona que pasa una spec a `Approved` (§11.3). |
| Herramienta de IA | Claude Code. Ejecuta las skills bajo los permisos de `.claude/settings.json`. |
| Agentes de rol | `architecture` (propietario), `security` (revisor obligatorio) y `testing` (revisa `sdd-validate` y la prueba del hook). |

## 5. Requerimientos

### 5.1 Funcionales

- **RF-1 `/sdd-new-spec <idea>`.** Calcula el siguiente `SPEC-NNNN` a partir de los **nombres de archivo**
  `specs/**/SPEC-NNNN-*.md` (no de menciones en texto). Elige la plantilla según el tipo (`feature`, `database`,
  `security`) y crea el archivo en su carpeta con `status: Draft`, `approved-by` vacío y la fecha del día. Añade la
  fila al índice general `specs/README.md` y al `README.md` de su carpeta. Lo que la idea no especifica lo deja como
  pregunta en la sección "Preguntas abiertas"; no lo supone (§11.4).
- **RF-2 `/sdd-implement SPEC-NNNN`.** Solo procede si la spec está `Approved` o `In Progress`; con cualquier otro
  estado se niega y no modifica archivos. Lee la constitución y AGENTS.md, determina los agentes con el campo `agents`
  de la spec y la matriz de `agents/README.md`, pasa la spec a `In Progress`, ejecuta el plan de implementación en
  orden respetando `owns`/`forbidden`, lista las revisiones `consults` pendientes y, si la sección "Manual de uso" no
  es "Ninguno", aplica el procedimiento de RF-4. Termina proponiendo el mensaje de commit y recomendando `/sdd-validate`.
- **RF-3 `/sdd-validate [SPEC-NNNN]`.** Ejecuta las comprobaciones de la tabla de comandos de AGENTS.md que
  correspondan a lo modificado (leyendo la tabla en el momento de ejecutar, sin copiarla). Verifica que cada criterio
  de aceptación de la spec tiene prueba o verificación documentada (§9.2), que se cumplen los *checklists* de salida de
  `testing`, `security` y `architecture`, que la documentación y el manual se actualizaron cuando §10.1 y §10.4 lo
  exigen, y que el estado de la spec coincide en su archivo y en los dos índices. Informa por ítem con evidencia
  (comando y resumen de la salida). Lo que no pudo ejecutar (por ejemplo, Docker apagado) lo informa como **no
  verificado** y no da la validación por completa.
- **RF-4 `/update-manual [SPEC-NNNN]`.** Lee la sección "Manual de uso" de la spec y, para cada proceso, crea o
  actualiza `frontend/public/manual/<id>.md` y su entrada en `index.json`, según DEVELOPMENT.md §8.1. Declara
  expresamente que revisó la ausencia de secretos, datos personales reales, nombres o IPs internos y versiones de
  componentes (el manual es público). Ejecuta las pruebas del frontend, que incluyen `manual-consistency.spec.ts`.
- **RF-5 Invocación explícita.** Las skills solo se ejecutan cuando una persona las invoca; no se activan solas.
- **RF-6 Reglas de bloqueo de secretos.** `.claude/settings.json` impide a Claude Code leer y editar `.env` y sus
  variantes, `secrets/`, los archivos de claves y certificados que `.gitignore` ya clasifica como secretos
  (`*.pfx`, `*.p12`, `*.pem`, `*.key`, `*.crt`, `*.cer`) y, por decisión del solicitante tras la revisión de `security`
  (§19.4), la configuración local que `.gitignore` también lista (`appsettings.*.local.json`,
  `docker-compose.override.yml`). `.env.example` sigue siendo legible y editable (§4.3).
- **RF-7 Hook `commit-msg`.** Rechaza el commit si el primer renglón del mensaje no empieza por uno o más IDs
  `SPEC-NNNN` o `ADR-NNNN` separados por coma y espacio, o por el prefijo `minor`, seguidos de `: ` y un texto
  (`^(((SPEC|ADR)-[0-9]{4})(, (SPEC|ADR)-[0-9]{4})*|minor): .+`, p. ej. `SPEC-0000, SPEC-0001: …` como en el
  historial). El prefijo `minor: <motivo>` identifica las correcciones menores sin spec que permite la
  constitución §11.6 (Q-2) y exige el motivo. Deja pasar los mensajes generados por git (`Merge `, `Revert "`,
  `fixup! `, `squash! `, `amend! `). El mensaje de rechazo explica el formato aceptado.
- **RF-8 Activación del hook.** Un script `scripts/install-git-hooks.sh`, idempotente, configura
  `core.hooksPath` a `.githooks`. DEVELOPMENT.md §1 lo incluye entre los pasos de instalación.

### 5.2 No funcionales

- **RNF-1 Fuente única de verdad.** Las skills no copian reglas: en cada ejecución leen `constitution.md`,
  `AGENTS.md`, `agents/` y las plantillas. Cambiar una regla no obliga a editar una skill salvo que cambie el
  procedimiento.
- **RNF-2 Mínimo privilegio.** Cada skill pide solo las herramientas que necesita cuando el mecanismo lo permite.
  Ninguna ejecuta `git push`; ninguna ejecuta `git commit` sin petición explícita; ninguna toca `.env`.
- **RNF-3 Idioma.** El contenido de las skills y los mensajes del hook, en español (§12.4); los identificadores
  (nombres de skills y de scripts), en inglés.
- **RNF-4 Simplicidad.** Sin dependencias nuevas: skills en Markdown y scripts en `sh` POSIX, como los de `scripts/`
  (§2.5, §12.5).
- **RNF-5 Contrato multiherramienta.** AGENTS.md sigue siendo el contrato para cualquier herramienta de IA; las
  skills son una comodidad de Claude Code y no lo sustituyen.

## 6. Reglas de negocio

Son reglas de proceso (no hay dominio de aplicación afectado).

- **RN-1** Solo una persona aprueba. Ni una skill ni el hook pasan una spec a `Approved` ni rellenan `approved-by`
  (§11.3; `agents/architecture.md`).
- **RN-2** Si el procedimiento de una skill choca con la constitución, prevalece la constitución (§1.3) y la skill se
  corrige.
- **RN-3** Las guardas **no son una frontera de seguridad.** Complementan §4 (que no existan secretos en el
  repositorio) y no la sustituyen. `git commit --no-verify` y las herramientas ajenas a Claude Code las eluden.
- **RN-4** No se hace commit ni push sin petición explícita de la persona. La historia sigue siendo lineal en `main`,
  con un commit `SPEC-NNNN:` por spec.
- **RN-5** Lo no especificado se pregunta, no se supone (§11.4).
- **RN-6** `minor:` es solo para lo que §11.6 permite sin spec (erratas, dependencias de parche, refactors locales sin
  cambio de comportamiento). El hook no puede comprobarlo: lo decide quien hace el commit y lo revisa quien revisa el
  cambio.

## 7. Permisos

No aplica: esta spec no añade endpoints, pantallas ni permisos de ITMas.

## 8. Comportamiento esperado

### 8.1 Ciclo de una spec con las skills

```text
persona:  /sdd-new-spec <idea>        → SPEC-NNNN en Draft + índices + preguntas abiertas
persona:  responde, revisa y aprueba  → status: Approved (a mano; ninguna skill lo hace)
persona:  /sdd-implement SPEC-NNNN    → In Progress; código, pruebas y docs; sin commit
persona:  /sdd-validate SPEC-NNNN     → informe por ítem, con lo no verificado señalado
persona:  commit "SPEC-NNNN: …"       → el hook valida el prefijo; push solo si se pide
```

`/update-manual` se invoca solo o lo aplica `/sdd-implement` cuando la spec cambia algo visible.

### 8.2 Rechazo del hook

```text
Mensaje de commit rechazado. El primer renglón debe empezar por un ID de spec o ADR:
  SPEC-0007: texto del cambio
  SPEC-0000, SPEC-0001: texto del cambio
Correcciones menores sin spec (§11.6), con el motivo:
  minor: texto del cambio
```

### 8.3 Cuando una skill no puede cumplir

Spec en `Draft`, plantilla ausente, Docker apagado, prueba que falla: la skill se detiene o lo informa como **no
verificado**, dice qué falta y no improvisa ni da el resultado por bueno.

## 9. Criterios de aceptación

- [ ] **CA-1** Dado que las specs existentes llegan hasta `SPEC-NNNN` por nombre de archivo, cuando se invoca
  `/sdd-new-spec`, entonces propone `NNNN+1` aunque el texto de `specs/README.md` mencione otros IDs como ejemplo;
  crea el archivo desde la plantilla con `status: Draft`, `approved-by` vacío y la fecha del día, y añade la fila en
  ambos índices.
- [ ] **CA-2** Dado cualquier uso de las skills o del hook, entonces ningún archivo queda con `status: Approved` ni con
  `approved-by` relleno por ellas.
- [ ] **CA-3** Dada una spec en `Draft`, cuando se invoca `/sdd-implement`, entonces se niega y no modifica archivos;
  dada una spec `Approved`, procede y la deja en `In Progress`.
- [ ] **CA-4** Dado que se ejecutó `/sdd-implement` o `/update-manual`, entonces no se ejecutó `git commit` ni
  `git push` sin petición explícita.
- [ ] **CA-5** Dado un cambio con spec, cuando se invoca `/sdd-validate`, entonces ejecuta los comandos aplicables de
  la tabla de AGENTS.md, informa por criterio de aceptación y detecta una divergencia de estado entre la spec y los
  índices (como la de SPEC-0005 antes de corregirla). Si un comando no se puede ejecutar, lo informa como **no
  verificado**.
- [ ] **CA-6** Dada una spec con procesos en "Manual de uso", cuando se invoca `/update-manual`, entonces crea o
  actualiza los `.md` y el `index.json`, `manual-consistency.spec.ts` pasa y la skill declara la revisión de contenido
  no público.
- [ ] **CA-7** Con las reglas de RF-6 activas, Claude Code no puede leer ni editar `.env`, `secrets/`, los archivos
  de claves y certificados ni `appsettings.*.local.json` y `docker-compose.override.yml`, y sí puede leer y editar
  `.env.example`, `appsettings.json`, `appsettings.Development.json` y `docker-compose.yml`. El alcance exacto (herramientas de lectura y
  edición frente a comandos de shell) y sus límites quedan documentados en DEVELOPMENT.md.
- [ ] **CA-8** Con el hook activo, todos los mensajes del historial de `main` al momento de implementar y los
  generados por git son aceptados, igual que `minor: corregir errata`; son rechazados `update stuff`, `spec-0007: x`,
  `SPEC-7: x`, `SPEC-0007 x`, `SPEC-0007:` sin texto, `minor:` sin motivo, `Minor: x` y el mensaje vacío.
- [ ] **CA-9** `scripts/install-git-hooks.sh` es idempotente (se puede ejecutar dos veces con el mismo resultado) y no
  requiere ni lee `.env`.
- [ ] **CA-10** `.claude/settings.local.json` queda ignorado por git; ningún archivo nuevo contiene secretos ni rutas
  propias del equipo de una persona.
- [ ] **CA-11** DEVELOPMENT.md describe las skills, el hook, su activación y los límites de las guardas; AGENTS.md
  las referencia; `agents/README.md` y `agents/architecture.md` asignan `.claude/**` y `.githooks/**` a `architecture`
  con revisión de `security`; `specs/README.md` usa un comando de siguiente ID sin falsos positivos.

## 10. Análisis arquitectónico

### 10.1 Impacto por capa

Ninguno en el producto: backend, frontend, agente, base de datos y Docker no cambian. Es herramienta de desarrollo y
documentación.

### 10.2 Alternativas consideradas

| Alternativa | Decisión |
|---|---|
| Seguir con las reglas solo en prosa | Descartada: ya produce divergencias (§2, puntos 1 y 2). |
| Skills de proyecto en `.claude/skills/` (P-1) | **Elegida:** versionadas, revisables como cualquier archivo y sin dependencias. |
| Skills o plugins globales de cada persona | Descartada como base: no se versionan y cada persona tendría un conjunto distinto. Pueden usarse de forma complementaria. |
| Envolver `agents/*.md` como subagentes de Claude Code | Diferida (P-7): duplicaría el bloque YAML; podría hacerse después con archivos que apunten a `agents/`. |
| Integración continua que ejecute la tabla de comandos | Diferida (P-7): es el control real de la fase *Validation*, pero implica decidir el alojamiento y no existe hoy `.github`. `/sdd-validate` es el sustituto local. |
| Hook de git en `.git/hooks/` | Descartada: no se versiona. Se usa `.githooks/` con `core.hooksPath`. |

### 10.3 ¿Requiere ADR?

**No** (Q-1, resuelta el 2026-10-17). No se añade ni se relaja ninguna regla de la constitución (§1.2), no hay librería, patrón ni
servicio nuevo en el producto (§2.5) y la decisión queda registrada en esta spec.

### 10.4 Conformidad con la constitución

§11.2 y §11.5 (el hook y las skills refuerzan la trazabilidad y los límites de `agents/`), §11.3 y §11.4 (RN-1, RN-5),
§4 (RF-6 es un complemento, no un reemplazo), §10.3 y §10.4 (RF-3 y RF-4), §9.2 (verificación documentada de los
criterios), §12.4 (idioma) y §2.5 y §12.5 (sin dependencias nuevas).

### 10.5 Encaje con `agents/`

`.claude/**` y `.githooks/**` no figuran hoy en la matriz de `agents/README.md`. Se asignan a `architecture`, con
revisión obligatoria de `security`, porque definen permisos y ejecutan código. `scripts/**` ya es de `architecture`,
revisado por `security`. La prueba del hook la revisa `testing`.

### 10.6 Riesgos

| Riesgo | Mitigación |
|---|---|
| Las skills se desfasan de las reglas | RNF-1: leen las fuentes en ejecución; RN-2: prevalece la constitución. |
| Falsa sensación de seguridad con las reglas de bloqueo | RN-3 y CA-7: los límites quedan escritos en DEVELOPMENT.md. |
| El hook bloquea commits legítimos | El patrón se contrasta con el historial real (CA-8) y admite el prefijo `minor:` para correcciones menores (Q-2). |
| Uso de `minor:` para eludir la spec | RN-6: el hook no puede comprobarlo; lo cubre la revisión del cambio. La verificación centralizada queda para CI (P-7). |
| Skills largas o ambiguas que se ignoran | Cada skill hace un solo procedimiento, con pasos numerados y condiciones de parada explícitas (§8.3). |

## 11. Cambios de frontend

Ninguno.

## 12. Cambios de backend

Ninguno.

## 13. Cambios de base de datos

Ninguno.

## 14. Consideraciones de seguridad

No se almacenan ni procesan datos de la aplicación. Las skills y el hook operan sobre archivos del repositorio.

### 14.1 Amenazas (STRIDE)

| Amenaza | Mitigación |
|---|---|
| **I** Lectura o edición accidental de secretos por una herramienta de IA | RF-6 (reglas de bloqueo) y RN-3. El control de fondo sigue siendo §4: sin secretos en el repositorio y `.env` con permisos `600` (`init-env.sh`). |
| **T/E** El hook ejecuta código dañino o inyectable a partir del mensaje de commit | `sh` POSIX versionado y revisado por `security`; el mensaje es entrada no confiable: se compara con el patrón, nunca se evalúa ni se interpola en un comando; sin red. |
| **T** Cambio malicioso o erróneo en `.claude/settings.json` que amplíe permisos | Revisión obligatoria de `security` para todo cambio en `.claude/**` y `.githooks/**`. |
| **E** Skills con más permisos de los necesarios | RNF-2. |
| **T** Una skill ejecuta comandos tomados de contenido leído (specs, salidas de comandos) | `/sdd-validate` solo ejecuta los comandos de la tabla de AGENTS.md, que está versionada y revisada; el resto del contenido leído se trata como datos. |
| **R** Evasión del hook con `--no-verify` | No es un control de seguridad (RN-3). La trazabilidad se mantiene en la revisión de commits y PR; la verificación centralizada queda para CI (P-7). |

### 14.2 Revisión de `security`

Obligatoria para: `.claude/settings.json`, `.githooks/commit-msg`, `scripts/install-git-hooks.sh`, el cambio en
`.gitignore` y el alcance exacto de RF-6.

## 15. Pruebas

| Criterio | Tipo | Prueba |
|---|---|---|
| CA-8 | Automatizada (script) | `scripts/test-commit-msg-hook.sh` alimenta el hook con los mensajes válidos e inválidos de CA-8 y con los asuntos del historial real al 2026-10-17 (*fixtures* congeladas, para que la prueba no dependa del estado de git), comprueba el código de salida y además hace commits reales en un repositorio temporal. Se añade a la tabla de comandos de AGENTS.md. |
| CA-9 | Automatizada (script) | La misma prueba ejecuta dos veces `install-git-hooks.sh` en un repositorio temporal y compara `core.hooksPath`. |
| CA-1 a CA-6 | Verificación documentada | Cada skill se ejecuta sobre un escenario real y se registra en §18 lo que hizo. `/sdd-new-spec` se usa para crear la siguiente spec del repositorio (verificación con uso real). Los casos de negativa de CA-3 se ejercitan con una spec `Draft`. |
| CA-7 | Verificación documentada | Intentar leer y editar `.env`, `secrets/` y `.env.example` desde Claude Code y registrar cuáles se bloquean. |
| CA-10, CA-11 | Verificación documentada | `git check-ignore`, `git grep` de secretos y revisión de los archivos listados. |

Las skills son procedimientos que ejecuta un modelo, no código determinista; por eso no tienen pruebas automatizadas
(§9.2 admite verificación documentada).

## 16. Plan de implementación

| # | Tarea | Agente | Archivos |
|---|---|---|---|
| 1 | Corregir el comando de siguiente ID y la fila de SPEC-0005 (erratas) | architecture | `specs/README.md`, `specs/features/README.md` |
| 2 | Skill `sdd-new-spec` | architecture | `.claude/skills/sdd-new-spec/SKILL.md` |
| 3 | Skill `sdd-implement` | architecture | `.claude/skills/sdd-implement/SKILL.md` |
| 4 | Skill `sdd-validate` (revisa `testing`) | architecture, testing | `.claude/skills/sdd-validate/SKILL.md` |
| 5 | Skill `update-manual` | architecture | `.claude/skills/update-manual/SKILL.md` |
| 6 | Reglas de bloqueo e ignorar `settings.local.json` (revisa `security`) | architecture, security | `.claude/settings.json`, `.gitignore` |
| 7 | Hook, activación y prueba (revisan `security` y `testing`) | architecture, security, testing | `.githooks/commit-msg`, `scripts/install-git-hooks.sh`, `scripts/test-commit-msg-hook.sh` |
| 8 | Documentación y matriz de agentes | architecture | `DEVELOPMENT.md`, `AGENTS.md`, `agents/README.md`, `agents/architecture.md` |
| 9 | Validación (§15), revisión de `security`, trazabilidad y estado `Implemented` | testing, security, architecture | esta spec |

## 17. Manual de uso

**Ninguno.** El manual describe procesos de las personas usuarias de ITMas y esta spec no cambia ninguna pantalla ni
proceso. La guía para quienes desarrollan va en DEVELOPMENT.md.

## 18. Trazabilidad (al implementar)

- Commits/PRs: `616d42b` (aprobación), `4ef5f8f` (implementación), `a2d8cc3` (verificación y arreglos de las skills) y
  el commit que pasa la spec a `Implemented`. Sin PR: historia lineal en `main`.
- Archivos principales:
  - Skills: `.claude/skills/{sdd-new-spec,sdd-implement,sdd-validate,update-manual}/SKILL.md`.
  - Guardas: `.claude/settings.json`, `.githooks/commit-msg`, `scripts/install-git-hooks.sh`, `.gitignore`.
  - Documentación: `DEVELOPMENT.md` (§1, §2, §7, §9 y la nueva §10), `AGENTS.md`, `agents/README.md`,
    `agents/architecture.md`, `README.md`. Erratas en `specs/README.md` y `specs/features/README.md`.
- Pruebas: `scripts/test-commit-msg-hook.sh` (54 comprobaciones, también con `dash`). Se probó además contra ocho
  versiones deliberadamente rotas del hook y del instalador (patrón que acepta todo, mensaje evaluado, instalador sin
  `chmod` ni aviso, IDs de 3 dígitos, líneas en blanco iniciales, sin `amend! `, sin `unset GREP_OPTIONS`) y detectó las
  ocho.
- Manual de uso actualizado: no aplica.

### 18.1 Verificación de criterios (2026-10-17)

| Criterio | Estado | Evidencia |
|---|---|---|
| CA-1 | **Verificado** (con un límite) | `/sdd-new-spec Prueba de verificación de skills (descartable)`, escrita por la persona en una sesión de Claude Code con el árbol en `4ef5f8f`. Propuso `SPEC-0008`: el mayor ID por nombre de archivo era `SPEC-0007` y no existía ningún archivo ni texto con `SPEC-0008`. Creó `specs/features/SPEC-0008-skills-verification-test.md` desde la plantilla (los mismos 19 encabezados `##`, diff vacío) con `status: Draft`, `approved-by` vacío y `created: 2026-10-17` (igual a `date +%F`), y añadió una fila `Draft` en `specs/README.md` y otra en `specs/features/README.md` (el `git diff` muestra solo esas dos líneas). No inventó requisitos: dejó seis `Q-n` y dos `P-n` en §19 y el resto de secciones con la indicación de la plantilla. **Límite:** el texto de los README solo mencionaba IDs que ya existían, así que el mayor por texto y el mayor por nombre coincidían. Se probó el cálculo por nombre de archivo, no el caso del comando antiguo (un ID en el texto mayor que el último archivo). |
| CA-2 | **Verificado** para las cuatro skills | Tras ejecutarlas, el único `status: Approved` de `specs/` es el de SPEC-0006, aprobada por una persona en el commit `73222e0`; SPEC-0008 quedó `Draft` con `approved-by` vacío y el `approved-by` de SPEC-0007 sigue siendo el de la persona. El hook no se ejercitó con commits reales en esta verificación (CA-8 lo prueba en repositorios temporales). |
| CA-3 | **Parcial** | Mitad «Draft»: `/sdd-implement SPEC-0008` localizó la spec, leyó `status: Draft` y `approved-by` vacío, se detuvo en el paso 2 y no modificó nada: `git status --short` y `git diff --stat` fueron idénticos antes y después (`M` en los dos README y `??` en SPEC-0008). Mitad «Approved»: **no ejercitada.** La única spec `Approved` es SPEC-0006 y ejecutarla habría empezado a implementar la agenda de infraestructura. Falta comprobar que una spec `Approved` procede y queda `In Progress` en su frontmatter y en los dos índices. |
| CA-4 | **Parcial** | Tras `/update-manual SPEC-0007` y `/sdd-implement SPEC-0008` no hay commits nuevos (`git log 4ef5f8f..HEAD` vacío) y la sesión no ejecutó ningún `git push`. Pero las dos skills terminaron en su camino de parada (paso 1 y paso 2), antes de editar nada: no se probó que no hagan commit cuando sí trabajan. |
| CA-5 | **Verificado** (con un límite) | Primera ejecución, `/sdd-validate SPEC-0007`: leyó la tabla de `AGENTS.md` y ejecutó solo `git status --short`, `git diff --name-only` y `./scripts/test-commit-msg-hook.sh` (54 comprobaciones en verde); no leyó `.env` ni ejecutó `docker compose config`. Informó por criterio de aceptación (CA-1 a CA-11) y por ítem de los *checklists*, marcó como **NO VERIFICADO** lo que no ejecutó (pruebas de backend y frontend, Compose, auditorías, `git check-ignore`) y cerró con «Validación incompleta». El estado coincidía: `In Progress` en los tres sitios. Segunda ejecución, con una divergencia provocada: sobre una spec `Draft` descartable (SPEC-0008, creada con `/sdd-new-spec`) se cambió a mano la fila de `specs/README.md` a `In Progress`, dejando el frontmatter y `specs/features/README.md` en `Draft`. `/sdd-validate SPEC-0008` la marcó como **FALLA** («frontmatter `Draft` frente a fila `In Progress`»), dio por buena la fila de `specs/features/README.md`, no ejecutó ningún comando (ninguna ruta tocada tiene comando en la tabla; lo dijo como **NO VERIFICADO**), marcó **SIN COBERTURA** los criterios que seguían con el texto de la plantilla y **FALLA** el análisis y el plan sin completar, y cerró con «Validación incompleta». **Límite:** la divergencia se provocó a propósito, no es la de SPEC-0005 original, y esta ejecución cargó la copia de las skills del clon principal, sin los arreglos de las observaciones 1 y 2. |
| CA-6 | **Parcial** | `/update-manual SPEC-0007` leyó §17 («Ninguno»), se detuvo en el paso 1, lo informó y no modificó nada (`git status --short` vacío). **No ejercitada** la mitad que crea o actualiza los `.md` y `index.json`, pasa `manual-consistency.spec.ts` y declara la revisión de contenido no público: ninguna spec tiene hoy procesos en «Manual de uso». |
| RF-5 (no es un CA) | **Comprobado** | Al intentar ejecutar `/sdd-validate` desde el modelo, la herramienta `Skill` lo rechazó por `disable-model-invocation`. Las cuatro solo se ejecutaron cuando la persona las escribió en el chat. |
| CA-7 | Verificado | Ficheros señuelo, nunca `.env` ni `secrets/`, con Claude Code 2.1.291. Resultados y límites en `DEVELOPMENT.md` §10.2. |
| CA-8, CA-9 | Verificado | `./scripts/test-commit-msg-hook.sh`: 54 comprobaciones en verde. |
| CA-10 | Verificado | `git check-ignore` confirma `.claude/settings.local.json`; escaneo de rutas propias, claves y credenciales en los archivos nuevos, sin hallazgos. |
| CA-11 | Verificado | Documentación y matriz de agentes actualizadas; el comando de siguiente ID devuelve el último ID real. |

La verificación de CA-1 a CA-6 la hizo una persona en una sesión de Claude Code abierta sobre un *worktree* en `4ef5f8f`. La copia de las skills que cargó Claude Code fue la del clon principal; es idéntica a la del *worktree* (`diff -r` sin diferencias). Observaciones del uso real. Los arreglos 1 a 4 se aplicaron el 2026-10-17, sin commit. Las skills no se volvieron a ejecutar tras el cambio. La revisión de `security` que exige cambiar `.claude/**` se hizo (§18.5) y obligó a corregir la primera versión de los arreglos 1 a 3.

1. **Las skills piden `Glob` y `Grep`, que una sesión puede no tener.** Esta sesión no las exponía y se sustituyeron por `find` y `grep` de solo lectura en Bash. En una sesión así, `allowed-tools: Read, Glob, Grep` no preaprueba nada y esos comandos piden permiso. Arreglo aplicado: cada skill indica que, si `Glob` o `Grep` no existen, se usan solo `git ls-files -co --exclude-standard -- <ruta>` y `git grep -nE --untracked -e <patrón> -- <ruta>`, con ruta acotada y nunca `find` ni `grep -r` (la primera versión usaba `find` y `grep` en Bash; la corrigió la revisión de §18.5); no se amplió `allowed-tools` (hallazgo medio de §18.4) y `DEVELOPMENT.md` §10.1 lo documenta.
2. **`/sdd-validate` con el árbol limpio no sabe qué ejecutar.** Los pasos 2 y 3 deducen el alcance de `git status` y `git diff`, que para una spec ya confirmada salen vacíos. Se eligió la prueba del hook a partir de las rutas de la sección «Trazabilidad» de la spec. Arreglo aplicado: el paso 2 usa esas rutas como respaldo cuando ambos comandos salen vacíos y lo dice en el informe; con ese respaldo el veredicto máximo es «Validación incompleta: alcance no contrastado con git» (§18.5, hallazgo 3).
3. **Las skills solo aparecen en sesiones abiertas sobre un commit que las contenga.** Este *worktree* se creó desde `73222e0`, sin `.claude/skills/` ni SPEC-0007; hubo que avanzarlo a `4ef5f8f` (fast-forward local, sin commit). Arreglo aplicado: aviso en `DEVELOPMENT.md` §10.1 y en su tabla de problemas frecuentes; avisa también de que ese *worktree* no tiene las reglas de bloqueo de secretos y de que el `git merge` lo ejecuta una persona, no una skill (§18.5, hallazgo 4).
4. **El paso 2 de `/sdd-validate` dice algo que `git diff --name-only` no hace.** El texto afirma que ambos comandos incluyen «lo que aún no tiene seguimiento», pero `git diff --name-only` solo lista lo modificado con seguimiento: SPEC-0008, recién creada, apareció en `git status --short` (`??`) y no en `git diff --name-only`. Arreglo aplicado: el paso 2 dice que `git status --short` es el que muestra lo que no tiene seguimiento y que `git diff --name-only` solo lo modificado con seguimiento.

### 18.2 Desviaciones respecto al texto de la spec

- **Fixtures del historial (§15):** la prueba usa los asuntos del historial como *fixtures* congeladas en lugar de leer
  `git log`.
- **P-5, reglas de Bash:** no se añadieron. Las reglas por prefijo de comando son fáciles de eludir (la revisión de
  `security` lo comprobó con `sed`, `awk` y `cp`) y dan una falsa sensación de control; el límite queda documentado.
- **P-5, `.env.example`:** se excluye con la negación `!` de las reglas de permisos, no enumerando variantes de `.env.*`.
- **RF-7, `amend! `:** se añade a los mensajes generados por git que pasan sin comprobar (`git commit --fixup=amend:`).
- **RF-6, ampliación:** por decisión del solicitante tras la revisión de `security` (§19.4), se bloquean también
  `appsettings.*.local.json` y `docker-compose.override.yml`.
- **RF-3 y RF-4, argumento:** el ID de la spec es obligatorio; si falta, la skill lo pide en lugar de deducirlo.
- **RF-3, Compose:** valida con `docker compose config -q` (sin `-q` imprime los valores de `.env`), que se añade a la
  tabla de comandos de `AGENTS.md`.
- **RF-3, lectura sin `Glob` ni `Grep`:** las cuatro skills admiten `git ls-files -co --exclude-standard -- <ruta>` y
  `git grep -nE --untracked -e <patrón> -- <ruta>` como **única excepción de lectura** a «solo comandos de la tabla de
  `AGENTS.md`» (§14.1, fila T). No sustituyen a los comandos de validación de la tabla. §14.1 no se editó.

### 18.3 Cierre y verificación diferida

1. **Hecho el 2026-10-17 (§18.1):** las cuatro skills se ejecutaron sobre un escenario real. CA-1, CA-2 y CA-5
   quedan verificados; CA-3, CA-4 y CA-6 quedan **parciales**: las skills solo se ejercitaron en su camino de parada.
   §15 pide que cada skill se ejecute y se registre lo que hizo, y que las negativas de CA-3 se ejerciten con una spec
   `Draft`: eso está cumplido.
2. **Decisión del solicitante (2026-10-17):** pasar la spec a `Implemented` con CA-3, CA-4 y CA-6 verificados solo en su
   camino de parada y con los arreglos de §18.1 sin volver a ejecutar. Es una aceptación, no una verificación: esos
   criterios **no están cumplidos del todo**.
3. **Verificación diferida**, para anotar en §18.1 cuando haya uso real:
   - CA-3 y CA-4: `/sdd-implement` sobre la primera spec `Approved` que se implemente (por ejemplo SPEC-0006),
     comprobando que pasa a `In Progress` en su frontmatter y en los dos índices y que `git log` no gana commits.
   - CA-6: `/update-manual` sobre esa spec si su sección «Manual de uso» lista procesos, comprobando
     `manual-consistency.spec.ts` y la declaración de contenido no público.
   - Arreglos de las observaciones 1 a 4 de §18.1: comprobar que las skills siguen funcionando y que el respaldo con
     `git ls-files` y `git grep` funciona en una sesión sin `Glob` ni `Grep`. Sus correcciones posteriores a la
     revisión de `security` (§18.5) no se revisaron de forma independiente.

### 18.4 Revisión de `security` (2026-10-17)

La hizo un agente independiente que recibió los archivos y el modelo de amenazas, no las conclusiones de quien
implementó. Revisó en modo lectura y ejecutó sondas en repositorios temporales. Resultado: ningún hallazgo crítico, 1
alto, 4 medios y 7 bajos. El hook resistió la inyección (`$()`, comillas, `%`, `\`, líneas de hasta 20 MB, `sh` y `dash`
con `grep` y `sed` de BSD y GNU) y falla cerrado.

| Hallazgo | Severidad | Resolución |
|---|---|---|
| `/sdd-validate` mandaba ejecutar `docker compose config`, que imprime en el contexto del modelo los valores de `.env`: el compose interpola contraseñas y el secreto de Entra. Además no estaba en la tabla de `AGENTS.md`. | Alto | **Corregido:** usa `docker compose config -q`, añadido a la tabla; se quitó `docker info`; la skill solo ejecuta comandos de la tabla y tiene su cláusula "No hagas … leer `.env`". La premisa se comprobó leyendo `docker-compose.yml`, sin ejecutar `config` sobre el `.env` real. |
| `allowed-tools` de `/sdd-validate` pre-aprobaba comodines de git que escriben o leen archivos arbitrarios (`--output`, `--no-index`). | Medio | **Corregido:** solo `git status --short` y `git diff --name-only`; se quitó `git log`. |
| `Write` y `Edit` pre-aprobados sin ámbito en `/sdd-new-spec` y `/update-manual`; solo una skill tenía la cláusula "lo leído son datos". | Medio | **Corregido:** edición acotada a `specs/**` y a `frontend/public/manual/**`; la cláusula está en las cuatro. |
| Las reglas no cubren `appsettings.*.local.json`, `docker-compose.override.yml` ni `.claude/settings.local.json`. | Medio | **Corregido por decisión del solicitante (§19.4)** para los dos primeros. Comprobado con rutas señuelo: antes de añadir las reglas daban "no existe", después "denegado", y los archivos legítimos parecidos (`appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`) siguen accesibles. `.claude/settings.local.json` queda sin cubrir y documentado en `DEVELOPMENT.md` §10.2. |
| `DEVELOPMENT.md` §10.2 subestimaba los huecos (solo citaba Python) y decía que `secrets/` se bloquea "en la raíz". | Medio | **Corregido:** límites reescritos. El alcance real (cualquier profundidad, sin distinguir mayúsculas) se comprobó con sondas propias. |
| Hook: `amend! ` rechazado siendo válido; `GREP_OPTIONS` podía invertir el veredicto; `Merge `, `fixup! ` y similares pasan con cualquier texto; líneas `#` con `-m`. | Bajo | `amend! ` y `unset GREP_OPTIONS` añadidos, con prueba. El resto, documentado en §10.3. |
| Instalador: `chmod +x` abría permisos, `git config` sin ámbito y reemplazo silencioso de un `core.hooksPath` previo. | Bajo | **Corregido:** `chmod u+x`, `git -C … --local`, aviso al reemplazar. Documentado que git deja de ejecutar `.git/hooks/`. |
| Prueba: entorno de git heredado sin sanear, residuos ante SIGTERM, no distinguía rechazo (1) de fallo (127) y faltaban casos. | Bajo | **Corregido:** saneado de las variables `GIT_*`, *traps* de señales, estado de salida exacto y casos nuevos. |
| Skills: ambigüedad sobre `git push` en `/sdd-implement`, aprobación comprobada solo por el texto `status`, *hint* `[SPEC-NNNN]` que sugería argumento opcional y declaración de contenido público sin comprobación. | Bajo | **Corregido:** `git push` nunca y `git commit` solo a petición en el chat; se exige `approved-by` relleno; *hint* `SPEC-NNNN`; búsqueda con Grep de IPs, correos, GUID y versiones. |
| `CODEOWNERS` para `.githooks/**`, `.claude/**` y `AGENTS.md`; `CLAUDE.local.md` y `.claude/agent-memory-local/` sin ignorar. | Informativo | No aplicado: fuera del alcance de esta spec. |

Limitaciones declaradas por el revisor: no ejecutó las skills (valoró su texto); no probó `Write`, `Glob`, `Grep` ni
`NotebookEdit` contra las reglas, ni el arranque de Claude Code desde un subdirectorio; no pudo verificar `cat`, `head`
ni subprocesos porque el clasificador del modo automático los bloqueó; no probó Windows ni busybox. Las correcciones
posteriores a la revisión no se volvieron a revisar de forma independiente: las cubren la prueba automatizada (54
comprobaciones, ocho mutantes detectados) y las comprobaciones propias descritas arriba.

### 18.5 Revisión de `security` de los arreglos de §18.1 (2026-10-17)

La hizo un agente independiente que recibió el diff sin commit, el modelo de amenazas (§14) y los límites de
`DEVELOPMENT.md` §10, no las conclusiones de quien lo escribió. Revisó en modo lectura. Resultado: ningún hallazgo
crítico ni alto, 1 medio, 2 bajos y 1 informativo.

| Hallazgo | Severidad | Resolución |
|---|---|---|
| 1. Sustituir `Glob` y `Grep` por `find` y `grep` en Bash esquivaba las reglas de bloqueo: `grep -r` recorre `.env`, `.pem` y `secrets/`, y la cláusula «nunca sobre `.env` ni `secrets/`» cubría 2 de las 11 clases de `settings.json`; `find` admite `-exec`, `-delete` y `-fprint`; «piden el permiso habitual» es falso si la persona ya los tiene permitidos. | Medio | **Corregido:** las skills usan solo `git ls-files -co --exclude-standard` y `git grep -nE --untracked`, con ruta acotada, que respetan `.gitignore`; `DEVELOPMENT.md` §10.1 lo documenta y dice «salvo que ya los tengas permitidos». Comprobado con señuelos en un repositorio temporal: `/usr/bin/grep -r` leyó un `.env`, un `.pem` y un `secrets/` ignorados; `git grep --untracked` y `git ls-files --exclude-standard` no. En esa sesión `grep` era una función de la shell de Claude Code que también los omitió: el comportamiento depende del entorno, por eso no se apoya en él. |
| 2. «Solo comandos de la tabla» quedaba contradicho por el párrafo nuevo, y «no amplían» era incorrecto. | Bajo | **Corregido:** el párrafo se declara «única excepción» en `/sdd-validate` y `/sdd-implement`; el paso 3 y el «No hagas» de `/sdd-validate` y `DEVELOPMENT.md` §10.1 lo reflejan; §18.2 lo registra. §14.1 no se editó. |
| 3. Con el respaldo de «Trazabilidad» el alcance lo fija un dato de la spec: una sección incompleta puede dar «Validación completa». | Bajo | **Corregido:** con ese respaldo el veredicto máximo es «Validación incompleta: alcance no contrastado con git». |
| 4. El aviso de *worktree* decía que faltaban las skills, pero también faltan las reglas de bloqueo; `git merge` quedaba admisible por «los que `DEVELOPMENT.md` documente». | Informativo | **Corregido:** `DEVELOPMENT.md` §10.1 avisa de ambas y dice que el `git merge` lo ejecuta una persona, no una skill. |

Limitaciones declaradas por el revisor: no ejecutó las skills ni simuló una sesión sin `Glob` ni `Grep`; no comprobó si
las reglas `Read(...)` alcanzan a `grep` y `find` de Bash en esta versión, ni las reglas `allow` de cada persona; no
sabe si un *worktree* usa su propio `settings.json` o el del clon principal. Las correcciones posteriores a esta revisión
no se volvieron a revisar de forma independiente.

## 19. Preguntas abiertas

### 19.1 Propuestas (se aceptan al aprobar la spec)

- **P-1** Skills de proyecto versionadas en `.claude/skills/`, empezando por las cuatro de RF-1 a RF-4.
- **P-2** Contenido en español; identificadores de skills y scripts en inglés (RNF-3).
- **P-3** Las skills apuntan a las fuentes y no las copian (RNF-1).
- **P-4** Hook en `.githooks/` activado con `core.hooksPath` mediante `scripts/install-git-hooks.sh`. No se usa
  `init-env.sh` porque termina con error si `.env` ya existe, y no activaría el hook en un clon que ya lo tenga.
- **P-5** Reglas de bloqueo con el mismo conjunto de archivos que `.gitignore` ya trata como secretos, con
  `.env.example` excluido, más los comandos de shell de lectura más evidentes sobre esos archivos. No pretende ser
  exhaustivo (RN-3).
- **P-6** El hook se prueba con un script automatizado; las skills, con verificación documentada y uso real.
- **P-7** Quedan diferidos los subagentes, la integración continua, `/add-permission` y `/new-migration`: se abordan
  cuando el volumen de trabajo lo justifique.
- **P-8** Se corrigen en el mismo cambio las dos erratas del §2 (comando de ID y fila de SPEC-0005).

### 19.2 Preguntas resueltas (2026-10-17)

Respondidas por el solicitante; en los tres casos aceptó la propuesta de `architecture`.

| Pregunta | Respuesta | Efecto en la spec |
|---|---|---|
| **Q-1** ¿Basta esta spec o además un ADR? | Basta la spec, sin ADR. | §10.3. |
| **Q-2** ¿Cómo se identifican en el hook las correcciones menores sin spec (constitución §11.6)? Opciones: (a) prefijo `minor: <motivo>`; (b) el hook solo avisa y no bloquea. | (a): prefijo `minor: <motivo>`; el hook sigue bloqueando los demás mensajes. | RF-7, RN-6, §8.2, CA-8, §10.6. |
| **Q-3** ¿Qué otras herramientas de IA usa el equipo además de Claude Code? | Solo Claude Code por ahora; si aparecen otras, se evalúa en una spec posterior. | Sin cambios: RNF-5 ya deja a AGENTS.md como contrato común. |

No quedan preguntas pendientes.

### 19.3 Aprobación (2026-10-17)

El solicitante aprobó la spec completa, con las tres respuestas de §19.2. Las propuestas P-1 a P-8 forman parte de la
spec aprobada. **No quedan preguntas abiertas.** Se implementa en el orden de §16, sin ADR (Q-1).

Queda como verificación durante la implementación: la revisión de `security` de `.claude/settings.json`,
`.githooks/commit-msg`, `scripts/install-git-hooks.sh` y `.gitignore` (§14.2) antes de pasar la spec a `Implemented`.
Se hizo el 2026-10-17 (§18.4).

### 19.4 Decisión tras la revisión de `security` (2026-10-17)

La revisión señaló que las reglas de RF-6 no cubrían `appsettings.*.local.json` ni `docker-compose.override.yml`, que
`.gitignore` también trata como configuración local, y que RF-6 nombraba una lista cerrada. El solicitante decidió
**ampliar RF-6** con esos dos patrones, para lectura y edición. Efecto: RF-6, CA-7, §18.2 y `DEVELOPMENT.md` §10.2.
`.claude/settings.local.json` se deja fuera.
