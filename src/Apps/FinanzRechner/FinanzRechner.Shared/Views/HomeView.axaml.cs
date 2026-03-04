using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels;

namespace FinanzRechner.Views;

public partial class HomeView : UserControl
{
    private MainViewModel? _vm;

    // --- Dashboard-Hintergrund Animation ---
    private DispatcherTimer? _dashboardTimer;
    private float _dashboardTime;
    private DateTime _lastFrameTime;

    public HomeView()
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

        if (DataContext is MainViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(_vm.HomeExpenseSegments):
            case nameof(_vm.HasHomeChartData):
                ExpenseDonutCanvas?.InvalidateSurface();
                break;
            case nameof(_vm.SparklineValues):
            case nameof(_vm.HasSparklineData):
                SparklineCanvas?.InvalidateSurface();
                break;
            case nameof(_vm.BudgetRings):
            case nameof(_vm.HasBudgetRings):
                BudgetMiniRingCanvas?.InvalidateSurface();
                break;
        }
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Dashboard-Hintergrund-Timer starten (~60fps)
        StartDashboardTimer();

        if (DataContext is MainViewModel vm)
            await vm.OnAppearingAsync();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Timer stoppen wenn View nicht sichtbar
        StopDashboardTimer();
    }

    /// <summary>
    /// Startet den Animations-Timer für den Dashboard-Hintergrund.
    /// </summary>
    private void StartDashboardTimer()
    {
        _dashboardTimer?.Stop();
        _lastFrameTime = DateTime.UtcNow;

        _dashboardTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _dashboardTimer.Tick += OnDashboardTimerTick;
        _dashboardTimer.Start();
    }

    /// <summary>
    /// Stoppt den Animations-Timer.
    /// </summary>
    private void StopDashboardTimer()
    {
        if (_dashboardTimer != null)
        {
            _dashboardTimer.Tick -= OnDashboardTimerTick;
            _dashboardTimer.Stop();
            _dashboardTimer = null;
        }
    }

    /// <summary>
    /// Timer-Tick: Aktualisiert Partikel und fordert Canvas-Neuzeichnung an.
    /// </summary>
    private void OnDashboardTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

        // DeltaTime begrenzen (z.B. nach App-Pause)
        deltaTime = Math.Min(deltaTime, 0.1f);
        _dashboardTime += deltaTime;

        FinanceDashboardRenderer.Update(deltaTime);
        DashboardBgCanvas?.InvalidateSurface();
    }

    /// <summary>
    /// Zeichnet den animierten Dashboard-Hintergrund.
    /// </summary>
    private void OnPaintDashboardBg(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        FinanceDashboardRenderer.Render(canvas, bounds, _dashboardTime);
    }

    /// <summary>
    /// Zeichnet den Ausgaben-Donut-Chart (Top-6 Kategorien).
    /// </summary>
    private void OnPaintExpenseDonut(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not MainViewModel vm || vm.HomeExpenseSegments == null
            || vm.HomeExpenseSegments.Length == 0) return;

        DonutChartVisualization.Render(canvas, bounds, vm.HomeExpenseSegments,
            innerRadiusFraction: 0.5f, showLabels: true, showLegend: true,
            startAngle: -90f);
    }

    /// <summary>
    /// Zeichnet die 30-Tage-Ausgaben-Sparkline im Header.
    /// </summary>
    private void OnPaintSparkline(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not MainViewModel vm || vm.SparklineValues == null
            || !vm.HasSparklineData) return;

        // Weiße Linie mit Transparenz passend zum Header-Bereich
        SparklineVisualization.Render(canvas, bounds, vm.SparklineValues,
            new SKColor(255, 255, 255, 180), showEndDot: true,
            trendLabel: vm.SparklineTrendLabel);
    }

    /// <summary>
    /// Zeichnet die Budget-Mini-Ringe.
    /// </summary>
    private void OnPaintBudgetMiniRings(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not MainViewModel vm || vm.BudgetRings == null
            || !vm.HasBudgetRings) return;

        BudgetMiniRingVisualization.Render(canvas, bounds, vm.BudgetRings);
    }
}
