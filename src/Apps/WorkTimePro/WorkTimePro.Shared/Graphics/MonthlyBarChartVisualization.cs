using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// SkiaSharp-Balkendiagramm für monatliche Arbeitsstunden + kumulative Saldo-Kurve.
/// Ersetzt LiveCharts CartesianChart in der YearOverviewView.
/// </summary>
public static class MonthlyBarChartVisualization
{
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _targetPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, PathEffect = SKPathEffect.CreateDash(new[] { 5f, 3f }, 0) };
    private static readonly SKFont _labelFont = new() { Size = 10f };
    private static readonly SKFont _valueFont = new() { Size = 8f };
    private static readonly SKFont _axisFont = new() { Size = 9f };

    /// <summary>
    /// Rendert das monatliche Arbeitszeit-Balkendiagramm.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="monthLabels">12 Monatsnamen (kurz)</param>
    /// <param name="workHours">Arbeitsstunden pro Monat</param>
    /// <param name="targetHours">Soll-Stunden pro Monat (einheitlich oder variabel)</param>
    /// <param name="showCumulative">Kumulative Saldo-Kurve anzeigen</param>
    /// <param name="cumulativeBalance">Kumulative Bilanz pro Monat (optional)</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string[] monthLabels, float[] workHours, float[] targetHours,
        bool showCumulative = false, float[]? cumulativeBalance = null)
    {
        int count = monthLabels.Length;
        if (count == 0 || workHours.Length != count || targetHours.Length != count) return;

        float padding = 8f;
        float leftMargin = 32f;
        float bottomMargin = 22f;
        float topMargin = 12f;

        float chartLeft = bounds.Left + leftMargin;
        float chartRight = bounds.Right - padding;
        float chartTop = bounds.Top + topMargin;
        float chartBottom = bounds.Bottom - bottomMargin;
        float chartW = chartRight - chartLeft;
        float chartH = chartBottom - chartTop;

        if (chartW <= 20 || chartH <= 20) return;

        // Max-Wert
        float maxVal = 10f;
        for (int i = 0; i < count; i++)
        {
            maxVal = Math.Max(maxVal, workHours[i]);
            maxVal = Math.Max(maxVal, targetHours[i]);
        }
        maxVal *= 1.15f;

        // Grid-Linien + Y-Achse
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 25);
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _axisFont.Size = 9f;

        float gridStep = maxVal <= 100 ? 20f : maxVal <= 200 ? 40f : 50f;
        for (float v = 0; v <= maxVal; v += gridStep)
        {
            float y = chartBottom - (v / maxVal) * chartH;
            canvas.DrawLine(chartLeft, y, chartRight, y, _gridPaint);
            canvas.DrawText($"{v:F0}", chartLeft - 4f, y + 3f, SKTextAlign.Right, _axisFont, _textPaint);
        }

        float barStep = chartW / count;
        float barW = Math.Min(barStep - 4f, 22f);

        // Balken
        for (int i = 0; i < count; i++)
        {
            float x = chartLeft + barStep * i + barStep / 2f;
            float fraction = workHours[i] / maxVal;
            float barH = fraction * chartH;

            if (barH > 1f)
            {
                float barTop = chartBottom - barH;

                // Farbe: Grün=Soll erreicht, Primary=unter Soll
                SKColor barColor = targetHours[i] > 0 && workHours[i] >= targetHours[i]
                    ? SkiaThemeHelper.Success
                    : SkiaThemeHelper.Primary;

                _barPaint.Shader?.Dispose();
                _barPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(x, barTop),
                    new SKPoint(x, chartBottom),
                    new[] { SkiaThemeHelper.AdjustBrightness(barColor, 1.3f), barColor },
                    null, SKShaderTileMode.Clamp);

                float cornerR = Math.Min(3f, barW / 2f);
                canvas.DrawRoundRect(new SKRect(x - barW / 2f, barTop, x + barW / 2f, chartBottom),
                    cornerR, cornerR, _barPaint);
                _barPaint.Shader?.Dispose();
                _barPaint.Shader = null;

                // Wert über Balken
                if (workHours[i] > 0)
                {
                    _textPaint.Color = SkiaThemeHelper.TextSecondary;
                    _valueFont.Size = 7f;
                    string val = workHours[i] >= 100 ? $"{workHours[i]:F0}" : $"{workHours[i]:F1}";
                    canvas.DrawText(val, x, barTop - 3f, SKTextAlign.Center, _valueFont, _textPaint);
                }
            }

            // Soll-Markierung
            if (targetHours[i] > 0)
            {
                float targetY = chartBottom - (targetHours[i] / maxVal) * chartH;
                _targetPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Warning, 120);
                canvas.DrawLine(x - barW / 2f - 2f, targetY, x + barW / 2f + 2f, targetY, _targetPaint);
            }

            // X-Label
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _labelFont.Size = count <= 6 ? 10f : 8f;
            canvas.DrawText(monthLabels[i], x, chartBottom + 14f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }

        // Kumulative Saldo-Kurve
        if (showCumulative && cumulativeBalance != null && cumulativeBalance.Length == count)
        {
            // Separater Max für kumulative Werte
            float cumMin = 0f, cumMax = 0f;
            for (int i = 0; i < count; i++)
            {
                cumMin = Math.Min(cumMin, cumulativeBalance[i]);
                cumMax = Math.Max(cumMax, cumulativeBalance[i]);
            }
            float cumRange = Math.Max(Math.Abs(cumMin), Math.Abs(cumMax));
            cumRange = Math.Max(cumRange, 1f) * 1.2f;

            // Saldo als Linie auf zweiter Y-Achse (Mitte = 0)
            float midY = (chartTop + chartBottom) / 2f;
            float scaleY = (chartH / 2f) / cumRange;

            var points = new SKPoint[count];
            for (int i = 0; i < count; i++)
            {
                float x = chartLeft + barStep * i + barStep / 2f;
                float y = midY - cumulativeBalance[i] * scaleY;
                points[i] = new SKPoint(x, y);
            }

            // Spline
            using var splinePath = CreateSmoothPath(points);
            _linePaint.StrokeWidth = 2f;
            _linePaint.Color = SkiaThemeHelper.Warning;
            canvas.DrawPath(splinePath, _linePaint);

            // Punkte
            for (int i = 0; i < count; i++)
            {
                _dotPaint.Color = SkiaThemeHelper.Warning;
                canvas.DrawCircle(points[i], 3f, _dotPaint);
            }
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
}
