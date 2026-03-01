using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// 4 kompakte Halbkreis-Gauges für die StatisticsView Summary:
/// Arbeitszeit (Ist/Soll), Überstunden, Schnitt/Tag, Arbeitstage-Quote.
/// Jeder Gauge ~60x50dp, 4 nebeneinander in einer Row.
/// </summary>
public static class StatsSummaryGaugeVisualization
{
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _valueFont = new() { Size = 14f };
    private static readonly SKFont _labelFont = new() { Size = 9f };

    /// <summary>
    /// Daten für einen einzelnen Mini-Gauge.
    /// </summary>
    public struct GaugeData
    {
        /// <summary>Aktueller Wert.</summary>
        public float Value;

        /// <summary>Maximalwert (für Fortschritts-Berechnung).</summary>
        public float MaxValue;

        /// <summary>Anzeige-Text (z.B. "32.5h", "+2.3h", "6.5h").</summary>
        public string DisplayText;

        /// <summary>Label (z.B. "Arbeitszeit", "Überstunden").</summary>
        public string Label;

        /// <summary>Farbe des Fortschritts-Arcs.</summary>
        public SKColor Color;

        /// <summary>Wenn true, kann Wert negativ sein (Überstunden).</summary>
        public bool AllowNegative;
    }

    /// <summary>
    /// Rendert eine Reihe von Halbkreis-Gauges.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="gauges">1-4 Gauge-Daten</param>
    public static void Render(SKCanvas canvas, SKRect bounds, GaugeData[] gauges)
    {
        if (gauges.Length == 0) return;

        int count = Math.Min(gauges.Length, 4);
        float gaugeW = bounds.Width / count;
        float gaugeH = bounds.Height;

        for (int i = 0; i < count; i++)
        {
            float gx = bounds.Left + i * gaugeW;
            var gaugeBounds = new SKRect(gx, bounds.Top, gx + gaugeW, bounds.Top + gaugeH);
            DrawSingleGauge(canvas, gaugeBounds, gauges[i]);
        }
    }

    /// <summary>
    /// Zeichnet einen einzelnen Halbkreis-Gauge.
    /// </summary>
    private static void DrawSingleGauge(SKCanvas canvas, SKRect bounds, GaugeData data)
    {
        float cx = bounds.MidX;
        float padding = 6f;
        float maxR = MathF.Min(bounds.Width / 2f - padding, bounds.Height * 0.55f);
        float cy = bounds.Top + maxR + 8f; // Mitte des Halbkreises

        float arcWidth = maxR * 0.18f;
        arcWidth = Math.Clamp(arcWidth, 5f, 10f);

        // Fortschritt berechnen (0-1)
        float fraction;
        if (data.AllowNegative && data.Value < 0)
        {
            // Negative Überstunden: Anzeige als Unterstunden
            fraction = 0;
        }
        else if (data.MaxValue > 0)
        {
            fraction = MathF.Min(MathF.Abs(data.Value) / data.MaxValue, 1.5f);
        }
        else
        {
            fraction = 0;
        }

        // Halbkreis-Track (180°, von links nach rechts)
        float arcRadius = maxR - arcWidth / 2f;
        var arcRect = new SKRect(cx - arcRadius, cy - arcRadius, cx + arcRadius, cy + arcRadius);

        _trackPaint.StrokeWidth = arcWidth;
        _trackPaint.StrokeCap = SKStrokeCap.Round;
        _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40);
        canvas.DrawArc(arcRect, 180f, 180f, false, _trackPaint);

        // Fortschritts-Arc
        if (fraction > 0)
        {
            float sweepAngle = MathF.Min(fraction, 1f) * 180f;

            // Glow
            _glowPaint.StrokeWidth = arcWidth + 4f;
            _glowPaint.StrokeCap = SKStrokeCap.Round;
            _glowPaint.Color = data.Color.WithAlpha(25);
            using (var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f))
            {
                _glowPaint.MaskFilter = blur;
                canvas.DrawArc(arcRect, 180f, sweepAngle, false, _glowPaint);
                _glowPaint.MaskFilter = null;
            }

            // Arc
            _arcPaint.StrokeWidth = arcWidth;
            _arcPaint.StrokeCap = SKStrokeCap.Round;
            _arcPaint.Color = data.Color;
            canvas.DrawArc(arcRect, 180f, sweepAngle, false, _arcPaint);

            // Überfluss markieren (>100%): Zweiter Arc in heller Farbe
            if (fraction > 1f)
            {
                float overflowSweep = (fraction - 1f) * 180f;
                overflowSweep = MathF.Min(overflowSweep, 90f); // Max 50% Überfluss anzeigen
                _arcPaint.Color = SkiaThemeHelper.AdjustBrightness(data.Color, 1.3f);
                canvas.DrawArc(arcRect, 180f, overflowSweep, false, _arcPaint);
            }
        }

        // Wert-Text (im Halbkreis zentriert)
        _textPaint.Color = data.Color;
        _valueFont.Size = Math.Clamp(arcRadius * 0.45f, 11f, 18f);
        canvas.DrawText(data.DisplayText, cx, cy - arcRadius * 0.1f,
            SKTextAlign.Center, _valueFont, _textPaint);

        // Label (unter dem Halbkreis)
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _labelFont.Size = Math.Clamp(arcRadius * 0.28f, 7f, 10f);
        canvas.DrawText(data.Label, cx, cy + _labelFont.Size + 4f,
            SKTextAlign.Center, _labelFont, _textPaint);
    }
}
