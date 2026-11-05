---
id: SPEC-0010
title: Identidad visual de ITMas
type: feature
status: Implemented
owner: jmartinez
created: 2026-11-03
approved-by: jmartinez (2026-11-03)
agents: [architecture, frontend, security, testing]
related: [ADR-0021, ADR-0005, ADR-0017, ADR-0013, SPEC-0004]
---

# SPEC-0010 — Identidad visual de ITMas

> **Compuerta (§1.1) superada.** [ADR-0021](../architecture/adr-0021-itmas-visual-identity.md) está `Accepted` y la constitución está en la 4.0.0 desde el
> 2026-11-03, así que esta spec ya no contradice §6. **Aprobada por el solicitante el 2026-11-03.**
>
> **Revisión independiente** desde `architecture` y `security` (2026-11-03): «aceptable con cambios». Sus hallazgos están incorporados, y las decisiones del
> solicitante están en §19.1.

## 1. Objetivo
Dar a ITMas una identidad visual propia en tema claro y oscuro: azul de marca, Poppins, superficies de vidrio, fondos y movimiento. No cambian la
funcionalidad, los permisos, las rutas ni los textos de los procesos.

## 2. Contexto
- **Solicitud (2026-11-03):** cambiar el aspecto gráfico de ITMas. El solicitante decidió que todo es de ITMas: nombre, logotipo e identidad, sin nombrar otras
  marcas (Q-7). También decidió mantener los temas claro y oscuro.
- **Maqueta revisada por el solicitante (2026-11-03).**
  - Muestra el inicio de sesión, la lista de equipos y el informe de un equipo, en claro y en oscuro, con datos ficticios. Los valores de §11.2 salen de ella.
  - Es un artefacto privado: no se enlaza ni se suben capturas suyas, porque el repositorio es público.
- **Estado actual** (detalle en el contexto de ADR-0021):
  - Un único `mat.theme` con paletas `azure`/`blue`, Roboto y densidad 0 (ADR-0005).
  - 36 archivos con estilos de componente (11 hojas y 25 con `styles` en línea). 27 usan roles `--mat-sys-*`.
  - Ningún color literal ni selector de clases de Material, pero sí 13 declaraciones tipográficas literales en 11 archivos.
  - Los estados de advertencia usan el rol terciario, hoy azul.
  - Roboto y Material Symbols vienen de Google Fonts. La compilación incrusta su CSS.
- **Constitución 4.0.0 (ADR-0021):** la identidad visual vive solo en la capa de tema, y fuera de ella el CSS de componentes sigue reglas comprobables (§6.3).

## 3. Alcance
- **Incluye:**
  - El tema global: paletas generadas desde la marca, tipografía Poppins y forma (radios).
  - Los tokens `--itmas-*` y las clases de superficie `itmas-*` de la capa de tema (ADR-0021, punto 2).
  - El *shell* autenticado: barra lateral y barra superior como islas flotantes y fondo de producto con orbes.
  - El aspecto de tarjetas, tablas, botones, estados, diálogos y menús en todas las pantallas existentes.
  - Los tonos de los estados (RF-8) y los colores de las actividades del calendario.
  - Las páginas públicas (inicio de sesión, manual, acceso denegado y página no encontrada) con el fondo de marca.
  - El logotipo tipográfico de ITMas y el nombre visible «ITMas».
  - Poppins y Material Symbols servidos desde el mismo origen, con procedencia verificable, y la retirada de Google Fonts.
  - La migración de las 13 declaraciones tipográficas literales.
  - Los estilos de impresión.
  - La prueba de guarda de §6.3.
- **Excluye:**
  - Cambios funcionales, de rutas, de permisos, de backend y de base de datos.
  - Contenido o navegación nuevos: filas de indicadores, gráficos, paneles, ruta de la página y etiquetas de sección (Q-13).
  - Elementos de marca de otros productos.
  - Cambiar el conjunto de iconos (Q-2) y el agente de dispositivo.

## 4. Actores
| Actor | Descripción |
|---|---|
| Persona usuaria | Cualquier persona que usa ITMas, con o sin sesión (el manual es público). Ve el nuevo aspecto; sus procesos no cambian. |
| Solicitante | Aprobó la identidad visual, los tonos y las decisiones de §19.1. |

## 5. Requerimientos
### 5.1 Funcionales
- **RF-1 Tema.**
  - Un único `mat.theme` con paletas generadas por el esquemático `@angular/material:theme-color` (`_theme-colors.scss`) desde `#004AAD` (primaria) y
    `#1893F8` (terciaria), tipografía Poppins (pesos 300 a 700) y densidad 0.
  - El rol `primary` del esquema claro es `#004AAD` exacto, fijado con `mat.theme-overrides`. En el oscuro es el tono 80 de la misma paleta (Q-12).
  - Los neutros de texto y líneas son los de §11.2, fijados con `mat.theme-overrides` (Q-10).
  - Ningún rol de Material usa el naranja.
- **RF-2 Capa de tema.** `frontend/src/styles.scss` y `frontend/src/styles/**` declaran:
  - Los tokens `--itmas-*` (§11.2), con `light-dark()` en cada color.
  - Las clases `itmas-*` (vidrio de tarjeta, vidrio oscuro, barra metálica, elemento anidado, fondo de producto y fondo de marca), con las condiciones de ADR-0021:
    solo lo que Material no expone como token, sin `!important`, respetando `:disabled` y los demás estados, y sin clases internas de Material.
- **RF-3 *Shell*.** Disposición de islas con 12 px de margen y separación:
  - **Barra lateral** de 240 px con vidrio oscuro (degradado de marca) y radio de 20 px. El elemento activo lleva un indicador naranja de 3 × 20 px a la
    izquierda y `aria-current="page"`.
  - **Barra superior** metálica de 56 px con radio de 20 px y los mismos elementos que hoy: botón de navegación, nombre de la aplicación, manual, menú de tema y
    menú de la cuenta. Sin ruta de la página (Q-13).
  - El área de contenido es fluida hasta 1440 px (hoy se limita a 1200 px, Q-5).
  - En móvil la barra lateral sigue en modo superpuesto, como hoy.
- **RF-4 Fondo de producto.** En las pantallas autenticadas:
  - El degradado de página.
  - Tres orbes desenfocados que se desplazan en ciclos de 45 a 60 s (Q-8).
  - Una textura de ruido muy suave.
- **RF-5 Superficies.**
  - Tarjetas y contenedores de tabla con vidrio de tarjeta: radio de 16 px y sombra de varias capas.
  - Elementos anidados (pares dato–valor, interfaces) con vidrio anidado y radio de 12 px.
  - Menús, listas de selección, diálogos, *tooltips* y avisos con los roles opacos `surface-container*` y sombra, nunca de vidrio.
- **RF-6 Tipografía y jerarquía.**
  - Títulos de página en el color primario y en el peso más alto de la escala.
  - Encabezados de tabla con el rol `label-small` del tema (11 px), en mayúsculas y con espaciado de 0,1 em, mediante `mat.table-overrides` y, para las
    mayúsculas, una clase `itmas-*`.
  - Cuerpo en Poppins 300 y campos en 400.
  - Los tamaños salen de la escala tipográfica del tema.
  - Sin etiquetas de sección nuevas sobre los títulos (Q-13).
- **RF-7 Botones.**
  - El principal (`mat-flat-button`) usa el degradado `--itmas-cta` si se logra con tokens o con una clase `itmas-*` en el propio botón, conservando su aspecto
    deshabilitado. Si no, usa `#004AAD` sólido (Q-4).
  - El secundario usa vidrio claro con borde y texto primario.
  - Los botones de icono conservan su nombre accesible.
- **RF-8 Estados.** Los estados de equipos, auditoría SQL y agenda se muestran como etiquetas tintadas: radio de 6 px, ícono o punto, y texto.
  - Cinco tonos (§11.2), asignados así (Q-11):

    | Tono | Equipos | Auditoría SQL | Agenda |
    |---|---|---|---|
    | Positivo | Activo | Válido | Completada, Publicado (Outlook) |
    | Advertencia | Pendiente | Alto, Cambió desde la validación, Certificado sin verificar, Datos no disponibles, Sin actualizar | Pendiente (Outlook) |
    | Riesgo | — | Crítico | Vencida, Error (Outlook) |
    | Informativo | — | — | Programada |
    | Neutro | Eliminado | General, Denegado, Pendiente (validación), Eliminado del motor, Equipo eliminado | Borrador, Cancelada, No publicado |

  - **Ningún estado de riesgo o de advertencia de seguridad usa el tono positivo ni el informativo.**
  - No cambian sus textos ni sus íconos, y el color nunca es la única señal.
  - Las actividades del calendario usan los mismos tonos en sus contenedores (ADR-0017, nota).
- **RF-9 Páginas públicas.**
  - Inicio de sesión, manual, acceso denegado y página no encontrada usan el fondo de marca: degradado azul vertical con ondas y nodos blancos al 8–20 %,
    asimétricos y decorativos.
  - El contenido va en una tarjeta opaca.
  - No cambian los textos, las acciones ni la redirección a Microsoft Entra ID (incluido `returnUrl`).
- **RF-10 Logotipo y nombre.**
  - Logotipo tipográfico «ITMas» en Poppins 600, con un punto naranja como detalle y un `mat-icon` en un recuadro, en la barra lateral y en el inicio de sesión.
  - Solo plantilla: sin SVG remoto ni `innerHTML`.
  - `appName` y el `<title>` pasan de «ITMAS3» a «ITMas», como ya dice el manual (Q-1).
  - El icono de la pestaña del navegador (`frontend/public/favicon.ico`) es el logotipo de ECS, la organización propietaria de ITMas (Q-18).
- **RF-11 Selección de tema.** No cambia: el menú «Cambiar tema» sigue ofreciendo **Claro**, **Oscuro** y **Según el sistema**, el cambio es inmediato y la
  preferencia se guarda como hoy.
- **RF-12 Movimiento.**
  - Entrada del contenido con desplazamiento vertical de 12 px y opacidad (0,4 s, escalonada cada 0,08 s).
  - Al pasar el cursor, tarjetas y botones se elevan 1 px y amplían la sombra.
  - Solo se animan `transform`, `opacity`, `box-shadow` y `filter`.
- **RF-13 Migración tipográfica.** Las 13 declaraciones tipográficas literales pasan a roles de la escala o a tokens:
  - `--itmas-font-mono` para la familia monoespaciada: `manual-page.scss`, `enrollment-code-dialog.ts`, `event-detail-page.scss` y `admin-page.scss`.
  - `--itmas-icon-size-*` para los tamaños de icono: `sql-status.ts`, `status-indicator.ts`, `agenda-page.scss` y `status-message.ts`.
  - Roles `--mat-sys-*` para el resto: `enrollment-code-dialog.ts`, `audit-event-dialog.ts` y `agenda-calendar.scss`.
- **RF-14 Impresión.** Con `@media print` no hay fondos, orbes ni vidrio, y el texto se imprime en oscuro sobre blanco. El manual promete la opción **Imprimir**
  (`usar-el-manual`).
- **RF-15 Fuentes propias (Q-2, Q-3).**
  - Poppins (OFL 1.1; pesos 300 a 700; subconjuntos latino y latino extendido) y una instancia estática de Material Symbols Outlined (Apache 2.0) se copian a
    `frontend/public/fonts/<fuente>/` desde su publicación oficial.
  - El peso de los iconos (300 o 400) se decide en la verificación visual, para que casen con Poppins.
  - Cada carpeta lleva su licencia (`OFL.txt` o `LICENSE`) y un `SOURCE.md` que anota:
    - la URL y la versión de origen (etiqueta, *commit* o fecha de la publicación);
    - la SHA-256 de cada archivo de origen y de cada archivo servido;
    - si se transforma (subconjunto o instancia), la herramienta, su versión y el comando exacto, para que el resultado sea reproducible.
  - Se comprueba que la licencia de Poppins no declara *Reserved Font Name*, porque el subconjunto es una modificación.
  - Se justifica según §12.5 como recurso de terceros.
  - Desaparecen las referencias a Google Fonts de `index.html` (hojas y `preconnect`).

### 5.2 No funcionales
- **RNF-1 Contraste.**
  - WCAG 2.2 AA en ambos esquemas: 4,5:1 para texto normal y 3:1 para texto grande, iconos e indicadores de foco.
  - Se mide sobre el vidrio con los orbes detrás y sobre cada degradado que lleve texto. Por eso esos degradados terminan en `#1270D6` (unos 4,9:1 con blanco)
    y no en `#1893F8` (3,2:1).
- **RNF-2 Preferencias.**
  - Con `prefers-reduced-motion` no hay orbes animados ni animaciones de entrada.
  - Con `prefers-reduced-transparency` (donde el navegador la exponga), o sin soporte de `backdrop-filter`, las superficies de vidrio pasan a ser opacas y
    siguen cumpliendo RNF-1.
- **RNF-3 Rendimiento.**
  - `backdrop-filter` solo en las superficies de fondo (barra lateral, barra superior y tarjetas), nunca en filas ni elementos anidados.
  - El presupuesto inicial de `angular.json` no supera el aviso (550 kB).
  - Poppins se carga con `font-display: swap`. Los iconos usan una instancia estática, no el archivo variable completo, con `font-display: block`, para
    que no se vean sus nombres como texto mientras cargan (Q-17).
- **RNF-4 CSP y terceros.**
  - La CSP no se relaja: el cambio de `frontend/nginx/security-headers.inc.template` solo retira `https://fonts.googleapis.com` de `style-src` y
    `https://fonts.gstatic.com` de `font-src`, que queda en `'self'`.
  - Las fuentes se sirven como archivos, nunca como `data:`.
  - Ningún tercero interviene en las fuentes, ni en ejecución ni al compilar.
- **RNF-5 Mantenibilidad.**
  - Solo las API públicas de *theming* de Angular Material.
  - El CSS de componentes cumple §6.3 (constitución 4.0.0), con las excepciones de encapsulación que enumera ADR-0021.
- **RNF-6 Accesibilidad de componentes.** No cambian el foco visible, los roles ARIA ni los nombres accesibles. El orden de tabulación solo cambia en el
  *shell*: la barra superior pasa a la columna del contenido, así que el foco recorre primero la navegación y después la barra (Q-15).

## 6. Reglas de negocio
No hay reglas de dominio. El significado que transmite cada estado (RF-8) lo decidió el solicitante (Q-11).

## 7. Permisos
| Permiso (`<módulo>.<recurso>.<acción>`) | Concede | Endpoints / pantallas |
|---|---|---|
| Ninguno nuevo | — | Las pantallas y su visibilidad por permisos no cambian. |

## 8. Comportamiento esperado
- **Pantallas autenticadas.**
  - Fondo de producto, barra lateral y barra superior flotantes, y contenido sobre tarjetas de vidrio.
  - Las listas (equipos, instancias SQL, agenda, usuarios, políticas, permisos y auditoría) muestran su cabecera de hoy (título, texto de apoyo y acción
    principal) y una tarjeta con filtros, tabla y paginador.
  - Los detalles (informe del equipo, instancia SQL, actividad, usuario y política) agrupan sus secciones en tarjetas con elementos anidados.
  - Inicio y los formularios usan las mismas superficies.
- **Diálogos y menús.** Superficie opaca sobre un velo, con los mismos botones y textos.
- **Páginas públicas.** Fondo de marca y contenido en una tarjeta opaca. El manual se pinta igual que hoy (`marked` y el saneado de Angular); solo cambia lo que lo
  rodea.
- **Cambio de tema.** Al elegir otra opción, todo cambia sin recargar, incluidos los calendarios de la agenda.
- **Móvil (375 px).** La barra lateral se superpone, las barras se apilan, las tablas se desplazan dentro de su contenedor y la página no se desplaza en
  horizontal.
- **Impresión.** Sin fondos ni vidrio (RF-14).
- **Estados de carga, vacío y error.** No cambian sus textos. Usan las mismas superficies y tonos.

## 9. Criterios de aceptación
- [ ] **CA-1** Dado el tema, cuando se inspecciona en el navegador, entonces `--mat-sys-primary` es `#004AAD` en claro y el tono 80 de la misma paleta en oscuro,
  y el cuerpo usa Poppins.
- [ ] **CA-2** Dada la compilación de producción, cuando se inspeccionan su `index.html` y las peticiones de red, entonces no hay ninguna referencia ni petición a
  `fonts.googleapis.com` ni a `fonts.gstatic.com`. Poppins y Material Symbols se sirven desde el propio origen.
- [ ] **CA-3** Dado cualquiera de los dos esquemas, cuando se recorren todas las rutas de `app.routes.ts` y sus diálogos, entonces todo texto, icono e indicador
  de foco cumple RNF-1.
- [ ] **CA-4** Dada una pantalla autenticada, cuando se navega, entonces la barra lateral y la barra superior cumplen RF-3, y el elemento activo lleva el
  indicador naranja y `aria-current="page"`.
- [ ] **CA-5** Dado un menú, una lista de selección, un diálogo, un *tooltip* o un aviso abierto, cuando se muestra sobre una tarjeta con texto, entonces ese
  texto no se lee a través del panel.
- [ ] **CA-6** Dado `prefers-reduced-motion: reduce`, cuando se carga una pantalla, entonces no hay orbes en movimiento ni animación de entrada. Dado
  `prefers-reduced-transparency: reduce` (emulado en el navegador), entonces las superficies son opacas y cumplen RNF-1.
- [ ] **CA-7** Dado el menú «Cambiar tema», cuando se elige Claro, Oscuro o Según el sistema, entonces se aplica sin recargar y se conserva al volver a entrar.
- [ ] **CA-8** Dados los estilos de `frontend/src/app/**` (hojas y `styles` en línea), cuando se ejecuta la guarda de estilos, entonces:
  - No hay selectores `.mat-*`, `.mdc-*` ni `.cdk-*`.
  - Solo hay `::ng-deep` o `ViewEncapsulation.None` en las dos excepciones de ADR-0021.
  - No se declaran `--mat-*` ni `--itmas-*` ni se incluyen mixins `mat.*`.
  - Color, fondo, color de borde, sombra, filtro, radio y tipografía solo usan los valores que admite §6.3 (sin literales ni colores compuestos con `color-mix`
    a partir de literales).
  - No hay `url(` a otros orígenes ni `@import url(`.
  - En `frontend/src/styles/**` tampoco hay selectores de clases internas de Material, y el naranja `#F2982A` solo aparece allí.
- [ ] **CA-9** Dadas las vistas Año, Mes y Semana de la agenda, cuando se cambia de esquema, entonces siguen los roles del nuevo tema, las actividades usan los
  tonos de RF-8 y se cumple RNF-1.
- [ ] **CA-10** Dadas las páginas públicas, cuando se cargan sin sesión, entonces usan el fondo de marca de RF-9, y sus textos, acciones y redirección no
  cambian.
- [ ] **CA-11** Dada la compilación de producción servida por nginx con su CSP, cuando se recorren las pantallas, entonces no hay violaciones de CSP, el
  presupuesto inicial no supera el aviso y el cambio de `security-headers.inc.template` solo retira los dos orígenes de Google Fonts.
- [ ] **CA-12** Dadas las suites actuales, cuando se ejecutan `npm test -- --watch=false` y `npm run build`, entonces pasan. Solo cambian las aserciones de
  presentación de los tonos (RF-8), nunca las de comportamiento.
- [ ] **CA-13** Dada una pantalla de 375 px de ancho, cuando se recorren todas las rutas, entonces la página no se desplaza en horizontal y la barra lateral se
  superpone.
- [ ] **CA-14** Dados los mapeos de estados (`sql-audit-labels.ts`, `agenda-labels.ts`, el estado de los equipos y `calendar-model.ts`), cuando se ejecutan sus
  pruebas, entonces cada estado tiene el tono de RF-8 y ningún estado de riesgo o advertencia usa el tono positivo ni el informativo.
- [ ] **CA-15** Dada una página del manual, cuando se imprime o se guarda en PDF, entonces no hay fondos, orbes ni vidrio y el texto se lee en oscuro sobre
  blanco.
- [ ] **CA-16** Dados los `.md` del manual, cuando se ejecuta `manual-consistency.spec.ts`, entonces ninguno contiene `class=` ni `style=`. El saneado de
  Angular conserva `class`, y las clases globales `itmas-*` no deben llegar desde el Markdown.
- [ ] **CA-17** Dados `layout/**`, `features/login/**`, `features/errors/**` y `shared/components/status-message/**`, cuando se ejecuta la guarda de
  seguridad, entonces no hay `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `bypassSecurityTrust*` ni `DomSanitizer`.
- [ ] **CA-18** Dado `frontend/public/fonts/**`, cuando se ejecuta la prueba de procedencia, entonces cada fuente tiene su licencia y su `SOURCE.md`, y la
  SHA-256 de cada archivo servido coincide con la anotada.

## 10. Análisis arquitectónico
- **ADR-0021** (enmienda de §6, MAJOR 4.0.0) está aceptado: esta spec cumple la constitución 4.0.0.
- **Capa de tema.** `styles.scss` importa parciales en `frontend/src/styles/`:
  - `_theme-colors.scss`, generado por el esquemático.
  - `_tokens.scss`.
  - `_material-overrides.scss`, con el primario, los neutros, la forma y los tokens de componente.
  - `_surfaces.scss`.
  - `_backgrounds.scss`.
  - `_motion.scss`, con las preferencias y la impresión.
  - `_fonts.scss`, con los `@font-face` de Poppins y Material Symbols.
- **Componentes.** Las plantillas aplican las clases `itmas-*` a sus propios elementos. Los estilos de componente siguen siendo de layout y cumplen §6.3.
- **Límites de los tokens** (revisión de `architecture`, A-5):
  - Los tokens de color de Material se aplican como `background-color` o `border-color`, no como fondo completo. Los degradados (barra lateral y botón
    principal) necesitan una clase `itmas-*` en el propio elemento, que debe mantener el aspecto deshabilitado. Si no se logra, el botón queda sólido (RF-7).
  - Los estilos de componente de Material se inyectan después de los globales: una clase `itmas-*` con la misma especificidad pierde frente a Material (por
    ejemplo, `.mat-drawer-side { box-shadow: none }`). La tarea 5 lo resuelve con tokens o renuncia al efecto, nunca con `!important`.
  - La barra de progreso pinta su indicador con clases internas: el medidor de uso queda en color sólido por token.
- **Disposición de islas.** El `mat-sidenav-container` coloca la barra lateral en modo `side`. El margen de 12 px y los radios se logran con estilos del propio
  contenedor y de la barra, sin clases internas. Se valida en la tarea 5.
- **Guarda primero.** La guarda de §6.3 (CA-8), la de seguridad (CA-17) y la de procedencia de las fuentes (CA-18) se escriben antes del tema (tarea 1), para
  que cada tarea posterior se compruebe con ellas.
- **FullCalendar (ADR-0017).** No cambia su integración: sus variables siguen asignadas a roles `--mat-sys-*`. Los contenedores de las actividades
  (`calendar-model.ts`) pasan a los tonos de RF-8, y su `theme.css` sigue fijando tamaños de letra propios (limitación aceptada).
- **Fuentes.** Sin dependencia npm nueva: archivos copiados con procedencia (RF-15). `optimization.fonts` de `angular.json` deja de tener efecto, porque ya no
  hay hojas de fuentes externas que incrustar.
- **Propiedad de archivos.** `frontend/public/fonts/**` es del agente `frontend`, con revisión de `security` (ADR-0021, seguimiento).
- **Respeta la constitución 4.0.0:** §6.2 a §6.4, §3.9 (la CSP solo pierde orígenes), §12.5 (recursos de terceros justificados) y §10 (documentación).

## 11. Cambios de frontend
### 11.1 Archivos
- **Capa de tema:** `frontend/src/styles.scss` y `frontend/src/styles/**` (nuevos). `frontend/angular.json`, si hace falta para estilos o fuentes.
- **Fuentes:**
  - `frontend/public/fonts/poppins/` y `frontend/public/fonts/material-symbols/`, cada una con su licencia y su `SOURCE.md`.
  - `frontend/src/index.html`: se retiran las hojas y los `preconnect` de Google Fonts, y el `<title>` pasa a «ITMas».
- **CSP:** `frontend/nginx/security-headers.inc.template` (solo retira orígenes, RNF-4).
- **Layout:** `layout/shell/*`, `layout/public-layout/*`, `layout/user-menu/*` y `layout/theme-toggle/*` (solo aspecto). El logotipo, en `layout/` o en `shared/`.
- **Plantillas de features (Q-15):** las páginas de listas, detalles y formularios y sus diálogos, solo para aplicar clases `itmas-*` y envoltorios
  (tarjeta de las listas, `itmas-table`, `itmas-inner`, `itmas-cta`, `itmas-glass-button`), sin cambiar textos, lógica ni roles.
- **Páginas públicas:** `features/login/login-page.ts`, `features/errors/*` y `shared/components/status-message/*`.
- **Estados (RF-8):**
  - `features/sql-audit/data/sql-audit-labels.ts` y `features/sql-audit/components/{sql-status,risk-chip}.ts`.
  - `features/agenda/data/agenda-labels.ts`, `features/agenda/components/{event-status-chips,status-indicator}.ts` y
    `features/agenda/calendar/calendar-model.ts`.
  - La columna de estado de `features/inventory/devices/devices-page.html`.
- **Migración tipográfica (RF-13):** los 11 archivos que enumera RF-13.
- **Pruebas:**
  - Nuevas: la guarda de estilos, la de seguridad y la de procedencia de las fuentes.
  - Existentes: `features/manual/manual-consistency.spec.ts` y las pruebas de los mapeos de estados.
- **Nombre de la aplicación:** `environments/*` (`appName` pasa a «ITMas»). No contiene secretos.
- Sin rutas nuevas, sin cambios de navegación y sin componentes Material nuevos.

### 11.2 Tokens de la identidad (aprobados el 2026-11-03)
| Token | Claro | Oscuro | Uso |
|---|---|---|---|
| `--itmas-page-background` | Brillo azul arriba a la derecha (5 %), brillo naranja abajo a la izquierda (4 %), `#FAFAF7 → #F6F5F1` | Brillo azul (16 %), naranja (6 %), `#001C47 → #00122E` | Fondo de producto |
| `--itmas-orb-1/2/3` | `rgba(0,74,173,.12)`, `rgba(47,159,207,.10)`, `rgba(242,152,42,.08)` | `rgba(24,147,248,.18)`, `rgba(47,145,207,.14)`, `rgba(242,152,42,.07)` | Orbes (700, 600 y 500 px; desenfoque de 80, 90 y 100 px; ciclos de 45, 55 y 60 s) |
| `--itmas-glass-card` (fondo / borde) | `rgba(255,255,255,.60)` / `rgba(255,255,255,.70)` | `rgba(255,255,255,.06)` / `rgba(255,255,255,.10)` | Tarjetas; `blur(36px) saturate(150%)` |
| `--itmas-glass-card-shadow` | `0 2px 12px rgba(0,40,120,.06)`, `0 6px 24px rgba(0,40,120,.04)` y dos sombras interiores | Las mismas capas con `rgba(0,8,30,.30/.25)` | Tarjetas |
| `--itmas-glass-dark` | `#002D6B → #004AAD → #1270D6` (135°) con un velo blanco del 6 al 2 % | Igual | Barra lateral; sombra interior naranja al 20 % en el borde inferior |
| `--itmas-bar` | Blanco del 82 al 78 %; `blur(50px) saturate(180%)` | Blanco del 10 al 6 % | Barra superior |
| `--itmas-inner` (fondo / borde) | `rgba(255,255,255,.28)` / `rgba(255,255,255,.30)` | `rgba(255,255,255,.05)` / `rgba(255,255,255,.08)` | Elementos anidados |
| `--itmas-cta` | `linear-gradient(135deg, #004AAD, #1270D6)` | Igual | Botón principal (RF-7) y recuadro del logotipo |
| `--itmas-meter` | `#004AAD` sobre `rgba(0,74,173,.10)` | `#3BB6FF` sobre `rgba(255,255,255,.10)` | Barras de uso (color sólido por token) |
| `--itmas-accent-detail` | `linear-gradient(180deg, #F2982A, #FA9241)` | Igual | Solo el indicador activo y el punto del logotipo |
| `--itmas-font-mono`, `--itmas-icon-size-*` | Familia monoespaciada de hoy; 18 y 48 px | Igual | RF-13 |
| Tono positivo (texto / fondo) | `#166534` / `rgba(34,197,94,.15)` | `#86EFAC` / `rgba(34,197,94,.15)` | RF-8 |
| Tono de advertencia | `#854D0E` / `rgba(234,179,8,.15)` | `#FDE047` / `rgba(234,179,8,.15)` | RF-8 |
| Tono de riesgo | `#B91C1C` / `rgba(239,68,68,.15)` | `#FCA5A5` / `rgba(239,68,68,.15)` | RF-8 |
| Tono informativo | `#004AAD` / `rgba(59,130,246,.10)` | `#8EC3FF` / `rgba(59,130,246,.15)` (antes `#78B6FF`, §19.1 Q-14) | RF-8 |
| Tono neutro | Rol `on-surface-variant` / rol `surface-container-high` | Igual (roles) | RF-8 |
| Neutros | Texto `#1C1C1E`, atenuado `#5B6270` (antes `#6B7280`, Q-14), líneas `#E5E7EB` | Texto `#F5F7FA`, atenuado `#B4BAC5` (antes `#9CA3AF`, Q-14), líneas `rgba(255,255,255,.08)` | `on-surface`, `on-surface-variant`, `outline-variant` (RF-1) |
| Paneles opacos | Roles `surface-container*` de la paleta generada | Igual | RF-5 |

- **Radios:** 6 px (estados), 8 px (botones y campos), 12 px (anidados), 16 px (tarjetas) y 20 px (barras).
- **Tonos de texto de los estados:** son más oscuros en claro y más claros en oscuro que los de partida, para alcanzar 4,5:1 sobre su fondo tintado.
- **Elemento activo de la barra lateral:** su velo es negro al 16 % (`secondary-container` dentro del vidrio oscuro), no blanco: con velo blanco, el texto blanco
  quedaba por debajo de 4,5:1 en la parte clara del degradado (Q-14).

## 12. Cambios de backend
| Método | Ruta | Permiso | Request | Response | Errores |
|---|---|---|---|---|---|
| — | — | — | — | — | — |

Ninguno.

## 13. Cambios de base de datos
Ninguno.

## 14. Consideraciones de seguridad
### 14.1 STRIDE
| Amenaza | Análisis | Mitigación |
|---|---|---|
| **S** Suplantación | El inicio de sesión cambia de aspecto. Sigue sin pedir credenciales: redirige a Microsoft Entra ID. | No cambian sus acciones, la redirección ni `returnUrl` (RF-9, CA-10). |
| **T** Manipulación | Fuentes de terceros alteradas. CSS o HTML que llegue desde datos. Acciones no intencionadas si un diálogo de confirmación no se lee. | Procedencia verificable y sumas comprobadas por una prueba (RF-15, CA-18). Las clases de tono salen de mapeos cerrados, sin `[style]` ni `[innerHTML]` con datos (CA-17). Paneles y diálogos opacos (RF-5, CA-5). |
| **R** Repudio | No aplica: no hay acciones ni registros nuevos. | — |
| **I** Divulgación | Terceros que reciban datos de cada visita. Información interna en un repositorio público. El manual es público. | Sin Google Fonts: las fuentes salen de `'self'` (RNF-4, CA-2). El repositorio no nombra otras marcas ni enlaza la maqueta (Q-7). El manual no cambia de contenido y sus `.md` no llevan `class` ni `style` (CA-16). |
| **D** Denegación de servicio | Coste de `backdrop-filter` en equipos modestos. | Limitado a las superficies de fondo y con alternativa opaca (RNF-2, RNF-3). |
| **E** Elevación de privilegios | No aplica: no cambian permisos, rutas ni guardas. | — |

### 14.2 Detalle
- **CSP:**
  - Solo pierde orígenes: `https://fonts.googleapis.com` de `style-src` y `https://fonts.gstatic.com` de `font-src` (RNF-4, CA-11).
  - Las fuentes desde `'self'` caben en `font-src 'self'`, y los `@font-face` van en la hoja global servida desde `'self'`.
  - La textura de ruido usa un `data:` en `background-image`, que `img-src 'self' data:` ya permite.
  - Las fuentes nunca van como `data:`: añadir `data:` a `font-src` relajaría §3.9.
- **Terceros:** desaparece Google Fonts, en ejecución y al compilar. Las dos fuentes son recursos de terceros copiados, con licencia, procedencia y sumas (RF-15).
- **Contenido:** el logotipo y los fondos son plantilla y CSS, sin `innerHTML`, `bypassSecurityTrust*` ni SVG remoto (CA-17). El manual se sigue pintando con
  `marked` y el saneado de Angular, y `::ng-deep` sigue limitado a `.content`.
- **Datos:** ninguno nuevo. La preferencia de tema sigue en `localStorage`, validada contra una lista cerrada, y no es sensible.
- **Repositorio público:**
  - La maqueta no se enlaza ni se suben capturas suyas.
  - Cualquier captura nueva del manual lleva solo datos ficticios (§10.4).
  - No se nombran otras marcas (Q-7).
- **Revisión de `security`:** la CSP servida por nginx (CA-11) y la procedencia de las fuentes (RF-15, CA-18).

## 15. Pruebas
| Criterio | Tipo (unit/integration/frontend) | Prueba |
|---|---|---|
| CA-1, CA-3, CA-5, CA-6, CA-9, CA-10, CA-13, CA-15 | Verificación en el navegador | Lista de comprobación en ambos esquemas: todas las rutas y sus diálogos, a 375 px, con las preferencias de movimiento y transparencia emuladas y en vista de impresión. Contraste medido con las herramientas del navegador (Q-9). |
| CA-2, CA-11 | Verificación en el navegador | Compilación de producción servida por nginx: `index.html` compilado, red sin peticiones a Google, consola sin violaciones de CSP, presupuesto de `npm run build` y diff de `security-headers.inc.template`. |
| CA-4 | frontend (unit) | `layout/shell/shell.spec.ts`: el elemento activo lleva `aria-current="page"` y el indicador. |
| CA-7 | frontend (unit) y verificación en el navegador | Pruebas actuales de `ThemeService`, sin cambios de aserción; el menú «Cambiar tema», en el navegador. |
| CA-8 | frontend (unit) | Nueva guarda `styles-guard.spec.ts`: recorre hojas y `styles` en línea de `frontend/src/app/**` y la capa de tema, con la lista de excepciones de ADR-0021. |
| CA-12 | frontend (unit) | `npm test -- --watch=false` y `npm run build`. |
| CA-14 | frontend (unit) | Pruebas de `sql-audit-labels.ts`, `agenda-labels.ts`, el estado de los equipos y `calendar-model.ts` con la asignación de RF-8. |
| CA-16 | frontend (unit) | `features/manual/manual-consistency.spec.ts`: ningún `.md` con `class=` ni `style=`. |
| CA-17 | frontend (unit) | Guarda de seguridad al estilo de `agenda-safety.spec.ts` para `layout/**`, `features/login/**`, `features/errors/**` y `shared/components/status-message/**`. |
| CA-18 | frontend (unit) | Nueva prueba de procedencia: licencia y `SOURCE.md` presentes y SHA-256 de cada archivo servido igual a la anotada. |

## 16. Plan de implementación
| # | Tarea | Agente | Archivos |
|---|---|---|---|
| 0 | **Compuerta.** Hecho el 2026-11-03: decisiones de §19.1, ADR-0021 `Accepted`, constitución 4.0.0, seguimiento de ADR-0021 y aprobación de esta spec. | architecture (security) | `constitution.md`, `specs/**`, `agents/*`, `AGENTS.md`, `DEVELOPMENT.md` |
| 1 | Guardas de estilos (CA-8), de seguridad (CA-17) y de procedencia de las fuentes (CA-18), y comprobación del manual (CA-16). La guarda de estilos falla al principio en las 13 declaraciones de RF-13. | testing | `frontend/src/app/**/*.spec.ts` |
| 2 | Migración tipográfica (RF-13) | frontend | Los 11 archivos de RF-13 |
| 3 | Paletas generadas y tema base: Poppins, densidad 0, primario `#004AAD` y neutros | frontend | `frontend/src/styles.scss`, `frontend/src/styles/_theme-colors.scss`, `frontend/src/styles/_material-overrides.scss` |
| 4 | Fuentes propias con procedencia (RF-15), retirada de Google Fonts de `index.html` y nombre «ITMas» | frontend (security) | `frontend/public/fonts/**`, `frontend/src/styles/_fonts.scss`, `frontend/src/index.html`, `frontend/src/environments/*` |
| 5 | Tokens, superficies, fondos, movimiento, preferencias e impresión. *Shell* de islas, logotipo y barras. Páginas públicas con fondo de marca. Tokens de componentes y paneles opacos. | frontend | `frontend/src/styles/**`, `layout/**`, `features/login/**`, `features/errors/**`, `shared/components/status-message/**` |
| 6 | Tonos de estado (RF-8), en etiquetas y en el calendario, con sus pruebas (CA-14) | frontend, testing | Archivos de estados de §11.1 y sus `*.spec.ts` |
| 7 | CSP: retirar `https://fonts.googleapis.com` de `style-src` y `https://fonts.gstatic.com` de `font-src` | frontend (security) | `frontend/nginx/security-headers.inc.template` |
| 8 | Verificación en el navegador (CA-1 a CA-3, CA-5, CA-6, CA-9 a CA-11, CA-13 y CA-15) y revisión de `security` de la CSP y las fuentes | testing, security | — |
| 9 | Documentación: «Temas» de `ARCHITECTURE.md`, `README.md`, `DEVELOPMENT.md`, `SECURITY.md` y `threat-model-baseline.md` (CSP sin Google Fonts), nota en ADR-0017 y revisión del manual (§17) | architecture (security) | `ARCHITECTURE.md`, `README.md`, `DEVELOPMENT.md`, `SECURITY.md`, `specs/security/threat-model-baseline.md`, `specs/architecture/adr-0017-*`, `frontend/public/manual/**` |

## 17. Manual de uso
No se prevé añadir, cambiar ni retirar ningún proceso: cambia el aspecto, no los pasos. El manual se sigue pintando igual (`marked` y el saneado de Angular). Se
revisa que ningún proceso describa colores o posiciones que cambien.

| Proceso (`id`) | Acción (nuevo / cambia / se retira) | Permiso | Pantallas (`routes`) |
|---|---|---|---|
| `iniciar-sesion` | Se revisa: el menú «Cambiar tema» y sus opciones no cambian (RF-11). El manual ya llama a la aplicación «ITMas». | — | `/login`, barra superior |
| `usar-el-manual` | Se revisa: menú lateral, botón de ayuda de la barra superior y opción **Imprimir** (RF-14, CA-15). | — | `/manual` |

## 18. Trazabilidad (al implementar)
- **Commits/PRs:** `SPEC-0010: apply the ITMas visual identity on the Material theme layer and self-host the fonts` (2026-11-03, en `main`). La spec pasa a `Implemented` el 2026-11-03, tras la segunda ejecución de `/sdd-validate`, con las revisiones obligatorias hechas y el residual Q-16 aceptado.
- **Archivos principales:**
  - **Capa de tema:** `frontend/src/styles.scss` y `frontend/src/styles/` (`_theme-colors.scss`, generado por el esquemático con
    `#004AAD`/`#1893F8`; `_tokens.scss`, `_material-overrides.scss`, `_surfaces.scss`, `_backgrounds.scss`, `_motion.scss` y `_fonts.scss`).
  - **Fuentes (RF-15):** `frontend/public/fonts/poppins/` (5 pesos, `OFL.txt`, `SOURCE.md`) y `frontend/public/fonts/material-symbols/` (instancia
    estática de peso 400, `LICENSE`, `SOURCE.md`). `frontend/src/index.html` sin Google Fonts y con `<title>ITMas</title>`;
    `frontend/src/environments/*` con `appName: 'ITMas'`.
  - **CSP (RNF-4):** `frontend/nginx/security-headers.inc.template`, solo sin `https://fonts.googleapis.com` ni `https://fonts.gstatic.com`.
  - **Layout y páginas públicas:** `layout/shell/*` (islas, fondo de producto, `ariaCurrentWhenActive` y `itmas-nav-active`),
    `layout/public-layout/public-layout.ts`, `shared/components/logo/logo.ts` (nuevo), `shared/components/status-message/status-message.ts`
    (tarjeta opaca y modo `brand`), `features/login/login-page.ts` y `features/errors/*`.
  - **Estados (RF-8):** `shared/models/status-tone.ts` (nuevo), `sql-audit-labels.ts`, `sql-status.ts`, `risk-chip.ts`, `agenda-labels.ts`,
    `status-indicator.ts`, `event-status-chips.ts`, `warning-list.ts`, `calendar-model.ts`, `inventory-labels.ts` y `devices-page.*`; los
    estados en línea de las páginas de auditoría SQL y de tipos de actividad.
  - **Migración tipográfica (RF-13):** los archivos de RF-13 (son 10: `enrollment-code-dialog.ts` aparece dos veces en la lista de 11). La guarda encontró además 5 radios literales (`device-report-page.scss`,
    `sql-audit-page.scss`, `manual-page.scss`, `audit-event-dialog.ts`, `enrollment-code-dialog.ts`), que pasan a `--mat-sys-corner-*`.
- **Desviaciones y decisiones al implementar (2026-11-03):**
  - **Plantillas de features (decisión del solicitante).** §8, RF-5 y RF-6 piden clases `itmas-*` en elementos que §11.1 y la tarea 5 no listaban.
    El solicitante decidió aplicarlas: las listas (equipos, registros, instancias, agenda, tipos, usuarios, políticas, permisos y auditoría)
    envuelven filtros, tabla y paginador en `.itmas-glass-card`; las secciones de la política y las pestañas de instancia y usuario también; las tablas
    llevan `itmas-table`, los pares dato–valor `itmas-inner`, `mat-flat-button` `itmas-cta` y `mat-stroked-button` `itmas-glass-button`. Solo
    atributos y envoltorios: no cambian textos, lógica ni roles.
  - **`mat-card` por su selector de elemento.** El desenfoque y la elevación al pasar el cursor de las tarjetas se aplican en la capa de tema con el
    selector público `mat-card` (no una clase interna); su color, borde, radio y sombra salen de `mat.card-overrides`.
  - **Poppins:** un archivo por peso con los subconjuntos latino y latino extendido juntos (no dos por peso): menos `@font-face` en la hoja inicial.
  - **Presupuesto (RNF-3):** 549,91 kB iniciales al cerrar (aviso en 550 kB; antes 534,42). Para caber: colores de los tokens en hexadecimal, sin prefijo
    `-webkit-backdrop-filter` (un navegador sin `backdrop-filter` sin prefijo recibe la alternativa opaca de RNF-2), y sin *overrides* iguales a los
    valores por defecto. Queda muy poco margen: la siguiente regla global que crezca lo superará.
    **Seguimiento (revisión de `architecture`, A-2):** antes de la próxima spec que añada peso a la carga inicial, cargar de forma diferida las hojas
    globales de FullCalendar (unos 21,8 kB en bruto; mitigación prevista en ADR-0017). No se sube el umbral de `angular.json` sin una nota de ADR.
  - **Sin avatar.** El menú de la cuenta conserva su icono, como hoy; `--itmas-cta` se usa en el botón principal y en el recuadro del logotipo.
  - **Logotipo** en `shared/components/logo/` (lo usan el *shell* y `status-message`); la guarda de CA-17 cubre también esa carpeta.
- **Pruebas:**
  - Nuevas: `frontend/src/app/styles-guard.spec.ts` (CA-8), `frontend/src/app/layout/layout-safety.spec.ts` (CA-17) y
    `frontend/src/app/fonts-provenance.spec.ts` (CA-18).
  - Ampliadas: `manual-consistency.spec.ts` (CA-16), `shell.spec.ts` (CA-4), `sql-audit-labels.spec.ts`, `agenda-labels.spec.ts`,
    `inventory-labels.spec.ts` y `calendar-model.spec.ts` (CA-14; solo cambian aserciones de presentación de tonos, CA-12).
  - `npm test -- --watch=false`: 58 archivos y 626 pruebas en verde (594 antes de endurecer las guardas tras la revisión de `testing`);
    `npm run build` sin avisos de presupuesto; Prettier limpio. `cd backend && dotnet test`: 1193 pruebas en verde (el backend no cambia).
  - Contraste calculado de los pares de §11.2 sobre el vidrio (sin orbes): todos ≥ 4,5:1 (el menor, el texto atenuado claro, 4,75:1; blanco sobre
    `#1270D6`, 4,87:1).
- **Verificación en el navegador (2026-11-03, `docker compose up --build`, nginx en `localhost:8088`):**
  - **Hecho:** CA-1 (`--mat-sys-primary` es `rgb(0, 74, 173)` en claro y `rgb(176, 198, 255)`, tono 80, en oscuro; cuerpo Poppins 300). CA-2 y
    CA-11 en las páginas públicas: `index.html` sin Google Fonts, ninguna petición a otros orígenes, fuentes servidas como `font/woff2` desde el
    propio origen, CSP servida sin los dos orígenes y consola sin violaciones. CA-10: inicio de sesión, manual y página no encontrada con el fondo
    de marca y tarjeta opaca; `returnUrl` se conserva. CA-13 en las páginas públicas (sin desplazamiento horizontal a 375 px).
  - **Hecho con sesión** (tras renovar el *client secret* local de Entra ID, que fallaba con `AADSTS7000215`, ajeno a esta spec): CA-4 (barra
    lateral de 240 px a 12 px del borde, barra superior de 56 px, indicador naranja de 3 × 20 px y `aria-current="page"`); listas en tarjeta de
    vidrio con cabeceras en mayúsculas, en claro y en oscuro; estados de los equipos como etiquetas tintadas de radio 6 px (Activo, tono positivo);
    CA-5 en el diálogo «Nuevo equipo» (superficie opaca, sin desenfoque, radio de 16 px; el botón «Crear» deshabilitado conserva el aspecto de
    Material); CA-9 en la vista Mes en oscuro; CA-13 en Equipos (sin desplazamiento horizontal a 375 px y barra lateral superpuesta). El peso 400
    de los iconos casa con Poppins.
  - **Comprobaciones del navegador del 2026-11-03 (después de `/sdd-validate`):**
    - **CA-11, hecho:** en las 13 rutas autenticadas y del manual no hay ningún evento `securitypolicyviolation`, ningún recurso de otro origen ni
      errores en la consola. Se cargan Poppins 300 a 700 y Material Symbols 400 desde el propio origen.
    - **CA-6, hecho por simulación:** el panel no emula las preferencias, así que se inyectaron como reglas normales los bloques
      `@media (prefers-reduced-motion: reduce)` y `(prefers-reduced-transparency: reduce)` de la hoja compilada. Con el primero no queda ninguna
      animación (orbes ni entrada). Con el segundo, tarjetas, `mat-card` y barra pasan a opacas (`surface-container-lowest`) y sin desenfoque.
    - **CA-15, hecho por simulación** (con un hallazgo menor ya corregido, ver abajo) (bloque `@media print` inyectado, en tema oscuro): el esquema pasa a claro y el texto se
      imprime oscuro sobre blanco, sin fondos de producto ni de marca, sin barra y sin vidrio. **Queda la sombra `level2` de la tarjeta opaca del
      manual**: la regla de impresión que la quitaba salió al recortar bytes para el presupuesto.
    - **CA-3, falló en la primera pasada (corregido, ver abajo).** Auditor de contraste en las 13 rutas, en ambos esquemas, y en los diálogos «Nuevo equipo» y evento de auditoría. Compone los
      fondos semitransparentes sobre el peor caso de la página: los dos extremos del degradado y el orbe azul detrás, como pide RNF-1. Las rutas
      del manual y los diálogos en oscuro cumplen. No cumplen:
      - **(A) Texto atenuado claro (`#6B7280`, Q-10)** directamente sobre el fondo de página: 4,40:1 sobre `#F6F5F1` sin orbe y 3,64:1 con el orbe
        azul. Afecta al texto de apoyo de todas las páginas.
      - **(B) El mismo color sobre el vidrio con el orbe detrás:** 4,33:1 (cabeceras de tabla, etiquetas de los campos, texto secundario,
        etiquetas de los *chips*).
      - **(C) El mismo color sobre `surface-container`:** 4,15:1 (el JSON del diálogo de auditoría). Ocurre igual sobre `surface-container-high`
        (avisos de auditoría SQL y del informe del equipo).
      - **(D) Oscuro, sobre el vidrio con el orbe detrás:** texto atenuado `#9CA3AF` a 4,25:1 y tono informativo `#78B6FF` a 4,31:1.
      - **(E) Elemento activo de la barra lateral:** el blanco sobre el velo blanco del 16 % queda entre 3,9 y 4,3:1 en el extremo derecho del
        elemento cuando está en la mitad inferior del degradado, por ejemplo con ventanas bajas.
      - **Con superficies opacas** (`prefers-reduced-transparency`) solo quedaban (A) y (E).
      - (A) a (D) dependían de los tonos aprobados en Q-6 y Q-10: el solicitante los cambió en Q-14.
    - **CA-7:** lo cubre `theme-service.spec.ts`. En el navegador se vio que todo cambia sin recargar al fijar `<html data-theme>`, que es lo que
      hace `ThemeService`, pero no se pulsó el menú «Cambiar tema». Observación: §15 cita pruebas del menú de tema que no existen.
    - **Correcciones (decisión Q-14 del solicitante, 2026-11-03):**
      - Texto atenuado `#5B6270` en claro y `#B4BAC5` en oscuro (primero `#A8AFBB`), tono informativo oscuro `#8EC3FF` y velo negro al 16 % en el elemento activo de la
        barra lateral (`_material-overrides.scss` y `_tokens.scss`).
      - La segunda pasada encontró los números de los días de otro mes de FullCalendar, que pintaba `--fc-classic-faint-foreground` con el rol
        `outline` (4,24:1). Ahora usan `on-surface-variant` (`agenda-calendar.scss`).
      - La impresión vuelve a quitar la sombra de la tarjeta opaca (`_motion.scss`).
    - **Resultado tras las correcciones: CA-3 cumple.**
      - Se auditaron 1835 elementos por esquema en las 14 rutas, en claro y en oscuro, y los dos diálogos en ambos esquemas: ninguno por debajo
        de su umbral.
      - Con transparencia reducida tampoco hay ninguno.
      - Único aviso del auditor: la barra de las páginas públicas en oscuro, porque el modelo supone el extremo claro del degradado de marca. Es un
        falso positivo: la barra está fija arriba, sobre el extremo oscuro (`#002D6B`), con unos 9:1 en el texto y 4,9:1 en el icono.
      - CA-15: la vista de impresión queda sin sombra, sin fondo y en esquema claro.
      - Build: 549,89 kB iniciales en ese momento, sin aviso.
- **Revisiones obligatorias (2026-11-03, independientes y de solo lectura, un agente por rol):**
  - **`security`: aceptable.** Sin hallazgos críticos, altos ni medios. Comprobó que la CSP solo retira orígenes, las sumas y licencias de las fuentes,
    la ausencia de *Reserved Font Name*, el renderizado sin `innerHTML` y la higiene del repositorio público. Checklist: `.env` ignorado, búsqueda de
    secretos limpia y `npm audit --omit=dev` con 0 vulnerabilidades. Hallazgos y su tratamiento:
    - S-1: nueva prueba «No third party serves fonts or styles» en `fonts-provenance.spec.ts`, sobre la CSP de nginx y sobre `index.html`.
    - S-2: `optimization.fonts: false` en `angular.json`.
    - S-3: tono neutro por defecto en `calendar-model.ts`.
    - S-4: verificado aparte. Las tablas `name` de los 6 WOFF2, leídas con el `fontTools` de `SOURCE.md`, solo tienen el copyright, la familia y la
      versión de origen, sin rutas ni nombres de equipo.
    - S-5 (que una segunda persona reproduzca la transformación): **pendiente, recomendado en la próxima actualización de las fuentes.**
    - S-6: informativo.
  - **`architecture`: aceptable con cambios.** Hallazgos y su tratamiento:
    - A-1: orden de tabulación, registrado como Q-15, y RNF-6 ajustado.
    - A-2: seguimiento del presupuesto, anotado arriba.
    - A-3: nota de implementación en ADR-0021, y lista permitida de elementos (`mat-card`) en la guarda.
    - A-4: las tarjetas dentro de `.itmas-glass-card` ya no desenfocan dos veces.
    - A-5: Q-15 y ampliación de §11.1.
    - A-6: comentario en el mixin `on-glass-dark`.
    - A-8: restos de texto corregidos (avatar, cifras, recuento de RF-13, ADR-0005 y la lista de *overrides* de ADR-0021).
    - A-7, A-9 y A-10: informativos.
  - **`testing`: aceptable con cambios.** Hallazgos y su tratamiento:
    - T-1 y T-2: verificaciones completadas (ver abajo).
    - T-3: §15 corregido.
    - T-4 a T-9: `styles-guard.spec.ts` reescrita como funciones puras y probada con 35 violaciones conocidas. Detecta:
      - los alias `>>>` y `/deep/` de `::ng-deep`, y cualquier `encapsulation:`;
      - interpolación SCSS, propiedades anidadas, `@extend`, `@use`/`@import` que no sean `sass:` y selectores `[class*=mat-]`;
      - `styles: [...]`, `<style>`, `style='…'`, `[attr.style]` y `style` en `host`;
      - más propiedades de color y tipografía, y la gramática de colores aplicada a las variables `--fc-*`;
      - comentarios y espacios dentro de `url(…)` y de cadenas, `@import 'https://…'` e `image-set(…)`;
      - el naranja en forma `rgb()`.
    - T-10: `layout-safety.spec.ts` no distingue mayúsculas de minúsculas.
    - T-11: no admite archivos sueltos en `public/fonts/` y exige `font-display` en cada `@font-face` (`swap` en Poppins y `block` en los iconos, Q-17).
    - T-13: los avisos usan `STALE_STATUS` y `UNAVAILABLE_STATUS` en lugar de literales.
    - T-12: CA-6 y CA-15 siguen verificados por simulación.
  - **Fuera de esta spec:** `features/agenda/agenda-safety.spec.ts` (SPEC-0006) tiene el mismo hueco de mayúsculas que T-10. Se propone corregirlo en un
    commit `minor:` aparte.
- **Verificación en el navegador tras las revisiones (2026-11-03, imagen reconstruida con `docker compose up --build -d frontend`):**
  - **CA-3:**
    - El auditor de contraste recorrió 22 rutas en claro y en oscuro: inicio; equipos con sus registros y su último informe; instancias SQL; agenda en
      lista, Año, Mes y Semana; nueva actividad; nueva serie; tipos; usuarios y detalle de usuario; políticas y nueva política; permisos; auditoría;
      manual e `iniciar-sesion`; acceso denegado; y página no encontrada.
    - Son 2427 elementos por esquema y ninguno queda por debajo de su umbral. La segunda pasada encontró las etiquetas de los pares dato–valor del
      informe en oscuro (4,25:1); el atenuado oscuro se aclaró a `#B4BAC5` (Q-14).
    - Diálogos: «Nuevo equipo», evento de auditoría, editar tipo, editar permiso, asignar política y confirmación de eliminar. Esta última se cerró
      con «Cancelar»; se comprobó que no se eliminó nada.
  - **CA-5:** menú de acciones, lista de selección, *tooltip* y esos diálogos: todos opacos (`surface-container*`, `inverse-surface`), sin
    desenfoque y con el contraste correcto en ambos esquemas.
  - **CA-7:** con el menú «Cambiar tema», «Oscuro» se aplica sin recargar, se guarda en `itmas3.theme` y se conserva al recargar. Después se dejó
    «Según el sistema».
  - **CA-9:** Año, Mes y Semana cumplen el contraste en ambos esquemas, con el esquema cambiado en caliente.
  - **CA-10:** se añade `/forbidden`, con fondo de marca y tarjeta opaca.
  - **CA-13:** las 22 rutas a 375 px no tienen desplazamiento horizontal (página ni contenido), y la barra lateral está en modo superpuesto.
  - **No alcanzables sin crear datos** (en el entorno local no hay políticas, actividades ni instancias SQL, y no se crearon):
    - detalle y edición de política;
    - detalle y edición de actividad, con sus diálogos de completar y cancelar;
    - instancia y cuenta SQL;
    - el diálogo del código de enrolamiento, que regenera el código.
    Usan los mismos componentes, tokens y superficies que las pantallas auditadas.
    **Riesgo residual aceptado por el solicitante el 2026-11-03 (Q-16)**, después de la segunda ejecución de `/sdd-validate`. Se revisarán cuando
    haya datos.
  - **CA-11 en el resto de rutas alcanzables (2026-11-03):** el detector de `securitypolicyviolation` también recorrió los informes del equipo, nueva
    actividad, nueva serie, detalle de usuario, nueva política, acceso denegado, página no encontrada y un proceso del manual. No hubo ninguna
    violación ni ningún recurso de otro origen. El único error de la consola fue un 400 de una petición de sondeo hecha a mano durante la verificación
    (`agenda-events?year=…`), no de la aplicación.
  - **Fuera de la tabla de `AGENTS.md`:** `git check-ignore .env`, la búsqueda de secretos y `npm audit` constan solo como resultado de la revisión de
    `security` (arriba), porque `/sdd-validate` no puede ejecutarlos.
- **Manual de uso actualizado:** revisados `iniciar-sesion` y `usar-el-manual`: siguen siendo correctos (barra superior, menú lateral, «Cambiar tema»,
  botón de ayuda e **Imprimir** no cambian) y ningún proceso describe colores o posiciones que cambien. Sin cambios en `frontend/public/manual/`.

## 19. Decisiones y preguntas abiertas

### 19.1 Decisiones del solicitante (2026-11-03)
| # | Tema | Decisión del solicitante | Efecto en la spec |
|---|---|---|---|
| Q-1 | Nombre visible | **ITMas** («todo es ITMas»). | RF-10: `appName` y `<title>` pasan a «ITMas». |
| Q-2 | Iconos | **Autoalojar** el mismo conjunto, Material Symbols Outlined. | RF-15 y RNF-4: sin Google Fonts; `font-src 'self'`. Desaparece el riesgo residual de terceros. |
| Q-3 | Origen de Poppins | **Archivos copiados** con su procedencia. | RF-15 y CA-18. Sin dependencia npm. |
| Q-4 | Botón principal | **Degradado si es viable**; si no, sólido. | RF-7. |
| Q-5 | Ancho del contenido | **Fluido hasta 1440 px**. | RF-3. |
| Q-6 | Tonos del tema oscuro | **Aprobados** los de la maqueta. | §11.2. |
| Q-7 | Nombrar otras marcas en el repositorio | **No**: todo es ITMas. | El título, los archivos y el texto de esta spec y de ADR-0021 no nombran otras marcas. |
| Q-8 | Orbes del fondo | **Animados**, salvo con reducción de movimiento. | RF-4 y RNF-2. |
| Q-9 | Verificación de contraste | **Manual** en esta spec. La automatización iría en otra. | §15. |
| Q-10 | Neutros | **Los de la maqueta**, fijados sobre los roles de Material. | RF-1 y §11.2. |
| Q-11 | Tono de cada estado | **Aprobada** la tabla propuesta. | RF-8 y CA-14. |
| Q-12 | Primario | **`#004AAD` exacto** en claro y tono 80 en oscuro. | RF-1 y CA-1. |
| Q-13 | Ruta de la página y etiqueta de sección | **No** en esta spec. | RF-3 y RF-6. |
| Q-14 | Contraste de los tonos aprobados (2026-11-03, tras la verificación de CA-3) | **Oscurecer el atenuado claro y aclarar el atenuado y el informativo oscuros**; velo oscuro en el elemento activo de la barra lateral. | §11.2: atenuado `#5B6270` / `#B4BAC5` e informativo oscuro `#8EC3FF`, que reemplazan a los de Q-6 y Q-10. El primer valor oscuro (`#A8AFBB`) se aclaró a `#B4BAC5` tras la segunda auditoría: daba 4,25:1 sobre el vidrio anidado del informe del equipo. El peor caso medido es 4,62:1. |
| Q-15 | Alcance de las plantillas y orden de tabulación (2026-11-03, durante la implementación y tras la revisión de `architecture`) | **Aplicar las clases `itmas-*` en las plantillas de features** (listas en tarjeta de vidrio, tablas, pares dato–valor y botones) y **mantener el nuevo orden de tabulación del *shell***: navegación y después barra superior. | §11.1 amplía los archivos; RNF-6 recoge el orden de tabulación. |
| Q-16 | Pantallas sin datos locales (2026-11-03, tras `/sdd-validate`) | **Aceptarlas como riesgo residual**: detalle y edición de política y de actividad (con los diálogos de completar y cancelar), instancia y cuenta SQL y el diálogo del código de enrolamiento no se recorrieron, porque el entorno local no tiene esos datos y no se crearon. | CA-3, CA-11 y CA-13 se dan por cubiertos en las 22 rutas alcanzables; estas pantallas usan los mismos componentes, tokens y superficies. |
| Q-17 | Carga de la fuente de iconos (2026-11-03, tras publicar `v1.1.1`) | **`font-display: block` para Material Symbols**: con `swap`, en la primera visita se veían un instante los nombres de los iconos («hub», «login») como texto. Poppins mantiene `swap`. | RNF-3; `_fonts.scss` y `fonts-provenance.spec.ts`, que exige `swap` en Poppins y `block` en los iconos. |
| Q-18 | Icono de la pestaña (2026-11-05) | **Logotipo de ECS** en lugar del icono por defecto de Angular. ECS es la organización propietaria de ITMas, no otro producto. | RF-10; `frontend/public/favicon.ico` en 16, 32, 48, 64 y 128 px con fondo transparente. |

### 19.2 Preguntas abiertas
Ninguna.
