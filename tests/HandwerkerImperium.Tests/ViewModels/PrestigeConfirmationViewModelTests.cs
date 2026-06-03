using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using NSubstitute;

namespace HandwerkerImperium.Tests.ViewModels;

/// <summary>
/// Tests fuer die Prestige-Punkte-Balance-Logik. Seit dem Refactor delegiert
/// <see cref="PrestigeConfirmationViewModel.CalculateEffectivePoints"/> an
/// <see cref="IPrestigeService.CalculateTotalPrestigePoints"/> — daher wird hier der ECHTE
/// PrestigeService verdrahtet (statt gemockt), damit die Logik real geprueft wird:
/// Tier-Multiplikator, Bronze-Mindestmenge, Prestige-Pass (+50%), Gilden-Forschung, Kombinationen.
/// </summary>
public sealed class PrestigeConfirmationViewModelTests
{
    // GetPrestigePoints = floor(sqrt(CurrentRunMoney / 100_000)).
    // Fuer exakte basePoints: money = basePoints^2 * 100_000.
    private static GameState StateForBasePoints(int basePoints, bool prestigePassActive = false, decimal guildResearchBonus = 0m)
    {
        var state = new GameState
        {
            CurrentRunMoney = (decimal)basePoints * basePoints * 100_000m,
            IsPrestigePassActive = prestigePassActive
        };
        if (guildResearchBonus > 0)
            state.GuildMembership = new GuildMembership { ResearchPrestigePointBonus = guildResearchBonus };
        return state;
    }

    private static PrestigeConfirmationViewModel CreateVm()
    {
        // Leerer State fuer _gameStateService.State → CalculateBonusPrestigePoints liefert 0
        // (keine Perfect-Ratings/Research/Gebaeude, PlayerLevel unter Tier-Minimum).
        var gameStateService = Substitute.For<IGameStateService>();
        gameStateService.State.Returns(new GameState());

        // ECHTER PrestigeService — die Balance-Logik liegt seit dem Refactor in CalculateTotalPrestigePoints.
        var prestigeService = new PrestigeService(
            gameStateService,
            Substitute.For<ISaveGameService>(),
            Substitute.For<IAscensionService>());

        var loc = Substitute.For<ILocalizationService>();
        loc.GetString(Arg.Any<string>()).Returns(call => call.Arg<string>());
        var adService = Substitute.For<IAdService>();
        var storyService = Substitute.For<IStoryService>();
        var hintService = Substitute.For<IContextualHintService>();

        var dialogVm = new DialogViewModel(loc, storyService, hintService, gameStateService,
            prestigeService, adService);
        return dialogVm.PrestigeConfirmation;
    }

    [Fact]
    public void CalculateEffectivePoints_Bronze_AppliesPointMultiplier()
    {
        // basePoints 100 * Bronze-Multiplikator (1.0) = 100, ueber der Bronze-Mindestmenge (15)
        var vm = CreateVm();
        Assert.Equal(100, vm.CalculateEffectivePoints(StateForBasePoints(100), PrestigeTier.Bronze));
    }

    [Fact]
    public void CalculateEffectivePoints_Bronze_EnforcesMinimum()
    {
        // basePoints 3 * Bronze (1.0) = 3, aber die Bronze-Mindestmenge (15) greift
        var vm = CreateVm();
        Assert.Equal(15, vm.CalculateEffectivePoints(StateForBasePoints(3), PrestigeTier.Bronze));
    }

    [Fact]
    public void CalculateEffectivePoints_Silver_NoBronzeMinimum()
    {
        // basePoints 3 * Silver-Multiplikator — die Mindestmenge gilt NUR fuer Bronze
        var vm = CreateVm();
        var expected = (int)System.Math.Round(3 * PrestigeTier.Silver.GetPointMultiplier());
        Assert.Equal(expected, vm.CalculateEffectivePoints(StateForBasePoints(3), PrestigeTier.Silver));
    }

    [Fact]
    public void CalculateEffectivePoints_PrestigePassActive_AddsFiftyPercent()
    {
        // basePoints 100 * Bronze 1.0 = 100, dann *1.5 (Prestige-Pass) = 150
        var vm = CreateVm();
        Assert.Equal(150, vm.CalculateEffectivePoints(
            StateForBasePoints(100, prestigePassActive: true), PrestigeTier.Bronze));
    }

    [Fact]
    public void CalculateEffectivePoints_GuildResearch_AddsResearchBonus()
    {
        // basePoints 100 * Bronze 1.0 = 100, dann *1.10 (Gilden-Forschung +10%) = 110
        var vm = CreateVm();
        Assert.Equal(110, vm.CalculateEffectivePoints(
            StateForBasePoints(100, guildResearchBonus: 0.10m), PrestigeTier.Bronze));
    }

    [Fact]
    public void CalculateEffectivePoints_PassAndGuildCombined_MultipliesBoth()
    {
        // 100 * Bronze 1.0 = 100, * Pass 1.5 = 150, * Gilden 1.10 = 165
        var vm = CreateVm();
        Assert.Equal(165, vm.CalculateEffectivePoints(
            StateForBasePoints(100, prestigePassActive: true, guildResearchBonus: 0.10m), PrestigeTier.Bronze));
    }
}
