using BingXBot.Backtest;
using BingXBot.Backtest.Portfolio;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Backtest;

/// <summary>
/// Regression: Bei EINEM Symbol, allen konto-weiten Gates praktisch deaktiviert und symbolInfo=null
/// muss der Portfolio-Run dieselben Trades + PnL liefern wie <see cref="BacktestEngine.RunAsync"/>
/// desselben Symbols. Beweist, dass der Portfolio-Pfad die Exit-/Entry-Logik NICHT dupliziert oder
/// veraendert (er teilt sich <see cref="BacktestExitProcessor"/> / <see cref="BacktestEntryProcessor"/>).
///
/// Modelliert wird mit der Live-Strategie TrendFollow-Fast (H4-only, kein Entry-TF-Sub-Loop) auf
/// einer deterministischen Kerzenreihe. Funding + dynamische Slippage sind AUS, damit der Vergleich
/// rein die Trade-Logik isoliert (nicht das stochastische/zeitliche Markt-Modell). Equity-Snapshot-
/// Frequenz darf abweichen — verglichen werden NUR Trades + PnL.
/// </summary>
public class PortfolioVsSingleRegressionTest
{
    private const string Symbol = "BTC-USDT";

    private static BotSettings GatesOff()
    {
        var s = new BotSettings();
        s.Backtest.InitialBalance = 1000m;
        s.Backtest.UseDynamicSlippage = false;
        s.Backtest.SimulateFundingRate = false;
        s.Backtest.SlippagePercent = 0.05m;
        s.Backtest.SpreadPercent = 0.08m;

        s.Risk.MaxOpenPositions = 999;
        s.Risk.MaxOpenPositionsPerSymbol = 999;
        s.Risk.MaxTotalMarginPercent = 0m;
        s.Risk.MaxCorrelatedExposurePercent = 0m;
        s.Risk.MaxDailyLossPercent = 0m;
        s.Risk.MaxDailyDrawdownPercent = 0m;
        s.Risk.MaxTotalDrawdownPercent = 100m;
        s.Risk.MaxDailyRiskPercent = 0m;
        s.Risk.EnableLossStreakDampening = false;
        s.Risk.EnableEquityCurveScaling = false;
        s.Risk.EnableVolatilityTargeting = false;

        // Leverage angleichen: Die Single-Engine reicht KEIN Kategorie-Leverage durch (ProcessEntryAsync
        // mit adaptLeverage=0 → ValidateTrade nutzt den globalen MaxLeverage). Die Portfolio-Engine reicht
        // (int)catSettings.MaxLeverage durch (Live-faithful). Damit der Regressionsvergleich NUR die Exit/
        // Entry-Logik isoliert (nicht den — beabsichtigt unterschiedlichen — Leverage-Pfad), wird das
        // Crypto-Kategorie-Leverage hier auf den globalen Default gesetzt → beide Engines sizen identisch.
        s.Risk.MaxLeverage = 10m;
        s.Risk.CategorySettings[MarketCategory.Crypto] = s.Risk.CategorySettings[MarketCategory.Crypto] with { MaxLeverage = 10m };
        return s;
    }

    /// <summary>Gemischte Auf-/Ab-Phasen, damit sowohl Long- als auch SL/TP-Exits ausgeloest werden.</summary>
    private static List<Candle> MixedRegime(int count)
    {
        var list = new List<Candle>(count);
        var rng = new Random(7);
        var price = 100m;
        for (int i = 0; i < count; i++)
        {
            // Wechselnde Trend-Phasen (alle ~40 Kerzen Richtungswechsel) + Rauschen.
            var phase = (i / 40) % 2 == 0 ? 1m : -1m;
            var drift = phase * 0.6m;
            var noise = (decimal)(rng.NextDouble() - 0.5) * 1.4m;
            var open = price;
            var close = Math.Max(price + drift + noise, 1m);
            var high = Math.Max(open, close) + (decimal)rng.NextDouble() * 0.8m;
            var low = Math.Max(Math.Min(open, close) - (decimal)rng.NextDouble() * 0.8m, 0.5m);
            var openTime = PortfolioCandleGen.Start.AddHours(4 * i);
            list.Add(new Candle(openTime, open, high, low, close, 1000m + (decimal)rng.NextDouble() * 300m, openTime.AddHours(4)));
            price = close;
        }
        return list;
    }

    [Fact]
    public async Task SingleSymbol_GatesOff_PortfolioMatchesSingleEngineTradesAndPnl()
    {
        var candles = MixedRegime(400);
        var settings = GatesOff();

        // --- Single-Symbol-Engine ---
        // TF-gefilterter Fake-Client: nur H4 liefert Kerzen → W1/D1 bleiben leer (TrendFollow ignoriert sie
        // ohnehin, aber so ist der Eingabe-Kontext beider Engines identisch).
        var singleClient = new FakePublicClient(new Dictionary<string, List<Candle>> { [Symbol] = candles }, onlyTf: TimeFrame.H4);
        var singleEngine = new BacktestEngine(singleClient, NullLogger<BacktestEngine>.Instance);
        var singleRisk = new RiskManager(settings.Risk, NullLogger<RiskManager>.Instance);
        var singleStrategy = StrategyFactory.Create("TrendFollow-Fast");
        var singleReport = await singleEngine.RunAsync(
            singleStrategy, singleRisk, Symbol, TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(400),
            settings.Backtest, scannerSettings: settings.Scanner, riskSettings: settings.Risk);

        // --- Portfolio-Engine (1 Symbol) ---
        var portfolioClient = new FakePublicClient(new Dictionary<string, List<Candle>> { [Symbol] = candles });
        var portfolioEngine = new PortfolioBacktestEngine(portfolioClient, symbolInfo: null, NullLogger.Instance);
        var portfolioReport = await portfolioEngine.RunAsync(
            [Symbol], TimeFrame.H4,
            PortfolioCandleGen.Start, PortfolioCandleGen.Start.AddDays(400), settings,
            strategyName: "TrendFollow-Fast");

        // Es MUSS Trades geben, sonst ist der Test wertlos.
        singleReport.TotalTrades.Should().BeGreaterThan(0, "die Strategie muss auf dieser Kerzenreihe handeln");

        // Trade-fuer-Trade-Vergleich (Symbol/Side/Entry/Exit/Qty/Pnl/Zeiten) — bit-identisch.
        portfolioReport.Trades.Should().HaveCount(singleReport.Trades.Count, "gleiche Anzahl Trades");
        for (int i = 0; i < singleReport.Trades.Count; i++)
        {
            var a = singleReport.Trades[i];
            var b = portfolioReport.Trades[i];
            b.Symbol.Should().Be(a.Symbol, $"Trade {i} Symbol");
            b.Side.Should().Be(a.Side, $"Trade {i} Side");
            b.EntryPrice.Should().Be(a.EntryPrice, $"Trade {i} EntryPrice");
            b.ExitPrice.Should().Be(a.ExitPrice, $"Trade {i} ExitPrice");
            b.Quantity.Should().Be(a.Quantity, $"Trade {i} Quantity");
            b.Pnl.Should().Be(a.Pnl, $"Trade {i} Pnl");
            b.EntryTime.Should().Be(a.EntryTime, $"Trade {i} EntryTime");
            b.ExitTime.Should().Be(a.ExitTime, $"Trade {i} ExitTime");
        }

        portfolioReport.TotalPnl.Should().Be(singleReport.TotalPnl, "Σ PnL bit-identisch");
    }
}
