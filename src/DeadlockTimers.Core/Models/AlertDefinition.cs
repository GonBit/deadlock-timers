namespace DeadlockTimers.Core.Models;

public sealed record AlertDefinition(
    string Id,
    TimeSpan Time,
    string Label,
    string Speech,
    bool Enabled,
    int? WarnSeconds);
