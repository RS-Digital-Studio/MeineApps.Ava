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
            _vm = null;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.FloatingTextRequested -= OnFloatingText;
            _vm.CelebrationRequested -= OnCelebration;
        }

        _vm = DataContext as MainViewModel;

        if (_vm != null)
        {
            _vm.FloatingTextRequested += OnFloatingText;
            _vm.CelebrationRequested += OnCelebration;
        }
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
