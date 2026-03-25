using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class HourlyRateView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public HourlyRateView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Alten Handler abmelden, um Memory-Leaks zu vermeiden
        if (_currentVm != null && _resultHandler != null)
            _currentVm.PropertyChanged -= _resultHandler;

        _currentVm = DataContext as INotifyPropertyChanged;
        if (_currentVm != null)
        {
            _resultHandler = (_, args) =>
            {
                // Canvas neu zeichnen sobald Ergebnis vorliegt
                if (args.PropertyName == nameof(HourlyRateViewModel.HasResult) ||
                    args.PropertyName == nameof(HourlyRateViewModel.TotalGross))
                    HourlyRateCanvas.InvalidateSurface();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);

        if (DataContext is HourlyRateViewModel vm && vm.HasResult)
        {
            HourlyRateVisualization.Render(
                canvas,
                canvas.LocalClipBounds,
                vm.NetLaborCost,
                vm.OverheadAmount,
                vm.VatAmount,
                vm.TotalGross,
                vm.NetLaborCostLabel,
                vm.OverheadAmountLabel,
                vm.VatAmountLabel,
                vm.TotalGrossLabel);
        }
    }
}
