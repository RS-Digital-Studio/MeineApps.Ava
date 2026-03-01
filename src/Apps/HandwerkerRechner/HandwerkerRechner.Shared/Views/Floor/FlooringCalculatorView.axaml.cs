using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Floor;

namespace HandwerkerRechner.Views.Floor;

public partial class FlooringCalculatorView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public FlooringCalculatorView()
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
                    FlooringVisualization.StartAnimation();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is FlooringCalculatorViewModel vm && vm.HasResult)
        {
            FlooringVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.RoomLength, (float)vm.RoomWidth,
                (float)vm.BoardLength, (float)vm.BoardWidth,
                (float)vm.WastePercentage, vm.HasResult);

            // Animation-Loop: weitere Frames anfordern
            if (FlooringVisualization.NeedsRedraw)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    (sender as Avalonia.Labs.Controls.SKCanvasView)?.InvalidateSurface(),
                    Avalonia.Threading.DispatcherPriority.Render);
            }
        }
    }
}
