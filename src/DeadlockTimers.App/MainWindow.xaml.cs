using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DeadlockTimers.Core.Alerts;
using DeadlockTimers.Core.Config;
using DeadlockTimers.Core.Models;
using DeadlockTimers.Core.Sync;
using DeadlockTimers.Core.Timing;
using DeadlockTimers.App.Interop;
using DeadlockTimers.App.Services;

namespace DeadlockTimers.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly Brush NormalBrush = Brushes.White;
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));

    private AppSettings _settings = new();
    private readonly GameClock _clock = new(new StopwatchTimeSource());
    private AlertScheduler _scheduler = new(new List<AlertDefinition>(), 15);
    private int _defaultWarn;
    private HotkeyService? _hotkeys;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private SpeechService? _speech;
    private ScreenClockReader? _reader;
    private readonly ClockSync _sync = new();
    private CancellationTokenSource? _ocrCts;
    private string _lastOcrText = "";
    private RegionWindow? _regionWindow;
    private bool _editMode;
    private int _topmostCounter;
    private string _statusExtra = "";

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            AppPaths.EnsureDefaults();
            _settings = ConfigLoader.LoadSettings(AppPaths.SettingsPath);
            var alerts = ConfigLoader.LoadAlerts(AppPaths.AlertsPath, out int defaultWarn);
            _defaultWarn = defaultWarn;
            _scheduler = new AlertScheduler(alerts, _defaultWarn);
        }
        catch (ConfigException ex)
        {
            MessageBox.Show(ex.Message, "Deadlock Timers");
            Application.Current.Shutdown();
            return;
        }

        Left = _settings.Overlay.Left;
        Top = _settings.Overlay.Top;
        Opacity = _settings.Overlay.Opacity;

        if (_settings.Speech.Enabled)
            _speech = new SpeechService(_settings.Speech.Volume, _settings.Speech.Rate, _settings.Speech.Voice);

        _tick.Tick += OnTick;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        OverlayInterop.SetClickThrough(this, true);

        _hotkeys = new HotkeyService(this);
        var failed = new List<string>();

        TryRegister(_settings.Hotkeys.StartPause, () => _clock.Toggle());
        TryRegister(_settings.Hotkeys.Reset, () => _clock.Reset());
        TryRegister(_settings.Hotkeys.NudgeBack, () => _clock.Nudge(-TimeSpan.FromSeconds(_settings.NudgeSeconds)));
        TryRegister(_settings.Hotkeys.NudgeForward, () => _clock.Nudge(TimeSpan.FromSeconds(_settings.NudgeSeconds)));
        TryRegister(_settings.Hotkeys.EditMode, ToggleEditMode);
        TryRegister(_settings.Hotkeys.Mute, ToggleMute);
        TryRegister(_settings.Hotkeys.ToggleOcr, ToggleOcr);

        if (failed.Count > 0) _statusExtra = "Hotkey in use: " + string.Join(", ", failed);

        if (_settings.Ocr.Enabled) StartOcr();

        _tick.Start();
        OnTick(null, EventArgs.Empty);

        void TryRegister(string combo, Action action)
        {
            if (!_hotkeys.Register(combo, action)) failed.Add(combo);
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var events = _scheduler.Update(_clock.Elapsed);
        if (_speech is not null)
        {
            foreach (var evt in events)
            {
                if (evt.Kind == TriggerKind.Warning) _speech.Say(SpeechText.For(evt));
                else if (evt.Kind == TriggerKind.Due && _settings.Speech.AnnounceDue) _speech.Say(SpeechText.For(evt));
            }
        }

        var elapsed = _clock.Elapsed;

        ClockText.Text = TimeFormat.Clock(elapsed);
        StatusText.Text = BuildStatus();
        BuildUpcoming(_scheduler.GetUpcoming(elapsed, _settings.Overlay.UpcomingCount));

        if (++_topmostCounter >= 50)
        {
            _topmostCounter = 0;
            Topmost = false;
            Topmost = true;
        }
    }

    private string BuildStatus()
    {
        if (_editMode)
        {
            var edit = "EDIT MODE — drag to move. Config: " + AppPaths.ConfigDir;
            if (!string.IsNullOrEmpty(_lastOcrText)) edit += " · OCR: " + _lastOcrText;
            return edit;
        }

        var parts = new List<string>();
        if (!_clock.IsRunning) parts.Add("PAUSED");
        if (_speech is null || _speech.Muted) parts.Add("MUTED");
        parts.Add(_settings.Ocr.Enabled ? "OCR" : "OCR off");
        if (!string.IsNullOrEmpty(_statusExtra)) parts.Add(_statusExtra);
        return string.Join(" · ", parts);
    }

    private void ToggleMute()
    {
        if (_speech is null) return;
        _speech.Muted = !_speech.Muted;
        _speech.Say(_speech.Muted ? "Voice off" : "Voice on", force: true);
    }

    private void BuildUpcoming(IReadOnlyList<UpcomingAlert> upcoming)
    {
        UpcomingList.Items.Clear();

        if (upcoming.Count == 0)
        {
            UpcomingList.Items.Add(Row("No more alerts", "", NormalBrush));
            return;
        }

        foreach (var u in upcoming)
        {
            var brush = u.Remaining <= TimeSpan.FromSeconds(WarnSeconds(u.Alert)) ? WarningBrush : NormalBrush;
            UpcomingList.Items.Add(Row(u.Alert.Label, TimeFormat.Countdown(u.Remaining), brush));
        }
    }

    private int WarnSeconds(AlertDefinition alert) => alert.WarnSeconds ?? _defaultWarn;

    private static Grid Row(string label, string countdown, Brush brush)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new TextBlock { Text = label, Foreground = brush };
        Grid.SetColumn(labelBlock, 0);
        grid.Children.Add(labelBlock);

        var countdownBlock = new TextBlock
        {
            Text = countdown,
            Foreground = brush,
            FontFamily = new FontFamily("Consolas")
        };
        Grid.SetColumn(countdownBlock, 1);
        grid.Children.Add(countdownBlock);

        return grid;
    }

    private void StartOcr()
    {
        if (_settings.Ocr.Region.Width <= 0)
        {
            _statusExtra = "OCR: set region in edit mode";
            return;
        }

        if (_ocrCts is not null) return;

        try
        {
            _reader = new ScreenClockReader();
        }
        catch (InvalidOperationException ex)
        {
            _statusExtra = ex.Message;
            return;
        }

        _ocrCts = new CancellationTokenSource();
        var token = _ocrCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.Ocr.IntervalMs));
                while (await timer.WaitForNextTickAsync(token))
                {
                    string text;
                    try
                    {
                        var r = _settings.Ocr.Region;
                        string? debug = _settings.Ocr.SaveDebugImage
                            ? Path.Combine(Path.GetTempPath(), "deadlock_ocr_debug.png")
                            : null;
                        text = await _reader!.ReadAsync(r.X, r.Y, r.Width, r.Height, debug);
                    }
                    catch (Exception ex) { text = "ERR " + ex.Message; }
                    await Dispatcher.InvokeAsync(() => ApplyOcr(text));
                }
            }
            catch (OperationCanceledException) { /* loop ends when the token is cancelled */ }
        }, token);
    }

    private void StopOcr()
    {
        if (_ocrCts is null) return;
        _ocrCts.Cancel();
        _ocrCts.Dispose();
        _ocrCts = null;
    }

    private void ApplyOcr(string text)
    {
        _lastOcrText = text;
        TimeSpan? reading = ClockTextParser.TryParse(text, out var t) ? t : null;
        var action = _sync.Process(reading, _clock.Elapsed, _clock.IsRunning);
        switch (action.Kind)
        {
            case SyncActionKind.StartAt: _clock.Set(action.Value); _clock.Start(); break;
            case SyncActionKind.SetTo: _clock.Set(action.Value); break;
            case SyncActionKind.Pause: _clock.Pause(); break;
            case SyncActionKind.NewMatch: _clock.Reset(); _clock.Set(action.Value); _clock.Start(); break;
        }
    }

    private void ToggleOcr()
    {
        _settings.Ocr.Enabled = !_settings.Ocr.Enabled;
        ConfigLoader.SaveSettings(AppPaths.SettingsPath, _settings);

        if (_settings.Ocr.Enabled) StartOcr();
        else StopOcr();
    }

    private void ToggleEditMode()
    {
        _editMode = !_editMode;

        if (_editMode)
        {
            OverlayInterop.SetClickThrough(this, false);
            Panel.BorderThickness = new Thickness(2);
            Panel.BorderBrush = WarningBrush;
            OpenRegionWindow();
        }
        else
        {
            CloseRegionWindow();
            OverlayInterop.SetClickThrough(this, true);
            Panel.BorderThickness = new Thickness(0);
            Panel.BorderBrush = Brushes.Transparent;

            _settings.Overlay.Left = Left;
            _settings.Overlay.Top = Top;
            ConfigLoader.SaveSettings(AppPaths.SettingsPath, _settings);
        }
    }

    private void OpenRegionWindow()
    {
        _regionWindow = new RegionWindow();
        var region = _settings.Ocr.Region;
        if (region.Width > 0 && region.Height > 0 && MainMatrix() is { } m)
        {
            // physical pixels -> DIPs
            _regionWindow.Left = region.X / m.M11;
            _regionWindow.Top = region.Y / m.M22;
            _regionWindow.Width = region.Width / m.M11;
            _regionWindow.Height = region.Height / m.M22;
        }
        else
        {
            _regionWindow.Left = (SystemParameters.PrimaryScreenWidth - _regionWindow.Width) / 2;
            _regionWindow.Top = 40;
        }

        _regionWindow.Show();
    }

    private void CloseRegionWindow()
    {
        if (_regionWindow is null) return;

        var src = PresentationSource.FromVisual(_regionWindow);
        if (src?.CompositionTarget?.TransformToDevice is { } m)
        {
            // DIPs -> physical pixels
            _settings.Ocr.Region.X = (int)Math.Round(_regionWindow.Left * m.M11);
            _settings.Ocr.Region.Y = (int)Math.Round(_regionWindow.Top * m.M22);
            _settings.Ocr.Region.Width = (int)Math.Round(_regionWindow.ActualWidth * m.M11);
            _settings.Ocr.Region.Height = (int)Math.Round(_regionWindow.ActualHeight * m.M22);
        }

        _regionWindow.Close();
        _regionWindow = null;
    }

    private Matrix? MainMatrix() => PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_editMode) DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _tick.Stop();
        StopOcr();
        _regionWindow?.Close();
        _hotkeys?.Dispose();
        _speech?.Dispose();
        base.OnClosed(e);
    }
}