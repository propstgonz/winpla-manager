// CONTRATOS DEL PROYECTO — propiedad del arquitecto. Los agentes NO modifican este fichero.
// Si un contrato no basta, se informa al arquitecto en el informe final en vez de cambiarlo.
namespace WinPla;

public enum PlacementMode { Normal, Maximized, Custom }

/// <summary>Regla: "las ventanas de este ejecutable van a este monitor con este modo".</summary>
public sealed class Rule
{
    /// <summary>Clave de coincidencia: nombre de fichero del proceso dueño de la ventana ("Discord.exe"). Sin distinguir mayúsculas.</summary>
    public string ExeName { get; set; } = "";
    /// <summary>Ruta completa, solo informativa (icono, tooltip). Puede quedar obsoleta; nunca se usa para casar.</summary>
    public string ExePath { get; set; } = "";
    /// <summary>MonitorInfo.Id del monitor de destino (estable entre reinicios).</summary>
    public string MonitorId { get; set; } = "";
    /// <summary>MonitorInfo.Label en el momento de guardar; se muestra cuando el monitor está desconectado.</summary>
    public string MonitorLabel { get; set; } = "";
    public PlacementMode Mode { get; set; } = PlacementMode.Maximized;
    /// <summary>Solo Mode == Custom. Píxeles físicos; X/Y relativos a la esquina superior izquierda del área de trabajo del monitor.</summary>
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class Config
{
    /// <summary>Si es false, el modo --startup termina sin hacer nada.</summary>
    public bool AutoPlacementEnabled { get; set; } = true;
    /// <summary>Segundos que el modo --startup vigila ventanas nuevas antes de terminar. Solo editable en el JSON.</summary>
    public int WatchSeconds { get; set; } = 300;
    public List<Rule> Rules { get; set; } = [];
}

/// <summary>Rectángulo en píxeles físicos, coordenadas de pantalla virtual.</summary>
public readonly record struct RectPx(int X, int Y, int Width, int Height);

public sealed record MonitorInfo(
    string Id,        // identificador estable del dispositivo (ruta de interfaz de EnumDisplayDevices); nunca vacío
    int Number,       // N de \\.\DISPLAYN; solo para mostrar, puede cambiar
    string Name,      // nombre legible del monitor, lo mejor que se pueda obtener
    RectPx Bounds,
    RectPx WorkArea,
    bool IsPrimary)
{
    /// <summary>Texto único para toda la UI: "Monitor 2 · 2560×1440 · Principal".</summary>
    public string Label => $"Monitor {Number} · {Bounds.Width}×{Bounds.Height}{(IsPrimary ? " · Principal" : "")}";
}

/// <summary>Ventana de aplicación de nivel superior candidata a ser colocada.</summary>
public sealed record WindowInfo(nint Handle, string Title, string ExeName, string ExePath);

/// <summary>Aplicación que Windows lanza al iniciar sesión y que está activada. ExeName/ExePath son los del programa real (no los del lanzador/actualizador).</summary>
public sealed record StartupApp(string Name, string ExeName, string ExePath);

public enum PlaceStatus
{
    Placed,               // se movió / cambió de estado
    AlreadyOk,            // ya estaba donde debía
    WindowNotFound,       // no hay ninguna ventana de ese ejecutable (Window == null)
    MonitorDisconnected,  // el monitor de la regla no está conectado; la ventana no se toca
    AccessDenied,         // la aplicación corre como administrador y WinPla no (UIPI)
    Failed,
}

public sealed record PlaceResult(Rule Rule, WindowInfo? Window, PlaceStatus Status);

/*  API estática que completa el contrato (cada clase en su fichero, con su agente dueño):

    Agente A — Core/
      Monitors.GetAll()                                   -> IReadOnlyList<MonitorInfo>
      AppWindows.GetAll()                                 -> IReadOnlyList<WindowInfo>
      AppWindows.GetRect(nint hwnd)                       -> RectPx?   (rect "normal"/restaurado, pantalla virtual)
      Placer.ComputeTarget(Rule, MonitorInfo, RectPx cur) -> RectPx    (función pura)
      Placer.Apply(WindowInfo, Rule, MonitorInfo)         -> PlaceStatus
      Engine.ApplyOnce(Config)                            -> IReadOnlyList<PlaceResult>
      Engine.WatchAsync(Config, CancellationToken)        -> Task
      SelfTest.Run()                                      -> string?   (null = OK)
      StartupApps.GetAll()                                -> IReadOnlyList<StartupApp>
      StartupApps.ParseCommand(string commandLine)        -> (string ExeName, string ExePath)?   (función pura)

    Agente B — Storage/
      ConfigStore.Load() / ConfigStore.Save(Config) / ConfigStore.Path
      Autostart.IsEnabled / Autostart.Set(bool) / Autostart.Refresh()
      Log.Write(string) / Log.Path

    Agente C — UI/  (App.xaml, MainWindow, RuleDialog). Solo consume lo anterior.

    Arquitecto — App.xaml.cs (punto de entrada y modos), Contracts.cs, WinPla.csproj, app.manifest.
*/
