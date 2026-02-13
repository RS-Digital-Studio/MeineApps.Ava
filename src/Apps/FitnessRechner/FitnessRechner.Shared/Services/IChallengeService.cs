using FitnessRechner.Models;

namespace FitnessRechner.Services;

/// <summary>
/// Verwaltet die tägliche Herausforderung.
/// </summary>
public interface IChallengeService
{
    /// <summary>
    /// Wird ausgelöst wenn die Challenge abgeschlossen wurde.
    /// Parameter: XP-Reward.
    /// </summary>
    event Action<int>? ChallengeCompleted;

    /// <summary>
    /// Heutige Challenge.
    /// </summary>
    DailyChallenge TodayChallenge { get; }

    /// <summary>
    /// Prüft den Fortschritt der heutigen Challenge.
    /// </summary>
    void CheckProgress(ChallengeCheckContext context);
}

/// <summary>
/// Kontext-Daten für Challenge-Prüfung.
/// </summary>
public class ChallengeCheckContext
{
    public double TodayWaterMl { get; set; }
    public int TodayMealsCount { get; set; }
    public bool HasWeightEntry { get; set; }
    public double TodayCalories { get; set; }
    public double CalorieGoal { get; set; }
    public int TodayFoodsTracked { get; set; }
    public double TodayProtein { get; set; }
    public bool HasUsedBmi { get; set; }
    public bool HasScannedBarcode { get; set; }
    public bool HasBreakfast { get; set; }
    public bool HasLunch { get; set; }
    public bool HasDinner { get; set; }
}
