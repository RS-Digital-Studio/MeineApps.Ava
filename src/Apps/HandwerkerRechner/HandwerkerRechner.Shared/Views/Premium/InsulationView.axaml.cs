using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class InsulationView : CalculatorViewBase
{
    public InsulationView()
    {
        InitializeComponent();
    }

    protected override void OnResultPropertyChanged()
    {
        InsulationVisualization.StartAnimation();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is InsulationViewModel vm && vm.HasResult && vm.Result != null)
        {
            InsulationVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.Area, (float)vm.Result.ThicknessCm,
                vm.SelectedInsulationType, (float)vm.Result.Lambda);

            if (InsulationVisualization.NeedsRedraw)
                RequestAnimationFrame(sender);
        }
    }
}
