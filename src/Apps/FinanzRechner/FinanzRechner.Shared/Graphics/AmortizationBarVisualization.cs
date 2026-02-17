using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// SkiaSharp-Renderer für gestapelte Balkendiagramme (Stacked Column Chart).
/// Ersetzt LiveCharts StackedColumnSeries in AmortizationView.
/// Zeigt Tilgung (grün) und Zinsen (amber) pro Jahr.
/// </summary>
public static class AmortizationBarVisualization
{
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _gridPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _labelFont = new() { Size = 10f };
    private static readonly SKFont _axisFont = new() { Size = 8f };

    private static readonly SKColor PrincipalColor = new(0x22, 0xC5, 0x5E);
    private static readonly SKColor InterestColor = new(0xF5, 0x9E, 0x0B);

    /// <summary>
    /// Rendert ein gestapeltes Balkendiagramm (Tilgung + Zinsen pro Jahr).
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="yearLabels">Jahreslabels (z.B. "1", "2", ...)</param>
    /// <param name="principalPayments">Tilgungsanteile pro Jahr</param>
    /// <param name="interestPayments">Zinsanteile pro Jahr</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string[] yearLabels, float[] principalPayments, float[] interestPayments)
    {
        int count = yearLabels.Length;
        if (count < 1 || principalPayments.Length != count || interestPayments.Length != count) return;

        float padding = 8f;
        float leftMargin = 40f;
        float bottomMargin = 22f;
        float topMargin = 10f;

        float chartLeft = bounds.Left + leftMargin;
        float chartRight = bounds.Right - padding;
        float chartTop = bounds.Top + topMargin;
        float chartBottom = bounds.Bottom - bottomMargin;
        float chartW = chartRight - chartLeft;
        float chartH = chartBottom - chartTop;

        if (chartW <= 20 || chartH <= 20) return;

        // Max-Wert bestimmen (gestapelt)
        float maxVal = 100f;
        for (int i = 0; i < count; i++)
        {
            float stacked = principalPayments[i] + interestPayments[i];
            maxVal = Math.Max(maxVal, stacked);
        }
        maxVal *= 1.1f;

        // Grid-Linien + Y-Achse
        _gridPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 20);
        _textPaint.Color = SkiaThemeHelper.TextMuted;

        float gridStep = CalculateGridStep(maxVal);
        for (float v = 0; v <= maxVal; v += gridStep)
        {
            float y = chartBottom - (v / maxVal) * chartH;
            canvas.DrawLine(chartLeft, y, chartRight, y, _gridPaint);
            string label = FormatYLabel(v);
            canvas.DrawText(label, chartLeft - 4f, y + 3f, SKTextAlign.Right, _axisFont, _textPaint);
        }

        // Balken zeichnen
        float barGroupWidth = chartW / count;
        float maxBarWidth = Math.Min(35f, barGroupWidth * 0.7f);
        float barRadius = 3f;

        for (int i = 0; i < count; i++)
        {
            float centerX = chartLeft + barGroupWidth * i + barGroupWidth / 2f;
            float barLeft = centerX - maxBarWidth / 2f;
            float barRight = centerX + maxBarWidth / 2f;

            float principalH = (principalPayments[i] / maxVal) * chartH;
            float interestH = (interestPayments[i] / maxVal) * chartH;

            // Tilgung (unten, grün)
            float principalTop = chartBottom - principalH;
            using var principalRect = new SKRoundRect(
                new SKRect(barLeft, principalTop, barRight, chartBottom), barRadius, barRadius);
            // Nur obere Ecken abrunden wenn kein Zinsen-Anteil
            if (interestH > 0.5f)
                principalRect.SetRectRadii(new SKRect(barLeft, principalTop, barRight, chartBottom),
                    new[] { SKPoint.Empty, SKPoint.Empty, new(barRadius, barRadius), new(barRadius, barRadius) });

            _barPaint.Color = PrincipalColor;
            canvas.DrawRoundRect(principalRect, _barPaint);

            // Zinsen (oben, amber)
            if (interestH > 0.5f)
            {
                float interestTop = principalTop - interestH;
                using var interestRect = new SKRoundRect(
                    new SKRect(barLeft, interestTop, barRight, principalTop), barRadius, barRadius);
                // Nur obere Ecken abrunden
                interestRect.SetRectRadii(new SKRect(barLeft, interestTop, barRight, principalTop),
                    new[] { new SKPoint(barRadius, barRadius), new SKPoint(barRadius, barRadius), SKPoint.Empty, SKPoint.Empty });

                _barPaint.Color = InterestColor;
                canvas.DrawRoundRect(interestRect, _barPaint);
            }

            // X-Label
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            // Nicht alle Labels anzeigen wenn zu viele
            if (count <= 10 || i % 2 == 0 || i == count - 1)
            {
                canvas.DrawText(yearLabels[i], centerX, chartBottom + 14f,
                    SKTextAlign.Center, _labelFont, _textPaint);
            }
        }
    }

    /// <summary>
    /// Berechnet den optimalen Grid-Schritt für die Y-Achse.
    /// </summary>
    private static float CalculateGridStep(float maxVal)
    {
        if (maxVal <= 500) return 100f;
        if (maxVal <= 1000) return 200f;
        if (maxVal <= 2000) return 500f;
        if (maxVal <= 5000) return 1000f;
        if (maxVal <= 10000) return 2000f;
        if (maxVal <= 50000) return 10000f;
        if (maxVal <= 100000) return 20000f;
        return maxVal / 5f;
    }

    /// <summary>
    /// Formatiert Y-Achsen-Labels kompakt.
    /// </summary>
    private static string FormatYLabel(float value)
    {
        if (value >= 1_000_000) return $"{value / 1_000_000:F1}M";
        if (value >= 1_000) return $"{value / 1_000:F0}k";
        return $"{value:F0}";
    }
}
