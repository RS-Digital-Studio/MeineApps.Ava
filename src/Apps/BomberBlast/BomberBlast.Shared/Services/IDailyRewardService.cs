using BomberBlast.Models;

namespace BomberBlast.Services;

/// <summary>
/// Service für den 7-Tage Login-Bonus-Zyklus
/// </summary>
public interface IDailyRewardService
{
    /// <summary>Ob heute ein Bonus verfügbar ist</summary>
    bool IsRewardAvailable { get; }

    /// <summary>Aktueller Tag im Zyklus (1-7)</summary>
    int CurrentDay { get; }

    /// <summary>Aktuelle Streak-Länge</summary>
    int CurrentStreak { get; }

    /// <summary>Alle 7 Tage mit Status</summary>
    IReadOnlyList<DailyReward> GetRewards();

    /// <summary>Heutigen Bonus abholen. Gibt die Belohnung zurück oder null wenn nicht verfügbar.</summary>
    DailyReward? ClaimReward();

    /// <summary>
    /// Prüft ob ein Comeback-Bonus verfügbar ist (>3 Tage inaktiv).
    /// Gibt (coins, gems) zurück oder null wenn kein Bonus.
    /// </summary>
    (int coins, int gems)? CheckComebackBonus();

    /// <summary>Letzte Aktivität aktualisieren (bei jedem App-Start aufrufen)</summary>
    void UpdateLastActivity();
}
