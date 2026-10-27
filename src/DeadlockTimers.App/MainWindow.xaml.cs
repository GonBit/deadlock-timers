using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DeadlockTimers.Core.Alerts;
using DeadlockTimers.Core.Config;
using DeadlockTimers.Core.Models;
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

        if (failed.Count > 0) _statusExtra = "Hotkey in use: " + string.Join(", ", failed);

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
        if (_editMode) return "EDIT MODE — drag to move. Config: " + AppPaths.ConfigDir;

        var parts = new List<string>();
        if (!_clock.IsRunning) parts.Add("PAUSED");
        if (_speech is null || _speech.Muted) parts.Add("MUTED");
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

    private void ToggleEditMode()
    {
        _editMode = !_editMode;

        if (_editMode)
        {
            OverlayInterop.SetClickThrough(this, false);
            Panel.BorderThickness = new Thickness(2);
            Panel.BorderBrush = WarningBrush;
        }
        else
        {
            OverlayInterop.SetClickThrough(this, true);
            Panel.BorderThickness = new Thickness(0);
            Panel.BorderBrush = Brushes.Transparent;

            _settings.Overlay.Left = Left;
            _settings.Overlay.Top = Top;
            ConfigLoader.SaveSettings(AppPaths.SettingsPath, _settings);
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_editMode) DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _tick.Stop();
        _hotkeys?.Dispose();
        _speech?.Dispose();
        base.OnClosed(e);
    }
}