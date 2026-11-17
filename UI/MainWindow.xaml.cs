using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WinPla;

/// <summary>Fila de la lista de reglas.</summary>
public sealed class RuleRow
{
    public required Rule Rule { get; init; }
    public System.Windows.Media.ImageSource? Icon { get; init; }
    public string Name => Rule.ExeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Rule.ExeName[..^4] : Rule.ExeName;
    public string Path => Rule.ExePath;
    public bool Enabled => Rule.Enabled;
    public bool Connected { get; init; }
    public string MonitorText { get; init; } = "";
    public Visibility DisconnectedVisibility => Connected ? Visibility.Collapsed : Visibility.Visible;
    public string ModeText => Rule.Mode switch
    {
        PlacementMode.Maximized => "Maximizada",
        PlacementMode.Custom => "Personalizada",
        _ => "Normal",
    };
}

public class MainWindow : Window
{
    readonly Config _config;
    readonly ItemsControl MonitorsList, RulesList;
    readonly TextBlock EmptyText, ResultText;
    readonly CheckBox AutostartBox, AutoPlaceBox;
    readonly Button ApplyButton;

    public MainWindow()
    {
        Title = "WinPla";
        Width = 760; Height = 680; MinWidth = 560; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = Ui.Load<FrameworkElement>("MainWindow.xaml");
        Content = root;
        T N<T>(string name) where T : class => (T)root.FindName(name);
        MonitorsList = N<ItemsControl>("MonitorsList"); RulesList = N<ItemsControl>("RulesList");
        EmptyText = N<TextBlock>("EmptyText"); ResultText = N<TextBlock>("ResultText");
        AutostartBox = N<CheckBox>("AutostartBox"); AutoPlaceBox = N<CheckBox>("AutoPlaceBox");
        ApplyButton = N<Button>("ApplyButton");
        N<Button>("AddButton").Click += Add_Click;
        ApplyButton.Click += Apply_Click;
        // Los botones de cada fila viven en un DataTemplate: un solo manejador enrutado, según su Tag.
        RulesList.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Row_Click));
        _config = ConfigStore.Load();
        try { AutostartBox.IsChecked = Autostart.IsEnabled; } catch (Exception ex) { Log.Write("Autostart: " + ex); }
        AutoPlaceBox.IsChecked = _config.AutoPlacementEnabled;
        // Las casillas escuchan Checked/Unchecked y no Click: así también responden al teclado y a las
        // tecnologías de asistencia (UI Automation cambia el estado sin generar Click). Cada manejador
        // compara con el estado guardado, de modo que los cambios hechos por código no hacen nada.
        AutostartBox.Checked += Autostart_Click; AutostartBox.Unchecked += Autostart_Click;
        AutoPlaceBox.Checked += AutoPlace_Click; AutoPlaceBox.Unchecked += AutoPlace_Click;
        RulesList.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(Row_Toggled));
        RulesList.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(Row_Toggled));
        RefreshAll();
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
    }

    void OnDisplayChanged(object? s, EventArgs e) => Dispatcher.BeginInvoke(RefreshAll);

    void RefreshAll()
    {
        IReadOnlyList<MonitorInfo> monitors;
        try { monitors = Monitors.GetAll(); }
        catch (Exception ex) { Log.Write("Monitors.GetAll: " + ex); monitors = []; }
        MonitorsList.ItemsSource = monitors;

        RulesList.ItemsSource = _config.Rules.Select(r =>
        {
            var m = monitors.FirstOrDefault(x => x.Id == r.MonitorId);
            return new RuleRow
            {
                Rule = r,
                Icon = LoadIcon(r.ExePath),
                Connected = m != null,
                MonitorText = m?.Label ?? r.MonitorLabel,
            };
        }).ToList();
        EmptyText.Visibility = _config.Rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    static System.Windows.Media.ImageSource? LoadIcon(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return null;
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;
            var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch { return null; }
    }

    void Warn(string text, Exception ex)
    {
        Log.Write(text + ": " + ex);
        MessageBox.Show(this, text + "\n" + ex.Message, "WinPla", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    void Save()
    {
        try { ConfigStore.Save(_config); }
        catch (Exception ex) { Warn("No se pudo guardar la configuración.", ex); }
    }

    static Rule RuleOf(object sender) => ((RuleRow)((FrameworkElement)sender).DataContext).Rule;

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new RuleDialog(null, _config.Rules) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result == null) return;
        _config.Rules.Add(dlg.Result);
        Save();
        RefreshAll();
    }

    void Row_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement { Tag: string tag } el || el.DataContext is not RuleRow) return;
        if (tag == "edit") Edit_Click(el, e);
        else if (tag == "delete") Delete_Click(el, e);
    }

    void Row_Toggled(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not CheckBox { DataContext: RuleRow row } box) return;
        bool on = box.IsChecked == true;
        if (row.Rule.Enabled == on) return;   // cambio hecho por el binding al cargar la fila
        row.Rule.Enabled = on;
        Save();
    }

    void Edit_Click(object sender, RoutedEventArgs e)
    {
        var rule = RuleOf(sender);
        var dlg = new RuleDialog(rule, _config.Rules) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result == null) return;
        _config.Rules[_config.Rules.IndexOf(rule)] = dlg.Result;
        Save();
        RefreshAll();
    }

    void Delete_Click(object sender, RoutedEventArgs e)
    {
        var rule = RuleOf(sender);
        if (MessageBox.Show(this, $"¿Eliminar la regla de {rule.ExeName}?", "WinPla",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _config.Rules.Remove(rule);
        Save();
        RefreshAll();
    }

    void Autostart_Click(object sender, RoutedEventArgs e)
    {
        bool on = AutostartBox.IsChecked == true;
        try { if (Autostart.IsEnabled != on) Autostart.Set(on); }
        catch (Exception ex)
        {
            AutostartBox.IsChecked = !on;
            Warn("No se pudo cambiar el inicio con Windows.", ex);
        }
    }

    void AutoPlace_Click(object sender, RoutedEventArgs e)
    {
        bool on = AutoPlaceBox.IsChecked == true;
        if (_config.AutoPlacementEnabled == on) return;
        _config.AutoPlacementEnabled = on;
        Save();
    }

    async void Apply_Click(object sender, RoutedEventArgs e)
    {
        ApplyButton.IsEnabled = false;
        ResultText.Text = "Aplicando…";
        try
        {
            var results = await Task.Run(() => Engine.ApplyOnce(_config));
            var sb = new StringBuilder();
            foreach (var r in results)
            {
                string name = r.Rule.ExeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? r.Rule.ExeName[..^4] : r.Rule.ExeName;
                string title = string.IsNullOrWhiteSpace(r.Window?.Title) ? "" : $" ({r.Window!.Title})";
                sb.AppendLine($"{name}{title}: {StatusText(r.Status)}");
            }
            ResultText.Text = sb.Length == 0 ? "No hay reglas que aplicar." : sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            ResultText.Text = "";
            Warn("No se pudieron aplicar las reglas.", ex);
        }
        finally { ApplyButton.IsEnabled = true; }
    }

    static string StatusText(PlaceStatus s) => s switch
    {
        PlaceStatus.Placed => "colocada",
        PlaceStatus.AlreadyOk => "ya estaba en su sitio",
        PlaceStatus.WindowNotFound => "no está abierta",
        PlaceStatus.MonitorDisconnected => "su monitor no está conectado",
        PlaceStatus.AccessDenied => "se ejecuta como administrador; WinPla no puede moverla",
        _ => "no se pudo mover",
    };
}
