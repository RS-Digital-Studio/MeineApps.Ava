using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>
/// v1.5.0 Phase 2 — Asymmetrisches CRV (SL aus LTF, TP aus HTF).
///
/// Voll integrierte Strategy-Tests sind in der Backtest-Suite (FiveMonthLiveBacktest)
/// abgedeckt. Hier liegt der Fokus auf:
/// - SignalResult.TpSourceTimeframe als UI-Tracking-Feld
/// - RiskSettings.UseAsymmetricCrv Default + Flag-Toggle
/// - Sanity-Cap-Logik (Faktor &gt; 5 Hard-Cap auf LTF × 3) als reine Berechnung
/// </summary>
public class AsymmetricCrvTests
{
    [Fact]
    public void RiskSettings_UseAsymmetricCrv_DefaultFalse()
    {
        var rs = new RiskSettings();
        rs.UseAsymmetricCrv.Should().BeFalse("Default soll false sein (opt-in)");
    }

    [Fact]
    public void RiskSettings_UseAsymmetricCrv_RoundTrip()
    {
        var rs = new RiskSettings { UseAsymmetricCrv = true };
        rs.UseAsymmetricCrv.Should().BeTrue();
    }

    [Fact]
    public void SignalResult_TpSourceTimeframe_DefaultNull()
    {
        var sig = new SignalResult(
            Signal: Signal.Long, Confidence: 0.8m,
            EntryPrice: 50000m, StopLoss: 49000m, TakeProfit: 52000m,
            Reason: "Test");
        sig.TpSourceTimeframe.Should().BeNull();
    }

    [Fact]
    public void SignalResult_TpSourceTimeframe_KannGesetztWerden()
    {
        var sig = new SignalResult(
            Signal: Signal.Long, Confidence: 0.8m,
            EntryPrice: 50000m, StopLoss: 49000m, TakeProfit: 52000m,
            Reason: "Test", TpSourceTimeframe: TimeFrame.D1);
        sig.TpSourceTimeframe.Should().Be(TimeFrame.D1);
    }

    [Fact]
    public void SignalResult_TpSourceTimeframe_W1Setup()
    {
        var sig = new SignalResult(
            Signal: Signal.Short, Confidence: 0.7m,
            EntryPrice: 3000m, StopLoss: 3100m, TakeProfit: 2700m,
            Reason: "GKL-W1", IsGklSetup: true, GklTimeframe: TimeFrame.W1,
            TpSourceTimeframe: TimeFrame.W1);
        sig.TpSourceTimeframe.Should().Be(TimeFrame.W1);
        sig.IsGklSetup.Should().BeTrue();
    }

    [Fact]
    public void SanityCap_HtfTpDistanzGroesserAlsFaktor5_GecapptAuf3xLtf()
    {
        // Reine Berechnungs-Verifikation des Sanity-Caps (entspricht der Logik in
        // SequenzKonzeptStrategy.Evaluate). Long: Entry 100, LTF-Ext1.618 = 110 (Distanz 10),
        // HTF-Ext1.618 = 200 (Distanz 100 → Faktor 10× zu weit). Cap → 100 + 30 = 130.
        const decimal entry = 100m;
        const decimal ltfTp1 = 110m;
        const decimal htfTp1Raw = 200m;
        var ltfDist = ltfTp1 - entry;
        var htfDist = htfTp1Raw - entry;
        var capped = htfDist > ltfDist * 5m
            ? entry + ltfDist * 3m
            : htfTp1Raw;
        capped.Should().Be(130m);
    }

    [Fact]
    public void SanityCap_HtfTpInGuterRange_BleibtUnveraendert()
    {
        // Long: Entry 100, LTF-Ext1.618 = 110 (Distanz 10), HTF-Ext1.618 = 130 (Distanz 30 → Faktor 3×).
        // 3× ≤ 5× → kein Cap, HTF bleibt.
        const decimal entry = 100m;
        const decimal ltfTp1 = 110m;
        const decimal htfTp1Raw = 130m;
        var ltfDist = ltfTp1 - entry;
        var htfDist = htfTp1Raw - entry;
        var capped = htfDist > ltfDist * 5m
            ? entry + ltfDist * 3m
            : htfTp1Raw;
        capped.Should().Be(130m);
    }
}
