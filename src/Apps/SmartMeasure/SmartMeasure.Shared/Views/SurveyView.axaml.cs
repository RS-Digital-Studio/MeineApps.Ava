using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class SurveyView : UserControl
{
    private SKCanvasView? _compassCanvas;
    private SurveyViewModel? _boundVm;
    private Action? _invalidateHandler;

    public SurveyView()
    {
        InitializeComponent();

        _compassCanvas = this.FindControl<SKCanvasView>("CompassCanvas");
        if (_compassCanvas != null)
            _compassCanvas.PaintSurface += OnCompassPaint;

        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Handler-Dedup via -= vor +=. Ohne das akkumuliert sich bei DataContext-Wechsel
    /// (Hot-Reload, View-Recycling) pro Switch ein neuer Handler → N-fache Invalidate-Calls
    /// pro Frame.
    /// </summary>
    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_boundVm != null && _invalidateHandler != null)
            _boundVm.CompassInvalidateRequested -= _invalidateHandler;

        _boundVm = DataContext as SurveyViewModel;
        _invalidateHandler = null;

        if (_boundVm != null)
        {
            _invalidateHandler = () => _compassCanvas?.InvalidateSurface();
            _boundVm.CompassInvalidateRequested += _invalidateHandler;
        }
    }

    private void OnCompassPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (DataContext is not SurveyViewModel vm) return;

        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        vm.CompassRenderer.Render(canvas, bounds);
    }
}
