using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class MetalView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public MetalView()
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
                    MetalVisualization.StartAnimation();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is MetalViewModel vm && vm.HasResult)
        {
            // ThreadSize-Label und DrillSize
            string threadSize = "";
            float drillSize = 0f;
            if (vm.ThreadResult != null)
            {
                threadSize = vm.ThreadResult.ThreadSize;
                drillSize = (float)vm.ThreadResult.DrillSize;
            }

            MetalVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.SelectedCalculator,
                vm.SelectedMetal, vm.SelectedProfile,
                (float)vm.Dimension1, (float)vm.Dimension2, (float)vm.WallThickness,
                threadSize, drillSize,
                vm.WeightResult != null ? (float)vm.WeightResult.Weight : 0f,
                vm.HasResult);

            // Animation-Loop: weitere Frames anfordern
            if (MetalVisualization.NeedsRedraw)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    (sender as Avalonia.Labs.Controls.SKCanvasView)?.InvalidateSurface(),
                    Avalonia.Threading.DispatcherPriority.Render);
            }
        }
    }
}
