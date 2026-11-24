# Ui — Style Reference
> frosted glass panels floating over warm ambient light

**Theme:** dark (glass)

Source of truth: `src/Compas.Server/ClientApp/src/styles/tokens.css` (tokens) and `styles/global.css` (components). If this document and the code disagree, the code wins — update this file.

Compás is a dark, glassmorphic workspace: a near-black backdrop lit by soft, blurred pools of warm amber, blue and violet light, with translucent smoked-glass cards floating on top. Every container blurs and saturates what is behind it, so the ambient light bleeds through the UI and gives it depth. The interface stays almost achromatic — light text, translucent surfaces, hairline white borders — with a single electric blue accent for progress and focus, a green for "connected" status, and a red reserved for destructive states. Typography keeps Geist's geometric neutrality with tight tracking on display sizes; the result reads like a calm, premium dashboard rather than a colourful consumer app.

## Tokens — Colors

| Name | Value | Token | Role |
|------|-------|-------|------|
| Backdrop | `#0b0d12` | `--color-backdrop` | Page background base, opaque overlays (`--bg`), under the ambient light layer |
| Glass | `rgba(28, 30, 38, 0.55)` | `--color-paper` | Card surfaces, active nav item, active tabs (always with backdrop blur) |
| Glass Fill | `rgba(255, 255, 255, 0.06)` | `--color-canvas` | Inputs, secondary buttons, chips, tags, hover fills, progress track |
| Deep | `#0f1117` | `--color-surface-alt` | Text/icons on light fills (primary button, solid badges), opaque dark hover surfaces |
| Ink | `#f4f4f6` | `--color-ink` | Primary text, headings, primary button fill, strong borders on hover/selected |
| Ink Soft | `#e4e4e8` | `--color-ink-soft` | Solid badge fill, primary button hover, secondary light text |
| Mid Gray | `#9b9ca6` | `--color-mid-gray` | Muted body text, placeholders, helper labels, icons at rest |
| Hairline | `rgba(255, 255, 255, 0.12)` | `--color-hairline` | Borders, card edges, dividers, ring track |
| Accent | `#5b8cff` | `--color-accent` | Progress bars, Today progress ring, focus outlines and focus rings. The only chromatic accent |
| OK | `#34d399` | `--color-ok` | "Connected" status dot (with a soft glow) |
| Ember | `#ff5a5f` | `--color-ember` | Destructive actions and errors only |

### Ambient light (`body::before`)

A fixed, non-interactive layer behind everything:

```css
radial-gradient(1100px 700px at 10% -10%, rgba(255, 168, 92, 0.26), transparent 60%),  /* warm amber, top-left */
radial-gradient(900px 650px at 100% 5%,   rgba(91, 140, 255, 0.24), transparent 60%),  /* blue, top-right */
radial-gradient(1000px 800px at 55% 115%, rgba(168, 96, 255, 0.16), transparent 60%),  /* violet, bottom */
linear-gradient(180deg, #11131a 0%, #0b0d12 100%);
```

It is the only place where large gradients are allowed. It must stay subtle: it tints the glass, it is never content.

## Tokens — Typography

### Geist — All interface text — body at 14px/400, headings ranging 24–48px/600, buttons at 13–14px/500. Geist's geometric letterforms and uniform stroke width keep the UI neutral against the atmospheric background; weight 600 at large sizes with tight tracking produces confident headlines. · `--font-geist`
- **Substitute:** Inter
- **Weights:** 400, 500, 600
- **Sizes:** 12, 13, 14, 16, 18, 24, 30, 36, 48
- **Line height:** 1.10, 1.11, 1.20, 1.33, 1.43, 1.50, 1.56, 1.63, 2.00
- **Letter spacing:** -0.0500em at display (48px), -0.0250em at subheading (24–30px), 0.0500em at caption (12px uppercase).
- **OpenType features:** `"ss01" on, "cv11" on`
- **Rendering:** `-webkit-font-smoothing: antialiased` — important for light text on dark backgrounds.

### Type Scale

| Role | Family | Weight | Size | Line Height | Letter Spacing | Token |
|------|--------|--------|------|-------------|----------------|-------|
| caption | — | — | 12px | 1.33 | 0.6px | `--text-caption` |
| body | — | — | 14px | 1.43 | — | `--text-body` |
| body-lg | — | — | 16px | 1.5 | — | `--text-body-lg` |
| subheading | — | — | 18px | 1.56 | — | `--text-subheading` |
| heading-sm | — | — | 24px | 1.33 | -0.6px | `--text-heading-sm` |
| heading | — | — | 30px | 1.2 | -0.75px | `--text-heading` |
| heading-lg | — | — | 36px | 1.11 | -0.9px | `--text-heading-lg` |
| display | — | — | 48px | 1.1 | -2.4px | `--text-display` |

## Tokens — Spacing & Shapes

**Base unit:** 4px

**Density:** compact

### Spacing Scale

| Name | Value | Token |
|------|-------|-------|
| 4 | 4px | `--spacing-4` |
| 8 | 8px | `--spacing-8` |
| 12 | 12px | `--spacing-12` |
| 16 | 16px | `--spacing-16` |
| 20 | 20px | `--spacing-20` |
| 24 | 24px | `--spacing-24` |
| 48 | 48px | `--spacing-48` |

### Border Radius

| Element | Value |
|---------|-------|
| cards | 24px |
| small | 6px |
| badges | 18px |
| inputs | 18px |
| nested | 10px |
| buttons | 18px |

### Shadows & Blur

| Name | Value | Token |
|------|-------|-------|
| card | `inset 0 1px 0 rgba(255,255,255,0.08), 0 8px 32px rgba(0,0,0,0.35), 0 1px 2px rgba(0,0,0,0.3)` | `--shadow-card` |
| card hover (clickable rows) | `inset 0 1px 0 rgba(255,255,255,0.1), 0 12px 36px rgba(0,0,0,0.45)` | — |
| primary button | `0 4px 16px rgba(0,0,0,0.35)` | — |
| focus ring | `0 0 0 3px rgba(91,140,255,0.25)` | — |
| glass blur | `blur(22px) saturate(150%)` | `--glass-blur` |

The inset 1px white highlight on the top edge is what sells the "glass" — it mimics light catching the bevel of the pane.

### Layout

- **Page max-width:** 1280px
- **Section gap:** 48-80px
- **Card padding:** 20px
- **Element gap:** 8px
- **Shell:** 88px left rail + fluid main column; below 720px the rail becomes a fixed glass bottom bar.

## Components

### Primary Filled Button
**Role:** High-emphasis action (Save, Create, Generate)

Background `#f4f4f6` (ink), text `#0f1117` (deep), no border, radius 18px, padding 0 16px, min-height 36px, font 14px weight 500, shadow `0 4px 16px rgba(0,0,0,0.35)`. Hover: `#e4e4e8` at 0.88 opacity. A bright pill on dark glass — the strongest element on any screen.

### Secondary Button
**Role:** Low-emphasis action (Cancel, Back)

Background `rgba(255,255,255,0.06)`, text ink, no border, radius 18px. Hover: `rgba(255,255,255,0.12)`.

### Ghost Button
**Role:** Tertiary inline action

Transparent, text mid-gray, padding 0 12px. Hover: glass fill + ink text.

### Destructive Button
**Role:** Delete, remove, revoke

Transparent, 1px hairline border, text `#ff5a5f`. Hover: glass fill.

### Card
**Role:** Content container for panels and dashboard blocks

Background `rgba(28,30,38,0.55)`, 1px hairline border (`rgba(255,255,255,0.12)`), radius 24px, `--shadow-card`, padding 20px, `backdrop-filter: blur(22px) saturate(150%)`. Hover brightens the border to `rgba(255,255,255,0.2)`. The same glass treatment applies to record rows and timeline blocks.

### Modal
**Role:** Dialogs over content

Overlay `rgba(0,0,0,0.5)` with `blur(6px)`. Panel is more opaque than cards — `rgba(22,24,31,0.88)` — so text stays legible over busy content; same border, radius and shadow as cards.

### Sidebar / Bottom Bar
**Role:** Main navigation

Background `rgba(15,17,23,0.45)` with glass blur and a 1px hairline right border. Active item: glass card surface + card shadow + ink text; inactive: mid-gray, hover glass fill. Brand mark: 40px ink square with deep "C".

### Input Field
**Role:** Text entry, search, form controls

Glass fill background, ink text, mid-gray placeholder, radius 18px. Focus: accent focus ring `0 0 0 3px rgba(91,140,255,0.25)`. Native controls render dark thanks to `color-scheme: dark`.

### Badges
- **Solid:** background `#e4e4e8`, text `#0f1117`, radius 18px, padding 2px 8px, 12px/500. Used for flags and high/urgent priority.
- **Soft:** glass fill background, ink-soft text.
- **Outline:** transparent with hairline border.

### Status Pill
Glass surface, hairline border, 12px/500 muted text, with a 7px dot: green `#34d399` with glow when connected, ember on error, hairline while loading.

### Progress
- **Bar:** 6px track in glass fill; fill is a `#3b6dff → #5b8cff` gradient with `0 0 10px rgba(91,140,255,0.5)` glow.
- **Ring (Today):** hairline track, accent stroke, round caps.

### Stat Block
Label 12–14px mid-gray, value 30–48px weight 600 ink with tight tracking. Relies on type scale; sits inside a glass card.

## Do's and Don'ts

### Do
- Put every container on glass: translucent paper + hairline border + `--glass-blur` + `--shadow-card`.
- Keep the ambient light layer as the only large gradient; let it show through the glass.
- Use `#f4f4f6` pills with dark text for primary actions — they must be the brightest element on screen.
- Use `#5b8cff` only for progress, focus and the current/selected state indicators.
- Reserve `#ff5a5f` exclusively for destructive actions and errors.
- Make overlays (modals, popovers over content) more opaque than cards so they stay readable.
- Use colour tokens, never literals — in TSX/SVG too (`style={{ stroke: "var(--color-accent)" }}`).

### Don't
- Do not introduce new chromatic colours beyond accent, OK and ember (project/calendar colours chosen by the user are data, not theme).
- Do not use black-based rgba (`rgba(10,10,10,…)`) for fills or rings — invisible on dark; use white-based rgba or tokens.
- Do not use radii other than 18px (interactive) and 24px (containers), plus 6/10px for small nested elements.
- Do not drop the hairline border on glass — blur alone does not define the edge.
- Do not stack many blurred layers inside each other; one level of glass per region (performance and legibility).
- Do not set body text below 14px or muted text darker than `#9b9ca6`.

## Surfaces

| Level | Name | Value | Purpose |
|-------|------|-------|---------|
| 0 | Backdrop | `#0b0d12` + ambient light | Page background |
| 1 | Rail | `rgba(15,17,23,0.45)` + blur | Navigation |
| 2 | Glass card | `rgba(28,30,38,0.55)` + blur | Primary content containers |
| 3 | Glass fill | `rgba(255,255,255,0.06)` | Inputs, chips, secondary buttons inside cards |
| 4 | Modal | `rgba(22,24,31,0.88)` + blur | Dialogs above everything |

## Elevation

- **Card:** `inset 0 1px 0 rgba(255,255,255,0.08), 0 8px 32px rgba(0,0,0,0.35), 0 1px 2px rgba(0,0,0,0.3)`
- **Clickable row (hover):** border ink + `translateY(-1px)` + `inset 0 1px 0 rgba(255,255,255,0.1), 0 12px 36px rgba(0,0,0,0.45)`
- **Button (primary):** `0 4px 16px rgba(0,0,0,0.35)`
- **Input (focus):** accent ring `0 0 0 3px rgba(91,140,255,0.25)`

## Imagery

No photography or illustrations. Atmosphere comes from the ambient light layer, not images. Icons are thin-stroke geometric marks (Lucide-like) at 1.5–2px stroke in ink or mid-gray.

## Agent Prompt Guide

**Quick Color Reference**
- Background: #0b0d12 + ambient radial light
- Card/surface: rgba(28,30,38,0.55) + blur(22px) saturate(150%)
- Primary text: #f4f4f6
- Muted text: #9b9ca6
- Border: rgba(255,255,255,0.12)
- Primary action: #f4f4f6 fill, #0f1117 text
- Accent: #5b8cff
- Destructive: #ff5a5f

**Example Component Prompts**
1. Dashboard stat card: background rgba(28,30,38,0.55), backdrop-filter blur(22px) saturate(150%), 1px solid rgba(255,255,255,0.12) border, 24px radius, shadow inset 0 1px 0 rgba(255,255,255,0.08) + 0 8px 32px rgba(0,0,0,0.35), 20px padding. Label 12px #9b9ca6, value 36px Geist 600 #f4f4f6, -0.025em tracking.
2. Primary button: background #f4f4f6, text #0f1117, no border, 18px radius, padding 0 16px, 36px height, 14px/500, shadow 0 4px 16px rgba(0,0,0,0.35).
3. Secondary button: background rgba(255,255,255,0.06), text #f4f4f6, 18px radius, same size as primary; hover rgba(255,255,255,0.12).
4. Input: background rgba(255,255,255,0.06), text #f4f4f6, placeholder #9b9ca6, 18px radius; focus ring 0 0 0 3px rgba(91,140,255,0.25).
5. Progress bar: 6px track rgba(255,255,255,0.06); fill linear-gradient(90deg, #3b6dff, #5b8cff) with 0 0 10px rgba(91,140,255,0.5) glow.

## Design Philosophy

(1) **Depth through light, not colour** — the palette stays achromatic; depth comes from translucency, blur and the ambient light behind it. (2) **Radius defines hierarchy** — 18px for interactive elements, 24px for containers. (3) **One accent, used sparingly** — blue means progress or focus; everything else is ink on glass. (4) **Tokens first** — the theme lives in `tokens.css`; components only reference variables, so the whole look can change from one file.

## Similar Brands

- **Apple visionOS / macOS** — Frosted materials over ambient content, bright pill controls, inset highlights.
- **Linear** — Dark achromatic UI, tight typography, single accent.
- **Raycast** — Dark glass panels with hairline borders and subtle glow.
- **Arc Browser** — Atmospheric gradient backgrounds behind translucent chrome.

## Quick Start

### CSS Custom Properties

```css
:root {
  color-scheme: dark;

  /* Colors */
  --color-canvas: rgba(255, 255, 255, 0.06);
  --color-paper: rgba(28, 30, 38, 0.55);
  --color-surface-alt: #0f1117;
  --color-ink: #f4f4f6;
  --color-ink-soft: #e4e4e8;
  --color-mid-gray: #9b9ca6;
  --color-hairline: rgba(255, 255, 255, 0.12);
  --color-ember: #ff5a5f;
  --color-accent: #5b8cff;
  --color-ok: #34d399;
  --color-backdrop: #0b0d12;

  /* Glass */
  --glass-blur: blur(22px) saturate(150%);

  /* Typography — Font Families */
  --font-geist: 'Geist', 'Inter', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;

  /* Typography — Scale */
  --text-caption: 12px;
  --leading-caption: 1.33;
  --tracking-caption: 0.6px;
  --text-body: 14px;
  --leading-body: 1.43;
  --text-body-lg: 16px;
  --leading-body-lg: 1.5;
  --text-subheading: 18px;
  --leading-subheading: 1.56;
  --text-heading-sm: 24px;
  --leading-heading-sm: 1.33;
  --tracking-heading-sm: -0.6px;
  --text-heading: 30px;
  --leading-heading: 1.2;
  --tracking-heading: -0.75px;
  --text-heading-lg: 36px;
  --leading-heading-lg: 1.11;
  --tracking-heading-lg: -0.9px;
  --text-display: 48px;
  --leading-display: 1.1;
  --tracking-display: -2.4px;

  /* Typography — Weights */
  --font-weight-regular: 400;
  --font-weight-medium: 500;
  --font-weight-semibold: 600;

  /* Spacing */
  --spacing-unit: 4px;
  --spacing-4: 4px;
  --spacing-8: 8px;
  --spacing-12: 12px;
  --spacing-16: 16px;
  --spacing-20: 20px;
  --spacing-24: 24px;
  --spacing-48: 48px;

  /* Layout */
  --page-max-width: 1280px;
  --card-padding: 20px;
  --element-gap: 8px;

  /* Named Radii */
  --radius-cards: 24px;
  --radius-small: 6px;
  --radius-badges: 18px;
  --radius-inputs: 18px;
  --radius-nested: 10px;
  --radius-buttons: 18px;

  /* Shadows */
  --shadow-card: inset 0 1px 0 rgba(255, 255, 255, 0.08), 0 8px 32px rgba(0, 0, 0, 0.35), 0 1px 2px rgba(0, 0, 0, 0.3);

  /* Surfaces */
  --surface-backdrop: #0b0d12;
  --surface-rail: rgba(15, 17, 23, 0.45);
  --surface-card: rgba(28, 30, 38, 0.55);
  --surface-input-fill: rgba(255, 255, 255, 0.06);
  --surface-modal: rgba(22, 24, 31, 0.88);
}
```

### Tailwind v4

```css
@theme {
  /* Colors */
  --color-canvas: rgba(255, 255, 255, 0.06);
  --color-paper: rgba(28, 30, 38, 0.55);
  --color-surface-alt: #0f1117;
  --color-ink: #f4f4f6;
  --color-ink-soft: #e4e4e8;
  --color-mid-gray: #9b9ca6;
  --color-hairline: rgba(255, 255, 255, 0.12);
  --color-ember: #ff5a5f;
  --color-accent: #5b8cff;
  --color-ok: #34d399;
  --color-backdrop: #0b0d12;

  /* Typography */
  --font-geist: 'Geist', 'Inter', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;

  /* Typography — Scale */
  --text-caption: 12px;
  --text-body: 14px;
  --text-body-lg: 16px;
  --text-subheading: 18px;
  --text-heading-sm: 24px;
  --text-heading: 30px;
  --text-heading-lg: 36px;
  --text-display: 48px;

  /* Spacing */
  --spacing-4: 4px;
  --spacing-8: 8px;
  --spacing-12: 12px;
  --spacing-16: 16px;
  --spacing-20: 20px;
  --spacing-24: 24px;
  --spacing-48: 48px;

  /* Border Radius */
  --radius-md: 6px;
  --radius-lg: 10px;
  --radius-2xl: 18px;
  --radius-3xl: 24px;

  /* Shadows */
  --shadow-card: inset 0 1px 0 rgba(255, 255, 255, 0.08), 0 8px 32px rgba(0, 0, 0, 0.35), 0 1px 2px rgba(0, 0, 0, 0.3);
}
```
