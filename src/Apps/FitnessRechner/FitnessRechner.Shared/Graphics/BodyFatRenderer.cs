using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Körperfett-Visualisierung: Prozent-Ring mit Kategorie-Farbe + Körper-Silhouette.
/// Thread-safe: Verwendet lokale Paint-Objekte statt statischer Felder.
/// </summary>
public static class BodyFatRenderer
{
    public static void Render(SKCanvas canvas, SKRect bounds, float bodyFatPercent, bool isMale, bool hasResult)
    {
        if (!hasResult || bodyFatPercent <= 0) return;

        float w = bounds.Width;
        float h = bounds.Height;

        // Links: Körper-Silhouette (40%), Rechts: Prozent-Ring (60%)
        float silhouetteW = w * 0.35f;
        float ringW = w * 0.55f;
        float ringCx = bounds.Right - ringW * 0.5f - w * 0.05f;
        float ringCy = bounds.MidY;

        // Farbe nach Kategorie
        SKColor categoryColor = GetCategoryColor(bodyFatPercent, isMale);

        // === Körper-Silhouette (vereinfacht) ===
        float silCx = bounds.Left + silhouetteW * 0.5f + w * 0.05f;
        float silCy = bounds.MidY;
        float silScale = Math.Min(silhouetteW, h) * 0.007f;

        DrawSilhouette(canvas, silCx, silCy, silScale, bodyFatPercent, isMale, categoryColor);

        // === Prozent-Ring ===
        float strokeW = Math.Max(8f, ringW * 0.08f);
        float radius = Math.Min(ringW, h) * 0.38f;
        var arcRect = new SKRect(ringCx - radius, ringCy - radius, ringCx + radius, ringCy + radius);

        // Track
        using var trackPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeW,
            Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 30)
        };
        canvas.DrawOval(arcRect, trackPaint);

        // Fortschritts-Arc (max 100%)
        float fraction = Math.Clamp(bodyFatPercent / 50f, 0f, 1f); // 50% = voller Ring
        float sweepAngle = fraction * 360f;

        // Glow-Effekt auf dem Fortschritts-Arc
        using var glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeW + 4f,
            Color = SkiaThemeHelper.WithAlpha(categoryColor, 80),
            MaskFilter = glowFilter
        };
        using var glowPath = new SKPath();
        glowPath.AddArc(arcRect, -90f, sweepAngle);
        canvas.DrawPath(glowPath, glowPaint);

        // Fortschritts-Arc
        using var arcPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeW,
            Color = categoryColor
        };
        using var arcPath = new SKPath();
        arcPath.AddArc(arcRect, -90f, sweepAngle);
        canvas.DrawPath(arcPath, arcPaint);

        // Prozentwert in der Mitte
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SkiaThemeHelper.TextPrimary,
            TextSize = radius * 0.4f,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true
        };
        canvas.DrawText($"{bodyFatPercent:F1}%", ringCx, ringCy + textPaint.TextSize * 0.35f, textPaint);
    }

    private static void DrawSilhouette(SKCanvas canvas, float cx, float cy, float scale, float bodyFatPercent, bool isMale, SKColor color)
    {
        // Vereinfachte Silhouette als Kopf + Körper
        float headR = 12f * scale;
        float bodyW = (isMale ? 22f : 20f) * scale;
        float bodyH = 45f * scale;

        // "Fett"-Faktor: mehr Fett = breiterer Körper
        float fatFactor = 1f + (bodyFatPercent - 15f) / 100f; // ab 15% wird breiter
        fatFactor = Math.Clamp(fatFactor, 0.9f, 1.5f);
        bodyW *= fatFactor;

        float headY = cy - bodyH * 0.5f - headR - 2f * scale;

        // Lokales Fill-Paint für Silhouette
        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // Hintergrund-Silhouette
        fillPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 40);
        canvas.DrawCircle(cx, headY, headR, fillPaint);
        canvas.DrawRoundRect(cx - bodyW / 2f, cy - bodyH * 0.35f, bodyW, bodyH, 8f * scale, 8f * scale, fillPaint);

        // Arme
        float armW = 6f * scale;
        float armH = 30f * scale;
        canvas.DrawRoundRect(cx - bodyW / 2f - armW - 2f * scale, cy - bodyH * 0.25f, armW, armH, 3f * scale, 3f * scale, fillPaint);
        canvas.DrawRoundRect(cx + bodyW / 2f + 2f * scale, cy - bodyH * 0.25f, armW, armH, 3f * scale, 3f * scale, fillPaint);

        // Beine
        float legW = 8f * scale * fatFactor;
        float legH = 35f * scale;
        canvas.DrawRoundRect(cx - legW - 1f * scale, cy + bodyH * 0.55f, legW, legH, 4f * scale, 4f * scale, fillPaint);
        canvas.DrawRoundRect(cx + 1f * scale, cy + bodyH * 0.55f, legW, legH, 4f * scale, 4f * scale, fillPaint);

        // Farbige Overlay (Fett-Bereich am Bauch)
        float fatH = bodyH * Math.Clamp(bodyFatPercent / 40f, 0.2f, 0.8f);
        float fatY = cy - bodyH * 0.35f + (bodyH - fatH) * 0.3f;
        fillPaint.Color = SkiaThemeHelper.WithAlpha(color, 80);

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(cx - bodyW / 2f, cy - bodyH * 0.35f, cx + bodyW / 2f, cy - bodyH * 0.35f + bodyH), 8f * scale));
        canvas.DrawRoundRect(cx - bodyW / 2f + 2f, fatY, bodyW - 4f, fatH, 6f * scale, 6f * scale, fillPaint);
        canvas.Restore();
    }

    private static SKColor GetCategoryColor(float bodyFatPercent, bool isMale)
    {
        if (isMale)
        {
            return bodyFatPercent switch
            {
                < 6f => new SKColor(0x3B, 0x82, 0xF6),  // Essential - Blau
                < 14f => new SKColor(0x22, 0xC5, 0x5E), // Athletes - Grün
                < 18f => new SKColor(0x22, 0xC5, 0x5E), // Fitness - Grün
                < 25f => new SKColor(0xF5, 0x9E, 0x0B), // Average - Gelb
                _ => new SKColor(0xEF, 0x44, 0x44),      // Obese - Rot
            };
        }
        return bodyFatPercent switch
        {
            < 14f => new SKColor(0x3B, 0x82, 0xF6),
            < 21f => new SKColor(0x22, 0xC5, 0x5E),
            < 25f => new SKColor(0x22, 0xC5, 0x5E),
            < 32f => new SKColor(0xF5, 0x9E, 0x0B),
            _ => new SKColor(0xEF, 0x44, 0x44),
        };
    }
}
