using SkiaSharp;

namespace BingXBot.Graphics;

/// <summary>
/// Zeichnet einen PnL-Kalender als farbige Tages-Kacheln.
/// Grün = Gewinn-Tag, Rot = Verlust-Tag, Intensität = Betrag.
/// </summary>
public static class PnlCalendarRenderer
{
    private static readonly SKColor BgColor = SKColor.Parse("#1E1E2E");
    private static readonly SKColor TextColor = SKColor.Parse("#94A3B8");
    private static readonly SKColor EmptyColor = SKColor.Parse("#2D2D44");
    private static readonly SKColor GridColor = SKColor.Parse("#3F3F5C");
    private static readonly SKFont DayFont = new(SKTypeface.Default, 9);
    private static readonly SKFont ValueFont = new(SKTypeface.Default, 8);
    private static readonly SKFont HeaderFont = new(SKTypeface.Default, 10);
    private static readonly SKPaint TextPaint = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPaint GridPaint = new() { Color = GridColor, Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f };

    /// <summary>
    /// Rendert den PnL-Kalender. dailyPnl: Dictionary&lt;DateTime(Date), decimal(PnL)&gt;
    /// </summary>
    public static void Render(SKCanvas canvas, SKRect bounds, Dictionary<DateTime, decimal> dailyPnl, int weeksToShow = 8)
    {
        canvas.Clear(BgColor);
        if (dailyPnl.Count == 0)
        {
            DrawEmptyHint(canvas, bounds, "Noch keine abgeschlossenen Trades");
            return;
        }

        var headerH = 20f;
        var dayLabelW = 25f;
        var cellPad = 2f;
        var cellW = (bounds.Width - dayLabelW) / weeksToShow;
        var cellH = (bounds.Height - headerH) / 7f;
        cellW = Math.Min(cellW, cellH); // Quadratische Zellen
        cellH = cellW;

        // Max PnL für Farbskalierung
        var maxAbsPnl = dailyPnl.Values.Select(Math.Abs).DefaultIfEmpty(1m).Max();
        if (maxAbsPnl == 0) maxAbsPnl = 1m;

        // 24.04.2026 Phase-4-Audit m7: UTC statt lokal — die `dailyPnl`-Keys kommen aus
        // `trade.ExitTime.Date` (UTC, siehe DashboardViewModel.BuildDailyPnlSnapshot).
        // Vorher `DateTime.Today` (lokal) führte um Mitternacht in Europa/Berlin (UTC+1/+2)
        // zu einem Tages-Offset: ein Trade um 0:30 lokal (UTC 23:30 Vortag) landete im
        // Kalender auf dem falschen Tag. UTC-Konsistenz fixt das.
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-(weeksToShow * 7 - 1));
        // Auf Montag zurückrunden
        startDate = startDate.AddDays(-(int)startDate.DayOfWeek + 1);
        if (startDate.DayOfWeek == DayOfWeek.Sunday) startDate = startDate.AddDays(-6);

        // Wochentag-Labels (links)
        var dayNames = new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        for (int d = 0; d < 7; d++)
        {
            var y = headerH + d * cellH + cellH / 2 + 4;
            canvas.DrawText(dayNames[d], dayLabelW - 4, y, SKTextAlign.Right, DayFont, TextPaint);
        }

        // Kacheln zeichnen
        var current = startDate;
        for (int w = 0; w < weeksToShow; w++)
        {
            for (int d = 0; d < 7; d++)
            {
                var x = dayLabelW + w * cellW + cellPad;
                var y = headerH + d * cellH + cellPad;
                var cw = cellW - cellPad * 2;
                var ch = cellH - cellPad * 2;

                if (current > today)
                {
                    current = current.AddDays(1);
                    continue;
                }

                SKColor color;
                if (dailyPnl.TryGetValue(current, out var pnl))
                {
                    var intensity = (byte)(Math.Min(Math.Abs(pnl) / maxAbsPnl, 1m) * 200m + 40);
                    color = pnl >= 0
                        ? new SKColor(16, 185, 129, intensity) // Grün
                        : new SKColor(239, 68, 68, intensity);  // Rot
                }
                else
                {
                    color = EmptyColor;
                }

                using var cellPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
                canvas.DrawRoundRect(x, y, cw, ch, 3, 3, cellPaint);

                // Datum im Monatsanfang
                if (current.Day == 1 || (w == 0 && d == 0))
                {
                    canvas.DrawText(current.ToString("MMM"), x + cw / 2, headerH - 4, SKTextAlign.Center, HeaderFont, TextPaint);
                }

                // PnL-Wert in der Zelle (nur wenn Platz genug)
                if (dailyPnl.ContainsKey(current) && cw > 20)
                {
                    using var valPaint = new SKPaint { Color = SKColors.White.WithAlpha(180), IsAntialias = true };
                    canvas.DrawText($"{pnl:+0;-0}", x + cw / 2, y + ch / 2 + 3, SKTextAlign.Center, ValueFont, valPaint);
                }

                current = current.AddDays(1);
            }
        }
    }

    private static readonly SKPaint HintPaint = new() { Color = SKColor.Parse("#64748B"), IsAntialias = true };
    private static readonly SKFont HintFont = new(SKTypeface.Default, 13);

    private static void DrawEmptyHint(SKCanvas canvas, SKRect bounds, string text)
    {
        var x = bounds.MidX;
        var y = bounds.MidY;
        canvas.DrawText(text, x, y, SKTextAlign.Center, HintFont, HintPaint);
    }
}
