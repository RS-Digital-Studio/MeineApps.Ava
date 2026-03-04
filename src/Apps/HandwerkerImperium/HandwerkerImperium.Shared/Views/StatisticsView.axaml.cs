using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views;

public partial class StatisticsView : UserControl
{
    private PrestigeRoadmapRenderer? _roadmapRenderer;
    private DispatcherTimer? _glowTimer;
    private float _animTime;
    private SKCanvasView? _roadmapCanvas;

    public StatisticsView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _roadmapCanvas = this.FindControl<SKCanvasView>("RoadmapCanvas");
        if (_roadmapCanvas == null) return;

        _roadmapRenderer = new PrestigeRoadmapRenderer();
        _roadmapCanvas.PaintSurface += OnRoadmapPaintSurface;

        // Langsamer Timer für Glow-Puls (10fps, energiesparend)
        _glowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _glowTimer.Tick += OnGlowTimerTick;
        _glowTimer.Start();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (_glowTimer != null)
        {
            _glowTimer.Stop();
            _glowTimer.Tick -= OnGlowTimerTick;
            _glowTimer = null;
        }

        if (_roadmapCanvas != null)
            _roadmapCanvas.PaintSurface -= OnRoadmapPaintSurface;
        _roadmapCanvas = null;

        _roadmapRenderer?.Dispose();
        _roadmapRenderer = null;
    }

    private void OnGlowTimerTick(object? sender, EventArgs e)
    {
        _animTime += 0.1f;
        _roadmapCanvas?.InvalidateSurface();
    }

    /// <summary>
    /// Rendert die Prestige-Tier-Roadmap via SkiaSharp.
    /// </summary>
    private void OnRoadmapPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_roadmapRenderer == null || DataContext is not StatisticsViewModel vm) return;

        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        canvas.Clear(SKColors.Transparent);

        // Bounds für HitTest cachen (physische Pixel)
        _lastCanvasBounds = bounds;

        _roadmapRenderer.Render(
            canvas, bounds,
            vm.RoadmapCurrentTier,
            vm.RoadmapTierCounts,
            vm.NextTierProgress,
            _animTime);
    }

    private SKRect _lastCanvasBounds;

    /// <summary>
    /// Tap auf Roadmap-Medaille → Detail-Popup für den gewählten Tier.
    /// DPI-Skalierung: PointerPressed gibt logische Koordinaten, SkiaSharp rendert in physischen Pixeln.
    /// </summary>
    private void OnRoadmapTapped(object? sender, PointerPressedEventArgs e)
    {
        if (_roadmapRenderer == null || DataContext is not StatisticsViewModel vm) return;
        if (_lastCanvasBounds.Width <= 0) return;

        var pos = e.GetPosition(_roadmapCanvas);
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        float x = (float)(pos.X * scale);
        float y = (float)(pos.Y * scale);

        int tierIndex = _roadmapRenderer.HitTest(x, y, _lastCanvasBounds);
        if (tierIndex >= 0)
        {
            vm.ShowTierDetail(tierIndex);
            e.Handled = true;
        }
    }
}
