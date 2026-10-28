using System.Runtime.InteropServices;
using System.Windows;

namespace DeadlockTimers.App.Interop;

public static class ConsoleCloseWatcher
{
    private const uint ATTACH_PARENT_PROCESS = uint.MaxValue;
    private const uint CTRL_C_EVENT = 0;
    private const uint CTRL_BREAK_EVENT = 1;
    private const uint CTRL_CLOSE_EVENT = 2;

    private delegate bool HandlerRoutine(uint ctrlType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

    // Must stay referenced or the GC frees the native callback.
    private static HandlerRoutine? _handler;

    /// Shuts the app down when the launching console is closed or Ctrl+C is pressed. No-op without a parent console.
    public static void Start()
    {
        if (!AttachConsole(ATTACH_PARENT_PROCESS)) return;

        _handler = ctrlType =>
        {
            if (ctrlType is not (CTRL_C_EVENT or CTRL_BREAK_EVENT or CTRL_CLOSE_EVENT)) return false;
            Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
            return true;
        };
        SetConsoleCtrlHandler(_handler, true);
    }
}
