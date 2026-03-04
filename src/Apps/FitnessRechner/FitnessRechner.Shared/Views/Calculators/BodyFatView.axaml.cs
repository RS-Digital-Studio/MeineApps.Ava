using Avalonia.Controls;
using Avalonia.Labs.Controls;
using FitnessRechner.Graphics;
using FitnessRechner.ViewModels.Calculators;
using SkiaSharp;

namespace FitnessRechner.Views.Calculators;

public partial class BodyFatView : UserControl
{
    private SKRect _lastHeaderBounds;
    private static readonly SKColor BodyFatDarkRed = SKColor.Parse("#DC2626");

    public BodyFatView()
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

        string title = "Körperfett";
        CalculatorHeaderRenderer.Render(canvas, bounds,
            title, MedicalColors.CriticalRed, BodyFatDarkRed, 0f);
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
            if (DataContext is BodyFatViewModel vm)
                vm.GoBackCommand.Execute(null);
        }
        e.Handled = true;
    }

    // ═══════ Körperfett-Visualisierung (BodyFatRenderer) ═══════

    private void OnPaintBodyFat(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (DataContext is BodyFatViewModel vm && vm.HasResult)
        {
            BodyFatRenderer.Render(canvas, canvas.LocalClipBounds,
                (float)vm.BodyFatValue, vm.IsMale, vm.HasResult);
        }
    }
}
