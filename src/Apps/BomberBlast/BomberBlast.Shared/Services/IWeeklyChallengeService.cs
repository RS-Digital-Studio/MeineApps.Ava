using BomberBlast.Models;

namespace BomberBlast.Services;

/// <summary>
/// Service für wöchentliche Herausforderungen.
/// 5 Missionen pro Woche, Reset jeden Montag 00:00 UTC.
/// Bonus-Belohnung wenn alle 5 abgeschlossen.
/// </summary>
public interface IWeeklyChallengeService
{
    /// <summary>Aktuelle 5 Missionen dieser Woche</summary>
    IReadOnlyList<WeeklyMission> Missions { get; }

    /// <summary>Ob alle 5 Missionen abgeschlossen sind</summary>
    bool IsAllComplete { get; }

    /// <summary>Anzahl abgeschlossener Missionen diese Woche</summary>
    int CompletedCount { get; }

    /// <summary>Bonus-Coins wenn alle 5 Missionen abgeschlossen (0 wenn noch nicht alle fertig)</summary>
    int AllCompleteBonusCoins { get; }

    /// <summary>Ob der All-Complete-Bonus bereits eingesammelt wurde</summary>
    bool IsBonusClaimed { get; }

    /// <summary>Anzahl komplett abgeschlossener Wochen insgesamt</summary>
    int TotalWeeksCompleted { get; }

    /// <summary>Zeitpunkt des nächsten Resets (Montag 00:00 UTC)</summary>
    DateTime NextResetDate { get; }

    /// <summary>
    /// Fortschritt für einen Missionstyp melden.
    /// Gibt true zurück wenn eine Mission dadurch abgeschlossen wurde.
    /// </summary>
    bool TrackProgress(WeeklyMissionType type, int amount = 1);

    /// <summary>
    /// Bonus für alle-abgeschlossen einsammeln.
    /// Gibt die Bonus-Coins zurück (0 wenn bereits eingesammelt oder nicht alle fertig).
    /// </summary>
    int ClaimAllCompleteBonus();
}
