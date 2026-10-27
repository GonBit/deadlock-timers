using DeadlockTimers.Core.Timing;

namespace DeadlockTimers.Tests;

public class GameClockTests
{
    [Fact]
    public void Starts_Paused_At_Zero()
    {
        var clock = new GameClock(new FakeTimeSource());
        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
    }

    [Fact]
    public void Start_Then_Advance_Counts_Up()
    {
        var time = new FakeTimeSource();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.True(clock.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(5), clock.Elapsed);
    }

    [Fact]
    public void Pause_Holds_Value_While_Time_Advances()
    {
        var time = new FakeTimeSource();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromSeconds(5));
        clock.Pause();
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.False(clock.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(5), clock.Elapsed);
    }

    [Fact]
    public void Nudge_Back_Clamps_To_Zero()
    {
        var time = new FakeTimeSource();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromSeconds(5));
        clock.Nudge(TimeSpan.FromSeconds(-10));

        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
        Assert.True(clock.IsRunning);
    }

    [Fact]
    public void Set_While_Running_Then_Advance()
    {
        var time = new FakeTimeSource();
        var clock = new GameClock(time);

        clock.Start();
        clock.Set(TimeSpan.FromMinutes(2));
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(121), clock.Elapsed);
    }

    [Fact]
    public void Reset_Goes_To_Zero_And_Pauses()
    {
        var time = new FakeTimeSource();
        var clock = new GameClock(time);

        clock.Start();
        time.Advance(TimeSpan.FromSeconds(30));
        clock.Reset();
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.Zero, clock.Elapsed);
        Assert.False(clock.IsRunning);
    }
}
