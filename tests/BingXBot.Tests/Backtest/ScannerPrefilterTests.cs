using BingXBot.Backtest.Portfolio;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Backtest;

/// <summary>
/// GAP 11: Der Live-Scanner-Vorfilter (MinVolume24h + MinPriceChange pro Nav-TF + TradFi-Marktstunden +
/// Crypto-Session-Bitmask) blockt im <see cref="PortfolioBacktestEngine"/> die Entries fuer Symbole/Zeitschritte,
/// die der Live-Bot ebenfalls nie scannen wuerde. Flag <see cref="BacktestSettings.EnableScannerPrefilter"/>=false
/// laesst alles durch (Backward-Compat), =true filtert. Die Tests fahren End-to-End ueber RunAsync mit manuell
/// befuellten Kerzen-Fixtures (kein HTTP/Avalonia).
/// </summary>
public class ScannerPrefilterTests
{
    /// <summary>Basis-Settings: alle konto-weiten Gates aus, nur der Scanner-Vorfilter ist die zu testende Variable.</summary>
    private static BotSettings Settings(bool prefilter)
    {
        var s = new BotSettings();
        s.Backtest.InitialBalance = 100_000m;
        s.Backtest.UseDynamicSlippage = false;
        s.Backtest.SimulateFundingRate = false;
        s.Backtest.SlippagePercent = 0m;
        s.Backtest.SpreadPercent = 0m;
        s.Backtest.MinRiskRewardRatio = 0m;
        s.Backtest.EnableScannerPrefilter = prefilter;
        s.Backtest.EnableBtcHealthScale = false;

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

    /// <summary>
    /// Steigende H4-Kerzen mit konfigurierbarem Basis-Volumen — daraus baut der Scan-Ticker das Quote-Volumen
    /// (Σ Volumen×Close der letzten 6 Kerzen). Hohes Volumen → liegt ueber MinVolume24h, niedriges → darunter.
    /// </summary>
    private static List<Candle> Candles(int count, decimal startPrice, decimal stepPerCandle, decimal baseVolume)
    {
        var list = new List<Candle>(count);
        var price = startPrice;
        for (int i = 0; i < count; i++)
        {
            var open = price;
            var close = price + stepPerCandle;
            var high = Math.Max(open, close) + 0.5m;
            var low = Math.Max(Math.Min(open, close) - 0.5m, 0.01m);
            var openTime = PortfolioCandleGen.Start.AddHours(4 * i);
            list.Add(new Candle(openTime, open, high, low, close, baseVolume, openTime.AddHours(4)));
            price = Math.Max(close, 0.01m);
        }
        return list;
    }

    [Fact]
    public async Task MinVolume_BlocksLowVolumeSymbol_WhenPrefilterOn()
    {
        // Preis ~100, Basis-Volumen 1 → Quote-Vol ~6×100×1 = ~600 USDT, weit unter MinVolume24hByTf[H4]=10M.
        var map = new Dictionary<string, List<Candle>> { ["LOWVOL-USDT"] = Candles(120, 100m, 0.5m, 1m) };
        var client = new FakePublicClient(map);
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var reportOff = await engine.RunAsync(["LOWVOL-USDT"], TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(60), Settings(prefilter: false),
            strategyFactory: _ => new AlwaysLongStrategy());
        var reportOn = await engine.RunAsync(["LOWVOL-USDT"], TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(60), Settings(prefilter: true),
            strategyFactory: _ => new AlwaysLongStrategy());

        reportOff.TotalTrades.Should().BeGreaterThan(0, "ohne Vorfilter wird das Low-Vol-Symbol normal getradet");
        reportOn.TotalTrades.Should().Be(0, "der MinVolume24h-Vorfilter blockt das illiquide Symbol komplett (GAP 11)");
    }

    [Fact]
    public async Task HighVolume_PassesFilter_AndTrades()
    {
        // Preis ~100, Basis-Volumen 50000 → Quote-Vol ~6×100×50000 = 30M, ueber MinVolume24hByTf[H4]=10M.
        var map = new Dictionary<string, List<Candle>> { ["HIVOL-USDT"] = Candles(120, 100m, 0.5m, 50_000m) };
        var client = new FakePublicClient(map);
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var reportOn = await engine.RunAsync(["HIVOL-USDT"], TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(60), Settings(prefilter: true),
            strategyFactory: _ => new AlwaysLongStrategy());

        reportOn.TotalTrades.Should().BeGreaterThan(0, "liquides Symbol passiert den Vorfilter und tradet (GAP 11)");
    }

    [Fact]
    public async Task MinPriceChange_BlocksFlatSymbol_WhenPrefilterOn()
    {
        // Flache Kerzen (stepPerCandle=0 → PriceChange ~0%), aber hohes Volumen. MinPriceChangeByTf[H4]=0.3%
        // → der Momentum-Filter blockt, obwohl Volumen passt.
        var map = new Dictionary<string, List<Candle>> { ["FLAT-USDT"] = Candles(120, 100m, 0m, 50_000m) };
        var client = new FakePublicClient(map);
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var reportOn = await engine.RunAsync(["FLAT-USDT"], TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(60), Settings(prefilter: true),
            strategyFactory: _ => new AlwaysLongStrategy());

        reportOn.TotalTrades.Should().Be(0, "PriceChange ~0% < MinPriceChangeByTf[H4]=0.3% blockt das flache Symbol (GAP 11)");
    }

    [Fact]
    public async Task SessionFilter_BlocksAllEntries_WhenNoSessionAllowed()
    {
        // EnabledSessions=None → IsSessionAllowed gibt fuer JEDEN Zeitschritt false → kein einziger Entry.
        var map = new Dictionary<string, List<Candle>> { ["HIVOL-USDT"] = Candles(120, 100m, 0.5m, 50_000m) };
        var client = new FakePublicClient(map);
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var settings = Settings(prefilter: true);
        settings.EnabledSessions = TradingSessions.None;

        var report = await engine.RunAsync(["HIVOL-USDT"], TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(60), settings,
            strategyFactory: _ => new AlwaysLongStrategy());

        report.TotalTrades.Should().Be(0, "EnabledSessions=None blockt jeden Entry-Zeitschritt (GAP 11)");
    }

    [Fact]
    public async Task PrefilterOff_DoesNotBlock_AnyStep()
    {
        // Backward-Compat: Flag aus → Low-Vol + flat wird gehandelt wie ohne den ganzen Vorfilter.
        var map = new Dictionary<string, List<Candle>> { ["LOWVOL-USDT"] = Candles(120, 100m, 0m, 1m) };
        var client = new FakePublicClient(map);
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var report = await engine.RunAsync(["LOWVOL-USDT"], TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(60), Settings(prefilter: false),
            strategyFactory: _ => new AlwaysLongStrategy());

        report.TotalTrades.Should().BeGreaterThan(0, "bei abgeschaltetem Vorfilter greift kein Volume/Change/Session-Block");
    }
}
