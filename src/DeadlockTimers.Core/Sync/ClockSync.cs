namespace DeadlockTimers.Core.Sync;

public sealed class ClockSync
{
    private readonly double _toleranceSeconds;
    private readonly int _pauseReads;
    private readonly double _newMatchBackSeconds;

    private TimeSpan? _prev;
    private int _sameCount;
    private bool _pausedBySync;

    public ClockSync(double toleranceSeconds = 2.0, int pauseReads = 3, double newMatchBackSeconds = 60.0)
    {
        _toleranceSeconds = toleranceSeconds;
        _pauseReads = pauseReads;
        _newMatchBackSeconds = newMatchBackSeconds;
    }

    public SyncAction Process(TimeSpan? reading, TimeSpan internalElapsed, bool internalRunning)
    {
        // 1. Unreadable (scoreboard open, menu, etc.) — do not change _prev.
        if (reading is null) return SyncAction.None;

        // 2. Need two readings to trust one.
        if (_prev is null)
        {
            _prev = reading;
            return SyncAction.None;
        }

        // 3. A reading is only trusted when it moves forward plausibly.
        var delta = reading.Value - _prev.Value;
        if (delta < TimeSpan.Zero || delta.TotalSeconds > 3)
        {
            _prev = reading;
            _sameCount = 0;
            return SyncAction.None;
        }

        // 4. Track consecutive identical readings (game clock frozen => paused).
        if (delta == TimeSpan.Zero) _sameCount++;
        else _sameCount = 0;
        _prev = reading;

        // 5. Enough frozen readings while our clock runs => the game clock is paused.
        if (_sameCount >= _pauseReads && internalRunning)
        {
            _pausedBySync = true;
            return new SyncAction(SyncActionKind.Pause, reading.Value);
        }

        // 6. Our clock stopped: a forward-moving reading means the match is running.
        if (!internalRunning)
        {
            if (delta > TimeSpan.Zero)
            {
                _pausedBySync = false;
                return new SyncAction(SyncActionKind.StartAt, MidSecond(reading.Value));
            }
            return SyncAction.None;
        }

        // 7. Reading far behind the internal clock => a new match started.
        if (reading.Value < internalElapsed - TimeSpan.FromSeconds(_newMatchBackSeconds))
            return new SyncAction(SyncActionKind.NewMatch, MidSecond(reading.Value));

        // 8. Drift beyond tolerance => correct the internal clock.
        if (Math.Abs((reading.Value - FloorSecond(internalElapsed)).TotalSeconds) >= _toleranceSeconds)
            return new SyncAction(SyncActionKind.SetTo, MidSecond(reading.Value));

        // 9. In agreement.
        return SyncAction.None;
    }

    // +0.5 s puts the internal clock in the middle of the displayed second.
    private static TimeSpan MidSecond(TimeSpan reading) => reading + TimeSpan.FromSeconds(0.5);

    private static TimeSpan FloorSecond(TimeSpan t) => TimeSpan.FromSeconds(Math.Floor(t.TotalSeconds));
}
