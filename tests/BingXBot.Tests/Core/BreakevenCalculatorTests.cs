using BingXBot.Core.Enums;
using BingXBot.Core.Services;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

// Tests fuer den zentralen BE-Trigger-Calculator. Deckt beide Trigger ab:
//   1) A-Bruch (SK-Buch Masterclass, 0,5 % Puffer)
//   2) 2x SL-Distanz (User-Ausnahme seit 24.04.2026, 0,2 % Puffer)
// Live- und Backtest-Code teilen sich diese Logik — Tests hier sichern beide Pfade ab.
public class BreakevenCalculatorTests
{
    // ────────────────── A-Bruch (Prio 1, 0,5 %) ──────────────────

    [Fact]
    public void ABruch_Long_PreisErreichtNavPointA_FeuertMit05ProzentPuffer()
    {
        // Setup: Long-Position, Entry 100, SL 95 (SL-Distanz 5). NavPointA 105.
        // Price 105 erreicht NavPointA → BE feuert, Puffer 0,5 %.
        var result = BreakevenCalculator.Evaluate(Side.Buy, price: 105m, entryPrice: 100m, originalStopLoss: 95m, navPointA: 105m);

        result.Should().NotBeNull();
        result!.Value.NewStopLoss.Should().Be(100.5m); // 100 × 1.005
        result.Value.TriggerName.Should().StartWith("A-Bruch");
    }

    [Fact]
    public void ABruch_Short_PreisErreichtNavPointA_FeuertMit05ProzentPuffer()
    {
        var result = BreakevenCalculator.Evaluate(Side.Sell, price: 95m, entryPrice: 100m, originalStopLoss: 105m, navPointA: 95m);

        result.Should().NotBeNull();
        result!.Value.NewStopLoss.Should().Be(99.5m); // 100 × 0.995
        result.Value.TriggerName.Should().StartWith("A-Bruch");
    }

    [Fact]
    public void ABruch_Long_PreisDurchbrichtNavPointA_Feuert()
    {
        // Wick ueber NavPointA → auch hier muss BE feuern (Live-Tick-Verhalten).
        var result = BreakevenCalculator.Evaluate(Side.Buy, price: 107m, entryPrice: 100m, originalStopLoss: 95m, navPointA: 105m);

        result.Should().NotBeNull();
        result!.Value.NewStopLoss.Should().Be(100.5m);
    }

    [Fact]
    public void ABruch_Long_PreisUnterNavPointA_FeuertNichtWennAuch2xNichtErreicht()
    {
        // Price 103 < NavPointA 105, und 2x SL-Ziel = 100 + 2*5 = 110 — beide nicht erreicht.
        var result = BreakevenCalculator.Evaluate(Side.Buy, price: 103m, entryPrice: 100m, originalStopLoss: 95m, navPointA: 105m);

        result.Should().BeNull();
    }

    // ────────────────── 2x SL-Distanz (Prio 2, 0,2 %) ──────────────────

    [Fact]
    public void TwoXSl_Long_OhneNavPointA_FeuertBei2xDistanzMit02ProzentPuffer()
    {
        // Entry 100, SL 95 (Distanz 5). 2x-Ziel = 100 + 10 = 110. NavPointA = 0 (kein A-Bruch moeglich).
        var result = BreakevenCalculator.Evaluate(Side.Buy, price: 110m, entryPrice: 100m, originalStopLoss: 95m, navPointA: 0m);

        result.Should().NotBeNull();
        result!.Value.NewStopLoss.Should().Be(100.2m); // 100 × 1.002
        result.Value.TriggerName.Should().StartWith("2x SL-Distanz");
    }

    [Fact]
    public void TwoXSl_Short_OhneNavPointA_FeuertBei2xDistanzMit02ProzentPuffer()
    {
        // Entry 100, SL 105 (Distanz 5). 2x-Ziel = 100 − 10 = 90.
        var result = BreakevenCalculator.Evaluate(Side.Sell, price: 90m, entryPrice: 100m, originalStopLoss: 105m, navPointA: 0m);

        result.Should().NotBeNull();
        result!.Value.NewStopLoss.Should().Be(99.8m); // 100 × 0.998
        result.Value.TriggerName.Should().StartWith("2x SL-Distanz");
    }

    [Fact]
    public void TwoXSl_Long_PreisZwischen1xUnd2x_FeuertNicht()
    {
        // Price 107 liegt zwischen Entry (100) und 2x-Ziel (110) — aber darunter → kein Trigger.
        var result = BreakevenCalculator.Evaluate(Side.Buy, price: 107m, entryPrice: 100m, originalStopLoss: 95m, navPointA: 0m);

        result.Should().BeNull();
    }

    // ────────────────── Prio-Regel: beide Trigger gleichzeitig ──────────────────

    [Fact]
    public void BeideTrigger_Long_ABruchHatPrioritaetMit05Prozent()
    {
        // NavPointA = 105 liegt genau auf 1x SL-Distanz. 2x-Ziel = 110. Price 110 erreicht beide
        // gleichzeitig. A-Bruch muss gewinnen (0,5 %-Puffer), NICHT 0,2 %.
        var result = BreakevenCalculator.Evaluate(Side.Buy, price: 110m, entryPrice: 100m, originalStopLoss: 95m, navPointA: 105m);

        result.Should().NotBeNull();
        result!.Value.NewStopLoss.Should().Be(100.5m); // 0,5 % weil A-Bruch gewinnt
        result.Value.TriggerName.Should().StartWith("A-Bruch");
    }

    // ────────────────── Edge Cases & Guards ──────────────────

    [Fact]
    public void KeinTrigger_EntryPriceUngueltig_LiefertNull()
    {
        var result = BreakevenCalculator.Evaluate(Side.Buy, price: 110m, entryPrice: 0m, originalStopLoss: 95m, navPointA: 105m);
        result.Should().BeNull();
    }

    [Fact]
    public void KeinTrigger_OriginalSlUngueltig_LiefertNull()
    {
        var result = BreakevenCalculator.Evaluate(Side.Buy, price: 110m, entryPrice: 100m, originalStopLoss: 0m, navPointA: 105m);
        result.Should().BeNull();
    }

    [Fact]
    public void KeinTrigger_NavPointA0UndTwoXNichtErreicht_LiefertNull()
    {
        // Price 105 ist unter 2x-Ziel (110). Kein NavPointA → kein A-Bruch. Ergebnis: kein BE.
        var result = BreakevenCalculator.Evaluate(Side.Buy, price: 105m, entryPrice: 100m, originalStopLoss: 95m, navPointA: 0m);
        result.Should().BeNull();
    }

    [Fact]
    public void Konstanten_HabenDokumentierteWerte()
    {
        BreakevenCalculator.ABreakBufferPct.Should().Be(0.005m);
        BreakevenCalculator.TwoXSlBufferPct.Should().Be(0.002m);
    }
}
