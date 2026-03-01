using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Kalorien-Aufschlüsselung als 3 konzentrische Ringe (BMR, TDEE, Ziele)
/// plus Makro-Anteile als Kreissegmente.
/// Thread-safe: Verwendet lokale Paint-Objekte statt statischer Felder.
/// </summary>
public static class CalorieRingRenderer
{
    // Ring-Farben (thread-safe, immutable structs)
    private static readonly SKColor _bmrColor = new(0x3B, 0x82, 0xF6);    // Blau
    private static readonly SKColor _tdeeColor = new(0xF5, 0x9E, 0x0B);   // Orange
    private static readonly SKColor _lossColor = new(0x22, 0xC5, 0x5E);   // Grün
    private static readonly SKColor _gainColor = new(0xEF, 0x44, 0x44);   // Rot

    public static void Render(SKCanvas canvas, SKRect bounds,
        float bmr, float tdee, float weightLoss, float weightGain, bool hasResult)
    {
        if (!hasResult || tdee <= 0) return;

        float w = bounds.Width;
        float h = bounds.Height;
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        float maxRadius = Math.Min(w, h) * 0.42f;
        float strokeW = Math.Max(6f, maxRadius * 0.08f);
        float ringGap = strokeW + 4f;

        // 3 Ringe von außen nach innen
        float r1 = maxRadius;                    // Außen: TDEE
        float r2 = maxRadius - ringGap;          // Mitte: BMR
        float r3 = maxRadius - ringGap * 2f;     // Innen: Verlust/Zunahme

        // Max-Wert für Normalisierung
        float maxVal = Math.Max(tdee, weightGain) * 1.1f;

        // Track-Kreise
        using var trackPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeW,
            Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 25)
        };
        canvas.DrawCircle(cx, cy, r1, trackPaint);
        canvas.DrawCircle(cx, cy, r2, trackPaint);
        canvas.DrawCircle(cx, cy, r3, trackPaint);

        // Ring 1: TDEE (außen, Orange)
        DrawRing(canvas, cx, cy, r1, strokeW, tdee / maxVal, _tdeeColor);

        // Ring 2: BMR (mitte, Blau)
        DrawRing(canvas, cx, cy, r2, strokeW, bmr / maxVal, _bmrColor);

        // Ring 3: WeightLoss/WeightGain (innen, als Hälften)
        float lossAngle = (weightLoss / maxVal) * 180f;
        float gainAngle = (weightGain / maxVal) * 180f;

        using var arcPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeW
        };

        var r3Rect = new SKRect(cx - r3, cy - r3, cx + r3, cy + r3);

        // Gewichtsverlust-Hälfte (Grün)
        arcPaint.Color = _lossColor;
        using (var lossPath = new SKPath())
        {
            lossPath.AddArc(r3Rect, -90f, lossAngle);
            canvas.DrawPath(lossPath, arcPaint);
        }

        // Gewichtszunahme-Hälfte (Rot)
        arcPaint.Color = _gainColor;
        using (var gainPath = new SKPath())
        {
            gainPath.AddArc(r3Rect, -90f + 180f, gainAngle);
            canvas.DrawPath(gainPath, arcPaint);
        }

        // TDEE-Text in der Mitte
        using var textPaint = new SKPaint { IsAntialias = true };

        textPaint.Color = SkiaThemeHelper.TextPrimary;
        textPaint.TextSize = Math.Max(14f, r3 * 0.35f);
        textPaint.TextAlign = SKTextAlign.Center;
        textPaint.FakeBoldText = true;
        canvas.DrawText($"{tdee:F0}", cx, cy + textPaint.TextSize * 0.15f, textPaint);

        textPaint.TextSize = Math.Max(8f, r3 * 0.18f);
        textPaint.FakeBoldText = false;
        textPaint.Color = SkiaThemeHelper.TextMuted;
        canvas.DrawText("kcal", cx, cy + r3 * 0.35f, textPaint);

        // Legende unten
        float legendY = cy + maxRadius + strokeW + 14f;
        float legendSpacing = w / 4f;
        DrawLegendDot(canvas, cx - legendSpacing * 1.3f, legendY, _tdeeColor, "TDEE");
        DrawLegendDot(canvas, cx - legendSpacing * 0.35f, legendY, _bmrColor, "BMR");
        DrawLegendDot(canvas, cx + legendSpacing * 0.55f, legendY, _lossColor, "-");
        DrawLegendDot(canvas, cx + legendSpacing * 1.2f, legendY, _gainColor, "+");
    }

    private static void DrawRing(SKCanvas canvas, float cx, float cy, float radius, float strokeW, float fraction, SKColor color)
    {
        float sweepAngle = Math.Clamp(fraction, 0f, 1f) * 360f;
        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // Glow-Effekt
        using var glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeW + 3f,
            Color = SkiaThemeHelper.WithAlpha(color, 60),
            MaskFilter = glowFilter
        };
        using var glowPath = new SKPath();
        glowPath.AddArc(rect, -90f, sweepAngle);
        canvas.DrawPath(glowPath, glowPaint);

        // Fortschritts-Arc
        using var arcPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeW,
            Color = color
        };
        using var arcPath = new SKPath();
        arcPath.AddArc(rect, -90f, sweepAngle);
        canvas.DrawPath(arcPath, arcPaint);
    }

    private static void DrawLegendDot(SKCanvas canvas, float x, float y, SKColor color, string label)
    {
        using var dotPaint = new SKPaint { IsAntialias = true, Color = color, Style = SKPaintStyle.Fill };
        canvas.DrawCircle(x, y, 4f, dotPaint);

        using var labelPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SkiaThemeHelper.TextMuted,
            TextSize = 9f,
            TextAlign = SKTextAlign.Left,
            FakeBoldText = false
        };
        canvas.DrawText(label, x + 7f, y + 3.5f, labelPaint);
    }
}
