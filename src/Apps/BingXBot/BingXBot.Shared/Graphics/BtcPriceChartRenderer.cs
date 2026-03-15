using SkiaSharp;
using BingXBot.Core.Models;

namespace BingXBot.Graphics;

/// <summary>
/// Zeichnet einen Candlestick-Chart fuer BTC-Preisdaten (SkiaSharp).
/// Unten 25% Volumen-Balken, oben 75% Candlesticks.
/// </summary>
public static class BtcPriceChartRenderer
{
    private static readonly SKColor BackgroundColor = SKColor.Parse("#1E1E2E");
    private static readonly SKColor GridColor = SKColor.Parse("#3F3F5C");
    private static readonly SKColor TextColor = SKColor.Parse("#94A3B8");
    private static readonly SKColor BullishColor = SKColor.Parse("#10B981");  // Gruen
    private static readonly SKColor BearishColor = SKColor.Parse("#EF4444");  // Rot

    // Gecachte Fonts (vermeidet Allokation pro Frame)
    private static readonly SKFont LabelFont = new(SKTypeface.Default, 10);
    private static readonly SKFont EmptyFont = new(SKTypeface.Default, 14);

    public static void Render(SKCanvas canvas, SKRect bounds, IReadOnlyList<Candle> candles)
    {
        canvas.Clear(BackgroundColor);

        if (candles.Count < 2)
        {
            using var textPaint = new SKPaint { Color = TextColor, IsAntialias = true };
            canvas.DrawText("Lade BTC-Daten...", bounds.MidX, bounds.MidY, SKTextAlign.Center, EmptyFont, textPaint);
            return;
        }

        // Layout: oben 75% Candles, unten 25% Volumen
        var padding = new SKRect(55, 15, 15, 30);
        var chartArea = new SKRect(
            bounds.Left + padding.Left, bounds.Top + padding.Top,
            bounds.Right - padding.Right, bounds.Bottom - padding.Bottom);

        var priceArea = new SKRect(chartArea.Left, chartArea.Top, chartArea.Right, chartArea.Top + chartArea.Height * 0.75f);
        var volumeArea = new SKRect(chartArea.Left, priceArea.Bottom + 5, chartArea.Right, chartArea.Bottom);

        // Min/Max berechnen
        var minPrice = candles[0].Low;
        var maxPrice = candles[0].High;
        var maxVolume = candles[0].Volume;
        for (int i = 1; i < candles.Count; i++)
        {
            if (candles[i].Low < minPrice) minPrice = candles[i].Low;
            if (candles[i].High > maxPrice) maxPrice = candles[i].High;
            if (candles[i].Volume > maxVolume) maxVolume = candles[i].Volume;
        }

        var priceRange = maxPrice - minPrice;
        minPrice -= priceRange * 0.02m;
        maxPrice += priceRange * 0.02m;

        // Grid + Preis-Labels
        DrawPriceGrid(canvas, priceArea, minPrice, maxPrice);

        // Zeit-Labels
        DrawTimeLabels(canvas, chartArea, candles);

        // Candlesticks zeichnen
        var candleWidth = priceArea.Width / candles.Count;
        var bodyWidth = Math.Max(candleWidth * 0.6f, 1f);

        for (int i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            var x = priceArea.Left + candleWidth * i + candleWidth / 2;
            var isBullish = c.Close >= c.Open;
            var color = isBullish ? BullishColor : BearishColor;

            // Docht (High-Low Linie)
            var highY = MapY(c.High, priceArea, minPrice, maxPrice);
            var lowY = MapY(c.Low, priceArea, minPrice, maxPrice);
            using var wickPaint = new SKPaint { Color = color, StrokeWidth = 1f, IsAntialias = true };
            canvas.DrawLine(x, highY, x, lowY, wickPaint);

            // Body (Open-Close Rechteck)
            var openY = MapY(c.Open, priceArea, minPrice, maxPrice);
            var closeY = MapY(c.Close, priceArea, minPrice, maxPrice);
            var bodyTop = Math.Min(openY, closeY);
            var bodyBottom = Math.Max(openY, closeY);
            var bodyHeight = Math.Max(bodyBottom - bodyTop, 1f);

            using var bodyPaint = new SKPaint
            {
                Color = color,
                Style = isBullish ? SKPaintStyle.Stroke : SKPaintStyle.Fill,
                StrokeWidth = 1f,
                IsAntialias = true
            };
            canvas.DrawRect(x - bodyWidth / 2, bodyTop, bodyWidth, bodyHeight, bodyPaint);

            // Volumen-Balken
            if (maxVolume > 0)
            {
                var volHeight = (float)(c.Volume / maxVolume) * volumeArea.Height;
                using var volPaint = new SKPaint
                {
                    Color = color.WithAlpha(80),
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(x - bodyWidth / 2, volumeArea.Bottom - volHeight, bodyWidth, volHeight, volPaint);
            }
        }

        // Aktueller Preis-Label rechts (gepunktete Linie + Text)
        var lastPrice = candles[^1].Close;
        var lastY = MapY(lastPrice, priceArea, minPrice, maxPrice);
        var lastColor = candles[^1].Close >= candles[^1].Open ? BullishColor : BearishColor;

        using var priceLabelPaint = new SKPaint { Color = lastColor, IsAntialias = true };
        using var dashEffect = SKPathEffect.CreateDash([4f, 4f], 0);
        using var priceLinePaint = new SKPaint
        {
            Color = lastColor.WithAlpha(100),
            StrokeWidth = 1f,
            PathEffect = dashEffect
        };
        canvas.DrawLine(priceArea.Left, lastY, priceArea.Right, lastY, priceLinePaint);
        canvas.DrawText($"{lastPrice:F1}", priceArea.Right + 3, lastY + 4, LabelFont, priceLabelPaint);
    }

    private static void DrawPriceGrid(SKCanvas canvas, SKRect area, decimal min, decimal max)
    {
        using var gridPaint = new SKPaint { Color = GridColor, StrokeWidth = 0.5f };
        using var textPaint = new SKPaint { Color = TextColor, IsAntialias = true };

        for (int i = 0; i <= 4; i++)
        {
            var y = area.Top + area.Height * i / 4f;
            canvas.DrawLine(area.Left, y, area.Right, y, gridPaint);
            var price = max - (max - min) * i / 4m;
            canvas.DrawText($"{price:F0}", area.Left - 5, y + 4, SKTextAlign.Right, LabelFont, textPaint);
        }
    }

    private static void DrawTimeLabels(SKCanvas canvas, SKRect area, IReadOnlyList<Candle> candles)
    {
        using var textPaint = new SKPaint { Color = TextColor, IsAntialias = true };
        var step = Math.Max(1, candles.Count / 6);
        var candleWidth = area.Width / candles.Count;

        for (int i = 0; i < candles.Count; i += step)
        {
            var x = area.Left + candleWidth * i + candleWidth / 2;
            var label = candles[i].OpenTime.ToString("HH:mm");
            canvas.DrawText(label, x, area.Bottom + 14, SKTextAlign.Center, LabelFont, textPaint);
        }
    }

    private static float MapY(decimal value, SKRect area, decimal min, decimal max)
    {
        var range = max - min;
        if (range == 0) return area.MidY;
        return area.Bottom - (float)((value - min) / range) * area.Height;
    }
}
