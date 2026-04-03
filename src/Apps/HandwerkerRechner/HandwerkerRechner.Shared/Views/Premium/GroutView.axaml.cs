using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class GroutView : CalculatorViewBase
{
    public GroutView()
    {
        InitializeComponent();
    }

    protected override void OnResultPropertyChanged()
    {
        GroutVisualization.StartAnimation();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is GroutViewModel vm && vm.HasResult && vm.Result != null)
        {
            GroutVisualization.Render(canvas, canvas.LocalClipBounds, vm.Result);

            if (GroutVisualization.NeedsRedraw)
                RequestAnimationFrame(sender);
        }
    }
}
