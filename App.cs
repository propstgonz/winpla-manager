// Entry point and run modes. Used by the exe and by WinPla.ps1 (Add-Type + [WinPla.App]::Main).
using System.Runtime.InteropServices;
using System.Windows;

namespace WinPla;

public static class App
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(nint value);

    [STAThread]
    public static int Main(string[] args)
    {
        // PerMonitorV2: the exe already gets it from the manifest; inside pwsh there is no manifest.
        try { SetProcessDpiAwarenessContext(-4); } catch { }

        // --startup: launched by the Run key at sign-in. No window; places the windows and exits.
        if (args.Contains("--startup", StringComparer.OrdinalIgnoreCase))
        {
            using var mutex = new Mutex(true, @"Local\WinPla.Startup", out bool first);
            try
            {
                var config = ConfigStore.Load();
                if (first && config.AutoPlacementEnabled && config.Rules.Count > 0)
                    Engine.WatchAsync(config, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Log.Write("--startup mode failed: " + ex); }
            return 0;
        }

        // --selftest: minimal check of the pure logic. Exit code 0 = OK; details in the log.
        if (args.Contains("--selftest", StringComparer.OrdinalIgnoreCase))
        {
            var error = SelfTest.Run();
            Log.Write(error is null ? "Selftest OK" : "Selftest FAILED: " + error);
            return error is null ? 0 : 1;
        }

        Autostart.Refresh();
        var app = new Application();
        try { app.ThemeMode = ThemeMode.System; }
        catch (Exception ex) { Log.Write("Couldn't set the theme: " + ex.Message); }
        app.DispatcherUnhandledException += (_, ex) => Log.Write("Unhandled error: " + ex.Exception);
        return app.Run(new MainWindow());
    }
}
