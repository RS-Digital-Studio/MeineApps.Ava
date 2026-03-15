using BingXBot.Core.Interfaces;
using BingXBot.Engine;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

public class StrategyManagerTests
{
    [Fact]
    public void GetOrCreateForSymbol_SameSymbol_ShouldReturnSameInstance()
    {
        var manager = new StrategyManager();
        manager.SetStrategy(new EmaCrossStrategy());

        var first = manager.GetOrCreateForSymbol("BTC-USDT");
        var second = manager.GetOrCreateForSymbol("BTC-USDT");

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void GetOrCreateForSymbol_DifferentSymbols_ShouldReturnDifferentInstances()
    {
        var manager = new StrategyManager();
        manager.SetStrategy(new EmaCrossStrategy());

        var btc = manager.GetOrCreateForSymbol("BTC-USDT");
        var eth = manager.GetOrCreateForSymbol("ETH-USDT");

        btc.Should().NotBeSameAs(eth);
        btc.Name.Should().Be(eth.Name); // Gleicher Typ
    }

    [Fact]
    public void Reset_ShouldClearAllInstances()
    {
        var manager = new StrategyManager();
        manager.SetStrategy(new EmaCrossStrategy());

        var before = manager.GetOrCreateForSymbol("BTC-USDT");
        manager.Reset();
        var after = manager.GetOrCreateForSymbol("BTC-USDT");

        before.Should().NotBeSameAs(after);
    }

    [Fact]
    public void GetOrCreateForSymbol_WithoutStrategy_ShouldThrow()
    {
        var manager = new StrategyManager();

        var act = () => manager.GetOrCreateForSymbol("BTC-USDT");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Keine Strategie*");
    }

    [Fact]
    public void SetStrategy_ShouldClearExistingInstances()
    {
        var manager = new StrategyManager();
        manager.SetStrategy(new EmaCrossStrategy());
        var emaCross = manager.GetOrCreateForSymbol("BTC-USDT");

        manager.SetStrategy(new RsiStrategy());
        var rsi = manager.GetOrCreateForSymbol("BTC-USDT");

        emaCross.Name.Should().Be("EMA Cross");
        rsi.Name.Should().Be("RSI");
        emaCross.Should().NotBeSameAs(rsi);
    }

    [Fact]
    public void CurrentTemplate_ShouldReflectSetStrategy()
    {
        var manager = new StrategyManager();
        manager.CurrentTemplate.Should().BeNull();

        var ema = new EmaCrossStrategy();
        manager.SetStrategy(ema);

        manager.CurrentTemplate.Should().BeSameAs(ema);
    }

    [Fact]
    public void RemoveSymbol_ShouldWork()
    {
        var manager = new StrategyManager();
        manager.SetStrategy(new EmaCrossStrategy());

        var before = manager.GetOrCreateForSymbol("BTC-USDT");
        manager.RemoveSymbol("BTC-USDT");
        var after = manager.GetOrCreateForSymbol("BTC-USDT");

        before.Should().NotBeSameAs(after);
    }
}
