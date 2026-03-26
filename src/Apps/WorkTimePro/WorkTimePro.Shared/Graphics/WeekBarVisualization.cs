using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// SkiaSharp-Wochen-Balkendiagramm für die WeekOverviewView.
/// Zeigt 7 Tage als Balken mit Ist/Soll-Vergleich, farbiger Saldo-Anzeige und Ziel-Linie.
/// </summary>
public static class WeekBarVisualization
{
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _targetPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0) };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
    private static readonly SKFont _labelFont = new() { Size = 11f };
    private static readonly SKFont _valueFont = new() { Size = 10f };

    /// <summary>
    /// Rendert das Wochen-Balkendiagramm.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="dayLabels">7 Tagesnamen (Mo-So)</param>
    /// <param name="actualHours">7 Ist-Stunden pro Tag</param>
    /// <param name="targetHours">7 Soll-Stunden pro Tag</param>
    /// <param name="todayIndex">Index des heutigen Tages (0-6, -1 wenn nicht in der Woche)</param>
    public static void Render(SKCanvas canvas, SKRect bounds,
        string[] dayLabels, float[] actualHours, float[] targetHours, int todayIndex)
    {
        if (dayLabels.Length != 7 || actualHours.Length != 7 || targetHours.Length != 7) return;

        float padding = 16f;
        float labelH = 20f;
        float valueH = 16f;
        float chartLeft = bounds.Left + padding;
        float chartRight = bounds.Right - padding;
        float chartTop = bounds.Top + padding + valueH;
        float chartBottom = bounds.Bottom - padding - labelH;
        float chartW = chartRight - chartLeft;
        float chartH = chartBottom - chartTop;

        if (chartH <= 10 || chartW <= 10) return;

        // Maximal-Wert für Skalierung (mindestens 8h)
        float maxHours = 8f;
        for (int i = 0; i < 7; i++)
        {
            maxHours = Math.Max(maxHours, actualHours[i]);
            maxHours = Math.Max(maxHours, targetHours[i]);
        }
        maxHours *= 1.1f; // 10% Luft oben

        float barW = chartW / 7f;
        float barMaxW = Math.Min(barW - 10f, 32f);

        // Grundlinie
        _linePaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 40);
        canvas.DrawLine(chartLeft, chartBottom, chartRight, chartBottom, _linePaint);

        for (int i = 0; i < 7; i++)
        {
            float barCx = chartLeft + barW * i + barW / 2f;

            // Ist-Balken
            float actualFraction = actualHours[i] / maxHours;
            float barH = Math.Max(actualFraction * chartH, actualHours[i] > 0 ? 3f : 0f);

            if (barH > 0)
            {
                float barLeft = barCx - barMaxW / 2f;
                float barTop = chartBottom - barH;
                var barRect = new SKRect(barLeft, barTop, barLeft + barMaxW, chartBottom);

                // Farbe: Grün wenn >= Soll, Orange wenn drunter
                SKColor barColor;
                if (targetHours[i] <= 0)
                    barColor = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 100); // Kein Soll (Wochenende etc.)
                else if (actualHours[i] >= targetHours[i])
                    barColor = SkiaThemeHelper.Success; // Soll erreicht
                else
                    barColor = SkiaThemeHelper.Warning; // Unter Soll

                // Gradient von oben nach unten
                _barPaint.Shader?.Dispose();
                _barPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(barCx, barTop),
                    new SKPoint(barCx, chartBottom),
                    new[] { SkiaThemeHelper.AdjustBrightness(barColor, 1.3f), barColor },
                    null, SKShaderTileMode.Clamp);

                float cornerR = Math.Min(5f, barMaxW / 2f);
                canvas.DrawRoundRect(barRect, cornerR, cornerR, _barPaint);
                _barPaint.Shader?.Dispose();
                _barPaint.Shader = null;

                // Heutiger Tag: Leuchtender Rahmen
                if (i == todayIndex)
                {
                    _linePaint.Color = SkiaThemeHelper.Primary;
                    _linePaint.StrokeWidth = 1.5f;
                    canvas.DrawRoundRect(barRect, cornerR, cornerR, _linePaint);
                    _linePaint.StrokeWidth = 1f;
                }
            }

            // Soll-Markierung (gestrichelte Linie)
            if (targetHours[i] > 0)
            {
                float targetY = chartBottom - (targetHours[i] / maxHours) * chartH;
                _targetPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.TextMuted, 120);
                canvas.DrawLine(barCx - barMaxW / 2f - 2f, targetY,
                    barCx + barMaxW / 2f + 2f, targetY, _targetPaint);
            }

            // Stunden-Wert über dem Balken
            if (actualHours[i] > 0)
            {
                string valueStr = actualHours[i] >= 10 ? $"{actualHours[i]:F0}" : $"{actualHours[i]:F1}";
                _textPaint.Color = SkiaThemeHelper.TextSecondary;
                _valueFont.Size = 10f;
                float valueY = chartBottom - barH - 4f;
                canvas.DrawText(valueStr, barCx, valueY, SKTextAlign.Center, _valueFont, _textPaint);
            }

            // Tagesname
            _textPaint.Color = i == todayIndex ? SkiaThemeHelper.Primary : SkiaThemeHelper.TextMuted;
            _labelFont.Size = 11f;
            canvas.DrawText(dayLabels[i], barCx, chartBottom + labelH,
                SKTextAlign.Center, _labelFont, _textPaint);
        }
    }
}
