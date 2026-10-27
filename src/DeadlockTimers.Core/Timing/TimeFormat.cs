namespace DeadlockTimers.Core.Timing;

public static class TimeFormat
{
    /// Floor to whole seconds, "m:ss". Negative shows as "-0:05".
    public static string Clock(TimeSpan t)
    {
        bool negative = t < TimeSpan.Zero;
        var whole = TimeSpan.FromSeconds(Math.Floor(t.TotalSeconds));
        return Format(negative, whole);
    }

    /// Ceiling to whole seconds, same format.
    public static string Countdown(TimeSpan remaining)
    {
        bool negative = remaining < TimeSpan.Zero;
        var whole = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
        return Format(negative, whole);
    }

    private static string Format(bool negative, TimeSpan whole)
    {
        var sign = negative ? "-" : "";
        return $"{sign}{whole.Minutes}:{whole.Seconds:00}";
    }
}
