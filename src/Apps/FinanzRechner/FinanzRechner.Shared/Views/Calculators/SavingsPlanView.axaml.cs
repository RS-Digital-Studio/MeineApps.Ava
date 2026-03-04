using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels.Calculators;

namespace FinanzRechner.Views.Calculators;

public partial class SavingsPlanView : UserControl
{
    // --- Header-Animation ---
    private DispatcherTimer? _headerTimer;
    private float _headerTime;
    private DateTime _lastFrameTime;

    public SavingsPlanView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is SavingsPlanViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(vm.ChartXLabels):
                    case nameof(vm.ChartArea1Data):
                    case nameof(vm.ChartArea2Data):
                    case nameof(vm.HasResult):
                        StackedAreaCanvas?.InvalidateSurface();
                        break;
                }
            };
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        StartHeaderTimer();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
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
    /// Zeichnet den animierten Header-Hintergrund (Treppen-Stufen).
    /// </summary>
    private void OnPaintHeaderBg(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        CalculatorHeaderRenderer.Render(canvas, bounds, _headerTime,
            CalculatorHeaderRenderer.CalculatorType.SavingsPlan);
    }

    /// <summary>
    /// Zeichnet das Sparplan-Wachstum als gestapeltes Flächendiagramm.
    /// </summary>
    private void OnPaintStackedArea(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not SavingsPlanViewModel vm
            || vm.ChartXLabels == null || vm.ChartArea1Data == null || vm.ChartArea2Data == null) return;

        StackedAreaVisualization.Render(canvas, bounds,
            vm.ChartXLabels, vm.ChartArea1Data, vm.ChartArea2Data,
            new SKColor(0x3B, 0x82, 0xF6), // Blau (Einzahlungen)
            new SKColor(0x22, 0xC5, 0x5E), // Grün (Zinsen)
            "", "");
    }
}
