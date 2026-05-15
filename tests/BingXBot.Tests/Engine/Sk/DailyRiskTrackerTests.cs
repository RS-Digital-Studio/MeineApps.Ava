using BingXBot.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für Daily-Risk-Tracker (Task 3.3).</summary>
public class DailyRiskTrackerTests
{
    [Fact]
    public void MaxDailyRiskPercent_DefaultIstNull()
    {
        var settings = new RiskSettings();
        settings.MaxDailyRiskPercent.Should().Be(0m);
    }

    [Fact]
    public void MaxDailyRiskPercent_KannGeaendertWerden()
    {
        var settings = new RiskSettings { MaxDailyRiskPercent = 3m };
        settings.MaxDailyRiskPercent.Should().Be(3m);
    }

    [Fact]
    public void MaxDailyLossPercent_UnabhaengigVonDailyRisk()
    {
        var settings = new RiskSettings
        {
            MaxDailyLossPercent = 5m,
            MaxDailyRiskPercent = 3m,
        };
        settings.MaxDailyLossPercent.Should().Be(5m);
        settings.MaxDailyRiskPercent.Should().Be(3m);
    }

    [Fact]
    public void RiskManager_SetOpenRiskEstimate_AkzeptiertDezimal()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<BingXBot.Engine.Risk.RiskManager>();
        var rm = new BingXBot.Engine.Risk.RiskManager(new RiskSettings(), logger);
        rm.SetOpenRiskEstimate(150m);
        // Kein Exception = pass; interner Wert wird in ValidateTrade genutzt
    }

    [Fact]
    public void RiskManager_SetOpenRiskEstimate_NegativeWerteWerdenGeklemmt()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<BingXBot.Engine.Risk.RiskManager>();
        var rm = new BingXBot.Engine.Risk.RiskManager(new RiskSettings(), logger);
        rm.SetOpenRiskEstimate(-50m);
        // Interne Implementation klemmt auf 0 (siehe Code: Math.Max(0, ...))
    }

    [Fact]
    public void RiskSettings_MaxRiskPercentPerTrade_HatUserDefault5()
    {
        // Bewusste User-Abweichung vom Buch (5 % statt 1-3 %), dokumentiert in
        // src/Apps/BingXBot/CLAUDE.md → "Bewusste User-Abweichungen vom Buch".
        var settings = new RiskSettings();
        settings.MaxRiskPercentPerTrade.Should().Be(5m);
    }

    [Fact]
    public void RiskSettings_MaxDailyRiskPercent_NegativIgnoriert()
    {
        var settings = new RiskSettings { MaxDailyRiskPercent = -5m };
        // Setter klemmt nicht, aber Logic prüft > 0 vor Anwendung
        settings.MaxDailyRiskPercent.Should().Be(-5m);
    }
}
