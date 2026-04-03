using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class PlasterView : CalculatorViewBase
{
    public PlasterView()
    {
        InitializeComponent();
    }

    protected override void OnResultPropertyChanged()
    {
        PlasterVisualization.StartAnimation();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is PlasterViewModel vm && vm.HasResult && vm.Result != null)
        {
            PlasterVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.WallArea, (float)vm.ThicknessMm,
                vm.SelectedPlasterType, vm.Result.BagsNeeded);

            if (PlasterVisualization.NeedsRedraw)
                RequestAnimationFrame(sender);
        }
    }
}
