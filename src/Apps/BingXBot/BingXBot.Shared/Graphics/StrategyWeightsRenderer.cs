using SkiaSharp;

namespace BingXBot.Graphics;

/// <summary>
/// Zeichnet ein horizontales Balkendiagramm der ATI-Strategie-Gewichte.
/// Zeigt welche Strategie im aktuellen Regime wie stark gewichtet wird.
/// </summary>
public static class StrategyWeightsRenderer
{
    private static readonly SKColor BgColor = SKColor.Parse("#1E1E2E");
    private static readonly SKColor TextColor = SKColor.Parse("#E2E8F0");
    private static readonly SKColor MutedColor = SKColor.Parse("#94A3B8");
    private static readonly SKColor BarBgColor = SKColor.Parse("#2D2D44");
    private static readonly SKFont LabelFont = new(SKTypeface.Default, 11);
    private static readonly SKFont ValueFont = new(SKTypeface.Default, 10);
    private static readonly SKPaint TextPaint = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPaint MutedPaint = new() { Color = MutedColor, IsAntialias = true };
    private static readonly SKPaint BarBgPaint = new() { Color = BarBgColor, Style = SKPaintStyle.Fill };

    // Farben pro Strategie (konsistente Zuordnung)
    private static readonly SKColor[] BarColors =
    [
        SKColor.Parse("#3B82F6"), // Blau
        SKColor.Parse("#10B981"), // Grün
        SKColor.Parse("#F59E0B"), // Amber
        SKColor.Parse("#EF4444"), // Rot
        SKColor.Parse("#8B5CF6"), // Lila
        SKColor.Parse("#06B6D4"), // Cyan
        SKColor.Parse("#EC4899"), // Pink
    ];

    public static void Render(SKCanvas canvas, SKRect bounds, IReadOnlyList<(string Name, decimal Weight)> weights)
    {
        canvas.Clear(BgColor);
        if (weights.Count == 0) return;

        var padding = 12f;
        var labelW = 130f;
        var barH = Math.Min((bounds.Height - padding * 2) / weights.Count - 6, 24f);
        var maxBarW = bounds.Width - labelW - padding * 2 - 50;
        var maxWeight = weights.Max(w => w.Weight);
        if (maxWeight <= 0) maxWeight = 1;

        for (int i = 0; i < weights.Count; i++)
        {
            var y = padding + i * (barH + 6);
            var (name, weight) = weights[i];
            var barW = (float)(weight / maxWeight) * maxBarW;

            // Label links
            canvas.DrawText(name, padding, y + barH / 2 + 4, LabelFont, TextPaint);

            // Balken-Hintergrund
            canvas.DrawRoundRect(labelW, y, maxBarW, barH, 4, 4, BarBgPaint);

            // Balken
            var color = BarColors[i % BarColors.Length];
            using var barPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
            if (barW > 4) canvas.DrawRoundRect(labelW, y, barW, barH, 4, 4, barPaint);

            // Wert rechts
            canvas.DrawText($"{weight:P0}", labelW + maxBarW + 8, y + barH / 2 + 4, ValueFont, MutedPaint);
        }
    }
}
