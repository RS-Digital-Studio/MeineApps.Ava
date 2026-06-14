using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels.Calculators;

namespace FinanzRechner.Views.Calculators;

public partial class AmortizationView : UserControl
{
    private AmortizationViewModel? _vm;

    // --- Header-Animation ---
    private DispatcherTimer? _headerTimer;
    private float _headerTime;
    private DateTime _lastFrameTime;

    public AmortizationView()
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

        if (DataContext is AmortizationViewModel vm)
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
            case nameof(_vm.AmortYearLabels):
            case nameof(_vm.AmortPrincipalData):
            case nameof(_vm.AmortInterestData):
            case nameof(_vm.HasResult):
                AmortBarCanvas?.InvalidateSurface();
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
    /// Koppelt den 60fps-Header-Timer an <see cref="AmortizationViewModel.IsHeaderActive"/>:
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
    /// Zeichnet den animierten Header-Hintergrund (gestapelte Balken).
    /// </summary>
    private void OnPaintHeaderBg(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        CalculatorHeaderRenderer.Render(canvas, bounds, _headerTime,
            CalculatorHeaderRenderer.CalculatorType.Amortization);
    }

    /// <summary>
    /// Zeichnet das Tilgungsplan-Balkendiagramm (Tilgung + Zinsen pro Jahr).
    /// </summary>
    private void OnPaintAmortBars(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not AmortizationViewModel vm
            || vm.AmortYearLabels == null || vm.AmortPrincipalData == null || vm.AmortInterestData == null) return;

        AmortizationBarVisualization.Render(canvas, bounds,
            vm.AmortYearLabels, vm.AmortPrincipalData, vm.AmortInterestData);
    }
}
