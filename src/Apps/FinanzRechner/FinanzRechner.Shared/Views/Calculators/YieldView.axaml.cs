using Avalonia.Controls;
using Avalonia.Labs.Controls;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;
using FinanzRechner.ViewModels.Calculators;

namespace FinanzRechner.Views.Calculators;

public partial class YieldView : UserControl
{
    public YieldView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is YieldViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(vm.DonutSegments):
                    case nameof(vm.HasResult):
                        DonutCanvas?.InvalidateSurface();
                        break;
                }
            };
        }
    }

    /// <summary>
    /// Zeichnet den Rendite-Donut (Investition vs. Rendite).
    /// </summary>
    private void OnPaintDonut(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not YieldViewModel vm || vm.DonutSegments == null
            || vm.DonutSegments.Length == 0) return;

        DonutChartVisualization.Render(canvas, bounds, vm.DonutSegments,
            innerRadiusFraction: 0.5f, showLabels: true, showLegend: false,
            startAngle: -90f);
    }
}
