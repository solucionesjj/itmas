# IT-MAS — Design System

**Infrastructure Management and Audit System.** Normative design specification for
everything under `frontend/src/`.

- **Design language**: [Material 3](https://m3.material.io/), as implemented by
  Angular Material 20's `mat.theme()` API.
- **Brand**: SAC / Effective Computer Solutions — palette and typography from
  [sac-saas.com](https://www.sac-saas.com/).
- **Audience of this document**: coding agents and the developers reviewing their output.
- **Status**: normative. Where this document and a component disagree, the component is wrong.

---

## 0. Agent contract

Read this before writing or modifying any file under `frontend/src/`.

1. **Never write a raw color.** No hex, `rgb()`, `hsl()` or named color in a component
   stylesheet or template. Use `var(--mat-sys-*)` (§2.4) or an IT-MAS token (§2.6, §2.7).
   The only files allowed to contain hex literals are
   `frontend/src/styles/_theme-colors.scss` and `frontend/src/styles/_tokens.scss`.
2. **Never fill with orange.** `#F2982A` is a detail color — borders, 3px indicator bars,
   dots, icon strokes, focus accents on data marks. It never becomes a button background,
   a card surface, a filled chip or a chart area larger than a 24px bar. (§2.5)
3. **Both themes, always.** Every screen must be correct in light and dark. A change is
   not done until it has been looked at in both. (§2.3)
4. **Spacing comes from the 4px scale** `--sp-1` … `--sp-12`. No arbitrary pixel values
   for margin, padding or gap. (§6)
5. **Type comes from the MD3 type scale** via `var(--mat-sys-body-medium)` and friends.
   No ad-hoc `font-size`. The sole exception is tabular data, which uses the `.mono`
   class defined in `styles.scss`. (§3)
6. **Use `mat-*` components before writing your own.** §9 has the canonical markup for the
   twelve components in the app. If a need is not covered there, compose it from MD3
   primitives — do not invent a new visual pattern.
7. **Every data view needs four states**: loaded, empty, loading, error. (§10.4)
8. **Every interactive element** needs a visible `:focus-visible` ring, an accessible name,
   and a ≥44×44px hit area. (§11)
9. **All user-visible strings go through i18n keys.** Default locale `es-CO`. (§12)
10. **Layout is the shell.** Screens render into the shell's content area and do not
    define their own page chrome. (§7)

---

## 1. Product context

IT-MAS inventories, monitors and audits an organization's technology infrastructure from
several points of view. That shapes three design consequences that run through the whole
system:

| Product trait | Design consequence |
| --- | --- |
| Data is dense, technical and long-lived (hostnames, IPs, rule sets, hashes) | Tabular layouts dominate. Monospace + tabular numerals for anything an operator compares character by character. Density 0 — comfortable, because audit work is long-session and error-costly. |
| Findings carry risk | A five-level severity scale (§2.6) with a consistent hue, a shape and a text label — never color alone. |
| Multi-viewpoint (devices, network, cloud, users) | The shell's navigation is the primary orientation device. Each feature area keeps the same page skeleton (§10.1) so operators can move between viewpoints without relearning. |

**Feature areas** (`frontend/src/app/features/`): `dashboard`, `devices`,
`security-group-rules`, `alerts`, `reports`, `admin`, `login`, `change-password`.
Shell lives in `frontend/src/app/core/layout/`.

**Platforms**: desktop web is the primary target; the shell and all data views must remain
usable down to 360px (§6.4).

---

## 2. Color

### 2.1 Seeds

Three brand colors seed the Material 3 tonal palettes. These are the only color decisions
in the system that are not derived.

| Role | Hex | Source | Lands on |
| --- | --- | --- | --- |
| Primary seed | `#004AAD` | SAC brand blue (dominant) | `primary` tone 40 |
| Secondary seed | `#1893F8` | SAC brand accent | `secondary` tone 60 |
| Tertiary seed | `#F2982A` | SAC brand orange (detail) | `tertiary` tone 70 |
| Neutral | `#004AAD` hue, chroma clamped | derived | `neutral` ramp |
| Neutral variant | `#004AAD` hue, chroma 2× neutral | derived | `neutral-variant` ramp |
| Error | `#B3261E` | Material 3 default red | `error` tone 40 (`#B4271F`) |

Each brand hex is **pinned** to its natural tone slot rather than being re-derived, so the
brand blue in the product is exactly `#004AAD` and not an approximation of it. Everything
else in the ramp is generated: hue and chroma held constant, lightness swept to hit each
Material tone (tone = CIE L\*), chroma clamped to the sRGB gamut per tone.

`#004AAD` sits at L\*≈34 rather than exactly 40, which makes it *darker* than a canonical
tone 40 — white on it measures 8.13:1 instead of ~6.5:1. That is a safe direction: more
contrast, not less.

**Regenerating**: tones are machine-generated. Do not hand-edit
`_theme-colors.scss`. If a seed changes, regenerate the whole ramp from these rules.

### 2.2 Tonal palettes

```
tone   primary    secondary  tertiary   neutral    neutral-v  error
────────────────────────────────────────────────────────────────────
0      #000000    #000000    #000000    #000000    #000000    #000000
4      —          —          —          #0B0E13    —          —
6      —          —          —          #101419    —          —
10     #00184A    #001C3B    #2E1600    #181C21    #141C28    #430000
12     —          —          —          #1C2025    —          —
17     —          —          —          #262A30    —          —
20     #002C71    #00315D    #4A2800    #2D3137    #29313E    #6A0000
22     —          —          —          #31353B    —          —
24     —          —          —          #353940    —          —
25     #003482    #003A6B    #563000    #383C42    #313A47    #7A0000
30     #00419D    #004882    #693C00    #43474D    #3E4755    #940001
35     #044DB0    #005495    #794600    #4E5359    #4A5361    #A5130F
40     #004AAD ★  #0060A9    #8A5100    #5A5F65    #565F6D    #B4271F
50     #3374DB    #0079D3    #AD6600    #73777E    #6E7887    #D2463A
60     #4D8FF8    #1893F8 ★  #D07D00    #8C9198    #8791A1    #F16353
70     #78ACFF    #61B0FF    #F2982A ★  #A7ABB3    #A2ACBC    #FF8A7A
80     #A5C8FF    #9ACBFF    #FFB870    #C2C7CE    #BDC7D8    #FFB4A8
87     —          —          —          #D5DAE2    —          —
90     #D2E4FF    #CDE5FF    #FFDCBC    #DEE3EB    #D8E3F4    #FFDAD4
92     —          —          —          #E3E8F0    —          —
94     —          —          —          #E9EEF6    —          —
95     #E9F1FF    #E6F2FF    #FFEEDE    #ECF1F9    #E9F1FF    #FFEDEA
96     —          —          —          #EFF4FC    —          —
98     #F6FAFF    #F5FAFF    #FFF8F2    #F6FAFF    #F6FAFF    #FFF8F6
99     #FAFCFF    #FAFCFF    #FFFCF8    #FAFCFF    #FAFCFF    #FFFBFB
100    #FFFFFF    #FFFFFF    #FFFFFF    #FFFFFF    #FFFFFF    #FFFFFF
```

★ = pinned brand hex.

### 2.3 Color roles

`mat.theme()` emits these as `--mat-sys-*` CSS variables for both schemes. **Use the
variable, never the hex.** The hex columns exist so you can reason about contrast and
review a screenshot, not so you can type them.

| `--mat-sys-` role | Light | Tone | Dark | Tone |
| --- | --- | --- | --- | --- |
| `primary` | `#004AAD` | P40 | `#A5C8FF` | P80 |
| `on-primary` | `#FFFFFF` | P100 | `#002C71` | P20 |
| `primary-container` | `#D2E4FF` | P90 | `#00419D` | P30 |
| `on-primary-container` | `#00184A` | P10 | `#D2E4FF` | P90 |
| `inverse-primary` | `#A5C8FF` | P80 | `#004AAD` | P40 |
| `secondary` | `#0060A9` | S40 | `#9ACBFF` | S80 |
| `on-secondary` | `#FFFFFF` | S100 | `#00315D` | S20 |
| `secondary-container` | `#CDE5FF` | S90 | `#004882` | S30 |
| `on-secondary-container` | `#001C3B` | S10 | `#CDE5FF` | S90 |
| `tertiary` | `#8A5100` | T40 | `#FFB870` | T80 |
| `on-tertiary` | `#FFFFFF` | T100 | `#4A2800` | T20 |
| `tertiary-container` | `#FFDCBC` | T90 | `#693C00` | T30 |
| `on-tertiary-container` | `#2E1600` | T10 | `#FFDCBC` | T90 |
| `error` | `#B4271F` | E40 | `#FFB4A8` | E80 |
| `on-error` | `#FFFFFF` | E100 | `#6A0000` | E20 |
| `error-container` | `#FFDAD4` | E90 | `#940001` | E30 |
| `on-error-container` | `#430000` | E10 | `#FFDAD4` | E90 |
| `surface` | `#F6FAFF` | N98 | `#101419` | N6 |
| `on-surface` | `#181C21` | N10 | `#DEE3EB` | N90 |
| `surface-dim` | `#D5DAE2` | N87 | `#101419` | N6 |
| `surface-bright` | `#F6FAFF` | N98 | `#353940` | N24 |
| `surface-container-lowest` | `#FFFFFF` | N100 | `#0B0E13` | N4 |
| `surface-container-low` | `#EFF4FC` | N96 | `#181C21` | N10 |
| `surface-container` | `#E9EEF6` | N94 | `#1C2025` | N12 |
| `surface-container-high` | `#E3E8F0` | N92 | `#262A30` | N17 |
| `surface-container-highest` | `#DEE3EB` | N90 | `#31353B` | N22 |
| `surface-variant` | `#D8E3F4` | NV90 | `#3E4755` | NV30 |
| `on-surface-variant` | `#3E4755` | NV30 | `#BDC7D8` | NV80 |
| `outline` | `#6E7887` | NV50 | `#8791A1` | NV60 |
| `outline-variant` | `#BDC7D8` | NV80 | `#3E4755` | NV30 |
| `inverse-surface` | `#2D3137` | N20 | `#DEE3EB` | N90 |
| `inverse-on-surface` | `#ECF1F9` | N95 | `#2D3137` | N20 |
| `surface-tint` | = `primary` | | = `primary` | |
| `shadow`, `scrim` | `#000000` | N0 | `#000000` | N0 |

> **Implementation note.** Five of the roles above resolve to a different tone than this table
> predicts, because Angular Material 20 implements the current MD3 role mapping: the four light
> `on-*-container` roles land on tone 30 rather than tone 10 (`#00419D`, `#004882`, `#693C00`,
> `#940001`), and dark `on-surface-variant` lands on NV90 `#D8E3F4` rather than NV80. All five
> emitted pairs pass WCAG AA (7.18:1–14.27:1), so the emitted values are correct and this table is
> the stale side — never override them with literals. See ADR-0017.

**Theme activation.** `html { color-scheme: light dark; }` follows the operating system by
default. An explicit user choice from the toolbar toggle writes `data-theme="light"` or
`data-theme="dark"` on `<html>` and persists to `localStorage` under `itmas.theme`
(values: `system` | `light` | `dark`).

```ts
// core/services/theme.service.ts
type ThemeMode = 'system' | 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly key = 'itmas.theme';
  readonly mode = signal<ThemeMode>((localStorage.getItem(this.key) as ThemeMode) ?? 'system');

  constructor() {
    effect(() => {
      const mode = this.mode();
      const root = document.documentElement;
      if (mode === 'system') root.removeAttribute('data-theme');
      else root.setAttribute('data-theme', mode);
      localStorage.setItem(this.key, mode);
    });
  }

  set(mode: ThemeMode) { this.mode.set(mode); }
}
```

### 2.4 Applying color

| You want | Use |
| --- | --- |
| Page background | `--mat-sys-surface` |
| Body text | `--mat-sys-on-surface` |
| Secondary / label text | `--mat-sys-on-surface-variant` |
| A card or raised panel | `--mat-sys-surface-container-low`, text `--mat-sys-on-surface` |
| A nested panel inside a card | `--mat-sys-surface-container` |
| A table header row | `--mat-sys-surface-container-high` |
| A divider or table rule | `--mat-sys-outline-variant` |
| An input border | `--mat-sys-outline` |
| Primary action | `--mat-sys-primary` on `--mat-sys-on-primary` |
| A selected nav item | `--mat-sys-secondary-container` / `--mat-sys-on-secondary-container` |
| A link | `--mat-sys-primary` |
| A destructive action | `--mat-sys-error` |
| A focus ring | `--mat-sys-secondary`, 3px, 2px offset |
| Emphasis on a data mark | `--itmas-accent-detail` as a border or 3px bar, never a fill |

Never use `primary` as a large background fill. The dominant surface of every IT-MAS
screen is neutral; blue appears at the size of a button, a nav selection, a chart bar or a
1px rule. This is what keeps a dense audit table legible.

### 2.5 The orange rule

`#F2982A` (`--itmas-accent-detail`, `tertiary` tone 70) is inherited from the SAC brand,
where it is a detail color only.

**Allowed**: 1–3px borders and left bars; indicator dots; icon strokes; underlines on an
active tab; a single ≤24px chart bar; the inset edge highlight on the sidenav.

**Not allowed**: button, chip or badge fills; card or toolbar backgrounds; large chart
areas; text on white (2.26:1 — fails AA at every size).

When orange must carry text, use the tonal pair `on-tertiary-container` on
`tertiary-container` (`#2E1600` on `#FFDCBC`, 10.5:1) or the `#8A5100` / `#FFDCBC` pair
used by severity High (4.98:1 — AA for ≥14px bold or ≥18px regular; body-size labels take
the darker pair).

### 2.6 Severity

Five levels, CVSS-shaped. Applies to alerts, audit findings and security-group rule
verdicts alike — one scale for the whole product.

| Level | Key | Light fg / bg | Dark fg / bg | Indicator | Icon (Material Symbols) |
| --- | --- | --- | --- | --- | --- |
| Critical | `critical` | `#B4271F` / `#FFDAD4` | `#FFB4A8` / `#6A0000` | `#EC5151` | `error` |
| High | `high` | `#8A5100` / `#FFDCBC` | `#FFB870` / `#4A2800` | `#F2982A` | `warning` |
| Medium | `medium` | `#7A5900` / `#FFDEA0` | `#F8BD35` / `#412D00` | `#DBA200` | `error_circle_rounded` |
| Low | `low` | `#0060A9` / `#CDE5FF` | `#9ACBFF` / `#00315D` | `#1893F8` | `info` |
| Info | `info` | `#3E4755` / `#D8E3F4` | `#BDC7D8` / `#3E4755` | `#6E7887` | `help` |

Tokens: `--sev-{level}-fg`, `--sev-{level}-bg`, `--sev-{level}-dot`.

The Medium amber is the one hue the SAC palette does not supply. It is derived from
`#F2982A` by an 18° hue rotation in OKLCH at constant chroma, so it reads as a sibling of
the brand orange rather than a foreign color.

**Rules.** Severity is never communicated by color alone: every severity badge carries its
text label, and every severity row carries the icon above. In tables, severity gets a 3px
left bar in `--sev-*-dot` plus the badge — the bar is what makes a 200-row findings table
scannable. Sort order is always Critical → Info, never alphabetical.

```html
<span class="sev sev--critical">
  <mat-icon aria-hidden="true">error</mat-icon>
  {{ 'severity.critical' | translate }}
</span>
```

```scss
.sev {
  display: inline-flex; align-items: center; gap: var(--sp-1);
  padding: 2px var(--sp-2);
  border-radius: 8px;
  font: var(--mat-sys-label-medium);
  white-space: nowrap;

  mat-icon { font-size: 16px; width: 16px; height: 16px; }

  &--critical { color: var(--sev-critical-fg); background: var(--sev-critical-bg); }
  &--high     { color: var(--sev-high-fg);     background: var(--sev-high-bg); }
  &--medium   { color: var(--sev-medium-fg);   background: var(--sev-medium-bg); }
  &--low      { color: var(--sev-low-fg);      background: var(--sev-low-bg); }
  &--info     { color: var(--sev-info-fg);     background: var(--sev-info-bg); }
}
```

### 2.7 Data visualisation

Eight fixed categorical slots, assigned in order and never cycled or generated. Extends
ADR-0009 (which set the 8-slot, no-library rule) by replacing its palette with the SAC
data palette and adding the dark-mode variant of each slot. Slot order and the "9th
category folds into a neutral *Otros* bucket" rule from ADR-0009 are unchanged.

| Slot | Semantic role | Light | Dark |
| --- | --- | --- | --- |
| `--chart-1` | Primary metric | `#3E88F9` | `#A5C8FF` |
| `--chart-2` | Secondary metric | `#78B6FF` | `#9ECAFF` |
| `--chart-3` | Tertiary / network | `#3BB6FF` | `#88CEFF` |
| `--chart-4` | Automation / agent | `#7F73E3` | `#C2C1FF` |
| `--chart-5` | Healthy / compliant | `#80C26E` | `#94D782` |
| `--chart-6` | Warning / attention | `#FA9241` | `#FFB785` |
| `--chart-7` | Risk / non-compliant | `#EC5151` | `#FFB3AD` |
| `--chart-8` | Other / unknown | `#8C9198` | `#A7ABB3` |

Dark variants are the same hue and chroma taken to tone 80, so a chart keeps its identity
across themes. **These slots are for marks, never for text**: at 12–14px on a light surface
they fall below 4.5:1. Text that needs a semantic green or red uses `--delta-up` /
`--delta-down` (§10.2) or the severity pairs (§2.6). Slots 5/6/7 are also the compliance triad — a "compliant / needs review /
non-compliant" chart uses exactly those three, in that order.

Mark spec (from ADR-0009, still binding): bars ≤24px thick, 4px rounded data-end, 2px
surface-colored gaps between bars, hairline gridlines in `--mat-sys-outline-variant`,
direct labels rather than a legend.

---

## 3. Typography

**Poppins** for everything, **Roboto Mono** for tabular data. Both from Google Fonts
(`index.html`), matching the existing delivery method.

```html
<link href="https://fonts.googleapis.com/css2?family=Poppins:ital,wght@0,300;0,400;0,500;0,600;0,700;1,300;1,400&family=Roboto+Mono:wght@400;500&display=swap" rel="stylesheet">
```

### 3.1 Type scale

MD3 scale, Poppins-tuned: SAC uses Light 300 for body copy and 600 for headings, which is
a deliberate departure from Roboto's 400/400. Use via `font: var(--mat-sys-*)`.

| `--mat-sys-` role | Size / line | Weight | Tracking | Used for |
| --- | --- | --- | --- | --- |
| `display-large` | 57 / 64 | 300 | −0.25 | Login hero only |
| `display-medium` | 45 / 52 | 300 | 0 | Not used |
| `display-small` | 36 / 44 | 400 | 0 | Dashboard hero KPI |
| `headline-large` | 32 / 40 | 600 | −0.02em | Page title (dashboard) |
| `headline-medium` | 28 / 36 | 600 | −0.02em | Page title (feature pages) |
| `headline-small` | 24 / 32 | 600 | −0.02em | Section title, dialog title |
| `title-large` | 22 / 28 | 500 | 0 | Card title |
| `title-medium` | 16 / 24 | 500 | 0.15 | Sub-section, list item title |
| `title-small` | 14 / 20 | 500 | 0.1 | Table header cell |
| `body-large` | 16 / 24 | 300 | 0.5 | Long-form copy, dialog body |
| `body-medium` | 14 / 20 | 300 | 0.25 | Default body, table cell |
| `body-small` | 12 / 16 | 300 | 0.4 | Helper text, timestamps |
| `label-large` | 14 / 20 | 500 | 0.1 | Button, tab, nav item |
| `label-medium` | 12 / 16 | 500 | 0.5 | Chip, badge, severity |
| `label-small` | 11 / 16 | 500 | 0.5 | Overline, column unit |

Configured in `styles.scss`:

```scss
typography: (
  plain-family: Poppins,
  brand-family: Poppins,
  bold-weight: 600,
  medium-weight: 500,
  regular-weight: 300,
),
```

> **This config alone does not produce the weights above.** `mat.theme()` exposes only three
> weight knobs, and Angular Material maps every role onto one of them by its own fixed mapping —
> which sends `display-*`, `headline-*` and `title-large` to *regular*, i.e. the 300 this table
> reserves for body copy. Five roles (`display-small`, the three `headline-*`, `title-large`)
> therefore need explicit per-role overrides, listed in §13. Without them a `mat-card-title`
> renders at 300, *lighter* than its own `mat-card-subtitle` at 500. `styles/type-scale.spec.ts`
> asserts this whole table against the emitted variables, so the two cannot silently drift again.
>
> Tracking is emitted in `rem` and matches this table's px values everywhere except `headline-*`,
> which emit `0` rather than −0.02em. Not yet overridden — see BL-029.

### 3.2 Tabular data

Roboto Mono 13/20, `font-variant-numeric: tabular-nums`, tracking 0, via `.mono`.

**Use `.mono` for**: device IDs, UUIDs, IPv4/IPv6, CIDR blocks, ports, MAC addresses,
hostnames, file paths, hashes and fingerprints, security-group IDs (`sg-…`), ARNs,
version strings, ISO timestamps, byte counts, agent config keys.

**Do not use `.mono` for**: display names, descriptions, locations, owners, any prose.

Numeric columns use `.cell-num` — right-aligned, tabular, no wrap. Never center-align a
numeric column.

### 3.3 Rules

- Uppercase only for `label-small` overlines, with 0.1em tracking. Never uppercase a
  heading or a button.
- One `<h1>` per route, matching the page title in the toolbar.
- `text-wrap: pretty` on paragraphs; `text-wrap: balance` on headings ≤3 lines.
- Truncate long identifiers with `text-overflow: ellipsis` **and** a `matTooltip`
  carrying the full value. Never truncate without a way to read the whole string.
- Do not italicize technical values.

---

## 4. Shape

MD3 shape scale. Corners come from `--mat-sys-corner-*`.

| Token | Radius | Applied to |
| --- | --- | --- |
| `corner-none` | 0 | Table cells, full-bleed dividers |
| `corner-extra-small` | 4 | Chips-in-cell, small badges, tooltips |
| `corner-small` | 8 | Inputs, severity badges, menu items |
| `corner-medium` | 12 | Cards, KPI tiles, panels |
| `corner-large` | 16 | Dialogs, bottom sheets, side panels |
| `corner-extra-large` | 28 | Login card, FAB, empty-state illustration frame |
| `corner-full` | 9999 | Buttons, filter chips, avatars, toggles |

One radius per element — no mixed corners except the standard "large top, none bottom"
of a bottom sheet.

---

## 5. Elevation and surfaces

MD3 elevation is **tonal first**: differentiate with `surface-container-*` steps, and add a
shadow only when the element floats above the page.

| Level | Shadow | Surface role | Used for |
| --- | --- | --- | --- |
| 0 | none | `surface` | Page background |
| 1 | `--mat-sys-level1` | `surface-container-low` | Cards, KPI tiles, table container |
| 2 | `--mat-sys-level2` | `surface-container` | Toolbar on scroll, sticky table header |
| 3 | `--mat-sys-level3` | `surface-container-high` | Menus, autocomplete, tooltips |
| 4 | `--mat-sys-level4` | `surface-container-high` | Navigation drawer (temporary mode) |
| 5 | `--mat-sys-level5` | `surface-container-highest` | Dialogs |

Rules:

- A card at rest is elevation 1. It does not gain a shadow on hover; it gains a state layer
  (§8.1) and, if clickable, a 1px `--mat-sys-outline-variant` border that becomes
  `--mat-sys-primary`.
- In dark mode, shadows are nearly invisible: the surface step is what carries the
  hierarchy. Never rely on a shadow alone to separate two dark surfaces.
- No blur or glass surfaces. IT-MAS is a data product; translucency costs legibility.

---

## 6. Spacing, grid, breakpoints

### 6.1 Scale

4px base. `--sp-1: 4` `--sp-2: 8` `--sp-3: 12` `--sp-4: 16` `--sp-5: 20` `--sp-6: 24`
`--sp-8: 32` `--sp-10: 40` `--sp-12: 48`.

| Context | Value |
| --- | --- |
| Icon to its label | `--sp-1` |
| Inside a chip / badge | `2px --sp-2` |
| Table cell padding | `--sp-3 --sp-4` |
| Inside a card | `--sp-4` (compact) / `--sp-6` (default) |
| Between cards in a grid | `--sp-4` |
| Between sections on a page | `--sp-8` |
| Page gutter | `--sp-6` (desktop) / `--sp-4` (≤905px) |
| Form field vertical rhythm | `--sp-5` |

Always use `gap` on a flex/grid parent. Do not space siblings with per-child margins.

### 6.2 Grid

12 columns, `--sp-6` gutters, content capped at `--shell-content-max` (1440px) and
centered. Dashboard KPI row: 4 columns ≥1240px, 2 columns ≥905px, 1 column below.

```scss
.page {
  max-width: var(--shell-content-max);
  margin-inline: auto;
  padding: var(--sp-6) var(--shell-gutter) var(--sp-12);
  display: flex;
  flex-direction: column;
  gap: var(--sp-8);
}

.kpi-grid {
  display: grid;
  gap: var(--sp-4);
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
}
```

### 6.3 Breakpoints

MD3 window size classes.

| Class | Range | Shell behavior |
| --- | --- | --- |
| Compact | < 600px | Sidenav `over`, closed by default; tables become card lists (§10.3) |
| Medium | 600–904px | Sidenav rail (72px), icons only |
| Expanded | 905–1239px | Sidenav `side`, open, 264px |
| Large | 1240–1599px | As Expanded; KPI grid 4-up |
| Extra-large | ≥1600px | Content capped at 1440px, centered |

### 6.4 Touch and pointer

Minimum hit area 44×44px everywhere, including icon buttons inside dense table rows —
pad the button, do not enlarge the icon.

---

## 7. Application shell

`core/layout/` owns all page chrome. Feature routes render into `<main>` and must not add
their own toolbar or background.

```
┌─────────────────────────────────────────────────────────────┐
│ toolbar  64px   [menu] IT-MAS · <page title>   [theme][user]│
├───────────────┬─────────────────────────────────────────────┤
│ sidenav 264px │ <main>                                      │
│               │   ┌───────────────────────────────────┐     │
│ ▸ Dashboard   │   │ page header: h1 + actions         │     │
│ ▸ Devices     │   ├───────────────────────────────────┤     │
│ ▸ Security    │   │ content, max 1440px, centered     │     │
│ ▸ Alerts      │   │                                   │     │
│ ▸ Reports     │   └───────────────────────────────────┘     │
│ ▸ Admin       │                                             │
└───────────────┴─────────────────────────────────────────────┘
```

**Toolbar** — `surface-container-low`, no shadow at rest, elevation 2 once `<main>`
scrolls. Left: menu toggle (compact/medium only), product name, then the current page
title. Right: theme toggle, then user menu. Height `--shell-toolbar-h`.

**Sidenav** — `surface-container-low` with a 1px `outline-variant` right border. Items are
`label-large`, 48px tall, `corner-full`, icon + label, `--sp-3` inline padding, `--sp-1`
between items. Selected item: `secondary-container` / `on-secondary-container` with a 3px
`--itmas-accent-detail` left bar. This bar is the one orange element in the shell and it
is what ties IT-MAS back to the SAC family.

**Page header** — `<h1>` in `headline-medium` (`headline-large` on the dashboard), optional
`body-small` subtitle in `on-surface-variant`, actions right-aligned on the same line;
actions wrap below the title under 600px.

Route titles are set through Angular's `Title` service and mirrored in the toolbar from a
single source in `app.routes.ts` (`data: { title: 'nav.devices' }`).

---

## 8. States and motion

### 8.1 State layers

MD3 state layers — an overlay of the element's own content color at a fixed opacity.
Never change an element's background color on hover.

| State | Opacity |
| --- | --- |
| Hover | 8% |
| Focus | 10% |
| Pressed | 10% |
| Dragged | 16% |
| Selected | 12% (`secondary-container` for nav/list selection) |
| Disabled | content 38%, container 12%, no state layer, `cursor: not-allowed` |

Table row hover uses `on-surface` at 8% (`color-mix(in srgb, var(--mat-sys-on-surface) 8%, transparent)`).

### 8.2 Focus

One ring for the whole app, defined once in `styles.scss`: 3px `--mat-sys-secondary`,
2px offset. Never `outline: none` without an equally visible replacement. Focus must be
visible on both themes — this is why the ring is `secondary` (`#0060A9` / `#9ACBFF`) rather
than `primary`, which nearly vanishes on a dark surface.

### 8.3 Motion

| Token | Value | Used for |
| --- | --- | --- |
| `--dur-short` | 150ms | State layers, ripples, tooltips, icon spins |
| `--dur-medium` | 250ms | Menus, expansion panels, tab indicator, snackbar |
| `--dur-long` | 400ms | Drawer, dialog enter/exit, route transition |
| `--ease-standard` | `cubic-bezier(0.2, 0, 0, 1)` | Default |
| `--ease-emphasized` | `cubic-bezier(0.05, 0.7, 0.1, 1)` | Entering surfaces (dialog, drawer) |
| `--ease-brand` | `cubic-bezier(0.25, 0.46, 0.45, 0.94)` | SAC hover lift, 1–2px translate |

Rules: animate `transform` and `opacity` only. No animation on table rows, chart bars, or
anything that changes on every poll — a monitoring product that pulses is unreadable.
`prefers-reduced-motion: reduce` cuts all durations to ~0 (already handled in
`styles.scss`); never gate function behind an animation.

---

## 9. Component catalogue

Canonical markup. Copy from here.

### 9.1 Buttons

Angular Material 20 button API. Hierarchy per view: **one** filled button, any number of
outlined/text.

```html
<!-- Primary action: one per page region -->
<button matButton="filled" (click)="save()">{{ 'action.save' | translate }}</button>

<!-- Secondary -->
<button matButton="outlined" (click)="cancel()">{{ 'action.cancel' | translate }}</button>

<!-- Tertiary / in-table -->
<button matButton (click)="details()">{{ 'action.details' | translate }}</button>

<!-- Elevated: on a colored or image surface only -->
<button matButton="elevated">{{ 'action.export' | translate }}</button>

<!-- Tonal: a second, equally-weighted action next to a filled one -->
<button matButton="tonal">{{ 'action.rescan' | translate }}</button>

<!-- Destructive -->
<button matButton="filled" class="btn-danger" (click)="confirmDelete()">
  {{ 'action.delete' | translate }}
</button>

<!-- Icon button: always needs an accessible name -->
<button matIconButton [attr.aria-label]="'action.refresh' | translate" (click)="refresh()">
  <mat-icon>refresh</mat-icon>
</button>

<!-- Icon + label -->
<button matButton="filled">
  <mat-icon>add</mat-icon>
  {{ 'devices.add' | translate }}
</button>
```

```scss
.btn-danger {
  --mat-button-filled-container-color: var(--mat-sys-error);
  --mat-button-filled-label-text-color: var(--mat-sys-on-error);
}
```

Sizing: default 40px, `--sp-6` inline padding, `corner-full`, `label-large`. Never place two
filled buttons side by side. Never use color alone to mark a destructive action — the label
says what will be deleted.

### 9.2 Table + sort + paginator

The workhorse of IT-MAS. Sticky header, sortable, server-paginated, one row action column.

```html
<div class="table-shell">
  <table mat-table [dataSource]="rows()" matSort (matSortChange)="onSort($event)"
         [attr.aria-label]="'devices.tableLabel' | translate">

    <ng-container matColumnDef="severity">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>
        {{ 'field.severity' | translate }}
      </th>
      <td mat-cell *matCellDef="let r" [class]="'sev-bar sev-bar--' + r.severity">
        <span class="sev" [class]="'sev sev--' + r.severity">
          <mat-icon aria-hidden="true">{{ severityIcon(r.severity) }}</mat-icon>
          {{ 'severity.' + r.severity | translate }}
        </span>
      </td>
    </ng-container>

    <ng-container matColumnDef="hostname">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'field.hostname' | translate }}</th>
      <td mat-cell *matCellDef="let r" class="mono">{{ r.hostname }}</td>
    </ng-container>

    <ng-container matColumnDef="ip">
      <th mat-header-cell *matHeaderCellDef>{{ 'field.ip' | translate }}</th>
      <td mat-cell *matCellDef="let r" class="mono">{{ r.ip }}</td>
    </ng-container>

    <ng-container matColumnDef="lastSeen">
      <th mat-header-cell *matHeaderCellDef mat-sort-header>{{ 'field.lastSeen' | translate }}</th>
      <td mat-cell *matCellDef="let r">
        <span [matTooltip]="r.lastSeen | date:'medium'">{{ r.lastSeen | relativeTime }}</span>
      </td>
    </ng-container>

    <ng-container matColumnDef="actions">
      <th mat-header-cell *matHeaderCellDef><span class="sr-only">{{ 'field.actions' | translate }}</span></th>
      <td mat-cell *matCellDef="let r">
        <button matIconButton [matMenuTriggerFor]="menu"
                [attr.aria-label]="'action.rowMenu' | translate">
          <mat-icon>more_vert</mat-icon>
        </button>
        <mat-menu #menu>
          <button mat-menu-item (click)="open(r)">{{ 'action.details' | translate }}</button>
          <button mat-menu-item (click)="rescan(r)">{{ 'action.rescan' | translate }}</button>
        </mat-menu>
      </td>
    </ng-container>

    <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
    <tr mat-row *matRowDef="let row; columns: columns" (click)="open(row)" tabindex="0"></tr>
  </table>

  <mat-paginator [length]="total()" [pageSize]="pageSize()"
                 [pageSizeOptions]="[25, 50, 100]" showFirstLastButtons />
</div>
```

```scss
.table-shell {
  background: var(--mat-sys-surface-container-low);
  border: 1px solid var(--mat-sys-outline-variant);
  border-radius: var(--mat-sys-corner-medium);
  overflow: hidden;
}

table { width: 100%; }

th.mat-mdc-header-cell {
  background: var(--mat-sys-surface-container-high);
  font: var(--mat-sys-title-small);
  color: var(--mat-sys-on-surface-variant);
  white-space: nowrap;
}

td.mat-mdc-cell {
  font: var(--mat-sys-body-medium);
  border-bottom-color: var(--mat-sys-outline-variant);
  padding: var(--sp-3) var(--sp-4);
}

tr.mat-mdc-row {
  cursor: pointer;
  transition: background var(--dur-short) var(--ease-standard);
}
tr.mat-mdc-row:hover {
  background: color-mix(in srgb, var(--mat-sys-on-surface) 8%, transparent);
}

// 3px severity bar on the first cell
.sev-bar { box-shadow: inset 3px 0 0 0 var(--sev-info-dot); }
.sev-bar--critical { box-shadow: inset 3px 0 0 0 var(--sev-critical-dot); }
.sev-bar--high     { box-shadow: inset 3px 0 0 0 var(--sev-high-dot); }
.sev-bar--medium   { box-shadow: inset 3px 0 0 0 var(--sev-medium-dot); }
.sev-bar--low      { box-shadow: inset 3px 0 0 0 var(--sev-low-dot); }
```

Rules: no zebra striping (the row rule is enough and stripes fight the severity bar).
Header is `title-small`, sticky. Column order: identity → status/severity → attributes →
time → actions. Row height 52px at density 0. Clickable rows are also keyboard-focusable
and must not be the only path to the row's action.

### 9.3 Form field, input, select

`appearance="outline"` everywhere. Never `fill`.

```html
<form [formGroup]="form" class="form-grid">
  <mat-form-field appearance="outline">
    <mat-label>{{ 'field.hostname' | translate }}</mat-label>
    <input matInput formControlName="hostname" class="mono" autocomplete="off">
    <mat-hint>{{ 'devices.hostnameHint' | translate }}</mat-hint>
    @if (form.controls.hostname.hasError('required')) {
      <mat-error>{{ 'error.required' | translate }}</mat-error>
    }
  </mat-form-field>

  <mat-form-field appearance="outline">
    <mat-label>{{ 'field.os' | translate }}</mat-label>
    <mat-select formControlName="os">
      @for (os of osOptions; track os.key) {
        <mat-option [value]="os.key">{{ 'os.' + os.key | translate }}</mat-option>
      }
    </mat-select>
  </mat-form-field>

  <mat-form-field appearance="outline">
    <mat-label>{{ 'field.cidr' | translate }}</mat-label>
    <input matInput formControlName="cidr" class="mono" placeholder="10.0.0.0/24">
    <mat-icon matSuffix aria-hidden="true">lan</mat-icon>
  </mat-form-field>
</form>
```

```scss
.form-grid {
  display: grid;
  gap: var(--sp-5);
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  max-width: 720px;
}
```

Rules: label always visible (`mat-label`), never a placeholder as the label. Placeholders
show *format* only (`10.0.0.0/24`). Errors are specific and say what to do —
"El CIDR debe incluir la máscara, por ejemplo 10.0.0.0/24", not "Valor inválido". Mark
optional fields, not required ones, when most fields are required. Error text is
`body-small` in `--mat-sys-error`; the field also gets `aria-describedby`.

### 9.4 Card

```html
<mat-card appearance="outlined" class="panel">
  <mat-card-header>
    <mat-card-title>{{ 'devices.summary' | translate }}</mat-card-title>
    <mat-card-subtitle>{{ 'devices.summaryHint' | translate }}</mat-card-subtitle>
  </mat-card-header>
  <mat-card-content>…</mat-card-content>
  <mat-card-actions align="end">
    <button matButton>{{ 'action.details' | translate }}</button>
  </mat-card-actions>
</mat-card>
```

```scss
.panel {
  --mat-card-outlined-container-color: var(--mat-sys-surface-container-low);
  --mat-card-outlined-outline-color: var(--mat-sys-outline-variant);
  border-radius: var(--mat-sys-corner-medium);
  padding: var(--sp-6);
}
```

`appearance="outlined"` is the default in IT-MAS. Use `elevated` only when a card floats
over another surface. Card titles are `title-large`; do not nest cards.

### 9.5 Dialog

```ts
const ref = this.dialog.open(ConfirmDialog, {
  width: '480px',
  maxWidth: '92vw',
  autoFocus: 'first-tabbable',
  restoreFocus: true,
  data: { deviceName: device.hostname },
});
```

```html
<h2 mat-dialog-title>{{ 'devices.deleteTitle' | translate }}</h2>
<mat-dialog-content>
  <p>{{ 'devices.deleteBody' | translate: { name: data.deviceName } }}</p>
</mat-dialog-content>
<mat-dialog-actions align="end">
  <button matButton mat-dialog-close>{{ 'action.cancel' | translate }}</button>
  <button matButton="filled" class="btn-danger" [mat-dialog-close]="true">
    {{ 'action.delete' | translate }}
  </button>
</mat-dialog-actions>
```

Widths: 480px confirm, 640px form, 880px detail. `corner-large`, elevation 5, scrim
`--mat-sys-scrim` at 32%. Title is `headline-small`, body `body-large`. The destructive
label names the object. Destructive confirmations never default-focus the destructive
button.

### 9.6 Snackbar

```ts
this.snackbar.open(
  this.t.instant('devices.rescanQueued'),
  this.t.instant('action.view'),
  { duration: 5000, horizontalPosition: 'start', verticalPosition: 'bottom' },
);
```

Success/neutral: default (`inverse-surface`). Errors go to a `.snack-error` panel class
using `error-container` / `on-error-container` and no auto-dismiss. One line, ≤60
characters, at most one action. Never use a snackbar for anything the user must read to
continue — that is a dialog.

### 9.7 Chip and status badge

```html
<!-- Filter set: multi-select, reflects the active query -->
<mat-chip-listbox multiple aria-label="{{ 'alerts.filterBySeverity' | translate }}">
  @for (s of severities; track s) {
    <mat-chip-option [selected]="isActive(s)" (selectionChange)="toggle(s)">
      {{ 'severity.' + s | translate }}
    </mat-chip-option>
  }
</mat-chip-listbox>

<!-- Removable applied filter -->
<mat-chip-row (removed)="clear('os')">
  {{ 'field.os' | translate }}: {{ activeOs() }}
  <button matChipRemove [attr.aria-label]="'action.removeFilter' | translate">
    <mat-icon>cancel</mat-icon>
  </button>
</mat-chip-row>

<!-- Status badge: a state, not an action -->
<span class="badge badge--online">
  <span class="badge__dot" aria-hidden="true"></span>{{ 'status.online' | translate }}
</span>
```

```scss
.badge {
  display: inline-flex; align-items: center; gap: var(--sp-1);
  padding: 2px var(--sp-2);
  border-radius: var(--mat-sys-corner-full);
  font: var(--mat-sys-label-medium);
  border: 1px solid var(--mat-sys-outline-variant);
  color: var(--mat-sys-on-surface-variant);
  background: var(--mat-sys-surface-container);

  &__dot { width: 8px; height: 8px; border-radius: 50%; background: currentColor; }
  &--online   { color: var(--sev-low-fg);      background: var(--sev-low-bg);      border-color: transparent; }
  &--degraded { color: var(--sev-medium-fg);   background: var(--sev-medium-bg);   border-color: transparent; }
  &--offline  { color: var(--sev-critical-fg); background: var(--sev-critical-bg); border-color: transparent; }
  &--unknown  { color: var(--sev-info-fg);     background: var(--sev-info-bg);     border-color: transparent; }
}
```

Chips are interactive (filters), badges are not (state). Never make a badge clickable;
never use a chip to display a read-only status.

### 9.8 Tabs

```html
<mat-tab-group mat-stretch-tabs="false" [selectedIndex]="tab()" (selectedIndexChange)="onTab($event)">
  <mat-tab [label]="'devices.tabInventory' | translate">…</mat-tab>
  <mat-tab [label]="'devices.tabSoftware' | translate">…</mat-tab>
  <mat-tab [label]="'devices.tabAudit' | translate">…</mat-tab>
</mat-tab-group>
```

Tabs switch viewpoints on the *same* object; they never navigate to a different object.
Labels are `label-large`, indicator 3px in `--mat-sys-primary` — the one place the active
indicator may instead be `--itmas-accent-detail`, on the dashboard only. Selected tab is
reflected in the URL as a query param so a view is linkable. Maximum five tabs; beyond
that, use sub-navigation.

### 9.9 Menu

```html
<button matIconButton [matMenuTriggerFor]="rowMenu"
        [attr.aria-label]="'action.rowMenu' | translate">
  <mat-icon>more_vert</mat-icon>
</button>
<mat-menu #rowMenu>
  <button mat-menu-item (click)="open(r)">
    <mat-icon>open_in_new</mat-icon><span>{{ 'action.details' | translate }}</span>
  </button>
  <button mat-menu-item (click)="rescan(r)">
    <mat-icon>sync</mat-icon><span>{{ 'action.rescan' | translate }}</span>
  </button>
  <mat-divider />
  <button mat-menu-item class="menu-item--danger" (click)="remove(r)">
    <mat-icon>delete</mat-icon><span>{{ 'action.delete' | translate }}</span>
  </button>
</mat-menu>
```

```scss
.menu-item--danger {
  --mat-menu-item-label-text-color: var(--mat-sys-error);
  --mat-menu-item-icon-color: var(--mat-sys-error);
}
```

Elevation 3, `corner-small`, min-width 200px. Destructive items last, after a divider.
Never put a primary action *only* in a menu.

### 9.10 Sidenav + toolbar

> **Two corrections to the snippet below, both found while implementing it.**
>
> 1. **Do not mix `nav.toggle()` with `[opened]="sidenavOpen()"`.** `toggle()` mutates
>    MatDrawer's internal state without touching the signal, so the two drift; once drifted,
>    writing the value the signal already holds is a no-op (signals don't emit on an equal
>    write) and the drawer stops responding — tapping a nav item fails to close it. Drive the
>    drawer from the signal alone (`sidenavOpen.update(o => !o)`) and sync back with
>    `(closedStart)` for backdrop dismissals.
> 2. **This markup puts the toolbar inside `mat-sidenav-content`**, i.e. to the *right* of a
>    full-height sidenav — which contradicts §7's diagram, where the toolbar spans the full
>    width above the sidenav. The markup is what the implementation follows, being the
>    copyable artefact; §7's diagram is the schematic that is out of step.

```html
<mat-sidenav-container class="shell">
  <mat-sidenav #nav [mode]="sidenavMode()" [opened]="sidenavOpen()" class="shell__nav">
    <nav aria-label="{{ 'nav.primary' | translate }}">
      @for (item of navItems; track item.route) {
        <a class="nav-item" [routerLink]="item.route" routerLinkActive="nav-item--active">
          <mat-icon aria-hidden="true">{{ item.icon }}</mat-icon>
          <span>{{ item.label | translate }}</span>
        </a>
      }
    </nav>
  </mat-sidenav>

  <mat-sidenav-content>
    <mat-toolbar class="shell__bar">
      @if (isCompact()) {
        <button matIconButton (click)="nav.toggle()" [attr.aria-label]="'nav.toggle' | translate">
          <mat-icon>menu</mat-icon>
        </button>
      }
      <span class="shell__brand">IT-MAS</span>
      <span class="shell__title">{{ pageTitle() | translate }}</span>
      <span class="shell__spacer"></span>
      <button matIconButton (click)="theme.cycle()" [attr.aria-label]="'theme.toggle' | translate">
        <mat-icon>{{ theme.icon() }}</mat-icon>
      </button>
      <button matIconButton [matMenuTriggerFor]="userMenu" [attr.aria-label]="'user.menu' | translate">
        <mat-icon>account_circle</mat-icon>
      </button>
    </mat-toolbar>

    <main tabindex="-1"><router-outlet /></main>
  </mat-sidenav-content>
</mat-sidenav-container>
```

```scss
.shell { height: 100%; }

.shell__nav {
  width: var(--shell-sidenav-w);
  background: var(--mat-sys-surface-container-low);
  border-right: 1px solid var(--mat-sys-outline-variant);
  padding: var(--sp-3);
}

.shell__bar {
  height: var(--shell-toolbar-h);
  background: var(--mat-sys-surface-container-low);
  color: var(--mat-sys-on-surface);
  gap: var(--sp-3);
  border-bottom: 1px solid var(--mat-sys-outline-variant);
}
.shell__brand { font: var(--mat-sys-title-medium); font-weight: 600; letter-spacing: 0.04em; }
.shell__title { font: var(--mat-sys-title-medium); color: var(--mat-sys-on-surface-variant); }
.shell__spacer { flex: 1; }

.nav-item {
  display: flex; align-items: center; gap: var(--sp-3);
  height: 48px; padding-inline: var(--sp-3);
  border-radius: var(--mat-sys-corner-full);
  font: var(--mat-sys-label-large);
  color: var(--mat-sys-on-surface-variant);
  text-decoration: none;
  transition: background var(--dur-short) var(--ease-standard);
}
.nav-item:hover { background: color-mix(in srgb, var(--mat-sys-on-surface) 8%, transparent); }
.nav-item--active {
  background: var(--mat-sys-secondary-container);
  color: var(--mat-sys-on-secondary-container);
  box-shadow: inset 3px 0 0 0 var(--itmas-accent-detail);
}
```

### 9.11 Progress and skeleton

```html
<!-- Determinate: a job with known length (report export, bulk rescan) -->
<mat-progress-bar mode="determinate" [value]="pct()" />

<!-- Indeterminate: a request in flight, on the container it will fill -->
<mat-progress-bar mode="indeterminate" />

<!-- Skeleton: first load of a known layout -->
<div class="skeleton skeleton--row" aria-hidden="true"></div>
```

```scss
.skeleton {
  background: var(--mat-sys-surface-container-high);
  border-radius: var(--mat-sys-corner-extra-small);
  animation: skeleton-pulse 1.6s var(--ease-standard) infinite;
}
.skeleton--row { height: 52px; margin-bottom: 2px; }

@keyframes skeleton-pulse { 50% { opacity: 0.55; } }
```

First load of a table or KPI grid: skeletons matching the real row height and column count.
Refresh of already-visible data: a 2px indeterminate bar at the top of the container, and
the stale data stays on screen. Never blank out loaded content to show a spinner. Any wait
over 10s gets a determinate bar with a count ("142 / 380 dispositivos").

### 9.12 Tooltip

```html
<span class="mono" [matTooltip]="r.fingerprint" matTooltipPosition="above">
  {{ r.fingerprint | slice:0:16 }}…
</span>
```

Tooltips carry *supplementary* information — the full value behind a truncation, the exact
timestamp behind a relative one, the meaning of an icon-only control. They never carry
information required to complete a task, and never appear on a touch-only path. Delay
300ms, `body-small`, `corner-extra-small`.

---

## 10. Data patterns

### 10.1 Page skeleton

Every feature page, in this order: page header (title + primary action) → filter bar →
content (table, grid, or chart set) → paginator. Filters live above the data, are reflected
in the URL, and show applied values as removable `mat-chip-row`s with a "limpiar todo"
text button.

### 10.2 KPI tile

```html
<mat-card appearance="outlined" class="kpi">
  <span class="kpi__label">{{ 'dashboard.devicesOnline' | translate }}</span>
  <span class="kpi__value">{{ online() | number:'1.0-0' }}</span>
  <span class="kpi__delta kpi__delta--up">
    <mat-icon aria-hidden="true">trending_up</mat-icon>
    {{ delta() | percent:'1.0-1' }}
    <span class="sr-only">{{ 'dashboard.vsLastWeek' | translate }}</span>
  </span>
</mat-card>
```

```scss
.kpi {
  display: flex; flex-direction: column; gap: var(--sp-1);
  padding: var(--sp-5);
  border-left: 3px solid var(--itmas-accent-detail); // the one orange detail on the dashboard
}
.kpi__label {
  font: var(--mat-sys-label-small);
  text-transform: uppercase; letter-spacing: 0.1em;
  color: var(--mat-sys-on-surface-variant);
}
.kpi__value {
  font: var(--mat-sys-display-small);
  font-weight: 700; letter-spacing: -0.04em;
  font-variant-numeric: tabular-nums;
  color: var(--mat-sys-on-surface);
}
.kpi__delta { display: inline-flex; align-items: center; gap: var(--sp-1); font: var(--mat-sys-body-small); }
.kpi__delta--up   { color: var(--delta-up); }
.kpi__delta--down { color: var(--delta-down); }
```

The delta uses `--delta-up` / `--delta-down`, **not** a chart slot. The chart palette is
tuned for marks ≥24px: `--chart-5` on `surface-container-low` measures 1.93:1 and
`--chart-7` 3.58:1, both failing AA for 12px text. The delta tokens are the same hues taken
to tone 35/40 in light mode (`#3F6B31` 5.6:1, `#B4271F` 6.2:1) and keep the tone-80 chart
values in dark mode, where they already pass.

A KPI is one number, one label, at most one comparison. Never two numbers in one tile.
Units go in the label, not the value. A delta always states its comparison period, at
minimum to a screen reader.

### 10.3 Tables on small screens

Below 600px a table becomes a card list: each row is a `surface-container-low` card
showing the identity column as `title-medium`, the severity/status badge, and at most three
attribute pairs; the row menu becomes a full-width action row. Do not horizontally scroll a
12-column audit table on a phone.

### 10.4 Empty, loading, error

Required for every data view.

| State | Content |
| --- | --- |
| **Empty (no data yet)** | Centered, max 420px: 48px outlined icon in `on-surface-variant`, `title-medium` heading, `body-medium` explanation, one filled primary action. |
| **Empty (filters exclude everything)** | Different copy — it names the filters and offers "limpiar filtros". Never the same message as no-data-yet. |
| **Loading (first)** | Skeletons matching the real layout (§9.11). |
| **Loading (refresh)** | 2px indeterminate bar, stale data visible. |
| **Error** | `error-container` panel, `corner-medium`, `error` icon, what failed in one sentence, what to do next, and a "reintentar" outlined button. Include the correlation id in `.mono` `body-small` when the API returns one. |

```html
@if (error()) {
  <div class="state state--error" role="alert">
    <mat-icon aria-hidden="true">error</mat-icon>
    <p>{{ 'error.devicesLoad' | translate }}</p>
    <p class="mono state__id" *ngIf="error()?.correlationId">{{ error()!.correlationId }}</p>
    <button matButton="outlined" (click)="reload()">{{ 'action.retry' | translate }}</button>
  </div>
} @else if (loading()) {
  <div class="skeleton skeleton--row" *ngFor="let _ of [1,2,3,4,5,6]" aria-hidden="true"></div>
  <p class="sr-only" role="status">{{ 'state.loading' | translate }}</p>
} @else if (!rows().length) {
  <div class="state state--empty">…</div>
} @else {
  <!-- table -->
}
```

```scss
.state {
  display: flex; flex-direction: column; align-items: center; gap: var(--sp-3);
  padding: var(--sp-12) var(--sp-6);
  text-align: center;
  border-radius: var(--mat-sys-corner-medium);
}
.state--error {
  background: var(--mat-sys-error-container);
  color: var(--mat-sys-on-error-container);
}
.state__id { opacity: 0.8; }
```

### 10.5 Charts

No charting library (ADR-0009). Hand-built inline SVG/CSS components, fixed 8-slot palette
(§2.7), direct labels, no legend where labels fit. Every chart ships with a
`.sr-only` data table mirroring its rows, `role="img"` on the container, and a computed
`aria-label` summarizing the distribution.

---

## 11. Accessibility (WCAG 2.2 AA)

| Requirement | How it is met |
| --- | --- |
| Text contrast ≥4.5:1 | All `on-*` / container pairs in §2.3 pass. `on-surface` on `surface` = 16.3:1 light, 14.3:1 dark. `on-surface-variant` on `surface` = 6.16:1 light. |
| Large text ≥3:1 | The severity High pair (`#8A5100` on `#FFDCBC`, 4.98:1) is AA for all sizes; the Medium pair is used at `label-medium` 500 and passes. |
| Non-text contrast ≥3:1 | `outline` on `surface` = 4.26:1. Icons use `on-surface-variant` or better. |
| Focus visible | One 3px `secondary` ring, 2px offset, defined once (§8.2). |
| Target size ≥24px (AA) | IT-MAS holds itself to 44×44px everywhere. |
| Color not the only cue | Severity = hue + icon + text label. Status = dot + text. Chart marks = direct labels. |
| Keyboard | Every action reachable; clickable rows are focusable; dialogs trap and restore focus; menus close on Escape. |
| Screen readers | One `<h1>` per route; `<nav aria-label>`, `<main>`; icon-only buttons have `aria-label`; tables have `aria-label`; loading regions are `role="status"`, errors `role="alert"`. |
| Motion | `prefers-reduced-motion: reduce` honored globally. |
| Zoom | Layout survives 200% zoom and 320px width without horizontal scroll. |

Contrast measurements (sRGB):
`#FFFFFF` on `#004AAD` 8.13 · `#00184A` on `#D2E4FF` 13.24 · `#181C21` on `#F6FAFF` 16.33 ·
`#3E4755` on `#F6FAFF` 6.16 · `#DEE3EB` on `#101419` 14.34 · `#A5C8FF` on `#101419` 10.83 ·
`#BDC7D8` on `#101419` 10.85 · `#B4271F` on `#FFFFFF` 6.46 · `#0060A9` on `#FFFFFF` 6.47.

`#F2982A` on white is 2.26 — orange is never text. `#1893F8` on white is 3.20 — the accent
is never body text either; it is a fill, a border or a dark-mode foreground.

---

## 12. Language and formats

Bilingual by i18n key, **`es-CO` default**. No hard-coded user-visible string, ever —
including error text, empty-state copy, `aria-label`s and tooltips.

| Item | es-CO | en-US |
| --- | --- | --- |
| Date | `dd/MM/yyyy` | `MM/dd/yyyy` |
| Date + time | `dd/MM/yyyy HH:mm` | `MM/dd/yyyy h:mm a` |
| Technical timestamp (logs, audit trail) | ISO 8601 with offset, `.mono`, both locales | same |
| Thousands / decimal | `1.234.567,89` | `1,234,567.89` |
| Bytes | `1,2 GB` (base 1024, one decimal) | `1.2 GB` |
| Relative time | "hace 5 min" | "5 min ago" |
| First day of week | Monday | Sunday |
| Timezone | `America/Bogota`, absolute times labeled with the zone | user zone |

Key structure: `feature.element` (`devices.tableLabel`), shared vocabulary under
`field.*`, `action.*`, `status.*`, `severity.*`, `error.*`, `state.*`, `nav.*`.
Enum keys stay English in code (`critical`, `online`); only labels are translated.

**Voice** (from the SAC brand): direct, human, no jargon where a plain word works. Errors
always name the cause and the next step. Buttons are verbs (`Guardar`, `Reintentar`,
`Exportar`), never `OK`. Never blame the user. No exclamation marks in system messages.

---

## 13. Theme implementation

Files:

```
frontend/src/
  index.html                     ← Poppins, Roboto Mono, Material Symbols Rounded
  styles.scss                    ← mat.theme(), globals, .mono, focus ring
  styles/
    _theme-colors.scss           ← generated tonal palettes (do not hand-edit)
    _tokens.scss                 ← spacing, layout, motion, severity, chart tokens
```

`styles.scss`:

```scss
@use 'sass:map';
@use '@angular/material' as mat;
@use './styles/theme-colors' as itmas;
@use './styles/tokens' as *;

// `mat.theme()` wants an M3 palette with its *own* tones (0…100) at the top level and
// `secondary`/`neutral`/`neutral-variant`/`error` nested beside them. `_theme-colors.scss`
// publishes the six ramps side by side, so assemble the two shapes here. This is a shape
// adaptation only — no tone is altered. Passing `$itmas-palettes` straight through
// compiles cleanly and silently emits `light-dark(, )` for all 18 primary/tertiary roles.
$_shared: (
  secondary: map.get(itmas.$itmas-palettes, secondary),
  neutral: map.get(itmas.$itmas-palettes, neutral),
  neutral-variant: map.get(itmas.$itmas-palettes, neutral-variant),
  error: map.get(itmas.$itmas-palettes, error),
);
$_primary: map.merge(map.get(itmas.$itmas-palettes, primary), $_shared);
$_tertiary: map.merge(map.get(itmas.$itmas-palettes, tertiary), $_shared);

html {
  @include mat.theme((
    color: (
      theme-type: color-scheme,
      primary: $_primary,
      tertiary: $_tertiary,
    ),
    typography: (
      plain-family: Poppins,
      brand-family: Poppins,
      bold-weight: 600,
      medium-weight: 500,
      regular-weight: 300,
    ),
    density: 0,
  ));

  color-scheme: light dark;
}

// Per-role weights (§3.1). `mat.theme()` has only three weight knobs and Angular
// Material maps display-*, headline-* and title-large onto *regular* — which §3.1
// sets to 300 for SAC's Light body copy — so those five roles emit 300 instead of
// the weight §3.1 asks for. Both halves are needed: Angular's own components read
// the `-weight` sub-variable, while our components follow §3.1 and use the `font:`
// shorthand, which mat.theme() emits as a literal. The shorthand is rebuilt from
// the live sub-variables so sizes and family cannot drift.
$_role-weights: (
  display-small: 400,
  headline-large: 600,
  headline-medium: 600,
  headline-small: 600,
  title-large: 500,
);

html {
  @each $role, $weight in $_role-weights {
    --mat-sys-#{$role}-weight: #{$weight};
    --mat-sys-#{$role}:
      var(--mat-sys-#{$role}-weight)
      var(--mat-sys-#{$role}-size) / var(--mat-sys-#{$role}-line-height)
      var(--mat-sys-#{$role}-font);
  }
}

html[data-theme='light'] { color-scheme: light; }
html[data-theme='dark']  { color-scheme: dark; }
```

`theme-type: color-scheme` emits both schemes as one set of `--mat-sys-*` variables that
resolve through `light-dark()`, so `color-scheme` alone switches the theme — no duplicated
CSS and no class toggling on `<body>`.

### 13.1 Iconography

**Material Symbols Rounded**, weight 300 (to sit with Poppins Light), grade 0, optical
size 20–24, `FILL 0` at rest and `FILL 1` for the active nav item.

```scss
.material-symbols-rounded, mat-icon {
  font-family: 'Material Symbols Rounded';
  font-variation-settings: 'FILL' 0, 'wght' 300, 'GRAD' 0, 'opsz' 24;
}
.nav-item--active mat-icon { font-variation-settings: 'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24; }
```

Sizes: 16px inside chips/badges, 20px inside buttons and table cells, 24px in the toolbar
and nav, 48px in empty states. Decorative icons get `aria-hidden="true"`; an icon-only
control gets `aria-label`.

Canonical icon per concept — do not substitute synonyms:

| Concept | Icon | Concept | Icon |
| --- | --- | --- | --- |
| Dashboard | `space_dashboard` | Refresh / rescan | `sync` |
| Devices | `devices` | Export | `download` |
| Network / CIDR | `lan` | Filter | `filter_list` |
| Cloud / security groups | `cloud` | Search | `search` |
| Firewall rule | `shield` | Add | `add` |
| Alerts | `notifications` | Edit | `edit` |
| Reports | `assessment` | Delete | `delete` |
| Admin / users | `manage_accounts` | Row menu | `more_vert` |
| Audit trail | `history` | Details | `open_in_new` |
| Agent | `smart_toy` | Copy value | `content_copy` |
| Online | `check_circle` | Theme | `light_mode` / `dark_mode` |
| Offline | `cancel` | Sort | handled by `mat-sort-header` |

---

## 14. Retrofit

The app predates this document: it currently runs `mat.$azure-palette` with Roboto and no
token layer. Migrate in this order — each step is independently shippable.

**Step 1 — foundations (no visual regression expected beyond hue).** — **done**, see ADR-0017 / BL-029.
- [x] Add `frontend/src/styles/_theme-colors.scss` and `_tokens.scss`.
- [x] Replace `styles.scss` with §13's version.
- [x] Update `index.html`: Poppins + Roboto Mono + Material Symbols Rounded; `lang="es-CO"`;
      `<meta name="color-scheme" content="light dark">`.
- [x] Delete the `body { font-family: Roboto … }` rule — typography now comes from the theme.
- [x] Verify every route renders in both schemes with `color-scheme` forced each way.
- [x] `ThemeService` (§2.3) + toolbar toggle — without it dark mode is not verifiable.

**Step 2 — purge hard-coded values.** — **done**, see BL-029.
- [x] `grep -rEn '#[0-9a-fA-F]{3,8}|rgba?\(' frontend/src/app` → 34 down to the 9 chart
      slot colours, explicitly deferred to step 5.
- [x] `grep -rn 'font-size' frontend/src/app` → 0; the only sizes left are `.mono`'s in
      `styles.scss`.
- [x] Replace ad-hoc margins/paddings with `--sp-*` → 43 declarations, 0 left.
- [x] Replace `outline: none` with the standard focus ring. **Read this one carefully**: our
      code never contained `outline: none`, so the item looks satisfied by inspection while the
      ring still does not paint. Angular Material sets it on `.mdc-text-field__input:focus`,
      `.mdc-button` and `.mdc-button:active`, injected after the global sheet, so `!important`
      in `styles.scss` is what actually makes §8.2 hold.
- [x] Retire every `@media (prefers-color-scheme: dark)` from component styles — it answers to
      the OS, not the theme, so it desynchronised whenever a user chose one explicitly.

**Step 3 — shell.** — **done**, see BL-029.
- [x] Apply §9.10 to `core/layout/`: toolbar heights, nav item treatment, orange left bar
      on the active item, theme toggle wired to `ThemeService`.
- [x] Move page titles into `app.routes.ts` and render them in the toolbar — as the router's
      native `title`, not `data.title`: the shell is not instantiated for `login` /
      `change-password`, so a shell-read `data.title` left those two routes with no document
      title at all. `ItmasTitleStrategy` formats `IT-MAS · <page>` for every route.
- [x] Add sidenav mode switching at the §6.3 breakpoints (`BreakpointObserver`, no new dependency).
- [x] Page header typography, and an `<h1>` on the dashboard, which had none (§11 wants one per route).

**Step 4 — data views** (`devices`, `security-group-rules`, `alerts`, `reports`, `admin`).
- [ ] Apply the §9.2 table treatment: sticky `title-small` header, no zebra, 52px rows,
      `.mono` on identifier columns, `.cell-num` on numerics.
- [ ] Adopt the five-level severity scale and delete any local status-color logic.
- [ ] Add the four states from §10.4 to each view.
- [ ] Convert filter bars to `mat-chip-row` applied-filter display with URL reflection.
- [ ] Small-screen card fallback (§10.3).

**Step 5 — dashboard and charts.**
- [ ] KPI tiles per §10.2.
- [ ] Repoint the OS-distribution chart to `--chart-1…8`, add dark variants, keep the
      ADR-0009 mark spec and the `.sr-only` mirror table.

**Step 6 — auth screens** (`login`, `change-password`).
- [ ] Centered `corner-extra-large` card on `surface`, max 420px, one filled action.
- [ ] `display-large` product name; no orange fill anywhere.
- [ ] Errors per §9.3: specific, actionable, `aria-describedby`.

**Step 7 — i18n.**
- [ ] Extract all user-visible strings to keys; `es-CO` as default locale.
- [ ] Register `es-CO` locale data and set `LOCALE_ID`; audit every `date` / `number` pipe.

---

## 15. Quick reference — do / don't

| Do | Don't |
| --- | --- |
| `var(--mat-sys-primary)` | `#004AAD` in a component |
| Orange as a 3px bar or dot | An orange button or filled badge |
| `--sp-4` | `padding: 15px` |
| `font: var(--mat-sys-body-medium)` | `font-size: 14px; font-weight: 400` |
| `.mono` on an IP column | `.mono` on a description |
| Severity hue + icon + label | Severity by color alone |
| Skeletons on first load | A spinner replacing loaded data |
| Tonal surface steps for hierarchy | Stacked shadows, glass, blur |
| One filled button per region | Three filled buttons in a row |
| `outline-variant` row rules | Zebra striping |
| Both themes verified | "It looks fine in light mode" |
| Named error + next step | "Error inesperado" |

---

## 16. Sources

- [Material 3](https://m3.material.io/) — color roles, type scale, shape, state layers, motion.
- [Angular Material theming](https://material.angular.dev/guide/theming) — `mat.theme()`,
  `--mat-sys-*` system variables.
- SAC brand palette and Poppins — [sac-saas.com](https://www.sac-saas.com/).
- `docs/adr/0009-dashboard-chart-implementation.md` — no charting library, 8 fixed slots,
  mark spec, chart accessibility. Extended by §2.7 and §10.5.
- `frontend/src/app/features/*` — the screens this document governs.
