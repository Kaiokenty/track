# Track

Windows 11 system-tray app for AI subscription + API usage — with **recommended pace** charts (actual vs linear budget).

Mac already has [OpenUsage.ai](https://www.openusage.ai/) / [CodexBar](https://github.com/steipete/codexbar). Track is the Windows tray equivalent. See [`docs/PLAN.md`](docs/PLAN.md) for product plan and competitive research.

## Status

**Phase 0 scaffold** — builds and runs:

- Tray icon (left-click flyout, right-click menu)
- Settings window (Connections / General shells)
- Core models, pace calculator, provider adapter stubs
- Stale-while-revalidate `UsageService` + SQLite history store

**Not yet:** Cursor session sync, pace charts, Admin API keys (Phase 1–2).

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

> WinUI 3 was preferred in the plan but the Windows App SDK workload isn’t required for this scaffold. Phase 0 uses **WPF + H.NotifyIcon** so it builds on a stock .NET 8 install. Core logic is UI-agnostic if we move to WinUI later.

## Run

```powershell
cd c:\Users\dev\Projects\track
dotnet run --project src\Track\Track.csproj
```

Then:

1. Find the teal circle in the notification area (show hidden icons if needed).
2. **Left-click** → flyout with provider stubs.
3. **Right-click → Settings** → Connections.
4. **Quit** from the tray menu (closing Settings only hides it).

## Solution layout

```
Track.sln
docs/PLAN.md
src/
  Track.Core/          # models, pace, adapters, UsageService, SQLite
  Track/               # WPF tray + settings UI
```

| Piece | Path |
|-------|------|
| Pace math | `Track.Core/Pace/PaceCalculator.cs` |
| Cursor stub | `Track.Core/Adapters/CursorSessionAdapter.cs` |
| Poll + cache | `Track.Core/Services/UsageService.cs` |
| Tray | `Track/TrayController.cs` |
| Flyout | `Track/FlyoutWindow.xaml` |
| Settings | `Track/SettingsWindow.xaml` |

## Next (Phase 1)

1. Read Cursor token from local `state.vscdb` / auth store.
2. Call `GetCurrentPeriodUsage` (unofficial) and map Total / Auto / API meters.
3. Draw recommended-pace line chart in the flyout.
4. Wire Credential Manager for secrets; launch-at-startup.
