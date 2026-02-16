using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class MetalView : UserControl
{
    public MetalView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is MetalViewModel vm && vm.HasResult)
        {
            // ThreadSize-Label und DrillSize
            string threadSize = "";
            float drillSize = 0f;
            if (vm.ThreadResult != null)
            {
                threadSize = vm.ThreadResult.ThreadSize;
                drillSize = (float)vm.ThreadResult.DrillSize;
            }

            MetalVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.SelectedCalculator,
                vm.SelectedMetal, vm.SelectedProfile,
                (float)vm.Dimension1, (float)vm.Dimension2, (float)vm.WallThickness,
                threadSize, drillSize,
                vm.WeightResult != null ? (float)vm.WeightResult.Weight : 0f,
                vm.HasResult);
        }
    }
}
