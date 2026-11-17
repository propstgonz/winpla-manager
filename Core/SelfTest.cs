namespace WinPla;

public static class SelfTest
{
    /// <summary>Checks Placer.ComputeTarget with fixed cases (without touching real windows). null = all good; otherwise, a description of the first failure.</summary>
    public static string? Run()
    {
        var m1 = new MonitorInfo("a", 1, "A", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true);
        var m2 = new MonitorInfo("b", 2, "B", new(-1280, 100, 1280, 1024), new(-1280, 140, 1280, 984), false);
        var cur = new RectPx(100, 100, 800, 600);
        var normal = new Rule { Mode = PlacementMode.Normal };
        var custom = new Rule { Mode = PlacementMode.Custom, X = 10, Y = 20, Width = 500, Height = 400 };

        static string? Check(string name, RectPx got, RectPx want) => got == want ? null : $"{name}: got {got}, expected {want}";
        static string? Parse(string cmd, string? want)
        {
            var r = StartupApps.ParseCommand(cmd);
            var got = r == null ? null : r.Value.ExeName + "|" + r.Value.ExePath;
            return got == want ? null : $"ParseCommand({cmd}): got {got ?? "null"}, expected {want ?? "null"}";
        }
        var lad = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        return Parse("\"C:\\Program Files\\Foo\\foo.exe\" -silent", "foo.exe|C:\\Program Files\\Foo\\foo.exe")
            ?? Parse("C:\\Program Files\\Foo\\foo.exe -silent", "foo.exe|C:\\Program Files\\Foo\\foo.exe")
            ?? Parse("%LOCALAPPDATA%\\Bar\\bar.exe /x", $"bar.exe|{lad}\\Bar\\bar.exe")
            ?? Parse("\"C:\\U\\Discord\\Update.exe\" --processStart Discord.exe", "Discord.exe|C:\\U\\Discord\\Update.exe")
            ?? Parse("\"C:\\U\\Discord\\Update.exe\" --processStart \"Discord.exe\" --a", "Discord.exe|C:\\U\\Discord\\Update.exe")
            ?? Parse("\"C:\\U\\slack\\Update.exe\" --processStart=slack.exe", "slack.exe|C:\\U\\slack\\Update.exe")
            ?? Parse("\"C:\\U\\My App\\Update.exe\" --processStart \"My App.exe\"", "My App.exe|C:\\U\\My App\\Update.exe")
            ?? Parse("", null)
            ?? Parse("cmd /c start x.bat", null)
            ?? Parse("\"C:\\Windows\\System32\\cmd.exe\" /c start x.bat", null)
            ?? Parse("C:\\Windows\\System32\\RUNDLL32.EXE shell32.dll,Foo", null)
            ?? Parse("C:\\Windows\\System32\\wscript.exe //B x.vbs", null)
            ?? (AppWindows.WorkspaceOffset(new(0, 0, 1920, 1080), new(48, 0, 1872, 1080)) == (48, 0) ? null : "workspace offset, taskbar on the left")
            ?? (AppWindows.WorkspaceOffset(m2.Bounds, m2.WorkArea) == (0, 40) ? null : "workspace offset, monitor with negative origin")
            ?? (Placer.Fits(custom, m1) ? null : "Fits: custom rule on the monitor")
            ?? (!Placer.Fits(new Rule { Mode = PlacementMode.Custom, X = 1800, Y = 0, Width = 500, Height = 400 }, m1) ? null : "Fits: centre off the monitor")
            ?? (!Placer.Fits(new Rule { Mode = PlacementMode.Custom, X = 0, Y = 0, Width = 2000, Height = 400 }, m1) ? null : "Fits: wider than the monitor")
            ?? (!Placer.Fits(new Rule { Mode = PlacementMode.Custom, X = int.MaxValue, Y = 0, Width = 500, Height = 400 }, m2) ? null : "Fits: overflowing X")
            ?? (ConfigStore.Parse("{\"Rules\":[null,{\"ExeName\":null,\"MonitorId\":null}]}").Rules is [{ ExeName: "", MonitorId: "", ExePath: "", MonitorLabel: "" }] ? null : "ConfigStore.Parse: null rule / null fields")
            ?? Check("custom relative", Placer.ComputeTarget(custom, m1, cur), new(10, 20, 500, 400))
            ?? Check("custom negative origin", Placer.ComputeTarget(custom, m2, cur), new(-1270, 160, 500, 400))
            ?? Check("custom without size", Placer.ComputeTarget(new Rule { Mode = PlacementMode.Custom }, m1, cur), new(0, 0, 800, 600))
            ?? Check("normal already on the monitor", Placer.ComputeTarget(normal, m1, cur), cur)
            ?? Check("normal on another monitor", Placer.ComputeTarget(normal, m2, cur), new(-1040, 332, 800, 600))
            ?? Check("window larger than the work area", Placer.ComputeTarget(new Rule { Mode = PlacementMode.Maximized }, m1, new(50, 50, 3000, 2000)), new(50, 50, 1920, 1040));
    }
}
