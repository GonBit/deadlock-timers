using DeadlockTimers.Core.Models;

namespace DeadlockTimers.Core.Alerts;

public sealed record UpcomingAlert(AlertDefinition Alert, TimeSpan Remaining);
