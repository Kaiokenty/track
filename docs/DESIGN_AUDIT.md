# Track — Design Audit (Phase 1)

Date: 2026-07-22  
Surfaces audited: **Settings (Connections)**, **Tray flyout**, **Tray icon / interaction**  
Method: code + XAML review against Mac peers (OpenUsage.ai / CodexBar) and Win11 tray norms. Live pixel capture was blocked (Settings HWND reported 0×0 while process stayed alive); findings below are from the shipping UI definition. Faithful mocks: `docs/audit/settings.png`, `docs/audit/flyout.png`.

**Implementation target (Jul 2026):** P0/P1 fixes land in **Tauri 2 + React** (`apps/desktop`), not further WPF polish. WPF tray open path is fixed for dogfood; UI redesign ships on Tauri. Audit P2 (Mica/Fluent depth) + click-to-flip → PLAN Phase 2.

---

## Verdict

**Functional Phase 1 shell, not yet a polished tray product.** Information architecture matches the plan (connections list + pace flyout), but visual system, hierarchy, and tray discoverability are still scaffold-grade. Biggest gaps vs OpenUsage.ai: no meter bars, weak provider identity, chart competes with dense text, Settings feels like a generic admin form.

---

## What’s working

1. **Clear dual-surface model** — Settings for linking, flyout for glance usage (right idea for a tray app).
2. **Pace as first-class** — Recommended vs actual is differentiated (dashed vs solid); that is the product wedge.
3. **Token palette is coherent** — warm off-white `#F7F5F2`, stone ink `#1C1917`, teal accent `#0F766E`, muted `#78716C` (defined in `App.xaml`). Avoids purple-slop defaults.
4. **Connections pattern** — list rows with status + action button mirrors Midday/Close-style linked accounts.
5. **Honest status copy** — “NotLinked / Phase 2” for API providers sets expectations.

---

## Critical issues (fix first)

### 1. Tray flyout discoverability is broken UX
Users already reported no flyout. Left-click popup + overflow-hidden icons means the core surface is invisible.
- **Fix:** Pin a readable tray title (`Track 100%` / `C · over`) via `H.NotifyIcon` text overlay; first-run tip in Settings; keep **Open usage** as primary menu item (already added).
- **Also:** Don’t auto-show full Settings on every launch long-term — show once, then tray-only.

### 2. Flyout hierarchy is upside-down
Primary job is “am I over/under pace?” but the UI leads with title + radio + long remaining string before the chart.
- **Fix order:** Plan badge → **pace verdict chip** (over/under) → chart → one-line remaining → secondary meters.
- Drop or demote the footnote until hover/settings.

### 3. Chart is under-designed for the wedge
Hard-coded 140×320 canvas, 9px axis labels, no legend for actual vs recommended except a tiny “recommended →”, no unit on Y ($ vs %).
- **Fix:** Y-axis `$` or `%` explicitly; legend row; thicker actual stroke; fill only between actual and recommended (green under / red over bands).

### 4. Settings is visually “admin template”
200px charcoal sidebar + default WPF buttons/radios/checkboxes = prototype, not Win11 companion.
- **Fix:** Use Fluent-ish list rows: provider logo, title, subtitle, trailing status pill + chevron; accent only on primary CTA; remove disabled Phase-2 chrome from the first viewport or group under “Coming soon”.

### 5. Provider identity is weak
No Cursor/OpenAI/Claude marks — just text names. OpenUsage wins partly on recognizable logos.
- **Fix:** 20–24px monochrome glyphs in flyout header and connection rows.

---

## Spacing / system (from XAML)

| Token in code | Value | Issue |
|---------------|-------|--------|
| Flyout padding | 16 | OK |
| Card padding | 12 | OK |
| Settings content padding | 24 | OK |
| Sidebar width | 200 | Heavy for a 720 window; try 176–184 |
| Connection card radius | 10 | Fine |
| Flyout card radius | 8 | Inconsistent with Settings 10 — pick one (8) |
| Chart height | 140 fixed | Feels stubby; 160–180 with room for legend |
| Margins | 6/8/10/12 mixed | Standardize on **4/8/12/16** only |

**Symmetry:** Header title left / refreshed time right is good. Connection button column is right-aligned but button chrome is default WPF — optically heavier than the text column.

**Alignment:** Status strings wrap under titles with no max width → uneven card heights. Cap subtitle to 2 lines + tooltip.

---

## Copy / information density

| Location | Problem | Fix |
|----------|---------|-----|
| Connection status | Raw enums: `Subscription · Ok — You've hit…` | Humanize: `Pro · Connected` + separate warning banner |
| Remaining line | `$0.00 left ($20.00/$20.00)` when limit hit | Prefer `Limit reached · $20 of $20` |
| Pace line | Three numbers in one sentence | Chip: `+65% over pace` |
| Footer | Explains dashed line every open | Once in empty/help state only |
| General tab | Disabled controls shown | Hide until Phase 2 or label “Coming soon” section |

---

## Interaction audit

| Action | Current | Should be |
|--------|---------|-----------|
| Left-click tray | TrayPopup (fragile historically) | Popup + fallback menu item |
| Right-click | Refresh / Settings / Quit (+ Open usage) | Open usage first |
| Connect Cursor | MessageBox after refresh | Inline success state on the row; no modal |
| Monthly/Weekly | RadioButtons (WPF default) | Segmented control / pill toggle |
| Closing Settings | Hides (good) | Same; don’t Quit |

---

## Contrast with Mac peers (what to steal next)

From OpenUsage.ai / CodexBar patterns already in `PLAN.md`:

1. **Per-meter progress bars** with reset countdown on the same row  
2. **Menu bar / tray pins** (compact %)  
3. **Click-to-flip** used ⟷ left  
4. **Stale-while-revalidate** already present — surface “Updated Xm ago” more prominently  

Track should stay different with the **pace chart**, but bars + countdown belong above the fold too.

---

## Priority fix list

| P | Change | Effort |
|---|--------|--------|
| P0 | Reliable tray open path + visible tray label | S |
| P0 | Flyout content order: verdict → chart → details | S |
| P1 | Segmented Monthly/Weekly; status chips | S |
| P1 | Provider logos; humanized connection rows | M |
| P1 | Chart legend, axis units, over/under fill | M |
| P2 | Fluent polish (hover, focus, Mica/acrylic later) | M |
| P2 | Hide/disable Phase-2 dead controls | S |

---

## Lazyweb

Hosted improve-report from Settings mock (`docs/audit/settings.png`): job `664a8511-ce8b-4238-a8db-aaece0f76793` — URL filled in when generation completes.

**Quick comps (connected-accounts settings):** Close, Midday, Calendly — logo + title + subtitle + trailing status/CTA rows; denser than Track’s heavy cards; Coming-soon providers should not dominate the first viewport.
