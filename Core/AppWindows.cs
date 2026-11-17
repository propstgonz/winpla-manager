using System.Runtime.InteropServices;
using System.Text;
using static WinPla.NativeMethods;

namespace WinPla;

public static class AppWindows
{
    /// <summary>Visible top-level application windows (includes minimized ones; excludes tool windows, owned windows, cloaked windows and WinPla's own).</summary>
    public static IReadOnlyList<WindowInfo> GetAll()
    {
        var list = new List<WindowInfo>();
        var paths = new Dictionary<uint, string?>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, 4 /* GW_OWNER */) != 0) return true;
            if ((GetWindowLong(hwnd, -20 /* GWL_EXSTYLE */) & 0x80 /* WS_EX_TOOLWINDOW */) != 0) return true;
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
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!IsWindow(hwnd) || !GetWindowPlacement(hwnd, ref wp)) return null;
        var (dx, dy) = WorkspaceOffset();
        var r = wp.rcNormalPosition;
        return new RectPx(r.Left + dx, r.Top + dy, r.Right - r.Left, r.Bottom - r.Top);
    }

    /// <summary>rcNormalPosition is in "workspace" coordinates: origin at the corner of the primary monitor's work area (a taskbar at the top/left shifts it).</summary>
    internal static (int dx, int dy) WorkspaceOffset()
    {
        SystemParametersInfo(0x30 /* SPI_GETWORKAREA */, 0, out RECT wa, 0);
        return (wa.Left, wa.Top);
    }

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
