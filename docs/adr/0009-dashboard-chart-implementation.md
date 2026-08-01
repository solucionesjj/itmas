# ADR-0009: Dashboard OS-distribution chart — no charting library

- **Status**: Accepted (sub-fase 1.5); accessibility gap closed in sub-fase 1.7
- **Related**: agent.md §5.1 ("minimiza dependencias"), §5.2 (UX: "gráfico de distribución por sistema operativo (pie/bar)"); CA-05

## Context

The dashboard needs a chart showing OS distribution. Adding a full charting library (chart.js, ngx-charts, d3) for one chart is a real dependency-weight and bundle-size cost against a UX requirement that also demands "carga rápida" (fast load).

## Decision

- Built as a **plain Angular component with inline SVG/CSS** — no charting library dependency. A horizontal bar chart (categorical "part-to-whole" job), one bar per OS, direct-labeled with name and count.
- Colors assigned from a **fixed-order, pre-validated categorical palette** (8 slots, CVD-safe adjacent-pair separation confirmed in both light and dark mode) — assigned in slot order, never cycled or generated; categories beyond the 8th slot fold into a neutral-gray "Otros" bucket rather than manufacture a 9th hue.
- Mark spec follows a fixed set of rules (≤24px bar thickness, 4px rounded data-end, 2px surface gaps between bars, hairline gridlines) rather than ad hoc styling.
- **Accessibility**: the chart alone (even with direct labels) doesn't serve a screen-reader user. Sub-phase 1.7 closed this gap by adding a visually-hidden data table mirroring the chart's rows, plus `role="img"` and a computed `aria-label` summarizing the distribution on the chart's container.

## Consequences

- No new frontend dependency for this chart, keeping the bundle lean — but the chart is single-purpose; a second, differently-shaped chart need (e.g. a time series) would likely need its own hand-built component rather than reusing this one, since nothing here is a general-purpose charting abstraction.
