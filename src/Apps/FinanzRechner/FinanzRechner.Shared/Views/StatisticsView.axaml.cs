using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels;

namespace FinanzRechner.Views;

public partial class StatisticsView : UserControl
{
    private StatisticsViewModel? _vm;

    public StatisticsView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }

        if (DataContext is StatisticsViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(_vm.ExpenseDonutSegments):
                ExpenseDonutCanvas?.InvalidateSurface();
                break;
            case nameof(_vm.IncomeDonutSegments):
                IncomeDonutCanvas?.InvalidateSurface();
                break;
            case nameof(_vm.TrendMonthLabels):
            case nameof(_vm.TrendIncomeData):
            case nameof(_vm.TrendExpenseData):
                TrendChartCanvas?.InvalidateSurface();
                break;
        }
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is StatisticsViewModel vm)
            await vm.LoadStatisticsCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Zeichnet den Ausgaben-Donut-Chart.
    /// </summary>
    private void OnPaintExpenseDonut(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not StatisticsViewModel vm
            || vm.ExpenseDonutSegments == null || vm.ExpenseDonutSegments.Length == 0) return;

        DonutChartVisualization.Render(canvas, bounds, vm.ExpenseDonutSegments,
            innerRadiusFraction: 0.55f, showLabels: true, showLegend: true,
            startAngle: -90f);
    }

    /// <summary>
    /// Zeichnet den Einnahmen-Donut-Chart.
    /// </summary>
    private void OnPaintIncomeDonut(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not StatisticsViewModel vm
            || vm.IncomeDonutSegments == null || vm.IncomeDonutSegments.Length == 0) return;

        DonutChartVisualization.Render(canvas, bounds, vm.IncomeDonutSegments,
            innerRadiusFraction: 0.55f, showLabels: true, showLegend: true,
            startAngle: -90f);
    }

    /// <summary>
    /// Zeichnet den 6-Monats-Trend (Einnahmen + Ausgaben).
    /// </summary>
    private void OnPaintTrendChart(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not StatisticsViewModel vm
            || vm.TrendMonthLabels == null || vm.TrendMonthLabels.Length == 0) return;

        TrendLineVisualization.Render(canvas, bounds,
            vm.TrendMonthLabels, vm.TrendIncomeData, vm.TrendExpenseData);
    }
}
