// PROJECT CONTRACTS — owned by the architect. Agents do NOT modify this file.
// If a contract is not enough, report it to the architect in the final report instead of changing it.
namespace WinPla;

public enum PlacementMode { Normal, Maximized, Custom }

/// <summary>Rule: "the windows of this executable go to this monitor in this mode".</summary>
public sealed class Rule
{
    /// <summary>Match key: file name of the process that owns the window ("Discord.exe"). Case-insensitive.</summary>
    public string ExeName { get; set; } = "";
    /// <summary>Full path, informational only (icon, tooltip). May become outdated; never used for matching.</summary>
    public string ExePath { get; set; } = "";
    /// <summary>MonitorInfo.Id of the target monitor (stable across restarts).</summary>
    public string MonitorId { get; set; } = "";
    /// <summary>MonitorInfo.Label at the time of saving; shown when the monitor is disconnected.</summary>
    public string MonitorLabel { get; set; } = "";
    public PlacementMode Mode { get; set; } = PlacementMode.Maximized;
    /// <summary>Only when Mode == Custom. Physical pixels; X/Y relative to the top-left corner of the monitor's work area.</summary>
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class Config
{
    /// <summary>If false, --startup mode exits without doing anything.</summary>
    public bool AutoPlacementEnabled { get; set; } = true;
    /// <summary>Seconds that --startup mode watches for new windows before exiting. Only editable in the JSON.</summary>
    public int WatchSeconds { get; set; } = 300;
    public List<Rule> Rules { get; set; } = [];
}

/// <summary>Rectangle in physical pixels, virtual-screen coordinates.</summary>
public readonly record struct RectPx(int X, int Y, int Width, int Height);

public sealed record MonitorInfo(
    string Id,        // stable device identifier (interface path from EnumDisplayDevices); never empty
    int Number,       // N of \\.\DISPLAYN; display only, may change
    string Name,      // human-readable monitor name, the best that can be obtained
    RectPx Bounds,
    RectPx WorkArea,
    bool IsPrimary)
{
    /// <summary>Single text for the whole UI: "Monitor 2 · 2560×1440 · Primary".</summary>
    public string Label => $"Monitor {Number} · {Bounds.Width}×{Bounds.Height}{(IsPrimary ? " · Primary" : "")}";
}

/// <summary>Top-level application window that is a candidate for placement.</summary>
public sealed record WindowInfo(nint Handle, string Title, string ExeName, string ExePath);

/// <summary>Application that Windows launches at sign-in and that is enabled. ExeName/ExePath are those of the real program (not the launcher/updater).</summary>
public sealed record StartupApp(string Name, string ExeName, string ExePath);

public enum PlaceStatus
{
    Placed,               // was moved / changed state
    AlreadyOk,            // was already where it should be
    WindowNotFound,       // there is no window of that executable (Window == null)
    MonitorDisconnected,  // the rule's monitor is not connected; the window is not touched
    AccessDenied,         // the application runs as administrator and WinPla does not (UIPI)
    Failed,
}

public sealed record PlaceResult(Rule Rule, WindowInfo? Window, PlaceStatus Status);

/*  Static API that completes the contract (each class in its own file, with its owning agent):

    Agent A — Core/
      Monitors.GetAll()                                   -> IReadOnlyList<MonitorInfo>
      AppWindows.GetAll()                                 -> IReadOnlyList<WindowInfo>
      AppWindows.GetRect(nint hwnd)                       -> RectPx?   ("normal"/restored rect, virtual screen)
      Placer.ComputeTarget(Rule, MonitorInfo, RectPx cur) -> RectPx    (pure function)
      Placer.Apply(WindowInfo, Rule, MonitorInfo)         -> PlaceStatus
      Engine.ApplyOnce(Config)                            -> IReadOnlyList<PlaceResult>
      Engine.WatchAsync(Config, CancellationToken)        -> Task
      SelfTest.Run()                                      -> string?   (null = OK)
      StartupApps.GetAll()                                -> IReadOnlyList<StartupApp>
      StartupApps.ParseCommand(string commandLine)        -> (string ExeName, string ExePath)?   (pure function)

    Agent B — Storage/
      ConfigStore.Load() / ConfigStore.Save(Config) / ConfigStore.Path
      Autostart.IsEnabled / Autostart.Set(bool) / Autostart.Refresh()
      Log.Write(string) / Log.Path

    Agent C — UI/  (App.xaml, MainWindow, RuleDialog). Only consumes the above.

    Architect — App.xaml.cs (entry point and modes), Contracts.cs, WinPla.csproj, app.manifest.
*/
