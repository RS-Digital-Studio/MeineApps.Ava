using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using FinanzRechner.ViewModels;

namespace FinanzRechner.Views;

public partial class ExpenseTrackerView : UserControl
{
    public ExpenseTrackerView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ExpenseTrackerViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(vm.CategoryDonutSegments):
                    case nameof(vm.HasCategoryChartData):
                        CategoryDonutCanvas?.InvalidateSurface();
                        break;
                }
            };
        }
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is ExpenseTrackerViewModel vm)
            await vm.LoadExpensesAsync();
    }

    /// <summary>
    /// Zeichnet den Kategorie-Donut-Chart für Ausgaben.
    /// </summary>
    private void OnPaintCategoryDonut(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not ExpenseTrackerViewModel vm || vm.CategoryDonutSegments == null
            || vm.CategoryDonutSegments.Length == 0) return;

        DonutChartVisualization.Render(canvas, bounds, vm.CategoryDonutSegments,
            innerRadiusFraction: 0.45f, showLabels: true, showLegend: true,
            startAngle: -90f);
    }
}
