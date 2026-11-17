using System.Runtime.InteropServices;
using static WinPla.NativeMethods;

namespace WinPla;

public static class Placer
{
    const int Tolerance = 2;

    /// <summary>Pure function: target normal rect of a window whose current normal rect is <paramref name="current"/>.</summary>
    public static RectPx ComputeTarget(Rule rule, MonitorInfo monitor, RectPx current)
    {
        var wa = monitor.WorkArea;
        if (rule.Mode == PlacementMode.Custom)
            return new RectPx(wa.X + rule.X, wa.Y + rule.Y,
                rule.Width > 0 ? rule.Width : current.Width, rule.Height > 0 ? rule.Height : current.Height);

        int w = Math.Min(current.Width, wa.Width), h = Math.Min(current.Height, wa.Height);
        if (Inside(current, monitor.Bounds)) return new RectPx(current.X, current.Y, w, h);
        return new RectPx(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
    }

    /// <summary>Places the window according to the rule. Idempotent: returns AlreadyOk if there is nothing to change.</summary>
    public static PlaceStatus Apply(WindowInfo window, Rule rule, MonitorInfo monitor)
    {
        var h = window.Handle;
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        // a hung window would block SetWindowPlacement (it sends messages) until it responds
        if (!IsWindow(h) || IsHungAppWindow(h) || !GetWindowPlacement(h, ref wp)) return PlaceStatus.Failed;

        var cur = AppWindows.FromWorkspace(wp.rcNormalPosition);
        var target = ComputeTarget(rule, monitor, cur);
        bool min = IsIconic(h), max = !min && wp.showCmd == SW_SHOWMAXIMIZED;
        bool restoreMax = min && (wp.flags & WPF_RESTORETOMAXIMIZED) != 0;
        bool wantMax = rule.Mode == PlacementMode.Maximized;
        GetWindowRect(h, out RECT wr);
        var actual = Monitors.ToRect(wr);

        bool ok;
        if (wantMax)
            ok = min ? restoreMax && Inside(cur, monitor.Bounds) : max && Inside(actual, monitor.Bounds);
        else
            // a snapped window keeps its pre-snap rect in rcNormalPosition: where it really is must agree too
            ok = !max && !restoreMax && Near(cur, target)
                && (min || (rule.Mode == PlacementMode.Custom ? Near(actual, target) : Inside(actual, monitor.Bounds)));
        if (ok) return PlaceStatus.AlreadyOk;

        var (dx, dy) = AppWindows.WorkspaceOffset(monitor.Bounds, monitor.WorkArea);   // the target is on this monitor
        wp.rcNormalPosition = new RECT { Left = target.X - dx, Top = target.Y - dy, Right = target.X + target.Width - dx, Bottom = target.Y + target.Height - dy };
        if (min)
        {
            // a minimized window stays minimized; when restored it appears where it should
            wp.showCmd = SW_SHOWMINNOACTIVE;
            wp.flags = wantMax ? wp.flags | WPF_RESTORETOMAXIMIZED : wp.flags & ~WPF_RESTORETOMAXIMIZED;
            return Set(h, ref wp);
        }
        // Step 1: to normal at the target (maximizing directly doesn't move a window already maximized on another monitor)
        wp.flags &= ~WPF_RESTORETOMAXIMIZED;
        wp.showCmd = SW_SHOWNOACTIVATE;
        var s = Set(h, ref wp);
        if (s != PlaceStatus.Placed || !wantMax) return s;
        wp.showCmd = SW_SHOWMAXIMIZED;
        return Set(h, ref wp);
    }

    static PlaceStatus Set(nint h, ref WINDOWPLACEMENT wp) =>
        SetWindowPlacement(h, ref wp) ? PlaceStatus.Placed
        : Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED ? PlaceStatus.AccessDenied : PlaceStatus.Failed;

    /// <summary>Custom rule (as typed in the dialog) whose window lands on the monitor: centre inside its bounds and no larger than it.</summary>
    internal static bool Fits(Rule rule, MonitorInfo monitor)
    {
        var t = ComputeTarget(rule, monitor, default);
        return t.Width <= monitor.Bounds.Width && t.Height <= monitor.Bounds.Height && Inside(t, monitor.Bounds);
    }

    static bool Inside(RectPx r, RectPx bounds)
    {
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        return cx >= bounds.X && cx < bounds.X + bounds.Width && cy >= bounds.Y && cy < bounds.Y + bounds.Height;
    }

    static bool Near(RectPx a, RectPx b) =>
        Math.Abs(a.X - b.X) <= Tolerance && Math.Abs(a.Y - b.Y) <= Tolerance &&
        Math.Abs(a.Width - b.Width) <= Tolerance && Math.Abs(a.Height - b.Height) <= Tolerance;
}
