using BingXBot.Backtest.Portfolio;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Filters;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Backtest;

/// <summary>
/// GAP 4: BTC-Health-Positionsskalierung im <see cref="PortfolioBacktestEngine"/> (Spiegel von
/// <c>TradingServiceBase</c> Z.1260-1303). Bei Crypto: harter Block wenn <c>AllowLong/AllowShort</c>=false,
/// sonst Multiplikation der Positionsgroesse mit <c>PositionScale</c> (0.65..1.0). Flag
/// <see cref="BacktestSettings.EnableBtcHealthScale"/>=false → kein Effekt (Backward-Compat).
/// Fixtures sind manuell befuellt (kein HTTP/Avalonia); BTC-USDT D1/H4 erzeugen einen deterministischen Score.
/// </summary>
public class BtcHealthScaleTests
{
    private static BotSettings Settings(bool btcHealth)
    {
        var s = new BotSettings();
        s.Backtest.InitialBalance = 100_000m;
        s.Backtest.UseDynamicSlippage = false;
        s.Backtest.SimulateFundingRate = false;
        s.Backtest.SlippagePercent = 0m;
        s.Backtest.SpreadPercent = 0m;
        s.Backtest.MinRiskRewardRatio = 0m;
        s.Backtest.EnableScannerPrefilter = false;     // nur BTC-Health ist die zu testende Variable
        s.Backtest.EnableBtcHealthScale = btcHealth;

        s.Risk.MaxOpenPositions = 999;
        s.Risk.MaxOpenPositionsPerSymbol = 1;
        s.Risk.MaxTotalMarginPercent = 0m;
        s.Risk.MaxCorrelatedExposurePercent = 0m;
        s.Risk.MaxDailyLossPercent = 0m;
        s.Risk.MaxDailyDrawdownPercent = 0m;
        s.Risk.MaxTotalDrawdownPercent = 100m;
        s.Risk.MaxDailyRiskPercent = 0m;
        s.Risk.MinRiskRewardRatio = 0m;
        s.Risk.EnableLossStreakDampening = false;
        s.Risk.EnableEquityCurveScaling = false;
        s.Risk.EnableVolatilityTargeting = false;
        return s;
    }

    /// <summary>Monotone Kerzenreihe — stark steigend (dir=+1) oder stark fallend (dir=-1) ab <paramref name="startTime"/>.</summary>
    private static List<Candle> Monotone(int count, decimal startPrice, decimal stepAbs, int dir, TimeSpan barLen, DateTime startTime)
    {
        var list = new List<Candle>(count);
        var price = startPrice;
        for (int i = 0; i < count; i++)
        {
            var open = price;
            var close = Math.Max(price + dir * stepAbs, 0.5m);
            var high = Math.Max(open, close) + 0.2m;
            var low = Math.Max(Math.Min(open, close) - 0.2m, 0.1m);
            var openTime = startTime.AddTicks(barLen.Ticks * i);
            list.Add(new Candle(openTime, open, high, low, close, 50_000m, openTime.Add(barLen)));
            price = close;
        }
        return list;
    }

    /// <summary>BTC-D1/H4-Fixture, die 130 Tage VOR der Universe-Timeline beginnt, damit zum ersten Alt-Trade
    /// bereits &gt;= 55 D1-Kerzen vorliegen (die Engine laedt BTC ab <c>from.AddDays(-120)</c>).</summary>
    private static readonly DateTime BtcStart = PortfolioCandleGen.Start.AddDays(-130);

    /// <summary>Verifiziert per MarketFilter direkt, dass die BTC-Fixture wirklich den erwarteten Score liefert.</summary>
    private static BtcHealthResult HealthOf(List<Candle> d1, List<Candle> h4) =>
        MarketFilter.CalculateBtcHealth(d1, h4, 0m);

    /// <summary>
    /// BTC stark bullish (Score +4) → AllowShort=false. Ein Short-Signal auf dem Test-Symbol wird hart geblockt.
    /// </summary>
    [Fact]
    public async Task BtcBullish_BlocksShort_WhenScaleOn()
    {
        // BTC stark steigend → D1>EMA50, H4-Supertrend↑, RSI>55, Funding neutral → Score +4 → AllowShort=false.
        var btcD1 = Monotone(400, 20_000m, 80m, +1, TimeSpan.FromDays(1), BtcStart);
        var btcH4 = Monotone(2100, 20_000m, 14m, +1, TimeSpan.FromHours(4), BtcStart);
        HealthOf(btcD1, btcH4).AllowShort.Should().BeFalse("Fixture-Sanity: starker Bull → Short verboten");

        // Test-Symbol: fallende Kerzen → AlwaysShort liefert dauerhaft Short-Signale.
        var altH4 = Monotone(320, 100m, 0.4m, -1, TimeSpan.FromHours(4), PortfolioCandleGen.Start);
        var client = new TfAwarePublicClient(new()
        {
            [("ALT-USDT", TimeFrame.H4)] = altH4,
            [("BTC-USDT", TimeFrame.D1)] = btcD1,
            [("BTC-USDT", TimeFrame.H4)] = btcH4,
        });
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var from = PortfolioCandleGen.Start;
        var to = PortfolioCandleGen.Start.AddDays(200);

        var reportOff = await engine.RunAsync(["ALT-USDT"], TimeFrame.H4, from, to, Settings(btcHealth: false),
            strategyFactory: _ => new AlwaysShortStrategy());
        var reportOn = await engine.RunAsync(["ALT-USDT"], TimeFrame.H4, from, to, Settings(btcHealth: true),
            strategyFactory: _ => new AlwaysShortStrategy());

        reportOff.TotalTrades.Should().BeGreaterThan(0, "ohne BTC-Health werden die Short-Signale normal getradet");
        reportOn.TotalTrades.Should().Be(0, "BTC-Health AllowShort=false blockt jeden Short (GAP 4)");
    }

    /// <summary>
    /// BTC bearish (Funding=0 begrenzt den Score auf min. -2: 3 Baer-Indikatoren −3 + neutrales Funding +1)
    /// → AllowShort=true, PositionScale=0.85. Eine erlaubte Short-Position wird mit 0.85 skaliert
    /// → Qty(on) ≈ 0.85 × Qty(off). (Score -4 ist mit Funding=0 nicht erreichbar.)
    /// </summary>
    [Fact]
    public async Task BtcBearish_ScalesShortPosition()
    {
        var btcD1 = Monotone(400, 60_000m, 80m, -1, TimeSpan.FromDays(1), BtcStart);
        var btcH4 = Monotone(2100, 60_000m, 14m, -1, TimeSpan.FromHours(4), BtcStart);
        var health = HealthOf(btcD1, btcH4);
        health.Score.Should().Be(-2, "3 Baer-Indikatoren (−3) + neutrales Funding (+1) = −2");
        health.AllowShort.Should().BeTrue("Fixture-Sanity: Baer → Short erlaubt");
        health.PositionScale.Should().Be(0.85m, "Score −2 → PositionScale 0.85");

        // Test-Symbol fallend → AlwaysShort liefert Shorts. ConfluenceScore=5 → SK-Faktor 1.0 (kein Extra-Effekt).
        var altH4 = Monotone(120, 100m, 0.4m, -1, TimeSpan.FromHours(4), PortfolioCandleGen.Start);
        var client = new TfAwarePublicClient(new()
        {
            [("ALT-USDT", TimeFrame.H4)] = altH4,
            [("BTC-USDT", TimeFrame.D1)] = btcD1,
            [("BTC-USDT", TimeFrame.H4)] = btcH4,
        });
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var from = PortfolioCandleGen.Start;
        var to = PortfolioCandleGen.Start.AddDays(60);

        var reportOff = await engine.RunAsync(["ALT-USDT"], TimeFrame.H4, from, to, Settings(btcHealth: false),
            strategyFactory: _ => new AlwaysShortStrategy());
        var reportOn = await engine.RunAsync(["ALT-USDT"], TimeFrame.H4, from, to, Settings(btcHealth: true),
            strategyFactory: _ => new AlwaysShortStrategy());

        reportOff.TotalTrades.Should().BeGreaterThan(0);
        reportOn.TotalTrades.Should().BeGreaterThan(0, "Short ist bei AllowShort=true erlaubt");

        // Erste Position beider Laeufe vergleichen (gleiche Entry-Kerze, identische Basis-Sizing).
        var qtyOff = reportOff.Trades[0].Quantity;
        var qtyOn = reportOn.Trades[0].Quantity;
        qtyOn.Should().BeApproximately(qtyOff * 0.85m, qtyOff * 0.001m,
            "BTC-Health PositionScale 0.85 skaliert die Short-Position (GAP 4)");
    }

    /// <summary>
    /// TradFi (NC-Prefix) ist von BTC entkoppelt — die BTC-PositionScale (0.85 bei diesem Baer-BTC) wirkt
    /// NICHT auf TradFi. Die TradFi-Long-Menge bleibt mit Flag an exakt gleich wie mit Flag aus.
    /// </summary>
    [Fact]
    public async Task TradFiSymbol_IsNotScaled_ByBtcHealth()
    {
        var btcD1 = Monotone(400, 60_000m, 80m, -1, TimeSpan.FromDays(1), BtcStart);
        var btcH4 = Monotone(2100, 60_000m, 14m, -1, TimeSpan.FromHours(4), BtcStart);
        // BTC-Baer → fuer Crypto wuerde PositionScale 0.85 greifen; TradFi muss davon unberuehrt bleiben.
        HealthOf(btcD1, btcH4).PositionScale.Should().Be(0.85m, "Fixture-Sanity: Baer-BTC liefert PositionScale 0.85");

        // TradFi-Symbol (NC-Prefix) mit steigenden Kerzen → AlwaysLong. Darf NICHT skaliert werden.
        var tradFi = Monotone(120, 100m, 0.4m, +1, TimeSpan.FromHours(4), PortfolioCandleGen.Start);
        var client = new TfAwarePublicClient(new()
        {
            [("NCSISPX500-USDT", TimeFrame.H4)] = tradFi,
            [("BTC-USDT", TimeFrame.D1)] = btcD1,
            [("BTC-USDT", TimeFrame.H4)] = btcH4,
        });
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var from = PortfolioCandleGen.Start;
        var to = PortfolioCandleGen.Start.AddDays(60);

        var reportOff = await engine.RunAsync(["NCSISPX500-USDT"], TimeFrame.H4, from, to, Settings(btcHealth: false),
            strategyFactory: _ => new AlwaysLongStrategy());
        var reportOn = await engine.RunAsync(["NCSISPX500-USDT"], TimeFrame.H4, from, to, Settings(btcHealth: true),
            strategyFactory: _ => new AlwaysLongStrategy());

        reportOff.TotalTrades.Should().BeGreaterThan(0);
        reportOn.TotalTrades.Should().Be(reportOff.TotalTrades, "TradFi ist von BTC-Health entkoppelt — kein Block/keine Skalierung");
        reportOn.Trades[0].Quantity.Should().Be(reportOff.Trades[0].Quantity, "TradFi-Position wird NICHT mit PositionScale skaliert");
    }
}

/// <summary>Test-Strategie: Short auf jeder Kerze. SL eng ueber Entry, TP weit darunter (aber positiv).</summary>
internal sealed class AlwaysShortStrategy(decimal slPct = 0.01m, decimal tpPct = 0.5m) : IStrategy
{
    public string Name => "AlwaysShort";
    public string Description => "Short auf jeder Kerze.";
    public IReadOnlyList<StrategyParameter> Parameters => [];
    public bool RequiresHigherTimeframeContext => false;

    public SignalResult Evaluate(MarketContext context)
    {
        if (context.Candles.Count == 0) return new(Signal.None, 0m, null, null, null, "");
        var price = context.Candles[^1].Close;
        // ConfluenceScore=5 → SK-Score-Faktor 1.0 (isoliert den BTC-PositionScale-Effekt).
        return new SignalResult(Signal.Short, 5m, price, price * (1m + slPct), price * (1m - tpPct),
            "AlwaysShort", ConfluenceScore: 5);
    }

    public void WarmUp(IReadOnlyList<Candle> history) { }
    public void Reset() { }
    public IStrategy Clone() => new AlwaysShortStrategy(slPct, tpPct);
}

/// <summary>
/// Fake-PublicClient mit (Symbol, TF)-Aufloesung — liefert BTC-D1/H4 separat von den Universe-H4-Kerzen.
/// Kein Netz, deterministisch.
/// </summary>
internal sealed class TfAwarePublicClient(Dictionary<(string Symbol, TimeFrame Tf), List<Candle>> data)
    : IPublicMarketDataClient
{
    public Task<List<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, DateTime from, DateTime to, CancellationToken ct = default)
        => Task.FromResult(data.TryGetValue((symbol, tf), out var c) ? new List<Candle>(c) : []);

    public Task<List<Ticker>> GetAllTickersAsync(CancellationToken ct = default) => Task.FromResult(new List<Ticker>());
    public Task<List<string>> GetAllSymbolsAsync(CancellationToken ct = default)
        => Task.FromResult(data.Keys.Select(k => k.Symbol).Distinct().ToList());
    public Task<DateTime> GetServerTimeAsync(CancellationToken ct = default) => Task.FromResult(DateTime.UtcNow);
}
