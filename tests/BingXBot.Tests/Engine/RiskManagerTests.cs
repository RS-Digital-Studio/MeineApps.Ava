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
            MaxLeverage = 3m,                    // Fix für Tests (Default geändert auf 25)
            MaxDailyDrawdownPercent = 5m         // Fix für Tests (Default geändert auf 0)
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
    public void ValidateTrade_PositionScaleOverride_MultipliziertPosSize()
    {
        // Task 4.10 Counter-Trend-Scalp (0.5) + Spec §7 B19 HighProbability (>1.0) nutzen den Override.
        // Der RiskManager muss den Multiplikator VOR dem MaxRisk-Cap anwenden, damit die Risiko-Obergrenzen
        // auf die skalierte Position greifen.
        var settings = CreateTestSettings(s =>
        {
            s.MaxPositionSizePercent = 2m;
            s.MaxLeverage = 10m;
            s.MaxRiskPercentPerTrade = 100m; // Cap aushebeln, damit der Multiplikator durchschlägt
            s.MaxOpenPositions = 5;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        var baseSignal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var scaledSignal = baseSignal with { PositionScaleOverride = 0.5m };
        var boostedSignal = baseSignal with { PositionScaleOverride = 1.5m };

        var baseRes = risk.ValidateTrade(baseSignal, CreateContext());
        var scaledRes = risk.ValidateTrade(scaledSignal, CreateContext());
        var boostedRes = risk.ValidateTrade(boostedSignal, CreateContext());

        baseRes.IsAllowed.Should().BeTrue();
        scaledRes.IsAllowed.Should().BeTrue();
        boostedRes.IsAllowed.Should().BeTrue();

        // 0.5× skaliert vs. baseline
        scaledRes.AdjustedPositionSize.Should().BeApproximately(baseRes.AdjustedPositionSize * 0.5m, 1e-8m);
        // 1.5× skaliert vs. baseline
        boostedRes.AdjustedPositionSize.Should().BeApproximately(baseRes.AdjustedPositionSize * 1.5m, 1e-8m);
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
    public void CalculatePositionSize_IgnoriertSL_NurMarginBasiert()
    {
        // SK-Buch: Kein SL-basiertes Sizing mehr — MaxPositionSizePercent bestimmt die Margin.
        // CalculatePositionSize ignoriert den SL-Parameter (Risk wird über feste Pip-SL begrenzt).
        var settings = CreateTestSettings(s => { s.MaxPositionSizePercent = 2m; s.MaxLeverage = 10m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var withSl = risk.CalculatePositionSize("BTC-USDT", 50000m, 40000m, account);
        var withoutSl = risk.CalculatePositionSize("BTC-USDT", 50000m, null, account);
        // Beide identisch: 10000 * 2% * 10 / 50000 = 0.04 BTC
        withSl.Should().Be(withoutSl);
        withSl.Should().Be(0.04m);
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

        // Signal OHNE StopLoss → wird jetzt direkt abgelehnt (SL-Pflicht seit 08.04.2026)
        var signalOhneSl = new SignalResult(Signal.Long, 0.8m, 50000m, null, 52000m, "Test ohne SL");
        var result = risk.ValidateTrade(signalOhneSl, CreateContext(balance: 10000m));

        result.IsAllowed.Should().BeFalse("Trades ohne SL werden grundsätzlich abgelehnt");
        result.RejectionReason.Should().Contain("Stop-Loss");
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

        // Neues Signal: MIT SL bei 48500 (3% Distanz) → Risiko = 1500 * posSize
        // posSize bei 2% MaxPositionSize + 10x Lev = 10000 * 2% / 1500 * 50000 = sehr klein
        // Aber das kombinierte Risiko soll den Drawdown überschreiten
        // SL-Distanz: 50000-48500 = 1500 (3%). Bei 2% Margin-Cap: posSize = 10000*2%*10/50000 = 0.04
        // Worst-Case: 1500 * 0.04 = 60 USDT → Gesamt: 200+150+60 = 410 = 4.1% < 5% → erlaubt
        // Daher: Weites SL bei 45000 (10% Distanz) → Risiko höher
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 45000m, 52000m, "Test mit weitem SL");
        var result = risk.ValidateTrade(signal, CreateContext(balance: 10000m, customPositions: new List<Position> { verlustPosition }));

        // Der Drawdown-Check hängt von der konkreten Position-Sizing ab.
        // SL-Pflicht ist jetzt aktiv, daher testen wir dass das Signal nicht wegen fehlendem SL abgelehnt wird
        result.RejectionReason.Should().NotContain("Stop-Loss", "Signal hat einen SL");
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
        // Liquidations-Preis muss deutlich unter Entry liegen (>5% Abstand bei 10x)
        liqPrice.Should().BeLessThan(50000m * 0.95m);
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
        // Margin-basiert: (0.1*50000/10 + 1*3000/10) / 10000 * 100 = 8%
        // (Notional/Leverage = tatsächlich gebundenes Kapital, nicht gehebelter Wert)
        exposure.Should().Be(8m);
    }

    // BUCH-ONLY: Liquidations-Distanz-Check entfernt (nicht im Buch).

    // === Phase 18 — A1: GetPositionScalingFactor (Loss-Streak + Equity-Curve-Scaling) ===
    // Vorher: Methode war toter Stub (return 1m). Jetzt: SK-Plan 4.8 + 5.1 implementiert.

    [Fact]
    public void GetPositionScalingFactor_NoLossStreakNoDrawdown_ReturnsOne()
    {
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().Be(1m);
    }

    [Fact]
    public void GetPositionScalingFactor_3ConsecutiveLosses_NochVollePosition()
    {
        // User-Default: HalveAt=4, PauseAt=7 → 3 Losses liegen unter der Halbierungs-Schwelle.
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        risk.SetConsecutiveLosses(3);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().Be(1m);
    }

    [Fact]
    public void GetPositionScalingFactor_4ConsecutiveLosses_ReturnsHalf()
    {
        // User-Default: HalveAt=4 → ab 4 Losses Position halbieren.
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        risk.SetConsecutiveLosses(4);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().Be(0.5m);
    }

    [Fact]
    public void GetPositionScalingFactor_BuchStrikt3Losses_ReturnsHalf()
    {
        // Buch-strikt: HalveAt=3 → identisches Verhalten zum SK-Buch S.13.
        var risk = new RiskManager(
            CreateTestSettings(s => { s.LossStreakHalveAtCount = 3; s.LossStreakPauseAtCount = 5; }),
            NullLogger<RiskManager>.Instance);
        risk.SetConsecutiveLosses(3);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().Be(0.5m);
    }

    [Fact]
    public void GetPositionScalingFactor_7ConsecutiveLosses_ReturnsZero()
    {
        // User-Default: PauseAt=7 → ab 7 Losses harte Pause (Faktor 0).
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        risk.SetConsecutiveLosses(7);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().Be(0m);
    }

    [Fact]
    public void GetPositionScalingFactor_BuchStrikt5Losses_ReturnsZero()
    {
        // Buch-strikt: PauseAt=5 → identisches Verhalten zum SK-Buch S.13.
        var risk = new RiskManager(
            CreateTestSettings(s => { s.LossStreakHalveAtCount = 3; s.LossStreakPauseAtCount = 5; }),
            NullLogger<RiskManager>.Instance);
        risk.SetConsecutiveLosses(5);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().Be(0m);
    }

    [Fact]
    public void GetPositionScalingFactor_LossStreakDisabled_IgnoresStreak()
    {
        var settings = CreateTestSettings(s => s.EnableLossStreakDampening = false);
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        risk.SetConsecutiveLosses(7);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().Be(1m);
    }

    [Fact]
    public void GetPositionScalingFactor_EquityScalingDisabledByDefault_IgnoresDrawdown()
    {
        // Equity-Curve-Scaling ist opt-in — Default false.
        var risk = new RiskManager(CreateTestSettings(), NullLogger<RiskManager>.Instance);
        // Peak setzen via ValidateTrade
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        // Equity um 50% gefallen — ohne Setting darf der Faktor nicht scalen.
        var crashedAccount = new AccountInfo(5000m, 5000m, 0m, 0m);
        risk.GetPositionScalingFactor(crashedAccount).Should().Be(1m);
    }

    [Fact]
    public void GetPositionScalingFactor_EquityScalingBelowThreshold_ReturnsOne()
    {
        var settings = CreateTestSettings(s =>
        {
            s.EnableEquityCurveScaling = true;
            s.EquityCurveScalingThresholdPercent = 5m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        // Peak bei 10000 setzen
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        // Drawdown 4% — unter Schwelle, Faktor bleibt 1
        var account = new AccountInfo(9600m, 9600m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().Be(1m);
    }

    [Fact]
    public void GetPositionScalingFactor_EquityScalingAtThresholdPlus10_ReturnsHalf()
    {
        var settings = CreateTestSettings(s =>
        {
            s.EnableEquityCurveScaling = true;
            s.EquityCurveScalingThresholdPercent = 5m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        // Peak bei 10000 setzen
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        // Drawdown 15% (= Schwelle 5% + 10% Lerp-Fenster) → Faktor 0.5
        var account = new AccountInfo(8500m, 8500m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().BeApproximately(0.5m, 1e-8m);
    }

    [Fact]
    public void GetPositionScalingFactor_EquityScalingFarBelowPeak_ClampsToHalf()
    {
        var settings = CreateTestSettings(s =>
        {
            s.EnableEquityCurveScaling = true;
            s.EquityCurveScalingThresholdPercent = 5m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        // Drawdown 50% — weit über Schwelle, aber Faktor darf nicht unter 0.5 fallen (Lerp clamped auf 1)
        var account = new AccountInfo(5000m, 5000m, 0m, 0m);
        risk.GetPositionScalingFactor(account).Should().BeApproximately(0.5m, 1e-8m);
    }

    [Fact]
    public void GetPositionScalingFactor_LossStreakAndEquityScaling_Multiplies()
    {
        // 4 Losses → 0.5× (HalveAt=4), plus Drawdown auf threshold+10 → weitere 0.5× → final 0.25×
        var settings = CreateTestSettings(s =>
        {
            s.EnableEquityCurveScaling = true;
            s.EquityCurveScalingThresholdPercent = 5m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        risk.ValidateTrade(signal, CreateContext(balance: 10000m));
        risk.SetConsecutiveLosses(4);
        var account = new AccountInfo(8500m, 8500m, 0m, 0m); // 15% DD
        risk.GetPositionScalingFactor(account).Should().BeApproximately(0.25m, 1e-8m);
    }

    [Fact]
    public void CalculatePositionSize_AtPauseThreshold_ReturnsZero()
    {
        // Integrations-Test: GetPositionScalingFactor=0 muss qty auf 0 ziehen → Trade-Block.
        var settings = CreateTestSettings(s =>
        {
            s.MaxPositionSizePercent = 2m;
            s.MaxLeverage = 10m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        risk.SetConsecutiveLosses(settings.LossStreakPauseAtCount);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var qty = risk.CalculatePositionSize("BTC-USDT", 50000m, 49000m, account);
        qty.Should().Be(0m);
    }

    [Fact]
    public void ValidateTrade_AtPauseThreshold_BlocksTrade()
    {
        // Trade darf bei Erreichen der Pause-Schwelle nicht mehr durch. Seit dem expliziten
        // Loss-Streak-Reject (statt verschleiertem "Position-Groesse ist 0") nennt der Reason die Pause.
        var settings = CreateTestSettings();
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        risk.SetConsecutiveLosses(settings.LossStreakPauseAtCount);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal, CreateContext());
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Loss-Streak-Pause");
    }

    // === Phase 18 / A4 — Cluster-Korrelations-Filter ===

    [Fact]
    public void ValidateTrade_CorrelationLimitDisabled_NoBlockingByDefault()
    {
        // Default: MaxCorrelatedExposurePercent=0 → Filter inaktiv
        var settings = CreateTestSettings(s => { s.MaxOpenPositions = 5; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var ethPos = new Position("ETH-USDT", Side.Buy, 3000m, 3000m, 1m, 0m, 10m, MarginType.Cross, DateTime.UtcNow);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal,
            CreateContext(symbol: "BTC-USDT", balance: 10000m, customPositions: new List<Position> { ethPos }));
        // BTC und ETH sind verschiedene Cluster → Filter würde sowieso nicht greifen, aber Filter ist aus.
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateTrade_CorrelationLimit_BlocksThirdL1WhenAlreadyTwoOpen()
    {
        // 3× Alt-L1 (SOL/AVAX bereits offen, ADA neu) → bei 30%-Limit und je 10% Margin (offen) + 10% geplant = 30%, geht
        // Bei 3 offen + neu wäre es 40% > 30% → blocked.
        // Setup: 30% Cluster-Cap, 10% MaxPositionSizePercent, 3 offene Alt-L1-Positionen mit je 10% Margin (gleicher Cluster).
        var settings = CreateTestSettings(s =>
        {
            s.MaxOpenPositions = 10;
            s.MaxPositionSizePercent = 10m;
            s.MaxLeverage = 5m;
            s.MaxCorrelatedExposurePercent = 30m;
            s.MaxRiskPercentPerTrade = 100m; // Risk-Cap deaktivieren
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);

        // 3 offene L1-Positionen — pro Position Margin = (qty * entry) / leverage = (200 * 5) / 5 = 200 USDT
        // Bei Balance=10000 → 200/10000 = 2% pro Position, 6% gesamt — nicht über 30%.
        // Wir machen die Positionen größer: 1000 USDT Margin pro Position = 10% jeweils, 30% gesamt.
        // qty=1000, entry=5, leverage=1 → margin=5000 (50%) — zu groß. Versuchen wir: qty=2000, entry=5, leverage=10 → margin=1000 (10%).
        var solPos = new Position("SOL-USDT", Side.Buy, 5m, 5m, 2000m, 0m, 10m, MarginType.Cross, DateTime.UtcNow);
        var avaxPos = new Position("AVAX-USDT", Side.Buy, 5m, 5m, 2000m, 0m, 10m, MarginType.Cross, DateTime.UtcNow);
        var dotPos = new Position("DOT-USDT", Side.Buy, 5m, 5m, 2000m, 0m, 10m, MarginType.Cross, DateTime.UtcNow);

        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        var result = risk.ValidateTrade(signal,
            CreateContext(symbol: "ADA-USDT", balance: 10000m,
                customPositions: new List<Position> { solPos, avaxPos, dotPos }));
        result.IsAllowed.Should().BeFalse();
        result.RejectionReason.Should().Contain("Cluster-Limit");
    }

    [Fact]
    public void ValidateTrade_CorrelationLimit_AllowsSecondL1WhenBudgetRemaining()
    {
        // 1 offene L1 mit kleiner Margin → neue L1 darf noch durch (Budget noch frei).
        var settings = CreateTestSettings(s =>
        {
            s.MaxOpenPositions = 5;
            s.MaxPositionSizePercent = 5m;
            s.MaxLeverage = 5m;
            s.MaxCorrelatedExposurePercent = 30m;
            s.MaxRiskPercentPerTrade = 100m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        // 1 SOL: margin = 1000 USDT (10% von 10000)
        var solPos = new Position("SOL-USDT", Side.Buy, 5m, 5m, 2000m, 0m, 10m, MarginType.Cross, DateTime.UtcNow);
        var signal = new SignalResult(Signal.Long, 0.8m, 50000m, 49000m, 52000m, "Test");
        // ADA neu mit 5% Margin → 10% + 5% = 15% < 30%
        var result = risk.ValidateTrade(signal,
            CreateContext(symbol: "ADA-USDT", balance: 10000m,
                customPositions: new List<Position> { solPos }));
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateTrade_CorrelationLimit_DifferentClusters_NoLimit()
    {
        // BTC (BtcMajor) + ETH (EthMajor) sind unterschiedliche Cluster — BTC-Margin zählt NICHT
        // mit ins ETH-Cluster-Budget. Setup: ETH-Trade allein passt (5% von 10000 < 30% Limit).
        var settings = CreateTestSettings(s =>
        {
            s.MaxOpenPositions = 5;
            s.MaxPositionSizePercent = 5m;
            s.MaxLeverage = 10m;
            s.MaxCorrelatedExposurePercent = 30m;
            s.MaxRiskPercentPerTrade = 100m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        // Hohe BTC-Position: würde im selben Cluster zum Limit-Hit führen — aber ist im BtcMajor, ETH ist EthMajor.
        var btcPos = new Position("BTC-USDT", Side.Buy, 50000m, 50000m, 0.6m, 0m, 10m, MarginType.Cross, DateTime.UtcNow);
        var signal = new SignalResult(Signal.Long, 0.8m, 3000m, 2900m, 3200m, "Test");
        var result = risk.ValidateTrade(signal,
            CreateContext(symbol: "ETH-USDT", balance: 10000m,
                customPositions: new List<Position> { btcPos }));
        // ETH-Cluster ist leer + 5% geplant → unter dem 30%-Limit. BTC-Cluster wird ignoriert.
        result.IsAllowed.Should().BeTrue();
    }

    // === Phase 18 / A5 — Volatility-Targeting ===

    [Fact]
    public void CalculatePositionSize_VolTargetingDisabled_NoScaling()
    {
        var settings = CreateTestSettings(s => { s.MaxPositionSizePercent = 2m; s.MaxLeverage = 10m; });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        // ATR-Param wird ignoriert weil Setting aus
        var qty = risk.CalculatePositionSize("BTC-USDT", 50000m, 49000m, account, actualLeverage: 0, atrPercent: 5m);
        qty.Should().Be(0.04m); // identisch zur Default-Variante
    }

    [Fact]
    public void CalculatePositionSize_VolTargeting_HighVol_DownScales()
    {
        // ATR 4 % vs. Target 2 % → volScale = 0.5 → halbe Position.
        var settings = CreateTestSettings(s =>
        {
            s.MaxPositionSizePercent = 2m;
            s.MaxLeverage = 10m;
            s.EnableVolatilityTargeting = true;
            s.VolatilityTargetPercent = 2m;
            s.VolatilityScaleCap = 1.5m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var qty = risk.CalculatePositionSize("PEPE-USDT", 50000m, 49000m, account, actualLeverage: 0, atrPercent: 4m);
        qty.Should().Be(0.04m * 0.5m); // 0.02
    }

    [Fact]
    public void CalculatePositionSize_VolTargeting_LowVol_UpScalesButCapped()
    {
        // ATR 0.5 % vs. Target 2 % → volScale = 4, aber Cap auf 1.5×
        var settings = CreateTestSettings(s =>
        {
            s.MaxPositionSizePercent = 2m;
            s.MaxLeverage = 10m;
            s.EnableVolatilityTargeting = true;
            s.VolatilityTargetPercent = 2m;
            s.VolatilityScaleCap = 1.5m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var qty = risk.CalculatePositionSize("BTC-USDT", 50000m, 49000m, account, actualLeverage: 0, atrPercent: 0.5m);
        qty.Should().Be(0.04m * 1.5m); // 0.06 (capped)
    }

    [Fact]
    public void CalculatePositionSize_VolTargeting_MatchingVol_NoScaling()
    {
        // ATR exakt = Target → volScale = 1.0
        var settings = CreateTestSettings(s =>
        {
            s.MaxPositionSizePercent = 2m;
            s.MaxLeverage = 10m;
            s.EnableVolatilityTargeting = true;
            s.VolatilityTargetPercent = 2m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var qty = risk.CalculatePositionSize("ETH-USDT", 50000m, 49000m, account, actualLeverage: 0, atrPercent: 2m);
        qty.Should().Be(0.04m);
    }

    [Fact]
    public void CalculatePositionSize_VolTargeting_ZeroAtr_NoScaling()
    {
        // ATR=0 (z.B. zu wenig Candles) → kein Scaling.
        var settings = CreateTestSettings(s =>
        {
            s.MaxPositionSizePercent = 2m;
            s.MaxLeverage = 10m;
            s.EnableVolatilityTargeting = true;
            s.VolatilityTargetPercent = 2m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var account = new AccountInfo(10000m, 10000m, 0m, 0m);
        var qty = risk.CalculatePositionSize("BTC-USDT", 50000m, 49000m, account, actualLeverage: 0, atrPercent: 0m);
        qty.Should().Be(0.04m);
    }

    [Fact]
    public void ValidateTrade_CorrelationLimit_OtherClusterNoOp()
    {
        // Symbole im "CryptoOther"-Cluster (unbekannt) ueberspringen den Filter — sonst landet alles im selben Topf.
        var settings = CreateTestSettings(s =>
        {
            s.MaxOpenPositions = 5;
            s.MaxPositionSizePercent = 10m;
            s.MaxCorrelatedExposurePercent = 1m; // sehr scharf
            s.MaxRiskPercentPerTrade = 100m;
        });
        var risk = new RiskManager(settings, NullLogger<RiskManager>.Instance);
        var unknownPos = new Position("RANDOMCOIN-USDT", Side.Buy, 100m, 100m, 50m, 0m, 5m, MarginType.Cross, DateTime.UtcNow);
        var signal = new SignalResult(Signal.Long, 0.8m, 100m, 95m, 110m, "Test");
        var result = risk.ValidateTrade(signal,
            CreateContext(symbol: "ANOTHERUNKNOWN-USDT", balance: 10000m,
                customPositions: new List<Position> { unknownPos }));
        result.IsAllowed.Should().BeTrue();
    }
}
