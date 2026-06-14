using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels;
using SkiaSharp;

namespace FinanzRechner.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _vm;
    private readonly Random _rng = new();

    // App-Pause-Zustand (Android-Lifecycle via MainViewModel.PauseStateChanged). Hat Vorrang vor
    // der Overlay-Sichtbarkeit: im Hintergrund läuft der Render-Timer nie. Bei Resume entscheidet
    // die bestehende UpdateRenderTimerState-Logik (verdeckt oder nicht).
    private bool _appPaused;

    // SkiaSharp Hintergrund-Renderer
    private readonly FinanceBackgroundRenderer _backgroundRenderer = new();
    private DispatcherTimer? _renderTimer;
    private float _renderTime;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartRenderTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Render-Timer stoppen und Renderer freigeben
        if (_renderTimer != null)
        {
            _renderTimer.Stop();
            _renderTimer.Tick -= OnRenderTimerTick;
            _renderTimer = null;
        }
        _backgroundRenderer.Dispose();

        // Events sauber abmelden
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.PauseStateChanged -= OnPauseStateChanged;
            _vm = null;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.PauseStateChanged -= OnPauseStateChanged;
        }

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;

            // Render-Timer pausieren wenn ein Rechner-/Sub-Page-Overlay den Hintergrund verdeckt.
            _vm.PropertyChanged += OnVmPropertyChanged;

            // App-Pause/Resume (Android-Lifecycle): im Hintergrund den Render-Timer stoppen.
            _vm.PauseStateChanged += OnPauseStateChanged;

            // Timer-Zustand sofort an den aktuellen VM-Zustand angleichen.
            UpdateRenderTimerState();
        }
    }

    /// <summary>
    /// App-Pause/Resume (Android-Lifecycle via MainViewModel). Im Hintergrund den dekorativen
    /// ~5fps-Hintergrund-Render-Timer anhalten (niemand sieht ihn → Akku). Bei Resume nicht blind
    /// starten, sondern die bestehende Sichtbarkeits-Logik entscheiden lassen (ein Overlay kann
    /// den Hintergrund weiterhin verdecken).
    /// </summary>
    private void OnPauseStateChanged(bool isPaused)
    {
        _appPaused = isPaused;
        UpdateRenderTimerState();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsCalculatorOpen) or nameof(MainViewModel.IsSubPageOpen))
            UpdateRenderTimerState();
    }

    /// <summary>
    /// Einziger Entscheidungspunkt für den Background-Render-Timer. Der Timer läuft nur, wenn die
    /// App im Vordergrund ist UND kein deckendes Overlay den Hintergrund verdeckt:
    /// - App im Hintergrund (<see cref="_appPaused"/>): immer aus (Akku — niemand sieht den Hintergrund).
    /// - Rechner-/Sub-Page-Overlay offen (ZIndex 50/60, deckender Hintergrund): aus (verdeckt → keine GPU-Arbeit).
    /// App-Pause hat Vorrang; bei Resume entscheidet die Overlay-Sichtbarkeit.
    /// </summary>
    private void UpdateRenderTimerState()
    {
        if (_renderTimer == null || _vm == null) return;

        bool overlayOpen = _vm.IsCalculatorOpen || _vm.IsSubPageOpen;
        bool shouldRun = !_appPaused && !overlayOpen;

        if (!shouldRun && _renderTimer.IsEnabled)
            _renderTimer.Stop();
        else if (shouldRun && !_renderTimer.IsEnabled)
            _renderTimer.Start();
    }

    // =====================================================================
    // Render-Timer (~5fps fuer dezenten animierten Hintergrund)
    // =====================================================================

    private void StartRenderTimer()
    {
        if (_renderTimer != null) return;

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) }; // ~5fps
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        _renderTime += 0.2f; // 200ms Delta
        _backgroundRenderer.Update(0.2f);
        BackgroundCanvas?.InvalidateSurface();
    }

    // =====================================================================
    // SkiaSharp Paint-Handler
    // =====================================================================

    /// <summary>
    /// Zeichnet den animierten Financial Data Stream Hintergrund (5 Layer).
    /// </summary>
    private void OnBackgroundPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        _backgroundRenderer.Render(canvas, bounds, _renderTime);
    }

    // =====================================================================
    // Game Juice Events (FloatingText + Celebration)
    // =====================================================================

    private void OnFloatingText(string text, string category)
    {
        var color = category switch
        {
            "success" => Color.Parse("#22C55E"),
            "income" => Color.Parse("#22C55E"),
            "expense" => Color.Parse("#EF4444"),
            _ => Color.Parse("#3B82F6")
        };
        var w = FloatingTextCanvas.Bounds.Width;
        if (w < 10) w = 300;
        var h = FloatingTextCanvas.Bounds.Height;
        if (h < 10) h = 400;
        FloatingTextCanvas.ShowFloatingText(text, w * (0.2 + _rng.NextDouble() * 0.6), h * 0.4, color, 16);
    }

    private void OnCelebration()
    {
        CelebrationCanvas.ShowConfetti();
    }
}
