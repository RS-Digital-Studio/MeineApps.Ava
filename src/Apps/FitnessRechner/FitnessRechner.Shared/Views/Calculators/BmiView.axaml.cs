using Avalonia.Controls;
using Avalonia.Labs.Controls;
using FitnessRechner.Graphics;
using FitnessRechner.ViewModels.Calculators;
using SkiaSharp;

namespace FitnessRechner.Views.Calculators;

public partial class BmiView : UserControl
{
    private SKRect _lastHeaderBounds;

    public BmiView()
    {
        InitializeComponent();
    }

    // ═══════ Header (CalculatorHeaderRenderer) ═══════

    private void OnHeaderPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var bounds = canvas.LocalClipBounds;
        _lastHeaderBounds = bounds;

        string title = "BMI";
        CalculatorHeaderRenderer.Render(canvas, bounds,
            title, MedicalColors.BmiBlue, MedicalColors.BmiBlueLight, 0f);
    }

    private void OnHeaderPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not SKCanvasView canvas) return;
        var pos = e.GetPosition(canvas);
        float scaleX = _lastHeaderBounds.Width / (float)canvas.Bounds.Width;
        float scaleY = _lastHeaderBounds.Height / (float)canvas.Bounds.Height;

        if (CalculatorHeaderRenderer.IsBackButtonHit(_lastHeaderBounds,
            (float)pos.X * scaleX, (float)pos.Y * scaleY))
        {
            if (DataContext is BmiViewModel vm)
                vm.GoBackCommand.Execute(null);
        }
        e.Handled = true;
    }

    // ═══════ BMI-Gauge (BmiGaugeRenderer) ═══════

    private void OnPaintBmiGauge(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (DataContext is BmiViewModel vm && vm.HasResult)
        {
            BmiGaugeRenderer.Render(canvas, canvas.LocalClipBounds,
                (float)vm.BmiValue, vm.HasResult);
        }
    }
}
