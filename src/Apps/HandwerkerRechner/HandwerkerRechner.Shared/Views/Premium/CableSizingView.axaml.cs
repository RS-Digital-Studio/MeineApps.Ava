using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class CableSizingView : CalculatorViewBase
{
    public CableSizingView()
    {
        InitializeComponent();
    }

    protected override void OnResultPropertyChanged()
    {
        CableSizingVisualization.StartAnimation();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is CableSizingViewModel vm && vm.HasResult && vm.Result != null)
        {
            CableSizingVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.Result.RecommendedCrossSection,
                (float)vm.Result.ActualDropPercent,
                (float)vm.Result.MaxDropPercent,
                vm.SelectedMaterial,
                vm.Result.IsVdeCompliant);

            if (CableSizingVisualization.NeedsRedraw)
                RequestAnimationFrame(sender);
        }
    }
}
