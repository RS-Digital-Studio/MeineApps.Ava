using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace WorkTimePro.Graphics;

/// <summary>
/// Kompakte Wochen-Fortschrittsbalken für die MonthOverviewView.
/// Rendert alle Wochen eines Monats als horizontale Balken in einem einzigen Canvas.
/// Ersetzt die ProgressBar-Controls in der ItemsControl DataTemplate.
/// </summary>
public static class MonthWeekProgressVisualization
{
    private static readonly SKPaint _barPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _glowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _weekFont = new() { Size = 12f };
    private static readonly SKFont _hoursFont = new() { Size = 11f };
    private static readonly SKFont _balanceFont = new() { Size = 11f };

    /// <summary>
    /// Daten für eine einzelne Woche.
    /// </summary>
    public struct WeekData
    {
        /// <summary>Wochen-Label (z.B. "KW 10").</summary>
        public string Label;

        /// <summary>Fortschritt 0-100 (kann >100 sein für Überstunden).</summary>
        public float ProgressPercent;

        /// <summary>Ist-Stunden als formatierter String (z.B. "32:30").</summary>
        public string ActualDisplay;

        /// <summary>Saldo als formatierter String (z.B. "+2:30" oder "-1:00").</summary>
        public string BalanceDisplay;

        /// <summary>Saldo-Farbe als SKColor.</summary>
        public SKColor BalanceColor;
    }

    /// <summary>
    /// Rendert alle Wochen als horizontale Balken mit Gradient, Glow und Labels.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="weeks">Wochen-Daten</param>
    public static void Render(SKCanvas canvas, SKRect bounds, WeekData[] weeks)
    {
        if (weeks.Length == 0) return;

        float padding = 8f;
        float rowH = 36f;
        float barH = 10f;
        float labelW = 50f;  // Platz für "KW XX"
        float rightW = 110f; // Platz für Stunden + Saldo

        float barLeft = bounds.Left + padding + labelW;
        float barRight = bounds.Right - padding - rightW;
        float barW = barRight - barLeft;

        if (barW < 20) return;

        float startY = bounds.Top + padding;
        float cornerR = barH / 2f;

        for (int i = 0; i < weeks.Length; i++)
        {
            float y = startY + i * rowH;
            if (y + rowH > bounds.Bottom) break;

            var week = weeks[i];

            // Wochen-Label (links)
            _textPaint.Color = SkiaThemeHelper.Accent;
            _weekFont.Size = 12f;
            canvas.DrawText(week.Label, bounds.Left + padding, y + rowH / 2f + 4f,
                SKTextAlign.Left, _weekFont, _textPaint);

            // Balken-Track (dezenter Hintergrund)
            float barY = y + (rowH - barH) / 2f;
            var trackRect = new SKRect(barLeft, barY, barRight, barY + barH);
            _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 30);
            canvas.DrawRoundRect(trackRect, cornerR, cornerR, _trackPaint);

            // Fortschritts-Balken (mit Gradient)
            float progress = MathF.Min(week.ProgressPercent / 100f, 1.2f);
            if (progress > 0)
            {
                float fillW = MathF.Min(progress, 1f) * barW;
                var fillRect = new SKRect(barLeft, barY, barLeft + fillW, barY + barH);

                // Gradient: Primary → Accent
                using var barShader = SKShader.CreateLinearGradient(
                    new SKPoint(barLeft, barY),
                    new SKPoint(barLeft + fillW, barY),
                    new[] { SkiaThemeHelper.Primary, SkiaThemeHelper.Accent },
                    null, SKShaderTileMode.Clamp);
                _barPaint.Shader = barShader;

                // Clip auf abgerundetes Rechteck
                canvas.Save();
                using var clipPath = new SKPath();
                clipPath.AddRoundRect(trackRect, cornerR, cornerR);
                canvas.ClipPath(clipPath);

                canvas.DrawRect(fillRect, _barPaint);
                _barPaint.Shader = null;

                // Glow am rechten Rand
                if (fillW > 10f)
                {
                    _glowPaint.Color = SkiaThemeHelper.Accent.WithAlpha(60);
                    using (var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f))
                    {
                        _glowPaint.MaskFilter = blur;
                        canvas.DrawCircle(barLeft + fillW, barY + barH / 2f, barH, _glowPaint);
                        _glowPaint.MaskFilter = null;
                    }
                }

                // Überstunden-Markierung (>100%)
                if (progress > 1f)
                {
                    float overW = (progress - 1f) * barW;
                    overW = MathF.Min(overW, barW * 0.2f);
                    _barPaint.Color = SkiaThemeHelper.AdjustBrightness(SkiaThemeHelper.Accent, 1.3f).WithAlpha(150);
                    canvas.DrawRect(barLeft + barW, barY, overW, barH, _barPaint);
                }

                canvas.Restore();
            }

            // Ist-Stunden (rechts vom Balken)
            _textPaint.Color = SkiaThemeHelper.TextPrimary;
            _hoursFont.Size = 11f;
            canvas.DrawText(week.ActualDisplay, barRight + 6f, y + rowH / 2f + 4f,
                SKTextAlign.Left, _hoursFont, _textPaint);

            // Saldo (ganz rechts, farbig)
            _textPaint.Color = week.BalanceColor.Alpha > 0 ? week.BalanceColor : SkiaThemeHelper.TextSecondary;
            _balanceFont.Size = 11f;
            canvas.DrawText(week.BalanceDisplay, bounds.Right - padding, y + rowH / 2f + 4f,
                SKTextAlign.Right, _balanceFont, _textPaint);
        }
    }
}
