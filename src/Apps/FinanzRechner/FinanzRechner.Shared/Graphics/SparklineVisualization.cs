using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// SkiaSharp Mini-Sparkline für Ausgaben-Trends.
/// Kompakter Linien-Chart mit Gradient-Füllung unter der Kurve.
/// </summary>
public static class SparklineVisualization
{
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeWidth = 2f };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _labelFont = new() { Size = 10f };

    /// <summary>
    /// Statische Felder vorinitialisieren (SKPaint, SKFont).
    /// Wird im SplashOverlay-Preloader aufgerufen um Jank beim ersten Render zu vermeiden.
    /// </summary>
    public static void WarmUp()
    {
        // Statische readonly-Felder werden durch diesen Methodenaufruf
        // vom CLR-Klassen-Initializer angelegt
    }

    /// <summary>
    /// Rendert eine Mini-Sparkline mit Gradient-Füllung.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="values">Datenpunkte (z.B. Ausgaben der letzten 7/30 Tage)</param>
    /// <param name="lineColor">Linienfarbe</param>
    /// <param name="showEndDot">Leuchtenden Punkt am Ende anzeigen</param>
    /// <param name="trendLabel">Optional: Label wie "+5%" oder "-12%"</param>
    // Hinweis: Nur vom UI-Thread aufrufen (statische Paints nicht thread-safe)
    public static void Render(SKCanvas canvas, SKRect bounds,
        float[] values, SKColor lineColor, bool showEndDot = true, string? trendLabel = null)
    {
        if (values == null || values.Length < 2) return;

        float padding = 8f;
        float labelSpace = trendLabel != null ? 36f : 0f;
        float chartLeft = bounds.Left + padding;
        float chartRight = bounds.Right - padding - labelSpace;
        float chartTop = bounds.Top + padding;
        float chartBottom = bounds.Bottom - padding;
        float chartW = chartRight - chartLeft;
        float chartH = chartBottom - chartTop;

        if (chartW <= 10 || chartH <= 5) return;

        // Min/Max für Skalierung
        float minVal = values.Min();
        float maxVal = values.Max();
        float range = maxVal - minVal;
        if (range < 0.01f) range = 1f; // Flache Linie verhindern

        // Punkte berechnen
        var points = new SKPoint[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float x = chartLeft + (i / (float)(values.Length - 1)) * chartW;
            float y = chartBottom - ((values[i] - minVal) / range) * chartH;
            points[i] = new SKPoint(x, y);
        }

        // 1. Gradient-Füllung unter der Kurve
        using (var fillPath = new SKPath())
        {
            fillPath.MoveTo(points[0].X, chartBottom);
            foreach (var p in points)
                fillPath.LineTo(p);
            fillPath.LineTo(points[^1].X, chartBottom);
            fillPath.Close();

            _fillPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, chartTop),
                new SKPoint(0, chartBottom),
                new[] { lineColor.WithAlpha(60), lineColor.WithAlpha(5) },
                null, SKShaderTileMode.Clamp);
            canvas.DrawPath(fillPath, _fillPaint);
            _fillPaint.Shader = null;
        }

        // 2. Linie zeichnen (smooth mit Bezier-Kurven)
        _linePaint.Color = lineColor;
        using (var linePath = new SKPath())
        {
            linePath.MoveTo(points[0]);

            for (int i = 1; i < points.Length; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];
                float tension = 0.3f;
                float cpX = (curr.X - prev.X) * tension;

                linePath.CubicTo(
                    prev.X + cpX, prev.Y,
                    curr.X - cpX, curr.Y,
                    curr.X, curr.Y);
            }

            canvas.DrawPath(linePath, _linePaint);
        }

        // 3. Endpunkt-Dot (leuchtend)
        if (showEndDot && points.Length > 0)
        {
            var lastPt = points[^1];
            _dotPaint.Color = lineColor;
            canvas.DrawCircle(lastPt, 4f, _dotPaint);
            _dotPaint.Color = SKColors.White.WithAlpha(200);
            canvas.DrawCircle(lastPt, 1.5f, _dotPaint);
        }

        // 4. Trend-Label (rechts)
        if (trendLabel != null)
        {
            bool isNegativeExpense = trendLabel.StartsWith("-");
            // Bei Ausgaben: weniger = gut (grün), mehr = schlecht (rot)
            _textPaint.Color = isNegativeExpense ? SKColor.Parse("#22C55E") : SKColor.Parse("#EF4444");
            _labelFont.Size = 10f;
            canvas.DrawText(trendLabel, chartRight + 6f, bounds.MidY + 4f,
                SKTextAlign.Left, _labelFont, _textPaint);
        }
    }
}
