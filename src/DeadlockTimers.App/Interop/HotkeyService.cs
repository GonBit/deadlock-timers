using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeadlockTimers.App.Interop;

public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public HotkeyService(Window window)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(WndProc);
    }

    /// Returns false if the text is invalid or the combo is already taken by another app.
    public bool Register(string combo, Action action)
    {
        if (!HotkeyParser.TryParse(combo, out var mods, out var vk)) return false;
        int id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, mods, vk)) return false;
        _actions[id] = action;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_hwnd, id);
        _source.RemoveHook(WndProc);
    }
}
