using System.Runtime.InteropServices;
using static WinPla.NativeMethods;

namespace WinPla;

public static class Monitors
{
    /// <summary>Monitores conectados y activos ahora mismo. Se consulta en cada llamada (sin caché).</summary>
    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(0, 0, (nint h, nint _, ref RECT _, nint _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (!GetMonitorInfo(h, ref mi)) return true;

            var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            EnumDisplayDevices(mi.szDevice, 0, ref dd, 1 /* EDD_GET_DEVICE_INTERFACE_NAME */);

            string id = string.IsNullOrEmpty(dd.DeviceID) ? mi.szDevice : dd.DeviceID;
            // DeviceID tipo \\?\DISPLAY#GSM5B09#5&...; el 2º trozo es el código de modelo
            string name = dd.DeviceString;
            if (string.IsNullOrWhiteSpace(name) || name.Contains("Generic PnP", StringComparison.OrdinalIgnoreCase))
            {
                var parts = id.Split('#');
                name = parts.Length > 1 ? parts[1] : mi.szDevice;
            }
            var digits = new string(mi.szDevice.Where(char.IsDigit).ToArray());
            int.TryParse(digits, out int number);

            list.Add(new MonitorInfo(id, number, name, ToRect(mi.rcMonitor), ToRect(mi.rcWork), (mi.dwFlags & 1) != 0));
            return true;
        }, 0);
        return list.OrderBy(m => m.Number).ToList();
    }

    internal static RectPx ToRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
