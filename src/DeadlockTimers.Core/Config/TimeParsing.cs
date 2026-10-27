namespace DeadlockTimers.Core.Config;

public static class TimeParsing
{
    /// Parses "m:ss" or "mm:ss". Seconds must be 0-59. Throws FormatException otherwise.
    public static TimeSpan ParseMinSec(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Time string is empty");

        var parts = text.Split(':');
        if (parts.Length != 2)
            throw new FormatException($"Time '{text}' must be 'm:ss'");

        if (!int.TryParse(parts[0], out var minutes) || parts[0].Length > 2 || minutes < 0)
            throw new FormatException($"Minutes in '{text}' must be 1-2 digits");

        if (!int.TryParse(parts[1], out var seconds) || parts[1].Length != 2 || seconds < 0 || seconds > 59)
            throw new FormatException($"Seconds in '{text}' must be 0-59");

        return TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
    }
}
