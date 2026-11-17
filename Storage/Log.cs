namespace WinPla;

public static class Log
{
    static readonly object _lock = new();

    /// <summary>%APPDATA%\WinPla\log.txt</summary>
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinPla", "log.txt");

    /// <summary>Appends a timestamped line. Never throws. Thread-safe.</summary>
    public static void Write(string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                // ponytail: past 512 KB the whole log is reset; add rotation if the history ever matters
                if (File.Exists(Path) && new FileInfo(Path).Length > 512 * 1024) File.Delete(Path);
                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
