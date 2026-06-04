using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using SunSeeker.Shared.ViewModels;

namespace SunSeeker.Shared.Views;

public partial class LivePowerView : UserControl
{
    private SKCanvasView? _canvas;
    private LivePowerViewModel? _boundVm;
    private Action? _invalidateHandler;

    public LivePowerView()
    {
        InitializeComponent();

        _canvas = this.FindControl<SKCanvasView>("ChartCanvas");
        if (_canvas != null)
            _canvas.PaintSurface += OnPaint;

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_boundVm != null && _invalidateHandler != null)
            _boundVm.InvalidateRequested -= _invalidateHandler;

        _boundVm = DataContext as LivePowerViewModel;
        _invalidateHandler = null;

        if (_boundVm != null)
        {
            _invalidateHandler = () => _canvas?.InvalidateSurface();
            _boundVm.InvalidateRequested += _invalidateHandler;
        }
    }

    private void OnPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (DataContext is not LivePowerViewModel vm) return;

        var bounds = e.Surface.Canvas.LocalClipBounds;
        vm.Renderer.Render(e.Surface.Canvas, bounds, vm.Samples);
    }
}
