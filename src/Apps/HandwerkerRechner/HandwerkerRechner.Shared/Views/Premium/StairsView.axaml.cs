using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class StairsView : UserControl
{
    public StairsView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is StairsViewModel vm && vm.HasResult && vm.StairsResult != null)
        {
            StairsVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.StairsResult.StepCount,
                (float)vm.StairsResult.StepHeight,
                (float)vm.StairsResult.TreadDepth,
                (float)vm.StairsResult.FloorHeight,
                (float)vm.StairsResult.Angle,
                vm.StairsResult.IsDinCompliant,
                vm.StairsResult.IsComfortable,
                vm.HasResult);
        }
    }
}
