using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Smoke-Tests für das SK-System (Multi-TF Standalone, 15.04.2026).
/// Ausführliche SK-Logik-Tests liegen in ScanRotationTests + manuellen Backtests.
/// </summary>
public class StrategyTests
{
    [Fact]
    public void SkSystem_NoCandles_ShouldReturnNoSignal()
    {
        var strategy = new SequenzKonzeptStrategy();

        var context = new MarketContext(
            "BTC-USDT",
            new List<Candle>(),
            new Ticker("BTC-USDT", 50000m, 49900m, 50100m, 1_000_000m, 1m, DateTime.UtcNow),
            new List<Position>(),
            new AccountInfo(1000m, 1000m, 0m, 0m),
            Category: MarketCategory.Crypto,
            NavigatorTimeframe: TimeFrame.H4);

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

        var clone = strategy.Clone();
        clone.Should().NotBeSameAs(strategy);
        clone.Name.Should().Be(strategy.Name);
        clone.Should().BeOfType<SequenzKonzeptStrategy>();
    }

    [Fact]
    public void SkSystem_ShouldDisableSmartBreakeven()
    {
        var strategy = new SequenzKonzeptStrategy();

        // SK-Regel: Kein Smart-BE (B-C Korrekturen stoppen sonst aus)
        strategy.DisableSmartBreakeven.Should().BeTrue();
    }

    [Theory]
    [InlineData(TimeFrame.D1)]
    [InlineData(TimeFrame.H4)]
    [InlineData(TimeFrame.H1)]
    [InlineData(TimeFrame.M15)]
    public void SkSystem_MultiTf_NoCandles_ShouldReturnNoSignal(TimeFrame navTf)
    {
        var strategy = new SequenzKonzeptStrategy();
        var context = new MarketContext(
            "BTC-USDT",
            new List<Candle>(),
            new Ticker("BTC-USDT", 50000m, 49900m, 50100m, 1_000_000m, 1m, DateTime.UtcNow),
            new List<Position>(),
            new AccountInfo(1000m, 1000m, 0m, 0m),
            Category: MarketCategory.Crypto,
            NavigatorTimeframe: navTf);

        var result = strategy.Evaluate(context);
        result.Signal.Should().Be(Signal.None);
        result.Reason.Should().Contain(navTf.ToString());
    }

    [Theory]
    [InlineData(TimeFrame.D1, TimeFrame.H4)]
    [InlineData(TimeFrame.H4, TimeFrame.H1)]
    [InlineData(TimeFrame.H1, TimeFrame.M15)]
    [InlineData(TimeFrame.M15, TimeFrame.M5)]
    public void GetFilterTimeframe_MapsNavigatorToFilter(TimeFrame navTf, TimeFrame expectedFilter)
    {
        var filter = SequenzKonzeptStrategy.GetFilterTimeframe(navTf);
        filter.Should().Be(expectedFilter);
    }

    [Fact]
    public void SkSystem_AmpelStatus_IsDictionary()
    {
        var strategy = new SequenzKonzeptStrategy();
        var context = new MarketContext(
            "BTC-USDT",
            new List<Candle>(),
            new Ticker("BTC-USDT", 50000m, 49900m, 50100m, 1_000_000m, 1m, DateTime.UtcNow),
            new List<Position>(),
            new AccountInfo(1000m, 1000m, 0m, 0m),
            Category: MarketCategory.Crypto,
            NavigatorTimeframe: TimeFrame.H4);

        strategy.Evaluate(context);

        // AmpelStatus enthält mindestens Einträge für W1/D1/Navigator + Filter-TF (optional)
        strategy.AmpelStatus.Should().ContainKey(TimeFrame.W1);
        strategy.AmpelStatus.Should().ContainKey(TimeFrame.D1);
        strategy.AmpelStatus.Should().ContainKey(TimeFrame.H4);
    }
}
