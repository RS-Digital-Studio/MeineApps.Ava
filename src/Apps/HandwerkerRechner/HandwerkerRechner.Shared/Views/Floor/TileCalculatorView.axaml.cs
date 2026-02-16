using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Floor;

namespace HandwerkerRechner.Views.Floor;

public partial class TileCalculatorView : UserControl
{
    public TileCalculatorView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is TileCalculatorViewModel vm && vm.HasResult)
        {
            TileVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.RoomLength, (float)vm.RoomWidth,
                (float)vm.TileLength, (float)vm.TileWidth,
                (float)vm.WastePercentage, vm.HasResult);
        }
    }
}
