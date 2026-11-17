using Microsoft.Win32;

namespace WinPla;

public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    const string Name = "WinPla";

    /// <summary>Command line for the Run key. WinPla.ps1 replaces it (under pwsh, ProcessPath would be pwsh.exe).</summary>
    public static string Command { get; set; } = $"\"{Environment.ProcessPath}\" --startup";

    /// <summary>The user's Run key contains a WinPla entry and the user hasn't disabled it.</summary>
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

    /// <summary>Creates or removes the HKCU\...\Run entry: "path\WinPla.exe" --startup.</summary>
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

    /// <summary>If the entry exists and doesn't point at the current exe (the user moved the exe), rewrites it. Doesn't create it. Doesn't throw.</summary>
    public static void Refresh()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(Name) is string s && s != Command) run.SetValue(Name, Command);
        }
        catch (Exception ex) { Log.Write("Autostart.Refresh failed: " + ex.Message); }
    }
}
