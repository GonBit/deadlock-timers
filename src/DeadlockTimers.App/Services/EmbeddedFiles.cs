using System.IO;
using System.Reflection;

namespace DeadlockTimers.App.Services;

public static class EmbeddedFiles
{
    public static Stream Open(string logicalName) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName)
        ?? throw new FileNotFoundException("Embedded resource not found: " + logicalName);
}
