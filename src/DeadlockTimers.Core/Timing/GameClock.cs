namespace DeadlockTimers.Core.Timing;

public sealed class GameClock
{
    private readonly ITimeSource _time;
    private TimeSpan _offset;
    private TimeSpan? _startedAt;

    public GameClock(ITimeSource time)
    {
        _time = time;
    }

    public bool IsRunning => _startedAt is not null;

    public TimeSpan Elapsed =>
        _offset + (_startedAt is { } s ? _time.Now - s : TimeSpan.Zero);

    public void Start()
    {
        if (IsRunning) return;
        _startedAt = _time.Now;
    }

    public void Pause()
    {
        if (!IsRunning) return;
        _offset = Elapsed;
        _startedAt = null;
    }

    public void Toggle()
    {
        if (IsRunning) Pause();
        else Start();
    }

    public void Reset()
    {
        _offset = TimeSpan.Zero;
        _startedAt = null;
    }

    public void Nudge(TimeSpan delta)
    {
        var newElapsed = Elapsed + delta;
        if (newElapsed < TimeSpan.Zero) newElapsed = TimeSpan.Zero;
        Set(newElapsed);
    }

    public void Set(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        _offset = value;
        if (IsRunning) _startedAt = _time.Now;
    }
}
