namespace DeadlockTimers.Core.Timing;

public interface ITimeSource
{
    TimeSpan Now { get; }
}
