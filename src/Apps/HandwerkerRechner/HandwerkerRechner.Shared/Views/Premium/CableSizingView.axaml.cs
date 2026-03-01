using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Labs.Controls;
using HandwerkerRechner.Graphics;
using HandwerkerRechner.ViewModels.Premium;

namespace HandwerkerRechner.Views.Premium;

public partial class CableSizingView : UserControl
{
    private INotifyPropertyChanged? _currentVm;
    private PropertyChangedEventHandler? _resultHandler;

    public CableSizingView()
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
                    CableSizingVisualization.StartAnimation();
            };
            _currentVm.PropertyChanged += _resultHandler;
        }
    }

    private void OnPaintVisualization(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        if (DataContext is CableSizingViewModel vm && vm.HasResult && vm.Result != null)
        {
            CableSizingVisualization.Render(canvas, canvas.LocalClipBounds,
                (float)vm.Result.RecommendedCrossSection,
                (float)vm.Result.ActualDropPercent,
                (float)vm.Result.MaxDropPercent,
                vm.SelectedMaterial,
                vm.Result.IsVdeCompliant);

            // Animation-Loop: weitere Frames anfordern
            if (CableSizingVisualization.NeedsRedraw)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    (sender as Avalonia.Labs.Controls.SKCanvasView)?.InvalidateSurface(),
                    Avalonia.Threading.DispatcherPriority.Render);
            }
        }
    }
}
