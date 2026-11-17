using System.Runtime.InteropServices;
using static WinPla.NativeMethods;

namespace WinPla;

public static class Placer
{
    const int Tolerance = 2;

    /// <summary>Función pura: rect normal de destino de una ventana cuyo rect normal actual es <paramref name="current"/>.</summary>
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

    /// <summary>Coloca la ventana según la regla. Idempotente: devuelve AlreadyOk si no hay nada que cambiar.</summary>
    public static PlaceStatus Apply(WindowInfo window, Rule rule, MonitorInfo monitor)
    {
        var h = window.Handle;
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!IsWindow(h) || !GetWindowPlacement(h, ref wp)) return PlaceStatus.Failed;

        var (dx, dy) = AppWindows.WorkspaceOffset();
        var n = wp.rcNormalPosition;
        var cur = new RectPx(n.Left + dx, n.Top + dy, n.Right - n.Left, n.Bottom - n.Top);
        var target = ComputeTarget(rule, monitor, cur);
        bool min = IsIconic(h), max = !min && wp.showCmd == SW_SHOWMAXIMIZED;
        bool restoreMax = min && (wp.flags & WPF_RESTORETOMAXIMIZED) != 0;
        bool wantMax = rule.Mode == PlacementMode.Maximized;

        bool ok;
        if (wantMax)
        {
            GetWindowRect(h, out RECT wr);
            ok = min ? restoreMax && Inside(cur, monitor.Bounds) : max && Inside(Monitors.ToRect(wr), monitor.Bounds);
        }
        else
            ok = !max && !restoreMax && Near(cur, target);
        if (ok) return PlaceStatus.AlreadyOk;

        wp.rcNormalPosition = new RECT { Left = target.X - dx, Top = target.Y - dy, Right = target.X + target.Width - dx, Bottom = target.Y + target.Height - dy };
        if (min)
        {
            // minimizada se queda minimizada; al restaurar aparece donde debe
            wp.showCmd = SW_SHOWMINNOACTIVE;
            wp.flags = wantMax ? wp.flags | WPF_RESTORETOMAXIMIZED : wp.flags & ~WPF_RESTORETOMAXIMIZED;
            return Set(h, ref wp);
        }
        // Paso 1: a normal en el destino (maximizar directamente no mueve una ventana ya maximizada en otro monitor)
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

    static bool Inside(RectPx r, RectPx bounds)
    {
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        return cx >= bounds.X && cx < bounds.X + bounds.Width && cy >= bounds.Y && cy < bounds.Y + bounds.Height;
    }

    static bool Near(RectPx a, RectPx b) =>
        Math.Abs(a.X - b.X) <= Tolerance && Math.Abs(a.Y - b.Y) <= Tolerance &&
        Math.Abs(a.Width - b.Width) <= Tolerance && Math.Abs(a.Height - b.Height) <= Tolerance;
}
