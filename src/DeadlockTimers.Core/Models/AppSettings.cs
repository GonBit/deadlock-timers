namespace DeadlockTimers.Core.Models;

public sealed class AppSettings
{
    public HotkeySettings Hotkeys { get; set; } = new();
    public int NudgeSeconds { get; set; } = 5;
    public OverlaySettings Overlay { get; set; } = new();
    public SpeechSettings Speech { get; set; } = new();
    public OcrSettings Ocr { get; set; } = new();
    public WebSettings Web { get; set; } = new();
}

public sealed class HotkeySettings
{
    public string StartPause { get; set; } = "Ctrl+Shift+F9";
    public string Reset { get; set; } = "Ctrl+Shift+F10";
    public string NudgeBack { get; set; } = "Ctrl+Shift+F11";
    public string NudgeForward { get; set; } = "Ctrl+Shift+F12";
    public string EditMode { get; set; } = "Ctrl+Shift+F8";
    public string Mute { get; set; } = "Ctrl+Shift+F7";
    public string ToggleOcr { get; set; } = "Ctrl+Shift+F6";
}

public sealed class OverlaySettings
{
    public double Left { get; set; } = 40;
    public double Top { get; set; } = 200;
    public double Opacity { get; set; } = 0.85;
    public int UpcomingCount { get; set; } = 3;
}

public sealed class SpeechSettings
{
    public bool Enabled { get; set; } = true;
    public bool AnnounceDue { get; set; } = true;
    public int Volume { get; set; } = 100;
    public int Rate { get; set; } = 0;
    public string Voice { get; set; } = "";
}

public sealed class OcrSettings
{
    public bool Enabled { get; set; } = false;
    public int IntervalMs { get; set; } = 1000;
    public RegionSettings Region { get; set; } = new();
    public bool SaveDebugImage { get; set; } = false;
}

public sealed class RegionSettings
{
    public int X { get; set; } = 0;
    public int Y { get; set; } = 0;
    public int Width { get; set; } = 0;
    public int Height { get; set; } = 0;
}

public sealed class WebSettings
{
    public bool Enabled { get; set; } = false;
    public int Port { get; set; } = 7777;
}
