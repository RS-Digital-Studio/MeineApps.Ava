using Avalonia;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using FinanzRechner.ViewModels;

namespace FinanzRechner.Views;

public partial class ExpenseTrackerView : UserControl
{
    private ExpenseTrackerViewModel? _vm;

    public ExpenseTrackerView()
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

        if (DataContext is ExpenseTrackerViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(_vm.CategoryDonutSegments):
            case nameof(_vm.HasCategoryChartData):
                CategoryDonutCanvas?.InvalidateSurface();
                break;
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
