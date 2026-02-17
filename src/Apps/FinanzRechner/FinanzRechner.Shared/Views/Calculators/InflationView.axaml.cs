using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels.Calculators;

namespace FinanzRechner.Views.Calculators;

public partial class InflationView : UserControl
{
    public InflationView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is InflationViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(vm.ChartXLabels):
                    case nameof(vm.ChartArea1Data):
                    case nameof(vm.ChartArea2Data):
                    case nameof(vm.HasResult):
                        StackedAreaCanvas?.InvalidateSurface();
                        break;
                }
            };
        }
    }

    /// <summary>
    /// Zeichnet die Kaufkraft-Entwicklung als gestapeltes Flächendiagramm.
    /// </summary>
    private void OnPaintStackedArea(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not InflationViewModel vm
            || vm.ChartXLabels == null || vm.ChartArea1Data == null || vm.ChartArea2Data == null) return;

        StackedAreaVisualization.Render(canvas, bounds,
            vm.ChartXLabels, vm.ChartArea1Data, vm.ChartArea2Data,
            new SKColor(0x22, 0xC5, 0x5E), // Grün (Kaufkraft)
            new SKColor(0xEF, 0x44, 0x44), // Rot (Verlust)
            "", "");
    }
}
