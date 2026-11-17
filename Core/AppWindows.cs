using System.Runtime.InteropServices;
using System.Text;
using static WinPla.NativeMethods;

namespace WinPla;

public static class AppWindows
{
    /// <summary>Visible top-level application windows (includes minimized ones; excludes tool windows, owned windows without WS_EX_APPWINDOW, cloaked windows and WinPla's own).</summary>
    public static IReadOnlyList<WindowInfo> GetAll()
    {
        var list = new List<WindowInfo>();
        var paths = new Dictionary<uint, string?>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            int exStyle = GetWindowLong(hwnd, -20 /* GWL_EXSTYLE */);
            if ((exStyle & 0x80 /* WS_EX_TOOLWINDOW */) != 0) return true;
            if (GetWindow(hwnd, 4 /* GW_OWNER */) != 0 && (exStyle & 0x40000 /* WS_EX_APPWINDOW */) == 0) return true;
            if (DwmGetWindowAttribute(hwnd, 14 /* DWMWA_CLOAKED */, out int cloaked, 4) == 0 && cloaked != 0) return true;

            int len = GetWindowTextLength(hwnd);
            if (len == 0) return true;
            var sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            if (sb.Length == 0) return true;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == Environment.ProcessId) return true;
            if (!paths.TryGetValue(pid, out var path)) paths[pid] = path = GetExePath(pid);
            if (path is null) return true;

            list.Add(new WindowInfo(hwnd, sb.ToString(), System.IO.Path.GetFileName(path), path));
            return true;
        }, 0);
        return list;
    }

    /// <summary>Restored ("normal") rect of the window in virtual-screen coordinates; null if the handle is no longer valid.</summary>
    public static RectPx? GetRect(nint hwnd)
    {
        var oldDpi = SetThreadDpiAwarenessContext(PER_MONITOR_AWARE_V2);
        try
        {
            var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
            if (!IsWindow(hwnd) || !GetWindowPlacement(hwnd, ref wp)) return null;
            return FromWorkspace(wp.rcNormalPosition);
        }
        finally { SetThreadDpiAwarenessContext(oldDpi); }
    }

    /// <summary>rcNormalPosition read from a window, converted to virtual-screen coordinates with the offset of the monitor it is on.</summary>
    internal static RectPx FromWorkspace(RECT r)
    {
        var (dx, dy) = (0, 0);
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (GetMonitorInfo(MonitorFromRect(ref r, 2 /* MONITOR_DEFAULTTONEAREST */), ref mi))
            (dx, dy) = WorkspaceOffset(Monitors.ToRect(mi.rcMonitor), Monitors.ToRect(mi.rcWork));
        return new RectPx(r.Left + dx, r.Top + dy, r.Right - r.Left, r.Bottom - r.Top);
    }

    /// <summary>WINDOWPLACEMENT is in "workspace" coordinates: shifted by how far the work area of the window's monitor starts
    /// from that monitor's corner (a taskbar at its top/left). Same conversion as ReactOS's IntGetWindowPlacement and Chromium.</summary>
    internal static (int dx, int dy) WorkspaceOffset(RectPx bounds, RectPx workArea) => (workArea.X - bounds.X, workArea.Y - bounds.Y);

    static string? GetExePath(uint pid)
    {
        var h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (h == 0) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }
}
