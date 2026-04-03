using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class MaterialCompareView : CalculatorViewBase
{
    public MaterialCompareView()
    {
        InitializeComponent();
    }

    protected override bool ShouldInvalidateOnPropertyChanged(string? propertyName)
    {
        return propertyName == nameof(MaterialCompareViewModel.HasResult) ||
               propertyName == nameof(MaterialCompareViewModel.TotalCostA) ||
               propertyName == nameof(MaterialCompareViewModel.TotalCostB);
    }

    protected override void OnResultPropertyChanged()
    {
        MaterialCompareCanvas.InvalidateSurface();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);

        if (DataContext is MaterialCompareViewModel vm && vm.HasResult)
        {
            MaterialCompareVisualization.Render(
                canvas,
                canvas.LocalClipBounds,
                vm.ProductAName,
                vm.TotalCostA,
                vm.ProductBName,
                vm.TotalCostB,
                vm.SavingsAmount,
                vm.SavingsPercent,
                vm.IsAcheaper);
        }
    }
}
