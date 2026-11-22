using System.Runtime.InteropServices;

namespace DugoutDeck.Desktop;

/// <summary>
/// Real fullscreen on Windows: the window loses its frame and covers the whole monitor, taskbar included
/// (Photino's own fullscreen only fills the work area, so the taskbar stayed visible).
/// Leaving fullscreen restores the frame, size and position the window had before.
/// </summary>
internal sealed class Fullscreen(IntPtr hwnd)
{
    private const int GWL_STYLE = -16;
    private const long WS_OVERLAPPEDWINDOW = 0x00CF0000L;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOOWNERZORDER = 0x0200, SWP_FRAMECHANGED = 0x0020;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT { public int length; public int flags; public int showCmd; public POINT ptMinPosition; public POINT ptMaxPosition; public RECT rcNormalPosition; }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
    [DllImport("user32.dll")] private static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private WINDOWPLACEMENT _saved;
    public bool IsOn { get; private set; }

    public void Set(bool on)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero || on == IsOn) return;
        var style = (long)GetWindowLongPtr(hwnd, GWL_STYLE);
        if (on)
        {
            _saved = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
            GetWindowPlacement(hwnd, ref _saved);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi)) return;
            SetWindowLongPtr(hwnd, GWL_STYLE, (IntPtr)(style & ~WS_OVERLAPPEDWINDOW));
            var r = mi.rcMonitor;
            SetWindowPos(hwnd, IntPtr.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, SWP_NOOWNERZORDER | SWP_FRAMECHANGED);
        }
        else
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, (IntPtr)(style | WS_OVERLAPPEDWINDOW));
            SetWindowPlacement(hwnd, ref _saved);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOOWNERZORDER | SWP_FRAMECHANGED);
        }
        IsOn = on;
    }
}
