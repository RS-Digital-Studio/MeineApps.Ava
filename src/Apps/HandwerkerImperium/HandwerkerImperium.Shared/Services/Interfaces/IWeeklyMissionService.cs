namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet wöchentliche Missionen mit höheren Belohnungen als Daily Challenges.
/// </summary>
public interface IWeeklyMissionService
{
    /// <summary>
    /// Wird ausgelöst wenn sich der Fortschritt einer Mission ändert.
    /// </summary>
    event Action? MissionProgressChanged;

    /// <summary>
    /// Initialisiert Event-Subscriptions auf GameStateService.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Prüft ob Montag 00:00 UTC seit letztem Reset vergangen ist und generiert ggf. neue Missionen.
    /// </summary>
    void CheckAndResetIfNewWeek();

    /// <summary>
    /// Beansprucht die Belohnung einer abgeschlossenen Mission.
    /// </summary>
    void ClaimMission(string missionId);

    /// <summary>
    /// Tier-basierter Alle-fertig-Bonus (GS).
    /// </summary>
    int AllCompletedBonusScrews { get; }

    /// <summary>
    /// Beansprucht den Bonus wenn alle Missionen abgeschlossen sind.
    /// </summary>
    void ClaimAllCompletedBonus();

    /// <summary>
    /// Extern aufgerufen wenn eine Daily Challenge abgeschlossen wird.
    /// </summary>
    void OnDailyChallengeCompleted();

    /// <summary>
    /// Extern aufgerufen wenn ein Arbeiter-Training abgeschlossen wird.
    /// </summary>
    void OnWorkerTrained();

    /// <summary>
    /// Extern aufgerufen wenn ein Crafting-Produkt eingesammelt wird.
    /// </summary>
    void OnCraftingCompleted();

    /// <summary>
    /// Extern aufgerufen wenn ein Workshop ein neues Level erreicht.
    /// </summary>
    void OnWorkshopLevelReached();

    /// <summary>
    /// Extern aufgerufen nach einem MiniGame mit PerfectStreak-Info.
    /// </summary>
    void OnPerfectStreakUpdated(int currentStreak);

    /// <summary>
    /// Wird aufgerufen wenn Items durch Auto-Produktion hergestellt werden.
    /// </summary>
    void OnItemsAutoProduced(int count);
}
