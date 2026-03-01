using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Floor;

namespace HandwerkerRechner.Views.Floor;

public partial class ConcreteCalculatorView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public ConcreteCalculatorView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Alten Handler abmelden
        if (_currentVm != null && _resultHandler != null)
            _currentVm.PropertyChanged -= _resultHandler;

        _currentVm = DataContext as INotifyPropertyChanged;
        if (_currentVm != null)
        {
            _resultHandler = (_, args) =>
            {
                if (args.PropertyName?.Contains("Result") == true)
                    ConcreteVisualization.StartAnimation();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is ConcreteCalculatorViewModel vm && vm.HasResult && vm.Result != null)
        {
            float dim1, dim2, dim3;
            if (vm.IsSlabSelected) { dim1 = (float)vm.SlabLength * 100f; dim2 = (float)vm.SlabWidth * 100f; dim3 = (float)vm.SlabHeight; }
            else if (vm.IsStripSelected) { dim1 = (float)vm.StripLength; dim2 = (float)vm.StripWidth; dim3 = (float)vm.StripDepth; }
            else { dim1 = (float)vm.ColumnDiameter; dim2 = (float)vm.ColumnHeight; dim3 = 0; }

            ConcreteVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.SelectedCalculator, dim1, dim2, dim3,
                (float)vm.Result.VolumeM3, vm.HasResult);

            // Animation-Loop: weitere Frames anfordern
            if (ConcreteVisualization.NeedsRedraw)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    (sender as Avalonia.Labs.Controls.SKCanvasView)?.InvalidateSurface(),
                    Avalonia.Threading.DispatcherPriority.Render);
            }
        }
    }
}
