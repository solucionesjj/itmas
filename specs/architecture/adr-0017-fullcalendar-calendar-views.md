---
id: ADR-0017
title: FullCalendar para las vistas de calendario de la agenda, sujeto al tema Material
status: Accepted
date: 2026-10-18
deciders: [architecture, security]
related: [SPEC-0006, ADR-0005]
---

# ADR-0017 — FullCalendar para las vistas de calendario de la agenda, sujeto al tema Material

> **Propuesto el 2026-10-18** con el resultado de la prueba técnica previa de SPEC-0006 §10.4 (tarea 13). **Aceptado por el solicitante el 2026-10-19**
> (decisión A de la spec), **con el riesgo residual de la cadena de suministro** de «Negativas / riesgos», que **volvió a aceptar de forma explícita el
> 2026-10-21** (SPEC-0006 §19.8). Revisión de `security` (cadena de suministro y CSP) **hecha el 2026-10-21** (SPEC-0006 §18): sin hallazgos críticos
> ni altos; la CSP no se relaja.

## Contexto
La agenda (SPEC-0006 RF-12) ofrece tres vistas de calendario además de la lista del año: **Año**, **Mes** y **Semana**. Angular Material
no incluye un calendario de eventos: `mat-calendar` es un selector de fechas. La constitución pide el componente oficial «siempre que
exista uno adecuado» (§6.2), prohíbe sobrescribir los tokens y los componentes de Material (§6.3) y exige justificar toda dependencia
nueva (§12.5).

Fuerzas en juego:
- La semana (colocación por hora, actividades simultáneas, las que cruzan la medianoche, franja de día completo) es la parte laboriosa de
  construir un calendario propio.
- Todas las fechas de la agenda son locales a la zona de la organización y no pasan por la del navegador (RNF-9).
- El calendario debe ser accesible (RNF-10), de solo lectura (Q-18) y no agravar la cadena de suministro ni la CSP (§3.9).

## Decisión
1. **Biblioteca.** FullCalendar **7.1.3** con su conector oficial de Angular, con **versiones exactas** (sin `^`) y `package-lock.json`:
   `fullcalendar` 7.1.3, `@fullcalendar/angular` 7.1.3 y `temporal-polyfill` 1.0.6 (dependencia par obligatoria de la v7). Licencia MIT.
   Arrastra `@fullcalendar/core`, `@full-ui/headless-calendar` (7.1.3, MIT), `preact` 10.29.8 (MIT), `temporal-spec` 1.0.1 (Apache-2.0) y
   `temporal-utils` 1.0.3 (MIT): **8 paquetes nuevos**, ninguno con *scripts* de instalación (se instalaron con `--ignore-scripts`) y
   `npm audit` sin vulnerabilidades.
2. **Complementos permitidos.** Solo los estándar (no *premium*): `daygrid` (Mes), `timegrid` (Semana), `multimonth` (Año), `interaction`
   (solo para `dateClick`) y el tema `classic`. En la v7 viven como subrutas del propio paquete (`@fullcalendar/angular/daygrid`…); los
   paquetes de complementos sueltos de la v6 (`@fullcalendar/daygrid`, `@fullcalendar/timegrid`, etc.) **no se instalan**. `@fullcalendar/core`
   **sí** se instala (7.1.3), como dependencia de `@fullcalendar/angular`: es uno de los 8 paquetes del punto 1. No se usan *scheduler*, recursos ni líneas
   de tiempo, ni la vista `list`: la **lista del año** sigue siendo una tabla de Angular Material (necesita selección múltiple, columnas
   propias y un menú de acciones).
3. **Sujeción al tema (§6.3).** Se usa el tema `classic` y **no** su `palette.css`: las variables `--fc-classic-*` se asignan a los roles
   `--mat-sys-*` en la hoja de la feature (`calendar/agenda-calendar.scss`), solo como mapeo. Así el esquema claro y el oscuro siguen el del
   tema de la aplicación sin ningún color propio. El estado de cada actividad se dibuja con un rol de color de Material 3 (vía `color` y
   `contrastColor` del evento, que son `var(--mat-sys-*)`), **siempre con ícono y texto** (plantilla `eventContent`), una leyenda visible y
   el nombre accesible completo; el tipo se nombra, no se colorea.
4. **Solo lectura.** `editable` apagado; sin arrastrar ni redimensionar (Q-18). El complemento `interaction` solo sirve para `dateClick`
   (planificar desde un día u hora vacíos, RF-12b).
5. **El estado manda la página.** La vista, el periodo y los filtros viven en la dirección (RF-12). El componente `app-agenda-calendar`
   solo dibuja lo que la página le da (vista, día, eventos, zona) y avisa cuando FullCalendar cambia de vista por sí mismo (el título de un
   mes, un día): la página navega y un `effect` mantiene a ambos en paso. La barra de herramientas es la de la página (Anterior, Hoy,
   Siguiente y el título), no la de FullCalendar (`headerToolbar: false`).
6. **Tiempo (RNF-9).** El calendario recibe la zona IANA de la organización (`timeZone`) y las fechas locales **sin desplazamiento**, tal
   como las da la API: nunca pasan por la zona del navegador. Un evento de día completo termina el día **siguiente** a su último día
   (FullCalendar quiere fin exclusivo; la API da el último día incluido, P-24).
7. **Carga.** El componente se carga con `@defer (on immediate)` al elegir una vista de calendario: la lista del año no paga su tamaño. Las
   hojas de FullCalendar (`skeleton.css` y la del tema) van en los estilos globales de `angular.json`, porque el presupuesto de estilos de
   un componente es de 8 kB y las dos juntas lo superan.
8. **Datos y seguridad.** Los eventos se pasan como una matriz, calculada en el cliente a partir de `GET /agenda-events`; no se usan
   fuentes de eventos por URL (el código de `fetch` de FullCalendar queda sin usar y `connect-src 'self'` lo limitaría igualmente). Todo lo
   que escribe una persona se pinta como texto (plantillas de Angular, sin `innerHTML`); el enlace de cada evento se construye solo con su
   id.
9. **Español.** La localización sale de `fullcalendar/locales/es` (el conector no publica la subruta `@fullcalendar/angular/locales/es`
   en la 7.1.3: el archivo no está en el paquete).

## Resultado de la prueba técnica previa (SPEC-0006 §10.4, tarea 13)
| # | Verificación | Resultado |
|---|---|---|
| 1 | Angular 22 *zoneless* y con *signals* | **Funciona.** El conector declara soporte de Angular 16 a 22 y usa `ngDoCheck`, que se ejecuta en cada ciclo de detección. Con entradas de señal pinta, recibe eventos nuevos y cambia de vista mediante un `effect`, sin `NgZone`. Verificado en pruebas (TestBed sin zona) y en el navegador. |
| 2 | Horas en la zona de la organización | **Funciona** con la opción `timeZone` y fechas locales sin desplazamiento. Probado con la zona del proceso cambiada a `Pacific/Auckland`, `Asia/Tokyo` y `America/Los_Angeles`: las horas y los días no cambian; una actividad de 23:30 a 00:30 se reparte entre dos días. Una zona que el navegador no conozca lanzaría `RangeError`: la API valida la zona al arrancar. |
| 3 | `temporal-polyfill` | **Obligatorio**, no opcional: es dependencia par de la v7 y la biblioteca importa su API funcional (`temporal-polyfill/fns/*`), que queda empaquetada; no necesita un `Temporal` global en el navegador. |
| 4 | Teclado y nombres accesibles | **Funciona.** Cada día es un enlace tabulable con nombre («Ir al 14 de octubre de 2026») y Enter lo activa; cada actividad es un `<a href>` con su nombre completo (título, tipo, estado); la columna de hoy lleva `aria-current`; el foco se ve (anillo del color primario). Limitación: con **una sola columna** de meses (pantalla estrecha) FullCalendar no convierte el título del mes en enlace; se llega al mes con el selector de vista. El texto de ese enlace en pantalla ancha era «Ir al 2026-01» y se sustituyó por «Ir a Enero de 2026» (`navLinkHint`). |
| 5 | Pruebas con Vitest y jsdom | **Funcionan** con un *stub* de `ResizeObserver` (jsdom no lo trae). Lo que depende de medir (cuántos meses caben en una fila, el corte de «+n más», los colores) no se puede probar allí y se verificó en el navegador. |
| 6 | Tamaño | Trozo diferido de FullCalendar: **338 kB** sin comprimir (**83,5 kB** con Brotli), que no se carga con la lista del año. Hojas en los estilos globales: **+15,5 kB** sin comprimir (**+3,1 kB** transferidos); el total inicial pasó de 518,6 a 534,4 kB (aviso del presupuesto: 550 kB, error: 1 MB). |
| 7 | Mapeo de variables al tema claro y oscuro | **Funciona.** Verificado en el navegador con los dos esquemas y en pantalla de 375 px, sin desplazamiento horizontal de la página. |
| — | CSP | La compilación de producción servida con la CSP de nginx (`script-src 'self'`, `connect-src 'self'`) pinta las tres vistas **sin violaciones**. El código de FullCalendar no usa `eval` ni `new Function`, y no crea etiquetas `<style>`: usa atributos `style` (permitidos por `style-src 'unsafe-inline'`). |

**Hallazgos de la integración**
- Con el calendario del año, el navegador avisa «ResizeObserver loop completed with undelivered notifications». No es un fallo, pero el
  navegador lo eleva a error de ventana y el manejador global mostraba «Se produjo un error inesperado». `GlobalErrorHandler` lo ignora
  ahora (con prueba), solo ese aviso.
- En un entorno sin presentación (pestañas ocultas) `ResizeObserver` no notifica y FullCalendar mide tarde: el año muestra cuatro meses por
  fila hasta que hay un fotograma.
- Los enlaces de día (`navLinks`) solo se ofrecen en las vistas Año y Mes, donde llevan a su semana o a su mes. En el encabezado de la Semana hacían zoom a un
  día que la página volvía a deshacer, reescribiendo la dirección sin cambiar lo que se ve; sin enlaces, FullCalendar añade además la fecha larga al nombre
  accesible de cada columna.
- Las clases del tema son opacas (`fc-classic-1Po`…): solo se personaliza por variables y por las opciones `*Class`; un cambio de versión
  puede alterar esas clases, y por eso las pruebas se apoyan en roles ARIA y en `data-date`, no en clases.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| **FullCalendar 7 con el tema `classic` mapeado a Material** (elegida) | Cubre Año, Mes y Semana con colocación, simultáneas, medianoche y «+n más»; accesibilidad de teclado y ARIA incluida; solo lectura sencilla; el solicitante la eligió. | Dependencia nueva con 8 paquetes y su propia apariencia; v7 reciente (7.1.3 se publicó el 2026-10-16); hojas globales; clases opacas. |
| Componentes propios con CSS grid y `--mat-sys-*` (plan B de §10.4) | Sin dependencias y control total. | La vista Semana es laboriosa; más código y más pruebas propias. |
| angular-calendar u otra de menor alcance | Menos peso. | No cubre un año de doce meses; ritmo de mantenimiento por comprobar. Descartada. |
| FullCalendar 6 (paquetes separados) | Más rodada. | Otra línea mayor que no es la vigente; el conector 7 ya declara Angular 22. |

## Consecuencias
- **Positivas:** las tres vistas con poco código propio, accesibles y sin colores propios; la lista del año no paga el tamaño; las
  pruebas del adaptador de eventos y del componente corren en CI sin navegador.
- **Negativas / riesgos:**
  - Cadena de suministro: 8 paquetes nuevos; 7 de ellos (todos menos `preact`, incluidos `@full-ui/headless-calendar` y `temporal-*`) tienen **un único
    mantenedor** en npm, publican **sin atestaciones de procedencia** y son recientes (`fullcalendar` 7.1.3 se publicó el 2026-10-16 y `temporal-polyfill`
    1.0.6 el 2026-10-13). Lo que sí se comprobó: los 8 *hashes* del `package-lock.json` coinciden con el registro, `npm audit signatures` valida las firmas,
    `npm audit` da 0, ningún paquete tiene *scripts* de instalación y hay una sola versión de cada uno. **Riesgo residual a aceptar por una persona
    (`security` o el solicitante)**: una cuenta npm comprometida podría publicar una versión maliciosa; con versiones exactas y `package-lock.json` no llegaría
    sin una actualización deliberada.
  - La v7 es joven: una actualización puede cambiar clases del tema y el comportamiento de la medición.
  - +3,1 kB transferidos en los estilos de toda la aplicación y poco margen en el presupuesto inicial (16 kB hasta el aviso).
  - Con una columna de meses (pantalla estrecha) el título del mes no es un enlace.
  - **Tipografía y sombras fuera del tema** (limitación aceptada, revisión de `architecture` del 2026-10-21): el `theme.css` del tema `classic` fija tamaños
    de letra propios (de 11 a 24 px) y sombras (`#0000001a`) que no salen de los roles de Material, y FullCalendar no expone variables para mapearlos.
    Solo los colores siguen el tema (punto 3); el texto de las actividades puede verse algo distinto del cuerpo de la aplicación.
- **Seguimiento:**
  - ~~**Revisión de `security`** de la cadena de suministro y de la CSP (SPEC-0006 §14.3)~~: hecha el 2026-10-21 (SPEC-0006 §18), sin hallazgos críticos ni
    altos. La revisión independiente asistida del 2026-10-18 tampoco los encontró.
  - La biblioteca puede pintar HTML (`eventContent: { html }`); la aplicación solo usa plantillas de Angular. `features/agenda/agenda-safety.spec.ts` prohíbe
    `innerHTML`, `bypassSecurityTrust*` y la forma `html:` en la feature.
  - `npm audit` al añadirla (hecho: 0) y en cada actualización; actualizar las versiones exactas con una spec o un `minor:` justificado y
    repetir las pruebas del calendario y la verificación en el navegador.
  - Si el presupuesto inicial se agota, cargar las hojas de FullCalendar de forma diferida (estilos con `inject: false` y carga explícita).
  - Si el título del mes en una columna importa, ofrecer un acceso propio al mes en la vista Año.

## Impacto en la constitución
Ninguno. §6.2 pide el componente oficial «siempre que exista uno adecuado» y Angular Material no tiene un calendario de eventos; §6.3 se
respeta porque solo se mapean variables a `--mat-sys-*`; §12.5 se cumple con la justificación de este ADR (licencia MIT, mantenimiento
activo, necesidad). `security` confirmó el 2026-10-21 la lectura de §3.9: la CSP no cambia, así que no se relaja.
