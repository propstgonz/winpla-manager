namespace WinPla;

public static class Log
{
    static readonly object _lock = new();

    /// <summary>%APPDATA%\WinPla\log.txt</summary>
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinPla", "log.txt");

    /// <summary>Añade una línea con marca de tiempo. Nunca lanza. Seguro entre hilos.</summary>
    public static void Write(string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                // ponytail: al superar 512 KB se reinicia el log entero; rotación si alguna vez importa el histórico
                if (File.Exists(Path) && new FileInfo(Path).Length > 512 * 1024) File.Delete(Path);
                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
