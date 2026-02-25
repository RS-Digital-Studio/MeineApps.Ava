namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für den Battle Pass (30 Tiers, Free + Premium Track, 30-Tage-Saison).
/// </summary>
public interface IBattlePassService
{
    /// <summary>Feuert wenn sich der Battle-Pass-Zustand ändert (XP, Tier-Up, Claim).</summary>
    event Action? BattlePassUpdated;

    /// <summary>Fügt Battle-Pass-XP hinzu.</summary>
    void AddXp(int amount, string source);

    /// <summary>Beansprucht eine Belohnung auf einem bestimmten Tier.</summary>
    void ClaimReward(int tier, bool isPremium);

    /// <summary>Prüft ob eine neue Saison beginnen sollte (alle 30 Tage).</summary>
    void CheckNewSeason();

    /// <summary>Schaltet den Premium-Track per IAP frei.</summary>
    Task UpgradeToPremiumAsync();
}
