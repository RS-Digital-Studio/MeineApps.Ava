using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class ElectricalView : UserControl
{
    public ElectricalView()
    {
        InitializeComponent();
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is ElectricalViewModel vm && vm.HasResult)
        {
            // Ohms Law: parse string values
            float ohmsV = 0, ohmsI = 0, ohmsR = 0, ohmsP = 0;
            if (vm.OhmsLawResult != null)
            {
                ohmsV = (float)vm.OhmsLawResult.Voltage;
                ohmsI = (float)vm.OhmsLawResult.Current;
                ohmsR = (float)vm.OhmsLawResult.Resistance;
                ohmsP = (float)vm.OhmsLawResult.Power;
            }

            ElectricalVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.SelectedCalculator,
                // Spannungsabfall
                (float)vm.Voltage,
                vm.VoltageDropResult != null ? (float)vm.VoltageDropResult.VoltageDrop : 0f,
                vm.VoltageDropResult != null ? (float)vm.VoltageDropResult.PercentDrop : 0f,
                vm.VoltageDropResult?.IsAcceptable ?? true,
                (float)vm.CableLength,
                // Stromkosten
                vm.PowerCostResult != null ? (float)vm.PowerCostResult.CostPerDay : 0f,
                vm.PowerCostResult != null ? (float)vm.PowerCostResult.CostPerMonth : 0f,
                vm.PowerCostResult != null ? (float)vm.PowerCostResult.CostPerYear : 0f,
                // Ohmsches Gesetz
                ohmsV, ohmsI, ohmsR, ohmsP,
                vm.HasResult);
        }
    }
}
