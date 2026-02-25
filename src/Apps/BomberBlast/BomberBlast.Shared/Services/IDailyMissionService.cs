using BomberBlast.Models;

namespace BomberBlast.Services;

/// <summary>
/// Service für tägliche Missionen (3 pro Tag, Mitternacht-Reset UTC).
/// Nutzt dieselben WeeklyMissionType-Kategorien wie die wöchentlichen Herausforderungen.
/// </summary>
public interface IDailyMissionService
{
    /// <summary>Aktuelle 3 tägliche Missionen</summary>
    List<WeeklyMission> Missions { get; }

    /// <summary>Ob alle 3 Missionen abgeschlossen sind</summary>
    bool IsAllComplete { get; }

    /// <summary>Anzahl abgeschlossener Missionen (0-3)</summary>
    int CompletedCount { get; }

    /// <summary>Bonus-Coins wenn alle 3 Missionen erledigt (500)</summary>
    int AllCompleteBonusCoins { get; }

    /// <summary>Ob der All-Complete-Bonus bereits eingesammelt wurde</summary>
    bool IsBonusClaimed { get; }

    /// <summary>Gesamtanzahl abgeschlossener Tage</summary>
    int TotalDaysCompleted { get; }

    /// <summary>Zeitpunkt des nächsten Resets (Mitternacht UTC)</summary>
    DateTime NextResetDate { get; }

    /// <summary>Fortschritt tracken (aus GameEngine-Hooks)</summary>
    void TrackProgress(WeeklyMissionType type, int amount = 1);

    /// <summary>All-Complete-Bonus einsammeln</summary>
    int ClaimAllCompleteBonus();

    /// <summary>Lazy-Injection: IAchievementService nach DI-Build setzen (zirkuläre Abhängigkeit)</summary>
    void SetAchievementService(IAchievementService achievementService);
}
