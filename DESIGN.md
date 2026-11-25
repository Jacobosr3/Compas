# Ui — Style Reference
> frosted glass HUD floating over a drifting neon aurora

**Theme:** dark (futuristic glass) · **Primary target:** mobile (most use is on the phone)

Source of truth: `src/Compas.Server/ClientApp/src/styles/tokens.css` (tokens) and `styles/global.css` (components). If this document and the code disagree, the code wins — update this file.

Compás is a dark, futuristic glass workspace designed mobile-first: a near-black backdrop with a slowly drifting aurora (cyan, violet, a touch of amber) and a faint HUD grid, with translucent smoked-glass cards floating on top. Every container blurs and saturates what is behind it, so the ambient light bleeds through the UI and gives it depth. The interface stays almost achromatic — light text, translucent surfaces, hairline white borders — with a cyan→indigo→violet neon accent for progress, focus and the active tab, a green for "connected" status, and a red reserved for destructive states. Typography keeps Geist for the interface and adds Geist Mono for HUD-style labels. Navigation is a floating glass dock on the phone (left rail on desktop) that feels alive: a light pill slides with a spring to the active tab and gently breathes, an energy line flows along its edge and a colour halo drifts inside it.

## Tokens — Colors

| Name | Value | Token | Role |
|------|-------|-------|------|
| Backdrop | `#07080d` | `--color-backdrop` | Page background base, opaque overlays (`--bg`), under the ambient light layer |
| Glass | `rgba(28, 30, 38, 0.55)` | `--color-paper` | Card surfaces, active nav item, active tabs (always with backdrop blur) |
| Glass Fill | `rgba(255, 255, 255, 0.06)` | `--color-canvas` | Inputs, secondary buttons, chips, tags, hover fills, progress track |
| Deep | `#0f1117` | `--color-surface-alt` | Text/icons on light fills (primary button, solid badges), opaque dark hover surfaces |
| Ink | `#f4f4f6` | `--color-ink` | Primary text, headings, primary button fill, strong borders on hover/selected |
| Ink Soft | `#e4e4e8` | `--color-ink-soft` | Solid badge fill, primary button hover, secondary light text |
| Mid Gray | `#9b9ca6` | `--color-mid-gray` | Muted body text, placeholders, helper labels, icons at rest |
| Hairline | `rgba(255, 255, 255, 0.12)` | `--color-hairline` | Borders, card edges, dividers, ring track |
| Accent | `#38bdf8` | `--color-accent` | Today progress ring, focus outlines/rings, active nav icon |
| Accent 2 | `#a855f7` | `--color-accent-2` | End of the neon gradient |
| Neon gradient | `linear-gradient(135deg, #22d3ee, #6366f1 55%, #a855f7)` | `--gradient-accent` | Progress bars, "Now" card border, nav light bar, logo ring |
| OK | `#34d399` | `--color-ok` | "Connected" status dot (with a soft glow) |
| Ember | `#ff5a5f` | `--color-ember` | Destructive actions and errors only |

### Aurora and grid (`body::before` / `body::after`)

- **Aurora (`body::before`):** fixed layer 25% larger than the viewport with radial pools — cyan top-right, amber top-left (faint), violet bottom — over `#0c0e16 → #07080d`. It drifts with `aurora-drift` (32s, transform only).
- **HUD grid (`body::after`):** 1px white lines at 3.5% every 44px, masked with a radial fade so it only shows near the top.

These are the only large gradients allowed. They tint the glass; they are never content.

## Tokens — Typography

### Geist — All interface text — body at 14px/400, headings ranging 24–48px/600, buttons at 13–14px/500. Geist's geometric letterforms and uniform stroke width keep the UI neutral against the atmospheric background; weight 600 at large sizes with tight tracking produces confident headlines. · `--font-geist`
- **Substitute:** Inter
- **Weights:** 400, 500, 600
- **Sizes:** 12, 13, 14, 16, 18, 24, 30, 36, 48
- **Line height:** 1.10, 1.11, 1.20, 1.33, 1.43, 1.50, 1.56, 1.63, 2.00
- **Letter spacing:** -0.0500em at display (48px), -0.0250em at subheading (24–30px), 0.0500em at caption (12px uppercase).
- **OpenType features:** `"ss01" on, "cv11" on`
- **Rendering:** `-webkit-font-smoothing: antialiased` — important for light text on dark backgrounds.

### Geist Mono — HUD labels · `--font-mono`
Eyebrows (`.now-eyebrow`), stat labels (`.stat-lbl`, uppercase, 0.08em) and page subtitles (`.topbar .date`, 12.5px, sentence case). Never for body text or long paragraphs.

### Gradient text
Page titles (`.topbar h1`: white → `#a5f3fc`) and stat numbers (`.stat-num`: white → `#93c5fd`) use `background-clip: text`.

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
| card | `inset 0 1px 0 rgba(125,211,252,0.14), 0 8px 32px rgba(0,0,0,0.4), 0 1px 2px rgba(0,0,0,0.3)` | `--shadow-card` |
| card hover glow | `--shadow-card` + `0 0 28px rgba(56,189,248,0.08)`, border `rgba(125,211,252,0.28)` | — |
| neon glow | `0 0 18px rgba(56,189,248,0.45)` | `--glow-accent` |
| card hover (clickable rows) | `inset 0 1px 0 rgba(255,255,255,0.1), 0 12px 36px rgba(0,0,0,0.45)` | — |
| primary button | `0 4px 16px rgba(0,0,0,0.35)` | — |
| focus ring | `0 0 0 3px rgba(56,189,248,0.25)` | — |
| glass blur | `blur(22px) saturate(160%)`; `blur(14px) saturate(150%)` at ≤720px | `--glass-blur` |

The inset 1px cyan-tinted highlight on the top edge is what sells the "glass" — it mimics light catching the bevel of the pane.

### Layout

- **Page max-width:** 1280px
- **Section gap:** 48-80px
- **Card padding:** 20px
- **Element gap:** 8px
- **Shell:** 88px left rail + fluid main column; below 720px the rail becomes a floating glass dock (12px from the sides, 10px + safe area from the bottom, radius 26px). Main bottom padding: `100px + safe area`.
- **Mobile first:** design and check at 390px wide first. Touch targets ≥ 44px; no horizontal overflow (single-column grids use `minmax(0, 1fr)`, not `1fr`).

### Two versions: phone and desktop

Compás is used on two kinds of device and **every view we build must be designed, built and checked for both** — it is part of "done", not a later polish. One codebase serves both: the phone version is the base and the desktop version is a layer on top, switched by media queries (no separate routes or components unless a view truly cannot adapt).

| | Phone (iPhone, installed as an app) | Desktop |
|---|---|---|
| Width | ≤ 720px (design and check at **390×844**) | > 720px (check at **1440×900**) |
| Navigation | Floating glass dock at the bottom | 88px left rail |
| Layout | One column, full-width cards; primary info first, actions below it | Main column + right rail, multi-column grids |
| Input | Touch: targets ≥ 44px, no hover, 16px form fields (iOS zooms below that) | Mouse: hover states (`(hover: hover) and (pointer: fine)` only) |
| Safe areas | Respect `env(safe-area-inset-*)`; the dock never covers content (bottom padding `100px + safe area`) | n/a |

Rules for every new view:
- **Phone first, then desktop.** Write the base CSS for the phone and add desktop behaviour afterwards. Phone-specific adjustments go in a `@media (max-width: 720px)` block next to the component's styles (existing ones live in the "Versión móvil" section at the end of `global.css`).
- **A row that fits on desktop rarely fits on a phone.** When a row mixes a title with actions or metadata (task cards, list items), stack it in two rows on the phone: text on top, actions below. Never let a text column shrink to a few characters.
- **Forms:** one field per row on the phone unless the fields are short (date + time); give long fields and selects the full width. Never rely on `auto-fit` columns to decide this.
- **No horizontal overflow** at 390px, with long names (projects, tasks, events) and with empty, loading and error states.
- **Features that need a tap on the phone** (camera, file picker) must be reachable in one tap; browsers will not open them from code.
- **The installed app** (`display-mode: standalone`, see `manifest.webmanifest`) has no browser bar and no Safari login: check the view signed in from the home-screen icon too.
- **Verify with screenshots** at both sizes before committing (Playwright is a devDependency; capture each route at the phone viewport `iPhone 14` and at 1440×900) and mention both in the commit or PR.

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

### Navigation — Rail (desktop) / Dock (mobile)
**Role:** Main navigation; it should feel alive

- **Surface:** `rgba(15,17,23,0.45)` + glass blur, hairline border; `overflow: hidden; isolation: isolate`.
- **Sliding indicator (`.nav-indicator`):** a single element per nav, positioned from `--idx` (active tab index, computed in `Sidebar.tsx`) and `--count`. Neon-tinted glass pill with a cyan inner ring; moves with `transform` and a spring (`cubic-bezier(0.34, 1.4, 0.5, 1)`, 0.55s). Its `::after` glow breathes (`breathe`, 3.2s, opacity only); its `::before` is a 3px neon light bar on the outer edge (left on desktop, top on mobile).
- **Energy line (`.rail-left::before`):** 1px neon gradient flowing along the edge (right on desktop, top on mobile) with `flow-y` / `flow-x`, 6–7s, transform only.
- **Aura (`.rail-aura`):** soft cyan/violet halo drifting inside the bar (`aura-y` / `aura-x`, 11–12s).
- **Items:** inactive mid-gray; active white label, icon `#7dd3fc` with neon drop-shadow and a spring `icon-pop` when it becomes active; pressing scales the icon to 0.86. Mobile labels 10.5px.
- **Brand (desktop):** 40px ink square with deep "C", cyan glow and a rotating conic neon ring (`@property --spin`, 4s).

### Input Field
**Role:** Text entry, search, form controls

Glass fill background, ink text, mid-gray placeholder, radius 18px. Focus: accent focus ring `0 0 0 3px rgba(56,189,248,0.25)`. Native controls render dark thanks to `color-scheme: dark`.

### Badges
- **Solid:** background `#e4e4e8`, text `#0f1117`, radius 18px, padding 2px 8px, 12px/500. Used for flags and high/urgent priority.
- **Soft:** glass fill background, ink-soft text.
- **Outline:** transparent with hairline border.

### Status Pill
Glass surface, hairline border, 12px/500 muted text, with a 7px dot: green `#34d399` with a breathing glow when connected, ember on error, hairline while loading.

### "Now" card
Glass card with a 1px neon gradient border (masked `::before`, 75% opacity) and an extra `0 0 40px rgba(56,189,248,0.1)` glow. The hero of the Today screen.

### Progress
- **Bar:** 6px track in glass fill; fill is `--gradient-accent` with `0 0 10px rgba(56,189,248,0.5)` glow.
- **Ring (Today):** hairline track, accent stroke, round caps.

### Hover — "about to press" (mouse/trackpad only)
**Role:** Tell desktop users what is clickable before they click

Lives in `@media (hover: hover) and (pointer: fine)`, so touch screens never get a stuck hover. Every rule excludes `:active`, so the existing press feedback (`scale(0.97)` on buttons, `0.86` on nav icons) always wins while clicking.
- **Buttons:** lift `translateY(-2px)` with a spring (`cubic-bezier(0.2, 0.8, 0.2, 1)`, 0.22s), cyan inner ring + soft neon glow; primary/secondary also get a light sheen sweeping across (0.7s). Destructive buttons glow red.
- **Nav items:** faint cyan tint and ring; icon lifts and scales to 1.1 with a neon drop-shadow.
- **Clickable rows and cards** (`.record-row.clickable`, `.board-card`): lift 3px, neon border `rgba(125,211,252,0.5)`, deeper shadow + glow. Tabs and tag buttons lift 1px.
- **Borderless list rows** (`.proj-row`, `.inbox-row.clickable`, `.mini-week`): slide 3px right and/or a cyan tint with a 2px cyan left edge.
- **Draggable blocks** (timeline blocks, calendar events): glow only, never moved, so dragging is not disturbed.
- Informational cards (`.card`, `.stat`) only brighten their border — they do not lift, because they are not clickable.

### Stat Block
Label 12–14px mid-gray, value 30–48px weight 600 ink with tight tracking. Relies on type scale; sits inside a glass card.

## Do's and Don'ts

### Do
- Put every container on glass: translucent paper + hairline border + `--glass-blur` + `--shadow-card`.
- Keep the ambient light layer as the only large gradient; let it show through the glass.
- Use `#f4f4f6` pills with dark text for primary actions — they must be the brightest element on screen.
- Use the neon accent only for progress, focus and the current/selected state.
- Animate only `transform` and `opacity` (or tiny elements); keep loops slow (≥3s) so the UI feels alive, not busy. Everything must still work with `prefers-reduced-motion` (a global rule disables motion).
- Check every change at phone width first, then on desktop — every view has two versions (see "Two versions: phone and desktop").
- Reserve `#ff5a5f` exclusively for destructive actions and errors.
- Make overlays (modals, popovers over content) more opaque than cards so they stay readable.
- Use colour tokens, never literals — in TSX/SVG too (`style={{ stroke: "var(--color-accent)" }}`).

### Don't
- Do not introduce new chromatic colours beyond accent, OK and ember (project/calendar colours chosen by the user are data, not theme).
- Do not use black-based rgba (`rgba(10,10,10,…)`) for fills or rings — invisible on dark; use white-based rgba or tokens.
- Do not use radii other than 18px (interactive) and 24px (containers), plus 6/10px for small nested elements.
- Do not drop the hairline border on glass — blur alone does not define the edge.
- Do not stack many blurred layers inside each other; one level of glass per region (performance and legibility).
- Do not animate `box-shadow`, `filter` or layout properties on large elements — it drains battery on phones.
- Do not use Geist Mono or uppercase for long sentences.
- Do not set body text below 14px or muted text darker than `#9b9ca6`.

## Surfaces

| Level | Name | Value | Purpose |
|-------|------|-------|---------|
| 0 | Backdrop | `#07080d` + aurora + HUD grid | Page background |
| 1 | Rail / Dock | `rgba(15,17,23,0.45)` + blur | Navigation |
| 2 | Glass card | `rgba(28,30,38,0.55)` + blur | Primary content containers |
| 3 | Glass fill | `rgba(255,255,255,0.06)` | Inputs, chips, secondary buttons inside cards |
| 4 | Modal | `rgba(22,24,31,0.88)` + blur | Dialogs above everything |

## Elevation

- **Card:** `inset 0 1px 0 rgba(255,255,255,0.08), 0 8px 32px rgba(0,0,0,0.35), 0 1px 2px rgba(0,0,0,0.3)`
- **Clickable row (hover):** border ink + `translateY(-1px)` + `inset 0 1px 0 rgba(255,255,255,0.1), 0 12px 36px rgba(0,0,0,0.45)`
- **Button (primary):** `0 4px 16px rgba(0,0,0,0.35)`
- **Input (focus):** accent ring `0 0 0 3px rgba(56,189,248,0.25)`
- **Mobile dock:** `inset 0 1px 0 rgba(125,211,252,0.16), 0 12px 40px rgba(0,0,0,0.55), 0 0 0 1px rgba(0,0,0,0.4)`

## Imagery

No photography or illustrations. Atmosphere comes from the aurora and the HUD grid, not images. Icons are thin-stroke geometric marks (Lucide-like) at 1.5–2px stroke in ink or mid-gray.

## Agent Prompt Guide

**Quick Color Reference**
- Background: #07080d + drifting aurora + faint HUD grid
- Card/surface: rgba(28,30,38,0.55) + blur(22px) saturate(150%)
- Primary text: #f4f4f6
- Muted text: #9b9ca6
- Border: rgba(255,255,255,0.12)
- Primary action: #f4f4f6 fill, #0f1117 text
- Accent: #38bdf8 · neon gradient #22d3ee → #6366f1 → #a855f7
- Destructive: #ff5a5f

**Example Component Prompts**
1. Dashboard stat card: background rgba(28,30,38,0.55), backdrop-filter blur(22px) saturate(150%), 1px solid rgba(255,255,255,0.12) border, 24px radius, shadow inset 0 1px 0 rgba(255,255,255,0.08) + 0 8px 32px rgba(0,0,0,0.35), 20px padding. Label 12px #9b9ca6, value 36px Geist 600 #f4f4f6, -0.025em tracking.
2. Primary button: background #f4f4f6, text #0f1117, no border, 18px radius, padding 0 16px, 36px height, 14px/500, shadow 0 4px 16px rgba(0,0,0,0.35).
3. Secondary button: background rgba(255,255,255,0.06), text #f4f4f6, 18px radius, same size as primary; hover rgba(255,255,255,0.12).
4. Input: background rgba(255,255,255,0.06), text #f4f4f6, placeholder #9b9ca6, 18px radius; focus ring 0 0 0 3px rgba(56,189,248,0.25).
5. Progress bar: 6px track rgba(255,255,255,0.06); fill linear-gradient(135deg, #22d3ee, #6366f1 55%, #a855f7) with 0 0 10px rgba(56,189,248,0.5) glow.

## Design Philosophy

(1) **Depth through light, not colour** — surfaces stay achromatic; depth comes from translucency, blur and the aurora behind them. (2) **Alive, not busy** — slow, GPU-friendly motion in the navigation and background; content stays still. (3) **Radius defines hierarchy** — 18px for interactive elements, 24px for containers. (4) **One neon accent, used sparingly** — it means progress, focus or "you are here"; everything else is ink on glass. (5) **Mobile first** — the phone is the main device. (6) **Tokens first** — the theme lives in `tokens.css`; components only reference variables, so the whole look can change from one file.

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
  --color-accent: #38bdf8;
  --color-accent-2: #a855f7;
  --color-ok: #34d399;
  --color-backdrop: #07080d;

  /* Neon */
  --gradient-accent: linear-gradient(135deg, #22d3ee 0%, #6366f1 55%, #a855f7 100%);
  --glow-accent: 0 0 18px rgba(56, 189, 248, 0.45);

  /* Glass */
  --glass-blur: blur(22px) saturate(160%);

  /* Typography — Font Families */
  --font-geist: 'Geist', 'Inter', ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  --font-mono: 'Geist Mono', ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;

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
  --shadow-card: inset 0 1px 0 rgba(125, 211, 252, 0.14), 0 8px 32px rgba(0, 0, 0, 0.4), 0 1px 2px rgba(0, 0, 0, 0.3);

  /* Surfaces */
  --surface-backdrop: #07080d;
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
  --color-accent: #38bdf8;
  --color-accent-2: #a855f7;
  --color-ok: #34d399;
  --color-backdrop: #07080d;

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
  --shadow-card: inset 0 1px 0 rgba(125, 211, 252, 0.14), 0 8px 32px rgba(0, 0, 0, 0.4), 0 1px 2px rgba(0, 0, 0, 0.3);
}
```
