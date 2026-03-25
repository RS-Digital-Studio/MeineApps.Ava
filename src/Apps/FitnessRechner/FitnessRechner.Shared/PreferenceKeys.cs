namespace FitnessRechner;

/// <summary>
/// Zentrale Preference-Keys und App-Konstanten.
/// Verhindert Duplikation und Typo-Bugs über mehrere ViewModels hinweg.
/// </summary>
public static class PreferenceKeys
{
    // Benutzerprofil
    public const string ProfileHeight = "profile_height";
    public const string ProfileAge = "profile_age";
    public const string ProfileIsMale = "profile_is_male";
    public const string ProfileActivityLevel = "profile_activity_level";

    // Ziel-Einstellungen
    public const string CalorieGoal = "daily_calorie_goal";
    public const string WaterGoal = "daily_water_goal";
    public const string WeightGoal = "weight_goal";
    public const string MacroProteinGoal = "macro_goal_protein";
    public const string MacroCarbsGoal = "macro_goal_carbs";
    public const string MacroFatGoal = "macro_goal_fat";
    public const string MacroAutoCalculated = "macro_auto_calculated";

    // Intervallfasten
    public const string FastingPlan = "fasting_plan";
    public const string FastingStartTime = "fasting_start_time";
    public const string FastingIsActive = "fasting_is_active";
    public const string FastingHistory = "fasting_history";
    public const string FastingCustomHours = "fasting_custom_hours";

    // Erweiterte Food-DB
    public const string ExtendedFoodDbExpiry = "ExtendedFoodDbExpiry";

    // Scan-Limit
    public const string ScanLimitCount = "ScanLimit_Count";
    public const string ScanLimitDate = "ScanLimit_Date";

    // Streak
    public const string StreakCurrent = "streak_current";
    public const string StreakBest = "streak_best";
    public const string StreakLastLogDate = "streak_last_log_date";

    // Chart
    public const string ChartDays = "chart_days";
    public const int DefaultChartDays = 30;

    // Gamification
    public const string FitnessXp = "fitness_xp";
    public const string FitnessLevel = "fitness_level";
    public const string AchievementsUnlocked = "achievements_unlocked";
    public const string AchievementsProgress = "achievements_progress";
    public const string ChallengeCompletedDate = "challenge_completed_date";

    // Gamification-Zähler (kumulativ, nie zurückgesetzt)
    public const string TotalMealsLogged = "total_meals_logged";
    public const string TotalBarcodesScanned = "total_barcodes_scanned";
    public const string DistinctFoodsTracked = "distinct_foods_tracked";
    public const string CalculatorsUsedMask = "calculators_used_mask";

    // UI-Konstanten
    public const int UndoTimeoutMs = 5000;
}
