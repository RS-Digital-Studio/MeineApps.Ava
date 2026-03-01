using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// SkiaSharp Halbkreis-Tachometer für das Gesamtbudget.
/// Zeigt Verbrauch als farbigen Bogen mit Zonen (Grün → Gelb → Rot).
/// </summary>
public static class BudgetGaugeVisualization
{
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _tickPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _valueFont = new() { Size = 28f };
    private static readonly SKFont _labelFont = new() { Size = 11f };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    // Budget-Farben: Grün (ok) → Gelb (Warnung) → Rot (überschritten)
    private static readonly SKColor _greenColor = SKColor.Parse("#22C55E");
    private static readonly SKColor _yellowColor = SKColor.Parse("#F59E0B");
    private static readonly SKColor _redColor = SKColor.Parse("#EF4444");

    /// <summary>
    /// Statische Felder vorinitialisieren (SKPaint, SKFont, SKMaskFilter).
    /// Wird im SplashOverlay-Preloader aufgerufen um Jank beim ersten Render zu vermeiden.
    /// </summary>
    public static void WarmUp()
    {
        // Statische readonly-Felder (_trackPaint, _arcPaint, _glowPaint, _textPaint,
        // _tickPaint, _dotPaint, _valueFont, _labelFont, _glowFilter)
        // werden durch diesen Methodenaufruf vom CLR-Klassen-Initializer angelegt
    }

    /// <summary>
    /// Rendert den Budget-Halbkreis-Tachometer.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="percentage">Budget-Verbrauch in Prozent (0-150+)</param>
    /// <param name="spentDisplay">Anzeige-Text für ausgegeben (z.B. "€1.234")</param>
    /// <param name="limitDisplay">Anzeige-Text für Budget-Limit (z.B. "€2.000")</param>
    /// <param name="isOverLimit">Ob das Budget überschritten ist</param>
    // Hinweis: Nur vom UI-Thread aufrufen (statische Paints nicht thread-safe)
    public static void Render(SKCanvas canvas, SKRect bounds,
        double percentage, string spentDisplay, string limitDisplay, bool isOverLimit)
    {
        float cx = bounds.MidX;
        float cy = bounds.MidY + 10f; // Leicht nach unten versetzt für Text oben
        float strokeW = 8f;
        float size = Math.Min(bounds.Width, bounds.Height * 1.6f);
        float radius = (size - strokeW * 2 - 24f) / 2f;

        if (radius <= 15) return;

        // Halbkreis: von 180° (links) bis 360° (rechts) = Süd-Hälfte
        float startAngle = 180f;
        float sweepTotal = 180f;

        var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // 1. Track-Bogen (dezenter Hintergrund)
        _trackPaint.StrokeWidth = strokeW;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40);
        using (var trackPath = new SKPath())
        {
            trackPath.AddArc(arcRect, startAngle, sweepTotal);
            canvas.DrawPath(trackPath, _trackPaint);
        }

        // 2. Zonen-Ticks bei 25%, 50%, 75%, 100%
        DrawZoneTicks(canvas, cx, cy, radius, strokeW);

        // 3. Fortschritts-Bogen (farbig je nach Verbrauch)
        float clampedPercent = (float)Math.Clamp(percentage, 0, 120);
        float progressSweep = (clampedPercent / 120f) * sweepTotal; // 120% = voller Bogen

        if (progressSweep > 0.5f)
        {
            // Farbe basierend auf Prozent
            SKColor arcColor = GetBudgetColor(percentage);

            // Glow bei Warnung/Überschreitung
            if (percentage > 75)
            {
                _glowPaint.StrokeWidth = strokeW + 6f;
                _glowPaint.Color = arcColor.WithAlpha(50);
                _glowPaint.MaskFilter = _glowFilter;
                using var glowPath = new SKPath();
                glowPath.AddArc(arcRect, startAngle, progressSweep);
                canvas.DrawPath(glowPath, _glowPaint);
                _glowPaint.MaskFilter = null;
            }

            // Gradient-Arc
            _arcPaint.StrokeWidth = strokeW;
            _arcPaint.Color = arcColor;
            _arcPaint.Shader = SKShader.CreateSweepGradient(
                new SKPoint(cx, cy),
                new[] { _greenColor, arcColor },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp,
                startAngle, startAngle + progressSweep);

            using (var arcPath = new SKPath())
            {
                arcPath.AddArc(arcRect, startAngle, progressSweep);
                canvas.DrawPath(arcPath, _arcPaint);
            }
            _arcPaint.Shader = null;

            // Endpunkt-Dot
            float endAngleRad = (startAngle + progressSweep) * MathF.PI / 180f;
            float endX = cx + MathF.Cos(endAngleRad) * radius;
            float endY = cy + MathF.Sin(endAngleRad) * radius;

            _dotPaint.Color = arcColor;
            canvas.DrawCircle(endX, endY, strokeW * 0.7f, _dotPaint);
            _dotPaint.Color = SKColors.White.WithAlpha(200);
            canvas.DrawCircle(endX, endY, strokeW * 0.25f, _dotPaint);
        }

        // 4. Prozent-Text in der Mitte
        string percentText = $"{percentage:F0}%";
        _textPaint.Color = isOverLimit ? _redColor : SkiaThemeHelper.TextPrimary;
        _valueFont.Size = 28f;
        canvas.DrawText(percentText, cx, cy + 4f, SKTextAlign.Center, _valueFont, _textPaint);

        // 5. Labels links/rechts unten
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _labelFont.Size = 11f;
        canvas.DrawText("0%", cx - radius + 8f, cy + 18f, SKTextAlign.Center, _labelFont, _textPaint);
        canvas.DrawText("100%", cx + radius - 8f, cy + 18f, SKTextAlign.Center, _labelFont, _textPaint);

        // 6. Ausgegeben / Limit unter dem Bogen
        _textPaint.Color = SkiaThemeHelper.TextSecondary;
        _labelFont.Size = 11f;
        canvas.DrawText($"{spentDisplay} / {limitDisplay}", cx, cy + 34f, SKTextAlign.Center, _labelFont, _textPaint);
    }

    /// <summary>
    /// Zeichnet dezente Markierungen bei 25%, 50%, 75%, 100%.
    /// </summary>
    private static void DrawZoneTicks(SKCanvas canvas, float cx, float cy, float radius, float strokeW)
    {
        float[] zones = { 0.25f, 0.5f, 0.75f, 1.0f };
        float startAngle = 180f;
        float sweepTotal = 180f;
        float outerR = radius + strokeW / 2f + 2f;
        float innerR = radius - strokeW / 2f - 2f;

        foreach (var zone in zones)
        {
            float angle = startAngle + zone * (sweepTotal / 120f * 100f); // 100% bei 150° von 180°
            float angleRad = angle * MathF.PI / 180f;

            _tickPaint.StrokeWidth = zone == 1.0f ? 1.5f : 0.8f;
            _tickPaint.Color = zone == 1.0f
                ? _redColor.WithAlpha(120)
                : SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 60);

            canvas.DrawLine(
                cx + MathF.Cos(angleRad) * innerR,
                cy + MathF.Sin(angleRad) * innerR,
                cx + MathF.Cos(angleRad) * outerR,
                cy + MathF.Sin(angleRad) * outerR,
                _tickPaint);
        }
    }

    /// <summary>
    /// Bestimmt die Farbe basierend auf dem Budget-Verbrauch.
    /// </summary>
    private static SKColor GetBudgetColor(double percentage) => percentage switch
    {
        < 70 => _greenColor,
        < 90 => _yellowColor,
        _ => _redColor
    };
}
