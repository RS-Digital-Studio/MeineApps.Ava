using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// SkiaSharp-Balkendiagramm für wöchentliche Arbeitszeiten.
/// Ersetzt LiveCharts CartesianChart in der StatisticsView.
/// Balken mit Gradient + Soll-Referenzlinie.
/// </summary>
public static class WeeklyWorkChartVisualization
{
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _targetPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0) };
    private static readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _labelFont = new() { Size = 9f };
    private static readonly SKFont _valueFont = new() { Size = 9f };
    private static readonly SKFont _axisFont = new() { Size = 9f };

    /// <summary>
    /// Rendert das wöchentliche Arbeitszeit-Balkendiagramm.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="weekLabels">KW-Labels (z.B. "KW 5")</param>
    /// <param name="actualHours">Ist-Stunden pro Woche</param>
    /// <param name="weeklyTarget">Wöchentliches Stundensoll</param>
    /// <param name="hoursLabel">Label für Y-Achse (z.B. "Stunden")</param>
    /// <param name="targetLabel">Label für Soll-Linie</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string[] weekLabels, float[] actualHours, float weeklyTarget,
        string? hoursLabel = null, string? targetLabel = null)
    {
        int count = weekLabels.Length;
        if (count == 0 || actualHours.Length != count) return;

        float padding = 8f;
        float leftMargin = 32f;
        float bottomMargin = 24f;
        float topMargin = 12f;

        float chartLeft = bounds.Left + leftMargin;
        float chartRight = bounds.Right - padding;
        float chartTop = bounds.Top + topMargin;
        float chartBottom = bounds.Bottom - bottomMargin;
        float chartW = chartRight - chartLeft;
        float chartH = chartBottom - chartTop;

        if (chartW <= 20 || chartH <= 20) return;

        // Max-Wert
        float maxVal = weeklyTarget;
        for (int i = 0; i < count; i++)
            maxVal = Math.Max(maxVal, actualHours[i]);
        maxVal = Math.Max(maxVal, 1f) * 1.15f;

        // Grid-Linien
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 25);
        _textPaint.Color = SkiaThemeHelper.TextMuted;
        _axisFont.Size = 9f;

        float gridStep = CalculateGridStep(maxVal);
        for (float v = 0; v <= maxVal; v += gridStep)
        {
            float y = chartBottom - (v / maxVal) * chartH;
            canvas.DrawLine(chartLeft, y, chartRight, y, _gridPaint);
            canvas.DrawText($"{v:F0}h", chartLeft - 4f, y + 3f, SKTextAlign.Right, _axisFont, _textPaint);
        }

        // Y-Achsen-Label
        if (!string.IsNullOrEmpty(hoursLabel))
        {
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _axisFont.Size = 8f;
            // Vertikal links
            canvas.Save();
            canvas.RotateDegrees(-90f, bounds.Left + 8f, bounds.MidY);
            canvas.DrawText(hoursLabel, bounds.Left + 8f, bounds.MidY, SKTextAlign.Center, _axisFont, _textPaint);
            canvas.Restore();
        }

        // Balkenbreite
        float barStep = chartW / count;
        float barW = Math.Min(barStep - 6f, 28f);

        for (int i = 0; i < count; i++)
        {
            float x = chartLeft + barStep * i + barStep / 2f;
            float fraction = actualHours[i] / maxVal;
            float barH = fraction * chartH;

            if (barH > 0)
            {
                float barTop = chartBottom - barH;

                // Farbe: Grün wenn >= Soll, Blau/Primary wenn < Soll
                SKColor barColor = actualHours[i] >= weeklyTarget
                    ? SkiaThemeHelper.Success
                    : SkiaThemeHelper.Primary;

                _barPaint.Shader?.Dispose();
                _barPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(x, barTop),
                    new SKPoint(x, chartBottom),
                    new[] { SkiaThemeHelper.AdjustBrightness(barColor, 1.3f), barColor },
                    null, SKShaderTileMode.Clamp);

                float cornerR = Math.Min(4f, barW / 2f);
                canvas.DrawRoundRect(new SKRect(x - barW / 2f, barTop, x + barW / 2f, chartBottom),
                    cornerR, cornerR, _barPaint);
                _barPaint.Shader?.Dispose();
                _barPaint.Shader = null;

                // Wert über dem Balken
                _textPaint.Color = SkiaThemeHelper.TextSecondary;
                _valueFont.Size = 8f;
                canvas.DrawText($"{actualHours[i]:F1}", x, barTop - 4f,
                    SKTextAlign.Center, _valueFont, _textPaint);
            }

            // X-Label
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _labelFont.Size = count > 8 ? 7f : 9f;
            canvas.DrawText(weekLabels[i], x, chartBottom + 14f,
                SKTextAlign.Center, _labelFont, _textPaint);
        }

        // Soll-Linie
        if (weeklyTarget > 0)
        {
            float targetY = chartBottom - (weeklyTarget / maxVal) * chartH;
            _targetPaint.Color = SkiaThemeHelper.Warning;
            canvas.DrawLine(chartLeft - 4f, targetY, chartRight + 4f, targetY, _targetPaint);

            // Soll-Label
            string label = targetLabel ?? $"{weeklyTarget:F0}h";
            _textPaint.Color = SkiaThemeHelper.Warning;
            _valueFont.Size = 8f;
            canvas.DrawText(label, chartRight + 2f, targetY - 4f,
                SKTextAlign.Right, _valueFont, _textPaint);
        }
    }

    private static float CalculateGridStep(float maxValue)
    {
        if (maxValue <= 10) return 2f;
        if (maxValue <= 20) return 5f;
        if (maxValue <= 50) return 10f;
        return 20f;
    }
}
