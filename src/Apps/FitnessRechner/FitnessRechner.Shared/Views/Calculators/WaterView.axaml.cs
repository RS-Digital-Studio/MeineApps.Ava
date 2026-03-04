using Avalonia.Controls;
using Avalonia.Labs.Controls;
using FitnessRechner.Graphics;
using FitnessRechner.ViewModels.Calculators;
using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FitnessRechner.Views.Calculators;

public partial class WaterView : UserControl
{
    private SKRect _lastHeaderBounds;

    public WaterView()
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

        string title = "Wasser";
        CalculatorHeaderRenderer.Render(canvas, bounds,
            title, MedicalColors.WaterGreen, MedicalColors.WaterGreenDark, 0f);
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
            if (DataContext is WaterViewModel vm)
                vm.GoBackCommand.Execute(null);
        }
        e.Handled = true;
    }

    // ═══════ Wasserglas-Visualisierung ═══════

    private void OnPaintWaterGlass(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (DataContext is WaterViewModel vm && vm.HasResult)
        {
            var bounds = canvas.LocalClipBounds;
            float fillPercent = (float)Math.Clamp(vm.TotalLitersValue / 4.0, 0.05, 1.0);
            DrawWaterGlass(canvas, bounds, fillPercent);
        }
    }

    private static readonly SKPaint _glassPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f };
    private static readonly SKPaint _waterPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glanzPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true };

    /// <summary>Einfaches Wasserglas mit Füllstand und Wellen.</summary>
    private static void DrawWaterGlass(SKCanvas canvas, SKRect bounds, float fillPercent)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float glassW = Math.Min(bounds.Width * 0.35f, 80f);
        float glassH = Math.Min(bounds.Height * 0.75f, 120f);

        float left = cx - glassW / 2f;
        float right = cx + glassW / 2f;
        float top = cy - glassH / 2f;
        float bottom = cy + glassH / 2f;

        // Leicht trapezförmiges Glas (unten schmaler)
        float taper = glassW * 0.08f;
        float bleft = left + taper;
        float bright = right - taper;

        // Wasser-Füllstand
        float waterH = glassH * fillPercent;
        float waterTop = bottom - waterH;

        // Wasser füllen (Cyan-Gradient)
        var waterColor = new SKColor(0x22, 0xC5, 0x5E);
        _waterPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(cx, waterTop), new SKPoint(cx, bottom),
            new[] { SkiaThemeHelper.WithAlpha(waterColor, 160), SkiaThemeHelper.WithAlpha(new SKColor(0x06, 0xB6, 0xD4), 200) },
            null, SKShaderTileMode.Clamp);

        // Wasser als Trapez-Path
        float waterLeftTop = Lerp(bleft, left, (bottom - waterTop) / glassH);
        float waterRightTop = Lerp(bright, right, (bottom - waterTop) / glassH);

        using var waterPath = new SKPath();
        waterPath.MoveTo(waterLeftTop, waterTop);
        // Wellen-Linie oben
        float waveAmp = 3f;
        for (float wx = waterLeftTop; wx <= waterRightTop; wx += 4f)
        {
            float wy = waterTop + MathF.Sin(wx * 0.08f) * waveAmp;
            waterPath.LineTo(wx, wy);
        }
        waterPath.LineTo(bright, bottom);
        waterPath.LineTo(bleft, bottom);
        waterPath.Close();
        canvas.DrawPath(waterPath, _waterPaint);
        _waterPaint.Shader = null;

        // Glas-Umriss
        _glassPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 100);
        using var glassPath = new SKPath();
        glassPath.MoveTo(left, top);
        glassPath.LineTo(right, top);
        glassPath.LineTo(bright, bottom);
        glassPath.LineTo(bleft, bottom);
        glassPath.Close();
        canvas.DrawPath(glassPath, _glassPaint);

        // Glas-Glanz (weißer Streifen links)
        _glanzPaint.Color = new SKColor(255, 255, 255, 30);
        canvas.DrawRect(left + 3f, top + 4f, 4f, glassH - 8f, _glanzPaint);

        // Prozent-Text unter dem Glas
        _textPaint.Color = SkiaThemeHelper.TextSecondary;
        _textPaint.TextSize = 12f;
        _textPaint.TextAlign = SKTextAlign.Center;
        canvas.DrawText($"{fillPercent * 100:F0}%", cx, bottom + 16f, _textPaint);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
