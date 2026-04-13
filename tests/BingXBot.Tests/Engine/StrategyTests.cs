using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Smoke-Tests für das SK-System (einzige aktive Strategie nach Buch-Refactoring 12.04.2026).
/// Ausführliche SK-Logik-Tests liegen in ScanRotationTests + manuellen Backtests.
/// </summary>
public class StrategyTests
{
    [Fact]
    public void SkSystem_NoCandles_ShouldReturnNoSignal()
    {
        var strategy = new SequenzKonzeptStrategy();
        strategy.ApplyPreset(TradingModePreset.Swing);

        var context = new MarketContext(
            "BTC-USDT",
            new List<Candle>(),
            new Ticker("BTC-USDT", 50000m, 49900m, 50100m, 1_000_000m, 1m, DateTime.UtcNow),
            new List<Position>(),
            new AccountInfo(1000m, 1000m, 0m, 0m),
            Category: MarketCategory.Crypto);

        var result = strategy.Evaluate(context);
        result.Signal.Should().Be(Signal.None);
    }

    [Fact]
    public void SkSystem_ShouldHaveCorrectName()
    {
        var strategy = new SequenzKonzeptStrategy();
        strategy.Name.Should().Be("SK-System");
    }

    [Fact]
    public void SkSystem_ShouldBeCloneable()
    {
        var strategy = new SequenzKonzeptStrategy();
        strategy.ApplyPreset(TradingModePreset.Swing);

        var clone = strategy.Clone();
        clone.Should().NotBeSameAs(strategy);
        clone.Name.Should().Be(strategy.Name);
        clone.Should().BeOfType<SequenzKonzeptStrategy>();
    }

    [Fact]
    public void SkSystem_ShouldDisableSmartBreakeven()
    {
        var strategy = new SequenzKonzeptStrategy();
        strategy.ApplyPreset(TradingModePreset.Swing);

        // SK-Regel: Kein Smart-BE (B-C Korrekturen stoppen sonst aus)
        strategy.DisableSmartBreakeven.Should().BeTrue();
    }
}
