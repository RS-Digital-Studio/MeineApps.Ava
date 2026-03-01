using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace ZeitManager.Graphics;

/// <summary>
/// SkiaSharp-Stoppuhr-Ring mit Minuten-/Sekunden-Ticks, Glow-Effekt, Rundenzeiger,
/// rotierendem Sekundenzeiger mit Nachleucht-Trail, Runden-Sektoren und Sub-Dial.
/// </summary>
public static class StopwatchVisualization
{
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _tickPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _needlePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _sectorPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _timeFont = new() { Size = 44f };
    private static readonly SKFont _msFont = new() { Size = 16f };
    private static readonly SKFont _subDialFont = new() { Size = 10f };
    private static readonly SKMaskFilter _glowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5f);
    private static readonly SKMaskFilter _needleGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
    private static readonly SKFont _smallFont = new() { Size = 10f };

    // Runden-Sektoren Farben (verschiedene Farben pro Runde)
    private static readonly SKColor[] LapColors =
    {
        new(0x22, 0xD3, 0xEE), // Cyan
        new(0x3B, 0x82, 0xF6), // Blau
        new(0x8B, 0x5C, 0xF6), // Violett
        new(0xA7, 0x8B, 0xFA), // Helles Violett
        new(0x22, 0xC5, 0x5E), // Grün
        new(0xF5, 0x9E, 0x0B), // Amber
        new(0xEF, 0x44, 0x44), // Rot
        new(0xEC, 0x48, 0x99), // Pink
    };

    // Nachleucht-Trail: Positionen des Sekundenzeigers
    private const int TrailLength = 6;

    /// <summary>
    /// Rendert den Stoppuhr-Ring mit animierten Effekten.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="elapsedSeconds">Vergangene Zeit in Sekunden (inkl. Bruchteile)</param>
    /// <param name="isRunning">Ob die Stoppuhr gerade läuft</param>
    /// <param name="lapCount">Anzahl bisheriger Runden</param>
    /// <param name="animTime">Laufender Animations-Timer für Glow-Pulsation</param>
    /// <param name="lapTimesSeconds">Optionale Rundenzeiten in Sekunden (für Runden-Sektoren)</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        double elapsedSeconds, bool isRunning, int lapCount, float animTime,
        double[]? lapTimesSeconds = null)
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

        // 2. Runden-Sektoren (farbige Bögen für jede Runde)
        if (lapTimesSeconds is { Length: > 0 })
            DrawLapSectors(canvas, cx, cy, radius, strokeW, lapTimesSeconds, elapsedSeconds);

        // 3. Sekunden-Ticks (60 Ticks, 5er = größer)
        DrawTicks(canvas, cx, cy, radius, strokeW);

        // 4. Sekunden-Fortschrittsring (0-60s, dreht sich jede Minute)
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
            _arcPaint.Shader?.Dispose();
            _arcPaint.Shader = SKShader.CreateSweepGradient(
                new SKPoint(cx, cy),
                new[] { accent, endColor },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp, -90f, -90f + sweepAngle);
            _arcPaint.Color = SKColors.White;

            using var arcPath = new SKPath();
            arcPath.AddArc(arcRect, -90f, sweepAngle);
            canvas.DrawPath(arcPath, _arcPaint);
            _arcPaint.Shader?.Dispose();
            _arcPaint.Shader = null;
        }

        // 5. Sekundenzeiger mit Nachleucht-Trail
        if (elapsedSeconds > 0.01)
            DrawSecondHand(canvas, cx, cy, radius, secondsFraction, isRunning, accent, animTime);

        // 6. Sub-Dial für Minuten (oben rechts)
        if (elapsedSeconds > 60)
            DrawSubDial(canvas, cx, cy, radius, elapsedSeconds, accent);

        // 7. Rundenzähler-Punkte (am unteren Rand im Ring)
        if (lapCount > 0)
            DrawLapDots(canvas, cx, cy + radius * 0.45f, lapCount, accent);

        // 8. Zeitanzeige zentral
        DrawTimeText(canvas, cx, cy, elapsedSeconds);
    }

    /// <summary>
    /// Zeichnet den rotierenden Sekundenzeiger mit Nachleucht-Trail (Ghost-Positionen).
    /// </summary>
    private static void DrawSecondHand(SKCanvas canvas, float cx, float cy,
        float radius, float secondsFraction, bool isRunning, SKColor accent, float animTime)
    {
        float needleAngleDeg = -90f + secondsFraction * 360f;
        float needleAngleRad = needleAngleDeg * MathF.PI / 180f;

        // Nachleucht-Trail: 6 Ghost-Positionen hinter dem Zeiger
        if (isRunning)
        {
            for (int i = TrailLength; i >= 1; i--)
            {
                // Trail-Position: leicht versetzt in der Vergangenheit
                float trailOffset = i * 0.015f; // ~0.9° pro Ghost
                float trailAngleRad = (needleAngleDeg - i * 5.4f) * MathF.PI / 180f;

                float trailInnerR = radius * 0.2f;
                float trailOuterR = radius - 8f;

                float alpha = (float)(TrailLength - i) / TrailLength;
                alpha *= 0.5f; // Trail dezenter

                _needlePaint.StrokeWidth = Math.Max(0.5f, 2f - i * 0.25f);
                _needlePaint.Color = accent.WithAlpha((byte)(alpha * 180));

                canvas.DrawLine(
                    cx + MathF.Cos(trailAngleRad) * trailInnerR,
                    cy + MathF.Sin(trailAngleRad) * trailInnerR,
                    cx + MathF.Cos(trailAngleRad) * trailOuterR,
                    cy + MathF.Sin(trailAngleRad) * trailOuterR,
                    _needlePaint);
            }
        }

        // Hauptzeiger (leuchtend)
        float innerR = radius * 0.15f;
        float outerR = radius - 6f;

        // Glow am Zeiger
        _needlePaint.StrokeWidth = 4f;
        _needlePaint.Color = accent.WithAlpha(60);
        _needlePaint.MaskFilter = _needleGlowFilter;
        canvas.DrawLine(
            cx + MathF.Cos(needleAngleRad) * innerR,
            cy + MathF.Sin(needleAngleRad) * innerR,
            cx + MathF.Cos(needleAngleRad) * outerR,
            cy + MathF.Sin(needleAngleRad) * outerR,
            _needlePaint);
        _needlePaint.MaskFilter = null;

        // Zeiger selbst
        _needlePaint.StrokeWidth = 2f;
        _needlePaint.Color = accent;
        canvas.DrawLine(
            cx + MathF.Cos(needleAngleRad) * innerR,
            cy + MathF.Sin(needleAngleRad) * innerR,
            cx + MathF.Cos(needleAngleRad) * outerR,
            cy + MathF.Sin(needleAngleRad) * outerR,
            _needlePaint);

        // Leuchtende Spitze
        _dotPaint.Color = accent;
        canvas.DrawCircle(
            cx + MathF.Cos(needleAngleRad) * outerR,
            cy + MathF.Sin(needleAngleRad) * outerR,
            3f, _dotPaint);

        // Mittelpunkt-Achse
        _dotPaint.Color = accent;
        canvas.DrawCircle(cx, cy, 4f, _dotPaint);
        _dotPaint.Color = SkiaThemeHelper.Background;
        canvas.DrawCircle(cx, cy, 2f, _dotPaint);
    }

    /// <summary>
    /// Zeichnet Runden-Sektoren als farbige Bögen auf dem Ring.
    /// Jede Runde bekommt eine eigene Farbe, basierend auf ihrer relativen Dauer.
    /// </summary>
    private static void DrawLapSectors(SKCanvas canvas, float cx, float cy,
        float radius, float strokeW, double[] lapTimesSeconds, double totalElapsed)
    {
        if (lapTimesSeconds.Length == 0) return;

        // Gesamtzeit für die Sektoren-Berechnung
        double totalLapTime = 0;
        foreach (var lt in lapTimesSeconds)
            totalLapTime += lt;

        // Aktuelle Runde (seit letzter Runde) hinzufügen
        double currentLapTime = totalElapsed - totalLapTime;
        if (currentLapTime < 0) currentLapTime = 0;
        double fullTime = totalLapTime + currentLapTime;
        if (fullTime <= 0) return;

        // Innerer Sektor-Ring (zwischen Track und Hauptring)
        float sectorRadius = radius - 8f;
        float sectorW = 3f;
        var sectorRect = new SKRect(cx - sectorRadius, cy - sectorRadius,
            cx + sectorRadius, cy + sectorRadius);

        float currentAngle = -90f;

        // Abgeschlossene Runden
        for (int i = 0; i < lapTimesSeconds.Length; i++)
        {
            float sweep = (float)(lapTimesSeconds[i] / fullTime) * 360f;
            if (sweep < 0.5f) continue;

            var color = LapColors[i % LapColors.Length];

            _trackPaint.StrokeWidth = sectorW;
            _trackPaint.Color = color.WithAlpha(100);

            using var sectorPath = new SKPath();
            sectorPath.AddArc(sectorRect, currentAngle, sweep - 1f); // -1f Gap
            canvas.DrawPath(sectorPath, _trackPaint);

            currentAngle += sweep;
        }

        // Aktuelle Runde (heller, pulsierend)
        if (currentLapTime > 0)
        {
            float sweep = (float)(currentLapTime / fullTime) * 360f;
            if (sweep >= 0.5f)
            {
                var color = LapColors[lapTimesSeconds.Length % LapColors.Length];

                _trackPaint.StrokeWidth = sectorW;
                _trackPaint.Color = color.WithAlpha(160);

                using var currentPath = new SKPath();
                currentPath.AddArc(sectorRect, currentAngle, sweep);
                canvas.DrawPath(currentPath, _trackPaint);
            }
        }
    }

    /// <summary>
    /// Zeichnet den Sub-Dial für Minuten (kleiner Ring oben rechts, 30% Größe des Hauptrings).
    /// </summary>
    private static void DrawSubDial(SKCanvas canvas, float cx, float cy,
        float mainRadius, double elapsedSeconds, SKColor accent)
    {
        float subSize = mainRadius * 0.28f;
        float subCx = cx + mainRadius * 0.55f;
        float subCy = cy - mainRadius * 0.45f;

        // Sub-Dial Hintergrund (dunkler Kreis)
        _sectorPaint.Color = SkiaThemeHelper.Background.WithAlpha(200);
        canvas.DrawCircle(subCx, subCy, subSize + 2f, _sectorPaint);

        // Track-Ring
        var subRect = new SKRect(subCx - subSize, subCy - subSize, subCx + subSize, subCy + subSize);
        _trackPaint.StrokeWidth = 2f;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 60);
        canvas.DrawOval(subRect, _trackPaint);

        // 12 Ticks (wie Stundenziffern einer Uhr)
        for (int i = 0; i < 12; i++)
        {
            float tickAngle = (i * 30f - 90f) * MathF.PI / 180f;
            float innerR = subSize - 4f;
            float outerR = subSize - 1.5f;

            _tickPaint.StrokeWidth = i % 3 == 0 ? 1.5f : 0.5f;
            _tickPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, (byte)(i % 3 == 0 ? 150 : 60));

            canvas.DrawLine(
                subCx + MathF.Cos(tickAngle) * innerR,
                subCy + MathF.Sin(tickAngle) * innerR,
                subCx + MathF.Cos(tickAngle) * outerR,
                subCy + MathF.Sin(tickAngle) * outerR,
                _tickPaint);
        }

        // Minuten-Fortschritt (0-60 Minuten = volle Drehung)
        float minutesFraction = (float)(elapsedSeconds % 3600.0) / 3600f;
        float minuteSweep = minutesFraction * 360f;

        if (minuteSweep > 0.5f)
        {
            _arcPaint.StrokeWidth = 2.5f;
            _arcPaint.Shader = null;
            _arcPaint.Color = accent.WithAlpha(160);

            using var minutePath = new SKPath();
            minutePath.AddArc(subRect, -90f, minuteSweep);
            canvas.DrawPath(minutePath, _arcPaint);

            // Endpunkt
            float endAngle = (-90f + minuteSweep) * MathF.PI / 180f;
            _dotPaint.Color = accent;
            canvas.DrawCircle(
                subCx + MathF.Cos(endAngle) * subSize,
                subCy + MathF.Sin(endAngle) * subSize,
                2.5f, _dotPaint);
        }

        // Minuten-Zahl in der Mitte
        int totalMinutes = (int)(elapsedSeconds / 60);
        _textPaint.Color = SkiaThemeHelper.TextPrimary;
        _subDialFont.Size = subSize * 0.65f;
        canvas.DrawText($"{totalMinutes}", subCx, subCy + _subDialFont.Size * 0.35f,
            SKTextAlign.Center, _subDialFont, _textPaint);

        // "min" Label darunter
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _subDialFont.Size = subSize * 0.3f;
        canvas.DrawText("min", subCx, subCy + subSize * 0.7f,
            SKTextAlign.Center, _subDialFont, _textPaint);
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
            string extra = $"+{count - 10}";
            canvas.DrawText(extra, startX + visibleCount * (dotR * 2 + spacing) + 4f, cy + 3.5f,
                SKTextAlign.Left, _smallFont, _textPaint);
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
