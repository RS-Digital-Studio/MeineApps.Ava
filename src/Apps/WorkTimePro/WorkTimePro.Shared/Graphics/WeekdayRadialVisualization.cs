using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// SkiaSharp radiales Balkendiagramm für Wochentag-Durchschnitte.
/// 7 Balken im Kreis (Mo-So), Soll-Kreis gestrichelt, Zentral Durchschnittswert.
/// </summary>
public static class WeekdayRadialVisualization
{
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _targetPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0) };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _dayFont = new() { Size = 11f };
    private static readonly SKFont _valueFont = new() { Size = 9f };
    private static readonly SKFont _centerFont = new() { Size = 20f };
    private static readonly SKFont _centerSubFont = new() { Size = 10f };

    /// <summary>
    /// Rendert das radiale Wochentag-Diagramm.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="dayLabels">7 Tagesnamen (Mo-So)</param>
    /// <param name="avgHours">7 Durchschnittsstunden pro Tag</param>
    /// <param name="targetHoursPerDay">Tägliches Soll in Stunden</param>
    /// <param name="centerLabel">Beschriftung in der Mitte (z.B. "Schnitt")</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string[] dayLabels, float[] avgHours, float targetHoursPerDay,
        string? centerLabel = null)
    {
        if (dayLabels.Length != 7 || avgHours.Length != 7) return;

        float cx = bounds.MidX;
        float cy = bounds.MidY;
        float maxRadius = Math.Min(bounds.Width, bounds.Height) / 2f - 8f;
        if (maxRadius <= 20) return;

        float innerRadius = maxRadius * 0.35f;
        float outerRadius = maxRadius * 0.85f;
        float labelRadius = maxRadius * 0.95f;
        float barWidth = maxRadius * 0.15f;

        // Maximal-Wert für Skalierung
        float maxHours = targetHoursPerDay;
        for (int i = 0; i < 7; i++)
            maxHours = Math.Max(maxHours, avgHours[i]);
        maxHours = Math.Max(maxHours, 1f) * 1.15f;

        // Hintergrund-Track (Ring)
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 25);
        _trackPaint.StrokeWidth = barWidth;
        _trackPaint.StrokeCap = SKStrokeCap.Round;
        for (int i = 0; i < 7; i++)
        {
            float angleStart = GetBarAngle(i) - 18f;
            float angleSweep = 36f;
            var trackRect = new SKRect(
                cx - (innerRadius + outerRadius) / 2f,
                cy - (innerRadius + outerRadius) / 2f,
                cx + (innerRadius + outerRadius) / 2f,
                cy + (innerRadius + outerRadius) / 2f);

            using var trackPath = new SKPath();
            trackPath.AddArc(trackRect, angleStart, angleSweep);
            canvas.DrawPath(trackPath, _trackPaint);
        }

        // Soll-Kreis (gestrichelt)
        if (targetHoursPerDay > 0)
        {
            float targetRadius = innerRadius + (outerRadius - innerRadius) * (targetHoursPerDay / maxHours);
            _targetPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 80);
            canvas.DrawCircle(cx, cy, targetRadius, _targetPaint);
        }

        // Balken für jeden Tag
        for (int i = 0; i < 7; i++)
        {
            if (avgHours[i] <= 0) continue;

            float fraction = Math.Min(avgHours[i] / maxHours, 1f);
            float barRadius = innerRadius + (outerRadius - innerRadius) * fraction;

            float angleDeg = GetBarAngle(i);
            float halfSweep = 16f; // Halbe Breite in Grad

            // Farbe abhängig vom Soll
            SKColor barColor;
            if (targetHoursPerDay <= 0 || avgHours[i] >= targetHoursPerDay)
                barColor = SkiaThemeHelper.Success;
            else if (avgHours[i] >= targetHoursPerDay * 0.7f)
                barColor = SkiaThemeHelper.Warning;
            else
                barColor = SkiaThemeHelper.Error;

            // Gradient-Balken
            float midRad = angleDeg * MathF.PI / 180f;
            var gradStart = new SKPoint(
                cx + MathF.Cos(midRad) * innerRadius,
                cy + MathF.Sin(midRad) * innerRadius);
            var gradEnd = new SKPoint(
                cx + MathF.Cos(midRad) * barRadius,
                cy + MathF.Sin(midRad) * barRadius);

            _barPaint.Shader?.Dispose();
            _barPaint.Shader = SKShader.CreateLinearGradient(
                gradStart, gradEnd,
                new[] { SkiaThemeHelper.WithAlpha(barColor, 150), barColor },
                null, SKShaderTileMode.Clamp);
            _barPaint.StrokeCap = SKStrokeCap.Round;

            // Arc-Segment zeichnen
            var barRect = new SKRect(
                cx - barRadius, cy - barRadius,
                cx + barRadius, cy + barRadius);

            using var barPath = new SKPath();
            var innerArcRect = new SKRect(
                cx - innerRadius, cy - innerRadius,
                cx + innerRadius, cy + innerRadius);

            barPath.ArcTo(barRect, angleDeg - halfSweep, halfSweep * 2f, true);
            barPath.ArcTo(innerArcRect, angleDeg + halfSweep, -halfSweep * 2f, false);
            barPath.Close();
            canvas.DrawPath(barPath, _barPaint);
            _barPaint.Shader = null;

            // Stundenwert am Ende des Balkens
            float valRadius = barRadius + 8f;
            float valX = cx + MathF.Cos(midRad) * valRadius;
            float valY = cy + MathF.Sin(midRad) * valRadius;
            _textPaint.Color = SkiaThemeHelper.TextSecondary;
            _valueFont.Size = 8f;
            canvas.DrawText($"{avgHours[i]:F1}", valX, valY + 3f,
                SKTextAlign.Center, _valueFont, _textPaint);
        }

        // Tagesnamen
        for (int i = 0; i < 7; i++)
        {
            float angleDeg = GetBarAngle(i);
            float angleRad = angleDeg * MathF.PI / 180f;
            float lx = cx + MathF.Cos(angleRad) * labelRadius;
            float ly = cy + MathF.Sin(angleRad) * labelRadius;

            _textPaint.Color = avgHours[i] > 0 ? SkiaThemeHelper.TextPrimary : SkiaThemeHelper.TextMuted;
            _dayFont.Size = 10f;
            canvas.DrawText(dayLabels[i], lx, ly + 4f,
                SKTextAlign.Center, _dayFont, _textPaint);
        }

        // Zentral: Durchschnitt
        float totalAvg = 0f;
        int activeDays = 0;
        for (int i = 0; i < 7; i++)
        {
            if (avgHours[i] > 0) { totalAvg += avgHours[i]; activeDays++; }
        }
        float avg = activeDays > 0 ? totalAvg / activeDays : 0f;

        // Hintergrund-Kreis in der Mitte
        _glowPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Surface, 200);
        canvas.DrawCircle(cx, cy, innerRadius - 4f, _glowPaint);

        _textPaint.Color = SkiaThemeHelper.TextPrimary;
        _centerFont.Size = 18f;
        canvas.DrawText($"{avg:F1}h", cx, cy + 4f,
            SKTextAlign.Center, _centerFont, _textPaint);

        if (!string.IsNullOrEmpty(centerLabel))
        {
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _centerSubFont.Size = 9f;
            canvas.DrawText(centerLabel, cx, cy + 16f,
                SKTextAlign.Center, _centerSubFont, _textPaint);
        }
    }

    /// <summary>
    /// Berechnet den Winkel für einen Tag (0=Mo, 6=So).
    /// Start oben, im Uhrzeigersinn verteilt.
    /// </summary>
    private static float GetBarAngle(int dayIndex)
    {
        // Start bei -90° (oben), 7 gleichmäßig verteilt
        return -90f + dayIndex * (360f / 7f);
    }
}
