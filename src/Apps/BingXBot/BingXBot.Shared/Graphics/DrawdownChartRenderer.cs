using SkiaSharp;
using BingXBot.Core.Models;

namespace BingXBot.Graphics;

/// <summary>
/// Zeichnet einen Drawdown-Chart als gefüllte rote Fläche unter der Null-Linie.
/// Zeigt wie weit der Account vom Höchststand gefallen ist.
/// </summary>
public static class DrawdownChartRenderer
{
    private static readonly SKColor BgColor = SKColor.Parse("#1E1E2E");
    private static readonly SKColor GridColor = SKColor.Parse("#3F3F5C");
    private static readonly SKColor TextColor = SKColor.Parse("#94A3B8");
    private static readonly SKColor DrawdownColor = SKColor.Parse("#EF4444");
    private static readonly SKColor DrawdownFill = SKColor.Parse("#EF4444").WithAlpha(40);
    private static readonly SKColor ZeroLineColor = SKColor.Parse("#94A3B8").WithAlpha(80);
    private static readonly SKFont LabelFont = new(SKTypeface.Default, 10);
    private static readonly SKPaint GridPaint = new() { Color = GridColor, StrokeWidth = 0.5f };
    private static readonly SKPaint TextPaint = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPaint LinePaint = new() { Color = DrawdownColor, StrokeWidth = 1.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint FillPaint = new() { Color = DrawdownFill, Style = SKPaintStyle.Fill };
    private static readonly SKPaint ZeroPaint = new() { Color = ZeroLineColor, StrokeWidth = 0.5f };

    public static void Render(SKCanvas canvas, SKRect bounds, IReadOnlyList<EquityPoint> equityData, decimal baseline)
    {
        canvas.Clear(BgColor);

        if (equityData.Count < 2) return;

        var padding = new SKRect(50, 10, 10, 20);
        var area = new SKRect(bounds.Left + padding.Left, bounds.Top + padding.Top,
            bounds.Right - padding.Right, bounds.Bottom - padding.Bottom);

        // Drawdown-Werte berechnen (% vom Peak)
        var drawdowns = new List<decimal>();
        var peak = baseline;
        foreach (var pt in equityData)
        {
            if (pt.Equity > peak) peak = pt.Equity;
            var dd = peak > 0 ? (pt.Equity - peak) / peak * 100m : 0m;
            drawdowns.Add(dd);
        }

        var minDd = drawdowns.Count > 0 ? drawdowns.Min() : -10m;
        minDd = Math.Min(minDd - 1m, -1m); // Mindestens -1% Bereich

        // Null-Linie
        var zeroY = area.Top;
        canvas.DrawLine(area.Left, zeroY, area.Right, zeroY, ZeroPaint);
        canvas.DrawText("0%", area.Left - 5, zeroY + 4, SKTextAlign.Right, LabelFont, TextPaint);

        // Max-DD Label
        var maxDdY = area.Bottom;
        canvas.DrawLine(area.Left, maxDdY, area.Right, maxDdY, GridPaint);
        canvas.DrawText($"{minDd:F1}%", area.Left - 5, maxDdY + 4, SKTextAlign.Right, LabelFont, TextPaint);

        // Drawdown-Linie + Füllung
        using var linePath = new SKPath();
        using var fillPath = new SKPath();
        var stepX = area.Width / (drawdowns.Count - 1);

        fillPath.MoveTo(area.Left, zeroY);

        for (int i = 0; i < drawdowns.Count; i++)
        {
            var x = area.Left + stepX * i;
            var y = zeroY + (float)(drawdowns[i] / minDd) * area.Height;
            y = Math.Clamp(y, area.Top, area.Bottom);

            if (i == 0) linePath.MoveTo(x, y);
            else linePath.LineTo(x, y);
            fillPath.LineTo(x, y);
        }

        fillPath.LineTo(area.Right, zeroY);
        fillPath.Close();

        canvas.DrawPath(fillPath, FillPaint);
        canvas.DrawPath(linePath, LinePaint);
    }
}
