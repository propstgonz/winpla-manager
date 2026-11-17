using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinPla;

public static class ConfigStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>%APPDATA%\WinPla\config.json</summary>
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinPla", "config.json");

    // Load couldn't read the file (lock, permissions...): saving the default Config it returned would wipe the real one.
    static bool _loadFailed;

    /// <summary>Never throws: if the file doesn't exist, returns the default Config; if it is corrupt, moves it aside as .bak and returns the default Config.</summary>
    public static Config Load()
    {
        try
        {
            if (!File.Exists(Path)) return new Config();
            try { return Parse(File.ReadAllText(Path)); }
            catch (JsonException ex) // invalid JSON only; a read lock (IOException) must not move a healthy config aside
            {
                Log.Write("config.json is unreadable, moving it aside as .bak: " + ex.Message);
                File.Move(Path, Path + ".bak", overwrite: true);
            }
        }
        catch (Exception ex) { _loadFailed = true; Log.Write("Couldn't load config.json: " + ex.Message); }
        return new Config();
    }

    /// <summary>Deserializes and normalizes hand-edited values (null rules are dropped, null texts become ""). Throws JsonException.</summary>
    internal static Config Parse(string json)
    {
        var config = JsonSerializer.Deserialize<Config>(json, Options) ?? new Config();
        config.Rules = (config.Rules ?? []).Where(r => r != null).ToList();
        foreach (var r in config.Rules)
        {
            r.ExeName ??= ""; r.ExePath ??= ""; r.MonitorId ??= ""; r.MonitorLabel ??= "";
        }
        if (config.WatchSeconds <= 0) config.WatchSeconds = 300;
        return config;
    }

    /// <summary>Atomic write (temporary file flushed to disk + replace). Throws if it can't be saved.</summary>
    public static void Save(Config config)
    {
        if (_loadFailed)
            throw new IOException("config.json couldn't be read when WinPla started, so it wasn't overwritten. Close WinPla and open it again.");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = Path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            JsonSerializer.Serialize(fs, config, Options);
            fs.Flush(flushToDisk: true);
        }
        File.Move(tmp, Path, overwrite: true);
    }
}
