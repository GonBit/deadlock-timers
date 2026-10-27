using DeadlockTimers.Core.Alerts;
using DeadlockTimers.Core.Config;
using DeadlockTimers.Core.Models;

namespace DeadlockTimers.Tests;

public class AlertSchedulerTests
{
    private const int DefaultWarn = 15;

    private static List<AlertDefinition> DefaultAlerts()
    {
        // The 11 default alerts, in file order (already ascending by time).
        (string id, string time, string label, string speech)[] rows =
        {
            ("jungle-boxes-03", "3:00", "Jungle boxes", "Jungle boxes"),
            ("bridge-05", "5:00", "Bridge buffs & towers", "Bridge buffs and towers"),
            ("jungle-boxes-06", "6:00", "Jungle boxes", "Jungle boxes"),
            ("sinners-08", "8:00", "Sinners", "Sinners"),
            ("jungle-boxes-09", "9:00", "Jungle boxes", "Jungle boxes"),
            ("rift-10", "10:00", "First rift", "First rift"),
            ("mid-boxes-10", "10:00", "Mid boxes", "Mid boxes"),
            ("jungle-boxes-12", "12:00", "Jungle boxes", "Jungle boxes"),
            ("mid-boxes-13", "13:00", "Mid boxes", "Mid boxes"),
            ("jungle-boxes-15", "15:00", "Jungle boxes", "Jungle boxes"),
            ("sinners-16", "16:00", "Sinners", "Sinners"),
        };

        return rows.Select(r => new AlertDefinition(
            r.id, TimeParsing.ParseMinSec(r.time), r.label, r.speech, true, null)).ToList();
    }

    // Walk up in 0.1 s steps so every crossing is detected by the normal path (no big jumps).
    private static void StepTo(AlertScheduler scheduler, double seconds)
    {
        for (int i = 1; i <= (int)Math.Round(seconds * 10); i++)
            scheduler.Update(TimeSpan.FromSeconds(i * 0.1));
    }

    [Fact]
    public void Small_Steps_Until_Just_Before_2_45_Fire_Nothing()
    {
        var scheduler = new AlertScheduler(DefaultAlerts(), DefaultWarn);
        var events = new List<AlertEvent>();

        for (int i = 0; i <= 1649; i++)   // 0:00 .. 2:44.9
            events.AddRange(scheduler.Update(TimeSpan.FromSeconds(i * 0.1)));

        Assert.Empty(events);
    }

    [Fact]
    public void Crossing_2_45_Fires_One_Warning_For_Jungle_Boxes()
    {
        var scheduler = new AlertScheduler(DefaultAlerts(), DefaultWarn);
        StepTo(scheduler, 164.9);

        var events = scheduler.Update(TimeSpan.FromSeconds(165));

        Assert.Single(events);
        Assert.Equal(TriggerKind.Warning, events[0].Kind);
        Assert.Equal(TimeSpan.FromSeconds(165), events[0].At);
        Assert.Single(events[0].Alerts);
        Assert.Equal("jungle-boxes-03", events[0].Alerts[0].Id);
        Assert.Equal(15, events[0].WarnSeconds);
    }

    [Fact]
    public void Crossing_3_00_Fires_One_Due_For_Jungle_Boxes()
    {
        var scheduler = new AlertScheduler(DefaultAlerts(), DefaultWarn);
        StepTo(scheduler, 179.9);

        var events = scheduler.Update(TimeSpan.FromSeconds(180));

        Assert.Single(events);
        Assert.Equal(TriggerKind.Due, events[0].Kind);
        Assert.Equal(TimeSpan.FromSeconds(180), events[0].At);
        Assert.Equal("jungle-boxes-03", events[0].Alerts[0].Id);
    }

    [Fact]
    public void Crossing_9_45_Fires_One_Warning_With_Both_Rift_And_Mid_Boxes()
    {
        var scheduler = new AlertScheduler(DefaultAlerts(), DefaultWarn);
        StepTo(scheduler, 584.9);

        var events = scheduler.Update(TimeSpan.FromSeconds(585));

        Assert.Single(events);
        Assert.Equal(TriggerKind.Warning, events[0].Kind);
        Assert.Equal(2, events[0].Alerts.Count);
        Assert.Contains("First rift", events[0].Alerts.Select(a => a.Label));
        Assert.Contains("Mid boxes", events[0].Alerts.Select(a => a.Label));
        Assert.Equal(15, events[0].WarnSeconds);
    }

    [Fact]
    public void Big_Jump_Fires_Nothing_And_Next_Small_Step_Fires_Nothing_Old()
    {
        var scheduler = new AlertScheduler(DefaultAlerts(), DefaultWarn);
        Assert.Empty(scheduler.Update(TimeSpan.Zero));

        Assert.Empty(scheduler.Update(TimeSpan.FromSeconds(2)));     // nothing crossed yet
        Assert.Empty(scheduler.Update(TimeSpan.FromMinutes(1)));
        Assert.Empty(scheduler.Update(TimeSpan.FromMinutes(4)));     // 3 minute jump -> big jump rule
        Assert.Empty(scheduler.Update(TimeSpan.FromSeconds(242)));   // small step: no burst of old callouts
    }

    [Fact]
    public void Going_Back_Then_Forward_Again_Repeats_The_Warning()
    {
        var scheduler = new AlertScheduler(DefaultAlerts(), DefaultWarn);
        Assert.Empty(scheduler.Update(TimeSpan.FromSeconds(190)));   // 3:10

        Assert.Empty(scheduler.Update(TimeSpan.FromSeconds(160)));   // clock went back to 2:40

        StepTo(scheduler, 164.9);                                     // stepping forward again, small steps
        var events = scheduler.Update(TimeSpan.FromSeconds(165));     // re-crossing 2:45

        Assert.Single(events);
        Assert.Equal(TriggerKind.Warning, events[0].Kind);
        Assert.Equal("jungle-boxes-03", events[0].Alerts[0].Id);
    }

    [Fact]
    public void Disabled_Alert_Never_Fires()
    {
        var alerts = DefaultAlerts().Select(a => a with { Enabled = false }).ToList();
        var scheduler = new AlertScheduler(alerts, DefaultWarn);

        Assert.Empty(scheduler.Update(TimeSpan.Zero));
        Assert.Empty(scheduler.Update(TimeSpan.FromSeconds(1)));

        StepTo(scheduler, 164.9);
        Assert.Empty(scheduler.Update(TimeSpan.FromSeconds(165)));   // would be a warning if enabled
        StepTo(scheduler, 179.9);
        Assert.Empty(scheduler.Update(TimeSpan.FromSeconds(180)));   // would be a due if enabled
    }

    [Fact]
    public void Negative_WarnTime_Is_Skipped_And_Per_Alert_Override_Is_Used()
    {
        var alerts = new List<AlertDefinition>
        {
            new("early", TimeSpan.FromSeconds(10), "Early", "Early", true, 20),  // warnAt = -10 -> skipped
            new("late", TimeSpan.FromSeconds(60), "Late", "Late", true, 20),     // warnAt = 0:40
        };
        var scheduler = new AlertScheduler(alerts, DefaultWarn);

        Assert.Empty(scheduler.Update(TimeSpan.Zero));
        var events = scheduler.Update(TimeSpan.FromSeconds(1));      // crossing -0:10 warn is skipped
        Assert.Empty(events);

        StepTo(scheduler, 9.9);
        events = scheduler.Update(TimeSpan.FromSeconds(10));         // due only, warning skipped
        Assert.Single(events);
        Assert.Equal(TriggerKind.Due, events[0].Kind);
        Assert.Equal("early", events[0].Alerts[0].Id);

        events = scheduler.Update(TimeSpan.FromSeconds(11));
        Assert.Empty(events);

        StepTo(scheduler, 39.9);
        events = scheduler.Update(TimeSpan.FromSeconds(40));         // crossing 0:40 warn for "late"
        Assert.Single(events);
        Assert.Equal(TriggerKind.Warning, events[0].Kind);
        Assert.Equal(20, events[0].WarnSeconds);
    }

    [Fact]
    public void GetUpcoming_At_9_30_Returns_Three_Rows()
    {
        var scheduler = new AlertScheduler(DefaultAlerts(), DefaultWarn);
        var upcoming = scheduler.GetUpcoming(TimeSpan.FromSeconds(570), 3);

        Assert.Equal(3, upcoming.Count);
        Assert.Equal("First rift", upcoming[0].Alert.Label);
        Assert.Equal(TimeSpan.FromSeconds(30), upcoming[0].Remaining);
        Assert.Equal("Mid boxes", upcoming[1].Alert.Label);
        Assert.Equal(TimeSpan.FromSeconds(30), upcoming[1].Remaining);
        Assert.Equal("Jungle boxes", upcoming[2].Alert.Label);
        Assert.Equal("jungle-boxes-12", upcoming[2].Alert.Id);
        Assert.Equal(TimeSpan.FromSeconds(150), upcoming[2].Remaining);
    }

    [Fact]
    public void LoadAlerts_Throws_ConfigException_On_Bad_Time()
    {
        var path = Path.Combine(Path.GetTempPath(), "deadlock_timers_bad_alerts.json");
        File.WriteAllText(path, "{ \"defaultWarnSeconds\": 15, \"alerts\": [ { \"id\": \"x\", \"time\": \"3:60\", \"label\": \"L\", \"speech\": \"S\" } ] }");

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.LoadAlerts(path, out _));
        Assert.Equal("Alert 'x' has invalid time '3:60'", ex.Message);
    }
}
