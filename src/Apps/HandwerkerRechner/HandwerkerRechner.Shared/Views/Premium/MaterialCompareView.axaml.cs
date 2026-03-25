using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class MaterialCompareView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public MaterialCompareView()
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
                // Canvas neu zeichnen wenn Ergebnisse aktualisiert wurden
                if (args.PropertyName == nameof(MaterialCompareViewModel.HasResult) ||
                    args.PropertyName == nameof(MaterialCompareViewModel.TotalCostA) ||
                    args.PropertyName == nameof(MaterialCompareViewModel.TotalCostB))
                    MaterialCompareCanvas.InvalidateSurface();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);

        if (DataContext is MaterialCompareViewModel vm && vm.HasResult)
        {
            MaterialCompareVisualization.Render(
                canvas,
                canvas.LocalClipBounds,
                vm.ProductAName,
                vm.TotalCostA,
                vm.ProductBName,
                vm.TotalCostB,
                vm.SavingsAmount,
                vm.SavingsPercent,
                vm.IsAcheaper);
        }
    }
}
