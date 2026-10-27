namespace DeadlockTimers.Core.Models;

public sealed class AlertsFile
{
    public int DefaultWarnSeconds { get; set; } = 15;
    public List<AlertJson> Alerts { get; set; } = new();
}

public sealed class AlertJson
{
    public string Id { get; set; } = "";
    public string Time { get; set; } = "";
    public string Label { get; set; } = "";
    public string Speech { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int? WarnSeconds { get; set; }
}
