using BingXBot.Backtest.Portfolio;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Backtest;

/// <summary>
/// Kern-Tests fuer GAP 1: der Portfolio-Backtest haelt das konto-weite <see cref="RiskSettings.MaxOpenPositions"/>
/// ein — egal wie viele Symbole gleichzeitig ein Entry-Signal liefern. Das war der Hauptfehler des alten
/// Lab-Pfads (pro Symbol eigene Engine → effektiv unbegrenzt viele gleichzeitig offene Positionen).
/// </summary>
public class PortfolioGatesTests
{
    private static BotSettings Settings(int maxOpen)
    {
        var s = new BotSettings();
        s.Backtest.InitialBalance = 100_000m;   // genug Margin, damit NUR der Positions-Gate limitiert
        s.Backtest.UseDynamicSlippage = false;
        s.Backtest.SimulateFundingRate = false;
        s.Backtest.SlippagePercent = 0m;
        s.Backtest.SpreadPercent = 0m;
        s.Backtest.MinRiskRewardRatio = 0m;

        s.Risk.MaxOpenPositions = maxOpen;
        s.Risk.MaxOpenPositionsPerSymbol = 1;
        s.Risk.MaxTotalMarginPercent = 0m;            // Margin-Gate aus
        s.Risk.MaxCorrelatedExposurePercent = 0m;     // Korrelations-Gate aus
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

    private static (FakePublicClient client, List<string> symbols) BuildUniverse(int symbolCount, int candleCount)
    {
        var map = new Dictionary<string, List<Candle>>();
        var symbols = new List<string>();
        for (int i = 0; i < symbolCount; i++)
        {
            // Verschiedene Crypto-Symbole (kein NC-Prefix) mit synchron schliessenden, steigenden Kerzen.
            var sym = $"SYM{i}-USDT";
            symbols.Add(sym);
            map[sym] = PortfolioCandleGen.Trending(candleCount, startPrice: 100m + i, stepPerCandle: 0.5m);
        }
        return (new FakePublicClient(map), symbols);
    }

    [Theory]
    [InlineData(8, 10)]   // 8 Symbole, Cap 10 → nie mehr als 8 (alle passen)
    [InlineData(20, 10)]  // 20 Symbole, Cap 10 → nie mehr als 10
    [InlineData(20, 3)]   // 20 Symbole, Cap 3 → nie mehr als 3
    public async Task Run_NeverExceedsMaxOpenPositions(int symbolCount, int maxOpen)
    {
        var (client, symbols) = BuildUniverse(symbolCount, candleCount: 120);
        var settings = Settings(maxOpen);
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var maxConcurrent = 0;
        await engine.RunAsync(symbols, TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(60), settings,
            strategyFactory: _ => new AlwaysLongStrategy(),
            onStepOpenPositions: c => maxConcurrent = Math.Max(maxConcurrent, c));

        maxConcurrent.Should().BeLessThanOrEqualTo(maxOpen,
            "der konto-weite MaxOpenPositions-Gate muss greifen (GAP 1)");
    }

    [Fact]
    public async Task Run_WithMaxOpenOne_NeverMoreThanOneOpen()
    {
        var (client, symbols) = BuildUniverse(symbolCount: 12, candleCount: 100);
        var settings = Settings(maxOpen: 1);
        var engine = new PortfolioBacktestEngine(client, symbolInfo: null, NullLogger.Instance);

        var maxConcurrent = 0;
        await engine.RunAsync(symbols, TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(50), settings,
            strategyFactory: _ => new AlwaysLongStrategy(),
            onStepOpenPositions: c => maxConcurrent = Math.Max(maxConcurrent, c));

        maxConcurrent.Should().BeLessThanOrEqualTo(1, "bei MaxOpenPositions=1 nie mehr als 1 offen");
        maxConcurrent.Should().Be(1, "mindestens eine Position wurde eroeffnet (Signale lagen an)");
    }
}
