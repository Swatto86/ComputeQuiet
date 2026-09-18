# ComputeQuiet

Windows utility that parks background work so games, local AI, or other hungry processes can use the machine — then restores everything with one click.

![ComputeQuiet](Assets/ComputeQuiet.png)

## Features

- **GO QUIET / RESTORE** toggle (WPF UI, DPI-aware)
- Stops noisy services (`SysMain`, `WSearch`, `DiagTrack`, …) and suspends known background hogs
- Optional **Aggressive** mode for the whole user session (shell/OS kept; foreground app skipped)
- **High Performance** power-plan switch while quiet (restored on exit)
- **System tray** with Go Quiet / Restore / Exit; close can minimize to tray
- **Start with Windows** (launches minimized to tray, elevated)
- Keep-alive list for processes that must stay running
- State in `%LocalAppData%\ComputeQuiet\`

Requires **Administrator** (UAC) for services, process suspend, and power plans.

## Build (Windows)

```powershell
winget install Microsoft.DotNet.SDK.8
cd ComputeQuiet
.\build.ps1
```

Run `.\publish\ComputeQuiet.exe` and accept the UAC prompt.

## Tests

```powershell
dotnet run --project ComputeQuiet.Tests
```

## Usage tips

1. Prefer balanced mode day-to-day; use Aggressive before a game/local AI run.
2. Enable **Start with Windows** if you want tray access after login.
3. Always **RESTORE** when finished (or use the tray menu).
