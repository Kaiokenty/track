# Track — Product Plan (Windows 11)

Working name: **Track**. A system-tray app that shows AI subscription + API usage at a glance, with **pace** charts (are you ahead or behind a steady burn?), not just bare percentages.

This document captures product intent, UX that can actually work, how we get data (subscriptions vs API keys), lessons from Mac/community apps, and build phases. It is a plan, not a visual mock.

**Why this exists:** Mac already has strong menu-bar trackers. Windows does not have an equivalent tray citizen. Track is the Windows answer — and we steal what works, then win on pace visualization.

---

## 1. Problem

People pay for Cursor, Claude, OpenAI, Grok, etc. and only discover they’ve burned the period when they hit a limit. Checking each provider’s website is annoying. Bars and “72% used” hide the useful question:

> Given where I am in this window, am I using **too fast** or **too slow**?

---

## 2. Product thesis

**Live in the Windows 11 notification area.** One left-click flyout = current state. Settings window = link accounts and configure alerts. Charts compare **actual usage** to a **recommended linear pace** for the active window.

### Recommended pace (core UX idea — our wedge)

```
recommended_%(now) = 100% × (elapsed_time / window_length)
```

| Window | How recommended is computed | Why |
|--------|----------------------------|-----|
| **Billing / monthly** (Cursor primary) | Elapsed ÷ days in billing period | Matches Cursor’s real cycle |
| **Weekly** | Elapsed ÷ 7 days | Steady daily budget (Mac apps treat this as first-class) |
| **Session / ~5-hour** | Elapsed ÷ ~5 hours | Same window Claude Code / Codex / Antigravity expose on Mac |

**Visual:** line (or area) chart — straight **recommended** diagonal vs **actual** cumulative curve. Under the line = under pace; over = over pace. One callout: “Day 10 of 30 · +12% above recommended.”

Mac apps mostly show **progress bars + pace badges**. We keep those for glanceability, but lead with the **pace chart** so Track isn’t a pale Windows clone.

---

## 3. Competitive landscape (Mac & community)

Research date: Jul 2026. These are the apps Windows users wish existed on PC.

### 3.1 Landscape map

| App | Platform | Form | Job |
|-----|----------|------|-----|
| **[OpenUsage.ai](https://www.openusage.ai/)** ([robinebers/openusage](https://github.com/robinebers/openusage)) | macOS 15+ | Native Swift menu bar | Glanceable subscription limits |
| **[OpenUsage.sh](https://openusage.sh/)** ([janekbaraniewski/openusage](https://github.com/janekbaraniewski/openusage)) | Terminal (local) | TUI + daemon | Deep spend/quota analytics across 30+ tools |
| **[CodexBar](https://github.com/steipete/codexbar)** | macOS (+ Linux CLI) | Menu bar | Multi-provider limits, reset countdowns, API spend charts |
| **[MyUsage](https://github.com/zchan0/MyUsage)** | macOS | Menu bar | Claude/Codex/Cursor + **multi-Mac** sync via your own folder |
| **[TokenBar](https://github.com/Nanako0129/TokenBar)** | macOS | Menu bar | Local session-log analytics + OAuth quota cards |
| Cursor usage VS Code extensions | Editor | Status bar | Cursor-only unofficial API |

**Windows gap:** No native tray equivalent of OpenUsage.ai / CodexBar. OpenUsage.sh can run in a Windows terminal but is not a taskbar flyout product. That gap is Track’s reason to exist.

### 3.2 What’s good in OpenUsage.ai (primary UX reference)

From product site + README — this is the closest “Mac version of Track”:

| Pattern | What they do | Steal for Track? |
|---------|--------------|------------------|
| **Menu bar home** | Always visible; popover on click | **Yes** — system tray + flyout |
| **Provider-grouped meters** | Codex / Claude / Cursor sections with plan label | **Yes** |
| **Session + Weekly side by side** | Claude/Codex: “Session 99% left · Resets in 4h 57m” + “Weekly 63% left” | **Yes** — matches our 5h + weekly |
| **Cursor split meters** | Total / Auto / API usage + Extra usage $ | **Yes** for Cursor adapter |
| **Reset countdowns** | Relative (“18d 23h”), not only calendar dates | **Yes** |
| **Pace indicators** | “Using too much too fast” style signals | **Yes** — we go further with charts |
| **Usage trend** | Cursor trend spark/section | **Yes** (Phase 1–2) |
| **Menu bar pins** | Pin up to 2 metrics per provider as text or mini-bars | **Yes** (tray tooltip / icon overlay) |
| **Customize** | Reorder providers, hide metrics, Always Visible vs On Demand | **Yes** (Phase 2) |
| **Click to flip** | used ⟷ left; countdown ⟷ exact reset | **Yes** (cheap delight) |
| **Stale-while-revalidate** | Show cache instantly; refresh in background (~5 min) | **Yes** — flyout must open fast |
| **Local credential reuse** | Keychain / auth files / app state — no extra login when possible | **Yes** |
| **Local HTTP API** | `127.0.0.1:6736` for other tools | **Later** (Phase 3+) |
| **Global shortcut** | Toggle popover from anywhere | **Yes** |
| **Plugin providers** | Add providers without rewriting the shell | **Yes** — adapter/plugin boundary |
| **Spend tiles** | Today / Yesterday / Last 30 days $ from local logs | **Phase 2** for API + CLI logs if we add Claude Code |

**What we should not copy blindly:** Progress-bar-only UI; Mac-only auth paths (Keychain → map to Windows Credential Manager + `%APPDATA%` paths); iCloud sync as MVP.

### 3.3 What’s good in OpenUsage.sh (analytics reference)

| Pattern | Steal for Track? |
|---------|------------------|
| Auto-detect installed tools + env API keys | **Yes** for setup (“We found Cursor”) |
| Daemon + SQLite history | **Yes** — we need history for pace charts |
| Model / rate-limit / burn-rate detail | **Selective** — in settings detail, not cluttering flyout |
| Statusline / tmux hooks | Skip for MVP; optional Windows terminal status later |
| 30+ provider breadth | Don’t chase on day one — Cursor + 1–2 APIs first |

Positioning: OpenUsage.sh = deep terminal analytics. Track = **tray glance + pace**. Different jobs; we can still learn their provider detection ideas.

### 3.4 What’s good in CodexBar

| Pattern | Steal for Track? |
|---------|------------------|
| Per-provider enable toggles in Settings | **Yes** |
| Merge mode (one tray icon + switcher) vs one icon per provider | **Consider** — Windows tray is crowded; **one icon + flyout switcher** is safer |
| Browser cookie **or** local auth fallbacks | **Yes** as advanced reconnect for Cursor |
| Inline spend charts for Admin/API providers | **Yes** — aligns with our API cost mode |
| Incident / status badges | Nice-to-have later |
| Bundled CLI for scripts | Later (like OpenUsage CLI) |

### 3.5 What’s good in MyUsage / TokenBar

| Pattern | Steal for Track? |
|---------|------------------|
| **Burn-rate projection** (“at this rate you hit the limit on…”) | **Yes** — next to pace callout |
| Multi-device sync via **user’s** folder (iCloud/Dropbox/Syncthing) | **Later** — map to OneDrive/Dropbox folder on Windows; no Track backend |
| Last-known value when refresh fails | **Yes** — never blank the flyout |
| Local session-log cost estimates | Phase 2+ if we support Claude Code / Codex CLIs |

### 3.6 What Track uniquely owns (don’t water this down)

1. **Windows-native tray** — the missing product.
2. **Recommended-pace charts** (actual vs linear budget), not only bars + a pace badge.
3. **Honest dual mode:** subscription limits (Cursor session) **and** Admin API cost tracking.
4. Clear copy about unofficial Cursor endpoints + manual fallback.

---

## 4. Surfaces (UX that will work)

Informed by OpenUsage.ai / CodexBar patterns, adapted for Win11.

### 4.1 System tray icon

- Always present while the app runs (option: start with Windows).
- **Pinned summary** (steal from OpenUsage pins): e.g. `C 41% · under` or mini remaining % — keep short; Windows tray tooltips are limited.
- Tooltip: one-line summary, e.g. `Cursor 41% · under pace · OpenAI $12/$50`.
- Left-click → **flyout**.
- Right-click → Refresh · Open settings · Quit.
- Optional **global hotkey** to toggle flyout (CodexBar / OpenUsage).
- Toast when crossing warn/critical thresholds or when **above recommended pace** by alert %.

### 4.2 Tray flyout (primary)

Compact panel (~320–400px wide). Not a second browser. **Open instantly on cached data** (stale-while-revalidate).

Layout (top → bottom):

1. **Header:** last refreshed · “Next update in Xm” · settings gear.
2. **Provider sections** (grouped like OpenUsage.ai): plan name + meters.
   - Per meter: label (Session / Weekly / Total / API), **% left or used** (click to flip), **Resets in …**
   - **Pace badge:** under / on track / over recommended.
3. **Pace chart** (our differentiator): for the focused meter/window — recommended line vs actual.
4. **Burn projection:** “At current rate, limit on Fri 6pm.”
5. Footer: empty-state CTA or “Open provider dashboard.”

Empty state: “Link Cursor or add an API key in Settings.”

**Cursor section should show** (when API returns them): Total · Auto · API · Extra/on-demand $ — same mental model as OpenUsage.ai’s Cursor card.

### 4.3 Settings window

Sidebar sections:

| Section | Job |
|---------|-----|
| **Connections** | Link / unlink providers; auto-detect installed tools |
| **Layout** | Reorder providers; show/hide meters; pin tray summary fields |
| **Budgets** | Optional monthly $ caps for API cost mode |
| **Alerts** | % of limit, % above recommended pace, reset-soon |
| **General** | Launch at startup, poll interval, hotkey, theme follow system |

**Connections row pattern:**

```
[Logo] Cursor          Linked · Ultra · resets in 18d    [Disconnect]
[Logo] OpenAI API      Admin key · cost tracking         [Edit] [Remove]
[Logo] Claude API      Not linked                        [Connect]
[Logo] Grok (xAI)      Not linked                        [Connect]
```

“Subscribe” only **deep-links** to official pricing. We do not sell their plans.

### 4.4 What we deliberately skip (for now)

- Track cloud account / our backend for credentials.
- Pixel-perfect marketing site as a blocker.
- Teams / multi-user admin.
- Chasing 30 providers before Cursor + API cost work.
- Scraping ChatGPT Plus / Claude.ai consumer chat / SuperGrok (no reliable public API).
- Multiple tray icons (one per provider) — Windows tray is hostile; one icon + flyout.

---

## 5. Two data modes (do not conflate)

| Mode | User mental model | Auth | What we chart |
|------|-------------------|------|----------------|
| **A. Subscription limits** | “My Cursor / Claude Code allowance” | Session / install login (not an API key) | Session, weekly, billing period |
| **B. API cost tracking** | “What my org keys are burning” | Admin / API keys | $ and tokens vs budget or calendar month |

Both use the **same pace chart**. Different adapters underneath. Mac apps already proved Mode A via local credentials; Mode B is where we lean on official Admin APIs (also present in CodexBar).

---

## 6. How we get the info (sync / link flows)

There is **no** single official library that syncs all subscriptions. Mac apps each implement **per-provider adapters**. We do the same.

### 6.1 Pattern stolen from Mac apps: reuse what’s already on the machine

| Source type | Examples (Mac) | Windows equivalent |
|-------------|----------------|--------------------|
| App SQLite / state DB | Cursor `state.vscdb` | Same paths under `%APPDATA%` / Cursor install |
| Auth JSON / CLI creds | `~/.claude`, `~/.codex` | `%USERPROFILE%\.claude`, etc. |
| OS secret store | Keychain | Windows Credential Manager / DPAPI |
| Browser cookies | Optional advanced | Optional advanced (WebView2 / manual paste) |
| API / Admin keys | User paste | User paste + Credential Manager |

**Connect flow UX (proven):** “We found Cursor — Connect” → one click. If not found → “Install/sign in to Cursor, then Retry” or paste Admin key for API mode.

### 6.2 Cursor — subscription-style (MVP priority)

| Step | What happens |
|------|----------------|
| 1 | User clicks **Connect Cursor** (or auto-detect offers it) |
| 2 | App finds local Cursor install + session token |
| 3 | Call unofficial dashboard usage endpoints with that token |
| 4 | Map to Total / Auto / API / on-demand + billing reset |
| 5 | If missing → guide sign-in to Cursor on this PC, then Retry |

**Likely endpoints (unofficial, shared with community apps):**

- `POST https://api2.cursor.sh/aiserver.v1.DashboardService/GetCurrentPeriodUsage`
- Legacy / alternate: `/auth/usage`, usage-summary / usage-events

**Risks:** Undocumented; breaks when Cursor changes. Mitigations: feature-detect fields, last-good cache, manual fallback, clear “needs reauth.”

### 6.3 OpenAI — API cost (official)

Admin API key → Usage + Costs APIs → daily buckets for charts + monthly pace vs budget.

Honest UI: **not** ChatGPT Plus quotas.

### 6.4 Anthropic / Claude

| Path | MVP? | Notes |
|------|------|-------|
| Admin API key (org cost) | Phase 2 | Official; same as plan |
| Claude Code local OAuth / session+weekly | Phase 2–3 | Mac apps already do this; Windows paths exist for CLI users |

Honest UI: claude.ai Pro chat ≠ Admin API.

### 6.5 xAI / Grok

API spend via key / console. SuperGrok chat: manual or open dashboard only.

### 6.6 Manual provider (always)

Name, window, limit, used, reset — survival mode when unofficial APIs break.

### 6.7 “Website-like” linking — what we actually mean

```
┌─────────────────┐     local token / key      ┌──────────────────┐
│  Settings UI    │ ─────────────────────────► │ Provider adapter │
│  Connect button │                            │ (HTTP + normalize)│
└────────┬────────┘                            └────────┬─────────┘
         │ open browser (help / pricing / console)       │
         ▼                                               ▼
┌─────────────────┐                            ┌──────────────────┐
│ Provider site   │  user creates admin key    │ Local cache      │
│ or Cursor app   │  or signs into Cursor      │ (SQLite)         │
└─────────────────┘                            └──────────────────┘
```

Browser is for **setup and help**, not continuous website scraping. Same philosophy as OpenUsage.ai / CodexBar.

---

## 7. Provider matrix (honest)

| Provider | Link type | Official? | Windows we pace | MVP? |
|----------|-----------|-----------|-----------------|------|
| Cursor | Local session / unofficial API | No | Billing month; Total/Auto/API; weekly derived; 5h if exposed | **Yes** |
| OpenAI API | Admin key | Yes | Month / budget + daily series | **Yes** (Phase 2) |
| Claude API | Admin key | Yes | Same | Phase 2 |
| Claude Code | Local OAuth / files | Semi | Session 5h + weekly | Phase 2–3 |
| Grok xAI API | API key | Partial | Month / budget | Phase 3 |
| ChatGPT Plus / Claude.ai Pro / SuperGrok | — | No | — | Out / manual |

---

## 8. Pace chart behavior (detail)

### Monthly (Cursor default)

- Recommended: line (start, 0%) → (end, 100%).
- Actual: cumulative % (from provider + our SQLite history).
- Callout: under / over recommended %.
- Burn projection: ETA to 100% at current rate (MyUsage/TokenBar pattern).

### Weekly

- Linear over 7 days (or provider’s weekly window when Claude Code–style data exists).
- Show beside session meter when both exist (OpenUsage.ai layout).

### Session / 5-hour

- Linear over the session window.
- Primary for Claude Code / Codex-style plans; secondary for Cursor if exposed.

### API cost mode

- $ vs recommended month (or custom budget).
- Optional Today / 30-day tiles (OpenUsage.ai spend tiles).

---

## 9. Architecture (sketch)

```
UI (WinUI flyout + settings)
        │
        ▼
UsageService (poll, stale-while-revalidate, alerts)
        │
        ├── IProviderAdapter[]   ← plugin-style boundary (OpenUsage pattern)
        │     CursorSessionAdapter
        │     OpenAIAdminAdapter
        │     AnthropicAdminAdapter
        │     ClaudeCodeAdapter (later)
        │     XaiApiAdapter (later)
        │     ManualAdapter
        │
        ├── CredentialStore (Windows Credential Manager / DPAPI)
        ├── SnapshotStore (SQLite history for charts)
        └── optional LocalHttpApi (later) — read-only limits for other tools
```

**Normalized snapshot:**

```text
ProviderSnapshot {
  id, displayName, planLabel?, mode: subscription | api_cost | manual
  meters: [{
    id, label                    // "Session", "Weekly", "Total", "API"
    kind: monthly | weekly | rolling_5h | custom
    start, end, resetsAt
    used, limit, remaining
    unit: percent | usd_cents | requests
    series?: [{ t, used }]
  }]
  extras?: [{ label, value }]    // "Extra usage $364"
  status: ok | needs_reauth | error | stale
  fetchedAt
}
```

---

## 10. Tech direction (Windows)

| Choice | Recommendation |
|--------|----------------|
| UI | **Phase 0 scaffold: WPF + H.NotifyIcon** (builds on stock .NET 8). WinUI 3 still optional later. |
| Tray | H.NotifyIcon.Wpf |
| Storage | SQLite history; Credential Manager for secrets (in-memory stub in scaffold) |
| Packaging | MSIX preferred; unpackaged OK early |
| Charts | LiveCharts2 (or similar) — line + area for pace (Phase 1) |
| Alt stack | **Tauri** if we want faster web UI iteration |

**Scaffold note (Jul 2026):** `Track.sln` → `src/Track` + `src/Track.Core`. WinUI workload wasn’t installed, so Phase 0 uses WPF. Core is UI-agnostic for a later WinUI move if desired.

---

## 11. Feature adoption checklist (Mac → Track)

| Priority | Feature | Source | Phase |
|----------|---------|--------|-------|
| P0 | Tray + popover / flyout | OpenUsage.ai, CodexBar | 0 |
| P0 | Stale-while-revalidate cache | OpenUsage.ai | 0–1 |
| P0 | Cursor local session connect | OpenUsage, extensions | 1 |
| P0 | Monthly pace chart vs recommended | **Track wedge** | 1 |
| P0 | Reset countdowns | OpenUsage.ai, CodexBar | 1 |
| P0 | Last-good on failed refresh | TokenBar | 1 |
| P1 | Session + Weekly meters where available | OpenUsage.ai, MyUsage | 1–2 |
| P1 | Cursor Total / Auto / API split | OpenUsage.ai | 1 |
| P1 | Burn-rate projection | MyUsage, TokenBar | 1 |
| P1 | Click flip used ⟷ left | OpenUsage.ai | 1 |
| P1 | Auto-detect installed tools | OpenUsage.sh | 1 |
| P2 | Tray pinned metrics | OpenUsage.ai | 2 |
| P2 | Customize order / hide meters | OpenUsage.ai | 2 |
| P2 | OpenAI + Anthropic Admin cost + charts | CodexBar, our plan | 2 |
| P2 | Global hotkey | OpenUsage.ai, CodexBar | 2 |
| P2 | Alerts before limit / over pace | All | 2 |
| P3 | Local HTTP API for other apps | OpenUsage.ai | 3 |
| P3 | Claude Code session/weekly | OpenUsage, MyUsage | 3 |
| P3 | Folder-based multi-PC sync | MyUsage → OneDrive | Later |
| P3 | CLI for scripts | OpenUsage, CodexBar | Later |
| — | 30+ providers day one | OpenUsage.sh | **No** |
| — | Multiple tray icons | CodexBar option | **No** (default) |

---

## 12. Build phases

### Phase 0 — Skeleton
- Tray icon, empty flyout, settings shell, start-with-Windows.
- Cache layer stub (instant open).

### Phase 1 — Cursor + pace (MVP)
- Auto-detect + Connect Cursor (local session).
- Cursor meters: Total (+ Auto/API if present).
- Monthly **pace chart** + under/over + burn projection.
- Weekly view (derived or native).
- Reset countdowns; last-good cache; manual fallback.
- Poll ~5 min (OpenUsage default).

### Phase 2 — API cost + Mac-parity UX polish
- OpenAI + Anthropic Admin adapters; $ pace vs budget.
- Tray pins; customize layout; global hotkey; alerts.
- Session/5h meter when data exists.
- Today / 30-day spend tiles where we have series.

### Phase 3 — Ecosystem
- Claude Code adapter; Grok/xAI; local HTTP API; Efficiency Mode.
- Optional browser-cookie reconnect; OneDrive folder sync experiment.

### Later
- Store listing, more providers, Enterprise Claude analytics, CLI.

---

## 13. Privacy & trust

- Local-first (same pitch as OpenUsage / MyUsage): no Track account required for MVP.
- Secrets never logged; never uploaded to our servers.
- Clear copy: Cursor uses **unofficial** endpoints; disconnect anytime.
- If we add a local HTTP API later: loopback-only, no credentials in responses (OpenUsage warning applies — browsers on the machine can hit loopback).

---

## 14. Open decisions

1. **App name** — keep “Track” or brand something else? (Avoid colliding with OpenUsage trademark if we ever imply affiliation.)
2. **Weekly definition** — provider window vs calendar week vs fair-share of month?
3. **WinUI vs Tauri** — native Win11 vs faster web UI (community OpenUsage has both philosophies).
4. **Default tray pin** — Cursor remaining % vs pace badge vs nothing until user pins?
5. **How aggressive on unofficial Cursor** — ship with strong manual fallback from day one? (**Yes**, recommended.)

---

## 15. Success criteria (MVP)

- Windows user who already uses OpenUsage on a Mac feels at home within 30 seconds — then notices the **pace chart**.
- Links Cursor without pasting an API key when Cursor is installed and logged in.
- Flyout opens on **cached** data in &lt;200ms; refresh does not blank UI.
- Shows reset countdown + under/over recommended for the billing window.
- Survives Cursor API breakage via last-good + manual entry.
- Feels like a Win11 tray citizen, not a browser wrapper.

---

## 16. Summary

| Decision | Choice |
|----------|--------|
| Why build | Mac has OpenUsage/CodexBar; **Windows does not** |
| Home surface | System tray flyout (+ optional pinned metrics) |
| Steal heavily | Meters, countdowns, pins, SWR cache, local creds, Cursor splits |
| Our wedge | **Recommended-pace charts** + honest API cost mode |
| Cursor window | Billing month first; session/weekly as data allows |
| Cursor auth | Local subscription session (Mac pattern) |
| OpenAI / Claude | Admin API keys for cost (Phase 2) |
| Provider scope | Few adapters done well &gt; 30 half-broken ones |
| Sync model | Local adapters; browser for setup only |
| Stack | WinUI 3 (default) or Tauri — decide before scaffold |

**Next step:** lock name + WinUI vs Tauri, then scaffold Phase 0.
