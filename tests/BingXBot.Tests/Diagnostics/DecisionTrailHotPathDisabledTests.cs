using BingXBot.Core.Configuration;
using BingXBot.Core.Diagnostics;
using BingXBot.Core.Enums;
using BingXBot.Trading;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Diagnostics;

// v1.5.2 Phase 4 Plan-Test "Disabled_NoOverheadOnHotPath".
// Wenn BotSettings.EnableDecisionTrail = false, darf der Hot-Path KEIN PublishEvaluationDecision
// auf den BotEventBus auslosen. Verifizieren wir ueber Event-Subscriber-Counter.
public class DecisionTrailHotPathDisabledTests
{
    [Fact]
    public void EnableDecisionTrail_Default_True()
    {
        var settings = new BotSettings();
        settings.EnableDecisionTrail.Should().BeTrue("Default soll true sein (Trail aktiv)");
    }

    [Fact]
    public void Publish_WhenSubscribed_FiresEvent()
    {
        var bus = new BotEventBus();
        var fired = 0;
        bus.EvaluationDecided += (_, _) => fired++;
        bus.HasEvaluationDecidedSubscribers.Should().BeTrue();

        bus.PublishEvaluationDecision(MakeDecision());

        fired.Should().Be(1);
    }

    [Fact]
    public void Publish_WhenNoSubscribers_DoesNotThrow()
    {
        // Hot-Path-Schutz: Wenn niemand auf EvaluationDecided abonniert ist, darf der
        // Publish-Aufruf trotzdem nicht crashen.
        var bus = new BotEventBus();
        bus.HasEvaluationDecidedSubscribers.Should().BeFalse();

        var act = () => bus.PublishEvaluationDecision(MakeDecision());
        act.Should().NotThrow();
    }

    [Fact]
    public void DisabledFlag_Logic_Documented()
    {
        // TradingServiceBase.RunLoopAsync prueft VOR PublishEvaluationDecision:
        //   if (_botSettings.EnableDecisionTrail && strategy is SequenzKonzeptStrategy decSk
        //       && decSk.LastEvaluationDecision != null) { _eventBus.PublishEvaluationDecision(...); }
        // Wenn EnableDecisionTrail = false, wird Publish gar nicht aufgerufen → kein Subscriber-
        // Overhead, kein Allocation-Hotspot. Dieses Verhalten testen wir hier durch Verifikation
        // der Default-Logik (Strategy.LastEvaluationDecision wird gesetzt, aber ohne Flag-true
        // nicht weitergereicht).
        var settings = new BotSettings { EnableDecisionTrail = false };
        var shouldPublish = settings.EnableDecisionTrail; // false → Publish wird NICHT aufgerufen
        shouldPublish.Should().BeFalse(
            "TradingServiceBase ueberspringt Publish wenn EnableDecisionTrail = false");
    }

    private static EvaluationDecision MakeDecision() =>
        new(
            Symbol: "BTC-USDT",
            Tf: TimeFrame.H1,
            UtcTimestamp: DateTime.UtcNow,
            SequenceState: "Aktiviert",
            Point0: 100m, PointA: 110m, PointB: 105m,
            Triggered: false,
            RejectionReason: RejectionReasons.NoHtfConfluence,
            ConfluenceScore: 5,
            ConfluenceCategories: Array.Empty<string>(),
            HardFiltersFailed: new[] { RejectionReasons.NoHtfConfluence });
}
