using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// Konzentrische Ringe für Urlaubsquote:
/// Äußerer Ring = Gesamtanspruch, mittlerer = genommen, innerer = Resturlaub.
/// Zentraler Text "12/30 Tage", Farbe wechselt grün→gelb→rot je nach Verbrauch.
/// </summary>
public static class VacationQuotaGaugeVisualization
{
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _valueFont = new() { Size = 22f };
    private static readonly SKFont _labelFont = new() { Size = 10f };
    private static readonly SKFont _ringLabelFont = new() { Size = 8f };

    // Farben für Verbrauchsstufen
    private static readonly SKColor _lowUsage = new(0x22, 0xC5, 0x5E);    // Grün (<50%)
    private static readonly SKColor _mediumUsage = new(0xF5, 0x9E, 0x0B);  // Amber (50-80%)
    private static readonly SKColor _highUsage = new(0xEF, 0x44, 0x44);    // Rot (>80%)

    // Ring-Farben
    private static readonly SKColor _usedColor = new(0x38, 0xBD, 0xF8);    // Blau (Genommen)
    private static readonly SKColor _plannedColor = new(0xA7, 0x8B, 0xFA); // Violett (Geplant)
    private static readonly SKColor _remainColor = new(0x22, 0xC5, 0x5E);  // Grün (Rest)

    /// <summary>
    /// Rendert die Urlaubsquote als konzentrische Ringe.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="totalDays">Gesamtanspruch (z.B. 30)</param>
    /// <param name="usedDays">Genommene Tage (z.B. 12)</param>
    /// <param name="plannedDays">Geplante Tage (z.B. 5)</param>
    /// <param name="remainingDays">Resturlaub (z.B. 13)</param>
    /// <param name="usedLabel">Label "Genommen"</param>
    /// <param name="plannedLabel">Label "Geplant"</param>
    /// <param name="remainLabel">Label "Rest"</param>
    /// <param name="daysLabel">Label "Tage"</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        float totalDays, float usedDays, float plannedDays, float remainingDays,
        string usedLabel = "Genommen", string plannedLabel = "Geplant",
        string remainLabel = "Rest", string daysLabel = "Tage")
    {
        if (totalDays <= 0) return;

        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float maxR = MathF.Min(bounds.Width, bounds.Height) / 2f - 8f;

        // 3 Ringe: außen (Genommen), mitte (Geplant), innen (Rest)
        float ringWidth = maxR * 0.12f;
        ringWidth = MathF.Max(ringWidth, 8f);
        float ringGap = 4f;

        float outerR = maxR - ringWidth / 2f;
        float middleR = outerR - ringWidth - ringGap;
        float innerR = middleR - ringWidth - ringGap;

        // Fortschritte berechnen
        float usedFrac = MathF.Min(usedDays / totalDays, 1f);
        float plannedFrac = MathF.Min(plannedDays / totalDays, 1f);
        float remainFrac = MathF.Min(remainingDays / totalDays, 1f);

        // === Äußerer Ring: Genommen ===
        DrawRing(canvas, cx, cy, outerR, ringWidth, usedFrac, _usedColor);

        // === Mittlerer Ring: Geplant ===
        DrawRing(canvas, cx, cy, middleR, ringWidth, plannedFrac, _plannedColor);

        // === Innerer Ring: Rest ===
        DrawRing(canvas, cx, cy, innerR, ringWidth, remainFrac, _remainColor);

        // === Zentraler Text ===
        float usedPercent = (usedDays / totalDays) * 100f;
        SKColor valueColor = usedPercent < 50 ? _lowUsage : usedPercent < 80 ? _mediumUsage : _highUsage;

        // Hauptwert: "12/30"
        _textPaint.Color = valueColor;
        _valueFont.Size = innerR * 0.55f;
        _valueFont.Size = Math.Clamp(_valueFont.Size, 14f, 28f);
        string valueText = $"{usedDays:F0}/{totalDays:F0}";
        canvas.DrawText(valueText, cx, cy + _valueFont.Size * 0.15f,
            SKTextAlign.Center, _valueFont, _textPaint);

        // Label: "Tage"
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _ringLabelFont.Size = Math.Clamp(innerR * 0.25f, 8f, 12f);
        canvas.DrawText(daysLabel, cx, cy + _valueFont.Size * 0.15f + _ringLabelFont.Size + 4f,
            SKTextAlign.Center, _ringLabelFont, _textPaint);

        // === Legende unter den Ringen ===
        float legendY = cy + maxR + 8f;
        float legendSpacing = bounds.Width / 3f;
        float legendStartX = bounds.Left + legendSpacing / 2f;

        DrawLegendItem(canvas, legendStartX, legendY, _usedColor, usedLabel, $"{usedDays:F0}");
        DrawLegendItem(canvas, legendStartX + legendSpacing, legendY, _plannedColor, plannedLabel, $"{plannedDays:F0}");
        DrawLegendItem(canvas, legendStartX + legendSpacing * 2, legendY, _remainColor, remainLabel, $"{remainingDays:F0}");
    }

    /// <summary>
    /// Zeichnet einen einzelnen Ring-Track + Fortschritts-Arc.
    /// </summary>
    private static void DrawRing(SKCanvas canvas, float cx, float cy,
        float radius, float width, float fraction, SKColor color)
    {
        // Track (dezent)
        _trackPaint.StrokeWidth = width;
        _trackPaint.StrokeCap = SKStrokeCap.Round;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40);
        var trackRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
        canvas.DrawArc(trackRect, -90f, 360f, false, _trackPaint);

        if (fraction <= 0) return;

        // Glow (breiterer, transparenter Arc)
        float sweepAngle = MathF.Min(fraction * 360f, 360f);
        float glowWidth = width + 4f;
        _glowPaint.StrokeWidth = glowWidth;
        _glowPaint.StrokeCap = SKStrokeCap.Round;
        _glowPaint.Color = color.WithAlpha(30);
        using (var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f))
        {
            _glowPaint.MaskFilter = blur;
            canvas.DrawArc(trackRect, -90f, sweepAngle, false, _glowPaint);
            _glowPaint.MaskFilter = null;
        }

        // Fortschritts-Arc
        _arcPaint.StrokeWidth = width;
        _arcPaint.StrokeCap = SKStrokeCap.Round;
        _arcPaint.Color = color;
        canvas.DrawArc(trackRect, -90f, sweepAngle, false, _arcPaint);

        // Endpunkt-Dot (leuchtend)
        if (fraction > 0.01f && fraction < 0.99f)
        {
            float endAngle = (-90f + sweepAngle) * MathF.PI / 180f;
            float dotX = cx + radius * MathF.Cos(endAngle);
            float dotY = cy + radius * MathF.Sin(endAngle);
            _dotPaint.Color = SKColors.White;
            canvas.DrawCircle(dotX, dotY, width / 3f, _dotPaint);
        }
    }

    /// <summary>
    /// Zeichnet einen Legende-Eintrag (Punkt + Label + Wert).
    /// </summary>
    private static void DrawLegendItem(SKCanvas canvas, float cx, float y,
        SKColor color, string label, string value)
    {
        float dotR = 4f;

        // Farbpunkt
        _dotPaint.Color = color;
        canvas.DrawCircle(cx, y + 6f, dotR, _dotPaint);

        // Wert (fett, darunter)
        _textPaint.Color = SkiaThemeHelper.TextPrimary;
        _labelFont.Size = 11f;
        canvas.DrawText(value, cx, y + 20f, SKTextAlign.Center, _labelFont, _textPaint);

        // Label (klein, darunter)
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _ringLabelFont.Size = 8f;
        canvas.DrawText(label, cx, y + 30f, SKTextAlign.Center, _ringLabelFont, _textPaint);
    }
}
