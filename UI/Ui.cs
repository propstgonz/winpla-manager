using System.Windows.Markup;

namespace WinPla;

/// <summary>Loads loose XAML: from disk if Dir is set (WinPla.ps1), otherwise from the embedded resource (exe).</summary>
public static class Ui
{
    public static string? Dir { get; set; }

    public static T Load<T>(string name)
    {
        using Stream s = Dir is not null
            ? File.OpenRead(System.IO.Path.Combine(Dir, name))
            : typeof(Ui).Assembly.GetManifestResourceStream(name) ?? throw new FileNotFoundException(name);
        return (T)XamlReader.Load(s);
    }
}
