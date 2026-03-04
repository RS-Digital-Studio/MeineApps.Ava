using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels.Calculators;

namespace FinanzRechner.Views.Calculators;

public partial class CompoundInterestView : UserControl
{
    // --- Header-Animation ---
    private DispatcherTimer? _headerTimer;
    private float _headerTime;
    private DateTime _lastFrameTime;

    public CompoundInterestView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is CompoundInterestViewModel vm)
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

    /// <summary>
    /// Startet den Animations-Timer für den Header-Hintergrund.
    /// </summary>
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

    /// <summary>
    /// Stoppt den Animations-Timer.
    /// </summary>
    private void StopHeaderTimer()
    {
        if (_headerTimer != null)
        {
            _headerTimer.Tick -= OnHeaderTimerTick;
            _headerTimer.Stop();
            _headerTimer = null;
        }
    }

    /// <summary>
    /// Timer-Tick: Aktualisiert Zeit und fordert Canvas-Neuzeichnung an.
    /// </summary>
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
    /// Zeichnet den animierten Header-Hintergrund (Exponentialkurve).
    /// </summary>
    private void OnPaintHeaderBg(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        CalculatorHeaderRenderer.Render(canvas, bounds, _headerTime,
            CalculatorHeaderRenderer.CalculatorType.CompoundInterest);
    }

    /// <summary>
    /// Zeichnet das Kapitalwachstum als gestapeltes Flächendiagramm.
    /// </summary>
    private void OnPaintStackedArea(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not CompoundInterestViewModel vm
            || vm.ChartXLabels == null || vm.ChartArea1Data == null || vm.ChartArea2Data == null) return;

        StackedAreaVisualization.Render(canvas, bounds,
            vm.ChartXLabels, vm.ChartArea1Data, vm.ChartArea2Data,
            new SKColor(0x3B, 0x82, 0xF6), // Blau (Kapital)
            new SKColor(0x22, 0xC5, 0x5E), // Grün (Zinsen)
            "", "");
    }
}
