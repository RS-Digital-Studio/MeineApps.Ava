using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Rendert die Streak-Card im VitalOS Medical-Design.
/// "VITAL STREAK: X TAGE" mit pulsierendem Herz und Mini-EKG.
/// Static Class - wird im 30fps Render-Loop aufgerufen.
/// </summary>
public static class StreakCardRenderer
{
    // Konstanten
    private const float CornerRadius = 12f;
    private const float IconCircleSize = 44f;
    private const float EkgWidth = 60f;
    private const float EkgAmplitude = 8f;
    private const float EkgCycles = 3f;

    /// <summary>
    /// Zeichnet die Streak-Card mit pulsierendem Herz-Icon, Streak-Zähler und Mini-EKG.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds,
        int currentStreak, int bestStreak, bool hasStreak, float time)
    {
        // 1. Card-Hintergrund (Medical-Style)
        MedicalCardRenderer.RenderCardBackground(canvas, bounds,
            accentColor: MedicalColors.CalorieAmber, cornerRadius: CornerRadius);

        float padding = 14f;
        float contentLeft = bounds.Left + padding;
        float contentRight = bounds.Right - padding;
        float centerY = bounds.MidY;

        // 2. Links: Pulsierendes Herz-Icon im Gradient-Kreis
        float iconRadius = IconCircleSize / 2f;
        float iconCenterX = contentLeft + iconRadius;
        float iconCenterY = centerY;
        RenderHeartIcon(canvas, iconCenterX, iconCenterY, iconRadius, hasStreak, time);

        // 3. Best-Streak Badge oben rechts
        float badgeRight = contentRight;
        float badgeTop = bounds.Top + 8f;
        float badgeWidth = RenderBestBadge(canvas, badgeRight, badgeTop, bestStreak);

        // 4. Rechts: Mini-EKG Trace
        float ekgRight = contentRight - badgeWidth - 8f;
        float ekgLeft = ekgRight - EkgWidth;
        RenderMiniEkg(canvas, ekgLeft, ekgRight, centerY, time);

        // 5. Mitte: Text
        float textLeft = iconCenterX + iconRadius + 12f;
        RenderStreakText(canvas, textLeft, centerY, currentStreak, hasStreak);
    }

    // =====================================================================
    // Pulsierendes Herz-Icon
    // =====================================================================

    /// <summary>
    /// Zeichnet ein Herz als SKPath in einem Gradient-Kreis.
    /// Puls: Scale 1.0 → 1.08 synchron zum Herzschlag (72 BPM).
    /// </summary>
    private static void RenderHeartIcon(SKCanvas canvas, float cx, float cy,
        float radius, bool hasStreak, float time)
    {
        // Puls-Faktor (Beat-synchron 72 BPM)
        float beatPhase = (time * MedicalColors.BeatsPerSecond) % 1f;
        float pulse = hasStreak ? 1f + 0.08f * MathF.Pow(MathF.Sin(beatPhase * MathF.PI), 4f) : 1f;

        canvas.Save();
        canvas.Scale(pulse, pulse, cx, cy);

        // Kreis-Hintergrund: Amber → Rot LinearGradient bei 60% Alpha
        using var circleShader = SKShader.CreateLinearGradient(
            new SKPoint(cx - radius, cy - radius),
            new SKPoint(cx + radius, cy + radius),
            new[] { MedicalColors.CalorieAmber.WithAlpha(153), MedicalColors.CriticalRed.WithAlpha(153) },
            null,
            SKShaderTileMode.Clamp);

        using var circlePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = circleShader
        };
        canvas.DrawCircle(cx, cy, radius, circlePaint);

        // Herz als Bezier-Pfad
        float heartSize = radius * 0.45f;
        DrawHeart(canvas, cx, cy + heartSize * 0.15f, heartSize);

        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet ein Herz mit Bezier-Kurven.
    /// </summary>
    private static void DrawHeart(SKCanvas canvas, float cx, float cy, float size)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White
        };

        using var path = new SKPath();
        float s = size;

        // Herz: Von unten Mitte, links hoch, Bogen, rechts hoch, Bogen, zurück
        path.MoveTo(cx, cy + s * 0.7f); // Untere Spitze
        path.CubicTo(
            cx - s * 1.2f, cy + s * 0.1f,    // Linke Wölbung unten
            cx - s * 1.0f, cy - s * 0.9f,     // Linke Wölbung oben
            cx, cy - s * 0.3f);                // Obere Mitte

        path.CubicTo(
            cx + s * 1.0f, cy - s * 0.9f,     // Rechte Wölbung oben
            cx + s * 1.2f, cy + s * 0.1f,      // Rechte Wölbung unten
            cx, cy + s * 0.7f);                 // Zurück zur Spitze
        path.Close();

        canvas.DrawPath(path, paint);
    }

    // =====================================================================
    // Mini-EKG Trace
    // =====================================================================

    /// <summary>
    /// Zeichnet einen animierten Mini-EKG-Trace (3 Zyklen, Amber, niedrige Amplitude).
    /// </summary>
    private static void RenderMiniEkg(SKCanvas canvas, float left, float right,
        float centerY, float time)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = MedicalColors.CalorieAmber.WithAlpha(180)
        };

        using var path = new SKPath();
        float width = right - left;
        float[] wave = MedicalColors.EkgWave;
        int waveLen = wave.Length;

        // Sweep-Offset (animiert von links nach rechts, 2s Zyklus)
        float sweepOffset = (time * 0.5f) % 1f;

        bool first = true;
        int totalPoints = (int)(waveLen * EkgCycles);

        for (int i = 0; i <= totalPoints; i++)
        {
            float t = (float)i / totalPoints;
            float x = left + t * width;

            // Wellenform-Index mit Sweep-Offset
            int waveIndex = (int)((t * EkgCycles + sweepOffset * waveLen) % waveLen);
            if (waveIndex < 0) waveIndex += waveLen;
            float y = centerY - wave[waveIndex % waveLen] * EkgAmplitude;

            if (first)
            {
                path.MoveTo(x, y);
                first = false;
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        canvas.DrawPath(path, paint);
    }

    // =====================================================================
    // Streak-Text
    // =====================================================================

    /// <summary>
    /// Zeichnet "VITAL STREAK" (klein) und die Streak-Zahl + "TAGE" (groß).
    /// </summary>
    private static void RenderStreakText(SKCanvas canvas, float left, float centerY,
        int currentStreak, bool hasStreak)
    {
        // Label: "VITAL STREAK"
        using var labelPaint = new SKPaint
        {
            IsAntialias = true,
            Color = MedicalColors.TextMuted,
            TextSize = 10f,
            TextAlign = SKTextAlign.Left
        };

        var labelMetrics = labelPaint.FontMetrics;
        float labelY = centerY - 6f - labelMetrics.Descent;
        canvas.DrawText("VITAL STREAK", left, labelY, labelPaint);

        // Wert: Streak-Zahl + " TAGE"
        using var valuePaint = new SKPaint
        {
            IsAntialias = true,
            Color = hasStreak ? MedicalColors.TextPrimary : MedicalColors.TextDimmed,
            TextSize = 20f,
            FakeBoldText = true,
            TextAlign = SKTextAlign.Left
        };

        var valueMetrics = valuePaint.FontMetrics;
        float valueY = centerY + 10f - valueMetrics.Ascent / 2f;

        string streakText = hasStreak ? $"{currentStreak} TAGE" : "0 TAGE";
        canvas.DrawText(streakText, left, valueY, valuePaint);
    }

    // =====================================================================
    // Best-Streak Badge
    // =====================================================================

    /// <summary>
    /// Zeichnet den Best-Streak Badge oben rechts.
    /// Gibt die Breite des Badges zurück (für Layout-Berechnung).
    /// </summary>
    private static float RenderBestBadge(SKCanvas canvas, float right, float top, int bestStreak)
    {
        string text = $"Best: {bestStreak}";

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = MedicalColors.TextMuted,
            TextSize = 10f,
            TextAlign = SKTextAlign.Right
        };

        float textWidth = textPaint.MeasureText(text);
        float paddingH = 8f;
        float paddingV = 4f;
        float badgeWidth = textWidth + paddingH * 2f;
        float badgeHeight = 10f + paddingV * 2f;

        var badgeRect = new SKRect(
            right - badgeWidth,
            top,
            right,
            top + badgeHeight);

        // Hintergrund
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.Surface.WithAlpha(200)
        };
        canvas.DrawRoundRect(badgeRect, 6f, 6f, bgPaint);

        // Text
        var metrics = textPaint.FontMetrics;
        float textY = badgeRect.MidY - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(text, right - paddingH, textY, textPaint);

        return badgeWidth;
    }
}
