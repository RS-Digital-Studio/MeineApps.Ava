using SkiaSharp;
using BingXBot.Core.Models;

namespace BingXBot.Graphics;

/// <summary>
/// Zeichnet eine Equity-Kurve als Linien-Chart auf einer SKCanvasView.
/// Trading-Theme mit Profit/Loss-Farbgebung und Baseline-Markierung.
/// </summary>
public static class EquityChartRenderer
{
    // Farben (Trading-Theme, passend zur AppPalette)
    private static readonly SKColor BackgroundColor = SKColor.Parse("#1E1E2E");
    private static readonly SKColor GridColor = SKColor.Parse("#3F3F5C");
    private static readonly SKColor TextColor = SKColor.Parse("#94A3B8");
    private static readonly SKColor ProfitColor = SKColor.Parse("#10B981");
    private static readonly SKColor LossColor = SKColor.Parse("#EF4444");
    private static readonly SKColor BaselineColor = SKColor.Parse("#3B82F6");

    // Gecachte Fonts (vermeidet Allokation pro Frame)
    private static readonly SKFont GridFont = new() { Size = 10 };
    private static readonly SKFont EmptyFont = new() { Size = 14 };

    // Gecachte Paints (vermeidet pro-Frame Allokationen)
    private static readonly SKPaint EmptyTextPaint = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPaint CachedGridPaint = new() { Color = GridColor, StrokeWidth = 0.5f, IsAntialias = true };
    private static readonly SKPaint CachedGridTextPaint = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPaint ProfitLinePaint = new() { Color = ProfitColor, StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private static readonly SKPaint LossLinePaint = new() { Color = LossColor, StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private static readonly SKPaint ProfitFillPaint = new() { Color = ProfitColor.WithAlpha(30), Style = SKPaintStyle.Fill, IsAntialias = true };
    private static readonly SKPaint LossFillPaint = new() { Color = LossColor.WithAlpha(30), Style = SKPaintStyle.Fill, IsAntialias = true };

    public static void Render(SKCanvas canvas, SKRect bounds, IReadOnlyList<EquityPoint> data, decimal initialBalance)
    {
        canvas.Clear(BackgroundColor);

        if (data.Count < 2)
        {
            canvas.DrawText("Keine Equity-Daten", bounds.MidX, bounds.MidY, SKTextAlign.Center, EmptyFont, EmptyTextPaint);
            return;
        }

        var padding = new SKRect(60, 20, 20, 40); // Links mehr Platz für Y-Achse
        var chartArea = new SKRect(
            bounds.Left + padding.Left,
            bounds.Top + padding.Top,
            bounds.Right - padding.Right,
            bounds.Bottom - padding.Bottom);

        // Min/Max berechnen
        var minEquity = data[0].Equity;
        var maxEquity = data[0].Equity;
        for (int i = 1; i < data.Count; i++)
        {
            if (data[i].Equity < minEquity) minEquity = data[i].Equity;
            if (data[i].Equity > maxEquity) maxEquity = data[i].Equity;
        }

        var range = maxEquity - minEquity;
        if (range == 0) range = 1; // Division durch Null vermeiden
        minEquity -= range * 0.05m; // 5% Padding
        maxEquity += range * 0.05m;

        // Grid zeichnen
        DrawGrid(canvas, chartArea, minEquity, maxEquity, data);

        // Baseline (Startkapital) zeichnen
        DrawBaseline(canvas, chartArea, initialBalance, minEquity, maxEquity);

        // Fläche unter der Kurve (Semi-transparent, VOR der Linie)
        DrawEquityFill(canvas, chartArea, data, initialBalance, minEquity, maxEquity);

        // Equity-Linie zeichnen (Gradient: Grün über Baseline, Rot darunter)
        DrawEquityLine(canvas, chartArea, data, initialBalance, minEquity, maxEquity);
    }

    private static void DrawGrid(SKCanvas canvas, SKRect area, decimal minVal, decimal maxVal, IReadOnlyList<EquityPoint> data)
    {
        // Horizontale Grid-Linien (5 Stueck)
        for (int i = 0; i <= 4; i++)
        {
            var y = area.Top + (area.Height * i / 4f);
            canvas.DrawLine(area.Left, y, area.Right, y, CachedGridPaint);

            var value = maxVal - (maxVal - minVal) * i / 4m;
            canvas.DrawText($"{value:F0}", area.Left - 5, y + 4, SKTextAlign.Right, GridFont, CachedGridTextPaint);
        }

        // Vertikale Grid-Linien (basierend auf Zeitstempel)
        var totalPoints = data.Count;
        var step = Math.Max(1, totalPoints / 5);
        for (int i = 0; i < totalPoints; i += step)
        {
            var x = area.Left + (area.Width * i / (totalPoints - 1f));
            canvas.DrawLine(x, area.Top, x, area.Bottom, CachedGridPaint);

            var label = data[i].Time.ToString("dd.MM");
            canvas.DrawText(label, x, area.Bottom + 15, SKTextAlign.Center, GridFont, CachedGridTextPaint);
        }
    }

    // Gecachter Baseline-Paint (vermeidet Allokation pro Frame)
    private static readonly SKPathEffect BaselineDashEffect = SKPathEffect.CreateDash([6f, 4f], 0);
    private static readonly SKPaint BaselinePaint = new()
    {
        Color = BaselineColor.WithAlpha(100),
        StrokeWidth = 1f,
        PathEffect = BaselineDashEffect,
        IsAntialias = true
    };

    private static void DrawBaseline(SKCanvas canvas, SKRect area, decimal baseline, decimal min, decimal max)
    {
        var y = MapY(baseline, area, min, max);
        canvas.DrawLine(area.Left, y, area.Right, y, BaselinePaint);
    }

    private static void DrawEquityLine(SKCanvas canvas, SKRect area, IReadOnlyList<EquityPoint> data,
        decimal baseline, decimal min, decimal max)
    {
        using var path = new SKPath();
        var firstPoint = true;

        for (int i = 0; i < data.Count; i++)
        {
            var x = MapX(i, area, data.Count);
            var y = MapY(data[i].Equity, area, min, max);

            if (firstPoint) { path.MoveTo(x, y); firstPoint = false; }
            else path.LineTo(x, y);
        }

        // Linie zeichnen - Farbe basierend auf letztem Wert vs Baseline
        var lastEquity = data[^1].Equity;
        canvas.DrawPath(path, lastEquity >= baseline ? ProfitLinePaint : LossLinePaint);
    }

    private static void DrawEquityFill(SKCanvas canvas, SKRect area, IReadOnlyList<EquityPoint> data,
        decimal baseline, decimal min, decimal max)
    {
        using var path = new SKPath();
        var baselineY = MapY(baseline, area, min, max);

        // Von Baseline starten
        path.MoveTo(MapX(0, area, data.Count), baselineY);

        // Equity-Linie
        for (int i = 0; i < data.Count; i++)
        {
            var x = MapX(i, area, data.Count);
            var y = MapY(data[i].Equity, area, min, max);
            path.LineTo(x, y);
        }

        // Zurück zur Baseline
        path.LineTo(MapX(data.Count - 1, area, data.Count), baselineY);
        path.Close();

        var lastEquity = data[^1].Equity;
        canvas.DrawPath(path, lastEquity >= baseline ? ProfitFillPaint : LossFillPaint);
    }

    private static float MapX(int index, SKRect area, int totalPoints)
    {
        if (totalPoints <= 1) return area.MidX;
        return area.Left + (area.Width * index / (totalPoints - 1f));
    }

    private static float MapY(decimal value, SKRect area, decimal min, decimal max)
    {
        var range = max - min;
        if (range == 0) return area.MidY;
        var normalized = (float)((value - min) / range);
        return area.Bottom - (normalized * area.Height);
    }
}
