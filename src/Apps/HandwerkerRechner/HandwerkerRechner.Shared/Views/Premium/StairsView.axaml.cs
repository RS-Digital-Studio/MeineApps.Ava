using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class StairsView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public StairsView()
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
                    StairsVisualization.StartAnimation();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is StairsViewModel vm && vm.HasResult && vm.StairsResult != null)
        {
            StairsVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.StairsResult.StepCount,
                (float)vm.StairsResult.StepHeight,
                (float)vm.StairsResult.TreadDepth,
                (float)vm.StairsResult.FloorHeight,
                (float)vm.StairsResult.Angle,
                vm.StairsResult.IsDinCompliant,
                vm.StairsResult.IsComfortable,
                vm.HasResult);

            // Animation-Loop: weitere Frames anfordern
            if (StairsVisualization.NeedsRedraw)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    (sender as Avalonia.Labs.Controls.SKCanvasView)?.InvalidateSurface(),
                    Avalonia.Threading.DispatcherPriority.Render);
            }
        }
    }
}
