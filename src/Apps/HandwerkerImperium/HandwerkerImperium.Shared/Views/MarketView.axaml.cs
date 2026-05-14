using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.ViewModels;
using SkiaSharp;

namespace HandwerkerImperium.Views;

/// <summary>
/// V7 (): Material-Markt-Seite.
/// ViewLocator findet diese ueber Namespace-Konvention (.Views.MarketView fuer .ViewModels.MarketViewModel).
///
/// Render-Loop (15 fps fuer Pulse-Animation des "Jetzt"-Indikators) wird nur aktiv wenn das
/// Heatmap-Detail-Panel sichtbar ist — sonst keine CPU/GC-Last.
/// </summary>
public partial class MarketView : UserControl
{
    private readonly MarketChartRenderer _chartRenderer = new();
    private SKCanvasView? _heatmapCanvas;
    private DispatcherTimer? _renderTimer;
    private MarketViewModel? _subscribedVm;
    private DateTime _lastFrameTime;
    private bool _disposed;

    public MarketView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_disposed) return;
        _disposed = true;

        StopRenderTimer();
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
            _subscribedVm.ChartInvalidated -= OnChartInvalidated;
            _subscribedVm = null;
        }
        _chartRenderer.Dispose();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
            _subscribedVm.ChartInvalidated -= OnChartInvalidated;
            _subscribedVm = null;
        }
        if (DataContext is MarketViewModel vm)
        {
            _subscribedVm = vm;
            vm.PropertyChanged += OnVmPropertyChanged;
            vm.ChartInvalidated += OnChartInvalidated;
            // Falls beim Reattach bereits ein Detail offen ist, Timer starten
            UpdateRenderTimerForDetailVisibility();
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MarketViewModel.SelectedEntry)
                          or nameof(MarketViewModel.HasSelectedEntry))
        {
            UpdateRenderTimerForDetailVisibility();
            _heatmapCanvas?.InvalidateSurface();
        }
    }

    private void OnChartInvalidated()
        => Dispatcher.UIThread.Post(() => _heatmapCanvas?.InvalidateSurface());

    private void UpdateRenderTimerForDetailVisibility()
    {
        if (_subscribedVm?.HasSelectedEntry == true)
            StartRenderTimer();
        else
            StopRenderTimer();
    }

    private void StartRenderTimer()
    {
        if (_renderTimer != null) return;
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(66) }; // ~15 fps
        _renderTimer.Tick += (_, _) => _heatmapCanvas?.InvalidateSurface();
        _renderTimer.Start();
        _lastFrameTime = DateTime.UtcNow;
    }

    private void StopRenderTimer()
    {
        if (_renderTimer == null) return;
        _renderTimer.Stop();
        _renderTimer = null;
    }

    private void OnPaintHeatmap(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_disposed) return;
        if (_heatmapCanvas == null && sender is SKCanvasView cv)
            _heatmapCanvas = cv;

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (_subscribedVm?.SelectedEntry == null) return;

        var series = _subscribedVm.SelectedEntryPriceSeries;
        if (series == null) return;

        var now = DateTime.UtcNow;
        var deltaSeconds = (float)Math.Max(0, (now - _lastFrameTime).TotalSeconds);
        _lastFrameTime = now;

        _chartRenderer.Render(canvas, bounds, series, _subscribedVm.SelectedEntryCurrentHour, deltaSeconds);
    }
}
