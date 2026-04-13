using BingXBot.Core.Interfaces;
using BingXBot.Engine;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Tests für den StrategyManager. Nach Buch-Refactoring nutzt das Template immer SequenzKonzeptStrategy.
/// </summary>
public class StrategyManagerTests
{
    [Fact]
    public void GetOrCreateForSymbol_SameSymbol_ShouldReturnSameInstance()
    {
        var manager = new StrategyManager();
        manager.SetStrategy(new SequenzKonzeptStrategy());

        var first = manager.GetOrCreateForSymbol("BTC-USDT");
        var second = manager.GetOrCreateForSymbol("BTC-USDT");

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void GetOrCreateForSymbol_DifferentSymbols_ShouldReturnDifferentInstances()
    {
        var manager = new StrategyManager();
        manager.SetStrategy(new SequenzKonzeptStrategy());

        var btc = manager.GetOrCreateForSymbol("BTC-USDT");
        var eth = manager.GetOrCreateForSymbol("ETH-USDT");

        btc.Should().NotBeSameAs(eth);
        btc.Name.Should().Be(eth.Name);
    }

    [Fact]
    public void Reset_ShouldClearAllInstances()
    {
        var manager = new StrategyManager();
        manager.SetStrategy(new SequenzKonzeptStrategy());

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
        var skA = new SequenzKonzeptStrategy();
        manager.SetStrategy(skA);
        var beforeReset = manager.GetOrCreateForSymbol("BTC-USDT");

        var skB = new SequenzKonzeptStrategy();
        manager.SetStrategy(skB);
        var afterReset = manager.GetOrCreateForSymbol("BTC-USDT");

        beforeReset.Should().NotBeSameAs(afterReset);
    }

    [Fact]
    public void CurrentTemplate_ShouldReflectSetStrategy()
    {
        var manager = new StrategyManager();
        manager.CurrentTemplate.Should().BeNull();

        var sk = new SequenzKonzeptStrategy();
        manager.SetStrategy(sk);

        manager.CurrentTemplate.Should().BeSameAs(sk);
    }

    [Fact]
    public void RemoveSymbol_ShouldWork()
    {
        var manager = new StrategyManager();
        manager.SetStrategy(new SequenzKonzeptStrategy());

        var before = manager.GetOrCreateForSymbol("BTC-USDT");
        manager.RemoveSymbol("BTC-USDT");
        var after = manager.GetOrCreateForSymbol("BTC-USDT");

        before.Should().NotBeSameAs(after);
    }
}
