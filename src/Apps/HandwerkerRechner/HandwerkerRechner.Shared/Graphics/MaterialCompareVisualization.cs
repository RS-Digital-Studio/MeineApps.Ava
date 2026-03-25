using SkiaSharp;

namespace HandwerkerRechner.Graphics;

/// <summary>
/// Visualisierung für den Material-Vergleich: Zwei Kostensäulen nebeneinander.
/// Günstigere Option wird grün hervorgehoben, Ersparnis als Badge.
/// </summary>
public static class MaterialCompareVisualization
{
    private static readonly SKPaint BarAPaint = new() { Color = new SKColor(59, 130, 246), IsAntialias = true }; // Blau
    private static readonly SKPaint BarBPaint = new() { Color = new SKColor(168, 85, 247), IsAntialias = true }; // Lila
    private static readonly SKPaint CheaperPaint = new() { Color = new SKColor(34, 197, 94), IsAntialias = true }; // Grün
    private static readonly SKPaint TextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private static readonly SKPaint ValuePaint = new() { Color = SKColors.White, IsAntialias = true };
    private static readonly SKPaint BgPaint = new() { Color = new SKColor(30, 30, 30, 120), IsAntialias = true };
    private static readonly SKPaint SavingsBgPaint = new() { Color = new SKColor(34, 197, 94, 50), IsAntialias = true };

    // SKFont-Objekte (nicht-veraltete API)
    private static readonly SKFont TextFont = new() { Size = 14f };
    private static readonly SKFont ValueFont = new() { Size = 16f, Embolden = true };

    public static void Render(SKCanvas canvas, SKRect bounds,
        string nameA, double costA, string nameB, double costB,
        double savings, double savingsPercent, bool isACheaper,
        float alpha = 1f)
    {
        if (costA <= 0 && costB <= 0) return;

        canvas.DrawRoundRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height, 12, 12, BgPaint);

        var padding = 20f;
        var maxCost = (float)Math.Max(costA, costB);
        if (maxCost <= 0) return;

        var barAreaTop = bounds.Top + padding;
        var barAreaBottom = bounds.Bottom - 60;
        var maxBarHeight = barAreaBottom - barAreaTop - 30;
        var barWidth = (bounds.Width - padding * 3) / 2;

        // Balken A
        var barAHeight = (float)(costA / maxCost) * maxBarHeight * alpha;
        var barALeft = bounds.Left + padding;
        var barATop = barAreaBottom - barAHeight;
        var paintA = isACheaper ? CheaperPaint : BarAPaint;
        canvas.DrawRoundRect(barALeft, barATop, barWidth, barAHeight, 8, 8, paintA);

        // Balken B
        var barBHeight = (float)(costB / maxCost) * maxBarHeight * alpha;
        var barBLeft = barALeft + barWidth + padding;
        var barBTop = barAreaBottom - barBHeight;
        var paintB = !isACheaper ? CheaperPaint : BarBPaint;
        canvas.DrawRoundRect(barBLeft, barBTop, barWidth, barBHeight, 8, 8, paintB);

        // Werte über den Balken
        var costAText = $"{costA:F0} €";
        var costBText = $"{costB:F0} €";
        canvas.DrawText(costAText, barALeft + barWidth / 2, barATop - 8,
            SKTextAlign.Center, ValueFont, ValuePaint);
        canvas.DrawText(costBText, barBLeft + barWidth / 2, barBTop - 8,
            SKTextAlign.Center, ValueFont, ValuePaint);

        // Labels unter den Balken
        canvas.DrawText(nameA, barALeft + barWidth / 2, barAreaBottom + 18,
            SKTextAlign.Center, TextFont, TextPaint);
        canvas.DrawText(nameB, barBLeft + barWidth / 2, barAreaBottom + 18,
            SKTextAlign.Center, TextFont, TextPaint);

        // Ersparnis-Badge
        if (savings > 0)
        {
            var savingsText = $"-{savings:F0} € ({savingsPercent:F0}%)";
            var savingsWidth = TextFont.MeasureText(savingsText, TextPaint);
            var badgeWidth = savingsWidth + 20;
            var badgeX = bounds.Left + (bounds.Width - badgeWidth) / 2;
            var badgeY = barAreaBottom + 32;

            canvas.DrawRoundRect(badgeX, badgeY, badgeWidth, 24, 12, 12, SavingsBgPaint);
            canvas.DrawText(savingsText, badgeX + badgeWidth / 2, badgeY + 17,
                SKTextAlign.Center, TextFont, CheaperPaint);
        }
    }
}
