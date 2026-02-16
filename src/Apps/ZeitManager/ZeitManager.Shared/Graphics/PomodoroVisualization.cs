using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace ZeitManager.Graphics;

/// <summary>
/// SkiaSharp-Pomodoro-Fortschrittsring mit Phase-Farben, Zyklus-Segmenten und Statistik-Balken.
/// Zwei Render-Methoden: Ring (Timer-Ansicht) und WeeklyBars (Statistik-Ansicht).
/// </summary>
public static class PomodoroVisualization
{
    // Phasen-Farben
    private static readonly SKColor WorkColor = new(0xEF, 0x44, 0x44);     // Rot
    private static readonly SKColor ShortBreakColor = new(0x22, 0xC5, 0x5E); // Grün
    private static readonly SKColor LongBreakColor = new(0x3B, 0x82, 0xF6);  // Blau

    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _segmentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _barStroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private static readonly SKFont _timeFont = new() { Size = 44f };
    private static readonly SKFont _phaseFont = new() { Size = 13f };
    private static readonly SKFont _labelFont = new() { Size = 11f };
    private static readonly SKFont _valueFont = new() { Size = 12f };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);

    /// <summary>
    /// Bestimmt die Farbe für eine Pomodoro-Phase.
    /// </summary>
    private static SKColor PhaseToColor(int phase)
    {
        return phase switch
        {
            0 => WorkColor,       // Work
            1 => ShortBreakColor, // ShortBreak
            2 => LongBreakColor,  // LongBreak
            _ => WorkColor
        };
    }

    /// <summary>
    /// Rendert den Pomodoro-Fortschrittsring mit Zyklus-Segmenten.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="progressFraction">Fortschritt der aktuellen Phase (0.0-1.0)</param>
    /// <param name="phase">Aktuelle Phase (0=Work, 1=ShortBreak, 2=LongBreak)</param>
    /// <param name="currentCycle">Aktueller Zyklus (1-basiert)</param>
    /// <param name="totalCycles">Gesamtanzahl Zyklen bis zur langen Pause</param>
    /// <param name="isRunning">Ob der Timer läuft</param>
    /// <param name="remainingFormatted">Formatierte Restzeit (z.B. "25:00")</param>
    /// <param name="phaseText">Lokalisierter Phasen-Text</param>
    /// <param name="animTime">Animations-Timer für Pulsation</param>
    public static void RenderRing(SKCanvas canvas, SKRect bounds,
        float progressFraction, int phase, int currentCycle, int totalCycles,
        bool isRunning, string remainingFormatted, string phaseText, float animTime)
    {
        float size = Math.Min(bounds.Width, bounds.Height);
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float strokeW = 8f;
        float radius = (size - strokeW * 2 - 14f) / 2f;

        if (radius <= 10) return;

        var phaseColor = PhaseToColor(phase);
        var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // 1. Track-Ring (Hintergrund)
        _trackPaint.StrokeWidth = strokeW;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40);
        canvas.DrawOval(arcRect, _trackPaint);

        // 2. Zyklus-Segmente am äußeren Rand
        DrawCycleSegments(canvas, cx, cy, radius, strokeW, currentCycle, totalCycles, phase);

        // 3. Fortschrittsring
        float progress = Math.Clamp(progressFraction, 0f, 1f);
        if (progress > 0.001f)
        {
            float sweepAngle = progress * 360f;

            // Glow wenn laufend
            if (isRunning)
            {
                float pulse = 0.6f + 0.4f * MathF.Sin(animTime * 2.5f);
                _glowPaint.StrokeWidth = strokeW + 6f;
                _glowPaint.Color = phaseColor.WithAlpha((byte)(70 * pulse));
                _glowPaint.MaskFilter = _glowFilter;

                using var glowPath = new SKPath();
                glowPath.AddArc(arcRect, -90f, sweepAngle);
                canvas.DrawPath(glowPath, _glowPaint);
                _glowPaint.MaskFilter = null;
            }

            // Gradient-Arc
            var endColor = SkiaThemeHelper.AdjustBrightness(phaseColor, 1.4f);
            _arcPaint.StrokeWidth = strokeW;
            _arcPaint.Shader = SKShader.CreateSweepGradient(
                new SKPoint(cx, cy),
                new[] { phaseColor, endColor },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp, -90f, -90f + sweepAngle);
            _arcPaint.Color = SKColors.White;

            using var arcPath = new SKPath();
            arcPath.AddArc(arcRect, -90f, sweepAngle);
            canvas.DrawPath(arcPath, _arcPaint);
            _arcPaint.Shader = null;

            // Leuchtender Endpunkt
            float endAngleRad = (-90f + sweepAngle) * MathF.PI / 180f;
            float endX = cx + MathF.Cos(endAngleRad) * radius;
            float endY = cy + MathF.Sin(endAngleRad) * radius;

            _segmentPaint.Color = endColor;
            canvas.DrawCircle(endX, endY, strokeW * 0.55f, _segmentPaint);
        }

        // 4. Zentrale Zeitanzeige
        _textPaint.Color = SkiaThemeHelper.TextPrimary;
        _timeFont.Size = Math.Max(28f, radius * 0.38f);

        using var timeBlob = SKTextBlob.Create(remainingFormatted, _timeFont);
        if (timeBlob != null)
        {
            canvas.DrawText(remainingFormatted, cx, cy + _timeFont.Size * 0.15f,
                SKTextAlign.Center, _timeFont, _textPaint);
        }

        // 5. Phasen-Label unter der Zeit
        _textPaint.Color = phaseColor;
        _phaseFont.Size = Math.Max(11f, radius * 0.12f);
        canvas.DrawText(phaseText, cx, cy + _timeFont.Size * 0.55f + _phaseFont.Size,
            SKTextAlign.Center, _phaseFont, _textPaint);
    }

    /// <summary>
    /// Zeichnet die Zyklus-Segmente als kleine Bögen am äußeren Rand.
    /// Abgeschlossene Zyklen = voll gefärbt, aktueller = halbtransparent, zukünftige = dunkel.
    /// </summary>
    private static void DrawCycleSegments(SKCanvas canvas, float cx, float cy,
        float radius, float strokeW, int currentCycle, int totalCycles, int phase)
    {
        if (totalCycles <= 0) return;

        float segmentRadius = radius + strokeW / 2f + 6f;
        float segmentW = 3f;
        float totalAngle = 50f; // 50° für alle Zyklus-Punkte
        float segAngle = totalAngle / totalCycles;
        float gapAngle = 2f;
        float startAngle = 270f - totalAngle / 2f; // Zentriert oben

        for (int i = 0; i < totalCycles; i++)
        {
            float angle = startAngle + i * segAngle;
            float sweep = segAngle - gapAngle;

            SKColor color;
            if (i < currentCycle - 1)
                color = WorkColor.WithAlpha(200); // Abgeschlossen
            else if (i == currentCycle - 1)
                color = PhaseToColor(phase).WithAlpha(140); // Aktuell
            else
                color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40); // Zukünftig

            _trackPaint.StrokeWidth = segmentW;
            _trackPaint.Color = color;

            var segRect = new SKRect(cx - segmentRadius, cy - segmentRadius,
                cx + segmentRadius, cy + segmentRadius);

            using var segPath = new SKPath();
            segPath.AddArc(segRect, angle - 180f, sweep);
            canvas.DrawPath(segPath, _trackPaint);
        }
    }

    /// <summary>
    /// Rendert das Wochen-Balkendiagramm für die Statistik-Ansicht.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="dayNames">7 Tagesnamen (Mo-So)</param>
    /// <param name="sessions">7 Session-Counts</param>
    /// <param name="todayIndex">Index des heutigen Tages (0-6)</param>
    public static void RenderWeeklyBars(SKCanvas canvas, SKRect bounds,
        string[] dayNames, int[] sessions, int todayIndex)
    {
        if (dayNames.Length != 7 || sessions.Length != 7) return;

        float padding = 16f;
        float labelH = 20f; // Platz für Tagesname
        float valueH = 18f; // Platz für Zahl oben
        float chartLeft = bounds.Left + padding;
        float chartRight = bounds.Right - padding;
        float chartTop = bounds.Top + padding + valueH;
        float chartBottom = bounds.Bottom - padding - labelH;
        float chartW = chartRight - chartLeft;
        float chartH = chartBottom - chartTop;

        if (chartH <= 10 || chartW <= 10) return;

        int maxSessions = 0;
        foreach (var s in sessions)
            if (s > maxSessions) maxSessions = s;
        if (maxSessions == 0) maxSessions = 1;

        float barW = chartW / 7f;
        float barMaxW = Math.Min(barW - 8f, 36f);

        for (int i = 0; i < 7; i++)
        {
            float barCx = chartLeft + barW * i + barW / 2f;
            float fraction = sessions[i] / (float)maxSessions;
            float barH = Math.Max(fraction * chartH, sessions[i] > 0 ? 4f : 0f);

            // Balken (Gradient von unten nach oben)
            if (barH > 0)
            {
                float barLeft = barCx - barMaxW / 2f;
                float barTop = chartBottom - barH;
                var barRect = new SKRect(barLeft, barTop, barLeft + barMaxW, chartBottom);

                // Gradient: Phase-Rot nach heller
                _barPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(barCx, barTop),
                    new SKPoint(barCx, chartBottom),
                    new[] { SkiaThemeHelper.AdjustBrightness(WorkColor, 1.2f), WorkColor },
                    null, SKShaderTileMode.Clamp);

                float cornerR = Math.Min(6f, barMaxW / 2f);
                canvas.DrawRoundRect(barRect, cornerR, cornerR, _barPaint);
                _barPaint.Shader = null;

                // Heutiger Tag: Akzent-Rahmen
                if (i == todayIndex)
                {
                    _barStroke.Color = SkiaThemeHelper.AdjustBrightness(WorkColor, 1.5f);
                    canvas.DrawRoundRect(barRect, cornerR, cornerR, _barStroke);
                }
            }

            // Session-Zahl über dem Balken
            if (sessions[i] > 0)
            {
                _textPaint.Color = WorkColor;
                _valueFont.Size = 12f;
                float valueY = chartBottom - barH - 4f;
                canvas.DrawText(sessions[i].ToString(), barCx, valueY,
                    SKTextAlign.Center, _valueFont, _textPaint);
            }

            // Tagesname
            _textPaint.Color = i == todayIndex
                ? SkiaThemeHelper.TextPrimary
                : SkiaThemeHelper.TextMuted;
            _labelFont.Size = 11f;
            canvas.DrawText(dayNames[i], barCx, chartBottom + labelH,
                SKTextAlign.Center, _labelFont, _textPaint);
        }

        // Horizontale Grundlinie
        _trackPaint.StrokeWidth = 1f;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 50);
        canvas.DrawLine(chartLeft, chartBottom, chartRight, chartBottom, _trackPaint);
    }
}
