using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Rendert die XP/Level-Bar im VitalOS Medical-Design.
/// Holografischer Level-Badge, animierte Progress-Bar mit Scan-Line + Glow, XP-Text.
/// Static Class - wird im 30fps Render-Loop aufgerufen.
/// </summary>
public static class LevelProgressRenderer
{
    // Konstanten
    private const float BadgeSize = 28f;
    private const float ProgressBarHeight = 10f;
    private const float ProgressBarCornerRadius = 5f;
    private const float ScanLinePeriod = 3f; // Scan-Line Zyklus in Sekunden
    private const float ScanLineWidth = 20f;

    /// <summary>
    /// Zeichnet die XP/Level-Bar mit holografischem Level-Badge,
    /// animierter Cyan→Teal Progress-Bar und XP-Text.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds,
        int level, float xpProgress, string xpText, float time)
    {
        // Fortschritt auf 0-1 clampen
        xpProgress = Math.Clamp(xpProgress, 0f, 1f);

        float padding = 6f;
        float contentLeft = bounds.Left + padding;
        float contentRight = bounds.Right - padding;
        float centerY = bounds.MidY;

        // 1. Links: Level-Badge (holografischer Kreis)
        float badgeRadius = BadgeSize / 2f;
        float badgeCenterX = contentLeft + badgeRadius;
        float badgeCenterY = centerY;
        RenderLevelBadge(canvas, badgeCenterX, badgeCenterY, badgeRadius, level);

        // 2. Rechts: XP-Text
        float xpTextWidth = MeasureXpText(xpText);
        float xpTextX = contentRight;

        // 3. Mitte: Progress-Bar (zwischen Badge und XP-Text)
        float barLeft = badgeCenterX + badgeRadius + 8f;
        float barRight = xpTextX - xpTextWidth - 8f;
        float barTop = centerY - ProgressBarHeight / 2f;
        RenderProgressBar(canvas, barLeft, barTop, barRight - barLeft, xpProgress, time);

        // XP-Text zeichnen
        RenderXpText(canvas, xpTextX, centerY, xpText);
    }

    // =====================================================================
    // Level-Badge
    // =====================================================================

    /// <summary>
    /// Zeichnet den holografischen Level-Badge (Kreis mit Cyan-Rand + Glow).
    /// </summary>
    private static void RenderLevelBadge(SKCanvas canvas, float cx, float cy,
        float radius, int level)
    {
        // Surface-Hintergrund
        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.Surface
        };
        canvas.DrawCircle(cx, cy, radius, fillPaint);

        // Cyan-Rand mit Glow
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = MedicalColors.Cyan,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f)
        };
        canvas.DrawCircle(cx, cy, radius, borderPaint);

        // Scharfer Rand nochmal drüber (ohne Blur)
        using var sharpBorderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = MedicalColors.Cyan
        };
        canvas.DrawCircle(cx, cy, radius, sharpBorderPaint);

        // Level-Zahl
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = MedicalColors.Cyan,
            TextSize = 12f,
            FakeBoldText = true,
            TextAlign = SKTextAlign.Center
        };

        var metrics = textPaint.FontMetrics;
        float textY = cy - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(level.ToString(), cx, textY, textPaint);
    }

    // =====================================================================
    // Progress-Bar
    // =====================================================================

    /// <summary>
    /// Zeichnet die animierte XP-Progress-Bar mit Cyan→Teal Gradient,
    /// Scan-Line und Glow am Ende.
    /// </summary>
    private static void RenderProgressBar(SKCanvas canvas,
        float left, float top, float width, float progress, float time)
    {
        if (width <= 0) return;

        var bgRect = new SKRect(left, top, left + width, top + ProgressBarHeight);

        // Hintergrund: NavyDark
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.BgDark
        };
        canvas.DrawRoundRect(bgRect, ProgressBarCornerRadius, ProgressBarCornerRadius, bgPaint);

        if (progress <= 0f) return;

        // Füllung: Cyan → Teal Gradient
        float fillWidth = width * progress;
        var fillRect = new SKRect(left, top, left + fillWidth, top + ProgressBarHeight);

        using var fillShader = SKShader.CreateLinearGradient(
            new SKPoint(fillRect.Left, fillRect.Top),
            new SKPoint(fillRect.Right, fillRect.Top),
            new[] { MedicalColors.Cyan, MedicalColors.Teal },
            null,
            SKShaderTileMode.Clamp);

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = fillShader
        };

        // Clipping auf den Hintergrund-RoundRect für saubere Ecken
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(bgRect, ProgressBarCornerRadius), antialias: true);
        canvas.DrawRect(fillRect, fillPaint);

        // Scan-Line: Heller Streifen der über die Füllung gleitet (3s Zyklus)
        float scanPhase = (time / ScanLinePeriod) % 1f;
        float scanX = left + scanPhase * fillWidth;

        using var scanPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White.WithAlpha(60)
        };
        canvas.DrawRect(scanX - ScanLineWidth / 2f, top, ScanLineWidth, ProgressBarHeight, scanPaint);
        canvas.Restore();

        // Glow am Ende der Füllung
        float glowX = left + fillWidth;
        float glowY = top + ProgressBarHeight / 2f;

        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.Cyan.WithAlpha(140),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f)
        };
        canvas.DrawCircle(glowX, glowY, 5f, glowPaint);
    }

    // =====================================================================
    // XP-Text
    // =====================================================================

    /// <summary>
    /// Misst die Breite des XP-Texts (für Layout-Berechnung).
    /// </summary>
    private static float MeasureXpText(string xpText)
    {
        using var paint = new SKPaint
        {
            TextSize = 10f
        };
        return paint.MeasureText(xpText);
    }

    /// <summary>
    /// Zeichnet den XP-Text rechts neben der Progress-Bar.
    /// </summary>
    private static void RenderXpText(SKCanvas canvas, float right, float centerY, string xpText)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = MedicalColors.TextMuted,
            TextSize = 10f,
            TextAlign = SKTextAlign.Right
        };

        var metrics = paint.FontMetrics;
        float textY = centerY - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(xpText, right, textY, paint);
    }
}
