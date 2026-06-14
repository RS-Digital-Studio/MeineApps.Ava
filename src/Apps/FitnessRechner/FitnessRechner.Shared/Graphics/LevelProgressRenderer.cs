using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Rendert die XP/Level-Bar im VitalOS Medical-Design.
/// Holografischer Level-Badge, animierte Progress-Bar mit Scan-Line + Glow, XP-Text.
/// Instance-basiert mit GC-freiem Render-Loop (gecachte Paints/Fonts, fill-gecachter Shader,
/// gecachte Glow-MaskFilter). Wird im 30fps Dashboard-Loop aufgerufen. Lifecycle: HomeView
/// haelt die Instanz und disposed sie in OnDetachedFromVisualTree.
/// </summary>
public sealed class LevelProgressRenderer : IDisposable
{
    private bool _disposed;

    // Konstanten
    private const float BadgeSize = 28f;
    private const float ProgressBarHeight = 10f;
    private const float ProgressBarCornerRadius = 5f;
    private const float ScanLinePeriod = 3f; // Scan-Line Zyklus in Sekunden
    private const float ScanLineWidth = 20f;

    // =====================================================================
    // Gecachte Paints (0 GC im Render-Loop)
    // =====================================================================

    private readonly SKPaint _badgeFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _badgeBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private readonly SKPaint _badgeSharpBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private readonly SKPaint _badgeTextPaint = new() { IsAntialias = true };
    private readonly SKPaint _barBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _scanPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _xpTextPaint = new() { IsAntialias = true };

    // =====================================================================
    // Gecachte Fonts
    // =====================================================================

    private readonly SKFont _badgeFont = new() { Size = 12f, Embolden = true };
    private readonly SKFont _xpFont = new() { Size = 10f };
    private readonly SKFont _measureFont = new() { Size = 10f };

    // =====================================================================
    // Gecachte Glow-MaskFilter (Per-Frame-Neuzuweisung waere ein nativer Leak/Allok)
    // =====================================================================

    private readonly SKMaskFilter _badgeGlowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f);
    private readonly SKMaskFilter _glowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    // =====================================================================
    // Shader-Cache: Progress-Fuellung haengt von left + fillWidth ab (Farben konstant) →
    // nur bei Aenderung neu erstellen.
    // =====================================================================

    private SKShader? _fillShader;
    private float _lastFillLeft = float.NaN;
    private float _lastFillWidth = float.NaN;

    // Cache fuer XP-Text-Breite: Text wechselt nur bei XP-Aenderung, nicht pro Frame.
    private string? _lastXpText;
    private float _lastXpWidth;

    /// <summary>
    /// Zeichnet die XP/Level-Bar mit holografischem Level-Badge,
    /// animierter Cyan→Teal Progress-Bar und XP-Text.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds,
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
    private void RenderLevelBadge(SKCanvas canvas, float cx, float cy,
        float radius, int level)
    {
        // Surface-Hintergrund
        _badgeFillPaint.Color = MedicalColors.Surface;
        canvas.DrawCircle(cx, cy, radius, _badgeFillPaint);

        // Cyan-Rand mit Glow
        _badgeBorderPaint.Color = MedicalColors.Cyan;
        _badgeBorderPaint.MaskFilter = _badgeGlowMask;
        canvas.DrawCircle(cx, cy, radius, _badgeBorderPaint);
        _badgeBorderPaint.MaskFilter = null;

        // Scharfer Rand nochmal drüber (ohne Blur)
        _badgeSharpBorderPaint.Color = MedicalColors.Cyan;
        canvas.DrawCircle(cx, cy, radius, _badgeSharpBorderPaint);

        // Level-Zahl
        _badgeTextPaint.Color = MedicalColors.Cyan;

        var metrics = _badgeFont.Metrics;
        float textY = cy - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(level.ToString(), cx, textY, SKTextAlign.Center, _badgeFont, _badgeTextPaint);
    }

    // =====================================================================
    // Progress-Bar
    // =====================================================================

    /// <summary>
    /// Zeichnet die animierte XP-Progress-Bar mit Cyan→Teal Gradient,
    /// Scan-Line und Glow am Ende.
    /// </summary>
    private void RenderProgressBar(SKCanvas canvas,
        float left, float top, float width, float progress, float time)
    {
        if (width <= 0) return;

        var bgRect = new SKRect(left, top, left + width, top + ProgressBarHeight);

        // Hintergrund: NavyDark
        _barBgPaint.Color = MedicalColors.BgDark;
        canvas.DrawRoundRect(bgRect, ProgressBarCornerRadius, ProgressBarCornerRadius, _barBgPaint);

        if (progress <= 0f) return;

        // Füllung: Cyan → Teal Gradient
        float fillWidth = width * progress;
        var fillRect = new SKRect(left, top, left + fillWidth, top + ProgressBarHeight);

        // Shader-Geometrie haengt nur von left + fillWidth ab (Farben konstant) → nur bei
        // Aenderung neu erstellen. Im Dauer-Loop ist progress stabil (nur bei Daten-Update gesetzt).
        if (_fillShader == null || left != _lastFillLeft || fillWidth != _lastFillWidth)
        {
            _fillShader?.Dispose();
            _fillShader = SKShader.CreateLinearGradient(
                new SKPoint(fillRect.Left, fillRect.Top),
                new SKPoint(fillRect.Right, fillRect.Top),
                new[] { MedicalColors.Cyan, MedicalColors.Teal },
                null,
                SKShaderTileMode.Clamp);
            _lastFillLeft = left;
            _lastFillWidth = fillWidth;
        }

        _fillPaint.Shader = _fillShader;

        // Clipping auf den Hintergrund-RoundRect für saubere Ecken
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(bgRect, ProgressBarCornerRadius), antialias: true);
        canvas.DrawRect(fillRect, _fillPaint);
        _fillPaint.Shader = null;

        // Scan-Line: Heller Streifen der über die Füllung gleitet (3s Zyklus)
        float scanPhase = (time / ScanLinePeriod) % 1f;
        float scanX = left + scanPhase * fillWidth;

        _scanPaint.Color = SKColors.White.WithAlpha(60);
        canvas.DrawRect(scanX - ScanLineWidth / 2f, top, ScanLineWidth, ProgressBarHeight, _scanPaint);
        canvas.Restore();

        // Glow am Ende der Füllung
        float glowX = left + fillWidth;
        float glowY = top + ProgressBarHeight / 2f;

        _glowPaint.Color = MedicalColors.Cyan.WithAlpha(140);
        _glowPaint.MaskFilter = _glowMask;
        canvas.DrawCircle(glowX, glowY, 5f, _glowPaint);
        _glowPaint.MaskFilter = null;
    }

    // =====================================================================
    // XP-Text (mit Cache, 30fps Render-Loop darf nicht pro Frame messen+allokieren)
    // =====================================================================

    /// <summary>
    /// Misst die Breite des XP-Texts (für Layout-Berechnung). Cached, da Text nur bei XP-Aenderung wechselt.
    /// </summary>
    private float MeasureXpText(string xpText)
    {
        if (xpText == _lastXpText) return _lastXpWidth;
        _lastXpText = xpText;
        _lastXpWidth = _measureFont.MeasureText(xpText);
        return _lastXpWidth;
    }

    /// <summary>
    /// Zeichnet den XP-Text rechts neben der Progress-Bar.
    /// </summary>
    private void RenderXpText(SKCanvas canvas, float right, float centerY, string xpText)
    {
        _xpTextPaint.Color = MedicalColors.TextMuted;

        var metrics = _xpFont.Metrics;
        float textY = centerY - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(xpText, right, textY, SKTextAlign.Right, _xpFont, _xpTextPaint);
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _badgeFillPaint.Dispose();
        _badgeBorderPaint.Dispose();
        _badgeSharpBorderPaint.Dispose();
        _badgeTextPaint.Dispose();
        _barBgPaint.Dispose();
        _fillPaint.Dispose();
        _scanPaint.Dispose();
        _glowPaint.Dispose();
        _xpTextPaint.Dispose();

        _badgeFont.Dispose();
        _xpFont.Dispose();
        _measureFont.Dispose();

        _badgeGlowMask.Dispose();
        _glowMask.Dispose();

        _fillShader?.Dispose();
    }
}
