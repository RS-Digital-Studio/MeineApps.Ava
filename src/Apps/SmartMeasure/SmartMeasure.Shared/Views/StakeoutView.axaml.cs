using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class StakeoutView : UserControl
{
    private StakeoutViewModel? _vm;
    private System.Action? _invalidateHandler;

    public StakeoutView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        // Alte Subscription entfernen (Handler-Dedup-Pattern)
        if (_vm != null && _invalidateHandler != null)
            _vm.InvalidateRequested -= _invalidateHandler;

        _vm = DataContext as StakeoutViewModel;
        _invalidateHandler = null;

        if (_vm != null)
        {
            _invalidateHandler = () => StakeoutCanvas.InvalidateSurface();
            _vm.InvalidateRequested += _invalidateHandler;
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        _vm?.Renderer.Render(canvas, bounds);
    }
}
