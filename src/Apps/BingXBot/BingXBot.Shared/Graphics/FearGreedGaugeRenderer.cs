using SkiaSharp;

namespace BingXBot.Graphics;

/// <summary>
/// Zeichnet ein Fear & Greed Gauge als Halbkreis mit Nadel.
/// Wert 0-100: 0=Extreme Fear (Rot), 50=Neutral (Gelb), 100=Extreme Greed (Grün).
/// </summary>
public static class FearGreedGaugeRenderer
{
    private static readonly SKColor BgColor = SKColor.Parse("#1E1E2E");
    private static readonly SKColor TextColor = SKColor.Parse("#E2E8F0");
    private static readonly SKColor MutedColor = SKColor.Parse("#94A3B8");
    private static readonly SKFont ValueFont = new(SKTypeface.Default, 28);
    private static readonly SKFont LabelFont = new(SKTypeface.Default, 11);
    private static readonly SKFont SmallFont = new(SKTypeface.Default, 9);
    private static readonly SKPaint TextPaint = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPaint MutedPaint = new() { Color = MutedColor, IsAntialias = true };
    private static readonly SKPaint NeedlePaint = new() { Color = SKColors.White, StrokeWidth = 2.5f, IsAntialias = true, StrokeCap = SKStrokeCap.Round };

    // 5 Segmente: Extreme Fear, Fear, Neutral, Greed, Extreme Greed
    private static readonly SKColor[] SegmentColors =
    [
        SKColor.Parse("#EF4444"), // 0-20: Extreme Fear
        SKColor.Parse("#F97316"), // 20-40: Fear
        SKColor.Parse("#EAB308"), // 40-60: Neutral
        SKColor.Parse("#84CC16"), // 60-80: Greed
        SKColor.Parse("#10B981"), // 80-100: Extreme Greed
    ];

    public static void Render(SKCanvas canvas, SKRect bounds, float value, string label)
    {
        canvas.Clear(BgColor);

        var cx = bounds.MidX;
        var cy = bounds.MidY + 20;
        var radius = Math.Min(bounds.Width * 0.4f, bounds.Height * 0.6f);

        // Halbkreis-Segmente zeichnen (180° aufgeteilt in 5)
        var arcWidth = radius * 0.18f;
        using var arcPaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = arcWidth, IsAntialias = true, StrokeCap = SKStrokeCap.Butt };

        var oval = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
        for (int i = 0; i < 5; i++)
        {
            arcPaint.Color = SegmentColors[i];
            canvas.DrawArc(oval, 180 + i * 36, 36, false, arcPaint);
        }

        // Nadel: Wert 0-100 auf 180°-0° (links nach rechts)
        var clampedValue = Math.Clamp(value, 0, 100);
        var angle = 180 - clampedValue / 100f * 180f;
        var rad = angle * MathF.PI / 180f;
        var needleLen = radius * 0.75f;
        var nx = cx + MathF.Cos(rad) * needleLen;
        var ny = cy - MathF.Sin(rad) * needleLen;
        canvas.DrawLine(cx, cy, nx, ny, NeedlePaint);

        // Mittelpunkt-Kreis
        using var centerPaint = new SKPaint { Color = SKColor.Parse("#3F3F5C"), Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawCircle(cx, cy, 6, centerPaint);
        using var centerRing = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        canvas.DrawCircle(cx, cy, 6, centerRing);

        // Wert-Text
        canvas.DrawText($"{(int)clampedValue}", cx, cy + 30, SKTextAlign.Center, ValueFont, TextPaint);

        // Label (z.B. "Fear", "Greed", "Neutral")
        canvas.DrawText(label, cx, cy + 48, SKTextAlign.Center, LabelFont, MutedPaint);

        // Skala-Beschriftungen
        canvas.DrawText("0", cx - radius - 5, cy + 14, SKTextAlign.Center, SmallFont, MutedPaint);
        canvas.DrawText("100", cx + radius + 5, cy + 14, SKTextAlign.Center, SmallFont, MutedPaint);
        canvas.DrawText("50", cx, cy - radius - 8, SKTextAlign.Center, SmallFont, MutedPaint);
    }
}
