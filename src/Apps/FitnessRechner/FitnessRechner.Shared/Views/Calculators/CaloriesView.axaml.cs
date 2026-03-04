using Avalonia.Controls;
using Avalonia.Labs.Controls;
using FitnessRechner.Graphics;
using FitnessRechner.ViewModels.Calculators;
using SkiaSharp;

namespace FitnessRechner.Views.Calculators;

public partial class CaloriesView : UserControl
{
    private SKRect _lastHeaderBounds;

    public CaloriesView()
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

        string title = "Kalorien";
        CalculatorHeaderRenderer.Render(canvas, bounds,
            title, MedicalColors.CalorieAmber, MedicalColors.CalorieAmberDark, 0f);
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
            if (DataContext is CaloriesViewModel vm)
                vm.GoBackCommand.Execute(null);
        }
        e.Handled = true;
    }

    // ═══════ Kalorien-Ring (CalorieRingRenderer) ═══════

    private void OnPaintCalorieRings(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (DataContext is CaloriesViewModel vm && vm.HasResult && vm.Result != null)
        {
            CalorieRingRenderer.Render(canvas, canvas.LocalClipBounds,
                (float)vm.Result.Bmr, (float)vm.Result.Tdee,
                (float)vm.Result.WeightLossCalories, (float)vm.Result.WeightGainCalories,
                vm.HasResult);
        }
    }
}
