using BingXBot.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für <see cref="RiskSettings.Tp1CloseRatio"/> (Task 4.6).</summary>
public class Tp1CloseRatioTests
{
    [Fact]
    public void UntereGrenzeWirdGeklemmt()
    {
        var settings = new RiskSettings { Tp1CloseRatio = 0.3m };
        settings.Tp1CloseRatio.Should().Be(0.5m);
    }

    [Fact]
    public void ObereGrenzeWirdGeklemmt()
    {
        var settings = new RiskSettings { Tp1CloseRatio = 0.9m };
        settings.Tp1CloseRatio.Should().Be(0.8m);
    }

    [Fact]
    public void MittlererWertBleibtErhalten()
    {
        var settings = new RiskSettings { Tp1CloseRatio = 0.65m };
        settings.Tp1CloseRatio.Should().Be(0.65m);
    }

    [Fact]
    public void DefaultIstFuenfzigProzent()
    {
        var settings = new RiskSettings();
        settings.Tp1CloseRatio.Should().Be(0.5m);
    }

    [Fact]
    public void ExaktAchtzigProzentGueltig()
    {
        var settings = new RiskSettings { Tp1CloseRatio = 0.8m };
        settings.Tp1CloseRatio.Should().Be(0.8m);
    }
}
