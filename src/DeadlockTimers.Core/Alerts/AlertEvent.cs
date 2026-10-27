using DeadlockTimers.Core.Models;

namespace DeadlockTimers.Core.Alerts;

public enum TriggerKind
{
    Warning,
    Due
}

public sealed record AlertEvent(TriggerKind Kind, TimeSpan At, int WarnSeconds, IReadOnlyList<AlertDefinition> Alerts);
