using FitnessRechner.Models;

namespace FitnessRechner.Services;

/// <summary>
/// Verwaltet Achievements/Badges für das Gamification-System.
/// </summary>
public interface IAchievementService
{
    /// <summary>
    /// Wird ausgelöst wenn ein Achievement freigeschaltet wird.
    /// Parameter: (Achievement-Titel, XP-Reward).
    /// </summary>
    event Action<string, int>? AchievementUnlocked;

    /// <summary>
    /// Alle definierten Achievements.
    /// </summary>
    IReadOnlyList<FitnessAchievement> Achievements { get; }

    /// <summary>
    /// Zuletzt freigeschaltete Achievements (max. 3).
    /// </summary>
    IReadOnlyList<FitnessAchievement> RecentUnlocked { get; }

    /// <summary>
    /// Anzahl freigeschalteter Achievements.
    /// </summary>
    int UnlockedCount { get; }

    /// <summary>
    /// Prüft alle Achievements auf Fortschritt.
    /// Sollte bei relevanten Events aufgerufen werden.
    /// </summary>
    void CheckProgress(AchievementCheckContext context);
}

/// <summary>
/// Kontext-Daten für Achievement-Prüfung.
/// </summary>
public class AchievementCheckContext
{
    public int TotalMealsLogged { get; set; }
    public int TotalBarcodesScanned { get; set; }
    public int TotalRecipesCreated { get; set; }
    public int DistinctFoodsLogged { get; set; }
    public int CurrentStreak { get; set; }
    public int TotalWeightEntries { get; set; }
    public double TotalWaterMl { get; set; }
    public int CalculatorsUsed { get; set; }
    public bool HasReachedWeightGoal { get; set; }
    public bool IsPremium { get; set; }
    public int CurrentHour { get; set; }
    public int CalorieGoalDaysInRow { get; set; }
    public int WaterGoalDaysInRow { get; set; }
}
