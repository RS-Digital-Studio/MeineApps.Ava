using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class ScreedView : CalculatorViewBase
{
    public ScreedView()
    {
        InitializeComponent();
    }

    protected override void OnResultPropertyChanged()
    {
        ScreedVisualization.StartAnimation();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is ScreedViewModel vm && vm.HasResult && vm.Result != null)
        {
            ScreedVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.FloorArea, (float)vm.ThicknessCm,
                vm.SelectedScreedType, vm.Result.BagsNeeded);

            if (ScreedVisualization.NeedsRedraw)
                RequestAnimationFrame(sender);
        }
    }
}
