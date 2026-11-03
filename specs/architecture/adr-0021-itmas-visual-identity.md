---
id: ADR-0021
title: Identidad visual propia de ITMas sobre Angular Material (enmienda de §6, MAJOR 4.0.0)
status: Accepted
date: 2026-11-03
deciders: [architecture, security, frontend]
related: [SPEC-0010, ADR-0005, ADR-0017, ADR-0013]
---

# ADR-0021 — Identidad visual propia de ITMas sobre Angular Material

> **Propuesto el 2026-11-03** a petición del solicitante, después de revisar una maqueta con el inicio de sesión, la lista de equipos y el informe de un equipo, en
> tema claro y oscuro. La maqueta es un artefacto privado con datos ficticios: no se enlaza ni se suben capturas suyas, porque el repositorio es público.
>
> **Revisión independiente desde los roles `architecture` y `security` hecha el 2026-11-03**: «aceptable con cambios» en ambas. Sus hallazgos (A-1 a A-13 y
> S-1 a S-7) están incorporados.
>
> **Aceptado por el solicitante el 2026-11-03**, con las decisiones de [SPEC-0010](../features/SPEC-0010-itmas-visual-identity.md) §19.1:
> - Q-7: la identidad es de ITMas («todo es ITMas») y el repositorio no nombra otras marcas.
> - Q-2: Material Symbols se sirve desde el mismo origen, así que no queda riesgo residual de Google Fonts que aceptar.
>
> La constitución pasa a la versión **4.0.0** en el mismo cambio.

## Contexto
El solicitante pide una identidad visual propia para ITMas: vidrio translúcido sobre un fondo claro, azul de marca `#004AAD`, tipografía Poppins, sombras de
varias capas y el naranja `#F2982A` solo como detalle.

La constitución lo impedía. Su §6, hasta la versión 3.0.0, decía:

> 2. La UI DEBE usar **Angular Material siguiendo Material Design 3 estándar**: componentes oficiales siempre que exista uno adecuado; paletas,
>    tipografía, densidad y elevaciones por defecto del tema definido en `styles.scss`.
> 3. NO DEBE crearse un sistema de diseño propio ni sobrescribir tokens, colores, tipografías o el comportamiento de los componentes Material. El CSS
>    de componentes se limita a layout y usa variables `--mat-sys-*`.
> 4. Los temas claro y oscuro DEBEN seguir funcionando para toda pantalla nueva.

[ADR-0005](adr-0005-frontend-material3-theming.md) descartó expresamente la «paleta corporativa personalizada», porque estaba «prohibido por el requerimiento
en esta etapa». Por §1.1 y §1.3, ni una spec ni un ADR pueden apartarse de §6 sin una enmienda.

Lo que se sabe del código actual (verificado en la revisión del 2026-11-03):
- **Tema.** `frontend/src/styles.scss` define un único `mat.theme` (paletas `azure`/`blue`, Roboto, densidad 0) con valores `light-dark()`. `ThemeService` fija el
  esquema con `<html data-theme>` y el menú de la barra superior ofrece **Claro**, **Oscuro** y **Según el sistema**.
- **Estilos de componente.** Hay 36 archivos: 11 hojas `.scss` y 25 componentes con `styles` en línea. De ellos, 27 usan roles `--mat-sys-*`.
  - Ninguno fija colores literales ni usa selectores de clases de Material o del CDK.
  - Sí hay **13 declaraciones tipográficas literales en 11 archivos**: tamaños de icono (18 y 48 px), la familia monoespaciada en cinco sitios y cuatro
    tamaños, pesos o espaciados sueltos.
  - Hay dos excepciones a la encapsulación: `features/manual/manual-page.scss` llega con `::ng-deep` al HTML del manual que pinta (`.content`), y
    `features/agenda/calendar/agenda-calendar.ts` usa `ViewEncapsulation.None` para asignar las variables de FullCalendar.
- **Estados.** Usan los roles `primary`, `tertiary`, `error`, `on-surface-variant` y `outline`. Las advertencias (riesgo alto, certificado sin verificar, datos no
  disponibles, sin actualizar) usan `tertiary`, que hoy es azul. El calendario pinta las actividades con `primary-container` y `tertiary-container`.
- **Fuentes.** Roboto y Material Symbols se cargan de Google Fonts. La compilación de producción incrusta la CSS de Google en `index.html`
  (`optimization.fonts: true`), así que en ejecución solo se contacta `fonts.gstatic.com`. La CSP de nginx admite `https://fonts.googleapis.com` en `style-src` y
  `https://fonts.gstatic.com` en `font-src`.
- **Nombre.** El manual llama a la aplicación «ITMas» en 55 sitios. La interfaz muestra «ITMAS3» (`environment.appName` y `<title>`).

La maqueta dejó cuatro hallazgos:
1. **Tema oscuro.** La maqueta partió de un diseño claro. §6.4 exige el oscuro, así que se define aquí y en SPEC-0010.
2. **Contraste.**
   - Los tonos de texto de estado de partida no alcanzaban 4,5:1 sobre su propio fondo tintado.
   - El texto blanco sobre `#1893F8` queda en 3,2:1. Los degradados que llevan texto encima terminan en `#1270D6` (unos 4,9:1).
3. **Iconos.** La maqueta usaba iconos de trazo. ITMas usa Material Symbols en `mat-icon` en todas sus pantallas y los conserva.
4. **Marca.** ITMas conserva su nombre y tiene su propio logotipo.

## Decisión
1. **Angular Material sigue siendo la base.** Se usan sus componentes oficiales siempre que exista uno adecuado. No se tocan su comportamiento, su
   accesibilidad (foco, teclado, ARIA) ni su densidad.
2. **ITMas tiene una identidad visual propia**, que se aplica **solo en la capa de tema**: `frontend/src/styles.scss` y `frontend/src/styles/**`. Esa capa usa:
   - `mat.theme` con **paletas generadas** por el esquemático `@angular/material:theme-color` (que escribe `_theme-colors.scss`) a partir de `#004AAD`
     (primaria) y `#1893F8` (terciaria), tipografía **Poppins** y densidad 0.
     - El rol `primary` del esquema claro se fija en `#004AAD` exacto con `mat.theme-overrides`. En el oscuro se usa el tono 80 de la misma paleta.
     - Los neutros de texto y líneas se fijan con `mat.theme-overrides` (SPEC-0010 §11.2).
     - Ningún rol de Material se asigna al naranja, porque el terciario pinta contenedores y el naranja nunca rellena.
   - `mat.theme-overrides` para los roles de superficie, forma y tipografía, y `mat.<componente>-overrides` (tarjeta, *sidenav*, barra, tabla, campos, botones,
     chips) para los tokens de cada componente.
   - Tokens propios **`--itmas-*`** para lo que Material no modela: degradados, vidrio, sombras de varias capas, tonos de los estados y el detalle naranja.
     Cada color (incluidas las paradas de los degradados y los colores de las sombras) usa `light-dark()`. Los valores no cromáticos (desenfoque, desplazamiento
     de las sombras, radios) son comunes a ambos esquemas.
   - Clases de superficie **`itmas-*`** (vidrio de tarjeta, vidrio oscuro, barra metálica, elemento anidado y fondos), que las plantillas aplican a sus propios
     elementos. **Solo fijan lo que Material no expone como token** (degradados, `backdrop-filter`, sombras de varias capas, fondos). Además:
     - No usan `!important`.
     - Respetan los estados de Material (`:disabled`, foco, selección).
     - Si un efecto solo se logra estilizando clases internas de Material, se renuncia a él y se usa el token disponible (por ejemplo, el medidor de uso
       queda en color sólido).
3. **Fuera de la capa de tema**, el CSS de componentes (hojas y `styles` en línea) cumple el §6.3 de abajo.
   - Hay dos excepciones a la encapsulación, enumeradas aquí:
     - (a) `features/manual/manual-page.scss`: `::ng-deep` limitado a `.content`, el HTML saneado del manual.
     - (b) `features/agenda/calendar/agenda-calendar.ts`: `ViewEncapsulation.None` para asignar las variables `--fc-*` de FullCalendar a roles `--mat-sys-*`
       (ADR-0017).
   - Las 13 declaraciones tipográficas literales actuales se migran a roles de la escala o a tokens `--itmas-*` (familia monoespaciada y tamaños de icono) en
     SPEC-0010.
   - Una prueba de guarda lo comprueba, incluidos los `styles` en línea.
4. **Claro y oscuro.** Los dos esquemas salen del mismo tema. El oscuro es propio de ITMas: fondo azul profundo derivado de la marca, el mismo vidrio con menos
   opacidad y texto claro. En ambos, todo texto cumple **WCAG 2.2 AA** (4,5:1, o 3:1 en texto grande, iconos e indicadores de foco), también sobre los
   degradados y el vidrio. Los estados conservan ícono y texto: el color nunca es la única señal.
5. **Paneles flotantes opacos.** Menús, listas de selección, diálogos, *tooltips* y avisos usan los roles opacos `surface-container*` de la paleta generada, con
   sombra. El vidrio queda solo en las superficies de fondo (barra lateral, barra superior y tarjetas), porque un panel translúcido deja leer el texto que tapa.
6. **Movimiento, transparencia e impresión.**
   - Solo se animan `transform`, `opacity`, `box-shadow` y `filter`.
   - Con `prefers-reduced-motion` desaparecen los orbes animados y las entradas.
   - Con `prefers-reduced-transparency` (donde el navegador la exponga), o sin soporte de `backdrop-filter`, las superficies de vidrio pasan a ser opacas.
   - Al imprimir (`@media print`) no hay fondos, orbes ni vidrio. El manual promete la opción **Imprimir**.
7. **Fuentes y CSP.**
   - Poppins (SIL OFL 1.1) y Material Symbols Outlined (Apache 2.0) se sirven desde el mismo origen, con procedencia verificable (SPEC-0010, RF-15), nunca
     como `data:`.
   - Roboto deja de cargarse.
   - ITMas deja de depender de Google Fonts, en ejecución y al compilar.
   - **La CSP no se relaja**: el cambio de `frontend/nginx/security-headers.inc.template` solo retira orígenes (`https://fonts.googleapis.com` de `style-src` y
     `https://fonts.gstatic.com` de `font-src`).
8. **Marca.**
   - ITMas conserva su nombre, que pasa a mostrarse como «ITMas» en toda la interfaz.
   - Tiene un logotipo tipográfico propio en Poppins, con el punto naranja como detalle.
   - El logotipo es texto y `mat-icon` en la plantilla: no se carga SVG remoto ni se inyecta con `innerHTML`.
   - No se usan elementos de marca de otros productos.
9. **Relación con otros ADR.**
   - Este ADR reemplaza a ADR-0005 en las paletas, la tipografía, la regla «sin sistema de diseño propio» y la carga de fuentes desde Google Fonts. ADR-0005
     sigue vigente en lo demás: un único tema con `light-dark()`, `ThemeService`, Material Symbols Outlined como conjunto de iconos y la estructura de carpetas.
   - ADR-0017 mantiene su integración (variables `--fc-*` asignadas a roles `--mat-sys-*`). Los colores de las actividades del calendario pasan a seguir los
     tonos de estado de SPEC-0010 (RF-8), y ADR-0017 recibe una nota con ese cambio.

### Texto de la constitución (versión 4.0.0)

**§6.2 a §6.4** (sustituyen a los anteriores):

> 2. La UI DEBE usar **Angular Material**, con componentes oficiales siempre que exista uno adecuado, sobre un **único tema Material 3** definido en la capa de
>    tema (`frontend/src/styles.scss` y `frontend/src/styles/**`). Ese tema aplica la identidad visual de ITMas (ADR-0021): paletas, tipografía, forma,
>    superficies y fondos.
> 3. La identidad visual se define **solo en la capa de tema**, mediante las API de tema de Angular Material (`mat.theme`, `mat.theme-overrides`,
>    `mat.<componente>-overrides`) y los tokens `--itmas-*` y clases `itmas-*` que declara. NO DEBE alterarse el comportamiento ni la accesibilidad de los
>    componentes Material. Fuera de la capa de tema, el CSS de componentes (hojas y estilos en línea) se limita a layout:
>    - NO DEBE usar selectores de clases que añadan Angular Material o el CDK (`.mat-*`, `.mdc-*`, `.cdk-*`).
>    - NO DEBE usar `::ng-deep` ni `ViewEncapsulation.None`, salvo las excepciones que enumera ADR-0021.
>    - NO DEBE declarar variables `--mat-*` o `--itmas-*` ni incluir mixins `mat.*`.
>    - En color, fondo, color de borde, sombra, filtro, radio de esquina y tipografía solo admite `var(--mat-sys-*)`, `var(--itmas-*)`, `inherit`,
>      `currentColor`, `transparent`, `none` o `0`.
> 4. Los temas claro y oscuro DEBEN funcionar en toda pantalla, con contraste WCAG 2.2 AA en ambos. Las animaciones DEBEN respetar `prefers-reduced-motion`, y
>    las transparencias, `prefers-reduced-transparency` donde el navegador la exponga.

**Cabecera e historial:**

> **Versión:** 4.0.0 · **Vigente desde:** 2026-11-03
>
> | 4.0.0 | 2026-11-03 | MAJOR: §6.2 y §6.3 admiten una identidad visual propia de ITMas en la capa de tema (paletas, tipografía, forma, superficies y fondos) mediante las API de tema de Angular Material, tokens `--itmas-*` y clases `itmas-*`, sin alterar el comportamiento ni la accesibilidad de sus componentes; fuera de la capa, el CSS de componentes sigue siendo de layout, con reglas comprobables. §6.4 exige WCAG 2.2 AA en ambos esquemas en toda pantalla y respetar `prefers-reduced-motion` y `prefers-reduced-transparency` (ADR-0021). |

### Versión: MAJOR 4.0.0
§1.2 fija el nivel por el efecto. Quitar «NO DEBE crearse un sistema de diseño propio ni sobrescribir tokens, colores, tipografías» elimina una prohibición, así
que corresponde MAJOR aunque §6.3 y §6.4 añadan reglas. Es el criterio de ADR-0015 y ADR-0019. Registrarla como MINOR infraclasificaría una relajación.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| **1. Identidad en la capa de tema, sobre Angular Material (elegida)** | Un solo lugar que cambiar; los componentes conservan su accesibilidad y comportamiento; las pantallas ya usan roles `--mat-sys-*`, así que la mayor parte del cambio llega por el tema; sin dependencias de interfaz nuevas | Relaja §6.3; los tokens de *overrides* pueden cambiar entre versiones de Material; los degradados y el desenfoque necesitan clases `itmas-*`, porque los tokens de color no admiten degradados; algunos efectos se pierden si exigen clases internas |
| 2. Solo paleta y tipografía de marca, sin vidrio ni fondos | Cambio mínimo y casi todo por `mat.theme` | Sigue exigiendo enmienda (§6.3 prohíbe cambiar colores y tipografías) y no logra el aspecto que se pide |
| 3. Adoptar tal cual un kit de interfaz externo | Menos diseño propio | Un segundo marco de estilos junto a Material; los componentes Material quedarían con su aspecto por defecto o se estilizarían por sus clases internas |
| 4. Sustituir Angular Material por componentes propios | Control total del aspecto | Se pierde la accesibilidad y el mantenimiento de Material; contradice lo esencial de §6.2; mucho más código y pruebas |
| 5. No enmendar y mantener ADR-0005 | Sin coste ni riesgo | No atiende la petición |

## Consecuencias
- **Positivas:**
  - ITMas tiene una identidad propia y reconocible, definida en un solo lugar.
  - Los componentes y su accesibilidad no cambian, y la mayor parte de las pantallas cambian solo a través del tema.
  - La regla de §6.3 pasa a ser comprobable por una prueba.
  - ITMas deja de depender de Google Fonts: `font-src` queda en `'self'` y `style-src` pierde `https://fonts.googleapis.com`. Ningún tercero recibe datos de
    las visitas por las fuentes.
- **Negativas / riesgos:**
  - **Se relaja §6.3**: ITMas mantiene un sistema visual propio, con su coste de diseño y de mantenimiento.
  - **Actualizaciones de Angular Material:** los nombres de los tokens de `mat.<componente>-overrides` pueden cambiar entre versiones mayores (esos mixins fallan
    si un token deja de existir). `mat.theme-overrides`, en cambio, ignora sin avisar las claves desconocidas. Se mitiga usando solo las API públicas de
    *theming* y repitiendo la verificación visual en cada actualización mayor.
  - **Rendimiento:** `backdrop-filter` cuesta en equipos modestos y en tablas largas. Se limita a las superficies de fondo, nunca a filas ni a elementos
    anidados, y tiene la alternativa opaca del punto 6.
  - **Peso y mantenimiento de las fuentes propias:** se sirve una instancia estática de Material Symbols (el archivo variable completo pesa varios MB, ADR-0005).
    Actualizar Poppins o los iconos exige repetir el procedimiento de procedencia de SPEC-0010.
  - **Contraste sobre vidrio:** depende de lo que haya detrás. Se verifica en ambos esquemas, con los orbes y en móvil (SPEC-0010).
  - **Tema oscuro:** sus tonos los propuso la maqueta y los aprobó el solicitante (SPEC-0010, Q-6). No hay otra referencia.
  - **Migración:** las 13 declaraciones tipográficas literales y las aserciones de presentación de los estados cambian en SPEC-0010.
  - **FullCalendar:** su `theme.css` fija tamaños de letra y sombras propios (limitación aceptada en ADR-0017). Con Poppins puede notarse algo más la diferencia.
  - **Transición:** hasta implementar SPEC-0010 siguen la carga desde Google Fonts aceptada en ADR-0005 y el tema actual. Con la constitución 4.0.0, el código
    de hoy solo incumple §6.3 en las 13 declaraciones tipográficas que migra SPEC-0010.
- **Seguimiento:**
  - **Hecho el 2026-11-03, en el mismo cambio que `constitution.md`:**
    1. `constitution.md`: §6.2 a §6.4, cabecera de versión e historial con el texto de arriba.
    2. `agents/frontend.md`:
       - La misión deja de decir «Material Design 3 estándar».
       - La prohibición sobre `styles.scss` remite a §6.3.
       - La responsabilidad 2 admite `--itmas-*` y las clases `itmas-*`.
       - Las responsabilidades incluyen el contraste, las preferencias de movimiento y transparencia y la impresión.
       - `owns` añade `frontend/public/fonts/**`.
    3. `agents/README.md`: la descripción del agente `frontend` y la fila de `frontend/public/fonts/**` (frontend, con revisión de `security`).
    4. `AGENTS.md` («Reglas que más se olvidan») y `DEVELOPMENT.md` (guía de estilos del frontend) remiten a §6.3.
    5. ADR-0005: nota de reemplazo parcial por este ADR.
  - **Hecho el 2026-11-03:** el solicitante aprobó SPEC-0010.
  - **Al implementar SPEC-0010:**
    - `ARCHITECTURE.md` (sección «Temas»), `README.md` y `DEVELOPMENT.md` (descripción del frontend).
    - `SECURITY.md` y `specs/security/threat-model-baseline.md`: la CSP sin Google Fonts.
    - La nota de ADR-0017 sobre los colores del calendario.
    - El manual, si cambia algo de lo que describe.
  - **Reexaminar** si cambia la identidad corporativa, si Angular Material cambia su API de *theming* o si se sustituye el conjunto de iconos.

## Impacto en la constitución
**Enmienda de §6.2 a §6.4, versión MAJOR 4.0.0** (§1.2: elimina una prohibición), con el texto de «Texto de la constitución (versión 4.0.0)». Cumple §1.2 con este
ADR, con la revisión de `architecture` y `security` (independiente, 2026-11-03) y con el incremento de versión y su fila en el historial, hechos en el mismo
cambio. La aceptación del solicitante (2026-11-03) la exigen `agents/README.md` y `agents/architecture.md` (§1.2 no la menciona). No cambian §3.9 (CSP), §6.1 ni
§6.5 a §6.7.
