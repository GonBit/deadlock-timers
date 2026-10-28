# Deadlock Timers

A small Windows overlay that tracks Deadlock objective timers and gives voice callouts.
Full implementation plan: [PLAN.md](PLAN.md).

## Run the exe

1. Run `publish.ps1` (needs the .NET 10 SDK) to produce `dist\DeadlockTimers.App.exe`.
2. Copy that single file anywhere and double-click it. No install, no .NET runtime needed.
3. Windows SmartScreen may warn about an unknown publisher because the exe is unsigned: More info, then Run anyway.

Default hotkeys (all `Ctrl+Shift+`): `F9` start/pause, `F10` reset, `F11`/`F12` nudge, `F8` edit mode, `F7` mute, `F6` toggle OCR, `F5` quit.

Config lives in `%APPDATA%\DeadlockTimers\` and is created on first run. OCR auto-sync needs the Windows English OCR language pack.

## Develop

- `src/DeadlockTimers.Core` - pure logic, testable on any OS (net10.0)
- `src/DeadlockTimers.App` - WPF shell (overlay, hotkeys, speech, OCR, OBS web source)
- `tests/DeadlockTimers.Tests` - xUnit tests for Core

```
dotnet build
dotnet test
dotnet run --project src/DeadlockTimers.App
```
