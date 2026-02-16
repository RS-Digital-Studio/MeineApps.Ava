using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Floor;

namespace HandwerkerRechner.Views.Floor;

public partial class WallpaperCalculatorView : UserControl
{
    public WallpaperCalculatorView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is WallpaperCalculatorViewModel vm && vm.HasResult && vm.Result != null)
        {
            WallpaperVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.WallLength, (float)vm.RoomHeight,
                (float)vm.RollWidth, (float)vm.PatternRepeat,
                vm.Result.StripsNeeded, vm.HasResult);
        }
    }
}
