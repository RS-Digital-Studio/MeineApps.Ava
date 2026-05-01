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
        // HasResult bleibt nach dem 1. Calculate dauerhaft true → Properties wie TotalGross
        // ändern sich aber bei jeder Live-Calculate-Iteration. Ohne explizites InvalidateSurface
        // würde Avalonia das Canvas nicht neu rendern. StartAnimation startet die Einschwing-Animation
        // (~500ms EaseOut) und hält den Frame-Loop via NeedsRedraw aufrecht.
        HourlyRateVisualization.StartAnimation();
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

            if (HourlyRateVisualization.NeedsRedraw)
                RequestAnimationFrame(sender);
        }
    }
}
