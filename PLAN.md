# Deadlock Timers — Implementation Plan

A small Windows overlay that tracks Deadlock objective timers and gives voice callouts.
Started by hotkey (v1), later auto-started and auto-synced by reading the in-game clock with OCR (v2).

---

## 0. Rules for the coding agent (read first, follow always)

1. **Work one phase at a time.** Only do the phase you were asked to do. When its "Done when" list is satisfied, stop and report: files changed, build result, test result, anything unclear.
2. **Do not start the next phase** until the user says so.
3. **Use the exact names in this plan** (projects, folders, files, classes, methods, JSON keys). Do not rename things.
4. **Do not add NuGet packages** that are not listed in this plan.
5. **Do not invent APIs.** Code snippets in this plan are known to be correct; copy them. If a build error points at an API you are unsure about, stop and report the exact error instead of guessing repeatedly.
6. **Keep files small** (aim for under 200 lines each). One class per file.
7. **Edit, don't rewrite.** When changing an existing file, change only the parts the step needs. Never delete code from an earlier phase unless a step says so.
8. **Build after every step**: `dotnet build`. Run tests when a phase has tests: `dotnet test`. Fix errors before moving on.
9. **No MVVM frameworks, no DI containers, no extra abstractions.** Plain classes and WPF code-behind are fine.
10. **Context budget:** you have about 160k tokens. To save context, only re-read the section of this plan for your current phase plus the files you are editing. Do not paste whole files back into the chat unless asked.
11. **If you run on Linux** (e.g. a container): the WPF project builds thanks to `EnableWindowsTargeting`, but you cannot run it. Run `dotnet build` and `dotnet test` only. The user will run the app on Windows.

---

## 1. What the app does

- Shows a small always-on-top, click-through overlay on top of Deadlock (game must be in **borderless windowed** mode).
- Overlay shows the match time (`m:ss`) and the next few upcoming alerts with countdowns.
- Speaks a warning a few seconds before each alert (default 15 s), e.g. *"Bridge buffs and towers in 15"*, and optionally *"Bridge buffs and towers now"* when it is due.
- Alerts that happen at the same time are spoken as one sentence: *"First rift and mid boxes in 15"*.
- Controlled by global hotkeys (work while the game has focus).
- All alerts and settings live in editable JSON files.
- Later: reads the in-game clock from the screen with Windows OCR to start and correct the timer automatically.

### Alerts (default `alerts.json`)

| Time  | Label                    | Spoken as                |
|-------|--------------------------|--------------------------|
| 3:00  | Jungle boxes             | Jungle boxes             |
| 5:00  | Bridge buffs & towers    | Bridge buffs and towers  |
| 6:00  | Jungle boxes             | Jungle boxes             |
| 8:00  | Sinners                  | Sinners                  |
| 9:00  | Jungle boxes             | Jungle boxes             |
| 10:00 | First rift               | First rift               |
| 10:00 | Mid boxes                | Mid boxes                |
| 12:00 | Jungle boxes             | Jungle boxes             |
| 13:00 | Mid boxes                | Mid boxes                |
| 15:00 | Jungle boxes             | Jungle boxes             |
| 16:00 | Sinners                  | Sinners                  |

("Bridge buffs & towers" = bridge buffs, sunken towers, bell towers. Jungle boxes respawn every 3 minutes.)

Every alert has an `enabled` flag now, so a UI toggle can be added later without changing the format.

---

## 2. Solution layout

```
deadlock-timers/
  PLAN.md
  DeadlockTimers.sln            (or .slnx — whatever `dotnet new sln` creates)
  src/
    DeadlockTimers.Core/        net10.0 class library — pure logic, NO WPF, NO Windows APIs
      DeadlockTimers.Core.csproj
      Models/
        AlertDefinition.cs
        AlertsFile.cs
        AppSettings.cs
      Config/
        ConfigLoader.cs
        ConfigException.cs
        TimeParsing.cs
      Timing/
        ITimeSource.cs
        StopwatchTimeSource.cs
        GameClock.cs
        TimeFormat.cs
      Alerts/
        AlertScheduler.cs
        AlertEvent.cs
        UpcomingAlert.cs
        SpeechText.cs
      Sync/                     (Phase 4)
        ClockTextParser.cs
        ClockSync.cs
        SyncAction.cs
    DeadlockTimers.App/         net10.0-windows WPF app — UI, hotkeys, speech, OCR capture
      DeadlockTimers.App.csproj
      app.manifest
      App.xaml / App.xaml.cs
      MainWindow.xaml / MainWindow.xaml.cs      (the overlay)
      RegionWindow.xaml / RegionWindow.xaml.cs  (Phase 5)
      Defaults/
        alerts.json
        settings.json
      Interop/
        OverlayInterop.cs
        HotkeyService.cs
        HotkeyParser.cs
      Services/
        AppPaths.cs
        SpeechService.cs
        ScreenClockReader.cs    (Phase 5)
        StateServer.cs          (Phase 6)
      Web/
        index.html              (Phase 6)
  tests/
    DeadlockTimers.Tests/       net10.0 xUnit — tests for Core only
      DeadlockTimers.Tests.csproj
      FakeTimeSource.cs
      TimeParsingTests.cs
      GameClockTests.cs
      AlertSchedulerTests.cs
      SpeechTextTests.cs
      ClockTextParserTests.cs   (Phase 4)
      ClockSyncTests.cs         (Phase 4)
```

**Why split Core from App:** all timing logic is testable on any OS (including Linux) without Windows. The App project is a thin shell.

---

## 3. Config files

Both files live in `%APPDATA%\DeadlockTimers\`. On first run the app copies `Defaults/alerts.json` and `Defaults/settings.json` there if they don't exist. The user edits the copies in `%APPDATA%`.

### `Defaults/alerts.json`

```json
{
  "defaultWarnSeconds": 15,
  "alerts": [
    { "id": "jungle-boxes-03", "time": "3:00",  "label": "Jungle boxes",          "speech": "Jungle boxes",            "enabled": true },
    { "id": "bridge-05",       "time": "5:00",  "label": "Bridge buffs & towers", "speech": "Bridge buffs and towers", "enabled": true },
    { "id": "jungle-boxes-06", "time": "6:00",  "label": "Jungle boxes",          "speech": "Jungle boxes",            "enabled": true },
    { "id": "sinners-08",      "time": "8:00",  "label": "Sinners",               "speech": "Sinners",                 "enabled": true },
    { "id": "jungle-boxes-09", "time": "9:00",  "label": "Jungle boxes",          "speech": "Jungle boxes",            "enabled": true },
    { "id": "rift-10",         "time": "10:00", "label": "First rift",            "speech": "First rift",              "enabled": true },
    { "id": "mid-boxes-10",    "time": "10:00", "label": "Mid boxes",             "speech": "Mid boxes",               "enabled": true },
    { "id": "jungle-boxes-12", "time": "12:00", "label": "Jungle boxes",          "speech": "Jungle boxes",            "enabled": true },
    { "id": "mid-boxes-13",    "time": "13:00", "label": "Mid boxes",             "speech": "Mid boxes",               "enabled": true },
    { "id": "jungle-boxes-15", "time": "15:00", "label": "Jungle boxes",          "speech": "Jungle boxes",            "enabled": true },
    { "id": "sinners-16",      "time": "16:00", "label": "Sinners",               "speech": "Sinners",                 "enabled": true }
  ]
}
```

Optional per-alert key: `"warnSeconds": 20` overrides `defaultWarnSeconds` for that alert.

### `Defaults/settings.json`

```json
{
  "hotkeys": {
    "startPause":  "Ctrl+Shift+F9",
    "reset":       "Ctrl+Shift+F10",
    "nudgeBack":   "Ctrl+Shift+F11",
    "nudgeForward":"Ctrl+Shift+F12",
    "editMode":    "Ctrl+Shift+F8",
    "mute":        "Ctrl+Shift+F7",
    "toggleOcr":   "Ctrl+Shift+F6"
  },
  "nudgeSeconds": 5,
  "overlay": {
    "left": 40,
    "top": 200,
    "opacity": 0.85,
    "upcomingCount": 3
  },
  "speech": {
    "enabled": true,
    "announceDue": true,
    "volume": 100,
    "rate": 0,
    "voice": ""
  },
  "ocr": {
    "enabled": false,
    "intervalMs": 1000,
    "region": { "x": 0, "y": 0, "width": 0, "height": 0 },
    "saveDebugImage": false
  },
  "web": {
    "enabled": false,
    "port": 7777
  }
}
```

---

## 4. Phases

Each phase lists: **Goal**, **Steps**, **Done when**.

---

### Phase 0 — Scaffold

**Goal:** empty solution with three projects that builds and runs one placeholder test.

**Steps**

1. In `deadlock-timers/` run:
   ```
   dotnet new sln -n DeadlockTimers
   dotnet new classlib -n DeadlockTimers.Core -o src/DeadlockTimers.Core -f net10.0
   dotnet new wpf -n DeadlockTimers.App -o src/DeadlockTimers.App
   dotnet new xunit -n DeadlockTimers.Tests -o tests/DeadlockTimers.Tests -f net10.0
   dotnet sln add src/DeadlockTimers.Core src/DeadlockTimers.App tests/DeadlockTimers.Tests
   dotnet add src/DeadlockTimers.App reference src/DeadlockTimers.Core
   dotnet add tests/DeadlockTimers.Tests reference src/DeadlockTimers.Core
   ```
2. Delete the template `Class1.cs` from Core.
3. Replace the `<PropertyGroup>` in `DeadlockTimers.App.csproj` with:
   ```xml
   <PropertyGroup>
     <OutputType>WinExe</OutputType>
     <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
     <UseWPF>true</UseWPF>
     <Nullable>enable</Nullable>
     <ImplicitUsings>enable</ImplicitUsings>
     <ApplicationManifest>app.manifest</ApplicationManifest>
     <EnableWindowsTargeting>true</EnableWindowsTargeting>
   </PropertyGroup>
   ```
   (The `10.0.19041.0` part is required later for Windows OCR. Set it now so the TFM never changes.)
4. Create `src/DeadlockTimers.App/app.manifest` (per-monitor DPI awareness so screen coordinates are real pixels):
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
     <assemblyIdentity version="1.0.0.0" name="DeadlockTimers.App"/>
     <application xmlns="urn:schemas-microsoft-com:asm.v3">
       <windowsSettings>
         <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
         <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
       </windowsSettings>
     </application>
   </assembly>
   ```
5. Create `Defaults/alerts.json` and `Defaults/settings.json` with the exact content from section 3. Add to the App csproj:
   ```xml
   <ItemGroup>
     <None Update="Defaults\*.json">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
     </None>
   </ItemGroup>
   ```
   If `None Update` does not pick them up, use `<None Include="Defaults\*.json">` instead.
6. Make sure Core and Tests csproj have `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.

**Done when**
- `dotnet build` succeeds for the whole solution.
- `dotnet test` runs (the template test passes).

---

### Phase 1 — Core logic + tests

**Goal:** all timing and alert logic in `DeadlockTimers.Core`, fully unit-tested. No UI.

#### 1.1 Models (`Models/`)

```csharp
// AlertDefinition.cs — the runtime form (time already parsed)
namespace DeadlockTimers.Core.Models;
public sealed record AlertDefinition(
    string Id,
    TimeSpan Time,
    string Label,
    string Speech,
    bool Enabled,
    int? WarnSeconds);
```

```csharp
// AlertsFile.cs — the JSON form
namespace DeadlockTimers.Core.Models;
public sealed class AlertsFile
{
    public int DefaultWarnSeconds { get; set; } = 15;
    public List<AlertJson> Alerts { get; set; } = new();
}
public sealed class AlertJson
{
    public string Id { get; set; } = "";
    public string Time { get; set; } = "";
    public string Label { get; set; } = "";
    public string Speech { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int? WarnSeconds { get; set; }
}
```
(Two classes in one file is fine here because they only describe JSON.)

`AppSettings.cs`: plain classes mirroring `settings.json` exactly: `AppSettings` with properties `Hotkeys` (`HotkeySettings`), `NudgeSeconds` (int), `Overlay` (`OverlaySettings`), `Speech` (`SpeechSettings`), `Ocr` (`OcrSettings` with nested `RegionSettings`), `Web` (`WebSettings`). Every property gets a default value equal to the default JSON. Mutable `{ get; set; }` properties (the app saves overlay position and OCR region back).

#### 1.2 Config (`Config/`)

- `ConfigException : Exception` — simple, message only.
- `TimeParsing.ParseMinSec(string text)` → `TimeSpan`. Accepts `"m:ss"` or `"mm:ss"`. Seconds must be 0–59. Throws `FormatException` otherwise.
- `ConfigLoader` (static):
  - `JsonSerializerOptions Options` = `new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`.
  - `IReadOnlyList<AlertDefinition> LoadAlerts(string path, out int defaultWarnSeconds)` — reads `AlertsFile`, converts each `AlertJson` to `AlertDefinition` using `TimeParsing.ParseMinSec`. On a bad time, throw `ConfigException($"Alert '{id}' has invalid time '{time}'")`. Return sorted by `Time`.
  - `AppSettings LoadSettings(string path)`.
  - `void SaveSettings(string path, AppSettings settings)`.
  - Use `System.Text.Json` only (built in, no package).

#### 1.3 Timing (`Timing/`)

```csharp
public interface ITimeSource { TimeSpan Now { get; } }

public sealed class StopwatchTimeSource : ITimeSource
{
    private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();
    public TimeSpan Now => _sw.Elapsed;
}
```

`GameClock`:
```csharp
public sealed class GameClock
{
    public GameClock(ITimeSource time);
    public bool IsRunning { get; }
    public TimeSpan Elapsed { get; }        // offset + (running ? time.Now - startedAt : 0)
    public void Start();                    // no-op if running
    public void Pause();                    // no-op if paused; folds running time into offset
    public void Toggle();
    public void Reset();                    // paused, Elapsed = 0
    public void Nudge(TimeSpan delta);      // Elapsed += delta, clamped to >= 0, keeps running state
    public void Set(TimeSpan value);        // Elapsed = value (clamped >= 0), keeps running state
}
```
Implementation hint: store `_offset` (TimeSpan) and `_startedAt` (TimeSpan?). `Elapsed = _offset + (_startedAt is { } s ? _time.Now - s : TimeSpan.Zero)`. `Set` = `_offset = value; if running _startedAt = _time.Now`.

`TimeFormat` (static):
- `string Clock(TimeSpan t)` → floor to whole seconds, `"m:ss"` (e.g. `"0:07"`, `"12:34"`). Negative → `"-0:05"`.
- `string Countdown(TimeSpan remaining)` → ceiling to whole seconds, same format (so it shows `0:01` until the moment it is due, then `0:00`).

#### 1.4 Alerts (`Alerts/`)

```csharp
public enum TriggerKind { Warning, Due }

public sealed record AlertEvent(TriggerKind Kind, TimeSpan At, int WarnSeconds, IReadOnlyList<AlertDefinition> Alerts);

public sealed record UpcomingAlert(AlertDefinition Alert, TimeSpan Remaining);
```

`AlertScheduler`:
```csharp
public sealed class AlertScheduler
{
    public AlertScheduler(IReadOnlyList<AlertDefinition> alerts, int defaultWarnSeconds, double maxStepSeconds = 2.0);
    public IReadOnlyList<AlertEvent> Update(TimeSpan elapsed);
    public IReadOnlyList<UpcomingAlert> GetUpcoming(TimeSpan elapsed, int count);
}
```

**Update algorithm (crossing detection — no per-alert flags):**
1. Keep `TimeSpan? _last`. If `_last` is null → set `_last = elapsed`, return empty.
2. If `elapsed < _last` (clock went back: reset or nudge back) → `_last = elapsed`, return empty. (Triggers after the new time are automatically "re-armed" because they will be crossed again.)
3. If `elapsed - _last > maxStepSeconds` (big jump forward: nudge or sync) → `_last = elapsed`, return empty. (Prevents a burst of old callouts.)
4. Otherwise, for each **enabled** alert build two trigger times: `warnAt = Time - warnSeconds` (warnSeconds = alert.WarnSeconds ?? defaultWarnSeconds) and `dueAt = Time`. A trigger fires if `_last < triggerTime && triggerTime <= elapsed`.
5. Group fired triggers by `(Kind, triggerTime)`; each group becomes one `AlertEvent` with all its alerts (keep alerts ordered by Id for stable output). Order events by `At`, Warnings before Due at equal times.
6. `_last = elapsed`, return the events.

Skip a warning whose `warnAt` is negative.

`GetUpcoming`: enabled alerts with `Time > elapsed`, ordered by `Time`, take `count`, `Remaining = Time - elapsed`. Alerts at the same time are separate rows.

`SpeechText` (static):
- `string Join(IReadOnlyList<string> parts)`: 1 → `"a"`; 2 → `"a and b"`; 3+ → `"a, b and c"`.
- `string For(AlertEvent e)`: Warning → `$"{Join(speeches)} in {e.WarnSeconds}"`; Due → `$"{Join(speeches)} now"`.
  (For a warning group all alerts share the same warnAt; use the warnSeconds of the first alert. Set `AlertEvent.WarnSeconds` when building the group.)

#### 1.5 Tests (`tests/DeadlockTimers.Tests/`)

`FakeTimeSource : ITimeSource` with `public TimeSpan Now { get; set; }` and `Advance(TimeSpan)`.

Write these tests (xUnit `[Fact]`, plain `Assert`):

- **TimeParsingTests:** `"3:00"`→3 min; `"10:00"`→10 min; `"0:45"`→45 s; `"3:60"` throws; `"abc"` throws.
- **GameClockTests:** starts paused at 0; Start + advance 5 s → 5 s; Pause holds the value while time advances; Nudge(-10 s) at 5 s → 0 (clamped); Set(2 min) while running then advance 1 s → 2:01; Reset → 0 and paused.
- **AlertSchedulerTests** (use the 11 default alerts, warn 15 s):
  - stepping 0.1 s at a time from 0:00 to 2:46 fires nothing; crossing 2:45 fires one Warning for Jungle boxes.
  - stepping across 3:00 fires one Due for Jungle boxes.
  - stepping across 9:45 fires ONE Warning event containing both "First rift" and "Mid boxes".
  - jumping from 1:00 to 4:00 in one call fires nothing (big jump rule), and the next small step fires nothing old.
  - going back from 3:10 to 2:40 and stepping forward again fires the 2:45 warning again.
  - a disabled alert never fires.
  - `GetUpcoming(9:30, 3)` returns First rift, Mid boxes, Jungle boxes (12:00) with 0:30, 0:30, 2:30.
- **SpeechTextTests:** Join of 1/2/3 items; warning text `"First rift and Mid boxes in 15"` (speech values as in JSON); due text `"Sinners now"`.

**Done when**
- `dotnet build` and `dotnet test` pass with all tests above.
- Core has no reference to WPF, `System.Windows`, or Windows-only APIs.

---

### Phase 2 — Overlay window + hotkeys (manual start)

**Goal:** the app runs on Windows, shows the overlay over the game, and is controlled by global hotkeys. No speech yet.

#### 2.1 `Services/AppPaths.cs` (static)
- `ConfigDir` = `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeadlockTimers")`.
- `AlertsPath`, `SettingsPath` inside `ConfigDir`.
- `EnsureDefaults()`: create `ConfigDir`; for each of `alerts.json`, `settings.json`, if missing copy from `Path.Combine(AppContext.BaseDirectory, "Defaults", name)`.

#### 2.2 `Interop/OverlayInterop.cs` — copy exactly

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeadlockTimers.App.Interop;

public static class OverlayInterop
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const long WS_EX_TOOLWINDOW  = 0x00000080;
    private const long WS_EX_LAYERED     = 0x00080000;
    private const long WS_EX_NOACTIVATE  = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// Call after the window handle exists (e.g. in SourceInitialized).
    public static void SetClickThrough(Window window, bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        style |= WS_EX_TOOLWINDOW | WS_EX_LAYERED;
        if (clickThrough) style |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
        else style &= ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
    }
}
```

#### 2.3 `Interop/HotkeyParser.cs`
- `static bool TryParse(string text, out uint modifiers, out uint virtualKey)`.
- Split on `+`, trim. Modifier names (case-insensitive): `Ctrl`/`Control` → 0x0002, `Shift` → 0x0004, `Alt` → 0x0001, `Win` → 0x0008. The last part is a WPF `System.Windows.Input.Key` name parsed with `Enum.TryParse<Key>(part, true, out var key)`; convert with `(uint)KeyInterop.VirtualKeyFromKey(key)`.
- Always add `MOD_NOREPEAT` = 0x4000 to modifiers.

#### 2.4 `Interop/HotkeyService.cs` — core of it, copy exactly

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeadlockTimers.App.Interop;

public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public HotkeyService(Window window)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
    }

    /// Returns false if the text is invalid or the combo is already taken by another app.
    public bool Register(string combo, Action action)
    {
        if (!HotkeyParser.TryParse(combo, out var mods, out var vk)) return false;
        int id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, mods, vk)) return false;
        _actions[id] = action;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_hwnd, id);
        _source.RemoveHook(WndProc);
    }
}
```

#### 2.5 `MainWindow.xaml`

Window attributes:
```
WindowStyle="None" AllowsTransparency="True" Background="Transparent"
Topmost="True" ShowInTaskbar="False" ResizeMode="NoResize"
SizeToContent="WidthAndHeight" ShowActivated="False"
```
Content: a `Border` named `Panel` (CornerRadius 6, Padding 10, Background `#CC101014`, BorderThickness 0) containing a `StackPanel`:
- `TextBlock` `ClockText` — FontSize 28, Bold, White, Consolas.
- `TextBlock` `StatusText` — FontSize 11, Gray (shows "PAUSED", "MUTED", "EDIT MODE", "OCR", hotkey errors).
- `ItemsControl` `UpcomingList` — each row is a `Grid` with label (left) and countdown (right, Consolas), FontSize 15. Build rows in code-behind (simplest): clear and add `Grid` children each tick, or reuse a fixed number of rows. Keep it simple.

Colors: row text White normally; Orange (`#FFA500`) when `Remaining <= warnSeconds`; LimeGreen when Remaining is 0 (just due, show for 3 s — optional).

#### 2.6 `MainWindow.xaml.cs` behavior

Fields: `AppSettings _settings`, `GameClock _clock`, `AlertScheduler _scheduler`, `int _defaultWarn`, `HotkeyService? _hotkeys`, `DispatcherTimer _tick`, `bool _editMode`, `int _topmostCounter`.

Constructor:
1. `AppPaths.EnsureDefaults()`; load settings and alerts with `ConfigLoader`. If loading throws `ConfigException`, show `MessageBox` with the message and `Application.Current.Shutdown()`.
2. `_clock = new GameClock(new StopwatchTimeSource())`, `_scheduler = new AlertScheduler(alerts, _defaultWarn)`.
3. `Left/Top/Opacity` from `settings.Overlay`.
4. `_tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) }; _tick.Tick += OnTick;`

`OnSourceInitialized` (override):
1. `OverlayInterop.SetClickThrough(this, true)`.
2. Create `HotkeyService`, register: startPause → `_clock.Toggle()`, reset → `_clock.Reset()`, nudgeBack → `_clock.Nudge(-nudgeSeconds)`, nudgeForward → `_clock.Nudge(+nudgeSeconds)`, editMode → `ToggleEditMode()`. (mute and toggleOcr are registered in later phases.) Collect any combos that failed and show them in `StatusText` as `"Hotkey in use: Ctrl+Shift+F9"`.
3. Start `_tick`.

`OnTick`:
1. `var events = _scheduler.Update(_clock.Elapsed);` (Phase 3 speaks them.)
2. Update `ClockText` with `TimeFormat.Clock`, `StatusText`, and upcoming rows from `_scheduler.GetUpcoming(elapsed, settings.Overlay.UpcomingCount)`. If nothing is upcoming, show one row "No more alerts".
3. Every ~5 s (`_topmostCounter` reaches 50) re-assert topmost: `Topmost = false; Topmost = true;` (games sometimes push the overlay behind).

`ToggleEditMode()`:
- Edit mode ON: `SetClickThrough(this, false)`, `Panel.BorderThickness = 2`, border brush Orange, StatusText shows `"EDIT MODE — drag to move. Config: {AppPaths.ConfigDir}"`. Allow dragging: handle `MouseLeftButtonDown` → `if (_editMode) DragMove();`.
- Edit mode OFF: `SetClickThrough(this, true)`, border 0, save `Left/Top` into `settings.Overlay` and `ConfigLoader.SaveSettings`.

`OnClosed`: dispose hotkeys, stop timer.

**Done when**
- Builds. On Windows: overlay appears top-left area, shows `0:00` and PAUSED.
- `Ctrl+Shift+F9` starts/pauses; `F10` resets; `F11/F12` nudge ±5 s; upcoming countdowns tick down; rows turn orange inside 15 s.
- Mouse clicks pass through the overlay to the game; in edit mode the overlay can be dragged and the position is remembered after restart.
- Works over Deadlock in borderless windowed mode with the game focused.

---

### Phase 3 — Voice callouts

**Goal:** speak each `AlertEvent`.

1. `dotnet add src/DeadlockTimers.App package System.Speech`
2. `Services/SpeechService.cs`:
   ```csharp
   using System.Speech.Synthesis;

   namespace DeadlockTimers.App.Services;

   public sealed class SpeechService : IDisposable
   {
       private readonly SpeechSynthesizer _synth = new();
       public bool Muted { get; set; }

       public SpeechService(int volume, int rate, string voice)
       {
           _synth.SetOutputToDefaultAudioDevice();
           _synth.Volume = Math.Clamp(volume, 0, 100);
           _synth.Rate = Math.Clamp(rate, -10, 10);
           if (!string.IsNullOrWhiteSpace(voice))
           {
               try { _synth.SelectVoice(voice); } catch (ArgumentException) { /* voice not installed; keep default */ }
           }
       }

       public void Say(string text, bool force = false)
       {
           if (Muted && !force) return;
           _synth.SpeakAsync(text);
       }

       public void Dispose() => _synth.Dispose();
   }
   ```
3. In `MainWindow`: create `SpeechService` from `settings.Speech` (skip creating if `Enabled` is false, and treat it as muted). In `OnTick`, for each event: if `Kind == Warning` → `Say(SpeechText.For(e))`; if `Kind == Due` and `settings.Speech.AnnounceDue` → `Say(SpeechText.For(e))`.
4. Register the `mute` hotkey: toggle `Muted`, then `Say(Muted ? "Voice off" : "Voice on", force: true)`. Show `MUTED` in `StatusText`.
5. Dispose `SpeechService` in `OnClosed`.

**Done when**
- At 2:45 you hear "Jungle boxes in 15", at 3:00 "Jungle boxes now".
- At 9:45 you hear one sentence: "First rift and Mid boxes in 15".
- Nudging forward past several alerts does not cause a burst of old callouts.
- Mute hotkey works.

**Note for the user:** speech goes to the default Windows audio device. If OBS captures desktop audio, viewers will hear it too.

---

### Phase 4 — OCR sync logic (Core, testable)

**Goal:** pure logic that turns OCR text into a time and decides how to adjust the clock. No screen capture yet.

#### 4.1 `Sync/ClockTextParser.cs`
- `static bool TryParse(string? raw, out TimeSpan value)`.
- Normalize: remove whitespace; replace `O`/`o` → `0`, `l`/`I`/`|` → `1`, `.` and `;` → `:`.
- Find the first match of regex `(\d{1,2}):(\d{2})`. Seconds must be < 60. Return minutes and seconds as a TimeSpan.

Tests (`ClockTextParserTests`): `"12:34"`→12:34; `"12.34"`→12:34; `"1 2:34"`→12:34; `"O5:O7"`→5:07; `"l2:34"`→12:34; `"abc"`→false; `"12:75"`→false; `""` and `null`→false.

#### 4.2 `Sync/SyncAction.cs`
```csharp
public enum SyncActionKind { None, StartAt, SetTo, Pause, ResumeAt, NewMatch }
public readonly record struct SyncAction(SyncActionKind Kind, TimeSpan Value)
{
    public static SyncAction None => new(SyncActionKind.None, TimeSpan.Zero);
}
```

#### 4.3 `Sync/ClockSync.cs`
```csharp
public sealed class ClockSync
{
    public ClockSync(double toleranceSeconds = 2.0, int pauseReads = 3, double newMatchBackSeconds = 60.0);
    public SyncAction Process(TimeSpan? reading, TimeSpan internalElapsed, bool internalRunning);
}
```
State: `TimeSpan? _prev` (last valid reading), `int _sameCount`, `bool _pausedBySync`.

Rules, in this order:
1. `reading` is null → return None (scoreboard open, menu, etc.). Do not change `_prev`.
2. If `_prev` is null → `_prev = reading`, return None (need two readings to trust one).
3. `delta = reading - _prev`. **Confirmed** if `0 <= delta <= 3 s`. If not confirmed → `_prev = reading`, `_sameCount = 0`, return None.
4. If `delta == 0` → `_sameCount++` else `_sameCount = 0`. `_prev = reading`.
5. If `_sameCount >= pauseReads` and `internalRunning` → `_pausedBySync = true`, return `Pause`.
6. If not `internalRunning`:
   - if `delta > 0` → `_pausedBySync = false`, return `StartAt(reading + 0.5 s)` (covers both auto-start and resume).
   - else return None.
7. Running: if `reading < internalElapsed - newMatchBackSeconds` → return `NewMatch(reading + 0.5 s)`.
8. Running: if `|reading - floor(internalElapsed)| >= toleranceSeconds` → return `SetTo(reading + 0.5 s)`.
9. Return None.

(`+0.5 s` puts the internal clock in the middle of the displayed second. `ResumeAt` is kept in the enum for clarity but rule 6 uses `StartAt` for both cases — that is fine.)

Tests (`ClockSyncTests`):
- first reading alone → None.
- clock stopped, readings 0:01 then 0:02 → StartAt(0:02.5).
- running at 5:00.2 internal, readings 5:00 then 5:01 → None (within tolerance).
- running at 5:00 internal, readings 5:10 then 5:11 → SetTo(5:11.5).
- readings 5:10, then 2:00 (unconfirmed jump) → None; then 2:01 → handled as running/stop case normally.
- running, same reading 4 times in a row (7:30 ×4) → Pause at the read where `_sameCount` reaches 3.
- running at 25:00, readings 0:05 then 0:06 → NewMatch(0:06.5).
- null reading → None and does not break the next confirmation.

**Done when** `dotnet test` passes including the new tests.

---

### Phase 5 — Screen capture + OCR + calibration (App)

**Goal:** read the in-game clock from the screen and apply `ClockSync` actions.

1. `dotnet add src/DeadlockTimers.App package System.Drawing.Common`
2. **Name clashes:** WPF and System.Drawing both have `Point`, `Color`, `Brush`, etc. In files that use System.Drawing, add `using Gdi = System.Drawing;` and `using Gdi2D = System.Drawing.Imaging;` and refer to `Gdi.Bitmap`, `Gdi.Graphics`, etc. Do NOT add `using System.Drawing;`.

#### 5.1 `Services/ScreenClockReader.cs`

```csharp
using System.Runtime.InteropServices.WindowsRuntime;   // AsBuffer()
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Gdi = System.Drawing;
using GdiImaging = System.Drawing.Imaging;

namespace DeadlockTimers.App.Services;

public sealed class ScreenClockReader
{
    private readonly OcrEngine _engine;

    public ScreenClockReader()
    {
        _engine = OcrEngine.TryCreateFromUserProfileLanguages()
                  ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"))
                  ?? throw new InvalidOperationException("Windows OCR is not available. Install an English language pack with OCR in Windows Settings.");
    }

    /// Captures the region (physical pixels), upscales 3x, runs OCR. Returns raw OCR text.
    public async Task<string> ReadAsync(int x, int y, int width, int height, string? debugPath = null)
    {
        using var shot = new Gdi.Bitmap(width, height, GdiImaging.PixelFormat.Format32bppArgb);
        using (var g = Gdi.Graphics.FromImage(shot))
            g.CopyFromScreen(x, y, 0, 0, new Gdi.Size(width, height));

        int scale = 3;
        using var big = new Gdi.Bitmap(width * scale, height * scale, GdiImaging.PixelFormat.Format32bppArgb);
        using (var g = Gdi.Graphics.FromImage(big))
        {
            g.InterpolationMode = Gdi.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(shot, 0, 0, big.Width, big.Height);
        }

        if (debugPath != null) big.Save(debugPath, GdiImaging.ImageFormat.Png);

        var rect = new Gdi.Rectangle(0, 0, big.Width, big.Height);
        var data = big.LockBits(rect, GdiImaging.ImageLockMode.ReadOnly, GdiImaging.PixelFormat.Format32bppArgb);
        byte[] pixels;
        try
        {
            pixels = new byte[data.Stride * data.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        }
        finally { big.UnlockBits(data); }

        // 32bppArgb in memory is B,G,R,A per pixel = Bgra8. Stride == width*4 for 32bpp.
        using var sb = SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(), BitmapPixelFormat.Bgra8, big.Width, big.Height, BitmapAlphaMode.Premultiplied);

        var result = await _engine.RecognizeAsync(sb);
        return result.Text;
    }
}
```
If `AsBuffer()` does not compile, report the exact error to the user — do not try random alternatives.

#### 5.2 OCR loop in `MainWindow`

- Fields: `ScreenClockReader? _reader`, `ClockSync _sync = new()`, `CancellationTokenSource? _ocrCts`, `string _lastOcrText = ""`.
- `StartOcr()`: if `settings.Ocr.Region.Width <= 0` show `"OCR: set region in edit mode"` and return. Create the reader (catch `InvalidOperationException` → show message in `StatusText`). Start a background loop:
  ```csharp
  _ocrCts = new CancellationTokenSource();
  var token = _ocrCts.Token;
  _ = Task.Run(async () =>
  {
      using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.Ocr.IntervalMs));
      while (await timer.WaitForNextTickAsync(token))
      {
          string text;
          try
          {
              var r = _settings.Ocr.Region;
              string? debug = _settings.Ocr.SaveDebugImage ? Path.Combine(Path.GetTempPath(), "deadlock_ocr_debug.png") : null;
              text = await _reader!.ReadAsync(r.X, r.Y, r.Width, r.Height, debug);
          }
          catch (Exception ex) { text = "ERR " + ex.Message; }
          await Dispatcher.InvokeAsync(() => ApplyOcr(text));
      }
  }, token);
  ```
  Catch `OperationCanceledException` around the loop body or ignore it — the loop ends when the token is cancelled.
- `StopOcr()`: cancel and dispose `_ocrCts`.
- `ApplyOcr(string text)` (runs on UI thread):
  ```csharp
  _lastOcrText = text;
  TimeSpan? reading = ClockTextParser.TryParse(text, out var t) ? t : null;
  var action = _sync.Process(reading, _clock.Elapsed, _clock.IsRunning);
  switch (action.Kind)
  {
      case SyncActionKind.StartAt:  _clock.Set(action.Value); _clock.Start(); break;
      case SyncActionKind.SetTo:    _clock.Set(action.Value); break;
      case SyncActionKind.Pause:    _clock.Pause(); break;
      case SyncActionKind.NewMatch: _clock.Reset(); _clock.Set(action.Value); _clock.Start(); break;
  }
  ```
  The scheduler's big-jump rule already prevents callout bursts after a sync.
- Register `toggleOcr` hotkey: flip `settings.Ocr.Enabled`, save settings, start/stop the loop. Show `OCR` / `OCR off` in `StatusText`. Start the loop on launch if `Enabled` is true.
- In edit mode, show `_lastOcrText` in `StatusText` so the user can see what OCR reads.

#### 5.3 `RegionWindow` (calibration)

- `RegionWindow.xaml`: `WindowStyle="None" AllowsTransparency="True" Background="#2200FF00" Topmost="True" ShowInTaskbar="False" ResizeMode="CanResizeWithGrip"`, a red `Border` (BorderThickness 2) filling it, and a small `TextBlock` "OCR region — cover the match clock". Drag with `MouseLeftButtonDown` → `DragMove()`.
- When edit mode turns ON: open `RegionWindow` at the saved region (convert pixels → DIPs) or at a default 120×40 near the top center of the primary screen.
- When edit mode turns OFF: read its bounds, convert DIPs → physical pixels, save to `settings.Ocr.Region`, close it.
- Conversion (copy exactly):
  ```csharp
  var src = PresentationSource.FromVisual(regionWindow);
  var m = src!.CompositionTarget!.TransformToDevice;   // DIP -> pixels
  int px = (int)Math.Round(regionWindow.Left * m.M11);
  int py = (int)Math.Round(regionWindow.Top * m.M22);
  int pw = (int)Math.Round(regionWindow.ActualWidth * m.M11);
  int ph = (int)Math.Round(regionWindow.ActualHeight * m.M22);
  ```
  Pixels → DIPs: divide by `m.M11` / `m.M22` (use the main window's `PresentationSource` before the region window exists).
- Tell the user: make the box tight around only the `mm:ss` digits, and don't place the overlay on top of the clock.

**Done when**
- In edit mode the green box can be moved/resized over the match clock; position persists.
- With OCR on and a match running, the overlay starts by itself within ~2 s of the clock appearing, and stays within 2 s of the game clock.
- Joining a match late sets the correct time automatically.
- With `saveDebugImage: true`, `%TEMP%\deadlock_ocr_debug.png` shows the clock digits clearly.

---

### Phase 6 — OBS browser source (optional)

**Goal:** viewers can see upcoming objectives on stream.

1. `Services/StateServer.cs` using `System.Net.HttpListener` on prefix `http://localhost:{port}/` (localhost prefixes need no admin rights).
   - `GET /` → serve `Web/index.html` (copy to output like the JSON defaults).
   - `GET /state` → JSON `{ "elapsed": "12:34", "running": true, "upcoming": [ { "label": "Mid boxes", "remaining": "0:26", "warning": true } ] }`.
   - The server reads a snapshot object that `MainWindow.OnTick` updates (protect it with a `lock`). Run the listener loop on a background task; stop it on close.
2. `Web/index.html`: transparent background, polls `/state` every 500 ms with `fetch`, renders the same rows as the overlay. Plain HTML/CSS/JS, no frameworks.
3. Start only if `settings.Web.Enabled`.

**Done when** adding `http://localhost:7777/` as an OBS Browser Source shows the timers updating live.

---

## 5. Later (not now — do not implement unless asked)

- UI to enable/disable individual alerts (the `enabled` flag already exists).
- Hotkey "boxes taken" that starts a 3-minute respawn countdown from that moment.
- Reload `alerts.json` without restarting (FileSystemWatcher).
- Choose a specific audio output device for speech.
- Tray icon with Exit / Open config folder.

---

## 6. Manual test checklist for the user (Windows)

1. Run the app. Overlay shows `0:00 PAUSED`.
2. Start a bot match. When the in-game clock hits 0:00, press `Ctrl+Shift+F9`.
3. Check overlay matches the game clock; use `F11/F12` (with Ctrl+Shift) to nudge if needed.
4. Wait for 2:45 → hear "Jungle boxes in 15".
5. Press `Ctrl+Shift+F8`, drag the overlay somewhere better, press again to lock.
6. (Phase 5) In edit mode, put the green box over the clock, exit edit mode, press `Ctrl+Shift+F6` to enable OCR, start a new match without touching anything — timer should start on its own.
