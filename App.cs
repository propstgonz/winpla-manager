// Punto de entrada y modos de ejecución. Sirve al exe y a WinPla.ps1 (Add-Type + [WinPla.App]::Main).
using System.Runtime.InteropServices;
using System.Windows;

namespace WinPla;

public static class App
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(nint value);

    [STAThread]
    public static int Main(string[] args)
    {
        // PerMonitorV2: el exe ya lo fija el manifiesto; dentro de pwsh no hay manifiesto.
        try { SetProcessDpiAwarenessContext(-4); } catch { }

        // --startup: lanzado por la clave Run al iniciar sesión. Sin ventana; coloca y termina.
        if (args.Contains("--startup", StringComparer.OrdinalIgnoreCase))
        {
            using var mutex = new Mutex(true, @"Local\WinPla.Startup", out bool first);
            try
            {
                var config = ConfigStore.Load();
                if (first && config.AutoPlacementEnabled && config.Rules.Count > 0)
                    Engine.WatchAsync(config, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Log.Write("Fallo en modo --startup: " + ex); }
            return 0;
        }

        // --selftest: comprobación mínima de la lógica pura. Código de salida 0 = OK; detalle en el log.
        if (args.Contains("--selftest", StringComparer.OrdinalIgnoreCase))
        {
            var error = SelfTest.Run();
            Log.Write(error is null ? "Selftest OK" : "Selftest FALLO: " + error);
            return error is null ? 0 : 1;
        }

        Autostart.Refresh();
        var app = new Application();
        try { app.ThemeMode = ThemeMode.System; }
        catch (Exception ex) { Log.Write("No se pudo fijar el tema: " + ex.Message); }
        app.DispatcherUnhandledException += (_, ex) => Log.Write("Error no controlado: " + ex.Exception);
        return app.Run(new MainWindow());
    }
}
