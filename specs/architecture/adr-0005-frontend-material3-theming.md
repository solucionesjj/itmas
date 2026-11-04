---
id: ADR-0005
title: Frontend Angular con Material 3 estándar y temas claro/oscuro
status: Accepted
date: 2026-10-07
deciders: [architecture, frontend]
related: [SPEC-0000]
---

# ADR-0005 — Frontend Angular + Material 3 sin sistema de diseño propio

> **Reemplazado en parte por [ADR-0021](adr-0021-itmas-visual-identity.md) (2026-11-03, constitución 4.0.0)** en las
> paletas, la tipografía, la regla «sin sistema de diseño propio» y la carga de fuentes desde Google Fonts. Sigue vigente
> en lo demás: un único tema con `light-dark()`, `ThemeService`, Material Symbols Outlined como conjunto de iconos y la
> estructura de carpetas. SPEC-0010 (2026-11-03) implementó el reemplazo: el tema y las fuentes son los de ADR-0021.

## Contexto
Se exige Angular Material siguiendo Material Design 3 estándar, sin personalizar colores, tipografía, elevaciones
ni componentes, con temas claro y oscuro seleccionables y un mecanismo preparado para evolucionar.

## Decisión
- Angular 22 *standalone*, *zoneless*, signals, rutas con carga diferida (incluido el `Shell`).
- Un único `mat.theme(...)` con las paletas que propone `ng add @angular/material` (`azure`/`blue`), Roboto y
  densidad 0. El tema usa `light-dark()`; `html { color-scheme: light dark }` sigue al sistema.
- `ThemeService` fija `light`/`dark` mediante `<html data-theme>` o vuelve a `system`; preferencia en `localStorage`
  (no sensible), sustituible por una preferencia de perfil en una spec futura.
- Iconos Material Symbols Outlined como *font set* por defecto de `mat-icon`.
- Estructura `core/ auth/ layout/ shared/ features/`.

## Alternativas consideradas
| Alternativa | A favor | En contra |
|---|---|---|
| Dos temas Sass compilados por clase (`.dark-theme`) | Patrón M2 clásico | Duplica CSS; innecesario con `light-dark()` |
| Paleta corporativa personalizada | Identidad de marca | Prohibido por el requerimiento en esta etapa |
| Fuentes/iconos autoalojados | Sin terceros; CSP más estricta | El *variable font* completo de Material Symbols pesa varios MB; se puede reevaluar |

## Consecuencias
- Positivas: cero CSS de tema propio; cambio de tema instantáneo y sin recarga.
- Negativas: dependencia de Google Fonts (permitida explícitamente en la CSP).

## Impacto en la constitución
Recogido en §6.
