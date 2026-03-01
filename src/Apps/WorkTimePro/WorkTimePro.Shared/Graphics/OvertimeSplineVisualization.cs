using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// SkiaSharp Überstunden-Trend als Smooth-Spline mit Flächenfüllung.
/// Grüne Fläche über 0h, rote Fläche unter 0h, Baseline bei 0.
/// Zeigt tägliche Bilanz als Balken + kumulative Linie.
/// </summary>
public static class OvertimeSplineVisualization
{
    private static readonly SKPaint _fillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private static readonly SKFont _labelFont = new() { Size = 9f };
    private static readonly SKFont _axisFont = new() { Size = 10f };

    /// <summary>
    /// Rendert den Überstunden-Trend.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="dailyBalance">Tägliche Bilanz in Stunden (kann negativ sein)</param>
    /// <param name="cumulativeBalance">Kumulative Bilanz in Stunden</param>
    /// <param name="dateLabels">Datums-Labels (dd.MM)</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        float[] dailyBalance, float[] cumulativeBalance, string[] dateLabels)
    {
        if (dailyBalance.Length == 0 || dailyBalance.Length != cumulativeBalance.Length) return;

        int count = dailyBalance.Length;
        float padding = 12f;
        float leftMargin = 36f; // Platz für Y-Achsen-Labels
        float bottomMargin = 22f;
        float topMargin = 8f;

        float chartLeft = bounds.Left + leftMargin;
        float chartRight = bounds.Right - padding;
        float chartTop = bounds.Top + topMargin;
        float chartBottom = bounds.Bottom - bottomMargin;
        float chartW = chartRight - chartLeft;
        float chartH = chartBottom - chartTop;

        if (chartW <= 20 || chartH <= 20) return;

        // Min/Max berechnen (über beide Datenreihen)
        float minVal = 0f, maxVal = 0f;
        for (int i = 0; i < count; i++)
        {
            minVal = Math.Min(minVal, Math.Min(dailyBalance[i], cumulativeBalance[i]));
            maxVal = Math.Max(maxVal, Math.Max(dailyBalance[i], cumulativeBalance[i]));
        }

        // Symmetrisch um 0 erweitern
        float absMax = Math.Max(Math.Abs(minVal), Math.Abs(maxVal));
        absMax = Math.Max(absMax, 1f) * 1.15f;
        minVal = -absMax;
        maxVal = absMax;

        float range = maxVal - minVal;
        float baselineY = chartTop + (maxVal / range) * chartH;

        // Grid-Linien + Y-Achsen-Labels
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 30);
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _axisFont.Size = 9f;

        // 0-Linie betont
        _gridPaint.StrokeWidth = 1f;
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 80);
        canvas.DrawLine(chartLeft, baselineY, chartRight, baselineY, _gridPaint);
        canvas.DrawText("0h", chartLeft - 4f, baselineY + 3f, SKTextAlign.Right, _axisFont, _textPaint);

        // Weitere Grid-Linien
        _gridPaint.StrokeWidth = 0.5f;
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 25);
        float step = CalculateNiceStep(absMax);
        for (float v = step; v <= absMax; v += step)
        {
            float yPos = chartTop + ((maxVal - v) / range) * chartH;
            float yNeg = chartTop + ((maxVal + v) / range) * chartH;
            canvas.DrawLine(chartLeft, yPos, chartRight, yPos, _gridPaint);
            canvas.DrawLine(chartLeft, yNeg, chartRight, yNeg, _gridPaint);
            canvas.DrawText($"+{v:F0}h", chartLeft - 4f, yPos + 3f, SKTextAlign.Right, _axisFont, _textPaint);
            canvas.DrawText($"-{v:F0}h", chartLeft - 4f, yNeg + 3f, SKTextAlign.Right, _axisFont, _textPaint);
        }

        // X-Schritt
        float xStep = count > 1 ? chartW / (count - 1) : chartW;

        // Tägliche Bilanz als dünne Balken
        float barW = Math.Min(xStep * 0.4f, 8f);
        for (int i = 0; i < count; i++)
        {
            float x = chartLeft + i * xStep;
            float barTop, barBottom;

            if (dailyBalance[i] >= 0)
            {
                barTop = baselineY - (dailyBalance[i] / range) * chartH;
                barBottom = baselineY;
                _barPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Success, 80);
            }
            else
            {
                barTop = baselineY;
                barBottom = baselineY + (Math.Abs(dailyBalance[i]) / range) * chartH;
                _barPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Error, 80);
            }

            if (Math.Abs(dailyBalance[i]) > 0.01f)
            {
                canvas.DrawRoundRect(new SKRect(x - barW / 2f, barTop, x + barW / 2f, barBottom),
                    2f, 2f, _barPaint);
            }
        }

        // Kumulative Linie als Smooth Spline
        if (count >= 2)
        {
            var points = new SKPoint[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = new SKPoint(
                    chartLeft + i * xStep,
                    chartTop + ((maxVal - cumulativeBalance[i]) / range) * chartH);
            }

            // Fläche unter/über Baseline
            using var areaPath = CreateSmoothAreaPath(points, baselineY);
            // Gradient: Grün oben, Rot unten
            using (var shader = SKShader.CreateLinearGradient(
                new SKPoint(0, chartTop),
                new SKPoint(0, chartBottom),
                new[]
                {
                    SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Success, 50),
                    SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Success, 10),
                    SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Error, 10),
                    SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Error, 50)
                },
                new float[] { 0f, 0.45f, 0.55f, 1f },
                SKShaderTileMode.Clamp))
            {
                _fillPaint.Shader = shader;
                canvas.DrawPath(areaPath, _fillPaint);
                _fillPaint.Shader = null;
            }

            // Spline-Linie
            using var linePath = CreateSmoothPath(points);
            _linePaint.StrokeWidth = 2.5f;
            _linePaint.Color = SkiaThemeHelper.Warning;
            canvas.DrawPath(linePath, _linePaint);

            // Endpunkt-Dot
            var lastPt = points[^1];
            _dotPaint.Color = SkiaThemeHelper.Warning;
            canvas.DrawCircle(lastPt, 4f, _dotPaint);
            _dotPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Warning, 60);
            using (var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f))
            {
                _dotPaint.MaskFilter = blur;
                canvas.DrawCircle(lastPt, 6f, _dotPaint);
                _dotPaint.MaskFilter = null;
            }
        }

        // X-Achsen-Labels (jeden n-ten anzeigen)
        if (dateLabels.Length == count)
        {
            int labelInterval = Math.Max(1, count / 8);
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _labelFont.Size = 8f;
            for (int i = 0; i < count; i += labelInterval)
            {
                float x = chartLeft + i * xStep;
                canvas.DrawText(dateLabels[i], x, chartBottom + 14f,
                    SKTextAlign.Center, _labelFont, _textPaint);
            }
        }
    }

    /// <summary>
    /// Erstellt einen glatten SKPath durch die Punkte (Catmull-Rom Spline).
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
    /// Erstellt einen geschlossenen Flächen-Path zwischen Spline und Baseline.
    /// </summary>
    private static SKPath CreateSmoothAreaPath(SKPoint[] points, float baselineY)
    {
        var path = CreateSmoothPath(points);
        // Zurück entlang der Baseline
        path.LineTo(points[^1].X, baselineY);
        path.LineTo(points[0].X, baselineY);
        path.Close();
        return path;
    }

    /// <summary>
    /// Berechnet einen sinnvollen Schritt für Grid-Linien.
    /// </summary>
    private static float CalculateNiceStep(float maxValue)
    {
        if (maxValue <= 2) return 1f;
        if (maxValue <= 5) return 2f;
        if (maxValue <= 10) return 5f;
        if (maxValue <= 20) return 5f;
        return 10f;
    }
}
