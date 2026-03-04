using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Kalorien-Aufschlüsselung als 3 konzentrische Ringe (BMR, TDEE, Ziele)
/// plus Makro-Anteile als Kreissegmente.
/// Medical-Ästhetik: Grid-Hintergrund, pulsierender Glow (72 BPM), Data-Stream Partikel.
/// Thread-safe: Verwendet lokale Paint-Objekte statt statischer Felder.
/// </summary>
public static class CalorieRingRenderer
{
    // Ring-Farben (thread-safe, immutable structs)
    private static readonly SKColor _bmrColor = new(0x3B, 0x82, 0xF6);    // Blau
    private static readonly SKColor _tdeeColor = new(0xF5, 0x9E, 0x0B);   // Orange
    private static readonly SKColor _lossColor = new(0x22, 0xC5, 0x5E);   // Grün
    private static readonly SKColor _gainColor = new(0xEF, 0x44, 0x44);   // Rot

    // Medical Grid
    private const float GridSpacing = 40f;
    private const byte GridAlpha = 20; // ~8% von 255

    // Data-Stream Partikel (kreisförmig zwischen den Ringen)
    private const int ParticleCount = 5;
    private const float ParticleRadius = 2.5f;

    public static void Render(SKCanvas canvas, SKRect bounds,
        float bmr, float tdee, float weightLoss, float weightGain, bool hasResult, float time = 0f)
    {
        if (!hasResult || tdee <= 0) return;

        float w = bounds.Width;
        float h = bounds.Height;
        float cx = bounds.MidX;
        float cy = bounds.MidY;

        // --- Medical Grid im Hintergrund ---
        RenderMedicalGrid(canvas, bounds);

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

        // Pulsierender Glow-Faktor (72 BPM synchron)
        float beatPhase = (time * MedicalColors.BeatsPerSecond) % 1f;
        // Sinuswelle 0→1→0 pro Beat, Alpha 30-60%
        float pulseAlpha = 0.3f + 0.3f * MathF.Sin(beatPhase * MathF.PI * 2f) * 0.5f + 0.15f;
        byte glowAlphaByte = (byte)(255 * Math.Clamp(pulseAlpha, 0.3f, 0.6f));

        // Ring 1: TDEE (außen, Orange)
        DrawRing(canvas, cx, cy, r1, strokeW, tdee / maxVal, _tdeeColor, glowAlphaByte);

        // Ring 2: BMR (mitte, Blau)
        DrawRing(canvas, cx, cy, r2, strokeW, bmr / maxVal, _bmrColor, glowAlphaByte);

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

        // --- Data-Stream Partikel zwischen den Ringen ---
        RenderDataStreamParticles(canvas, cx, cy, r2, r1, time);

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

    /// <summary>
    /// Medical Grid: Feine Cyan-Linien im Hintergrund (8% Opacity).
    /// </summary>
    private static void RenderMedicalGrid(SKCanvas canvas, SKRect bounds)
    {
        using var gridPaint = new SKPaint
        {
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
            Color = MedicalColors.Grid.WithAlpha(GridAlpha)
        };

        for (float x = bounds.Left + GridSpacing; x < bounds.Right; x += GridSpacing)
            canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, gridPaint);

        for (float y = bounds.Top + GridSpacing; y < bounds.Bottom; y += GridSpacing)
            canvas.DrawLine(bounds.Left, y, bounds.Right, y, gridPaint);
    }

    /// <summary>
    /// Data-Stream Partikel: 5 kleine Cyan-Punkte die kreisförmig zwischen den Ringen fließen.
    /// Position wird rein mathematisch aus time berechnet (keine persistenten Objekte nötig).
    /// </summary>
    private static void RenderDataStreamParticles(SKCanvas canvas, float cx, float cy,
        float innerRadius, float outerRadius, float time)
    {
        float midRadius = (innerRadius + outerRadius) * 0.5f;

        using var particlePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.Cyan.WithAlpha(140)
        };

        for (int i = 0; i < ParticleCount; i++)
        {
            // Gleichmäßig verteilte Partikel, unterschiedliche Geschwindigkeiten
            float speed = 0.3f + i * 0.08f; // leicht unterschiedlich schnell
            float baseAngle = (float)(i * 2.0 * Math.PI / ParticleCount);
            float angle = baseAngle + time * speed * MathF.PI * 2f;

            // Leichtes radialer Wobble für organischen Look
            float radialOffset = MathF.Sin(time * 1.5f + i * 1.2f) * (outerRadius - innerRadius) * 0.3f;
            float r = midRadius + radialOffset;

            float px = cx + MathF.Cos(angle) * r;
            float py = cy + MathF.Sin(angle) * r;

            canvas.DrawCircle(px, py, ParticleRadius, particlePaint);
        }
    }

    private static void DrawRing(SKCanvas canvas, float cx, float cy, float radius,
        float strokeW, float fraction, SKColor color, byte glowAlpha)
    {
        float sweepAngle = Math.Clamp(fraction, 0f, 1f) * 360f;
        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // Pulsierender Glow-Effekt (Alpha variiert mit Herzschlag)
        using var glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = strokeW + 3f,
            Color = color.WithAlpha(glowAlpha),
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
