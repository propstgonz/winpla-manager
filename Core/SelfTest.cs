namespace WinPla;

public static class SelfTest
{
    /// <summary>Comprueba Placer.ComputeTarget con casos fijos (sin tocar ventanas reales). null = todo bien; si no, descripción del primer fallo.</summary>
    public static string? Run()
    {
        var m1 = new MonitorInfo("a", 1, "A", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true);
        var m2 = new MonitorInfo("b", 2, "B", new(-1280, 100, 1280, 1024), new(-1280, 140, 1280, 984), false);
        var cur = new RectPx(100, 100, 800, 600);
        var normal = new Rule { Mode = PlacementMode.Normal };
        var custom = new Rule { Mode = PlacementMode.Custom, X = 10, Y = 20, Width = 500, Height = 400 };

        static string? Check(string name, RectPx got, RectPx want) => got == want ? null : $"{name}: obtenido {got}, esperado {want}";
        static string? Parse(string cmd, string? want)
        {
            var r = StartupApps.ParseCommand(cmd);
            var got = r == null ? null : r.Value.ExeName + "|" + r.Value.ExePath;
            return got == want ? null : $"ParseCommand({cmd}): obtenido {got ?? "null"}, esperado {want ?? "null"}";
        }
        var lad = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        return Parse("\"C:\\Program Files\\Foo\\foo.exe\" -silent", "foo.exe|C:\\Program Files\\Foo\\foo.exe")
            ?? Parse("C:\\Program Files\\Foo\\foo.exe -silent", "foo.exe|C:\\Program Files\\Foo\\foo.exe")
            ?? Parse("%LOCALAPPDATA%\\Bar\\bar.exe /x", $"bar.exe|{lad}\\Bar\\bar.exe")
            ?? Parse("\"C:\\U\\Discord\\Update.exe\" --processStart Discord.exe", "Discord.exe|C:\\U\\Discord\\Update.exe")
            ?? Parse("\"C:\\U\\Discord\\Update.exe\" --processStart \"Discord.exe\" --a", "Discord.exe|C:\\U\\Discord\\Update.exe")
            ?? Parse("\"C:\\U\\slack\\Update.exe\" --processStart=slack.exe", "slack.exe|C:\\U\\slack\\Update.exe")
            ?? Parse("", null)
            ?? Parse("cmd /c start x.bat", null)
            ?? Check("custom relativo", Placer.ComputeTarget(custom, m1, cur), new(10, 20, 500, 400))
            ?? Check("custom origen negativo", Placer.ComputeTarget(custom, m2, cur), new(-1270, 160, 500, 400))
            ?? Check("custom sin tamaño", Placer.ComputeTarget(new Rule { Mode = PlacementMode.Custom }, m1, cur), new(0, 0, 800, 600))
            ?? Check("normal ya en el monitor", Placer.ComputeTarget(normal, m1, cur), cur)
            ?? Check("normal en otro monitor", Placer.ComputeTarget(normal, m2, cur), new(-1040, 332, 800, 600))
            ?? Check("ventana mayor que el área", Placer.ComputeTarget(new Rule { Mode = PlacementMode.Maximized }, m1, new(50, 50, 3000, 2000)), new(50, 50, 1920, 1040));
    }
}
