---
id: ADR-0019
title: El borde de red de producción y las operaciones del host fuera de Compose (enmienda de §8, MAJOR 3.0.0)
status: Accepted
date: 2026-10-25
deciders: [architecture, security]
related: [ADR-0018, ADR-0020, SPEC-0009, ADR-0007, ADR-0015]
---

# ADR-0019 — El borde de red de producción y las operaciones del host fuera de Compose

> **Propuesto el 2026-10-25** a petición del solicitante, tras la revisión de `security` de ADR-0018 (hallazgo F2). **Revisión independiente desde los roles `architecture` y `security` hecha el 2026-10-26**:
> «aceptable con cambios». Sus hallazgos se incorporaron (texto de §8.7 consolidado, citas, versión, alternativas y seguimiento). **Aceptado por el solicitante el 2026-10-26** con el texto consolidado de la revisión;
> la constitución pasa a la versión **3.0.0** en el mismo cambio. La persona que acepta es también quien administra el servidor y aprueba el entorno `production` (SPEC-0009, R-2): no hay separación de funciones, riesgo ya aceptado.

## Contexto
La constitución §8 regula Docker e infraestructura. Su texto hasta la versión 2.0.0 era:

> Esta sección aplica a los componentes de servidor. El agente de dispositivo se rige por §13.
>
> 1. Todo el sistema de servidor DEBE poder levantarse con `docker compose up --build`.
> 2. Imágenes multi-stage, base mínima (Alpine/chiseled), usuario no root, sin herramientas de build en runtime.
> …
> 4. Solo el frontend publica un puerto (en `127.0.0.1` en local). La base de datos vive en una red `internal`.
> 5. Los contenedores DEBERÍAN ejecutarse con `read_only`, `cap_drop: [ALL]` y `no-new-privileges` cuando sea posible.

Dos decisiones ya tomadas ponen componentes de producción **fuera de Compose**:

- **ADR-0018** (`Accepted`) termina el TLS en un proxy inverso instalado en el host (Caddy como referencia). Lo leyó como infraestructura de red, igual que un balanceador, y declaró en la versión aceptada: «Ninguno si se acepta la lectura de §8.1 indicada arriba».
- **SPEC-0009** (`Approved`) añade, también en el host, las copias de seguridad, el endurecimiento, el script de despliegue y el monitoreo.

La revisión de `security` de ADR-0018 (2026-10-25, hallazgo F2) concluyó que esa lectura es defendible en su intención pero **no se sostiene sin una enmienda formal**:

1. §8 dice «componentes de **servidor**» y §8.1 «**todo el sistema de servidor**». ADR-0018 citaba «componentes de la aplicación».
2. Por §1.3 la constitución prevalece sobre los ADR, y por §1.1 una spec no puede contradecirla. Un ADR no puede reinterpretarla a su favor.
3. Caddy **no equivale a un balanceador administrado**: es software instalado y operado en el mismo host, expuesto a Internet, que custodia la clave privada TLS y queda fuera de §8.2 y §8.5 (imagen mínima, `read_only`, `cap_drop`) y del escaneo de imágenes.
4. SPEC-0009 extiende esa lectura a copias, despliegue y monitoreo, que ya no son «infraestructura de red».

La revisión independiente de este ADR (2026-10-26) añadió que el primer texto propuesto de §8.7 no lograba lo que decía: «puertos de la aplicación» no estaba definido y lo incumplía el propio proxy (80 y 443); «no debilitar §3 ni §4» era vacío o ya lo incumplían los riesgos aceptados de ADR-0018 y SPEC-0009;
y la categoría abarcaba seis dominios con reglas sacadas de uno solo (el proxy), de modo que las credenciales de CI, las copias o los privilegios quedaban sin regular. El texto de abajo recoge esas correcciones.

Lo que ya se cumple: §8.4. Solo el `frontend` publica un puerto en `docker-compose.yml`, en `127.0.0.1` (el `docker-compose.dev.yml` de desarrollo publica además PostgreSQL en `127.0.0.1:5432`, preexistente y solo para desarrollo).

## Decisión
1. **Reconocer en §8 una categoría propia: el borde de red de producción y las operaciones del host.** Comprende la terminación TLS, el cortafuegos, las copias de seguridad, el despliegue, el monitoreo y el endurecimiento del servidor que **no** son servicios de Compose, incluidos los recursos de nube de los que dependen sus controles. Se rige por un nuevo §8.7.
2. **§8.1 pasa a cubrir los servicios de la aplicación definidos en los `docker-compose*.yml`**, que se levantan con `docker compose up --build` y **en producción se ejecutan como servicios de Compose**. Solo se ejecutan fuera de Compose los componentes del host que admite §8.7. Lo que corre en el host fuera de Compose se rige por §3, §4 y §8.7 **en lugar de** §8.1 a §8.6, y si un componente de esos se ejecuta en un contenedor, a ese contenedor le aplican además §8.2, §8.5 y §8.6.
3. **§8.7 exige enumerar cada componente** en un ADR `Accepted` o en una spec `Approved`, `In Progress` o `Implemented`, **con revisión de `security`**, y añade seis reglas (a) a (f): configuración versionada y comprobable, puertos enumerados y loopback para el frontend, mínimo privilegio y protecciones frente a un cliente remoto, software expuesto,
   credenciales de mínimo alcance y copias cifradas con la restauración probada.
4. **No cambia nada más.** El agente de dispositivo sigue rigiéndose por §13. §8.2 a §8.6 siguen aplicando a cada servicio de Compose sin cambios.

### Texto de la constitución (versión 3.0.0)

**Introducción de §8:**

> Esta sección aplica a los componentes de servidor. El agente de dispositivo se rige por §13. Lo que en producción corre en el host fuera de los servicios de Compose (§8.7) se rige por §3, §4 y §8.7 en lugar de §8.1 a §8.6; si alguno de esos componentes se ejecuta en un contenedor, a ese contenedor le aplican además §8.2, §8.5 y §8.6.

**§8.1:**

> 1. Todos los servicios de la aplicación (los definidos en los archivos `docker-compose*.yml` del repositorio) DEBEN poder levantarse con `docker compose up --build`, y en producción se ejecutan como servicios de Compose. Solo se ejecutan fuera de Compose los componentes del host que admite §8.7.

**§8.7 (nuevo):**

> 7. **Borde de red de producción y operaciones del host.** La terminación TLS, el cortafuegos, las copias de seguridad, el despliegue, el monitoreo y el endurecimiento del servidor que no sean servicios de Compose, incluidos los recursos de nube de los que dependen sus controles, DEBEN estar enumerados en un ADR `Accepted` o en una spec `Approved`, `In Progress` o `Implemented`, con revisión de `security`. Además:
>    - **(a)** Su configuración y sus scripts DEBEN estar versionados en el repositorio, sin secretos (§4), y DEBE existir una comprobación documentada de que lo instalado coincide con lo versionado.
>    - **(b)** NO DEBEN abrir hacia fuera del host más puertos que los que enumera ese ADR o spec, ni publicar con Docker ningún puerto distinto del del frontend (§8.4). Mientras un proxy del host preceda al frontend, este DEBE publicarse solo en `127.0.0.1`.
>    - **(c)** NO DEBEN relajar §3.8 (los scripts y tareas usan el mínimo privilegio que permita su función; el uso de `root` o del grupo `docker` se justifica en la spec) ni las protecciones de §3.9 frente a un cliente remoto sin acceso al host: cabeceras y CSP, filtrado de hosts y límites por IP calculados con la IP real de la conexión. Un riesgo residual conocido (por ejemplo, la confianza en `X-Forwarded-*` desde el loopback) DEBE constar en el ADR o la spec con su aceptación expresa.
>    - **(d)** Todo software que escuche en una dirección que no sea loopback DEBE tener un rol responsable, una cadencia máxima para los parches de seguridad y una comprobación que avise si no se aplican; NO DEBE tener una interfaz de administración sin autenticación, tampoco en loopback (solo socket Unix de acceso restringido o autenticación por clave); y DEBE guardar sus claves y credenciales legibles solo por su usuario de servicio, fuera de logs y de copias en claro. DEBERÍA aplicar, con el sandbox del gestor de servicios, el equivalente de §8.5 (sin nuevos privilegios, capacidades mínimas, sistema de archivos de solo lectura salvo sus directorios de estado).
>    - **(e)** Las credenciales de despliegue, de registro y de copias DEBEN ser de mínimo alcance (solo lectura o solo añadir) y NO DEBEN estar en el repositorio ni en la línea de comandos (§4). Ninguna credencial de CI PUEDE dar acceso de ejecución al servidor sin un ADR propio con revisión de `security`.
>    - **(f)** Las copias de seguridad de datos de la aplicación DEBEN cifrarse antes de salir del servidor con una clave que no resida en él, y su restauración DEBE probarse antes de publicar y de forma periódica.

**Cabecera e historial:**

> **Versión:** 3.0.0 · **Vigente desde:** 2026-10-26
>
> | 3.0.0 | 2026-10-26 | MAJOR: §8.1 se limita a los servicios de Compose; nuevo §8.7 para el borde de red de producción y las operaciones del host, con reglas de definición, puertos, protecciones, software expuesto, credenciales y copias (ADR-0019). |

### Versión: MAJOR 3.0.0
§1.2 fija el nivel por el efecto de la enmienda, no por preferencia: limitar §8.1 relaja una regla, así que corresponde MAJOR aunque se añada §8.7. Es el criterio de ADR-0015 (2.0.0, excepción acotada: MAJOR). La versión 1.1.0 registró como PATCH que «§8 aplica a los componentes de servidor», porque excluyó al agente,
que no forma parte del sistema de servidor y tiene su propio régimen (§13). Aquí se excluye parte de ese sistema, de modo que el criterio aplicable es el de ADR-0015. Registrar MINOR o PATCH infraclasificaría una relajación.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| **1. Enmienda con §8.7 enumerado y reglas (a) a (f) (elegida)** | Cierra el conflicto con §1.3; exige enumerar y revisar cada componente; regula puertos, privilegios, software expuesto, credenciales y copias; hace conformes a ADR-0018 y SPEC-0009 | Relaja la letra de §8.1; requiere versión MAJOR y añade reglas que SPEC-0009 debe cumplir |
| 2. Enmienda clasificada como PATCH o MINOR | Menor impacto de versión | §1.2 fija el nivel por el efecto: limitar §8.1 relaja una regla (precedente de ADR-0015) |
| 3. Mantener la lectura de ADR-0018 sin enmendar | Sin cambio de la constitución | Contradice §1.3 y §1.1: un ADR no puede reinterpretar la constitución, y SPEC-0009 quedaría no conforme |
| 4. Enmienda estrecha con registro cerrado (solo el proxy TLS del host) | Menor alcance; el registro cerrado es la mejor defensa contra el cajón de sastre | Copias, despliegue, credenciales y privilegios siguen necesitando reglas. De ella se adopta la enumeración obligatoria con revisión de `security` |
| 5. Caddy como servicio de Compose con `network_mode: host` | Evita el proxy de usuario de Docker, no usa `ports:` y admite imagen mínima, `cap_drop`, `read_only`, *digest* y escaneo | Comparte el espacio de red del host (loopback e IMDS accesibles); un servicio escucharía en 80 y 443 y cambia la premisa de loopback de ADR-0018 |
| 6. Poner el proxy y las operaciones del host en Compose con `ports:` | Cumple §8.1 al pie de la letra | Un servicio publicaría 80 y 443 (exige enmendar §8.4); el proxy de usuario de Docker puede ocultar la IP real (ADR-0018); copias, despliegue y monitoreo no son servicios naturales de un contenedor |
| 7. Balanceador o CDN del proveedor (en AWS, ALB o CloudFront con ACM) | Retira la clave TLS del servidor y añade alta disponibilidad | Fuera del alcance decidido (SPEC-0009: sin balanceador ni servicios administrados); coste y un servicio administrado más |
| 8. TLS en el nginx del contenedor y sin proxy en el host | Evita la enmienda del proxy | ADR-0018 lo descartó (más cambios y más operación); copias, despliegue y monitoreo seguirían en el host |

## Consecuencias
- **Positivas:**
  - La constitución, ADR-0018 y SPEC-0009 dejan de contradecirse.
  - Cada componente del host se enumera y lo revisa `security`, y los controles dejan de ser una lectura para ser reglas con las que se revisa cada cambio.
  - El host no puede abrir puertos sin enumerarlos, ni publicar con Docker puertos distintos del frontend, ni dar credenciales de CI con ejecución en el servidor sin un ADR.
- **Negativas / riesgos:**
  - **Se relaja la letra de §8.1** para el host: `docker compose up --build` ya no garantiza, por regla, todo lo que corre en producción.
  - **El host queda fuera de §8.2 a §8.6** (sin imagen mínima, `read_only` ni escaneo) salvo lo que §8.7(d) exige a su software. Lo compensan §8.7 y la revisión de `security` de cada cambio (`agents/README.md`: `deploy/**`, `scripts/**`, `.github/workflows/**`, filas que se añaden en este mismo cambio).
  - **Riesgo de cajón de sastre:** lo acota exigir enumerar cada componente en un ADR o spec con revisión de `security`.
  - **Menos garantía de reproducibilidad:** un servidor nuevo no se levanta solo con Compose. Lo mitigan (a) y (f): configuración versionada y comprobable, y restauración probada.
  - **El canal de distribución del agente** (CI, GHCR, `deploy.sh`, `/downloads/agent/`) vive en esta categoría; las sumas SHA-256 de §13.5 se sirven desde el mismo origen que los paquetes y no protegen frente a un CI o un servidor comprometidos. Queda como riesgo conocido de SPEC-0002 (P-11).
  - **Sin separación de funciones:** una sola persona acepta, administra y aprueba el entorno (SPEC-0009, R-2).
- **Seguimiento (hecho en el mismo cambio que `constitution.md`, 2026-10-26):**
  0. `agents/README.md`: filas `.github/workflows/**` y `deploy/**` con revisión obligatoria de `security`; `agents/architecture.md` (`owns`), `agents/security.md` (`reads` y `triggers`) y `agents/database.md` (`triggers`). Sin ello el control compensatorio de §8.7 no existe.
  1. `constitution.md`: introducción de §8, §8.1, §8.7, cabecera de versión e historial.
  2. ADR-0018: «Impacto en la constitución» pasa a «enmendado por ADR-0019».
  3. SPEC-0009: R-6 cerrado; las reglas (a) a (f) se traducen en requisitos y criterios; recursos de nube enumerados.
  - **Hecho el 2026-10-26:** los documentos que describen el sistema (ARCHITECTURE.md, SECURITY.md, README.md, DEVELOPMENT.md) se actualizaron con la implementación de SPEC-0009. **Pendiente:** el commit, que lleva los prefijos de la spec y de los ADR (hook `commit-msg`, §11.2).
  - **Orden de §1.1:** SPEC-0009 (`Approved`) y ADR-0018 (`Accepted`) se aprobaron antes que esta enmienda. La compuerta de SPEC-0009 (§16.2) impide implementar antes de aceptarla, y se aceptó antes de cualquier cambio de implementación.
  - **Reexaminar §8.7** si el servidor deja de ser único o se adopta un balanceador o un servicio administrado.

## Impacto en la constitución
**Enmienda de §8, versión MAJOR 3.0.0** (§1.2: relaja una regla), con el texto de «Texto de la constitución (versión 3.0.0)». Cumple §1.2: este ADR, la revisión de `architecture` y `security` (independiente, 2026-10-26), la aceptación del solicitante (`agents/README.md` y `agents/architecture.md` la exigen para las enmiendas; §1.2 no la menciona)
y el incremento de versión con su fila en el historial. No cambian §3, §4 ni §8.4; el agente de dispositivo sigue en §13; §8.2 a §8.6 siguen aplicando a cada servicio de Compose.
