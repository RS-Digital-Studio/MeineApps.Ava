using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// SkiaSharp-Renderer für 6-Monats-Trend mit 2 Spline-Kurven (Einnahmen + Ausgaben).
/// Ersetzt LiveCharts CartesianChart (LineSeries) in StatisticsView.
/// </summary>
public static class TrendLineVisualization
{
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _labelFont = new() { Size = 10f };
    private static readonly SKFont _axisFont = new() { Size = 9f };

    /// <summary>
    /// Rendert einen 6-Monats-Trend mit Einnahmen- und Ausgaben-Kurven.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="monthLabels">Monatsnamen (z.B. "Jan", "Feb", ...)</param>
    /// <param name="incomeValues">Einnahmen pro Monat</param>
    /// <param name="expenseValues">Ausgaben pro Monat</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string[] monthLabels, float[] incomeValues, float[] expenseValues)
    {
        int count = monthLabels.Length;
        if (count < 2 || incomeValues.Length != count || expenseValues.Length != count) return;

        float padding = 8f;
        float leftMargin = 36f;
        float bottomMargin = 22f;
        float topMargin = 10f;

        float chartLeft = bounds.Left + leftMargin;
        float chartRight = bounds.Right - padding;
        float chartTop = bounds.Top + topMargin;
        float chartBottom = bounds.Bottom - bottomMargin;
        float chartW = chartRight - chartLeft;
        float chartH = chartBottom - chartTop;

        if (chartW <= 20 || chartH <= 20) return;

        // Max-Wert bestimmen
        float maxVal = 100f;
        for (int i = 0; i < count; i++)
        {
            maxVal = Math.Max(maxVal, incomeValues[i]);
            maxVal = Math.Max(maxVal, expenseValues[i]);
        }
        maxVal *= 1.15f;

        // Grid-Linien + Y-Achse
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 20);
        _textPaint.Color = SkiaThemeHelper.TextMuted;

        float gridStep = CalculateGridStep(maxVal);
        for (float v = 0; v <= maxVal; v += gridStep)
        {
            float y = chartBottom - (v / maxVal) * chartH;
            canvas.DrawLine(chartLeft, y, chartRight, y, _gridPaint);
            _axisFont.Size = 8f;
            string label = v >= 1000 ? $"{v / 1000:F0}k" : $"{v:F0}";
            canvas.DrawText(label, chartLeft - 4f, y + 3f, SKTextAlign.Right, _axisFont, _textPaint);
        }

        // X-Labels
        float xStep = chartW / (count - 1);
        for (int i = 0; i < count; i++)
        {
            float x = chartLeft + xStep * i;
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _labelFont.Size = 10f;
            canvas.DrawText(monthLabels[i], x, chartBottom + 14f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }

        // Einnahmen-Kurve (grün)
        var incomeColor = new SKColor(0x22, 0xC5, 0x5E);
        DrawSplineCurve(canvas, chartLeft, chartBottom, xStep, chartH, maxVal,
            incomeValues, count, incomeColor);

        // Ausgaben-Kurve (rot)
        var expenseColor = new SKColor(0xEF, 0x44, 0x44);
        DrawSplineCurve(canvas, chartLeft, chartBottom, xStep, chartH, maxVal,
            expenseValues, count, expenseColor);
    }

    /// <summary>
    /// Zeichnet eine einzelne Spline-Kurve mit Gradient-Füllung und Endpunkt-Dots.
    /// </summary>
    private static void DrawSplineCurve(SKCanvas canvas,
        float chartLeft, float chartBottom, float xStep, float chartH, float maxVal,
        float[] values, int count, SKColor color)
    {
        // Punkte berechnen
        var points = new SKPoint[count];
        for (int i = 0; i < count; i++)
        {
            float x = chartLeft + xStep * i;
            float y = chartBottom - (values[i] / maxVal) * chartH;
            points[i] = new SKPoint(x, y);
        }

        // Spline-Pfad
        using var splinePath = CreateSmoothPath(points);

        // Gradient-Füllung unter der Kurve
        using var fillPath = new SKPath(splinePath);
        fillPath.LineTo(points[count - 1].X, chartBottom);
        fillPath.LineTo(points[0].X, chartBottom);
        fillPath.Close();

        _fillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, points.Min(p => p.Y)),
            new SKPoint(0, chartBottom),
            new[] { SkiaThemeHelper.WithAlpha(color, 60), SkiaThemeHelper.WithAlpha(color, 5) },
            null, SKShaderTileMode.Clamp);
        canvas.DrawPath(fillPath, _fillPaint);
        _fillPaint.Shader = null;

        // Linie
        _linePaint.Color = color;
        _linePaint.StrokeWidth = 2.5f;
        canvas.DrawPath(splinePath, _linePaint);

        // Punkte
        for (int i = 0; i < count; i++)
        {
            // Weißer Hintergrund-Kreis
            _dotPaint.Color = SkiaThemeHelper.Card;
            canvas.DrawCircle(points[i], 4f, _dotPaint);
            // Farbiger Punkt
            _dotPaint.Color = color;
            canvas.DrawCircle(points[i], 3f, _dotPaint);
        }
    }

    /// <summary>
    /// Erstellt einen glatten SKPath (Catmull-Rom Spline).
    /// </summary>
    private static SKPath CreateSmoothPath(SKPoint[] points)
    {
        var path = new SKPath();
        if (points.Length < 2) return path;

        path.MoveTo(points[0]);
        if (points.Length == 2)
        {
            path.LineTo(points[1]);
            return path;
        }

        for (int i = 0; i < points.Length - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i < points.Length - 2 ? points[i + 2] : points[i + 1];

            float cp1x = p1.X + (p2.X - p0.X) / 6f;
            float cp1y = p1.Y + (p2.Y - p0.Y) / 6f;
            float cp2x = p2.X - (p3.X - p1.X) / 6f;
            float cp2y = p2.Y - (p3.Y - p1.Y) / 6f;

            path.CubicTo(cp1x, cp1y, cp2x, cp2y, p2.X, p2.Y);
        }

        return path;
    }

    /// <summary>
    /// Berechnet den optimalen Grid-Schritt für die Y-Achse.
    /// </summary>
    private static float CalculateGridStep(float maxVal)
    {
        if (maxVal <= 500) return 100f;
        if (maxVal <= 1000) return 200f;
        if (maxVal <= 5000) return 1000f;
        if (maxVal <= 10000) return 2000f;
        return 5000f;
    }
}
