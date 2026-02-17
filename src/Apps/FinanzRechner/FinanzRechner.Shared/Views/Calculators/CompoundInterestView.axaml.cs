using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels.Calculators;

namespace FinanzRechner.Views.Calculators;

public partial class CompoundInterestView : UserControl
{
    public CompoundInterestView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is CompoundInterestViewModel vm)
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
    /// Zeichnet das Kapitalwachstum als gestapeltes Flächendiagramm.
    /// </summary>
    private void OnPaintStackedArea(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not CompoundInterestViewModel vm
            || vm.ChartXLabels == null || vm.ChartArea1Data == null || vm.ChartArea2Data == null) return;

        StackedAreaVisualization.Render(canvas, bounds,
            vm.ChartXLabels, vm.ChartArea1Data, vm.ChartArea2Data,
            new SKColor(0x3B, 0x82, 0xF6), // Blau (Kapital)
            new SKColor(0x22, 0xC5, 0x5E), // Grün (Zinsen)
            "", "");
    }
}
