using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class DrywallView : UserControl
{
    public DrywallView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is DrywallViewModel vm && vm.HasResult && vm.Result != null)
        {
            DrywallVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.WallLength, (float)vm.WallHeight,
                vm.DoublePlated,
                vm.Result.CwProfiles, vm.Result.Plates,
                vm.HasResult);
        }
    }
}
