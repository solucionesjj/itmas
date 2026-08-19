# ADR-0009: Dashboard OS-distribution chart — no charting library

- **Status**: Accepted (sub-fase 1.5); accessibility gap closed in sub-fase 1.7 — **palette extended by [ADR-0017](0017-design-system-material3-sac-brand.md)**
- **Related**: agent.md §5.1 ("minimiza dependencias"), §5.2 (UX: "gráfico de distribución por sistema operativo (pie/bar)"); CA-05

> **Extension note (ADR-0017)**: the eight categorical colour *values* referenced below have been replaced by the SAC data palette in `design.md` §2.7, which also adds a dark-mode variant per slot (this ADR's chart component instead carried a private hard-coded light/dark pair per slot). Everything structural on this page is unchanged and still binding: the no-charting-library decision, the eight-slot count and their fixed assignment order, "never cycled or generated", the neutral *Otros* bucket for a 9th category, the mark spec, and the screen-reader requirements. `design.md` §2.7 also adds one rule this ADR did not state: the slots are for **marks only, never for text** — several fall below 4.5:1 at 12–14px, so text uses `--delta-up`/`--delta-down` or the severity pairs instead. See ADR-0017 for the full rationale.

## Context

The dashboard needs a chart showing OS distribution. Adding a full charting library (chart.js, ngx-charts, d3) for one chart is a real dependency-weight and bundle-size cost against a UX requirement that also demands "carga rápida" (fast load).

## Decision

- Built as a **plain Angular component with inline SVG/CSS** — no charting library dependency. A horizontal bar chart (categorical "part-to-whole" job), one bar per OS, direct-labeled with name and count.
- Colors assigned from a **fixed-order, pre-validated categorical palette** (8 slots, CVD-safe adjacent-pair separation confirmed in both light and dark mode) — assigned in slot order, never cycled or generated; categories beyond the 8th slot fold into a neutral-gray "Otros" bucket rather than manufacture a 9th hue.
- Mark spec follows a fixed set of rules (≤24px bar thickness, 4px rounded data-end, 2px surface gaps between bars, hairline gridlines) rather than ad hoc styling.
- **Accessibility**: the chart alone (even with direct labels) doesn't serve a screen-reader user. Sub-phase 1.7 closed this gap by adding a visually-hidden data table mirroring the chart's rows, plus `role="img"` and a computed `aria-label` summarizing the distribution on the chart's container.

## Consequences

- No new frontend dependency for this chart, keeping the bundle lean — but the chart is single-purpose; a second, differently-shaped chart need (e.g. a time series) would likely need its own hand-built component rather than reusing this one, since nothing here is a general-purpose charting abstraction.
