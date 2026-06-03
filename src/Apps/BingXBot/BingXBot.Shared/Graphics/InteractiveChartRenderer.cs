using SkiaSharp;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Graphics;

/// <summary>
/// Interaktiver Candlestick-Chart mit Indikatoren, Regime-Hintergrund,
/// Crosshair, Zoom/Pan, Trade-Markers und SL/TP-Overlay.
/// Instanz-Klasse mit ChartState (ersetzt den statischen BtcPriceChartRenderer).
/// </summary>
public class InteractiveChartRenderer
{
    // Farben
    private static readonly SKColor BgColor = SKColor.Parse("#1E1E2E");
    private static readonly SKColor GridColor = SKColor.Parse("#3F3F5C");
    private static readonly SKColor TextColor = SKColor.Parse("#94A3B8");
    private static readonly SKColor BullColor = SKColor.Parse("#10B981");
    private static readonly SKColor BearColor = SKColor.Parse("#EF4444");
    private static readonly SKColor CrosshairColor = SKColor.Parse("#6B7280");

    // Indikator-Farben
    private static readonly SKColor Ema50Color = SKColor.Parse("#F59E0B");     // Amber
    private static readonly SKColor Ema200Color = SKColor.Parse("#8B5CF6");    // Lila
    private static readonly SKColor BbUpperColor = SKColor.Parse("#6366F1");   // Indigo
    private static readonly SKColor BbLowerColor = SKColor.Parse("#6366F1");
    private static readonly SKColor BbFillColor = SKColor.Parse("#6366F1").WithAlpha(15);
    private static readonly SKColor SupertrendBullColor = SKColor.Parse("#10B981").WithAlpha(180);
    private static readonly SKColor SupertrendBearColor = SKColor.Parse("#EF4444").WithAlpha(180);

    // Regime-Hintergrund-Farben (sehr dezent)
    private static readonly SKColor RegimeBullBg = SKColor.Parse("#10B981").WithAlpha(10);
    private static readonly SKColor RegimeBearBg = SKColor.Parse("#EF4444").WithAlpha(10);
    private static readonly SKColor RegimeRangeBg = SKColor.Parse("#3B82F6").WithAlpha(8);
    private static readonly SKColor RegimeChaoticBg = SKColor.Parse("#F59E0B").WithAlpha(12);

    // Trade-Marker-Farben
    private static readonly SKColor LongEntryColor = SKColor.Parse("#10B981");
    private static readonly SKColor ShortEntryColor = SKColor.Parse("#EF4444");
    private static readonly SKColor ExitWinColor = SKColor.Parse("#FFD700");
    private static readonly SKColor ExitLossColor = SKColor.Parse("#FF6B6B");
    private static readonly SKColor SlColor = SKColor.Parse("#EF4444");
    private static readonly SKColor TpColor = SKColor.Parse("#10B981");
    private static readonly SKColor Tp2Color = SKColor.Parse("#3B82F6");
    private static readonly SKColor EntryColor = SKColor.Parse("#F59E0B");

    // Gecachte Fonts
    private static readonly SKFont LabelFont = new(SKTypeface.Default, 10);
    private static readonly SKFont SmallFont = new(SKTypeface.Default, 9);
    private static readonly SKFont EmptyFont = new(SKTypeface.Default, 14);
    private static readonly SKFont CrosshairFont = new(SKTypeface.Default, 11);
    private static readonly SKFont TooltipFont = new(SKTypeface.Default, 10);

    // Gecachte Paints (statisch, Thread-safe da nur gelesen)
    private static readonly SKPaint BullWick = new() { Color = BullColor, StrokeWidth = 1f, IsAntialias = true };
    private static readonly SKPaint BearWick = new() { Color = BearColor, StrokeWidth = 1f, IsAntialias = true };
    private static readonly SKPaint BullBody = new() { Color = BullColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
    private static readonly SKPaint BearBody = new() { Color = BearColor, Style = SKPaintStyle.Fill, StrokeWidth = 1f, IsAntialias = true };
    private static readonly SKPaint BullVol = new() { Color = BullColor.WithAlpha(80), Style = SKPaintStyle.Fill };
    private static readonly SKPaint BearVol = new() { Color = BearColor.WithAlpha(80), Style = SKPaintStyle.Fill };
    private static readonly SKPaint GridPaint = new() { Color = GridColor, StrokeWidth = 0.5f };
    private static readonly SKPaint TextPaint = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPaint EmptyText = new() { Color = TextColor, IsAntialias = true };
    private static readonly SKPathEffect DashEffect = SKPathEffect.CreateDash([4f, 4f], 0);
    private static readonly SKPathEffect SlTpDash = SKPathEffect.CreateDash([6f, 4f], 0);
    private static readonly SKPaint CrosshairPaint = new() { Color = CrosshairColor, StrokeWidth = 0.5f, PathEffect = SKPathEffect.CreateDash([3f, 3f], 0) };
    private static readonly SKPaint CrosshairBgPaint = new() { Color = SKColor.Parse("#2D2D44"), Style = SKPaintStyle.Fill };
    private static readonly SKPaint CrosshairTextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private static readonly SKPaint TooltipBgPaint = new() { Color = SKColor.Parse("#1E1E2E").WithAlpha(230), Style = SKPaintStyle.Fill };
    private static readonly SKPaint TooltipBorderPaint = new() { Color = GridColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };

    // Indikator-Paints
    private static readonly SKPaint Ema50Paint = new() { Color = Ema50Color, StrokeWidth = 1.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint Ema200Paint = new() { Color = Ema200Color, StrokeWidth = 1.5f, IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint BbUpperPaint = new() { Color = BbUpperColor.WithAlpha(120), StrokeWidth = 1f, IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint BbLowerPaint = new() { Color = BbLowerColor.WithAlpha(120), StrokeWidth = 1f, IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint BbFillPaint = new() { Color = BbFillColor, Style = SKPaintStyle.Fill };
    private static readonly SKPaint StBullPaint = new() { Color = SupertrendBullColor, StrokeWidth = 2f, IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint StBearPaint = new() { Color = SupertrendBearColor, StrokeWidth = 2f, IsAntialias = true, Style = SKPaintStyle.Stroke };

    // SL/TP-Overlay Paints
    private static readonly SKPaint SlLine = new() { Color = SlColor.WithAlpha(150), StrokeWidth = 1f, PathEffect = SlTpDash, IsAntialias = true };
    private static readonly SKPaint TpLine = new() { Color = TpColor.WithAlpha(150), StrokeWidth = 1f, PathEffect = SlTpDash, IsAntialias = true };
    private static readonly SKPaint Tp2Line = new() { Color = Tp2Color.WithAlpha(120), StrokeWidth = 1f, PathEffect = SlTpDash, IsAntialias = true };
    private static readonly SKPaint EntryLine = new() { Color = EntryColor.WithAlpha(120), StrokeWidth = 1f, PathEffect = SlTpDash, IsAntialias = true };
    private static readonly SKPaint SlLabel = new() { Color = SlColor, IsAntialias = true };
    private static readonly SKPaint TpLabel = new() { Color = TpColor, IsAntialias = true };
    private static readonly SKPaint Tp2Label = new() { Color = Tp2Color, IsAntialias = true };
    private static readonly SKPaint EntryLabel = new() { Color = EntryColor, IsAntialias = true };

    private static readonly SKFont PointFont = new(SKTypeface.Default, 11);

    // 04.05.2026 — Per-Frame-Allokationen aus Render-Methoden ausgelagert.
    // Reduziert GC-Pressure auf Android-Mid-Tier + Pi (~150 SKPaint-Allokationen pro Frame entfernt).
    // Color-variable Paints werden vor Verwendung mit .Color = ... aktualisiert (UI-Thread, kein Race).
    private static readonly SKPaint PriceLinePaint = new() { StrokeWidth = 1f, PathEffect = DashEffect };
    private static readonly SKPaint PriceLabelPaint = new() { IsAntialias = true };
    private static readonly SKPaint ProfitZonePaint = new() { Color = TpColor.WithAlpha(15), Style = SKPaintStyle.Fill };
    private static readonly SKPaint LossZonePaint = new() { Color = SlColor.WithAlpha(15), Style = SKPaintStyle.Fill };
    private static readonly SKPaint MarkerCircleFillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private static readonly SKPaint MarkerCircleBorderPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
    private static readonly SKPaint MarkerTextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private static readonly SKPaint MarkerPricePaint = new() { IsAntialias = true };
    private static readonly SKPaint TradeMarkerFillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };

    public ChartState State { get; } = new();

    // Berechnete Layout-Bereiche (für Mouse-Event-Handling im View)
    public SKRect PriceArea { get; private set; }
    public float CandleWidth { get; private set; }
    public decimal MinPrice { get; private set; }
    public decimal MaxPrice { get; private set; }

    /// <summary>Hauptrender-Methode mit allen Layern.</summary>
    public void Render(SKCanvas canvas, SKRect bounds, IReadOnlyList<Candle> candles,
        IReadOnlyList<TradeMarker>? markers = null,
        ActivePositionOverlay? overlay = null,
        ChartIndicatorData? indicators = null)
    {
        canvas.Clear(BgColor);

        if (candles.Count < 2)
        {
            canvas.DrawText("Lade Daten...", bounds.MidX, bounds.MidY, SKTextAlign.Center, EmptyFont, EmptyText);
            return;
        }

        // Viewport initialisieren wenn nötig
        if (State.ViewEnd == 0) State.ResetViewport(candles.Count);
        State.ViewEnd = Math.Min(State.ViewEnd, candles.Count);
        State.ViewStart = Math.Max(0, State.ViewStart);
        if (State.ViewEnd <= State.ViewStart) State.ResetViewport(candles.Count);

        // Sichtbare Candles
        var visStart = State.ViewStart;
        var visEnd = State.ViewEnd;
        var visCount = visEnd - visStart;
        if (visCount < 2) return;

        // Layout
        var padding = new SKRect(55, 15, 60, 30); // Rechts mehr Platz für Labels
        var chartArea = new SKRect(
            bounds.Left + padding.Left, bounds.Top + padding.Top,
            bounds.Right - padding.Right, bounds.Bottom - padding.Bottom);
        var priceArea = new SKRect(chartArea.Left, chartArea.Top, chartArea.Right, chartArea.Top + chartArea.Height * 0.75f);
        var volumeArea = new SKRect(chartArea.Left, priceArea.Bottom + 5, chartArea.Right, chartArea.Bottom);

        // Min/Max für sichtbaren Bereich
        decimal minP = decimal.MaxValue, maxP = decimal.MinValue, maxVol = 0;
        for (int i = visStart; i < visEnd; i++)
        {
            if (candles[i].Low < minP) minP = candles[i].Low;
            if (candles[i].High > maxP) maxP = candles[i].High;
            if (candles[i].Volume > maxVol) maxVol = candles[i].Volume;
        }
        var range = maxP - minP;
        minP -= range * 0.03m;
        maxP += range * 0.03m;

        // Layout-Werte speichern (für Mouse-Events)
        PriceArea = priceArea;
        CandleWidth = priceArea.Width / visCount;
        MinPrice = minP;
        MaxPrice = maxP;

        // Layer 2: Grid
        DrawPriceGrid(canvas, priceArea, minP, maxP);
        DrawTimeLabels(canvas, chartArea, candles, visStart, visEnd);

        // Layer 3: Bollinger Bands (unter den Candles)
        if (State.ShowBollingerBands && indicators?.BollingerUpper != null)
            DrawBollingerBands(canvas, priceArea, minP, maxP, visStart, visEnd, indicators);

        // Layer 4: Candlesticks + Volumen
        var bodyWidth = Math.Max(CandleWidth * 0.6f, 1f);
        for (int i = visStart; i < visEnd; i++)
        {
            var c = candles[i];
            var idx = i - visStart;
            var x = priceArea.Left + CandleWidth * idx + CandleWidth / 2;
            var bull = c.Close >= c.Open;

            var highY = MapY(c.High, priceArea, minP, maxP);
            var lowY = MapY(c.Low, priceArea, minP, maxP);
            canvas.DrawLine(x, highY, x, lowY, bull ? BullWick : BearWick);

            var openY = MapY(c.Open, priceArea, minP, maxP);
            var closeY = MapY(c.Close, priceArea, minP, maxP);
            var bodyTop = Math.Min(openY, closeY);
            var bodyH = Math.Max(Math.Abs(closeY - openY), 1f);
            canvas.DrawRect(x - bodyWidth / 2, bodyTop, bodyWidth, bodyH, bull ? BullBody : BearBody);

            if (maxVol > 0)
            {
                var volH = (float)(c.Volume / maxVol) * volumeArea.Height;
                canvas.DrawRect(x - bodyWidth / 2, volumeArea.Bottom - volH, bodyWidth, volH, bull ? BullVol : BearVol);
            }
        }

        // Layer 5: Indikatoren (über den Candles)
        if (indicators != null)
        {
            if (State.ShowEma50 && indicators.Ema50 != null)
                DrawIndicatorLine(canvas, priceArea, minP, maxP, visStart, visEnd, indicators.Ema50, Ema50Paint);
            if (State.ShowEma200 && indicators.Ema200 != null)
                DrawIndicatorLine(canvas, priceArea, minP, maxP, visStart, visEnd, indicators.Ema200, Ema200Paint);
            if (State.ShowSupertrend && indicators.SupertrendLine != null && indicators.SupertrendBullish != null)
                DrawSupertrend(canvas, priceArea, minP, maxP, visStart, visEnd, indicators.SupertrendLine, indicators.SupertrendBullish);
        }

        // Layer 6: Aktueller Preis
        var lastPrice = candles[visEnd - 1].Close;
        var lastY = MapY(lastPrice, priceArea, minP, maxP);
        var lastBull = candles[visEnd - 1].Close >= candles[visEnd - 1].Open;
        PriceLinePaint.Color = (lastBull ? BullColor : BearColor).WithAlpha(100);
        canvas.DrawLine(priceArea.Left, lastY, priceArea.Right, lastY, PriceLinePaint);
        PriceLabelPaint.Color = lastBull ? BullColor : BearColor;
        canvas.DrawText($"{lastPrice:F1}", priceArea.Right + 3, lastY + 4, LabelFont, PriceLabelPaint);

        // Layer 8: SL/TP-Overlay
        if (overlay != null)
            DrawPositionOverlay(canvas, priceArea, minP, maxP, overlay);

        // Layer 9: Trade-Markers
        if (markers is { Count: > 0 })
            DrawTradeMarkers(canvas, priceArea, minP, maxP, candles, visStart, visEnd, markers);

        // Layer 10: Crosshair (ganz oben)
        if (State.ShowCrosshair && State.CrosshairX >= priceArea.Left && State.CrosshairX <= priceArea.Right)
            DrawCrosshair(canvas, priceArea, volumeArea, minP, maxP, candles, visStart, visEnd);
    }

    // ═══ Indikator-Zeichenmethoden ═══

    private void DrawIndicatorLine(SKCanvas canvas, SKRect area, decimal min, decimal max,
        int visStart, int visEnd, IReadOnlyList<decimal?> values, SKPaint paint)
    {
        using var path = new SKPath();
        var started = false;
        for (int i = visStart; i < visEnd && i < values.Count; i++)
        {
            if (!values[i].HasValue) { started = false; continue; }
            var x = area.Left + CandleWidth * (i - visStart) + CandleWidth / 2;
            var y = MapY(values[i]!.Value, area, min, max);
            if (!started) { path.MoveTo(x, y); started = true; }
            else path.LineTo(x, y);
        }
        if (started) canvas.DrawPath(path, paint);
    }

    private void DrawSupertrend(SKCanvas canvas, SKRect area, decimal min, decimal max,
        int visStart, int visEnd, IReadOnlyList<decimal?> line, IReadOnlyList<bool?> bullish)
    {
        for (int i = visStart + 1; i < visEnd && i < line.Count; i++)
        {
            if (!line[i].HasValue || !line[i - 1].HasValue) continue;
            var x1 = area.Left + CandleWidth * (i - 1 - visStart) + CandleWidth / 2;
            var x2 = area.Left + CandleWidth * (i - visStart) + CandleWidth / 2;
            var y1 = MapY(line[i - 1]!.Value, area, min, max);
            var y2 = MapY(line[i]!.Value, area, min, max);
            var bull = bullish[i] ?? true;
            canvas.DrawLine(x1, y1, x2, y2, bull ? StBullPaint : StBearPaint);
        }
    }

    private void DrawBollingerBands(SKCanvas canvas, SKRect area, decimal min, decimal max,
        int visStart, int visEnd, ChartIndicatorData ind)
    {
        // Füll-Bereich zwischen Upper und Lower
        using var fillPath = new SKPath();
        var started = false;
        var upperPoints = new List<SKPoint>();

        for (int i = visStart; i < visEnd && i < (ind.BollingerUpper?.Count ?? 0); i++)
        {
            if (!ind.BollingerUpper![i].HasValue || !ind.BollingerLower![i].HasValue) continue;
            var x = area.Left + CandleWidth * (i - visStart) + CandleWidth / 2;
            var yU = MapY(ind.BollingerUpper[i]!.Value, area, min, max);
            var yL = MapY(ind.BollingerLower[i]!.Value, area, min, max);

            if (!started) { fillPath.MoveTo(x, yU); started = true; }
            else fillPath.LineTo(x, yU);
            upperPoints.Add(new SKPoint(x, yL));
        }
        // Unteren Rand rückwärts hinzufügen
        for (int i = upperPoints.Count - 1; i >= 0; i--)
            fillPath.LineTo(upperPoints[i].X, upperPoints[i].Y);
        fillPath.Close();
        canvas.DrawPath(fillPath, BbFillPaint);

        // Linien zeichnen
        DrawIndicatorLine(canvas, area, min, max, visStart, visEnd, ind.BollingerUpper!, BbUpperPaint);
        DrawIndicatorLine(canvas, area, min, max, visStart, visEnd, ind.BollingerLower!, BbLowerPaint);
    }

    // ═══ Crosshair + Tooltip ═══

    private void DrawCrosshair(SKCanvas canvas, SKRect priceArea, SKRect volArea, decimal min, decimal max,
        IReadOnlyList<Candle> candles, int visStart, int visEnd)
    {
        var x = State.CrosshairX;
        var y = State.CrosshairY;

        // Vertikale + horizontale Linie
        canvas.DrawLine(x, priceArea.Top, x, volArea.Bottom, CrosshairPaint);
        if (y >= priceArea.Top && y <= priceArea.Bottom)
            canvas.DrawLine(priceArea.Left, y, priceArea.Right, y, CrosshairPaint);

        // Preis-Label rechts
        if (y >= priceArea.Top && y <= priceArea.Bottom)
        {
            var price = MapPrice(y, priceArea, min, max);
            var txt = $"{price:F1}";
            var tw = CrosshairFont.MeasureText(txt, CrosshairTextPaint);
            var lx = priceArea.Right + 2;
            canvas.DrawRect(lx, y - 8, tw + 8, 16, CrosshairBgPaint);
            canvas.DrawText(txt, lx + 4, y + 4, CrosshairFont, CrosshairTextPaint);
        }

        // Candle-Tooltip
        var candleIdx = (int)((x - priceArea.Left) / CandleWidth) + visStart;
        if (candleIdx >= visStart && candleIdx < visEnd)
        {
            var c = candles[candleIdx];
            var tooltipX = Math.Min(x + 12, priceArea.Right - 140);
            var tooltipY = Math.Max(priceArea.Top, y - 80);

            var lines = new[]
            {
                c.OpenTime.ToLocalTime().ToString("dd.MM HH:mm"),
                $"O: {c.Open:F2}",
                $"H: {c.High:F2}",
                $"L: {c.Low:F2}",
                $"C: {c.Close:F2}",
                $"Vol: {c.Volume:F0}"
            };

            canvas.DrawRoundRect(tooltipX, tooltipY, 130, 90, 4, 4, TooltipBgPaint);
            canvas.DrawRoundRect(tooltipX, tooltipY, 130, 90, 4, 4, TooltipBorderPaint);

            for (int i = 0; i < lines.Length; i++)
                canvas.DrawText(lines[i], tooltipX + 8, tooltipY + 14 + i * 13, TooltipFont, CrosshairTextPaint);

            // Zeit-Label unten
            var timeLabel = c.OpenTime.ToLocalTime().ToString("HH:mm");
            var timeTw = SmallFont.MeasureText(timeLabel, CrosshairTextPaint);
            canvas.DrawRect(x - timeTw / 2 - 4, volArea.Bottom + 2, timeTw + 8, 14, CrosshairBgPaint);
            canvas.DrawText(timeLabel, x, volArea.Bottom + 13, SKTextAlign.Center, SmallFont, CrosshairTextPaint);
        }
    }

    // ═══ SL/TP-Overlay ═══

    private static void DrawPositionOverlay(SKCanvas canvas, SKRect area, decimal min, decimal max, ActivePositionOverlay ov)
    {
        DrawHLine(canvas, area, min, max, ov.EntryPrice, EntryLine, $"Entry {ov.EntryPrice:F1}", EntryLabel);
        if (ov.StopLoss.HasValue) DrawHLine(canvas, area, min, max, ov.StopLoss.Value, SlLine, $"SL {ov.StopLoss.Value:F1}", SlLabel);
        if (ov.TakeProfit.HasValue) DrawHLine(canvas, area, min, max, ov.TakeProfit.Value, TpLine, $"TP1 {ov.TakeProfit.Value:F1}", TpLabel);
        if (ov.TakeProfit2.HasValue) DrawHLine(canvas, area, min, max, ov.TakeProfit2.Value, Tp2Line, $"TP2 {ov.TakeProfit2.Value:F1}", Tp2Label);

        // Profit/Loss-Zonen
        if (ov.StopLoss.HasValue && ov.TakeProfit.HasValue)
        {
            var entryY = MapY(ov.EntryPrice, area, min, max);
            var slY = MapY(ov.StopLoss.Value, area, min, max);
            var tpY = MapY(ov.TakeProfit.Value, area, min, max);
            DrawZone(canvas, area, entryY, tpY, ProfitZonePaint);
            DrawZone(canvas, area, entryY, slY, LossZonePaint);
        }
    }

    // ═══ Position-Overlay ═══

    private static void DrawHLine(SKCanvas canvas, SKRect area, decimal min, decimal max,
        decimal price, SKPaint linePaint, string label, SKPaint labelPaint)
    {
        var y = MapY(price, area, min, max);
        if (y < area.Top || y > area.Bottom) return;
        canvas.DrawLine(area.Left, y, area.Right, y, linePaint);
        canvas.DrawText(label, area.Right + 3, y + 4, SmallFont, labelPaint);
    }

    private static void DrawZone(SKCanvas canvas, SKRect area, float y1, float y2, SKPaint paint)
    {
        var top = Math.Min(y1, y2);
        var bottom = Math.Max(y1, y2);
        canvas.DrawRect(area.Left, top, area.Width, bottom - top, paint);
    }

    // ═══ Trade-Markers ═══

    // 04.05.2026 — Wiederverwendbarer Path für Entry-Marker, vermeidet N×Allokation pro Frame.
    private static readonly SKPath SharedTradeMarkerPath = new();

    private void DrawTradeMarkers(SKCanvas canvas, SKRect area, decimal min, decimal max,
        IReadOnlyList<Candle> candles, int visStart, int visEnd, IReadOnlyList<TradeMarker> markers)
    {
        var firstTime = candles[visStart].OpenTime;
        var lastTime = candles[visEnd - 1].OpenTime;

        foreach (var m in markers)
        {
            if (m.Time < firstTime || m.Time > lastTime.AddHours(2)) continue;
            var idx = FindCandleIndex(candles, m.Time);
            if (idx < visStart || idx >= visEnd) continue;

            var x = area.Left + CandleWidth * (idx - visStart) + CandleWidth / 2;
            var y = MapY(m.Price, area, min, max);
            if (y < area.Top || y > area.Bottom) continue;

            var sz = 7f;
            if (m.IsEntry)
            {
                TradeMarkerFillPaint.Color = m.Side == Side.Buy ? LongEntryColor : ShortEntryColor;
                SharedTradeMarkerPath.Reset();
                if (m.Side == Side.Buy)
                { SharedTradeMarkerPath.MoveTo(x, y + sz + 4); SharedTradeMarkerPath.LineTo(x - sz, y + sz * 2 + 4); SharedTradeMarkerPath.LineTo(x + sz, y + sz * 2 + 4); }
                else
                { SharedTradeMarkerPath.MoveTo(x, y - sz - 4); SharedTradeMarkerPath.LineTo(x - sz, y - sz * 2 - 4); SharedTradeMarkerPath.LineTo(x + sz, y - sz * 2 - 4); }
                SharedTradeMarkerPath.Close();
                canvas.DrawPath(SharedTradeMarkerPath, TradeMarkerFillPaint);
            }
            else
            {
                TradeMarkerFillPaint.Color = m.Pnl >= 0 ? ExitWinColor : ExitLossColor;
                canvas.DrawCircle(x, y, sz * 0.7f, TradeMarkerFillPaint);
            }
        }
    }

    // ═══ Grid + Labels ═══

    private void DrawPriceGrid(SKCanvas canvas, SKRect area, decimal min, decimal max)
    {
        for (int i = 0; i <= 4; i++)
        {
            var y = area.Top + area.Height * i / 4f;
            canvas.DrawLine(area.Left, y, area.Right, y, GridPaint);
            var price = max - (max - min) * i / 4m;
            canvas.DrawText($"{price:F0}", area.Left - 5, y + 4, SKTextAlign.Right, LabelFont, TextPaint);
        }
    }

    private void DrawTimeLabels(SKCanvas canvas, SKRect area, IReadOnlyList<Candle> candles, int visStart, int visEnd)
    {
        var visCount = visEnd - visStart;
        var step = Math.Max(1, visCount / 6);
        for (int i = visStart; i < visEnd; i += step)
        {
            var x = area.Left + CandleWidth * (i - visStart) + CandleWidth / 2;
            var label = candles[i].OpenTime.ToLocalTime().ToString("HH:mm");
            canvas.DrawText(label, x, area.Bottom + 14, SKTextAlign.Center, LabelFont, TextPaint);
        }
    }

    // ═══ Hilfsmethoden ═══

    private static float MapY(decimal value, SKRect area, decimal min, decimal max)
    {
        var range = max - min;
        if (range == 0) return area.MidY;
        return area.Bottom - (float)((value - min) / range) * area.Height;
    }

    /// <summary>Berechnet den Preis für eine Y-Koordinate (für Crosshair + Drag).</summary>
    public decimal MapPrice(float y, SKRect area, decimal min, decimal max)
    {
        var range = max - min;
        if (area.Height == 0) return min;
        return max - (decimal)((y - area.Top) / area.Height) * range;
    }

    private static int FindCandleIndex(IReadOnlyList<Candle> candles, DateTime time)
    {
        int lo = 0, hi = candles.Count - 1;
        while (lo < hi) { var m = (lo + hi) / 2; if (candles[m].OpenTime < time) lo = m + 1; else hi = m; }
        return lo;
    }
}

/// <summary>Vorberechnete Indikator-Daten für den Chart.</summary>
public class ChartIndicatorData
{
    public IReadOnlyList<decimal?>? Ema50 { get; init; }
    public IReadOnlyList<decimal?>? Ema200 { get; init; }
    public IReadOnlyList<decimal?>? BollingerUpper { get; init; }
    public IReadOnlyList<decimal?>? BollingerLower { get; init; }
    public IReadOnlyList<decimal?>? SupertrendLine { get; init; }
    public IReadOnlyList<bool?>? SupertrendBullish { get; init; }
}
