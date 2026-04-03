using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class GardenView : CalculatorViewBase
{
    public GardenView()
    {
        InitializeComponent();
    }

    protected override void OnResultPropertyChanged()
    {
        GardenVisualization.StartAnimation();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is GardenViewModel vm && vm.HasResult)
        {
            GardenVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.SelectedCalculator,
                // Pflaster
                (float)vm.PavingArea, (float)vm.StoneLength, (float)vm.StoneWidth,
                (float)vm.JointWidth, vm.PavingResult?.StonesNeeded ?? 0,
                // Erde
                (float)vm.SoilArea, (float)vm.SoilDepth,
                vm.SoilResult?.BagsNeeded ?? 0,
                // Teich
                (float)vm.PondLength, (float)vm.PondWidth, (float)vm.PondDepth,
                (float)vm.Overlap,
                vm.PondResult != null ? (float)vm.PondResult.LinerArea : 0f,
                vm.HasResult);

            if (GardenVisualization.NeedsRedraw)
                RequestAnimationFrame(sender);
        }
    }
}
