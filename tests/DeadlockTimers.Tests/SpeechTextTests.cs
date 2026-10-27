using DeadlockTimers.Core.Alerts;
using DeadlockTimers.Core.Models;

namespace DeadlockTimers.Tests;

public class SpeechTextTests
{
    [Fact]
    public void Join_Of_One()
    {
        Assert.Equal("Sinners", SpeechText.Join(new List<string> { "Sinners" }));
    }

    [Fact]
    public void Join_Of_Two()
    {
        Assert.Equal("First rift and Mid boxes",
            SpeechText.Join(new List<string> { "First rift", "Mid boxes" }));
    }

    [Fact]
    public void Join_Of_Three()
    {
        Assert.Equal("Jungle boxes, Mid boxes and Sinners",
            SpeechText.Join(new List<string> { "Jungle boxes", "Mid boxes", "Sinners" }));
    }

    [Fact]
    public void Warning_Text_For_Two_Alerts()
    {
        var alerts = new List<AlertDefinition>
        {
            new("rift-10", TimeSpan.FromMinutes(10), "First rift", "First rift", true, null),
            new("mid-boxes-10", TimeSpan.FromMinutes(10), "Mid boxes", "Mid boxes", true, null),
        };
        var e = new AlertEvent(TriggerKind.Warning, TimeSpan.FromSeconds(585), 15, alerts);

        Assert.Equal("First rift and Mid boxes in 15", SpeechText.For(e));
    }

    [Fact]
    public void Due_Text_For_One_Alert()
    {
        var alerts = new List<AlertDefinition>
        {
            new("sinners-08", TimeSpan.FromMinutes(8), "Sinners", "Sinners", true, null),
        };
        var e = new AlertEvent(TriggerKind.Due, TimeSpan.FromMinutes(8), 15, alerts);

        Assert.Equal("Sinners now", SpeechText.For(e));
    }
}
