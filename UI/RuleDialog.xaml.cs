using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WinPla;

public class RuleDialog : Window
{
    sealed record MonItem(string Id, string Label, MonitorInfo? Info)
    {
        public override string ToString() => Label;
    }

    readonly Rule? _existing;
    readonly IReadOnlyList<Rule> _all;
    string _exeName = "", _exePath = "";
    readonly TextBox AppBox, XBox, YBox, WBox, HBox;
    readonly ComboBox WindowsBox, StartupBox, MonitorBox;
    readonly RadioButton MaxRadio, NormalRadio, CustomRadio;
    readonly FrameworkElement CustomPanel;

    /// <summary>Resulting rule if the dialog was accepted (new instance).</summary>
    public Rule? Result { get; private set; }

    public RuleDialog(Rule? existing, IReadOnlyList<Rule> all)
    {
        Width = 520; MinWidth = 460; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false; ResizeMode = ResizeMode.CanResizeWithGrip;
        var root = Ui.Load<FrameworkElement>("RuleDialog.xaml");
        Content = root;
        T N<T>(string name) where T : class => (T)root.FindName(name);
        AppBox = N<TextBox>("AppBox"); XBox = N<TextBox>("XBox"); YBox = N<TextBox>("YBox");
        WBox = N<TextBox>("WBox"); HBox = N<TextBox>("HBox");
        WindowsBox = N<ComboBox>("WindowsBox"); StartupBox = N<ComboBox>("StartupBox"); MonitorBox = N<ComboBox>("MonitorBox");
        MaxRadio = N<RadioButton>("MaxRadio"); NormalRadio = N<RadioButton>("NormalRadio"); CustomRadio = N<RadioButton>("CustomRadio");
        CustomPanel = N<FrameworkElement>("CustomPanel");
        N<Button>("BrowseButton").Click += Browse_Click;
        N<Button>("UseCurrentButton").Click += UseCurrent_Click;
        N<Button>("OkButton").Click += Ok_Click;
        WindowsBox.SelectionChanged += Window_Selected;
        StartupBox.SelectionChanged += Startup_Selected;
        foreach (var r in new[] { MaxRadio, NormalRadio, CustomRadio }) r.Checked += Mode_Changed;
        _existing = existing;
        _all = all;
        Title = existing == null ? "Add application" : "Edit application";

        try
        {
            WindowsBox.ItemsSource = AppWindows.GetAll()
                .GroupBy(w => w.ExeName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(w => w.ExeName, StringComparer.OrdinalIgnoreCase)
                .Select(w => new WinItem(w))
                .ToList();
        }
        catch (Exception ex) { Log.Write("AppWindows.GetAll: " + ex); }

        try
        {
            var apps = StartupApps.GetAll().Select(a => new StartupItem(a)).ToList();
            StartupBox.ItemsSource = apps;
            StartupBox.IsEnabled = apps.Count > 0;
        }
        catch (Exception ex) { StartupBox.IsEnabled = false; Log.Write("StartupApps.GetAll: " + ex); }

        var items = new List<MonItem>();
        try { items.AddRange(Monitors.GetAll().Select(m => new MonItem(m.Id, $"{m.Label} — {m.Name}", m))); }
        catch (Exception ex) { Log.Write("Monitors.GetAll: " + ex); }
        if (existing != null && items.All(i => i.Id != existing.MonitorId))
            items.Insert(0, new MonItem(existing.MonitorId, existing.MonitorLabel + " (disconnected)", null));
        MonitorBox.ItemsSource = items;

        if (existing != null)
        {
            SetApp(existing.ExeName, existing.ExePath);
            MonitorBox.SelectedItem = items.FirstOrDefault(i => i.Id == existing.MonitorId);
            (existing.Mode switch
            {
                PlacementMode.Custom => CustomRadio,
                PlacementMode.Normal => NormalRadio,
                _ => MaxRadio,
            }).IsChecked = true;
            XBox.Text = existing.X.ToString();
            YBox.Text = existing.Y.ToString();
            if (existing.Width > 0) WBox.Text = existing.Width.ToString();
            if (existing.Height > 0) HBox.Text = existing.Height.ToString();
        }
        else if (items.Count > 0) MonitorBox.SelectedIndex = 0;
    }

    sealed class WinItem(WindowInfo w)
    {
        public WindowInfo Info { get; } = w;
        public override string ToString()
        {
            string n = Info.ExeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Info.ExeName[..^4] : Info.ExeName;
            return string.IsNullOrWhiteSpace(Info.Title) ? n : $"{n} — {Info.Title}";
        }
    }

    sealed class StartupItem(StartupApp a)
    {
        public StartupApp App { get; } = a;
        public override string ToString()
        {
            string n = App.ExeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? App.ExeName[..^4] : App.ExeName;
            return App.Name.Equals(n, StringComparison.OrdinalIgnoreCase) ? App.Name : $"{App.Name} — {App.ExeName}";
        }
    }

    // Deselecting another drop-down from code raises its SelectionChanged with SelectedItem == null: it does nothing, so there is no loop.
    void Startup_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (StartupBox.SelectedItem is not StartupItem s) return;
        SetApp(s.App.ExeName, s.App.ExePath);
        WindowsBox.SelectedIndex = -1;
    }

    void SetApp(string exeName, string exePath)
    {
        _exeName = exeName;
        _exePath = exePath;
        AppBox.Text = string.IsNullOrEmpty(exePath) ? exeName : $"{exeName}   ({exePath})";
    }

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Applications (*.exe)|*.exe", Title = "Choose an application" };
        if (dlg.ShowDialog(this) != true) return;
        SetApp(System.IO.Path.GetFileName(dlg.FileName), dlg.FileName);
        WindowsBox.SelectedIndex = -1;
        StartupBox.SelectedIndex = -1;
    }

    void Window_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (WindowsBox.SelectedItem is not WinItem w) return;
        SetApp(w.Info.ExeName, w.Info.ExePath);
        StartupBox.SelectedIndex = -1;
    }

    void Mode_Changed(object sender, RoutedEventArgs e)
    {
        CustomPanel.Visibility = CustomRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    void UseCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_exeName == "") { Info("Choose the application first."); return; }
        try
        {
            var win = AppWindows.GetAll().FirstOrDefault(w => w.ExeName.Equals(_exeName, StringComparison.OrdinalIgnoreCase));
            if (win == null) { Info($"{_exeName} isn't open. Open it and try again."); return; }
            var r = AppWindows.GetRect(win.Handle);
            if (r == null) { Info("Couldn't read the window's position."); return; }
            int cx = r.Value.X + r.Value.Width / 2, cy = r.Value.Y + r.Value.Height / 2;
            var mon = Monitors.GetAll().FirstOrDefault(m =>
                cx >= m.Bounds.X && cx < m.Bounds.X + m.Bounds.Width && cy >= m.Bounds.Y && cy < m.Bounds.Y + m.Bounds.Height);
            if (mon == null) { Info("The window isn't on any connected monitor."); return; }
            MonitorBox.SelectedItem = ((IEnumerable<MonItem>)MonitorBox.ItemsSource).FirstOrDefault(i => i.Id == mon.Id);
            XBox.Text = (r.Value.X - mon.WorkArea.X).ToString();
            YBox.Text = (r.Value.Y - mon.WorkArea.Y).ToString();
            WBox.Text = r.Value.Width.ToString();
            HBox.Text = r.Value.Height.ToString();
        }
        catch (Exception ex)
        {
            Log.Write("UseCurrent: " + ex);
            Info("Couldn't read the window.");
        }
    }

    void Info(string text) => MessageBox.Show(this, text, "WinPla", MessageBoxButton.OK, MessageBoxImage.Information);

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_exeName == "") { Info("Choose an application."); return; }
        if (MonitorBox.SelectedItem is not MonItem mon) { Info("Choose a monitor."); return; }
        if (_all.Any(r => r != _existing && r.ExeName.Equals(_exeName, StringComparison.OrdinalIgnoreCase)))
        { Info($"There's already a rule for {_exeName}. Edit it instead of adding another one."); return; }

        var rule = new Rule
        {
            ExeName = _exeName,
            ExePath = _exePath,
            MonitorId = mon.Id,
            MonitorLabel = mon.Info?.Label ?? _existing?.MonitorLabel ?? "",
            Enabled = _existing?.Enabled ?? true,
            Mode = CustomRadio.IsChecked == true ? PlacementMode.Custom
                 : NormalRadio.IsChecked == true ? PlacementMode.Normal : PlacementMode.Maximized,
        };
        if (rule.Mode == PlacementMode.Custom)
        {
            if (!int.TryParse(XBox.Text, out int x) || !int.TryParse(YBox.Text, out int y)
                || !int.TryParse(WBox.Text, out int w) || !int.TryParse(HBox.Text, out int h))
            { Info("X, Y, Width and Height must be whole numbers."); return; }
            if (w <= 0 || h <= 0) { Info("Width and Height must be greater than 0."); return; }
            (rule.X, rule.Y, rule.Width, rule.Height) = (x, y, w, h);
        }
        Result = rule;
        DialogResult = true;
    }
}
