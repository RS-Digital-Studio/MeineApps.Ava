using BingXBot.Core.Diagnostics;
using BingXBot.Core.Enums;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Diagnostics;

// v1.5.2 — Decision-Trail / Rejection-Log.
//
// In-Memory-Ringpuffer-Tests: Append, Trim bei Capacity, Filter, Disable-Pfad.
// Hot-Path-Integration in der Strategy ist via Backtest-Suite getestet — hier liegt der
// Fokus auf der reinen Buffer-Mechanik + Konstanten.
public class DecisionTrailBufferTests
{
    [Fact]
    public void Append_AddsEntry()
    {
        var buffer = new DecisionTrailBuffer(capacity: 100);
        buffer.Append(MakeDecision("BTC-USDT", TimeFrame.H1, triggered: true));
        buffer.Count.Should().Be(1);
        buffer.AppendedCount.Should().Be(1);
    }

    [Fact]
    public void RingBuffer_TrimsAtLimit()
    {
        var buffer = new DecisionTrailBuffer(capacity: 3);
        buffer.Append(MakeDecision("A", TimeFrame.H1, triggered: false, reason: RejectionReasons.NewsBlackout));
        buffer.Append(MakeDecision("B", TimeFrame.H1, triggered: false, reason: RejectionReasons.NoHtfConfluence));
        buffer.Append(MakeDecision("C", TimeFrame.H1, triggered: false, reason: RejectionReasons.RrrTooSmall));
        buffer.Append(MakeDecision("D", TimeFrame.H1, triggered: true));

        // Capacity 3 → A wurde verworfen.
        buffer.Count.Should().Be(3);
        buffer.AppendedCount.Should().Be(4);
        var latest = buffer.GetLatest(10);
        latest.Should().NotContain(d => d.Symbol == "A");
        latest.Should().Contain(d => d.Symbol == "D");
    }

    [Fact]
    public void Filter_BySymbol_Korrekt()
    {
        var buffer = new DecisionTrailBuffer();
        buffer.Append(MakeDecision("BTC-USDT", TimeFrame.H1));
        buffer.Append(MakeDecision("ETH-USDT", TimeFrame.H1));
        buffer.Append(MakeDecision("BTC-USDT", TimeFrame.H4));

        var btcOnly = buffer.Filter(symbol: "BTC-USDT");
        btcOnly.Should().HaveCount(2);
        btcOnly.Should().AllSatisfy(d => d.Symbol.Should().Be("BTC-USDT"));
    }

    [Fact]
    public void Filter_ByTf_Korrekt()
    {
        var buffer = new DecisionTrailBuffer();
        buffer.Append(MakeDecision("BTC-USDT", TimeFrame.H1));
        buffer.Append(MakeDecision("BTC-USDT", TimeFrame.H4));
        buffer.Append(MakeDecision("BTC-USDT", TimeFrame.D1));

        var h4Only = buffer.Filter(tf: TimeFrame.H4);
        h4Only.Should().HaveCount(1);
        h4Only[0].Tf.Should().Be(TimeFrame.H4);
    }

    [Fact]
    public void Filter_ByRejectionReason_Korrekt()
    {
        var buffer = new DecisionTrailBuffer();
        buffer.Append(MakeDecision("A", TimeFrame.H1, triggered: false, reason: RejectionReasons.NoHtfConfluence));
        buffer.Append(MakeDecision("B", TimeFrame.H1, triggered: false, reason: RejectionReasons.RrrTooSmall));
        buffer.Append(MakeDecision("C", TimeFrame.H1, triggered: false, reason: RejectionReasons.NoHtfConfluence));

        var noHtf = buffer.Filter(rejectionReason: RejectionReasons.NoHtfConfluence);
        noHtf.Should().HaveCount(2);
    }

    [Fact]
    public void Filter_BySince_FiltertAlteEintraege()
    {
        var buffer = new DecisionTrailBuffer();
        var oldDecision = MakeDecision("OLD", TimeFrame.H1) with { UtcTimestamp = DateTime.UtcNow.AddHours(-2) };
        var newDecision = MakeDecision("NEW", TimeFrame.H1) with { UtcTimestamp = DateTime.UtcNow };
        buffer.Append(oldDecision);
        buffer.Append(newDecision);

        var sinceLastHour = buffer.Filter(since: DateTime.UtcNow.AddHours(-1));
        sinceLastHour.Should().HaveCount(1);
        sinceLastHour[0].Symbol.Should().Be("NEW");
    }

    [Fact]
    public void GetLatest_OrderJuengsteZuerst()
    {
        var buffer = new DecisionTrailBuffer();
        buffer.Append(MakeDecision("FIRST", TimeFrame.H1));
        buffer.Append(MakeDecision("SECOND", TimeFrame.H1));
        buffer.Append(MakeDecision("THIRD", TimeFrame.H1));

        var latest = buffer.GetLatest(10);
        latest[0].Symbol.Should().Be("THIRD");
        latest[1].Symbol.Should().Be("SECOND");
        latest[2].Symbol.Should().Be("FIRST");
    }

    [Fact]
    public void Clear_LeertBuffer()
    {
        var buffer = new DecisionTrailBuffer();
        buffer.Append(MakeDecision("BTC-USDT", TimeFrame.H1));
        buffer.Append(MakeDecision("ETH-USDT", TimeFrame.H1));
        buffer.Clear();
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void RejectionReasons_KonstantenStabil()
    {
        // Diese Codes sind stabile Stringkonstanten — ein versehentlicher Rename
        // bricht UI-Filter und persistente Einträge.
        RejectionReasons.NewsBlackout.Should().Be("news_blackout");
        RejectionReasons.NoHtfConfluence.Should().Be("no_htf_confluence");
        RejectionReasons.ScoreBelowMin.Should().Be("score_below_min");
        RejectionReasons.RrrTooSmall.Should().Be("rrr_too_small");
        RejectionReasons.MtaTargetZoneBlock.Should().Be("mta_target_zone_block");
        RejectionReasons.StateNotActivated.Should().Be("state_not_activated");
        RejectionReasons.Other.Should().Be("other");
    }

    [Fact]
    public void EvaluationDecision_Triggered_KeinRejectionReason()
    {
        var d = MakeDecision("BTC-USDT", TimeFrame.H1, triggered: true);
        d.Triggered.Should().BeTrue();
        d.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void EvaluationDecision_Reject_HatRejectionReason()
    {
        var d = MakeDecision("BTC-USDT", TimeFrame.H1, triggered: false, reason: RejectionReasons.RrrTooSmall);
        d.Triggered.Should().BeFalse();
        d.RejectionReason.Should().Be(RejectionReasons.RrrTooSmall);
    }

    [Fact]
    public void Capacity_Validierung_AbweisendBeiNullOderNegativ()
    {
        Action act1 = () => new DecisionTrailBuffer(0);
        act1.Should().Throw<ArgumentOutOfRangeException>();
        Action act2 = () => new DecisionTrailBuffer(-1);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static EvaluationDecision MakeDecision(string symbol, TimeFrame tf,
        bool triggered = false, string? reason = null) =>
        new(
            Symbol: symbol,
            Tf: tf,
            UtcTimestamp: DateTime.UtcNow,
            SequenceState: "SucheB",
            Point0: 100m,
            PointA: 110m,
            PointB: 105m,
            Triggered: triggered,
            RejectionReason: triggered ? null : (reason ?? RejectionReasons.Other),
            ConfluenceScore: 5,
            ConfluenceCategories: new[] { "Fibonacci", "Volume" },
            HardFiltersFailed: triggered ? Array.Empty<string>() : new[] { reason ?? RejectionReasons.Other });
}
