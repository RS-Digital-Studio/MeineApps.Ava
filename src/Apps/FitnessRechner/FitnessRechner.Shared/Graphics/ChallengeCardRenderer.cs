using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Rendert die Daily Challenge Card im VitalOS Medical-Design.
/// "DAILY MISSION" Briefing-Style mit animierter Progress-Bar.
/// Instance-basiert mit GC-freiem Render-Loop (gecachte Paints/Fonts/Paths, bounds- bzw.
/// fill-gecachte Shader, gecachter Glow-MaskFilter). Wird im 30fps Dashboard-Loop aufgerufen.
/// Lifecycle: HomeView haelt die Instanz und disposed sie in OnDetachedFromVisualTree.
/// </summary>
public sealed class ChallengeCardRenderer : IDisposable
{
    private bool _disposed;

    // Cache: Truncated-Titel (Titel-Wechsel ~1x/Tag, Render-Loop ~30fps).
    // Single-Entry = kein Wachstum.
    private string? _lastTitleInput;
    private string? _lastTitleOutput;
    private float _lastTitleMaxWidth;

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

    // =====================================================================
    // Gecachte Paints (0 GC im Render-Loop)
    // =====================================================================

    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _bracketPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _completedPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _circleFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _circleBorderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private readonly SKPaint _starPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White };
    private readonly SKPaint _checkPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _labelPaint = new() { IsAntialias = true };
    private readonly SKPaint _titlePaint = new() { IsAntialias = true, Color = SKColors.White };
    private readonly SKPaint _barBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _scanPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _xpTextPaint = new() { IsAntialias = true, Color = SKColors.White };
    private readonly SKPaint _xpBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // =====================================================================
    // Gecachte Fonts
    // =====================================================================

    private readonly SKFont _labelFont = new() { Size = 9f };
    private readonly SKFont _titleFont = new() { Size = 13f, Embolden = true };
    private readonly SKFont _xpFont = new() { Size = 10f, Embolden = true };

    // =====================================================================
    // Gecachte Paths
    // =====================================================================

    private readonly SKPath _starPath = new();
    private readonly SKPath _checkPath = new();

    // =====================================================================
    // Gecachter Glow-MaskFilter (Per-Frame-Neuzuweisung waere ein nativer Leak/Allok)
    // =====================================================================

    private readonly SKMaskFilter _glowMask = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);

    // =====================================================================
    // Shader-Cache: Hintergrund haengt nur von bounds ab; Progress-Fuellung haengt
    // von left + fillWidth ab (Farben konstant). Beide werden nur bei Aenderung neu erstellt.
    // =====================================================================

    private SKShader? _bgShader;
    private SKRect _lastBgBounds;

    private SKShader? _fillShader;
    private float _lastFillLeft = float.NaN;
    private float _lastFillWidth = float.NaN;

    /// <summary>
    /// Zeichnet die Daily Challenge Card mit Gradient-Hintergrund,
    /// HUD-Bracketing, Challenge-Info und animierter Progress-Bar.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds,
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

    private void RenderGradientBackground(SKCanvas canvas, SKRect bounds)
    {
        // Geometrie + Farben haengen nur von bounds ab → Shader nur bei Bounds-Aenderung neu erstellen.
        if (_bgShader == null || bounds != _lastBgBounds)
        {
            _bgShader?.Dispose();
            _bgShader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.Left, bounds.Top),
                new SKPoint(bounds.Right, bounds.Bottom),
                new[] { IndigoStart.WithAlpha(217), PurpleEnd.WithAlpha(217) }, // 85% Opacity
                null,
                SKShaderTileMode.Clamp);
            _lastBgBounds = bounds;
        }

        _bgPaint.Shader = _bgShader;
        canvas.DrawRoundRect(bounds, CornerRadius, CornerRadius, _bgPaint);
        _bgPaint.Shader = null;
    }

    // =====================================================================
    // Schicht 2: HUD-Bracketing (weiß 20%)
    // =====================================================================

    private void RenderHudBrackets(SKCanvas canvas, SKRect bounds)
    {
        _bracketPaint.Color = SKColors.White.WithAlpha(51); // 20% Alpha

        float left = bounds.Left + BracketOffset;
        float top = bounds.Top + BracketOffset;
        float right = bounds.Right - BracketOffset;
        float bottom = bounds.Bottom - BracketOffset;

        // Oben-links
        canvas.DrawLine(left, top, left + BracketArmLength, top, _bracketPaint);
        canvas.DrawLine(left, top, left, top + BracketArmLength, _bracketPaint);

        // Oben-rechts
        canvas.DrawLine(right, top, right - BracketArmLength, top, _bracketPaint);
        canvas.DrawLine(right, top, right, top + BracketArmLength, _bracketPaint);

        // Unten-links
        canvas.DrawLine(left, bottom, left + BracketArmLength, bottom, _bracketPaint);
        canvas.DrawLine(left, bottom, left, bottom - BracketArmLength, _bracketPaint);

        // Unten-rechts
        canvas.DrawLine(right, bottom, right - BracketArmLength, bottom, _bracketPaint);
        canvas.DrawLine(right, bottom, right, bottom - BracketArmLength, _bracketPaint);
    }

    // =====================================================================
    // Schicht 3: Completed Overlay (grün)
    // =====================================================================

    private void RenderCompletedOverlay(SKCanvas canvas, SKRect bounds)
    {
        // Grüner Overlay
        _completedPaint.Color = MedicalColors.WaterGreen.WithAlpha(51); // 20% Alpha
        canvas.DrawRoundRect(bounds, CornerRadius, CornerRadius, _completedPaint);
    }

    // =====================================================================
    // Challenge-Icon
    // =====================================================================

    /// <summary>
    /// Zeichnet ein Stern/Target-Icon in einem holografischen Kreis.
    /// Bei Completed: Checkmark statt Stern.
    /// </summary>
    private void RenderChallengeIcon(SKCanvas canvas, float cx, float cy,
        float radius, bool isCompleted)
    {
        // Kreis: Surface-Hintergrund + Cyan-Rand
        _circleFillPaint.Color = MedicalColors.Surface.WithAlpha(180);
        canvas.DrawCircle(cx, cy, radius, _circleFillPaint);

        _circleBorderPaint.Color = MedicalColors.Cyan.WithAlpha(128);
        canvas.DrawCircle(cx, cy, radius, _circleBorderPaint);

        // Icon: Checkmark bei Completed, sonst Stern
        if (isCompleted)
            DrawCheckmark(canvas, cx, cy, radius * 0.5f);
        else
            DrawStar(canvas, cx, cy, radius * 0.55f);
    }

    /// <summary>
    /// Zeichnet einen 5-zackigen Stern.
    /// </summary>
    private void DrawStar(SKCanvas canvas, float cx, float cy, float size)
    {
        _starPath.Reset();
        float outerR = size;
        float innerR = size * 0.4f;

        for (int i = 0; i < 10; i++)
        {
            float angle = MathF.PI / 2f + i * MathF.PI / 5f;
            float r = (i % 2 == 0) ? outerR : innerR;
            float x = cx + MathF.Cos(angle) * r;
            float y = cy - MathF.Sin(angle) * r;

            if (i == 0) _starPath.MoveTo(x, y);
            else _starPath.LineTo(x, y);
        }
        _starPath.Close();

        canvas.DrawPath(_starPath, _starPaint);
    }

    /// <summary>
    /// Zeichnet ein Checkmark (Häkchen).
    /// </summary>
    private void DrawCheckmark(SKCanvas canvas, float cx, float cy, float size)
    {
        _checkPaint.Color = MedicalColors.WaterGreen;

        _checkPath.Reset();
        // Häkchen: von links-mitte nach unten-mitte, dann nach rechts-oben
        _checkPath.MoveTo(cx - size * 0.6f, cy);
        _checkPath.LineTo(cx - size * 0.1f, cy + size * 0.5f);
        _checkPath.LineTo(cx + size * 0.7f, cy - size * 0.5f);

        canvas.DrawPath(_checkPath, _checkPaint);
    }

    // =====================================================================
    // Challenge Content (Text + Progress-Bar)
    // =====================================================================

    private void RenderChallengeContent(SKCanvas canvas,
        float left, float right, float centerY, float bottom,
        string title, float progress, float time)
    {
        // Label: "DAILY MISSION"
        _labelPaint.Color = SKColors.White.WithAlpha(179); // 70%

        float labelY = centerY - 16f;
        canvas.DrawText("DAILY MISSION", left, labelY, SKTextAlign.Left, _labelFont, _labelPaint);

        float titleY = labelY + 14f;
        // Titel abschneiden wenn zu lang — Truncate-Ergebnis cachen, damit die Truncate-Schleife
        // nicht bei 30fps pro Frame laeuft (Challenge-Titel wechselt nur 1x/Tag).
        float maxTitleWidth = right - left;
        string displayTitle;
        if (title == _lastTitleInput && _lastTitleOutput is not null
            && Math.Abs(maxTitleWidth - _lastTitleMaxWidth) < 0.5f)
        {
            displayTitle = _lastTitleOutput;
        }
        else
        {
            displayTitle = title;
            if (_titleFont.MeasureText(displayTitle) > maxTitleWidth)
            {
                while (displayTitle.Length > 3 && _titleFont.MeasureText(displayTitle + "...") > maxTitleWidth)
                    displayTitle = displayTitle[..^1];
                displayTitle += "...";
            }
            _lastTitleInput = title;
            _lastTitleMaxWidth = maxTitleWidth;
            _lastTitleOutput = displayTitle;
        }
        canvas.DrawText(displayTitle, left, titleY, SKTextAlign.Left, _titleFont, _titlePaint);

        // Progress-Bar
        float barTop = titleY + 8f;
        float barWidth = right - left;
        RenderProgressBar(canvas, left, barTop, barWidth, progress, time);
    }

    /// <summary>
    /// Zeichnet die animierte Progress-Bar mit Scan-Line und Glow.
    /// </summary>
    private void RenderProgressBar(SKCanvas canvas,
        float left, float top, float width, float progress, float time)
    {
        var bgRect = new SKRect(left, top, left + width, top + ProgressBarHeight);

        // Hintergrund: Navy-dunkel
        _barBgPaint.Color = MedicalColors.BgDark.WithAlpha(180);
        canvas.DrawRoundRect(bgRect, ProgressBarCornerRadius, ProgressBarCornerRadius, _barBgPaint);

        if (progress <= 0f) return;

        // Füllung: Cyan-Gradient
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
                new[] { MedicalColors.Cyan, MedicalColors.CyanBright },
                null,
                SKShaderTileMode.Clamp);
            _lastFillLeft = left;
            _lastFillWidth = fillWidth;
        }

        _fillPaint.Shader = _fillShader;

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(bgRect, ProgressBarCornerRadius), antialias: true);
        canvas.DrawRect(fillRect, _fillPaint);
        _fillPaint.Shader = null;

        // Scan-Line: Heller Streifen der über die Füllung gleitet
        float scanPhase = (time / ScanLinePeriod) % 1f;
        float scanX = left + scanPhase * fillWidth;
        float scanWidth = 12f;

        _scanPaint.Color = SKColors.White.WithAlpha(80);
        canvas.DrawRect(scanX - scanWidth / 2f, top, scanWidth, ProgressBarHeight, _scanPaint);
        canvas.Restore();

        // Glow am Ende der Füllung
        float glowX = left + fillWidth;
        float glowY = top + ProgressBarHeight / 2f;

        _glowPaint.Color = MedicalColors.CyanBright.WithAlpha(120);
        _glowPaint.MaskFilter = _glowMask;
        canvas.DrawCircle(glowX, glowY, 4f, _glowPaint);
        _glowPaint.MaskFilter = null;
    }

    // =====================================================================
    // XP-Badge
    // =====================================================================

    /// <summary>
    /// Zeichnet den XP-Badge oben rechts. Gibt die Breite zurück.
    /// </summary>
    private float RenderXpBadge(SKCanvas canvas, float right, float top, int xpReward)
    {
        string text = $"+{xpReward} XP";

        float textWidth = _xpFont.MeasureText(text);
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
        _xpBgPaint.Color = SKColors.White.WithAlpha(51); // 20%
        canvas.DrawRoundRect(badgeRect, 6f, 6f, _xpBgPaint);

        // Text
        var metrics = _xpFont.Metrics;
        float textY = badgeRect.MidY - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(text, badgeRect.MidX, textY, SKTextAlign.Center, _xpFont, _xpTextPaint);

        return badgeWidth;
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _bgPaint.Dispose();
        _bracketPaint.Dispose();
        _completedPaint.Dispose();
        _circleFillPaint.Dispose();
        _circleBorderPaint.Dispose();
        _starPaint.Dispose();
        _checkPaint.Dispose();
        _labelPaint.Dispose();
        _titlePaint.Dispose();
        _barBgPaint.Dispose();
        _fillPaint.Dispose();
        _scanPaint.Dispose();
        _glowPaint.Dispose();
        _xpTextPaint.Dispose();
        _xpBgPaint.Dispose();

        _labelFont.Dispose();
        _titleFont.Dispose();
        _xpFont.Dispose();

        _starPath.Dispose();
        _checkPath.Dispose();

        _glowMask.Dispose();

        _bgShader?.Dispose();
        _fillShader?.Dispose();
    }
}
