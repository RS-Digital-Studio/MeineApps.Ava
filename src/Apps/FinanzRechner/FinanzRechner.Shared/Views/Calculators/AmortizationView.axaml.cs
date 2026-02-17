using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using FinanzRechner.Graphics;
using FinanzRechner.ViewModels.Calculators;

namespace FinanzRechner.Views.Calculators;

public partial class AmortizationView : UserControl
{
    public AmortizationView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is AmortizationViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(vm.AmortYearLabels):
                    case nameof(vm.AmortPrincipalData):
                    case nameof(vm.AmortInterestData):
                    case nameof(vm.HasResult):
                        AmortBarCanvas?.InvalidateSurface();
                        break;
                }
            };
        }
    }

    /// <summary>
    /// Zeichnet das Tilgungsplan-Balkendiagramm (Tilgung + Zinsen pro Jahr).
    /// </summary>
    private void OnPaintAmortBars(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;

        if (DataContext is not AmortizationViewModel vm
            || vm.AmortYearLabels == null || vm.AmortPrincipalData == null || vm.AmortInterestData == null) return;

        AmortizationBarVisualization.Render(canvas, bounds,
            vm.AmortYearLabels, vm.AmortPrincipalData, vm.AmortInterestData);
    }
}
