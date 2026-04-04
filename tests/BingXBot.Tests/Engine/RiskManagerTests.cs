using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Risk;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BingXBot.Tests.Engine;

public class RiskManagerTests
{
    /// <summary>
    /// Standard-RiskSettings für Tests: Neue Checks (Liquidation/Exposure/Funding) relaxiert,
    /// damit bestehende Tests die auf Drawdown/Position-Limits fokussieren weiterhin korrekt testen.
    /// </summary>
    private static RiskSettings CreateTestSettings(Action<RiskSettings>? configure = null)
    {
        var settings = new RiskSettings
        {
            // Neue Checks relaxieren (Tests die diese Features testen, setzen eigene Werte)
            MinLiquidationDistancePercent = 0m, // Deaktiviert
            MaxNetExposurePercent = 99999m,     // Praktisch unbegrenzt
            ConsiderFundingRate = false          // Deaktiviert
        };
        configure?.Invoke(settings);
        return settings;
    }

    private static MarketContext CreateContext(int openPositions = 0, decimal balance = 10000m, string symbol = "BTC-USDT", decimal unrealizedPnl = 0m, List<Position>? customPositions = null)
    {
        var positions = customPositions ?? Enumerable.Range(0, openPositions)
            .Select(i => new Position($"SYM{i}-USDT", Side.Buy, 100m, 100m, 1m, 0m, 10m, MarginType.Cross, DateTime.UtcNow))
            .ToList();

        return new MarketContext(
            symbol,
            new List<Candle>(),
            new Ticker(symbol, 50000m, 49999m, 50001m, 10000000m, 5m, DateTime.UtcNow),
            positions,
            new AccountInfo(balance, balance, unrealizedPnl, 0m));
    }

    [Fact]
    public void ValidateTrade_NoSignal_ShouldReject()
    {
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.None, 0m, null, null, null, "Kein Signal");
        var result = risk.ValidateTrade(signal, CreateContext());
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void ValidateTrade_MaxPositionsReached_ShouldReject()
    {
        var settings = CreateTestSettings(s => s.MaxOpenPositions = 2);
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext(openPositions: 2));
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Max");
    }

    [Fact]
    public void ValidateTrade_UnderLimits_ShouldAllow()
    {
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext());
        result.IsAllowed.Should().BeTrue();
        result.AdjustedPositionSize.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CalculatePositionSize_WithStopLoss_ShouldCalculate()
    {
        var settings = CreateTestSettings(s => { s.MaxPositionSizePercent = 2m; s.MaxLeverage = 10m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var size = risk.CalculatePositionSize("BTC-USDT", 50000m, 49000m, account);
        size.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CalculatePositionSize_Margin_basiert()
    {
        // 2% von 10.000 = 200 USDT Margin, * 10x Leverage = 2.000 USDT Position
        // 2.000 / 50.000 = 0,04 BTC
        var settings = CreateTestSettings(s => { s.MaxPositionSizePercent = 2m; s.MaxLeverage = 10m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var size = risk.CalculatePositionSize("BTC-USDT", 50000m, null, account);
        size.Should().Be(0.04m);
    }

    [Fact]
    public void CalculatePositionSize_MitUndOhneSL_UnterschiedlicheGroesse()
    {
        // H-1 Fix: Risiko-basiertes Sizing: SL-Distanz bestimmt Positionsgröße.
        // Enger SL → größere Position, weiter SL → kleinere Position.
        // Ohne SL → Fallback auf Margin-basiertes Sizing.
        var settings = CreateTestSettings(s => { s.MaxPositionSizePercent = 2m; s.MaxLeverage = 10m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var withSl = risk.CalculatePositionSize("BTC-USDT", 50000m, 49000m, account);
        var withoutSl = risk.CalculatePositionSize("BTC-USDT", 50000m, null, account);
        // Mit SL: Risiko-basiertes Sizing (maxLoss / slDistance). Ohne SL: Margin-basiert.
        // Bei SL 49000 (2% Distanz): 200 USDT maxLoss / 1000 slDistance = 0.2 BTC
        // Ohne SL: 200 USDT * 10x / 50000 = 0.04 BTC
        withSl.Should().NotBe(withoutSl, "Risiko-basiertes Sizing mit SL unterscheidet sich von Margin-basiertem ohne SL");
    }

    [Fact]
    public void DailyDrawdown_ShouldBlockAfterThreshold()
    {
        var settings = CreateTestSettings(s => { s.MaxDailyDrawdownPercent = 5m; s.MaxOpenPositions = 10; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // Verluste die 5% DD verursachen (550 von 10000 = 5.5%)
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 47500m, 0.1m, -300m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));
        risk.UpdateDailyStats(new CompletedTrade("ETH-USDT", Side.Buy, 3000m, 2800m, 1m, -250m, 3m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Drawdown");
    }

    [Fact]
    public void ResetDailyStats_ShouldClearDailyPnl()
    {
        var settings = CreateTestSettings(s => { s.MaxDailyDrawdownPercent = 5m; s.MaxOpenPositions = 10; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 47500m, 0.1m, -600m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        risk.ResetDailyStats();

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void DailyDrawdown_WithUnrealizedLoss_ShouldBlockEarlier()
    {
        // Realisierte Verluste allein reichen nicht (1% < 5%), aber mit unrealisierten
        // Verlusten (-300 = 3%) + neuem Position-Risiko kommen wir über 5% -> blockiert
        // Neues Risiko bei SL: slDistance * posSize = 1000 * 0.04 = 40 USDT (0.4%)
        var settings = CreateTestSettings(s => { s.MaxDailyDrawdownPercent = 5m; s.MaxOpenPositions = 10; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 100 realisierter Verlust = 1%
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 49000m, 0.1m, -100m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");

        // Ohne unrealisierte Verluste: 1% realisiert + 0.4% neues Risiko = 1.4% < 5% -> erlaubt
        var resultOk = risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        resultOk.IsAllowed.Should().BeTrue();

        // Mit -400 unrealisierten Verlusten auf offener Position:
        // 1% realisiert + 4% unrealisiert + 0.4% neues Risiko = 5.4% >= 5% -> blockiert
        var losingPosition = new Position("ETH-USDT", Side.Buy, 3000m, 2600m, 1m, -400m, 10m, MarginType.Cross, DateTime.UtcNow);
        var resultBlocked = risk.ValidateTrade(signal, CreateContext(balance: 10000m, customPositions: new List<Position> { losingPosition }));
        resultBlocked.IsAllowed.Should().BeFalse();
        resultBlocked.RejectionReason.Should().Contain("Drawdown");
    }

    [Fact]
    public void DailyDrawdown_MixedPositions_ShouldUseIndividualLosses()
    {
        // Netto-UnrealizedPnl = +200 -300 = -100 (nur 1%), aber der einzelne Verlust
        // von -300 (3%) muss in den Drawdown einfliessen. Bei 3% realisiert + 3% unrealisiert = 6% -> blockiert
        var settings = CreateTestSettings(s => { s.MaxDailyDrawdownPercent = 5m; s.MaxOpenPositions = 10; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 300 realisierter Verlust = 3%
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 47500m, 0.1m, -300m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");

        // Position A: +200, Position B: -300. Netto = -100 (wuerde nur 1% sein),
        // aber Summe der Verluste = -300 (3%). 3% realisiert + 3% unrealisiert = 6% >= 5% -> blockiert
        var positions = new List<Position>
        {
            new("ETH-USDT", Side.Buy, 3000m, 3200m, 1m, 200m, 10m, MarginType.Cross, DateTime.UtcNow),
            new("SOL-USDT", Side.Buy, 150m, 120m, 10m, -300m, 10m, MarginType.Cross, DateTime.UtcNow)
        };
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m, customPositions: positions));
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Drawdown");
    }

    [Fact]
    public void DailyDrawdown_WithUnrealizedProfit_ShouldNotAffectDrawdown()
    {
        // Positive unrealisierte PnL soll den Drawdown nicht reduzieren
        var settings = CreateTestSettings(s => { s.MaxDailyDrawdownPercent = 5m; s.MaxOpenPositions = 10; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 550 realisierter Verlust = 5.5% -> blockiert
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 47500m, 0.1m, -550m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");

        // Trotz +500 unrealisierter Profit (Position im Plus) bleibt der Drawdown bei 5.5% -> blockiert
        var profitPosition = new Position("ETH-USDT", Side.Buy, 3000m, 3500m, 1m, 500m, 10m, MarginType.Cross, DateTime.UtcNow);
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m, customPositions: new List<Position> { profitPosition }));
        result.IsAllowed.Should().BeFalse();
    }

    // === Neue Tests: Position-Risiko im Drawdown (17.03.2026) ===

    [Fact]
    public void ValidateTrade_SignalMitSL_WorstCaseRisikoWirdEingerechnet()
    {
        // 2% von 10000 = 200 USDT MaxPositionSize.
        // SL-Distanz: |50000 - 49000| = 1000, slPercent = 2%.
        // PositionValue = 200 / 0.02 = 10000, capped auf 10000*10 = 100000 -> 2 BTC.
        // WorstCase = 1000 USDT * 2 BTC = 2000 USDT... aber MaxRisk wird durch PositionSize begrenzt.
        // Wir testen nur: Bei 4.8% realisiertem Verlust + SL-Risiko kein zusätzlicher Verlust ohne SL-Hit.
        // Konkret: 480 USDT realisiert = 4.8%. newPositionRisk mit SL = slDistance * posSize.
        // Test-Prüfpunkt: Mit SL wird ein anderer (höherer) WorstCase berechnet als ohne SL.
        var settings = CreateTestSettings(s => { s.MaxDailyDrawdownPercent = 5m; s.MaxOpenPositions = 10; s.MaxPositionSizePercent = 1m; s.MaxLeverage = 2m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 100 USDT realisierter Verlust = 1% von 10000
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 49000m, 0.1m, -100m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        // Signal mit engem SL (1% Distanz): newPositionRisk = slDistance * posSize
        // posSize = (10000 * 1% / 0.02) / 50000 = 100 USDT Value -> 0.002 BTC
        // WorstCase = 500 * 0.002 = 1 USDT → sehr klein → erlaubt
        var signalMitSl = new SignalResult(Signal.Long, 0.8m, 50000m, 49500m, 51000m, "Test");
        var resultMitSl = risk.ValidateTrade(signalMitSl, CreateContext(balance: 10000m));
        resultMitSl.IsAllowed.Should().BeTrue("Kleines SL-Risiko passt noch in Drawdown-Budget");
    }

    [Fact]
    public void ValidateTrade_SignalOhneSL_NutztKonservativenFallback()
    {
        // Ohne SL: newPositionRisk = AvailableBalance * MaxPositionSizePercent / 100
        // Wenn dieser Fallback allein bereits den verbleibenden Drawdown-Puffer übersteigt, wird blockiert.
        // Settings: MaxDrawdown=5%, MaxPositionSize=4.5%, Balance=10000.
        // Kein bisheriger Verlust. Fallback-Risiko = 10000 * 4.5% = 450 USDT = 4.5% -> noch erlaubt.
        // Mit 1% realisiertem Verlust: 100 + 450 = 550 USDT = 5.5% >= 5% -> blockiert.
        var settings = CreateTestSettings(s => { s.MaxDailyDrawdownPercent = 5m; s.MaxOpenPositions = 10; s.MaxPositionSizePercent = 4.5m; s.MaxLeverage = 10m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 1% realisierter Verlust
        risk.UpdateDailyStats(new CompletedTrade("ETH-USDT", Side.Buy, 3000m, 2970m, 1m, -100m, 3m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        // Signal OHNE StopLoss
        var signalOhneSl = new SignalResult(Signal.Long, 0.8m, 50000m, null, 52000m, "Test ohne SL");
        var result = risk.ValidateTrade(signalOhneSl, CreateContext(balance: 10000m));

        // 100 (realisiert) + 450 (Fallback-Risiko ohne SL) = 550 = 5.5% >= 5% -> blockiert
        result.IsAllowed.Should().BeFalse("Konservativer Fallback ohne SL überschreitet Drawdown-Limit");
        result.RejectionReason.Should().Contain("Drawdown");
    }

    [Fact]
    public void ValidateTrade_HoherDrawdownPlusNeuesRisiko_WirdKorrektBlockiert()
    {
        // Testet das kombinierte Szenario: bestehender Drawdown (realisiert + unrealisiert)
        // plus das Worst-Case-Risiko der neuen Position überschreitet das Limit.
        var settings = CreateTestSettings(s => { s.MaxDailyDrawdownPercent = 5m; s.MaxOpenPositions = 10; s.MaxPositionSizePercent = 2m; s.MaxLeverage = 10m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 2% realisierter Verlust
        risk.UpdateDailyStats(new CompletedTrade("BTC-USDT", Side.Buy, 50000m, 49000m, 0.1m, -200m, 5m, DateTime.UtcNow, DateTime.UtcNow, "SL", TradingMode.Live));

        // Offene Position mit 1.5% unrealisiertem Verlust
        var verlustPosition = new Position("ETH-USDT", Side.Buy, 3000m, 2955m, 0.5m, -150m, 10m, MarginType.Cross, DateTime.UtcNow);

        // Neues Signal: ohne SL → Fallback-Risiko = 10000 * 2% = 200 USDT (2%)
        // Gesamt: 2% (real.) + 1.5% (unreal.) + 2% (neu) = 5.5% >= 5% → blockiert
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, null, 52000m, "Test ohne SL");
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m, customPositions: new List<Position> { verlustPosition }));

        result.IsAllowed.Should().BeFalse("Kombination aus realisierten, unrealisierten Verlusten und neuem Risiko überschreitet Limit");
        result.RejectionReason.Should().Contain("Drawdown");
    }

    [Fact]
    public void ValidateTrade_SignalMitSL_KleinesRisiko_WirdNichtFaelschlichBlockiert()
    {
        // Stellt sicher dass ein Signal mit sehr engem SL (kleines WorstCase-Risiko)
        // nicht fälschlicherweise blockiert wird, obwohl noch Drawdown-Budget vorhanden ist.
        var settings = CreateTestSettings(s => { s.MaxDailyDrawdownPercent = 5m; s.MaxOpenPositions = 10; s.MaxPositionSizePercent = 2m; s.MaxLeverage = 10m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // Kein bisheriger Verlust
        var signal = new SignalResult(Signal.Long, 0.9m, 50000m, 49900m, 50500m, "Enger SL");
        // SL-Distanz = 100, slPercent = 0.2%, posSize = (10000*2%) / 0.002 / 50000 = 2 BTC, capped auf 10000*10/50000 = 2 BTC
        // WorstCase = 100 * 2 = 200 USDT = 2% < 5% → erlaubt
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m));

        result.IsAllowed.Should().BeTrue("Kleines SL-Risiko passt problemlos in das Drawdown-Budget");
        result.AdjustedPositionSize.Should().BeGreaterThan(0m);
    }

    // === Neue Tests: Liquidation, Exposure, Funding-Rate ===

    [Fact]
    public void CalculateLiquidationPrice_Long_ShouldBeBelow()
    {
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        var liqPrice = risk.CalculateLiquidationPrice(50000m, 10m, Side.Buy);
        liqPrice.Should().BeLessThan(50000m);
        // Bei 10x: 50000 * (1 - 1/10 + 0.004) = 50000 * 0.904 = 45200
        liqPrice.Should().BeApproximately(45200m, 1m);
    }

    [Fact]
    public void CalculateLiquidationPrice_Short_ShouldBeAbove()
    {
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        var liqPrice = risk.CalculateLiquidationPrice(50000m, 10m, Side.Sell);
        liqPrice.Should().BeGreaterThan(50000m);
    }

    [Fact]
    public void CalculateNetExposure_ShouldSumPositionValues()
    {
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        var positions = new List<Position>
        {
            new("BTC-USDT", Side.Buy, 50000m, 50000m, 0.1m, 0m, 10m, MarginType.Cross, DateTime.UtcNow), // 5000 USDT
            new("ETH-USDT", Side.Buy, 3000m, 3000m, 1m, 0m, 10m, MarginType.Cross, DateTime.UtcNow)      // 3000 USDT
        };
        var exposure = risk.CalculateNetExposure(positions, 10000m);
        // (0.1*50000 + 1*3000) / 10000 * 100 = 80%
        exposure.Should().Be(80m);
    }

    [Fact]
    public void ValidateTrade_ExposureExceeded_ShouldReject()
    {
        var settings = CreateTestSettings(s => s.MaxNetExposurePercent = 20m);
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        // Bestehende Position: 0.1 BTC * 50000 = 5000 USDT = 50% Exposure
        var positions = new List<Position>
        {
            new("BTC-USDT", Side.Buy, 50000m, 50000m, 0.1m, 0m, 10m, MarginType.Cross, DateTime.UtcNow)
        };
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m, customPositions: positions, symbol: "ETH-USDT"));
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Exposure");
    }

    [Fact]
    public void ValidateTrade_LiquidationTooClose_ShouldReject()
    {
        // 10x Leverage → Liquidation ~9.6% entfernt. MinLiquidationDistance=15% → blockiert
        var settings = CreateTestSettings(s => { s.MinLiquidationDistancePercent = 15m; s.MaxLeverage = 10m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Liquidation");
    }

    [Fact]
    public void ValidateTrade_AdverseFunding_ShouldReject()
    {
        var settings = CreateTestSettings(s =>
        {
            s.ConsiderFundingRate = true;
            s.MaxAdverseFundingRatePercent = 0.05m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        // Long bei positiver Funding (0.1% > 0.05% Max) → blockiert
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m), currentFundingRate: 0.001m);
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Funding");
    }

    [Fact]
    public void ValidateTrade_FavorableFunding_ShouldAllow()
    {
        var settings = CreateTestSettings(s =>
        {
            s.ConsiderFundingRate = true;
            s.MaxAdverseFundingRatePercent = 0.05m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        // Long bei negativer Funding → günstig, nicht blockiert
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m), currentFundingRate: -0.001m);
        result.IsAllowed.Should().BeTrue();
    }
}
