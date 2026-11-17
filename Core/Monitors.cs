using System.Runtime.InteropServices;
using static WinPla.NativeMethods;

namespace WinPla;

public static class Monitors
{
    /// <summary>Monitors that are connected and active right now. Queried on every call (no cache).</summary>
    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var list = new List<MonitorInfo>();
        var friendly = FriendlyNames();
        EnumDisplayMonitors(0, 0, (nint h, nint _, ref RECT _, nint _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (!GetMonitorInfo(h, ref mi)) return true;

            var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            EnumDisplayDevices(mi.szDevice, 0, ref dd, 1 /* EDD_GET_DEVICE_INTERFACE_NAME */);

            string id = string.IsNullOrEmpty(dd.DeviceID) ? mi.szDevice : dd.DeviceID;
            // DeviceID like \\?\DISPLAY#GSM5B09#5&...; the 2nd segment is the model code
            // We prefer the friendly name that Settings shows ("LG ULTRAGEAR")
            if (!friendly.TryGetValue(id, out string? name)) name = dd.DeviceString;
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

    /// <summary>Monitor interface path (= MonitorInfo.Id) → friendly name (EDID). Empty if anything fails.</summary>
    static Dictionary<string, string> FriendlyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint np, out uint nm) != 0) return names;
            var paths = new DISPLAYCONFIG_PATH_INFO[np];
            var modes = new DISPLAYCONFIG_MODE_INFO[nm];
            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref np, paths, ref nm, modes, 0) != 0) return names;
            for (int i = 0; i < np; i++)
            {
                var t = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                t.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                t.header.size = Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                t.header.adapterId = paths[i].targetAdapterId;
                t.header.id = paths[i].targetId;
                if (DisplayConfigGetDeviceInfo(ref t) == 0 && !string.IsNullOrWhiteSpace(t.monitorFriendlyDeviceName) && !string.IsNullOrEmpty(t.monitorDevicePath))
                    names[t.monitorDevicePath] = t.monitorFriendlyDeviceName.Trim();
            }
        }
        catch { }   // friendly name is optional: if it fails, the usual name is used
        return names;
    }

    internal static RectPx ToRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
