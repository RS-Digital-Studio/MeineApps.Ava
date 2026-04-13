using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

public class ConfigTests
{
    [Fact]
    public void RiskSettings_ShouldHaveBuchCompliantDefaults()
    {
        // SK-Buch-Refactoring 12.04.2026: 1% Risiko/Trade (Buch S.13), max 3 offene Positionen
        var s = new RiskSettings();
        s.MaxPositionSizePercent.Should().Be(3m);           // Buch: 3% Margin-Limit
        s.MaxMarginPerTradePercent.Should().Be(1m);         // Buch: 1% Risiko pro Trade
        s.MaxDailyDrawdownPercent.Should().Be(0m);          // Deaktiviert (User-Entscheidung)
        s.MaxTotalDrawdownPercent.Should().Be(10m);
        s.MaxOpenPositions.Should().Be(3);                  // Buch: max 3 Trades gleichzeitig
        s.MaxLeverage.Should().Be(10m);
        s.Tp1CloseRatio.Should().Be(0.5m);                  // Buch: 50% bei TP1
        s.Tp2CloseRatio.Should().Be(0.5m);                  // Buch: 50% Rest bei TP2
        s.MinRiskRewardRatio.Should().Be(0m);               // Strategy hat eigenen 1:1-Check
        s.MaxHoldHours.Should().Be(0);                      // Unbegrenzt (SL/TP managed Exit)
        s.CooldownHours.Should().Be(0);                     // Deaktiviert (Symbol-Cooldown direkt 4h)
        s.MaxTradesPerDay.Should().Be(0);                   // Unbegrenzt
    }

    [Fact]
    public void BotSettings_DefaultMode_ShouldBePaper()
    {
        var s = new BotSettings();
        s.LastMode.Should().Be(TradingMode.Paper);
        s.Risk.Should().NotBeNull();
        s.Scanner.Should().NotBeNull();
    }

    [Fact]
    public void BacktestSettings_ShouldHaveBingXFees()
    {
        var s = new BacktestSettings();
        s.MakerFee.Should().Be(0.0002m);
        s.TakerFee.Should().Be(0.0005m);
        s.InitialBalance.Should().Be(1000m);
    }

    [Fact]
    public void ScannerSettings_ShouldHaveBuchCompliantDefaults()
    {
        // SK-Buch: H4 Navigator, Reversal-Modus, breites Screening, niedrige Min-Volume für Stabilisierungsphasen
        var s = new ScannerSettings();
        s.MinVolume24h.Should().Be(1_000_000m);
        s.ScanTimeFrame.Should().Be(TimeFrame.H4);
        s.MaxResults.Should().Be(100);
        s.ScanIntervalSeconds.Should().Be(60);              // H4 nutzt 60s für schnelle M30-Reaktion
        s.Mode.Should().Be(ScanMode.Reversal);              // SK = Mean-Reversion
        s.EnableTradFi.Should().BeTrue();
    }
}
