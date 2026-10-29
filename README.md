# Deadlock Timers

A small Windows overlay that tracks Deadlock objective timers and gives voice callouts.
Full implementation plan: [PLAN.md](PLAN.md).

## Run it

Double-click `run.cmd`. It builds the app on your PC and starts it. If the .NET 10 SDK isn't installed, it downloads a private copy into `.dotnet\` first (about 300 MB, needs internet, no admin rights).
Building locally avoids Windows Smart App Control blocking a downloaded, unsigned exe. The console window closes as soon as the app starts; quit with `Ctrl+Shift+F5`. Running `run.cmd` again restarts it.

### If Windows blocks it

If Smart App Control says "this type of file can be dangerous on the internet", the files still carry a downloaded-from-the-internet tag. Remove it from the whole folder in PowerShell, then run `run.cmd` again:

```powershell
cd path\to\deadlock-timers
Get-ChildItem -Recurse | Unblock-File
```

If you downloaded a zip, right-click the zip, choose Properties, tick Unblock, and extract it again.

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
