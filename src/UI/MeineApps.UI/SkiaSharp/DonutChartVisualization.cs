using SkiaSharp;

namespace MeineApps.UI.SkiaSharp;

/// <summary>
/// Wiederverwendbarer Donut-Chart-Renderer für alle Apps.
/// Segmente mit Farben, InnerRadius konfigurierbar, Labels, optionale Legende.
/// </summary>
public static class DonutChartVisualization
{
    private static readonly SKPaint _segmentPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gapPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKFont _labelFont = new() { Size = 11f };
    private static readonly SKFont _valueFont = new() { Size = 12f };
    private static readonly SKFont _centerFont = new() { Size = 18f };

    /// <summary>
    /// Ein Segment des Donut-Charts.
    /// </summary>
    public struct Segment
    {
        public float Value;
        public SKColor Color;
        public string Label;
        public string ValueText;
    }

    /// <summary>
    /// Rendert einen Donut-Chart.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="segments">Chart-Segmente</param>
    /// <param name="innerRadiusFraction">Innerer Radius als Bruchteil des äußeren (0.0-0.9), Standard 0.55</param>
    /// <param name="centerText">Optionaler Text in der Mitte</param>
    /// <param name="centerSubText">Optionaler Untertext in der Mitte</param>
    /// <param name="showLabels">Labels an den Segmenten anzeigen</param>
    /// <param name="showLegend">Legende unter dem Chart anzeigen</param>
    /// <param name="startAngle">Start-Winkel in Grad (-90 = oben)</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        Segment[] segments, float innerRadiusFraction = 0.55f,
        string? centerText = null, string? centerSubText = null,
        bool showLabels = true, bool showLegend = true,
        float startAngle = -90f)
    {
        if (segments.Length == 0) return;

        float totalValue = 0f;
        for (int i = 0; i < segments.Length; i++)
            totalValue += segments[i].Value;
        if (totalValue <= 0) return;

        // Layout berechnen
        float legendH = showLegend ? Math.Min(segments.Length * 18f + 8f, bounds.Height * 0.3f) : 0f;
        float chartAreaH = bounds.Height - legendH;
        float chartCenterY = bounds.Top + chartAreaH / 2f;
        float chartCenterX = bounds.MidX;

        float maxRadius = Math.Min(chartAreaH, bounds.Width) / 2f - 8f;
        if (maxRadius <= 10) return;

        float outerRadius = maxRadius;
        float innerRadius = outerRadius * Math.Clamp(innerRadiusFraction, 0f, 0.9f);

        // Segmente zeichnen
        float currentAngle = startAngle;
        float gapDeg = segments.Length > 1 ? 1.5f : 0f;

        using var path = new SKPath();

        for (int i = 0; i < segments.Length; i++)
        {
            float sweepAngle = (segments[i].Value / totalValue) * (360f - gapDeg * segments.Length);
            if (sweepAngle < 0.5f) { currentAngle += sweepAngle + gapDeg; continue; }

            // Segment als Arc-Path
            path.Reset();
            var outerRect = new SKRect(
                chartCenterX - outerRadius, chartCenterY - outerRadius,
                chartCenterX + outerRadius, chartCenterY + outerRadius);
            var innerRect = new SKRect(
                chartCenterX - innerRadius, chartCenterY - innerRadius,
                chartCenterX + innerRadius, chartCenterY + innerRadius);

            path.ArcTo(outerRect, currentAngle, sweepAngle, true);
            path.ArcTo(innerRect, currentAngle + sweepAngle, -sweepAngle, false);
            path.Close();

            // Farbe mit leichtem Gradient
            float midAngleRad = (currentAngle + sweepAngle / 2f) * MathF.PI / 180f;
            var gradStart = new SKPoint(
                chartCenterX + MathF.Cos(midAngleRad) * innerRadius,
                chartCenterY + MathF.Sin(midAngleRad) * innerRadius);
            var gradEnd = new SKPoint(
                chartCenterX + MathF.Cos(midAngleRad) * outerRadius,
                chartCenterY + MathF.Sin(midAngleRad) * outerRadius);

            _segmentPaint.Shader = SKShader.CreateLinearGradient(
                gradStart, gradEnd,
                new[] { SkiaThemeHelper.AdjustBrightness(segments[i].Color, 1.2f), segments[i].Color },
                null, SKShaderTileMode.Clamp);
            canvas.DrawPath(path, _segmentPaint);
            _segmentPaint.Shader = null;

            // Label an der Segment-Mitte
            if (showLabels && sweepAngle > 15f && !string.IsNullOrEmpty(segments[i].ValueText))
            {
                float labelRadius = (outerRadius + innerRadius) / 2f;
                float labelX = chartCenterX + MathF.Cos(midAngleRad) * labelRadius;
                float labelY = chartCenterY + MathF.Sin(midAngleRad) * labelRadius;

                _textPaint.Color = SKColors.White;
                _valueFont.Size = sweepAngle > 30f ? 11f : 9f;
                canvas.DrawText(segments[i].ValueText, labelX, labelY + 4f,
                    SKTextAlign.Center, _valueFont, _textPaint);
            }

            currentAngle += sweepAngle + gapDeg;
        }

        // Innerer Kreis-Hintergrund (für sauberen Donut-Look)
        _segmentPaint.Color = SkiaThemeHelper.Card;
        _segmentPaint.Shader = null;
        canvas.DrawCircle(chartCenterX, chartCenterY, innerRadius - 1f, _segmentPaint);

        // Subtiler Glow auf dem inneren Rand
        _glowPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Primary, 30);
        _glowPaint.StrokeWidth = 2f;
        _glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f);
        canvas.DrawCircle(chartCenterX, chartCenterY, innerRadius, _glowPaint);
        _glowPaint.MaskFilter = null;

        // Center-Text
        if (!string.IsNullOrEmpty(centerText))
        {
            _textPaint.Color = SkiaThemeHelper.TextPrimary;
            _centerFont.Size = 18f;
            canvas.DrawText(centerText, chartCenterX, chartCenterY + 2f,
                SKTextAlign.Center, _centerFont, _textPaint);

            if (!string.IsNullOrEmpty(centerSubText))
            {
                _textPaint.Color = SkiaThemeHelper.TextMuted;
                _labelFont.Size = 10f;
                canvas.DrawText(centerSubText, chartCenterX, chartCenterY + 16f,
                    SKTextAlign.Center, _labelFont, _textPaint);
            }
        }

        // Legende
        if (showLegend && legendH > 0)
        {
            float legendTop = bounds.Top + chartAreaH + 4f;
            float legendLeft = bounds.Left + 16f;
            float itemH = 16f;
            int maxItems = (int)(legendH / itemH);

            for (int i = 0; i < Math.Min(segments.Length, maxItems); i++)
            {
                float y = legendTop + i * itemH;
                float dotSize = 8f;

                // Farb-Punkt
                _segmentPaint.Color = segments[i].Color;
                canvas.DrawRoundRect(new SKRect(legendLeft, y, legendLeft + dotSize, y + dotSize), 2f, 2f, _segmentPaint);

                // Label
                _textPaint.Color = SkiaThemeHelper.TextSecondary;
                _labelFont.Size = 10f;
                string legendText = segments[i].Label;
                if (!string.IsNullOrEmpty(segments[i].ValueText))
                    legendText += $" ({segments[i].ValueText})";
                canvas.DrawText(legendText, legendLeft + dotSize + 6f, y + dotSize - 1f,
                    SKTextAlign.Left, _labelFont, _textPaint);
            }
        }
    }
}
