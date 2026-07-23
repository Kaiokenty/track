# Track

Windows 11 system-tray app for AI subscription + API usage — **recommended pace** charts (actual vs linear budget).

Primary stack: **Tauri 2 + React** (`apps/desktop`). Legacy WPF under `src/Track` is reference-only.

## Requirements

- Windows 10/11
- [Node.js 20+](https://nodejs.org/)
- [Rust](https://rustup.rs/) (stable)
- WebView2 Evergreen (Windows 11 usually has it)

## Run (Tauri)

```powershell
cd c:\Users\dev\Projects\track\apps\desktop
npm install
npm run tauri dev
```

Then:

1. Find tray icon (overflow chevron if hidden).
2. **Left-click** → flyout (pace chart + meters).
3. **Right-click → Open usage / Settings / Quit**.
4. First launch opens Settings once with a tray tip.

## Layout

```
apps/desktop/          # Tauri 2 + Vite React
  src/                 # React flyout + Settings (design-audit UX)
  src-tauri/           # Rust: tray, UsageService, Cursor adapter, SQLite
src/Track/             # Legacy WPF (dogfood / reference)
src/Track.Core/        # Legacy .NET core (ported to Rust)
docs/PLAN.md
docs/DESIGN_AUDIT.md
```

## Notes

- Cursor connect reads local `state.vscdb` (same unofficial approach as Mac OpenUsage).
- History DB: `%LOCALAPPDATA%\Track\history.db`
- Settings: `%LOCALAPPDATA%\Track\settings.json`
- Secrets (future Admin keys): Windows Credential Manager via `keyring`

## Legacy WPF

```powershell
dotnet run --project src\Track\Track.csproj
```

Prefer Tauri for new work. See `docs/PLAN.md` Phase 2 for Admin APIs, pins, hotkey, click-to-flip, Fluent polish.
