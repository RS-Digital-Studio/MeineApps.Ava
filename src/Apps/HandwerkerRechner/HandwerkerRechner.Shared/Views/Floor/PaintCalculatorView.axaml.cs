using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Floor;

namespace HandwerkerRechner.Views.Floor;

public partial class PaintCalculatorView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public PaintCalculatorView()
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
                    PaintVisualization.StartAnimation();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is PaintCalculatorViewModel vm && vm.HasResult && vm.Result != null)
        {
            PaintVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.Area, vm.NumberOfCoats,
                (float)vm.Result.LitersNeeded, vm.HasResult);

            // Animation-Loop: weitere Frames anfordern
            if (PaintVisualization.NeedsRedraw)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    (sender as Avalonia.Labs.Controls.SKCanvasView)?.InvalidateSurface(),
                    Avalonia.Threading.DispatcherPriority.Render);
            }
        }
    }
}
