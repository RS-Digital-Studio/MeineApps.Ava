using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung der <see cref="IProgressionFacade"/> — Service-Container für die fünf
/// Endgame-Reset-/Belohnungs-Loops. Singleton, kein State.
/// </summary>
public sealed class ProgressionFacade : IProgressionFacade
{
    public IPrestigeService Prestige { get; }
    public IRebirthService Rebirth { get; }
    public IAscensionService Ascension { get; }
    public IEternalMasteryService EternalMastery { get; }
    public IReputationShopService ReputationShop { get; }

    public ProgressionFacade(
        IPrestigeService prestige,
        IRebirthService rebirth,
        IAscensionService ascension,
        IEternalMasteryService eternalMastery,
        IReputationShopService reputationShop)
    {
        Prestige = prestige;
        Rebirth = rebirth;
        Ascension = ascension;
        EternalMastery = eternalMastery;
        ReputationShop = reputationShop;
    }
}
