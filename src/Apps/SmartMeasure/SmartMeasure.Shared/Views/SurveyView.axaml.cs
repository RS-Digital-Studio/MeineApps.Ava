using Avalonia.Controls;
using Avalonia.Labs.Controls;
using SkiaSharp;
using SmartMeasure.Shared.ViewModels;

namespace SmartMeasure.Shared.Views;

public partial class SurveyView : UserControl
{
    private SKCanvasView? _compassCanvas;

    public SurveyView()
    {
        InitializeComponent();

        _compassCanvas = this.FindControl<SKCanvasView>("CompassCanvas");
        if (_compassCanvas != null)
            _compassCanvas.PaintSurface += OnCompassPaint;

        DataContextChanged += (_, _) =>
        {
            if (DataContext is SurveyViewModel vm)
            {
                vm.CompassInvalidateRequested += () =>
                    _compassCanvas?.InvalidateSurface();
            }
        };
    }

    private void OnCompassPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (DataContext is not SurveyViewModel vm) return;

        var canvas = e.Surface.Canvas;
        var bounds = canvas.LocalClipBounds;
        vm.CompassRenderer.Render(canvas, bounds);
    }
}
