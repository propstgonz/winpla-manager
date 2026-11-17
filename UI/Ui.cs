using System.Windows.Markup;

namespace WinPla;

/// <summary>Carga XAML suelto: del disco si Dir está fijado (WinPla.ps1), si no del recurso incrustado (exe).</summary>
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
