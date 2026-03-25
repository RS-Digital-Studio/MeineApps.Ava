using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Zentraler Service für Prestige-Challenge-Constraints.
/// Liest die aktiven Challenges aus GameState.Prestige.ActiveChallenges
/// und stellt alle Constraint-Abfragen an einer Stelle bereit.
/// </summary>
public sealed class ChallengeConstraintService : IChallengeConstraintService
{
    private readonly IGameStateService _gameStateService;
    private readonly IAscensionService _ascensionService;

    public ChallengeConstraintService(
        IGameStateService gameStateService,
        IAscensionService ascensionService)
    {
        _gameStateService = gameStateService;
        _ascensionService = ascensionService;
    }

    public IReadOnlyList<PrestigeChallengeType> GetActiveChallenges()
        => _gameStateService.State.Prestige.ActiveChallenges;

    public bool IsChallengeActive(PrestigeChallengeType challenge)
        => _gameStateService.State.Prestige.ActiveChallenges.Contains(challenge);

    public int GetMaxWorkers(int defaultMax)
        => IsChallengeActive(PrestigeChallengeType.Spartaner) ? 3 : defaultMax;

    public decimal GetUpgradeCostMultiplier()
        => IsChallengeActive(PrestigeChallengeType.Inflationszeit) ? 2.0m : 1.0m;

    public bool IsWorkshopUnlockBlocked(int currentWorkshopCount)
        => IsChallengeActive(PrestigeChallengeType.SoloMeister) && currentWorkshopCount >= 1;

    public bool IsResearchBlocked()
        => IsChallengeActive(PrestigeChallengeType.OhneForschung);

    public bool IsOfflineIncomeBlocked()
        => IsChallengeActive(PrestigeChallengeType.Sprint);

    public bool IsDeliveryBlocked()
        => IsChallengeActive(PrestigeChallengeType.KeinNetz);

    public bool ToggleChallenge(PrestigeChallengeType challenge)
    {
        var challenges = _gameStateService.State.Prestige.ActiveChallenges;

        // Bereits aktiv → deaktivieren
        if (challenges.Contains(challenge))
        {
            challenges.Remove(challenge);
            return true;
        }

        // Maximum erreicht
        if (challenges.Count >= PrestigeChallengeExtensions.MaxActiveChallenges)
            return false;

        // SoloMeister inkompatibel mit QuickStart-Ascension-Perk
        if (challenge == PrestigeChallengeType.SoloMeister
            && _ascensionService.GetQuickStartWorkshops() > 0)
            return false;

        challenges.Add(challenge);
        return true;
    }

    public decimal GetChallengePpMultiplier()
    {
        var challenges = _gameStateService.State.Prestige.ActiveChallenges;
        return challenges.Count > 0
            ? ((IReadOnlyList<PrestigeChallengeType>)challenges).GetTotalPpMultiplier()
            : 1.0m;
    }
}
