using System.Text.RegularExpressions;

namespace DeadlockTimers.Core.Sync;

public static class ClockTextParser
{
    private static readonly Regex Pattern = new(@"(\d{1,2}):(\d{2})");

    /// Reads a "m:ss" clock from raw OCR text. Returns false if nothing usable is found.
    public static bool TryParse(string? raw, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (string.IsNullOrEmpty(raw)) return false;

        var normalized = raw
            .Replace(" ", "")
            .Replace("\t", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace('O', '0')
            .Replace('o', '0')
            .Replace('l', '1')
            .Replace('I', '1')
            .Replace('|', '1')
            .Replace('.', ':')
            .Replace(';', ':');

        var match = Pattern.Match(normalized);
        if (!match.Success) return false;

        int minutes = int.Parse(match.Groups[1].Value);
        int seconds = int.Parse(match.Groups[2].Value);
        if (seconds >= 60) return false;

        value = new TimeSpan(0, minutes, seconds);
        return true;
    }
}
