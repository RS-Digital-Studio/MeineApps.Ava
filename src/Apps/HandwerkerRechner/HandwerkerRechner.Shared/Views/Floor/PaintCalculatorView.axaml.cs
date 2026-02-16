using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Floor;

namespace HandwerkerRechner.Views.Floor;

public partial class PaintCalculatorView : UserControl
{
    public PaintCalculatorView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is PaintCalculatorViewModel vm && vm.HasResult && vm.Result != null)
        {
            PaintVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.Area, vm.NumberOfCoats,
                (float)vm.Result.LitersNeeded, vm.HasResult);
        }
    }
}
