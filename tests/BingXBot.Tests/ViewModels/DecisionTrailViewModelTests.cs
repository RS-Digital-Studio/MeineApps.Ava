using BingXBot.Contracts.Dto;
using BingXBot.Core.Diagnostics;
using BingXBot.Core.Enums;
using BingXBot.ViewModels;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.ViewModels;

// v1.6.0 Phase 10A — DecisionTrailViewModel.
public class DecisionTrailViewModelTests
{
    [Fact]
    public void LoadInitial_PopulatesDecisions()
    {
        var vm = new DecisionTrailViewModel();
        var batch = new[]
        {
            MakeDto("BTC-USDT", TimeFrame.H1, ts: DateTime.UtcNow),
            MakeDto("ETH-USDT", TimeFrame.H4, ts: DateTime.UtcNow.AddMinutes(-5)),
        };
        vm.LoadInitial(batch);

        // Background-Dispatch — eventuell synchron in xunit, aber wir warten kurz.
        WaitForDispatcher();

        vm.Decisions.Count.Should().Be(2);
        vm.Decisions[0].Symbol.Should().Be("BTC-USDT", "neueste zuerst");
    }

    [Fact]
    public void Filter_BySymbol_ReducesList()
    {
        var vm = new DecisionTrailViewModel();
        vm.LoadInitial(new[]
        {
            MakeDto("BTC-USDT", TimeFrame.H1),
            MakeDto("ETH-USDT", TimeFrame.H1),
            MakeDto("BTC-USDT", TimeFrame.H4),
        });
        WaitForDispatcher();

        vm.SelectedSymbol = "BTC-USDT";
        WaitForDispatcher();
        vm.FilteredDecisions.Count.Should().Be(2);
        vm.FilteredDecisions.Should().AllSatisfy(d => d.Symbol.Should().Be("BTC-USDT"));
    }

    [Fact]
    public void Filter_OnlyRejected_HidesSuccess()
    {
        var vm = new DecisionTrailViewModel();
        vm.LoadInitial(new[]
        {
            MakeDto("BTC-USDT", TimeFrame.H1, triggered: true),
            MakeDto("BTC-USDT", TimeFrame.H1, reason: RejectionReasons.NoHtfConfluence),
            MakeDto("BTC-USDT", TimeFrame.H1, triggered: true),
        });
        WaitForDispatcher();

        vm.OnlyRejected = true;
        WaitForDispatcher();
        vm.FilteredDecisions.Count.Should().Be(1);
        vm.FilteredDecisions[0].Triggered.Should().BeFalse();
    }

    [Fact]
    public void MaxItems_TrimmedAt500()
    {
        var vm = new DecisionTrailViewModel();
        var batch = Enumerable.Range(0, 600)
            .Select(i => MakeDto("BTC-USDT", TimeFrame.H1, ts: DateTime.UtcNow.AddSeconds(i)))
            .ToList();
        vm.LoadInitial(batch);
        WaitForDispatcher();

        vm.Decisions.Count.Should().Be(DecisionTrailViewModel.MaxItems,
            "Plan-Spez: max 500 Eintraege im UI-Buffer");
    }

    [Fact]
    public void AvailableReasons_ContainsAllPlanCodes()
    {
        var vm = new DecisionTrailViewModel();
        vm.AvailableReasons.Should().Contain(RejectionReasons.NoHtfConfluence);
        vm.AvailableReasons.Should().Contain(RejectionReasons.RrrTooSmall);
        vm.AvailableReasons.Should().Contain(RejectionReasons.SlippageTooHigh);
        vm.AvailableReasons.Should().Contain(RejectionReasons.TfAutoDisabled);
    }

    private static EvaluationDecisionDto MakeDto(string symbol, TimeFrame tf,
        DateTime? ts = null, bool triggered = false, string? reason = null) =>
        new(
            UtcTimestamp: ts ?? DateTime.UtcNow,
            Symbol: symbol,
            Tf: (int)tf,
            SequenceState: "Aktiviert",
            Point0: 100m, PointA: 110m, PointB: 105m,
            Triggered: triggered,
            RejectionReason: triggered ? null : (reason ?? RejectionReasons.Other),
            ConfluenceScore: 5,
            ConfluenceCategories: Array.Empty<string>(),
            HardFiltersFailed: triggered ? Array.Empty<string>() : new[] { reason ?? RejectionReasons.Other });

    /// <summary>Dispatcher.UIThread.Post braucht einen Run-Zyklus — in xunit ohne Avalonia-App
    /// laufen die Posts synchron, aber zur Sicherheit ein kleiner Sleep.</summary>
    private static void WaitForDispatcher()
    {
        Thread.Sleep(20);
    }
}
