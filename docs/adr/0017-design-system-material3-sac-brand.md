# ADR-0017: Design system — Material 3 on the SAC brand palette, Poppins + Roboto Mono

- **Status**: Accepted — **extends [ADR-0009](0009-dashboard-chart-implementation.md)**
- **Related**: [`design.md`](../../design.md) (normative visual specification), agent.md §5.2 (Frontend: "Angular Material … WCAG AA"), agent.md §11 (Quality Gates), [BL-029](../backlog.md#bl-029)

## Context

The frontend shipped on the Angular CLI's stock theme: `mat.$azure-palette` as primary, `mat.$blue-palette` as tertiary, `typography: Roboto`, plus a `body { font-family: Roboto … }` rule and the legacy Material Icons webfont. Three problems followed from that:

- **No brand identity.** Nothing in the product tied it to SAC / Effective Computer Solutions. The blues were Material's defaults, unrelated to the brand's `#004AAD` / `#1893F8` / `#F2982A`.
- **No token layer.** Material 3 defines colour, type, shape, elevation and state, but not spacing, motion, severity, or categorical chart colours. Components therefore hard-coded their own: 34 raw hex/`rgba()` literals across the feature folders, 42 ad-hoc pixel margins/paddings, and two components carrying private light/dark palettes gated on `prefers-color-scheme`.
- **Dark mode was unreachable.** `styles.scss` pinned `color-scheme: light` on `body`, so the OS preference was ignored and there was no way for a user to choose.

`agent.md` §5.2 requires "diseño moderno, responsivo y accesible (**WCAG AA**)" but names no concrete design language, leaving each screen to invent one. `design.md` now fills that gap as a normative specification.

## Decision

- **Generated tonal palettes replace `mat.$azure-palette` / `mat.$blue-palette`.** The six MD3 ramps in `frontend/src/styles/_theme-colors.scss` are generated from the three SAC brand seeds — `#004AAD` (primary), `#1893F8` (secondary), `#F2982A` (tertiary) — with `neutral` / `neutral-variant` derived from the primary hue at clamped chroma and `error` kept as the Material 3 default red. Each brand hex is **pinned** to its natural tone slot (P40 / S60 / T70) rather than re-derived, so the product's blue is exactly `#004AAD` and not an approximation. Tones are machine-generated and **must never be hand-edited**; if a seed changes, the whole ramp is regenerated from the rules in design.md §2.1.
- **Poppins replaces Roboto**, with **Roboto Mono** for technical values (device ids, IPs, CIDRs, ports, hashes, ISO timestamps, byte counts) via the `.mono` class. The type config departs from Roboto's 400/400 on purpose — `regular-weight: 300`, `medium-weight: 500`, `bold-weight: 600` — because SAC sets body copy in Light 300 and headings in SemiBold 600. The `body { font-family: Roboto … }` rule is deleted; typography now comes from the theme.
- **Material Symbols Rounded replaces the legacy Material Icons webfont**, at `wght 300` to sit with Poppins Light, `FILL 0` at rest and `FILL 1` for the active nav item.
- **`theme-type: color-scheme`** emits both schemes as one set of `--mat-sys-*` variables resolved through CSS `light-dark()`, so switching theme is a `color-scheme` change — no duplicated CSS, no class toggling on `<body>`.
- **A token layer (`frontend/src/styles/_tokens.scss`) carries what MD3 does not define**: the 4px spacing scale (`--sp-*`), shell layout metrics, motion durations/easings, the five-level severity scale, the eight categorical chart slots, and the KPI delta colours. These and `_theme-colors.scss` are the **only** two files in the frontend allowed to contain colour literals.
- **`design.md` is normative for frontend visuals** — colour, typography, spacing, shape, elevation, states, and the component catalogue — and cedes to `agent.md` on everything else. Recorded in both `CLAUDE.md` and `agent.md` §5.2.

### Sass shape adaptation (implementation note)

`_theme-colors.scss` publishes its six ramps side by side as `$itmas-palettes: (primary: …, secondary: …, tertiary: …, neutral: …, neutral-variant: …, error: …)`. `mat.theme()` expects something different: an M3 palette whose **own** tones `0…100` sit at the top level, with `secondary` / `neutral` / `neutral-variant` / `error` as nested maps beside them. The two shapes are assembled in `styles.scss` with `map.merge`, producing a `$_primary` and a `$_tertiary` palette that share the same nested ramps.

This matters because passing the raw map straight through — as design.md §13's snippet originally showed — **fails silently rather than erroring**: Sass compiles, the build succeeds, and 18 of 49 colour roles emit `light-dark(, )` with empty values (every `primary`, every `tertiary`, and `surface-tint`). The neutral, secondary and error roles resolve correctly, which makes the breakage look like a partial theming bug rather than a config error. design.md §13's snippet was corrected to the assembling form.

### Where Angular Material 20 diverges from design.md §2.3

Five of the 35 roles in design.md §2.3's table resolve to a different tone than the table predicts, because Angular Material 20 implements the current MD3 role→tone mapping:

| Role | design.md §2.3 | Angular Material 20 emits | Contrast of the emitted pair |
| --- | --- | --- | --- |
| `on-primary-container` (light) | `#00184A` (P10) | `#00419D` (P30) | 7.26:1 |
| `on-secondary-container` (light) | `#001C3B` (S10) | `#004882` (S30) | 7.22:1 |
| `on-tertiary-container` (light) | `#2E1600` (T10) | `#693C00` (T30) | 7.22:1 |
| `on-error-container` (light) | `#430000` (E10) | `#940001` (E30) | 7.18:1 |
| `on-surface-variant` (dark) | `#BDC7D8` (NV80) | `#D8E3F4` (NV90) | 14.27:1 |

All five emitted pairs pass WCAG AA comfortably, and the dark `on-surface-variant` is *higher* contrast than the table's value. Since these come from Angular Material's role mapping rather than from our palette, overriding them would mean writing colour literals into a stylesheet — which design.md §0 forbids and which would fight the framework at every upgrade. **The emitted values are accepted as correct and design.md §2.3's table is the stale side.**

## Consequences

- **The app now follows the operating system's colour-scheme preference by default.** `theme-type: color-scheme` plus `html { color-scheme: light dark; }` replaces the fixed `color-scheme: light`, so a user on a dark-mode OS gets a dark portal on first load without configuring anything. An explicit choice is written by `ThemeService` to `<html data-theme="light|dark">` and persisted in `localStorage` under `itmas.theme` (`system` | `light` | `dark`); `system` removes the attribute and hands control back to the OS. Two things follow:
  - **Every screen must now be verified in both schemes**, doubling the visual review surface of any frontend change. This is a standing Quality Gate obligation, not a one-off migration cost.
  - **Any component that hard-codes a light palette, or gates its dark palette on `@media (prefers-color-scheme: dark)`, is now actively wrong** — not merely un-themed. When a user overrides the theme, a `prefers-color-scheme` query still answers to the *OS*, so the component's internals desynchronize from the page around them. `dashboard.component.scss` and `os-distribution-chart.component.scss` both do this today and both visibly break (a dark chart canvas and near-invisible KPI labels on a forced-light page). Components must key off `--mat-sys-*` / the token layer, which already handle `data-theme` as well as the media query.
- **design.md §2.7 replaces ADR-0009's categorical chart palette, preserving everything structural about it.** The eight-slot count, the fixed slot **order**, the "assigned in order, never cycled or generated" rule, the "9th category folds into a neutral *Otros* bucket" rule, the mark spec (bars ≤24px, 4px rounded data-end, 2px surface-coloured gaps, hairline gridlines in `--mat-sys-outline-variant`, direct labels over a legend), and the accessibility requirement (`role="img"`, computed `aria-label`, `.sr-only` mirror table) are all carried forward unchanged. What changes is the eight hex values themselves — now drawn from the SAC data palette — plus a **dark variant per slot**, which ADR-0009 never had (its chart component instead kept a private light/dark pair per slot, hard-coded). A note pointing here was added to ADR-0009; ADR-0009 is otherwise unmodified and still binding.
- **The chart slots are for marks, never for text.** The palette is tuned for marks ≥24px: on `surface-container-low`, `--chart-5` measures 1.93:1 and `--chart-7` 3.58:1, both failing AA for 12–14px text. Any text that needs a semantic green or red — most immediately the KPI delta in design.md §10.2 — uses `--delta-up` / `--delta-down` (the same hues taken to tone 35/40 in light mode: `#3F6B31` at 5.6:1, `#B4271F` at 6.2:1; the tone-80 chart values are kept in dark mode, where they already pass) or the severity pairs from §2.6. A reviewer seeing `color: var(--chart-N)` on a text node should treat it as a defect.
- **Two files monopolise colour literals**, so a `grep -rEn '#[0-9a-fA-F]{3,8}|rgba?\(' frontend/src/app` returning anything is by definition a violation. That grep currently returns 34 hits; driving it to zero is the second step of the adoption (BL-029) and is what makes the token layer trustworthy rather than advisory.
- **Adoption is staged, and the intermediate states are knowingly inconsistent.** design.md §14 breaks the retrofit into seven independently-shippable steps; this ADR covers step 1 (foundations) only. Until steps 2–7 land, screens legitimately mix themed chrome with un-themed component internals. That is a planned condition with a tracked backlog item, not drift.
- **No new dependency.** Poppins, Roboto Mono and Material Symbols Rounded load from Google Fonts via `index.html`, matching the delivery method Roboto and Material Icons already used, and the token layer is plain CSS custom properties. Charts remain hand-built per ADR-0009.
