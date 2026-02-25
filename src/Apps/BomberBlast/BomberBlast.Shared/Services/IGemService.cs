namespace BomberBlast.Services;

/// <summary>
/// Verwaltet die Gem-Währung des Spielers.
/// Gems sind NUR durch Gameplay verdienbar (kein IAP-Kauf).
/// Quellen: Daily Challenge Streaks, Dungeon-Bosse, Liga-Belohnungen, Battle Pass, Achievements.
/// </summary>
public interface IGemService
{
    /// <summary>Aktueller Gem-Stand</summary>
    int Balance { get; }

    /// <summary>Insgesamt verdiente Gems (Lifetime)</summary>
    int TotalEarned { get; }

    /// <summary>Gems hinzufügen</summary>
    void AddGems(int amount);

    /// <summary>Gems ausgeben (gibt false zurück wenn nicht genug)</summary>
    bool TrySpendGems(int amount);

    /// <summary>Prüfen ob genug Gems vorhanden</summary>
    bool CanAfford(int amount);

    /// <summary>Gem-Stand hat sich geändert</summary>
    event EventHandler? BalanceChanged;

    /// <summary>Lazy-Injection: Mission-Services nach DI-Build setzen (Phase 9.4)</summary>
    void SetMissionServices(IWeeklyChallengeService weeklyService, IDailyMissionService dailyMissionService);
}
