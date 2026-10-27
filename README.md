# Deadlock Timers

A small Windows overlay that tracks Deadlock objective timers and gives voice callouts.
Full implementation plan: [PLAN.md](PLAN.md).

## Status

- **Phase 0** — scaffold: done
- **Phase 1** — core timing/alert logic + tests: done
- Phase 2 (overlay + hotkeys) onward: not started

## Layout

- `src/DeadlockTimers.Core` — pure logic, testable on any OS (net10.0)
- `src/DeadlockTimers.App` — WPF shell (hotkeys, speech, OCR) — currently just the template
- `tests/DeadlockTimers.Tests` — xUnit tests for Core

## Build & test

```
dotnet build
dotnet test
```

Run the app on Windows (needs .NET 10 SDK): `dotnet run --project src/DeadlockTimers.App`.
Config lives in `%APPDATA%\DeadlockTimers\` (created on first run from `Defaults/`).
