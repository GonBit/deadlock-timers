# Deadlock Timers

A small Windows overlay that tracks Deadlock objective timers and gives voice callouts.
Full implementation plan: [PLAN.md](PLAN.md).

## Run it

Double-click `run.cmd`. It builds the app on your PC (needs the .NET 10 SDK) and starts it.
Building locally avoids Windows Smart App Control blocking a downloaded, unsigned exe.

To make a standalone exe instead, run `publish.ps1` to get `dist\DeadlockTimers.App.exe`. It is unsigned, so Smart App Control may block it on other PCs.

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
