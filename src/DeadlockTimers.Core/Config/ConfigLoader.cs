using System.Text.Json;
using DeadlockTimers.Core.Models;

namespace DeadlockTimers.Core.Config;

public static class ConfigLoader
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<AlertDefinition> LoadAlerts(string path, out int defaultWarnSeconds)
    {
        var file = ReadJson<AlertsFile>(path);
        defaultWarnSeconds = file.DefaultWarnSeconds;

        var result = new List<AlertDefinition>();
        foreach (var alert in file.Alerts)
        {
            TimeSpan time;
            try
            {
                time = TimeParsing.ParseMinSec(alert.Time);
            }
            catch (FormatException)
            {
                throw new ConfigException($"Alert '{alert.Id}' has invalid time '{alert.Time}'");
            }

            result.Add(new AlertDefinition(alert.Id, time, alert.Label, alert.Speech, alert.Enabled, alert.WarnSeconds));
        }

        return result.OrderBy(a => a.Time).ToList();
    }

    public static AppSettings LoadSettings(string path) => ReadJson<AppSettings>(path);

    public static void SaveSettings(string path, AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
    }

    private static T ReadJson<T>(string path)
    {
        if (!File.Exists(path))
            throw new ConfigException($"Config file not found: {path}");

        try
        {
            var json = File.ReadAllText(path);
            var value = JsonSerializer.Deserialize<T>(json, Options);
            if (value is null)
                throw new ConfigException($"Config file is empty: {path}");
            return value;
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Invalid JSON in {path}: {ex.Message}");
        }
    }
}
