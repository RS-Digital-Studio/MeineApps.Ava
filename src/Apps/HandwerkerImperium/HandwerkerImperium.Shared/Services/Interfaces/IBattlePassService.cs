namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für den Battle Pass (30 Tiers, Free + Premium Track, 30-Tage-Saison).
/// </summary>
public interface IBattlePassService
{
    /// <summary>Feuert wenn sich der Battle-Pass-Zustand ändert (XP, Tier-Up, Claim).</summary>
    event Action? BattlePassUpdated;

    /// <summary>
    /// Feuert wenn der Spieler einen oder mehrere BP-Tiers aufsteigt (v2.1.0).
    /// (oldTier, newTier, seasonNumber). Bei Multi-Tier-Aufstieg wird das Event nur einmal
    /// mit den End-Werten gefeuert.
    /// </summary>
    event Action<int, int, int>? TierUpReached;

    /// <summary>Fügt Battle-Pass-XP hinzu.</summary>
    void AddXp(int amount, string source);

    /// <summary>Beansprucht eine Belohnung auf einem bestimmten Tier.</summary>
    void ClaimReward(int tier, bool isPremium);

    /// <summary>Prüft ob eine neue Saison beginnen sollte (alle 30 Tage).</summary>
    void CheckNewSeason();

    /// <summary>Schaltet den Premium-Track per IAP frei.</summary>
    Task UpgradeToPremiumAsync();
}
