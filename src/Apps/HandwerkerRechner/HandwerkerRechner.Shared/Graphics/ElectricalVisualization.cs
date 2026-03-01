using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// 3 Sub: Spannungsabfall-Kurve, Jahreskosten-Balken, Ohmsches Dreieck.
/// </summary>
public static class ElectricalVisualization
{
    // Einschwing-Animation
    private static readonly AnimatedVisualizationBase _animation = new()
    {
        AnimationDurationMs = 500f,
        EasingFunction = EasingFunctions.EaseOutCubic
    };

    /// <summary>Startet die Einschwing-Animation.</summary>
    public static void StartAnimation() => _animation.StartAnimation();

    /// <summary>True wenn noch animiert wird (für InvalidateSurface-Loop).</summary>
    public static bool NeedsRedraw => _animation.IsAnimating;

    private static readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _axisPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private static readonly SKPaint _layerPaint = new() { IsAntialias = false };

    /// <summary>
    /// subType: 0=Spannungsabfall, 1=Stromkosten, 2=Ohmsches Gesetz
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, int subType,
        float voltage, float voltageDrop, float percentDrop, bool isAcceptable, float cableLength,
        float costPerDay, float costPerMonth, float costPerYear,
        float ohmsV, float ohmsI, float ohmsR, float ohmsP,
        bool hasResult)
    {
        if (!hasResult) return;

        _animation.UpdateAnimation();
        float progress = _animation.AnimationProgress;

        // Global Alpha Fade-In
        _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * progress));
        canvas.SaveLayer(_layerPaint);

        switch (subType)
        {
            case 0: RenderVoltageDrop(canvas, bounds, voltage, voltageDrop, percentDrop, isAcceptable, cableLength); break;
            case 1: RenderPowerCost(canvas, bounds, costPerDay, costPerMonth, costPerYear); break;
            case 2: RenderOhmsLaw(canvas, bounds, ohmsV, ohmsI, ohmsR, ohmsP); break;
        }

        canvas.Restore();
    }

    /// <summary>
    /// Spannungsabfall als Kurve: X = Kabellänge, Y = Spannung.
    /// </summary>
    private static void RenderVoltageDrop(SKCanvas canvas, SKRect bounds,
        float voltage, float voltageDrop, float percentDrop, bool isAcceptable, float cableLength)
    {
        if (voltage <= 0 || cableLength <= 0) return;

        float padding = 40f;
        float graphL = bounds.Left + padding;
        float graphR = bounds.Right - 20f;
        float graphT = bounds.Top + 20f;
        float graphB = bounds.Bottom - 30f;
        float graphW = graphR - graphL;
        float graphH = graphB - graphT;

        // Achsen
        _axisPaint.Color = SkiaThemeHelper.TextMuted;
        canvas.DrawLine(graphL, graphB, graphR, graphB, _axisPaint); // X-Achse
        canvas.DrawLine(graphL, graphT, graphL, graphB, _axisPaint); // Y-Achse

        // Spannungskurve (linear abfallend)
        float endVoltage = voltage - voltageDrop;
        float voltMin = endVoltage * 0.95f;
        float voltRange = voltage * 1.05f - voltMin;

        SKColor lineColor = isAcceptable ? SkiaThemeHelper.Success : SkiaThemeHelper.Error;

        // Verlaufs-Füllung unter der Kurve
        using var fillPath = new SKPath();
        fillPath.MoveTo(graphL, graphT + (voltage * 1.05f - voltage) / voltRange * graphH);

        int steps = 20;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float v = voltage - voltageDrop * t;
            float x = graphL + t * graphW;
            float y = graphT + (voltage * 1.05f - v) / voltRange * graphH;
            fillPath.LineTo(x, y);
        }
        fillPath.LineTo(graphR, graphB);
        fillPath.LineTo(graphL, graphB);
        fillPath.Close();

        _fillPaint.Color = SkiaThemeHelper.WithAlpha(lineColor, 30);
        canvas.DrawPath(fillPath, _fillPaint);

        // Linie
        _linePaint.Color = lineColor;
        float startY = graphT + (voltage * 1.05f - voltage) / voltRange * graphH;
        float endY = graphT + (voltage * 1.05f - endVoltage) / voltRange * graphH;
        canvas.DrawLine(graphL, startY, graphR, endY, _linePaint);

        // 3% Grenzlinie
        float limitVolt = voltage * 0.97f;
        float limitY = graphT + (voltage * 1.05f - limitVolt) / voltRange * graphH;
        SkiaBlueprintCanvas.DrawDashedLine(canvas,
            new SKPoint(graphL, limitY), new SKPoint(graphR, limitY),
            SkiaThemeHelper.Warning, 4f, 3f);

        SkiaBlueprintCanvas.DrawMeasurementText(canvas, "3% Max",
            new SKPoint(graphR - 20f, limitY - 8f),
            SkiaThemeHelper.Warning, 8f);

        // Beschriftungen
        SkiaBlueprintCanvas.DrawMeasurementText(canvas, $"{voltage:F0}V",
            new SKPoint(graphL - 18f, startY), SkiaThemeHelper.TextSecondary, 8f);

        SkiaBlueprintCanvas.DrawMeasurementText(canvas, $"{endVoltage:F1}V",
            new SKPoint(graphR + 2f, endY), lineColor, 8f, SKTextAlign.Left);

        SkiaBlueprintCanvas.DrawMeasurementText(canvas, $"{cableLength:F0}m",
            new SKPoint(graphR, graphB + 12f), SkiaThemeHelper.TextMuted, 8f);

        SkiaBlueprintCanvas.DrawMeasurementText(canvas, $"-{percentDrop:F2}%",
            new SKPoint(bounds.MidX, graphT - 4f), lineColor, 10f);
    }

    /// <summary>
    /// Stromkosten als 3 Balken: Tag / Monat / Jahr.
    /// </summary>
    private static void RenderPowerCost(SKCanvas canvas, SKRect bounds,
        float costPerDay, float costPerMonth, float costPerYear)
    {
        float padding = 35f;
        float graphL = bounds.Left + padding;
        float graphR = bounds.Right - 20f;
        float graphT = bounds.Top + 20f;
        float graphB = bounds.Bottom - 25f;
        float graphW = graphR - graphL;
        float graphH = graphB - graphT;

        float maxVal = Math.Max(0.01f, costPerYear);
        float[] values = { costPerDay, costPerMonth, costPerYear };
        string[] labels = { "Tag", "Monat", "Jahr" };
        SKColor[] colors = { SkiaThemeHelper.Success, SkiaThemeHelper.Warning, SkiaThemeHelper.Accent };

        // X-Achse
        _axisPaint.Color = SkiaThemeHelper.TextMuted;
        canvas.DrawLine(graphL, graphB, graphR, graphB, _axisPaint);

        float barWidth = graphW / 5f;
        float gap = barWidth * 0.5f;
        float startX = graphL + gap;

        for (int i = 0; i < 3; i++)
        {
            float x = startX + i * (barWidth + gap);
            float barH = (values[i] / maxVal) * graphH * 0.9f;
            barH = Math.Max(barH, 4f);
            float y = graphB - barH;

            // Balken mit Gradient
            _barPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(x, y), new SKPoint(x, graphB),
                new[] { colors[i], SkiaThemeHelper.AdjustBrightness(colors[i], 0.6f) },
                new[] { 0f, 1f }, SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(x, y, x + barWidth, graphB), 4f), _barPaint);
            _barPaint.Shader = null;

            // Wert über dem Balken
            SkiaBlueprintCanvas.DrawMeasurementText(canvas,
                $"{values[i]:F2} €",
                new SKPoint(x + barWidth / 2f, y - 6f),
                colors[i], 9f);

            // Label unter dem Balken
            SkiaBlueprintCanvas.DrawMeasurementText(canvas,
                labels[i],
                new SKPoint(x + barWidth / 2f, graphB + 12f),
                SkiaThemeHelper.TextMuted, 8f);
        }
    }

    /// <summary>
    /// Ohmsches Dreieck: U oben, I links unten, R rechts unten.
    /// </summary>
    private static void RenderOhmsLaw(SKCanvas canvas, SKRect bounds,
        float ohmsV, float ohmsI, float ohmsR, float ohmsP)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float size = Math.Min(bounds.Width, bounds.Height) * 0.35f;

        // Dreieck zeichnen
        float topY = cy - size * 0.6f;
        float bottomY = cy + size * 0.5f;
        float leftX = cx - size * 0.7f;
        float rightX = cx + size * 0.7f;

        using var triPath = new SKPath();
        triPath.MoveTo(cx, topY);
        triPath.LineTo(rightX, bottomY);
        triPath.LineTo(leftX, bottomY);
        triPath.Close();

        _fillPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Accent, 20);
        canvas.DrawPath(triPath, _fillPaint);

        _linePaint.Color = SkiaThemeHelper.TextSecondary;
        _linePaint.StrokeWidth = 1.5f;
        canvas.DrawPath(triPath, _linePaint);
        _linePaint.StrokeWidth = 2.5f;

        // Trennlinie horizontal (U oben, I×R unten)
        float midY = topY + (bottomY - topY) * 0.45f;
        float lineLeftX = leftX + (cx - leftX) * 0.45f;
        float lineRightX = rightX - (rightX - cx) * 0.45f;
        _axisPaint.Color = SkiaThemeHelper.TextMuted;
        canvas.DrawLine(lineLeftX, midY, lineRightX, midY, _axisPaint);

        // U oben
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"U = {ohmsV:F2} V",
            new SKPoint(cx, topY + (midY - topY) / 2f + 2f),
            SkiaThemeHelper.Warning, 11f);

        // I links unten
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"I = {ohmsI:F2} A",
            new SKPoint(cx - size * 0.25f, midY + (bottomY - midY) / 2f + 2f),
            SkiaThemeHelper.Accent, 10f);

        // Multiplikationszeichen
        SkiaBlueprintCanvas.DrawMeasurementText(canvas, "×",
            new SKPoint(cx, midY + (bottomY - midY) / 2f + 2f),
            SkiaThemeHelper.TextMuted, 10f);

        // R rechts unten
        SkiaBlueprintCanvas.DrawMeasurementText(canvas,
            $"R = {ohmsR:F2} Ω",
            new SKPoint(cx + size * 0.25f, midY + (bottomY - midY) / 2f + 2f),
            SkiaThemeHelper.Error, 10f);

        // P unterhalb des Dreiecks
        if (ohmsP > 0)
        {
            SkiaBlueprintCanvas.DrawMeasurementText(canvas,
                $"P = {ohmsP:F2} W",
                new SKPoint(cx, bottomY + 16f),
                SkiaThemeHelper.Success, 10f);
        }
    }
}
