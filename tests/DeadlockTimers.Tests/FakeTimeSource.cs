using DeadlockTimers.Core.Timing;

namespace DeadlockTimers.Tests;

public sealed class FakeTimeSource : ITimeSource
{
    public TimeSpan Now { get; set; }

    public void Advance(TimeSpan delta) => Now += delta;
}
