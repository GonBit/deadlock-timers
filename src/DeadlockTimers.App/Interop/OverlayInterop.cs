using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeadlockTimers.App.Interop;

public static class OverlayInterop
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_LAYERED = 0x00080000;
    private const long WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// Call after the window handle exists (e.g. in SourceInitialized).
    public static void SetClickThrough(Window window, bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        style |= WS_EX_TOOLWINDOW | WS_EX_LAYERED;
        if (clickThrough) style |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
        else style &= ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
    }
}
