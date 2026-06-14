using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels.Calculators;

namespace FinanzRechner.Views.Calculators;

public partial class YieldView : UserControl
{
    private YieldViewModel? _vm;

    // --- Header-Animation ---
    private DispatcherTimer? _headerTimer;
    private float _headerTime;
    private DateTime _lastFrameTime;

    public YieldView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        if (DataContext is YieldViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
            UpdateHeaderTimerState();
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(_vm.DonutSegments):
            case nameof(_vm.HasResult):
                DonutCanvas?.InvalidateSurface();
                break;
            case nameof(_vm.IsHeaderActive):
                UpdateHeaderTimerState();
                break;
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        UpdateHeaderTimerState();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        StopHeaderTimer();
    }

    /// <summary>
    /// Koppelt den 60fps-Header-Timer an <see cref="YieldViewModel.IsHeaderActive"/>:
    /// Er läuft nur, wenn genau dieser Rechner offen, sichtbar und die App im Vordergrund ist.
    /// Analog zum HomeView-IsHomeActive-Muster — sonst tickten alle 6 Rechner-Header parallel
    /// (Avalonia 12 detacht IsVisible=False-Elemente nicht aus dem Visual Tree).
    /// </summary>
    private void UpdateHeaderTimerState()
    {
        var shouldRun = _vm?.IsHeaderActive == true;
        if (shouldRun && _headerTimer == null)
            StartHeaderTimer();
        else if (!shouldRun && _headerTimer != null)
            StopHeaderTimer();
    }

    private void StartHeaderTimer()
    {
        _headerTimer?.Stop();
        _lastFrameTime = DateTime.UtcNow;

        _headerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _headerTimer.Tick += OnHeaderTimerTick;
        _headerTimer.Start();
    }

    private void StopHeaderTimer()
    {
        if (_headerTimer != null)
        {
            _headerTimer.Tick -= OnHeaderTimerTick;
            _headerTimer.Stop();
            _headerTimer = null;
        }
    }

    private void OnHeaderTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;
        deltaTime = Math.Min(deltaTime, 0.1f);
        _headerTime += deltaTime;

        HeaderBgCanvas?.InvalidateSurface();
    }

    /// <summary>
    /// Zeichnet den animierten Header-Hintergrund (Trendlinie mit Glow).
    /// </summary>
    private void OnPaintHeaderBg(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        CalculatorHeaderRenderer.Render(canvas, bounds, _headerTime,
            CalculatorHeaderRenderer.CalculatorType.Yield);
    }

    /// <summary>
    /// Zeichnet den Rendite-Donut (Investition vs. Rendite).
    /// </summary>
    private void OnPaintDonut(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not YieldViewModel vm || vm.DonutSegments == null
            || vm.DonutSegments.Length == 0) return;

        DonutChartVisualization.Render(canvas, bounds, vm.DonutSegments,
            innerRadiusFraction: 0.5f, showLabels: true, showLegend: false,
            startAngle: -90f);
    }
}
