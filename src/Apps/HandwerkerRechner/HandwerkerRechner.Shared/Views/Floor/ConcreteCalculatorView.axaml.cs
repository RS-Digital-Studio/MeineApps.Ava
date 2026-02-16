using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Floor;

namespace HandwerkerRechner.Views.Floor;

public partial class ConcreteCalculatorView : UserControl
{
    public ConcreteCalculatorView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is ConcreteCalculatorViewModel vm && vm.HasResult && vm.Result != null)
        {
            float dim1, dim2, dim3;
            if (vm.IsSlabSelected) { dim1 = (float)vm.SlabLength * 100f; dim2 = (float)vm.SlabWidth * 100f; dim3 = (float)vm.SlabHeight; }
            else if (vm.IsStripSelected) { dim1 = (float)vm.StripLength; dim2 = (float)vm.StripWidth; dim3 = (float)vm.StripDepth; }
            else { dim1 = (float)vm.ColumnDiameter; dim2 = (float)vm.ColumnHeight; dim3 = 0; }

            ConcreteVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.SelectedCalculator, dim1, dim2, dim3,
                (float)vm.Result.VolumeM3, vm.HasResult);
        }
    }
}
