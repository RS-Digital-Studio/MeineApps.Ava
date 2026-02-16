using MeineApps.UI.SkiaSharp;
using SkiaSharp;

namespace FinanzRechner.Graphics;

/// <summary>
/// SkiaSharp Mini-Ringe für Budget-Kategorien (Übersicht auf HomeView).
/// Zeigt mehrere kleine Ringe nebeneinander mit Kategorie-Farben.
/// </summary>
public static class BudgetMiniRingVisualization
{
    private static readonly SKPaint _trackPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _arcPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private static readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint _dotPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKFont _percentFont = new() { Size = 9f };
    private static readonly SKFont _nameFont = new() { Size = 8f };

    /// <summary>
    /// Daten für einen einzelnen Budget-Ring.
    /// </summary>
    public readonly struct BudgetRingData
    {
        public readonly string Name;
        public readonly float Percentage; // 0-100+
        public readonly SKColor Color;

        public BudgetRingData(string name, float percentage, SKColor color)
        {
            Name = name;
            Percentage = percentage;
            Color = color;
        }
    }

    /// <summary>
    /// Rendert eine Reihe von Mini-Budget-Ringen horizontal nebeneinander.
    /// </summary>
    /// <param name="canvas">SkiaSharp Canvas</param>
    /// <param name="bounds">Zeichenbereich</param>
    /// <param name="budgets">Budget-Daten (max. 5 Ringe empfohlen)</param>
    public static void Render(SKCanvas canvas, SKRect bounds, BudgetRingData[] budgets)
    {
        if (budgets == null || budgets.Length == 0) return;

        int count = Math.Min(budgets.Length, 6); // Max 6 Ringe
        float padding = 8f;
        float availW = bounds.Width - padding * 2;
        float ringDiameter = Math.Min(availW / count - 4f, bounds.Height - padding * 2 - 18f);
        ringDiameter = Math.Min(ringDiameter, 44f); // Max 44px Durchmesser
        float strokeW = 3.5f;
        float radius = (ringDiameter - strokeW * 2) / 2f;

        if (radius <= 5) return;

        float totalW = count * ringDiameter + (count - 1) * 6f;
        float startX = bounds.MidX - totalW / 2f + ringDiameter / 2f;
        float cy = bounds.MidY - 6f; // Platz für Name darunter

        for (int i = 0; i < count; i++)
        {
            var budget = budgets[i];
            float cx = startX + i * (ringDiameter + 6f);
            var arcRect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

            // Track (dezenter Hintergrund-Kreis)
            _trackPaint.StrokeWidth = strokeW;
            _trackPaint.Color = SkiaThemeHelper.WithAlpha(SkiaThemeHelper.Border, 35);
            canvas.DrawOval(arcRect, _trackPaint);

            // Fortschritts-Arc
            float clampedPercent = Math.Clamp(budget.Percentage, 0f, 100f);
            float sweepAngle = (clampedPercent / 100f) * 360f;

            if (sweepAngle > 1f)
            {
                // Farbe: Budget-Farbe oder Rot wenn >100%
                SKColor arcColor = budget.Percentage > 100f
                    ? SKColor.Parse("#EF4444")
                    : budget.Color;

                _arcPaint.StrokeWidth = strokeW;
                _arcPaint.Color = arcColor;

                using var arcPath = new SKPath();
                arcPath.AddArc(arcRect, -90f, sweepAngle);
                canvas.DrawPath(arcPath, _arcPaint);
            }

            // Über-Budget: Voller roter Ring + Warnung
            if (budget.Percentage > 100f)
            {
                _arcPaint.StrokeWidth = strokeW;
                _arcPaint.Color = SKColor.Parse("#EF4444").WithAlpha(180);
                canvas.DrawOval(arcRect, _arcPaint);
            }

            // Prozent-Text in der Mitte
            _textPaint.Color = budget.Percentage > 100f
                ? SKColor.Parse("#EF4444")
                : SkiaThemeHelper.TextPrimary;
            _percentFont.Size = radius > 12 ? 9f : 7f;
            string pText = budget.Percentage > 99.5f ? $"{budget.Percentage:F0}" : $"{budget.Percentage:F0}";
            canvas.DrawText(pText, cx, cy + 3.5f, SKTextAlign.Center, _percentFont, _textPaint);

            // Kategorie-Name darunter (abgekürzt)
            _textPaint.Color = SkiaThemeHelper.TextMuted;
            _nameFont.Size = 8f;
            string shortName = budget.Name.Length > 5 ? budget.Name[..4] + "." : budget.Name;
            canvas.DrawText(shortName, cx, cy + radius + 12f, SKTextAlign.Center, _nameFont, _textPaint);
        }
    }
}
