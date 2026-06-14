using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Material.Icons.Avalonia;
using SkiaSharp;
using WorkTimePro.Graphics;
using WorkTimePro.ViewModels;

namespace WorkTimePro.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    // Tab-Icon/Label Referenzen fuer Highlighting
    private MaterialIcon?[] _tabIcons = new MaterialIcon?[5];
    private TextBlock?[] _tabLabels = new TextBlock?[5];

    // Animierter Hintergrund (Professional Dashboard)
    private readonly WorkspaceBackgroundRenderer _backgroundRenderer = new();
    private DispatcherTimer? _bgTimer;
    private float _bgTime;
    private Window? _hostWindow; // Desktop: Activated/Deactivated für Hintergrund-Detektion

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnKeyDown;
        // Reine View-State-Aktion (Overlay schließen) — im Code-Behind verdrahtet statt als
        // XAML-Click, konsistent mit der MessageRequested→OnMessage-Code-Behind-Logik.
        MessageOverlayOk.Click += OnMessageOverlayOk;
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

        // Initialer Tab-State: aktuellen Tab aus dem VM (nicht hart 0 — sonst Desync bei Re-Attach)
        var tab = _vm?.CurrentTab ?? 0;
        UpdateTabHighlighting(tab);
        UpdateTabIndicator(tab);

        // Bei Größenänderung (v.a. Desktop-Resize) den Indikator neu positionieren,
        // sonst driftet er aus der aktiven Tab-Spalte.
        SizeChanged += OnMainViewSizeChanged;

        // Hintergrund-Render-Loop starten (~5fps)
        _bgTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _bgTimer.Tick += OnBackgroundTimerTick;
        _bgTimer.Start();

        // Desktop-Lifecycle: Timer pausieren wenn Fenster nicht aktiv (Fokus weg / minimiert).
        // Spart Akku/CPU — Renderer läuft sonst weiter obwohl niemand das Pixel sieht.
        // Auf Android existiert kein direktes "Window Deactivated"-Event auf TopLevel-Ebene;
        // dort übernimmt die Activity-Lifecycle-Pause im Manifest die Drosselung.
        _hostWindow = TopLevel.GetTopLevel(this) as Window;
        if (_hostWindow != null)
        {
            _hostWindow.Activated += OnTopLevelActivated;
            _hostWindow.Deactivated += OnTopLevelDeactivated;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Window-Lifecycle-Hooks abmelden
        if (_hostWindow != null)
        {
            _hostWindow.Activated -= OnTopLevelActivated;
            _hostWindow.Deactivated -= OnTopLevelDeactivated;
            _hostWindow = null;
        }

        // Hintergrund-Render-Loop stoppen und Renderer freigeben
        if (_bgTimer != null)
        {
            _bgTimer.Stop();
            _bgTimer.Tick -= OnBackgroundTimerTick;
            _bgTimer = null;
        }
        _backgroundRenderer.Dispose();

        // Symmetrische Abmeldung der VM-Events. Bei Singleton-VMs kein echtes Leak,
        // aber konsistent zur Subscription in OnDataContextChanged und robust gegen
        // zukünftige Änderungen am ViewModel-Lifetime.
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm.MessageRequested -= OnMessage;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        DataContextChanged -= OnDataContextChanged;
        KeyDown -= OnKeyDown;
        SizeChanged -= OnMainViewSizeChanged;
        // Falls der Indikator noch auf das erste Layout wartet: Handler abmelden (kein Leak bei Re-Attach)
        LayoutUpdated -= OnLayoutUpdatedForIndicator;
    }

    private void OnMainViewSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Indikator-Offset hängt an Bounds.Width → bei Resize neu setzen.
        UpdateTabIndicator(_vm?.CurrentTab ?? 0);
    }

    // =====================================================================
    // Hintergrund-Animation (~5fps)
    // =====================================================================

    private void OnBackgroundTimerTick(object? sender, EventArgs e)
    {
        const float deltaTime = 0.2f; // 200ms Intervall
        _bgTime += deltaTime;
        _backgroundRenderer.Update(deltaTime);
        BackgroundCanvas?.InvalidateSurface();
    }

    private void OnTopLevelActivated(object? sender, EventArgs e)
    {
        // App wieder im Vordergrund → Renderer reaktivieren
        _bgTimer?.Start();
    }

    private void OnTopLevelDeactivated(object? sender, EventArgs e)
    {
        // App im Hintergrund → Render-Loop pausieren (Akku sparen)
        _bgTimer?.Stop();
    }

    private void OnBackgroundPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();
        _backgroundRenderer.Render(canvas, canvas.LocalClipBounds, _bgTime);
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

    // Merkt sich den Ziel-Tab, falls der Indikator vor dem ersten Layout positioniert werden
    // soll (Bounds noch 0). Nach dem ersten gültigen Layout wird er einmalig nachgezogen.
    private int _pendingIndicatorTab = -1;

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

        var totalWidth = Bounds.Width;
        if (totalWidth < 10)
        {
            // Bounds noch nicht bekannt (Aufruf aus OnAttachedToVisualTree, vor dem ersten
            // Layout-Pass). NICHT auf DispatcherPriority.Render reposten: ein vor der ersten
            // Frame geposteter Render-Prioritäts-Job verklemmt auf Android (Avalonia 12) die
            // Erstellung der TopLevel-SurfaceView → kein Compositor-Tick → erste Frame wird nie
            // committed → App haengt deterministisch im System-Splash (Layout + Loading-Pipeline
            // laufen durch, aber nichts wird gerendert). Da Bounds.Width ohne Render nie >= 10
            // wird, repostet sich der Render-Job zudem endlos und haelt den Render-Pfad besetzt.
            // Stattdessen einmalig auf das nächste LayoutUpdated warten (idiomatisch, deadlock-frei).
            _pendingIndicatorTab = activeTab;
            LayoutUpdated -= OnLayoutUpdatedForIndicator;
            LayoutUpdated += OnLayoutUpdatedForIndicator;
            return;
        }

        var tabWidth = totalWidth / 5.0;
        var offset = tabWidth * activeTab + (tabWidth - 48) / 2.0;
        indicator.RenderTransform = new TranslateTransform(offset, 0);
    }

    /// <summary>
    /// Zieht den Tab-Indikator nach, sobald nach dem Attach das erste gültige Layout vorliegt.
    /// Meldet sich nach dem ersten erfolgreichen Lauf selbst wieder ab (Einmal-Trigger).
    /// </summary>
    private void OnLayoutUpdatedForIndicator(object? sender, EventArgs e)
    {
        if (Bounds.Width < 10) return; // noch kein gültiges Layout — auf nächstes Update warten
        LayoutUpdated -= OnLayoutUpdatedForIndicator;
        if (_pendingIndicatorTab >= 0)
        {
            var tab = _pendingIndicatorTab;
            _pendingIndicatorTab = -1;
            UpdateTabIndicator(tab);
        }
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

        // Persistentes Overlay statt fluechtigem FloatingText: voller Text, muss aktiv mit OK
        // bestaetigt werden — so geht eine fehlgeschlagene Aktion (z.B. Speichern) nicht verloren.
        MessageOverlayTitle.Text = title;
        MessageOverlayText.Text = message;
        MessageOverlayText.IsVisible = !string.IsNullOrWhiteSpace(message);
        MessageOverlay.IsVisible = true;
    }

    private void OnMessageOverlayOk(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MessageOverlay.IsVisible = false;
    }
}
