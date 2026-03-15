using BingXBot.Engine.Strategies;
using BingXBot.Core.Interfaces;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Tests für die StrategyFactory - Erstellung aller 6 Strategien.
/// </summary>
public class StrategyFactoryTests
{
    [Theory]
    [InlineData("Trend-Following")]
    [InlineData("EMA Cross")]
    [InlineData("RSI Momentum")]
    [InlineData("Bollinger Breakout")]
    [InlineData("MACD")]
    [InlineData("Smart Grid")]
    public void Create_ShouldReturnCorrectStrategy(string name)
    {
        var strategy = StrategyFactory.Create(name);
        strategy.Should().NotBeNull();
        strategy.Should().BeAssignableTo<IStrategy>();
        strategy.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_UnknownName_ShouldThrow()
    {
        var act = () => StrategyFactory.Create("Unbekannt");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AvailableStrategies_ShouldHave6Entries()
    {
        StrategyFactory.AvailableStrategies.Should().HaveCount(6);
    }

    [Theory]
    [InlineData("Trend-Following")]
    [InlineData("EMA Cross")]
    [InlineData("RSI Momentum")]
    [InlineData("Bollinger Breakout")]
    [InlineData("MACD")]
    [InlineData("Smart Grid")]
    public void Create_ShouldReturnCloneableStrategy(string name)
    {
        var strategy = StrategyFactory.Create(name);
        var clone = strategy.Clone();
        clone.Should().NotBeSameAs(strategy);
        clone.Name.Should().Be(strategy.Name);
        clone.Parameters.Count.Should().Be(strategy.Parameters.Count);
    }

    [Fact]
    public void Create_TrendFollowing_ShouldBeDefault()
    {
        // Trend-Following ist die erste Strategie (Default für Krypto)
        StrategyFactory.AvailableStrategies[0].Should().Be("Trend-Following");
    }

    [Fact]
    public void Create_OldNames_ShouldThrow()
    {
        // Alte Namen (vor Krypto-Optimierung) sollen nicht mehr funktionieren
        var act1 = () => StrategyFactory.Create("RSI");
        act1.Should().Throw<ArgumentException>();

        var act2 = () => StrategyFactory.Create("Bollinger Bands");
        act2.Should().Throw<ArgumentException>();

        var act3 = () => StrategyFactory.Create("Grid");
        act3.Should().Throw<ArgumentException>();
    }
}
