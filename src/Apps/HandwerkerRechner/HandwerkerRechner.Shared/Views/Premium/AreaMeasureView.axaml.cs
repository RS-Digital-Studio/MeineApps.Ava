using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class AreaMeasureView : CalculatorViewBase
{
    public AreaMeasureView()
    {
        InitializeComponent();
    }

    protected override bool ShouldInvalidateOnPropertyChanged(string? propertyName)
    {
        return propertyName == nameof(AreaMeasureViewModel.HasResult) ||
               propertyName == nameof(AreaMeasureViewModel.CurrentShapeArea) ||
               propertyName == nameof(AreaMeasureViewModel.SelectedShapeIndex);
    }

    protected override void OnResultPropertyChanged()
    {
        AreaMeasureCanvas.InvalidateSurface();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);

        if (DataContext is AreaMeasureViewModel vm && vm.HasResult)
        {
            AreaMeasureVisualization.Render(
                canvas,
                canvas.LocalClipBounds,
                vm.SelectedShapeIndex,
                vm.Dimension1,
                vm.Dimension2,
                vm.Dimension3,
                vm.Dimension4,
                vm.Dimension5,
                vm.CurrentShapeArea);
        }
    }
}
