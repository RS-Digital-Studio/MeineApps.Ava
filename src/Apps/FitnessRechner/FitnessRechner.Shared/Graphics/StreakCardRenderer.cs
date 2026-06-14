using System;
using SkiaSharp;

namespace FitnessRechner.Graphics;

/// <summary>
/// Rendert die Streak-Card im VitalOS Medical-Design.
/// "VITAL STREAK: X TAGE" mit pulsierendem Herz und Mini-EKG.
/// Instance-basiert mit GC-freiem Render-Loop (gecachte Paints/Fonts/Paths, bounds-gecachter
/// Shader). Wird im 30fps Dashboard-Loop aufgerufen. Lifecycle: HomeView haelt die Instanz
/// und disposed sie in OnDetachedFromVisualTree.
/// </summary>
public sealed class StreakCardRenderer : IDisposable
{
    private bool _disposed;

    // Cache: Best-Streak-Text+Width aendert sich nur bei Meilensteinen (nicht pro Frame bei 30fps).
    // Single-Entry (kein Dict) → kein unbegrenztes Wachstum.
    private string? _lastBestText;
    private float _lastBestWidth;

    // Konstanten
    private const float CornerRadius = 12f;
    private const float IconCircleSize = 44f;
    private const float EkgWidth = 60f;
    private const float EkgAmplitude = 8f;
    private const float EkgCycles = 3f;

    // =====================================================================
    // Gecachte Paints (0 GC im Render-Loop)
    // =====================================================================

    private readonly SKPaint _circlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _heartPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White };
    private readonly SKPaint _ekgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
    private readonly SKPaint _labelPaint = new() { IsAntialias = true };
    private readonly SKPaint _valuePaint = new() { IsAntialias = true };
    private readonly SKPaint _badgeTextPaint = new() { IsAntialias = true };
    private readonly SKPaint _badgeBgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    // =====================================================================
    // Gecachte Fonts
    // =====================================================================

    private readonly SKFont _labelFont = new() { Size = 10f };
    private readonly SKFont _valueFont = new() { Size = 20f, Embolden = true };
    private readonly SKFont _badgeFont = new() { Size = 10f };

    // =====================================================================
    // Gecachte Paths
    // =====================================================================

    private readonly SKPath _heartPath = new();
    private readonly SKPath _ekgPath = new();

    // =====================================================================
    // Shader-Cache mit Bounds-Check (Herz-Kreis-Gradient haengt nur von bounds ab)
    // =====================================================================

    private SKShader? _circleShader;
    private SKRect _lastBounds;

    /// <summary>
    /// Zeichnet die Streak-Card mit pulsierendem Herz-Icon, Streak-Zähler und Mini-EKG.
    /// </summary>
    public void Render(SKCanvas canvas, SKRect bounds,
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
        RenderHeartIcon(canvas, bounds, iconCenterX, iconCenterY, iconRadius, hasStreak, time);

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
    private void RenderHeartIcon(SKCanvas canvas, SKRect bounds, float cx, float cy,
        float radius, bool hasStreak, float time)
    {
        // Puls-Faktor (Beat-synchron 72 BPM)
        float beatPhase = (time * MedicalColors.BeatsPerSecond) % 1f;
        float pulse = hasStreak ? 1f + 0.08f * MathF.Pow(MathF.Sin(beatPhase * MathF.PI), 4f) : 1f;

        canvas.Save();
        canvas.Scale(pulse, pulse, cx, cy);

        // Kreis-Hintergrund: Amber → Rot LinearGradient bei 60% Alpha.
        // Geometrie haengt nur von cx/cy/radius (= bounds) ab → Shader nur bei Bounds-Aenderung neu erstellen.
        if (_circleShader == null || bounds != _lastBounds)
        {
            _circleShader?.Dispose();
            _circleShader = SKShader.CreateLinearGradient(
                new SKPoint(cx - radius, cy - radius),
                new SKPoint(cx + radius, cy + radius),
                new[] { MedicalColors.CalorieAmber.WithAlpha(153), MedicalColors.CriticalRed.WithAlpha(153) },
                null,
                SKShaderTileMode.Clamp);
            _lastBounds = bounds;
        }

        _circlePaint.Shader = _circleShader;
        canvas.DrawCircle(cx, cy, radius, _circlePaint);
        _circlePaint.Shader = null;

        // Herz als Bezier-Pfad
        float heartSize = radius * 0.45f;
        DrawHeart(canvas, cx, cy + heartSize * 0.15f, heartSize);

        canvas.Restore();
    }

    /// <summary>
    /// Zeichnet ein Herz mit Bezier-Kurven.
    /// </summary>
    private void DrawHeart(SKCanvas canvas, float cx, float cy, float size)
    {
        _heartPath.Reset();
        float s = size;

        // Herz: Von unten Mitte, links hoch, Bogen, rechts hoch, Bogen, zurück
        _heartPath.MoveTo(cx, cy + s * 0.7f); // Untere Spitze
        _heartPath.CubicTo(
            cx - s * 1.2f, cy + s * 0.1f,    // Linke Wölbung unten
            cx - s * 1.0f, cy - s * 0.9f,     // Linke Wölbung oben
            cx, cy - s * 0.3f);                // Obere Mitte

        _heartPath.CubicTo(
            cx + s * 1.0f, cy - s * 0.9f,     // Rechte Wölbung oben
            cx + s * 1.2f, cy + s * 0.1f,      // Rechte Wölbung unten
            cx, cy + s * 0.7f);                 // Zurück zur Spitze
        _heartPath.Close();

        canvas.DrawPath(_heartPath, _heartPaint);
    }

    // =====================================================================
    // Mini-EKG Trace
    // =====================================================================

    /// <summary>
    /// Zeichnet einen animierten Mini-EKG-Trace (3 Zyklen, Amber, niedrige Amplitude).
    /// </summary>
    private void RenderMiniEkg(SKCanvas canvas, float left, float right,
        float centerY, float time)
    {
        _ekgPaint.Color = MedicalColors.CalorieAmber.WithAlpha(180);

        _ekgPath.Reset();
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
                _ekgPath.MoveTo(x, y);
                first = false;
            }
            else
            {
                _ekgPath.LineTo(x, y);
            }
        }

        canvas.DrawPath(_ekgPath, _ekgPaint);
    }

    // =====================================================================
    // Streak-Text
    // =====================================================================

    /// <summary>
    /// Zeichnet "VITAL STREAK" (klein) und die Streak-Zahl + "TAGE" (groß).
    /// </summary>
    private void RenderStreakText(SKCanvas canvas, float left, float centerY,
        int currentStreak, bool hasStreak)
    {
        // Label: "VITAL STREAK"
        _labelPaint.Color = MedicalColors.TextMuted;

        var labelMetrics = _labelFont.Metrics;
        float labelY = centerY - 6f - labelMetrics.Descent;
        canvas.DrawText("VITAL STREAK", left, labelY, SKTextAlign.Left, _labelFont, _labelPaint);

        // Wert: Streak-Zahl + " TAGE"
        _valuePaint.Color = hasStreak ? MedicalColors.TextPrimary : MedicalColors.TextDimmed;

        var valueMetrics = _valueFont.Metrics;
        float valueY = centerY + 10f - valueMetrics.Ascent / 2f;

        string streakText = hasStreak ? $"{currentStreak} TAGE" : "0 TAGE";
        canvas.DrawText(streakText, left, valueY, SKTextAlign.Left, _valueFont, _valuePaint);
    }

    // =====================================================================
    // Best-Streak Badge
    // =====================================================================

    /// <summary>
    /// Zeichnet den Best-Streak Badge oben rechts.
    /// Gibt die Breite des Badges zurück (für Layout-Berechnung).
    /// </summary>
    private float RenderBestBadge(SKCanvas canvas, float right, float top, int bestStreak)
    {
        string text = $"Best: {bestStreak}";

        _badgeTextPaint.Color = MedicalColors.TextMuted;

        // Cache: Best-Streak-Text aendert sich nur bei Meilensteinen, nicht pro Frame
        if (text != _lastBestText)
        {
            _lastBestText = text;
            _lastBestWidth = _badgeFont.MeasureText(text);
        }
        float textWidth = _lastBestWidth;
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
        _badgeBgPaint.Color = MedicalColors.Surface.WithAlpha(200);
        canvas.DrawRoundRect(badgeRect, 6f, 6f, _badgeBgPaint);

        // Text
        var metrics = _badgeFont.Metrics;
        float textY = badgeRect.MidY - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(text, right - paddingH, textY, SKTextAlign.Right, _badgeFont, _badgeTextPaint);

        return badgeWidth;
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _circlePaint.Dispose();
        _heartPaint.Dispose();
        _ekgPaint.Dispose();
        _labelPaint.Dispose();
        _valuePaint.Dispose();
        _badgeTextPaint.Dispose();
        _badgeBgPaint.Dispose();

        _labelFont.Dispose();
        _valueFont.Dispose();
        _badgeFont.Dispose();

        _heartPath.Dispose();
        _ekgPath.Dispose();

        _circleShader?.Dispose();
    }
}
