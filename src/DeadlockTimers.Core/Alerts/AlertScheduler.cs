using DeadlockTimers.Core.Models;

namespace DeadlockTimers.Core.Alerts;

public sealed class AlertScheduler
{
    private readonly IReadOnlyList<AlertDefinition> _alerts;
    private readonly int _defaultWarnSeconds;
    private readonly double _maxStepSeconds;
    private TimeSpan? _last;

    public AlertScheduler(IReadOnlyList<AlertDefinition> alerts, int defaultWarnSeconds, double maxStepSeconds = 2.0)
    {
        _alerts = alerts;
        _defaultWarnSeconds = defaultWarnSeconds;
        _maxStepSeconds = maxStepSeconds;
    }

    public IReadOnlyList<AlertEvent> Update(TimeSpan elapsed)
    {
        if (_last is null)
        {
            _last = elapsed;
            return Array.Empty<AlertEvent>();
        }

        // Clock went backwards (reset or nudge back): re-arm by moving the baseline.
        if (elapsed < _last)
        {
            _last = elapsed;
            return Array.Empty<AlertEvent>();
        }

        // Big jump forward (nudge or sync): no burst of old callouts.
        if ((elapsed - _last.Value).TotalSeconds > _maxStepSeconds)
        {
            _last = elapsed;
            return Array.Empty<AlertEvent>();
        }

        var fired = new List<FiredTrigger>();
        foreach (var alert in _alerts)
        {
            if (!alert.Enabled) continue;
            int warnSeconds = alert.WarnSeconds ?? _defaultWarnSeconds;

            var warnAt = alert.Time - TimeSpan.FromSeconds(warnSeconds);
            if (warnAt >= TimeSpan.Zero && Crossed(warnAt, elapsed))
                fired.Add(new FiredTrigger(TriggerKind.Warning, warnAt, warnSeconds, alert));

            if (Crossed(alert.Time, elapsed))
                fired.Add(new FiredTrigger(TriggerKind.Due, alert.Time, warnSeconds, alert));
        }

        _last = elapsed;

        return fired
            .GroupBy(t => (t.Kind, t.At))
            .Select(g => new AlertEvent(
                g.Key.Kind,
                g.Key.At,
                g.First().WarnSeconds,
                g.Select(x => x.Alert).OrderBy(a => a.Id).ToList()))
            .OrderBy(e => e.At)
            .ThenBy(e => e.Kind)   // Warning before Due at equal times
            .ToList();
    }

    public IReadOnlyList<UpcomingAlert> GetUpcoming(TimeSpan elapsed, int count)
    {
        return _alerts
            .Where(a => a.Enabled && a.Time > elapsed)
            .OrderBy(a => a.Time)
            .Take(count)
            .Select(a => new UpcomingAlert(a, a.Time - elapsed))
            .ToList();
    }

    private bool Crossed(TimeSpan triggerTime, TimeSpan elapsed) => _last! < triggerTime && triggerTime <= elapsed;

    private sealed record FiredTrigger(TriggerKind Kind, TimeSpan At, int WarnSeconds, AlertDefinition Alert);
}
