using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Material.Icons.Avalonia;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    // Tab-Icon/Label Referenzen für Highlighting
    private MaterialIcon?[] _tabIcons = new MaterialIcon?[5];
    private TextBlock?[] _tabLabels = new TextBlock?[5];

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnKeyDown;
        Focusable = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Tab-Icon/Label Referenzen cachen
        _tabIcons[0] = this.FindControl<MaterialIcon>("TabIconToday");
        _tabIcons[1] = this.FindControl<MaterialIcon>("TabIconWeek");
        _tabIcons[2] = this.FindControl<MaterialIcon>("TabIconCalendar");
        _tabIcons[3] = this.FindControl<MaterialIcon>("TabIconStatistics");
        _tabIcons[4] = this.FindControl<MaterialIcon>("TabIconSettings");

        _tabLabels[0] = this.FindControl<TextBlock>("TabLabelToday");
        _tabLabels[1] = this.FindControl<TextBlock>("TabLabelWeek");
        _tabLabels[2] = this.FindControl<TextBlock>("TabLabelCalendar");
        _tabLabels[3] = this.FindControl<TextBlock>("TabLabelStatistics");
        _tabLabels[4] = this.FindControl<TextBlock>("TabLabelSettings");

        // Initialer Tab-State
        UpdateTabHighlighting(0);
        UpdateTabIndicator(0);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Altes ViewModel abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm.MessageRequested -= OnMessage;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as MainViewModel;

        // Neues ViewModel anmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;
            _vm.MessageRequested += OnMessage;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentTab) && _vm != null)
        {
            UpdateTabHighlighting(_vm.CurrentTab);
            UpdateTabIndicator(_vm.CurrentTab);
        }
    }

    /// <summary>
    /// Aktiver Tab bekommt PrimaryBrush, alle anderen TextSecondaryBrush
    /// </summary>
    private void UpdateTabHighlighting(int activeTab)
    {
        for (int i = 0; i < 5; i++)
        {
            var brush = i == activeTab ? "PrimaryBrush" : "TextSecondaryBrush";

            if (_tabIcons[i] != null && Application.Current != null &&
                Application.Current.TryGetResource(brush, Avalonia.Styling.ThemeVariant.Default, out var res) &&
                res is IBrush b)
            {
                _tabIcons[i]!.Foreground = b;
                if (_tabLabels[i] != null)
                    _tabLabels[i]!.Foreground = b;
            }
        }
    }

    /// <summary>
    /// Bewegt den Tab-Indikator zum aktiven Tab (via translateX)
    /// </summary>
    private void UpdateTabIndicator(int activeTab)
    {
        var indicator = this.FindControl<Border>("TabIndicator");
        if (indicator == null) return;

        // Tab-Bereich berechnen: Der Canvas ist in einem Grid mit 5 gleichen Spalten
        // Wir nutzen die aktuelle Breite der Tab-Bar
        var tabBar = indicator.Parent;
        if (tabBar == null) return;

        // Offset berechnen: Canvas-Breite / 5 * activeTab + Zentrierung
        // Die Berechnung erfolgt bei LayoutUpdated falls Breite noch 0 ist
        var totalWidth = Bounds.Width;
        if (totalWidth < 10)
        {
            // Verzögert ausführen wenn noch nicht gelayoutet
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateTabIndicator(activeTab), Avalonia.Threading.DispatcherPriority.Render);
            return;
        }

        var tabWidth = totalWidth / 5.0;
        var offset = tabWidth * activeTab + (tabWidth - 48) / 2.0;
        indicator.RenderTransform = new TranslateTransform(offset, 0);
    }

    // === Keyboard Shortcuts (Desktop) ===

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm == null) return;

        switch (e.Key)
        {
            // F5 = Aktualisieren
            case Key.F5:
                _vm.LoadDataCommand.Execute(null);
                e.Handled = true;
                break;

            // Escape = Sub-Page schließen
            case Key.Escape:
                if (_vm.IsSubPageActive)
                {
                    _vm.GoBackCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            // Ziffern 1-5 = Tab-Navigation
            case Key.D1: _vm.SelectTodayTabCommand.Execute(null); e.Handled = true; break;
            case Key.D2: _vm.SelectWeekTabCommand.Execute(null); e.Handled = true; break;
            case Key.D3: _vm.SelectCalendarTabCommand.Execute(null); e.Handled = true; break;
            case Key.D4: _vm.SelectStatisticsTabCommand.Execute(null); e.Handled = true; break;
            case Key.D5: _vm.SelectSettingsTabCommand.Execute(null); e.Handled = true; break;

            // Ctrl+Z = Undo
            case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                if (_vm.IsUndoVisible)
                {
                    _vm.UndoLastActionCommand.Execute(null);
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnFloatingText(string text, string category)
    {
        var color = category switch
        {
            "success" => Color.Parse("#22C55E"),
            "overtime" => Color.Parse("#F59E0B"),
            "achievement" => Color.Parse("#FFD700"),
            "error" => Color.Parse("#F44336"),
            _ => Color.Parse("#3B82F6")
        };
        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;
        // Position 40-50% der Höhe → gut sichtbar auf allen Bildschirmgrößen
        FloatingTextCanvas.ShowFloatingText(text, w * (0.2 + _rng.NextDouble() * 0.6), Math.Max(100, h * 0.45), color, 18);
    }

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }

    private void OnMessage(string title, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[WorkTimePro] {title}: {message}");

        // Fehlermeldungen als FloatingText anzeigen (Titel + Nachricht für Kontext)
        var color = Color.Parse("#F44336"); // Rot für Fehler
        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;
        // Nachricht kürzen falls zu lang (max 80 Zeichen für FloatingText)
        var displayText = string.IsNullOrWhiteSpace(message) ? title : message;
        if (displayText.Length > 80)
            displayText = displayText[..77] + "...";
        FloatingTextCanvas.ShowFloatingText(displayText, w * 0.5, Math.Max(80, h * 0.3), color, 14);
    }
}
