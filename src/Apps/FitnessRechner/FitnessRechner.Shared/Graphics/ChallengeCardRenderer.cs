using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Rendert die Daily Challenge Card im VitalOS Medical-Design.
/// "DAILY MISSION" Briefing-Style mit animierter Progress-Bar.
/// Static Class - wird im 30fps Render-Loop aufgerufen.
/// </summary>
public static class ChallengeCardRenderer
{
    // Konstanten
    private const float CornerRadius = 12f;
    private const float BracketArmLength = 10f;
    private const float BracketOffset = 3f;
    private const float ProgressBarHeight = 6f;
    private const float ProgressBarCornerRadius = 3f;
    private const float ScanLinePeriod = 2f; // Scan-Line Zyklus in Sekunden
    private const float IconCircleSize = 30f;

    // Gradient-Farben
    private static readonly SKColor IndigoStart = SKColor.Parse("#6366F1");
    private static readonly SKColor PurpleEnd = SKColor.Parse("#8B5CF6");

    /// <summary>
    /// Zeichnet die Daily Challenge Card mit Gradient-Hintergrund,
    /// HUD-Bracketing, Challenge-Info und animierter Progress-Bar.
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string title, float progress, int xpReward, bool isCompleted, float time)
    {
        // Fortschritt auf 0-1 clampen
        progress = Math.Clamp(progress, 0f, 1f);

        // 1. Hintergrund: Indigo → Lila Gradient
        RenderGradientBackground(canvas, bounds);

        // 2. HUD-Bracketing Ecken (weiß 20%)
        RenderHudBrackets(canvas, bounds);

        // 3. Completed Overlay
        if (isCompleted)
            RenderCompletedOverlay(canvas, bounds);

        float padding = 14f;
        float contentLeft = bounds.Left + padding;
        float contentRight = bounds.Right - padding;
        float centerY = bounds.MidY;

        // 4. Links: Challenge-Icon im holografischen Kreis
        float iconRadius = IconCircleSize / 2f;
        float iconCenterX = contentLeft + iconRadius;
        float iconCenterY = centerY;
        RenderChallengeIcon(canvas, iconCenterX, iconCenterY, iconRadius, isCompleted);

        // 5. Rechts: XP-Badge
        float xpBadgeWidth = RenderXpBadge(canvas, contentRight, bounds.Top + 10f, xpReward);

        // 6. Mitte: Text + Progress-Bar
        float textLeft = iconCenterX + iconRadius + 10f;
        float textRight = contentRight - xpBadgeWidth - 8f;
        RenderChallengeContent(canvas, textLeft, textRight, centerY, bounds.Bottom - padding,
            title, progress, time);
    }

    // =====================================================================
    // Schicht 1: Gradient-Hintergrund
    // =====================================================================

    private static void RenderGradientBackground(SKCanvas canvas, SKRect bounds)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(bounds.Left, bounds.Top),
            new SKPoint(bounds.Right, bounds.Bottom),
            new[] { IndigoStart.WithAlpha(217), PurpleEnd.WithAlpha(217) }, // 85% Opacity
            null,
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = shader
        };

        canvas.DrawRoundRect(bounds, CornerRadius, CornerRadius, paint);
    }

    // =====================================================================
    // Schicht 2: HUD-Bracketing (weiß 20%)
    // =====================================================================

    private static void RenderHudBrackets(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = SKColors.White.WithAlpha(51) // 20% Alpha
        };

        float left = bounds.Left + BracketOffset;
        float top = bounds.Top + BracketOffset;
        float right = bounds.Right - BracketOffset;
        float bottom = bounds.Bottom - BracketOffset;

        // Oben-links
        canvas.DrawLine(left, top, left + BracketArmLength, top, paint);
        canvas.DrawLine(left, top, left, top + BracketArmLength, paint);

        // Oben-rechts
        canvas.DrawLine(right, top, right - BracketArmLength, top, paint);
        canvas.DrawLine(right, top, right, top + BracketArmLength, paint);

        // Unten-links
        canvas.DrawLine(left, bottom, left + BracketArmLength, bottom, paint);
        canvas.DrawLine(left, bottom, left, bottom - BracketArmLength, paint);

        // Unten-rechts
        canvas.DrawLine(right, bottom, right - BracketArmLength, bottom, paint);
        canvas.DrawLine(right, bottom, right, bottom - BracketArmLength, paint);
    }

    // =====================================================================
    // Schicht 3: Completed Overlay (grün)
    // =====================================================================

    private static void RenderCompletedOverlay(SKCanvas canvas, SKRect bounds)
    {
        // Grüner Overlay
        using var overlayPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.WaterGreen.WithAlpha(51) // 20% Alpha
        };
        canvas.DrawRoundRect(bounds, CornerRadius, CornerRadius, overlayPaint);
    }

    // =====================================================================
    // Challenge-Icon
    // =====================================================================

    /// <summary>
    /// Zeichnet ein Stern/Target-Icon in einem holografischen Kreis.
    /// Bei Completed: Checkmark statt Stern.
    /// </summary>
    private static void RenderChallengeIcon(SKCanvas canvas, float cx, float cy,
        float radius, bool isCompleted)
    {
        // Kreis: Surface-Hintergrund + Cyan-Rand
        using var circleFillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.Surface.WithAlpha(180)
        };
        canvas.DrawCircle(cx, cy, radius, circleFillPaint);

        using var circleBorderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = MedicalColors.Cyan.WithAlpha(128)
        };
        canvas.DrawCircle(cx, cy, radius, circleBorderPaint);

        // Icon: Checkmark bei Completed, sonst Stern
        if (isCompleted)
            DrawCheckmark(canvas, cx, cy, radius * 0.5f);
        else
            DrawStar(canvas, cx, cy, radius * 0.55f);
    }

    /// <summary>
    /// Zeichnet einen 5-zackigen Stern.
    /// </summary>
    private static void DrawStar(SKCanvas canvas, float cx, float cy, float size)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White
        };

        using var path = new SKPath();
        float outerR = size;
        float innerR = size * 0.4f;

        for (int i = 0; i < 10; i++)
        {
            float angle = MathF.PI / 2f + i * MathF.PI / 5f;
            float r = (i % 2 == 0) ? outerR : innerR;
            float x = cx + MathF.Cos(angle) * r;
            float y = cy - MathF.Sin(angle) * r;

            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();

        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Zeichnet ein Checkmark (Häkchen).
    /// </summary>
    private static void DrawCheckmark(SKCanvas canvas, float cx, float cy, float size)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = MedicalColors.WaterGreen
        };

        using var path = new SKPath();
        // Häkchen: von links-mitte nach unten-mitte, dann nach rechts-oben
        path.MoveTo(cx - size * 0.6f, cy);
        path.LineTo(cx - size * 0.1f, cy + size * 0.5f);
        path.LineTo(cx + size * 0.7f, cy - size * 0.5f);

        canvas.DrawPath(path, paint);
    }

    // =====================================================================
    // Challenge Content (Text + Progress-Bar)
    // =====================================================================

    private static void RenderChallengeContent(SKCanvas canvas,
        float left, float right, float centerY, float bottom,
        string title, float progress, float time)
    {
        // Label: "DAILY MISSION"
        using var labelPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha(179), // 70%
            TextSize = 9f,
            TextAlign = SKTextAlign.Left
        };

        float labelY = centerY - 16f;
        canvas.DrawText("DAILY MISSION", left, labelY, labelPaint);

        // Titel
        using var titlePaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White,
            TextSize = 13f,
            FakeBoldText = true,
            TextAlign = SKTextAlign.Left
        };

        float titleY = labelY + 14f;
        // Titel abschneiden wenn zu lang
        string displayTitle = title;
        float maxTitleWidth = right - left;
        if (titlePaint.MeasureText(displayTitle) > maxTitleWidth)
        {
            while (displayTitle.Length > 3 && titlePaint.MeasureText(displayTitle + "...") > maxTitleWidth)
                displayTitle = displayTitle[..^1];
            displayTitle += "...";
        }
        canvas.DrawText(displayTitle, left, titleY, titlePaint);

        // Progress-Bar
        float barTop = titleY + 8f;
        float barWidth = right - left;
        RenderProgressBar(canvas, left, barTop, barWidth, progress, time);
    }

    /// <summary>
    /// Zeichnet die animierte Progress-Bar mit Scan-Line und Glow.
    /// </summary>
    private static void RenderProgressBar(SKCanvas canvas,
        float left, float top, float width, float progress, float time)
    {
        var bgRect = new SKRect(left, top, left + width, top + ProgressBarHeight);

        // Hintergrund: Navy-dunkel
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.BgDark.WithAlpha(180)
        };
        canvas.DrawRoundRect(bgRect, ProgressBarCornerRadius, ProgressBarCornerRadius, bgPaint);

        if (progress <= 0f) return;

        // Füllung: Cyan-Gradient
        float fillWidth = width * progress;
        var fillRect = new SKRect(left, top, left + fillWidth, top + ProgressBarHeight);

        using var fillShader = SKShader.CreateLinearGradient(
            new SKPoint(fillRect.Left, fillRect.Top),
            new SKPoint(fillRect.Right, fillRect.Top),
            new[] { MedicalColors.Cyan, MedicalColors.CyanBright },
            null,
            SKShaderTileMode.Clamp);

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = fillShader
        };

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(bgRect, ProgressBarCornerRadius), antialias: true);
        canvas.DrawRect(fillRect, fillPaint);

        // Scan-Line: Heller Streifen der über die Füllung gleitet
        float scanPhase = (time / ScanLinePeriod) % 1f;
        float scanX = left + scanPhase * fillWidth;
        float scanWidth = 12f;

        using var scanPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White.WithAlpha(80)
        };
        canvas.DrawRect(scanX - scanWidth / 2f, top, scanWidth, ProgressBarHeight, scanPaint);
        canvas.Restore();

        // Glow am Ende der Füllung
        float glowX = left + fillWidth;
        float glowY = top + ProgressBarHeight / 2f;

        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = MedicalColors.CyanBright.WithAlpha(120),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f)
        };
        canvas.DrawCircle(glowX, glowY, 4f, glowPaint);
    }

    // =====================================================================
    // XP-Badge
    // =====================================================================

    /// <summary>
    /// Zeichnet den XP-Badge oben rechts. Gibt die Breite zurück.
    /// </summary>
    private static float RenderXpBadge(SKCanvas canvas, float right, float top, int xpReward)
    {
        string text = $"+{xpReward} XP";

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White,
            TextSize = 10f,
            FakeBoldText = true,
            TextAlign = SKTextAlign.Center
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

        // Hintergrund: Weiß 20%
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White.WithAlpha(51) // 20%
        };
        canvas.DrawRoundRect(badgeRect, 6f, 6f, bgPaint);

        // Text
        var metrics = textPaint.FontMetrics;
        float textY = badgeRect.MidY - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(text, badgeRect.MidX, textY, textPaint);

        return badgeWidth;
    }
}
