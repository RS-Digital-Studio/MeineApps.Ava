using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace ZeitManager.Graphics;

/// <summary>
/// SkiaSharp-Stoppuhr-Ring mit Minuten-/Sekunden-Ticks, Glow-Effekt und Rundenzeiger.
/// Ersetzt die statischen CSS-Border-Ringe durch einen animierten, visuell reichhaltigen Ring.
/// </summary>
public static class StopwatchVisualization
{
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _tickPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _timeFont = new() { Size = 44f };
    private static readonly SKFont _msFont = new() { Size = 16f };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f);

    /// <summary>
    /// Rendert den Stoppuhr-Ring mit animierten Effekten.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="elapsedSeconds">Vergangene Zeit in Sekunden (inkl. Bruchteile)</param>
    /// <param name="isRunning">Ob die Stoppuhr gerade läuft</param>
    /// <param name="lapCount">Anzahl bisheriger Runden</param>
    /// <param name="animTime">Laufender Animations-Timer für Glow-Pulsation</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        double elapsedSeconds, bool isRunning, int lapCount, float animTime)
    {
        float size = Math.Min(bounds.Width, bounds.Height);
        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float strokeW = 5f;
        float radius = (size - strokeW * 2 - 16f) / 2f;

        if (radius <= 10) return;

        var accent = SkiaThemeHelper.StopwatchAccent;
        var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        // 1. Track-Ring (voller Kreis, dezent)
        _trackPaint.StrokeWidth = strokeW;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 50);
        canvas.DrawOval(arcRect, _trackPaint);

        // 2. Sekunden-Ticks (60 Ticks, 5er = größer)
        DrawTicks(canvas, cx, cy, radius, strokeW);

        // 3. Sekunden-Fortschrittsring (0-60s, dreht sich jede Minute)
        float secondsFraction = (float)(elapsedSeconds % 60.0) / 60f;

        if (secondsFraction > 0.001f)
        {
            float sweepAngle = secondsFraction * 360f;

            // Glow-Layer (nur wenn laufend)
            if (isRunning)
            {
                float pulseAlpha = 0.5f + 0.3f * MathF.Sin(animTime * 3.5f);
                _glowPaint.StrokeWidth = strokeW + 6f;
                _glowPaint.Color = accent.WithAlpha((byte)(80 * pulseAlpha));
                _glowPaint.MaskFilter = _glowFilter;

                using var glowPath = new SKPath();
                glowPath.AddArc(arcRect, -90f, sweepAngle);
                canvas.DrawPath(glowPath, _glowPaint);
                _glowPaint.MaskFilter = null;
            }

            // Gradient-Arc (Cyan → helles Cyan)
            var endColor = SkiaThemeHelper.AdjustBrightness(accent, 1.3f);
            _arcPaint.StrokeWidth = strokeW;
            _arcPaint.Shader = SKShader.CreateSweepGradient(
                new SKPoint(cx, cy),
                new[] { accent, endColor },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp, -90f, -90f + sweepAngle);
            _arcPaint.Color = SKColors.White;

            using var arcPath = new SKPath();
            arcPath.AddArc(arcRect, -90f, sweepAngle);
            canvas.DrawPath(arcPath, _arcPaint);
            _arcPaint.Shader = null;

            // Endpunkt-Dot (leuchtend)
            float endAngleRad = (-90f + sweepAngle) * MathF.PI / 180f;
            float endX = cx + MathF.Cos(endAngleRad) * radius;
            float endY = cy + MathF.Sin(endAngleRad) * radius;

            _dotPaint.Color = accent;
            canvas.DrawCircle(endX, endY, strokeW * 0.8f, _dotPaint);

            // Innerer Glow am Endpunkt
            _dotPaint.Color = SKColors.White.WithAlpha(180);
            canvas.DrawCircle(endX, endY, strokeW * 0.3f, _dotPaint);
        }

        // 4. Minuten-Zeiger (innerer Ring, subtil)
        if (elapsedSeconds > 60)
        {
            float minutesFraction = (float)(elapsedSeconds % 3600.0) / 3600f;
            float innerRadius = radius - 14f;
            var innerRect = new SKRect(cx - innerRadius, cy - innerRadius, cx + innerRadius, cy + innerRadius);

            _trackPaint.StrokeWidth = 2f;
            _trackPaint.Color = SkiaThemeHelper.WithAlpha(accent, 40);
            canvas.DrawOval(innerRect, _trackPaint);

            float minuteSweep = minutesFraction * 360f;
            _arcPaint.StrokeWidth = 2.5f;
            _arcPaint.Shader = null;
            _arcPaint.Color = accent.WithAlpha(120);

            using var minutePath = new SKPath();
            minutePath.AddArc(innerRect, -90f, minuteSweep);
            canvas.DrawPath(minutePath, _arcPaint);
        }

        // 5. Rundenzähler-Punkte (am unteren Rand im Ring)
        if (lapCount > 0)
            DrawLapDots(canvas, cx, cy + radius * 0.45f, lapCount, accent);

        // 6. Zeitanzeige zentral
        DrawTimeText(canvas, cx, cy, elapsedSeconds);
    }

    /// <summary>
    /// Zeichnet 60 Sekunden-Ticks (5er und 15er hervorgehoben).
    /// </summary>
    private static void DrawTicks(SKCanvas canvas, float cx, float cy, float radius, float strokeW)
    {
        float outerR = radius + strokeW / 2f + 1f;

        for (int i = 0; i < 60; i++)
        {
            float angleRad = (i * 6f - 90f) * MathF.PI / 180f;
            bool is15 = i % 15 == 0;
            bool is5 = i % 5 == 0;

            float innerR = is15 ? outerR - 10f : (is5 ? outerR - 7f : outerR - 3.5f);
            _tickPaint.StrokeWidth = is15 ? 2f : (is5 ? 1.2f : 0.5f);
            _tickPaint.Color = is15
                ? SkiaThemeHelper.WithAlpha(SkiaThemeHelper.StopwatchAccent, 200)
                : SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, (byte)(is5 ? 120 : 50));

            canvas.DrawLine(
                cx + MathF.Cos(angleRad) * innerR,
                cy + MathF.Sin(angleRad) * innerR,
                cx + MathF.Cos(angleRad) * outerR,
                cy + MathF.Sin(angleRad) * outerR,
                _tickPaint);
        }
    }

    /// <summary>
    /// Zeichnet Rundenpunkte als kleine Kreise.
    /// </summary>
    private static void DrawLapDots(SKCanvas canvas, float cx, float cy, int count, SKColor color)
    {
        int visibleCount = Math.Min(count, 10); // Max 10 Punkte anzeigen
        float dotR = 3f;
        float spacing = 10f;
        float totalW = visibleCount * dotR * 2f + (visibleCount - 1) * spacing;
        float startX = cx - totalW / 2f + dotR;

        for (int i = 0; i < visibleCount; i++)
        {
            _dotPaint.Color = color.WithAlpha(200);
            canvas.DrawCircle(startX + i * (dotR * 2 + spacing), cy, dotR, _dotPaint);
        }

        // "+N" wenn mehr als 10
        if (count > 10)
        {
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            var font = new SKFont { Size = 10f };
            string extra = $"+{count - 10}";
            canvas.DrawText(extra, startX + visibleCount * (dotR * 2 + spacing) + 4f, cy + 3.5f,
                SKTextAlign.Left, font, _textPaint);
        }
    }

    /// <summary>
    /// Zeichnet die zentrale Zeitanzeige (mm:ss.cc).
    /// </summary>
    private static void DrawTimeText(SKCanvas canvas, float cx, float cy, double elapsedSeconds)
    {
        int totalSeconds = (int)elapsedSeconds;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        int centiseconds = (int)((elapsedSeconds - totalSeconds) * 100);

        string mainTime = $"{minutes:D2}:{seconds:D2}";
        string msTime = $".{centiseconds:D2}";

        // Hauptzeit (groß)
        _textPaint.Color = SkiaThemeHelper.TextPrimary;
        _timeFont.Size = 44f;

        using var mainBlob = SKTextBlob.Create(mainTime, _timeFont);
        if (mainBlob != null)
        {
            float mainW = mainBlob.Bounds.Width;
            float mainX = cx - mainW / 2f - 10f; // Etwas nach links für die Centisekunden
            canvas.DrawText(mainTime, mainX, cy + 16f, SKTextAlign.Left, _timeFont, _textPaint);

            // Centisekunden (kleiner, rechts)
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _msFont.Size = 16f;
            canvas.DrawText(msTime, mainX + mainW + 2f, cy + 16f, SKTextAlign.Left, _msFont, _textPaint);
        }
    }
}
