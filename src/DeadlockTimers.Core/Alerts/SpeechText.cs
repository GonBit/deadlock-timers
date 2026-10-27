namespace DeadlockTimers.Core.Alerts;

public static class SpeechText
{
    public static string Join(IReadOnlyList<string> parts)
    {
        if (parts.Count == 1) return parts[0];
        if (parts.Count == 2) return $"{parts[0]} and {parts[1]}";
        return string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
    }

    public static string For(AlertEvent e)
    {
        var speeches = e.Alerts.Select(a => a.Speech).ToList();
        if (e.Kind == TriggerKind.Warning)
            return $"{Join(speeches)} in {e.WarnSeconds}";
        return $"{Join(speeches)} now";
    }
}
