using System.IO;
using DeadlockTimers.Core.Config;

namespace DeadlockTimers.App.Services;

public static class AppPaths
{
    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeadlockTimers");

    public static string AlertsPath => Path.Combine(ConfigDir, "alerts.json");

    public static string SettingsPath => Path.Combine(ConfigDir, "settings.json");

    public static void EnsureDefaults()
    {
        Directory.CreateDirectory(ConfigDir);

        foreach (var name in new[] { "alerts.json", "settings.json" })
        {
            var target = Path.Combine(ConfigDir, name);
            if (File.Exists(target)) continue;

            using var source = EmbeddedFiles.Open("Defaults." + name);
            using var file = File.Create(target);
            source.CopyTo(file);
        }
    }
}
