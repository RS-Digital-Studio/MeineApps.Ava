using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class RoofSolarView : UserControl
{
    public RoofSolarView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is RoofSolarViewModel vm && vm.HasResult)
        {
            RoofSolarVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.SelectedCalculator,
                // Dachneigung
                (float)vm.Run, (float)vm.Rise,
                vm.PitchResult != null ? (float)vm.PitchResult.PitchDegrees : 0f,
                vm.PitchResult != null ? (float)vm.PitchResult.PitchPercent : 0f,
                // Dachziegel
                (float)vm.RoofArea, (float)vm.TilesPerSqm,
                vm.TilesResult?.TilesNeeded ?? 0,
                // Solar
                (float)vm.SolarRoofArea,
                (float)(vm.SolarResult?.KwPeak ?? 0),
                (float)(vm.SolarResult?.AnnualYieldKwh ?? 0),
                vm.SelectedOrientation,
                (float)vm.TiltDegrees,
                vm.HasResult);
        }
    }
}
