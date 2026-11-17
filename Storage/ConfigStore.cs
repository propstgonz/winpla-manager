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

    /// <summary>Nunca lanza: si no existe devuelve Config por defecto; si está corrupto lo aparta como .bak y devuelve Config por defecto.</summary>
    public static Config Load()
    {
        try
        {
            if (!File.Exists(Path)) return new Config();
            try
            {
                var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(Path), Options) ?? new Config();
                config.Rules ??= [];
                if (config.WatchSeconds <= 0) config.WatchSeconds = 300;
                return config;
            }
            catch (JsonException ex) // solo JSON inválido; un bloqueo de lectura (IOException) no debe apartar una config sana
            {
                Log.Write("config.json ilegible, se aparta como .bak: " + ex.Message);
                File.Move(Path, Path + ".bak", overwrite: true);
            }
        }
        catch (Exception ex) { Log.Write("No se pudo apartar config.json: " + ex.Message); }
        return new Config();
    }

    /// <summary>Escritura atómica (temporal + reemplazo). Lanza si no se puede guardar.</summary>
    public static void Save(Config config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Options));
        File.Move(tmp, Path, overwrite: true);
    }
}
