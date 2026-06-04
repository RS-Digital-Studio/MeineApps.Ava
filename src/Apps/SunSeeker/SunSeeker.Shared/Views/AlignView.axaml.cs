using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using SunSeeker.Shared.ViewModels;

namespace SunSeeker.Shared.Views;

public partial class AlignView : UserControl
{
    private SKCanvasView? _canvas;
    private AlignViewModel? _boundVm;
    private Action? _invalidateHandler;

    public AlignView()
    {
        InitializeComponent();

        _canvas = this.FindControl<SKCanvasView>("CompassCanvas");
        if (_canvas != null)
            _canvas.PaintSurface += OnPaint;

        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>Handler-Dedup (-= vor +=), damit sich bei DataContext-Wechsel keine
    /// Invalidate-Handler akkumulieren.</summary>
    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_boundVm != null && _invalidateHandler != null)
            _boundVm.InvalidateRequested -= _invalidateHandler;

        _boundVm = DataContext as AlignViewModel;
        _invalidateHandler = null;

        if (_boundVm != null)
        {
            _invalidateHandler = () => _canvas?.InvalidateSurface();
            _boundVm.InvalidateRequested += _invalidateHandler;
        }
    }

    private void OnPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (DataContext is not AlignViewModel vm) return;

        var bounds = e.Surface.Canvas.LocalClipBounds;
        vm.Renderer.Render(e.Surface.Canvas, bounds);
    }
}
