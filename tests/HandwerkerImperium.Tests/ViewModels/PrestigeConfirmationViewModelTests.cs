using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using NSubstitute;

namespace HandwerkerImperium.Tests.ViewModels;

/// <summary>
/// AAA-Audit Review-Pass (12.05.2026): Tests fuer die Balance-Logik
/// <see cref="PrestigeConfirmationViewModel.CalculateEffectivePoints"/> —
/// Bronze-Mindestmenge, Prestige-Pass (+50%), Gilden-Forschung (+10%) und Kombinationen.
/// </summary>
public sealed class PrestigeConfirmationViewModelTests
{
    private static PrestigeConfirmationViewModel CreateVm(
        int basePoints = 100,
        bool prestigePassActive = false,
        decimal guildResearchBonus = 0m)
    {
        var state = new GameState
        {
            IsPrestigePassActive = prestigePassActive
        };
        if (guildResearchBonus > 0)
        {
            state.GuildMembership = new GuildMembership
            {
                ResearchPrestigePointBonus = guildResearchBonus
            };
        }

        var gameStateService = Substitute.For<IGameStateService>();
        gameStateService.State.Returns(state);

        var prestigeService = Substitute.For<IPrestigeService>();
        prestigeService.GetPrestigePoints(Arg.Any<decimal>()).Returns(basePoints);

        var loc = Substitute.For<ILocalizationService>();
        loc.GetString(Arg.Any<string>()).Returns(call => call.Arg<string>());

        var adService = Substitute.For<IAdService>();

        // DialogViewModel braucht weitere Services — wir mocken nur was nötig ist
        var storyService = Substitute.For<IStoryService>();
        var hintService = Substitute.For<IContextualHintService>();
        var dialogVm = new DialogViewModel(loc, storyService, hintService, gameStateService,
            prestigeService, adService);

        return dialogVm.PrestigeConfirmation;
    }

    [Fact]
    public void CalculateEffectivePoints_Bronze_AppliesPointMultiplier()
    {
        // basePoints 100 * Bronze.Multiplier (1.0) = 100, ueber Bronze-Mindestmenge 10
        var vm = CreateVm(basePoints: 100);
        Assert.Equal(100, vm.CalculateEffectivePoints(new GameState(), PrestigeTier.Bronze));
    }

    [Fact]
    public void CalculateEffectivePoints_Bronze_EnforcesMinimumOf10()
    {
        // basePoints 3 * Bronze.Multiplier (1.0) = 3, aber Bronze-Mindestmenge 10 greift
        var vm = CreateVm(basePoints: 3);
        Assert.Equal(10, vm.CalculateEffectivePoints(new GameState(), PrestigeTier.Bronze));
    }

    [Fact]
    public void CalculateEffectivePoints_Silver_NoBronzeMinimum()
    {
        // basePoints 3 * Silver.Multiplier — Mindestmenge gilt NUR fuer Bronze
        var vm = CreateVm(basePoints: 3);
        var silverMult = PrestigeTier.Silver.GetPointMultiplier();
        var expected = (int)(3 * silverMult);
        Assert.Equal(expected, vm.CalculateEffectivePoints(new GameState(), PrestigeTier.Silver));
    }

    [Fact]
    public void CalculateEffectivePoints_PrestigePassActive_AddsFiftyPercent()
    {
        // basePoints 100 * Bronze 1.0 = 100, dann *1.5 (Pass) = 150
        var vm = CreateVm(basePoints: 100, prestigePassActive: true);
        // Wichtig: VM liest aus _gameStateService.State, nicht aus state-Parameter (Quirk)
        var state = new GameState { IsPrestigePassActive = true };
        // Da Implementation den injected state ignoriert (nutzt _gameStateService.State),
        // passt der Wert aus dem Mock.
        Assert.Equal(150, vm.CalculateEffectivePoints(state, PrestigeTier.Bronze));
    }

    [Fact]
    public void CalculateEffectivePoints_GuildResearch_AddsResearchBonus()
    {
        // basePoints 100 * Bronze 1.0 = 100, dann *1.10 (Gilden) = 110
        var vm = CreateVm(basePoints: 100, guildResearchBonus: 0.10m);
        var state = new GameState();
        state.GuildMembership = new GuildMembership { ResearchPrestigePointBonus = 0.10m };
        Assert.Equal(110, vm.CalculateEffectivePoints(state, PrestigeTier.Bronze));
    }

    [Fact]
    public void CalculateEffectivePoints_PassAndGuildCombined_MultipliesBoth()
    {
        // basePoints 100 * Bronze 1.0 = 100
        // * Pass 1.5 = 150
        // * Gilden 1.10 = 165
        var vm = CreateVm(basePoints: 100, prestigePassActive: true, guildResearchBonus: 0.10m);
        var state = new GameState { IsPrestigePassActive = true };
        state.GuildMembership = new GuildMembership { ResearchPrestigePointBonus = 0.10m };
        Assert.Equal(165, vm.CalculateEffectivePoints(state, PrestigeTier.Bronze));
    }
}
