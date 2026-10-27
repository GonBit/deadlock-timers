using DeadlockTimers.Core.Sync;

namespace DeadlockTimers.Tests;

public class ClockSyncTests
{
    private static TimeSpan T(int minutes, int seconds) => new TimeSpan(0, minutes, seconds);

    [Fact]
    public void First_Alone_Reading_Returns_None()
    {
        var sync = new ClockSync();
        Assert.Equal(SyncAction.None, sync.Process(T(0, 1), TimeSpan.Zero, false));
    }

    [Fact]
    public void Stopped_Clock_Two_Plausible_Readings_Starts()
    {
        var sync = new ClockSync();
        sync.Process(T(0, 1), TimeSpan.Zero, false);

        var action = sync.Process(T(0, 2), TimeSpan.Zero, false);
        Assert.Equal(SyncActionKind.StartAt, action.Kind);
        Assert.Equal(T(0, 2) + TimeSpan.FromSeconds(0.5), action.Value);
    }

    [Fact]
    public void Running_Within_Tolerance_Returns_None()
    {
        var sync = new ClockSync();
        sync.Process(T(5, 0), T(5, 0).Add(TimeSpan.FromSeconds(0.2)), true);

        Assert.Equal(SyncAction.None, sync.Process(T(5, 1), T(5, 1).Add(TimeSpan.FromSeconds(0.2)), true));
    }

    [Fact]
    public void Running_Drift_Beyond_Tolerance_Sets_To_Reading()
    {
        var sync = new ClockSync();
        sync.Process(T(5, 10), T(5, 0), true);

        var action = sync.Process(T(5, 11), T(5, 1), true);
        Assert.Equal(SyncActionKind.SetTo, action.Kind);
        Assert.Equal(T(5, 11) + TimeSpan.FromSeconds(0.5), action.Value);
    }

    [Fact]
    public void Unconfirmed_Jump_Returns_None_And_Then_Normal_Rules_Apply()
    {
        var sync = new ClockSync();
        sync.Process(T(5, 10), T(5, 11), true);

        // 5:10 -> 2:00 is a backwards jump: unconfirmed, returns None.
        Assert.Equal(SyncAction.None, sync.Process(T(2, 0), T(5, 12), true));

        // 2:00 -> 2:01 is confirmed; internal clock agrees, so just None.
        Assert.Equal(SyncAction.None, sync.Process(T(2, 1), T(2, 2), true));
    }

    [Fact]
    public void Same_Reading_Four_Times_Pauses_At_Third_Repeat()
    {
        var sync = new ClockSync();
        Assert.Equal(SyncAction.None, sync.Process(T(7, 30), T(7, 30), true));  // sets _prev
        Assert.Equal(SyncAction.None, sync.Process(T(7, 30), T(7, 30), true));  // sameCount 1
        Assert.Equal(SyncAction.None, sync.Process(T(7, 30), T(7, 30), true));  // sameCount 2
        Assert.Equal(SyncActionKind.Pause, sync.Process(T(7, 30), T(7, 30), true).Kind);  // sameCount 3
    }

    [Fact]
    public void Reading_Far_Behind_Internal_Clock_Means_New_Match()
    {
        var sync = new ClockSync();
        sync.Process(T(0, 5), T(25, 0), true);

        var action = sync.Process(T(0, 6), T(25, 1), true);
        Assert.Equal(SyncActionKind.NewMatch, action.Kind);
        Assert.Equal(T(0, 6) + TimeSpan.FromSeconds(0.5), action.Value);
    }

    [Fact]
    public void Null_Reading_Does_Not_Break_Next_Confirmation()
    {
        var sync = new ClockSync();
        sync.Process(T(0, 1), TimeSpan.Zero, false);

        Assert.Equal(SyncAction.None, sync.Process(null, TimeSpan.Zero, false));

        var action = sync.Process(T(0, 2), TimeSpan.Zero, false);
        Assert.Equal(SyncActionKind.StartAt, action.Kind);
        Assert.Equal(T(0, 2) + TimeSpan.FromSeconds(0.5), action.Value);
    }
}
