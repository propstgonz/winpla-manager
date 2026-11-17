using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WinPla;

public static class StartupApps
{
    const string Win = @"Software\Microsoft\Windows\CurrentVersion\";
    const string Approved = Win + @"Explorer\StartupApproved\";

    // ponytail: startup tasks of packaged Store apps (UWP/MSIX, StartupTask in the manifest) are left out;
    // they are not in Run or in the folders. Adding them would require reading each package's manifest.

    /// <summary>Enabled startup apps (registry Run keys and Startup folders), without duplicates by ExeName and without WinPla. Doesn't throw.</summary>
    public static IReadOnlyList<StartupApp> GetAll()
    {
        var found = new List<StartupApp>();
        void Add(string name, string? cmd)
        {
            try
            {
                if (name.Equals("WinPla", StringComparison.OrdinalIgnoreCase) || cmd == null) return;
                var p = ParseCommand(cmd);
                if (p == null) return;
                var (exe, path) = p.Value;
                if (path.EndsWith("Update.exe", StringComparison.OrdinalIgnoreCase) && !exe.Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
                    path = FindInAppDir(path, exe) ?? path;
                found.Add(new StartupApp(name, exe, path));
            }
            catch (Exception ex) { Log.Write($"StartupApps '{name}': {ex.Message}"); }
        }

        foreach (var (hive, sub, approvedName) in new[]
        {
            (Registry.CurrentUser, Win + "Run", "Run"),
            (Registry.LocalMachine, Win + "Run", "Run"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32"),
        })
        {
            try
            {
                using var key = hive.OpenSubKey(sub);
                if (key == null) continue;
                foreach (var n in key.GetValueNames())
                    if (n != "" && !IsDisabled(approvedName, n)) Add(n, key.GetValue(n) as string);
            }
            catch (Exception ex) { Log.Write($"StartupApps {sub}: {ex.Message}"); }
        }

        foreach (var folder in new[] { Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup })
        {
            try
            {
                var dir = Environment.GetFolderPath(folder);
                if (!Directory.Exists(dir)) continue;
                foreach (var lnk in Directory.GetFiles(dir, "*.lnk"))
                {
                    var file = Path.GetFileName(lnk);
                    if (IsDisabled("StartupFolder", file)) continue;
                    var (target, args) = ReadLnk(lnk);
                    if (string.IsNullOrWhiteSpace(target)) continue;
                    Add(Path.GetFileNameWithoutExtension(file), $"\"{target}\" {args}");
                }
            }
            catch (Exception ex) { Log.Write($"StartupApps folder {folder}: {ex.Message}"); }
        }

        return found
            .GroupBy(a => a.ExeName, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Extracts the real executable from a startup command line. Pure (doesn't touch the disk). null if there is no recognizable .exe.
    /// Squirrel ("Update.exe --processStart X.exe"): ExeName is X.exe and ExePath is the path of Update.exe.</summary>
    public static (string ExeName, string ExePath)? ParseCommand(string commandLine)
    {
        var s = Environment.ExpandEnvironmentVariables(commandLine ?? "").Trim();
        if (s == "") return null;
        string path, rest;
        if (s[0] == '"')
        {
            int end = s.IndexOf('"', 1);
            if (end < 0) return null;
            path = s[1..end]; rest = s[(end + 1)..];
        }
        else
        {
            var m = Regex.Match(s, @"\.exe(?=\s|$)", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            path = s[..(m.Index + 4)]; rest = s[(m.Index + 4)..];
        }
        path = path.Trim();
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
        var name = Path.GetFileName(path);
        if (name == "") return null;
        // host programs: the real program is somewhere in the arguments and can't be resolved reliably
        if (new[] { "rundll32.exe", "cmd.exe", "wscript.exe", "cscript.exe", "conhost.exe" }.Contains(name, StringComparer.OrdinalIgnoreCase))
            return null;

        if (name.Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
        {
            // quoted ("My App.exe", may contain spaces) or bare (Discord.exe); exactly one of the two groups matches
            var sq = Regex.Match(rest, @"--processStart(?:AndWait)?(?:=|\s+)(?:""([^""]+\.exe)""|([^""\s]+\.exe))", RegexOptions.IgnoreCase);
            if (sq.Success) return (Path.GetFileName(sq.Groups[1].Value + sq.Groups[2].Value), path);
        }
        return (name, path);
    }

    // Squirrel installs the real exe in <Update.exe dir>\app-<version>\; the most recent folder that contains it is used.
    static string? FindInAppDir(string updateExe, string exeName)
    {
        var dir = Path.GetDirectoryName(updateExe);
        if (dir == null || !Directory.Exists(dir)) return null;
        return Directory.GetDirectories(dir, "app-*")
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .Select(d => Path.Combine(d, exeName))
            .FirstOrDefault(File.Exists);
    }

    // Disabled = binary value in StartupApproved (HKCU or HKLM) whose first byte is odd. Same rule as Autostart.
    static bool IsDisabled(string approvedName, string valueName)
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var k = hive.OpenSubKey(Approved + approvedName);
                if (k?.GetValue(valueName) is byte[] { Length: > 0 } b && (b[0] & 1) == 1) return true;
            }
            catch { /* unreadable key: treated as enabled */ }
        }
        return false;
    }

    static (string Target, string Args) ReadLnk(string path)
    {
        object? sh = null, lnk = null;
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return ("", "");
            sh = Activator.CreateInstance(t);
            lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, [path]);
            if (lnk == null) return ("", "");
            var lt = lnk.GetType();
            string Get(string p) => lt.InvokeMember(p, BindingFlags.GetProperty, null, lnk, null) as string ?? "";
            return (Get("TargetPath"), Get("Arguments"));
        }
        catch (Exception ex) { Log.Write($"StartupApps lnk {path}: {ex.Message}"); return ("", ""); }
        finally
        {
            if (lnk != null) Marshal.FinalReleaseComObject(lnk);
            if (sh != null) Marshal.FinalReleaseComObject(sh);
        }
    }
}
