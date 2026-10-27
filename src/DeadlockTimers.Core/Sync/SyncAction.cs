namespace DeadlockTimers.Core.Sync;

public enum SyncActionKind { None, StartAt, SetTo, Pause, ResumeAt, NewMatch }

public readonly record struct SyncAction(SyncActionKind Kind, TimeSpan Value)
{
    public static SyncAction None => new(SyncActionKind.None, TimeSpan.Zero);
}
