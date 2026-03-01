using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace ZeitManager.Graphics;

/// <summary>
/// SkiaSharp-Pomodoro-Fortschrittsring mit Phase-Farben, Zyklus-Segmenten, Statistik-Balken,
/// Pulsier-Effekt auf aktivem Segment, Session-Ring für Tages-Fortschritt und Partikel-Support.
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
    private static readonly SKPaint _sessionRingPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKFont _timeFont = new() { Size = 44f };
    private static readonly SKFont _phaseFont = new() { Size = 13f };
    private static readonly SKFont _labelFont = new() { Size = 11f };
    private static readonly SKFont _valueFont = new() { Size = 12f };
    private static readonly SKFont _sessionFont = new() { Size = 9f };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f);
    private static readonly SKMaskFilter _pulseGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8f);

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
    /// Rendert den Pomodoro-Fortschrittsring mit Zyklus-Segmenten, Pulsier-Effekt und Session-Ring.
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
    /// <param name="todaySessions">Anzahl heutiger Sessions (für inneren Session-Ring)</param>
    /// <param name="todayGoal">Tages-Session-Ziel (Standard: 8)</param>
    public static void RenderRing(SKCanvas canvas, SKRect bounds,
        float progressFraction, int phase, int currentCycle, int totalCycles,
        bool isRunning, string remainingFormatted, string phaseText, float animTime,
        int todaySessions = 0, int todayGoal = 8)
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

        // 2. Zyklus-Segmente am äußeren Rand (mit Pulsier-Effekt auf aktivem Segment)
        DrawCycleSegments(canvas, cx, cy, radius, strokeW, currentCycle, totalCycles, phase, isRunning, animTime);

        // 3. Fortschrittsring
        float progress = Math.Clamp(progressFraction, 0f, 1f);
        if (progress > 0.001f)
        {
            float sweepAngle = progress * 360f;

            // Glow wenn laufend (mit verstärktem Puls)
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
            _arcPaint.Shader?.Dispose();
            _arcPaint.Shader = SKShader.CreateSweepGradient(
                new SKPoint(cx, cy),
                new[] { phaseColor, endColor },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp, -90f, -90f + sweepAngle);
            _arcPaint.Color = SKColors.White;

            using var arcPath = new SKPath();
            arcPath.AddArc(arcRect, -90f, sweepAngle);
            canvas.DrawPath(arcPath, _arcPaint);
            _arcPaint.Shader?.Dispose();
            _arcPaint.Shader = null;

            // Leuchtender Endpunkt
            float endAngleRad = (-90f + sweepAngle) * MathF.PI / 180f;
            float endX = cx + MathF.Cos(endAngleRad) * radius;
            float endY = cy + MathF.Sin(endAngleRad) * radius;

            _segmentPaint.Color = endColor;
            canvas.DrawCircle(endX, endY, strokeW * 0.55f, _segmentPaint);
        }

        // 4. Innerer Session-Ring (Tages-Fortschritt)
        if (todaySessions > 0 || todayGoal > 0)
            DrawSessionRing(canvas, cx, cy, radius, todaySessions, todayGoal, animTime);

        // 5. Zentrale Zeitanzeige
        _textPaint.Color = SkiaThemeHelper.TextPrimary;
        _timeFont.Size = Math.Max(28f, radius * 0.38f);

        using var timeBlob = SKTextBlob.Create(remainingFormatted, _timeFont);
        if (timeBlob != null)
        {
            canvas.DrawText(remainingFormatted, cx, cy + _timeFont.Size * 0.15f,
                SKTextAlign.Center, _timeFont, _textPaint);
        }

        // 6. Phasen-Label unter der Zeit
        _textPaint.Color = phaseColor;
        _phaseFont.Size = Math.Max(11f, radius * 0.12f);
        canvas.DrawText(phaseText, cx, cy + _timeFont.Size * 0.55f + _phaseFont.Size,
            SKTextAlign.Center, _phaseFont, _textPaint);
    }

    /// <summary>
    /// Zeichnet die Zyklus-Segmente als kleine Bögen am äußeren Rand.
    /// Aktuelles Segment pulsiert (Scale 1.0-1.05, 2Hz) wenn der Timer läuft.
    /// </summary>
    private static void DrawCycleSegments(SKCanvas canvas, float cx, float cy,
        float radius, float strokeW, int currentCycle, int totalCycles, int phase,
        bool isRunning, float animTime)
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

            bool isCurrentSegment = i == currentCycle - 1;
            bool isCompleted = i < currentCycle - 1;

            // Pulsier-Effekt auf aktivem Segment
            float pulseScale = 1f;
            float pulseStrokeW = segmentW;
            if (isCurrentSegment && isRunning)
            {
                pulseScale = 1f + 0.15f * (0.5f + 0.5f * MathF.Sin(animTime * 4f * MathF.PI)); // 2Hz Puls
                pulseStrokeW = segmentW + 1.5f * (0.5f + 0.5f * MathF.Sin(animTime * 4f * MathF.PI));
            }

            float actualRadius = segmentRadius * pulseScale;

            SKColor color;
            if (isCompleted)
                color = WorkColor.WithAlpha(200); // Abgeschlossen
            else if (isCurrentSegment)
                color = PhaseToColor(phase).WithAlpha(180); // Aktuell (heller als vorher)
            else
                color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40); // Zukünftig

            _trackPaint.StrokeWidth = pulseStrokeW;
            _trackPaint.Color = color;

            var segRect = new SKRect(cx - actualRadius, cy - actualRadius,
                cx + actualRadius, cy + actualRadius);

            using var segPath = new SKPath();
            segPath.AddArc(segRect, angle - 180f, sweep);
            canvas.DrawPath(segPath, _trackPaint);

            // Glow auf aktivem Segment wenn laufend
            if (isCurrentSegment && isRunning)
            {
                _glowPaint.StrokeWidth = pulseStrokeW + 4f;
                _glowPaint.Color = PhaseToColor(phase).WithAlpha((byte)(40 * (0.5f + 0.5f * MathF.Sin(animTime * 4f * MathF.PI))));
                _glowPaint.MaskFilter = _pulseGlowFilter;

                using var glowPath = new SKPath();
                glowPath.AddArc(segRect, angle - 180f, sweep);
                canvas.DrawPath(glowPath, _glowPaint);
                _glowPaint.MaskFilter = null;
            }
        }
    }

    /// <summary>
    /// Zeichnet den inneren Session-Ring für den Tages-Fortschritt.
    /// Zeigt abgeschlossene Work-Sessions als kleine Segmente.
    /// </summary>
    private static void DrawSessionRing(SKCanvas canvas, float cx, float cy,
        float outerRadius, int todaySessions, int todayGoal, float animTime)
    {
        if (todayGoal <= 0) return;

        float sessionRadius = outerRadius - 20f;
        float sessionW = 2.5f;
        var sessionRect = new SKRect(cx - sessionRadius, cy - sessionRadius,
            cx + sessionRadius, cy + sessionRadius);

        // Track für Session-Ring (dezent)
        _sessionRingPaint.StrokeWidth = sessionW;
        _sessionRingPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 25);
        canvas.DrawOval(sessionRect, _sessionRingPaint);

        // Session-Segmente (kleine Bögen im unteren Halbkreis)
        int displayGoal = Math.Max(todayGoal, todaySessions);
        float totalArc = 120f; // 120° Bogen (unten)
        float segArc = totalArc / displayGoal;
        float startAng = 210f; // Beginnt links unten

        for (int i = 0; i < displayGoal; i++)
        {
            float ang = startAng + i * segArc;
            float sw = segArc - 1.5f; // Gap

            bool completed = i < todaySessions;
            bool isLatest = i == todaySessions - 1;

            if (completed)
            {
                var sessionColor = WorkColor.WithAlpha(160);
                if (isLatest)
                {
                    // Neueste Session leuchtet etwas heller
                    sessionColor = WorkColor.WithAlpha(220);
                }

                _sessionRingPaint.StrokeWidth = sessionW + (isLatest ? 1f : 0f);
                _sessionRingPaint.Color = sessionColor;

                using var sesPath = new SKPath();
                sesPath.AddArc(sessionRect, ang, sw);
                canvas.DrawPath(sesPath, _sessionRingPaint);
            }
            else
            {
                // Leere Session-Slots (dezent)
                _sessionRingPaint.StrokeWidth = sessionW;
                _sessionRingPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 20);

                using var emptyPath = new SKPath();
                emptyPath.AddArc(sessionRect, ang, sw);
                canvas.DrawPath(emptyPath, _sessionRingPaint);
            }
        }

        // Session-Zähler Text unten
        if (todaySessions > 0)
        {
            _textPaint.Color = WorkColor.WithAlpha(140);
            _sessionFont.Size = 9f;
            string sessionText = $"{todaySessions}/{todayGoal}";
            canvas.DrawText(sessionText, cx, cy + outerRadius * 0.7f,
                SKTextAlign.Center, _sessionFont, _textPaint);
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
    /// <param name="animFraction">Animation (0.0=leer, 1.0=voll). Fuer Einfahren der Balken.</param>
    public static void RenderWeeklyBars(SKCanvas canvas, SKRect bounds,
        string[] dayNames, int[] sessions, int todayIndex, float animFraction = 1f)
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

        // animFraction begrenzen (0.0 bis 1.0)
        float anim = Math.Clamp(animFraction, 0f, 1f);

        for (int i = 0; i < 7; i++)
        {
            float barCx = chartLeft + barW * i + barW / 2f;
            float fraction = sessions[i] / (float)maxSessions;
            // Balkenhoehe mit Animation multiplizieren (Einfahren von unten)
            float barH = Math.Max(fraction * chartH * anim, sessions[i] > 0 ? 4f * anim : 0f);

            // Balken (Gradient von unten nach oben)
            if (barH > 0)
            {
                float barLeft = barCx - barMaxW / 2f;
                float barTop = chartBottom - barH;
                var barRect = new SKRect(barLeft, barTop, barLeft + barMaxW, chartBottom);

                // Gradient: Phase-Rot nach heller
                _barPaint.Shader?.Dispose();
                _barPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(barCx, barTop),
                    new SKPoint(barCx, chartBottom),
                    new[] { SkiaThemeHelper.AdjustBrightness(WorkColor, 1.2f), WorkColor },
                    null, SKShaderTileMode.Clamp);

                float cornerR = Math.Min(6f, barMaxW / 2f);
                canvas.DrawRoundRect(barRect, cornerR, cornerR, _barPaint);
                _barPaint.Shader?.Dispose();
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
