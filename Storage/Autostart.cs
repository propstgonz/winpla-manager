using Microsoft.Win32;

namespace WinPla;

public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    const string Name = "WinPla";

    /// <summary>Línea de comando de la clave Run. WinPla.ps1 la sustituye (bajo pwsh, ProcessPath sería pwsh.exe).</summary>
    public static string Command { get; set; } = $"\"{Environment.ProcessPath}\" --startup";

    /// <summary>La clave Run del usuario contiene una entrada de WinPla y el usuario no la ha desactivado.</summary>
    public static bool IsEnabled
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            if (run?.GetValue(Name) is null) return false;
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return !(approved?.GetValue(Name) is byte[] { Length: > 0 } b && (b[0] & 1) == 1);
        }
    }

    /// <summary>Crea o elimina la entrada HKCU\...\Run: "ruta\WinPla.exe" --startup.</summary>
    public static void Set(bool enabled)
    {
        if (enabled)
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey)) run.SetValue(Name, Command);
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            approved?.DeleteValue(Name, throwOnMissingValue: false);
        }
        else
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            run?.DeleteValue(Name, throwOnMissingValue: false);
        }
    }

    /// <summary>Si la entrada existe y no apunta al exe actual (el usuario movió el exe), la reescribe. No la crea. No lanza.</summary>
    public static void Refresh()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(Name) is string s && s != Command) run.SetValue(Name, Command);
        }
        catch (Exception ex) { Log.Write("Autostart.Refresh falló: " + ex.Message); }
    }
}
