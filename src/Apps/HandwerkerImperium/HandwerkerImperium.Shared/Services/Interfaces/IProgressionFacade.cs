namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Bounded-Context "Progression": Bündelt alle Endgame-Reset-Loops + Reputation-Belohnungen.
/// AAA-Audit P1 Service-Sprawl-Reduction (12.05.2026).
///
/// Reset-Hierarchie: Prestige → Rebirth → Ascension. Eternal Mastery skaliert mit
/// Prestige-Count ohne Reset. Reputation-Shop ist Endgame-Belohnung gekoppelt an Score.
/// </summary>
public interface IProgressionFacade
{
    /// <summary>Prestige-Tiers (Bronze→Legende), Prestige-Punkte, Permanent-Multiplier.</summary>
    IPrestigeService Prestige { get; }

    /// <summary>Workshop-Rebirth (5 Sterne pro Werkstatt, Bonus-Worker + Discount).</summary>
    IRebirthService Rebirth { get; }

    /// <summary>Ascension (Meta-Reset nach 3x Legende, AP-Perks).</summary>
    IAscensionService Ascension { get; }

    /// <summary>Eternal Mastery (permanenter Bonus pro Prestige, kein Reset).</summary>
    IEternalMasteryService EternalMastery { get; }

    /// <summary>Reputation-Shop (Cosmetics + Boosts gegen Reputation-Score).</summary>
    IReputationShopService ReputationShop { get; }
}
