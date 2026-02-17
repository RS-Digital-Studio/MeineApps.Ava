using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels;

namespace FinanzRechner.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(vm.OverallBudgetPercentage):
                    case nameof(vm.HasBudgets):
                    case nameof(vm.TopBudgets):
                        BudgetGaugeCanvas?.InvalidateSurface();
                        break;
                    case nameof(vm.HomeExpenseSegments):
                    case nameof(vm.HasHomeChartData):
                        ExpenseDonutCanvas?.InvalidateSurface();
                        break;
                    case nameof(vm.SparklineValues):
                    case nameof(vm.HasSparklineData):
                        SparklineCanvas?.InvalidateSurface();
                        break;
                    case nameof(vm.BudgetRings):
                    case nameof(vm.HasBudgetRings):
                        BudgetMiniRingCanvas?.InvalidateSurface();
                        break;
                }
            };
        }
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            await vm.OnAppearingAsync();
    }

    /// <summary>
    /// Zeichnet den Budget-Halbkreis-Tachometer.
    /// </summary>
    private void OnPaintBudgetGauge(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not MainViewModel vm || !vm.HasBudgets) return;

        BudgetGaugeVisualization.Render(canvas, bounds,
            vm.OverallBudgetPercentage, "", "",
            vm.OverallBudgetPercentage > 100);
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
