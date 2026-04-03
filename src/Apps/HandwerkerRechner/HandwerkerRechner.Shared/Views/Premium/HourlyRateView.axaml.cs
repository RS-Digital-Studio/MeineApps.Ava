using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class HourlyRateView : CalculatorViewBase
{
    public HourlyRateView()
    {
        InitializeComponent();
    }

    protected override bool ShouldInvalidateOnPropertyChanged(string? propertyName)
    {
        return propertyName == nameof(HourlyRateViewModel.HasResult) ||
               propertyName == nameof(HourlyRateViewModel.TotalGross);
    }

    protected override void OnResultPropertyChanged()
    {
        HourlyRateCanvas.InvalidateSurface();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);

        if (DataContext is HourlyRateViewModel vm && vm.HasResult)
        {
            HourlyRateVisualization.Render(
                canvas,
                canvas.LocalClipBounds,
                vm.NetLaborCost,
                vm.OverheadAmount,
                vm.VatAmount,
                vm.TotalGross,
                vm.NetLaborCostLabel,
                vm.OverheadAmountLabel,
                vm.VatAmountLabel,
                vm.TotalGrossLabel);
        }
    }
}
