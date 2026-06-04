using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using SunSeeker.Shared.ViewModels;

namespace SunSeeker.Shared.Views;

public partial class DashboardView : UserControl
{
    private SKCanvasView? _sunPathCanvas;
    private DashboardViewModel? _boundVm;
    private Action? _invalidateHandler;

    public DashboardView()
    {
        InitializeComponent();

        _sunPathCanvas = this.FindControl<SKCanvasView>("SunPathCanvas");
        if (_sunPathCanvas != null)
            _sunPathCanvas.PaintSurface += OnSunPathPaint;

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_boundVm != null && _invalidateHandler != null)
            _boundVm.SunPathInvalidateRequested -= _invalidateHandler;

        _boundVm = DataContext as DashboardViewModel;
        _invalidateHandler = null;

        if (_boundVm != null)
        {
            _invalidateHandler = () => _sunPathCanvas?.InvalidateSurface();
            _boundVm.SunPathInvalidateRequested += _invalidateHandler;
        }
    }

    private void OnSunPathPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm) return;

        var bounds = e.Surface.Canvas.LocalClipBounds;
        vm.SunPathRenderer.Render(e.Surface.Canvas, bounds);
    }
}
