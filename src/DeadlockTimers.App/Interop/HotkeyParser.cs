using System.Windows.Input;

namespace DeadlockTimers.App.Interop;

public static class HotkeyParser
{
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    /// Returns false if the combo text is invalid (e.g. "Ctrl+Shift+F9").
    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+');
        if (parts.Length == 0) return false;

        foreach (var raw in parts[..^1])
        {
            var part = raw.Trim();
            if (part.Length == 0) return false;

            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "win":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    return false;
            }
        }

        var keyPart = parts[^1].Trim();
        if (!Enum.TryParse(keyPart, true, out Key key)) return false;

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        modifiers |= MOD_NOREPEAT;
        return true;
    }
}
