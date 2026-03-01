using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class GardenView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public GardenView()
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
                    GardenVisualization.StartAnimation();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is GardenViewModel vm && vm.HasResult)
        {
            GardenVisualization.Render(canvas, canvas.LocalClipBounds,
                vm.SelectedCalculator,
                // Pflaster
                (float)vm.PavingArea, (float)vm.StoneLength, (float)vm.StoneWidth,
                (float)vm.JointWidth, vm.PavingResult?.StonesNeeded ?? 0,
                // Erde
                (float)vm.SoilArea, (float)vm.SoilDepth,
                vm.SoilResult?.BagsNeeded ?? 0,
                // Teich
                (float)vm.PondLength, (float)vm.PondWidth, (float)vm.PondDepth,
                (float)vm.Overlap,
                vm.PondResult != null ? (float)vm.PondResult.LinerArea : 0f,
                vm.HasResult);

            // Animation-Loop: weitere Frames anfordern
            if (GardenVisualization.NeedsRedraw)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    (sender as Avalonia.Labs.Controls.SKCanvasView)?.InvalidateSurface(),
                    Avalonia.Threading.DispatcherPriority.Render);
            }
        }
    }
}
