using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Floor;

namespace HandwerkerRechner.Views.Floor;

public partial class FlooringCalculatorView : UserControl
{
    public FlooringCalculatorView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is FlooringCalculatorViewModel vm && vm.HasResult)
        {
            FlooringVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.RoomLength, (float)vm.RoomWidth,
                (float)vm.BoardLength, (float)vm.BoardWidth,
                (float)vm.WastePercentage, vm.HasResult);
        }
    }
}
