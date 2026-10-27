namespace DeadlockTimers.Core.Timing;

public sealed class StopwatchTimeSource : ITimeSource
{
    private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

    public TimeSpan Now => _sw.Elapsed;
}
