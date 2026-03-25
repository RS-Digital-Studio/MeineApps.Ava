using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class AreaMeasureView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public AreaMeasureView()
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
                // Canvas neu zeichnen wenn Form oder Fläche sich ändern
                if (args.PropertyName == nameof(AreaMeasureViewModel.HasResult) ||
                    args.PropertyName == nameof(AreaMeasureViewModel.CurrentShapeArea) ||
                    args.PropertyName == nameof(AreaMeasureViewModel.SelectedShapeIndex))
                    AreaMeasureCanvas.InvalidateSurface();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);

        if (DataContext is AreaMeasureViewModel vm && vm.HasResult)
        {
            AreaMeasureVisualization.Render(
                canvas,
                canvas.LocalClipBounds,
                vm.SelectedShapeIndex,
                vm.Dimension1,
                vm.Dimension2,
                vm.Dimension3,
                vm.Dimension4,
                vm.Dimension5,
                vm.CurrentShapeArea);
        }
    }
}
