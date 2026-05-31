using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Verhaltens-Tests fuer die TrendFollow-Strategie. Verifiziert die SL/TP-Geometrie (Long: SL &lt; Entry &lt; TP,
/// Short: TP &lt; Entry &lt; SL), den Market-Entry (PreferLimitOrder=false, EntryPrice=Close), den ECHTEN
/// Donchian-Breakout-Crossover (Fix G) und dass ohne Trend/Breakout kein Signal kommt.
/// </summary>
public class TrendFollowStrategyTests
{
    private static MarketContext Ctx(IReadOnlyList<Candle> candles, IReadOnlyList<Position>? open = null)
    {
        var last = candles[^1];
        var ticker = new Ticker("TEST-USDT", last.Close, last.Close, last.Close, 1000m, 0m, last.CloseTime);
        return new MarketContext("TEST-USDT", candles, ticker, open ?? [], new AccountInfo(1000m, 1000m, 0m, 0m),
            NavigatorTimeframe: TimeFrame.H4);
    }

    /// <summary>
    /// Baut ein echtes Breakout-Setup: starker Trend (Donchian-Hoch/Tief etabliert), kurze Konsolidierung
    /// INNERHALB des Kanals, dann eine Ausbruch-Kerze, die ueber/unter den Kanal schliesst. So feuert der
    /// echte Crossover (Vorkerze noch im Kanal, aktuelle bricht aus) — anders als ein Dauer-Trend.
    /// </summary>
    private static List<Candle> BreakoutSetup(bool up, int trendBars = 80, int consolidationBars = 4)
    {
        var list = new List<Candle>();
        var t = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        decimal p = 100m;
        int idx = 0;
        decimal step = up ? 0.9m : -0.9m;

        // Phase 1: starker, gleichmaessiger Trend (baut EMA-Lage, DMI-Richtung, ADX, Donchian-Extrem).
        for (int i = 0; i < trendBars; i++)
        {
            var open = p;
            p += step;
            var close = p;
            var hi = Math.Max(open, close) + 0.15m;
            var lo = Math.Min(open, close) - 0.15m;
            list.Add(new Candle(t.AddHours(4 * idx), open, hi, lo, close, 1200m, t.AddHours(4 * (idx + 1))));
            idx++;
        }
        var extreme = p; // letztes Trend-Extrem (Hoch bei up, Tief bei down)

        // Phase 2: kurze Konsolidierung INNERHALB des Kanals (Closes bleiben unter Hoch / ueber Tief).
        for (int i = 0; i < consolidationBars; i++)
        {
            var lvl = up ? extreme - 2.5m : extreme + 2.5m;
            var open = lvl;
            var close = lvl + (i % 2 == 0 ? 0.25m : -0.25m);
            var hi = Math.Max(open, close) + 0.2m;
            var lo = Math.Min(open, close) - 0.2m;
            list.Add(new Candle(t.AddHours(4 * idx), open, hi, lo, close, 1000m, t.AddHours(4 * (idx + 1))));
            idx++;
        }

        // Phase 3: Ausbruch-Kerze — Close deutlich ueber/unter dem Kanal-Extrem.
        {
            var open = up ? extreme - 1.5m : extreme + 1.5m;
            var close = up ? extreme + 3.5m : extreme - 3.5m;
            var hi = Math.Max(open, close) + 0.2m;
            var lo = Math.Min(open, close) - 0.2m;
            list.Add(new Candle(t.AddHours(4 * idx), open, hi, lo, close, 2500m, t.AddHours(4 * (idx + 1))));
        }
        return list;
    }

    [Fact]
    public void Evaluate_UptrendBreakout_ShouldEmitLongWithValidGeometry()
    {
        // adxMin:0 isoliert die Geometrie-/Crossover-Pruefung vom ADX-Schwellenwert (synthetische
        // Daten erzeugen keinen verlaesslichen ADX). Trend-Lage (close>EMA, +DI>-DI) + Breakout-Crossover
        // muessen trotzdem erfuellt sein.
        var strategy = new TrendFollowStrategy(adxMin: 0m);
        var signal = strategy.Evaluate(Ctx(BreakoutSetup(up: true)));

        signal.Signal.Should().Be(Signal.Long);
        signal.PreferLimitOrder.Should().BeFalse("TrendFollow nutzt Market-Entry (Backtest-treu)");
        signal.EntryPrice.Should().NotBeNull();
        signal.StopLoss.Should().BeLessThan(signal.EntryPrice!.Value);
        signal.TakeProfit.Should().BeGreaterThan(signal.EntryPrice!.Value);
        signal.TakeProfit2.Should().BeGreaterThan(signal.TakeProfit!.Value);

        var risk = signal.EntryPrice!.Value - signal.StopLoss!.Value;
        var reward = signal.TakeProfit!.Value - signal.EntryPrice!.Value;
        (reward / risk).Should().BeApproximately(1.5m, 0.01m, "TP1 = 1.5R");
    }

    [Fact]
    public void Evaluate_DowntrendBreakdown_ShouldEmitShortWithValidGeometry()
    {
        var strategy = new TrendFollowStrategy(adxMin: 0m);
        var signal = strategy.Evaluate(Ctx(BreakoutSetup(up: false)));

        signal.Signal.Should().Be(Signal.Short);
        signal.StopLoss.Should().BeGreaterThan(signal.EntryPrice!.Value);
        signal.TakeProfit.Should().BeLessThan(signal.EntryPrice!.Value);
        signal.TakeProfit2.Should().BeLessThan(signal.TakeProfit!.Value);
    }

    [Fact]
    public void Evaluate_ContinuousTrend_NoFreshBreakout_ShouldNotReEnter()
    {
        // Fix G: Ein Dauer-Trend ohne Konsolidierung hat keinen frischen Crossover auf der letzten Kerze
        // (Vorkerze war bereits ueber dem Kanal). Vorher feuerte das faelschlich bei JEDER Kerze.
        var list = new List<Candle>();
        var t = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        decimal p = 100m;
        for (int i = 0; i < 90; i++)
        {
            var open = p; p += 0.9m; var close = p;
            list.Add(new Candle(t.AddHours(4 * i), open, Math.Max(open, close) + 0.15m, Math.Min(open, close) - 0.15m, close, 1200m, t.AddHours(4 * (i + 1))));
        }
        new TrendFollowStrategy(adxMin: 0m).Evaluate(Ctx(list)).Signal.Should().Be(Signal.None);
    }

    [Fact]
    public void Evaluate_OpenPositionOnSymbol_ShouldNotEmit()
    {
        var candles = BreakoutSetup(up: true);
        var last = candles[^1];
        var openPos = new Position("TEST-USDT", Side.Buy, last.Close, last.Close, 1m, 0m, 5m, MarginType.Isolated, last.CloseTime);
        new TrendFollowStrategy(adxMin: 0m).Evaluate(Ctx(candles, [openPos])).Signal.Should().Be(Signal.None);
    }

    [Fact]
    public void Evaluate_InsufficientData_ShouldNotEmit()
    {
        var candles = BreakoutSetup(up: true, trendBars: 15, consolidationBars: 2);
        new TrendFollowStrategy().Evaluate(Ctx(candles)).Signal.Should().Be(Signal.None);
    }
}
