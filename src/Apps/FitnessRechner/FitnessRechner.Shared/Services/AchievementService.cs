using System.Text.Json;
using FitnessRechner.Models;
using Material.Icons;
using MeineApps.Core.Ava.Services;

namespace FitnessRechner.Services;

/// <summary>
/// Preferences-basierter Achievement-Service.
/// Speichert freigeschaltete IDs und Fortschritt als JSON.
/// </summary>
public class AchievementService : IAchievementService
{
    private const string UnlockedKey = "achievements_unlocked";
    private const string ProgressKey = "achievements_progress";

    private readonly IPreferencesService _preferences;
    private readonly List<FitnessAchievement> _achievements;
    private readonly HashSet<string> _unlockedIds;
    private readonly Dictionary<string, int> _progressData;

    public event Action<string, int>? AchievementUnlocked;

    public AchievementService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _achievements = CreateAchievements();

        // Gespeicherten Zustand laden
        _unlockedIds = LoadUnlockedIds();
        _progressData = LoadProgressData();

        // Zustand auf Achievements anwenden
        foreach (var a in _achievements)
        {
            a.IsUnlocked = _unlockedIds.Contains(a.Id);
            if (_progressData.TryGetValue(a.Id, out var progress))
                a.CurrentValue = progress;
        }
    }

    public IReadOnlyList<FitnessAchievement> Achievements => _achievements;

    public IReadOnlyList<FitnessAchievement> RecentUnlocked =>
        _achievements.Where(a => a.IsUnlocked)
            .OrderByDescending(a => a.UnlockedAt)
            .Take(3)
            .ToList();

    public int UnlockedCount => _unlockedIds.Count;

    public void CheckProgress(AchievementCheckContext ctx)
    {
        var newlyUnlocked = new List<FitnessAchievement>();

        foreach (var a in _achievements)
        {
            if (a.IsUnlocked) continue;

            // Fortschritt aktualisieren basierend auf Achievement-ID
            var newValue = GetCurrentValue(a.Id, ctx);
            if (newValue != a.CurrentValue)
            {
                a.CurrentValue = newValue;
                _progressData[a.Id] = newValue;
            }

            // Prüfen ob Achievement erreicht
            if (a.CurrentValue >= a.TargetValue)
            {
                a.IsUnlocked = true;
                a.UnlockedAt = DateTime.UtcNow;
                _unlockedIds.Add(a.Id);
                newlyUnlocked.Add(a);
            }
        }

        // Speichern wenn sich was geändert hat
        if (newlyUnlocked.Count > 0 || _progressData.Count > 0)
        {
            SaveUnlockedIds();
            SaveProgressData();
        }

        // Events auslösen
        foreach (var a in newlyUnlocked)
            AchievementUnlocked?.Invoke(a.TitleKey, a.XpReward);
    }

    private int GetCurrentValue(string id, AchievementCheckContext ctx) => id switch
    {
        // Tracking
        "first_log" => ctx.TotalMealsLogged > 0 || ctx.TotalWeightEntries > 0 ? 1 : 0,
        "week_streak" => ctx.CurrentStreak,
        "month_streak" => ctx.CurrentStreak,
        "quarter_streak" => ctx.CurrentStreak,
        "year_streak" => ctx.CurrentStreak,
        // Ernährung
        "gourmet" => ctx.DistinctFoodsLogged,
        "meal_master" => ctx.TotalMealsLogged,
        "barcode_scanner" => ctx.TotalBarcodesScanned,
        "recipe_chef" => ctx.TotalRecipesCreated,
        "calorie_pro" => ctx.CalorieGoalDaysInRow,
        // Wasser
        "first_water" => (int)(ctx.TotalWaterMl > 0 ? 1 : 0),
        "hydration_hero" => ctx.WaterGoalDaysInRow,
        "aqua_master" => (int)(ctx.TotalWaterMl / 1000), // Liter
        "ocean_conqueror" => (int)(ctx.TotalWaterMl / 1000),
        // Körper
        "scale_friend" => ctx.TotalWeightEntries,
        "transformation" => ctx.HasReachedWeightGoal ? 1 : 0,
        "body_conscious" => ctx.CalculatorsUsed,
        // Special
        "premium_athlete" => ctx.IsPremium ? 1 : 0,
        "night_owl" => ctx.CurrentHour >= 22 ? 1 : 0,
        "early_bird" => ctx.CurrentHour < 6 && ctx.CurrentHour >= 0 ? 1 : 0,
        _ => 0
    };

    private static List<FitnessAchievement> CreateAchievements() =>
    [
        // Tracking (5)
        new() { Id = "first_log", TitleKey = "AchFirstLog", DescriptionKey = "AchFirstLogDesc",
                Icon = MaterialIconKind.RocketLaunch, Category = AchievementCategory.Tracking,
                TargetValue = 1, XpReward = 25 },
        new() { Id = "week_streak", TitleKey = "AchWeekStreak", DescriptionKey = "AchWeekStreakDesc",
                Icon = MaterialIconKind.Fire, Category = AchievementCategory.Tracking,
                TargetValue = 7, XpReward = 50 },
        new() { Id = "month_streak", TitleKey = "AchMonthStreak", DescriptionKey = "AchMonthStreakDesc",
                Icon = MaterialIconKind.CalendarCheck, Category = AchievementCategory.Tracking,
                TargetValue = 30, XpReward = 100 },
        new() { Id = "quarter_streak", TitleKey = "AchQuarterStreak", DescriptionKey = "AchQuarterStreakDesc",
                Icon = MaterialIconKind.Trophy, Category = AchievementCategory.Tracking,
                TargetValue = 90, XpReward = 150 },
        new() { Id = "year_streak", TitleKey = "AchYearStreak", DescriptionKey = "AchYearStreakDesc",
                Icon = MaterialIconKind.Crown, Category = AchievementCategory.Tracking,
                TargetValue = 365, XpReward = 500 },

        // Ernährung (5)
        new() { Id = "gourmet", TitleKey = "AchGourmet", DescriptionKey = "AchGourmetDesc",
                Icon = MaterialIconKind.Silverware, Category = AchievementCategory.Nutrition,
                TargetValue = 10, XpReward = 50 },
        new() { Id = "meal_master", TitleKey = "AchMealMaster", DescriptionKey = "AchMealMasterDesc",
                Icon = MaterialIconKind.FoodApple, Category = AchievementCategory.Nutrition,
                TargetValue = 100, XpReward = 100 },
        new() { Id = "barcode_scanner", TitleKey = "AchBarcodeScanner", DescriptionKey = "AchBarcodeScannerDesc",
                Icon = MaterialIconKind.Barcode, Category = AchievementCategory.Nutrition,
                TargetValue = 10, XpReward = 50 },
        new() { Id = "recipe_chef", TitleKey = "AchRecipeChef", DescriptionKey = "AchRecipeChefDesc",
                Icon = MaterialIconKind.ChefHat, Category = AchievementCategory.Nutrition,
                TargetValue = 5, XpReward = 75 },
        new() { Id = "calorie_pro", TitleKey = "AchCaloriePro", DescriptionKey = "AchCalorieProDesc",
                Icon = MaterialIconKind.Fire, Category = AchievementCategory.Nutrition,
                TargetValue = 7, XpReward = 100 },

        // Wasser (4)
        new() { Id = "first_water", TitleKey = "AchFirstWater", DescriptionKey = "AchFirstWaterDesc",
                Icon = MaterialIconKind.Water, Category = AchievementCategory.Water,
                TargetValue = 1, XpReward = 25 },
        new() { Id = "hydration_hero", TitleKey = "AchHydrationHero", DescriptionKey = "AchHydrationHeroDesc",
                Icon = MaterialIconKind.WaterCheck, Category = AchievementCategory.Water,
                TargetValue = 7, XpReward = 75 },
        new() { Id = "aqua_master", TitleKey = "AchAquaMaster", DescriptionKey = "AchAquaMasterDesc",
                Icon = MaterialIconKind.Waves, Category = AchievementCategory.Water,
                TargetValue = 100, XpReward = 100 },
        new() { Id = "ocean_conqueror", TitleKey = "AchOceanConqueror", DescriptionKey = "AchOceanConquerorDesc",
                Icon = MaterialIconKind.Tsunami, Category = AchievementCategory.Water,
                TargetValue = 1000, XpReward = 200 },

        // Körper (3)
        new() { Id = "scale_friend", TitleKey = "AchScaleFriend", DescriptionKey = "AchScaleFriendDesc",
                Icon = MaterialIconKind.ScaleBathroom, Category = AchievementCategory.Body,
                TargetValue = 10, XpReward = 50 },
        new() { Id = "transformation", TitleKey = "AchTransformation", DescriptionKey = "AchTransformationDesc",
                Icon = MaterialIconKind.StarShooting, Category = AchievementCategory.Body,
                TargetValue = 1, XpReward = 200 },
        new() { Id = "body_conscious", TitleKey = "AchBodyConscious", DescriptionKey = "AchBodyConsciousDesc",
                Icon = MaterialIconKind.HumanMaleBoard, Category = AchievementCategory.Body,
                TargetValue = 5, XpReward = 75 },

        // Special (3)
        new() { Id = "premium_athlete", TitleKey = "AchPremiumAthlete", DescriptionKey = "AchPremiumAthleteDesc",
                Icon = MaterialIconKind.Star, Category = AchievementCategory.Special,
                TargetValue = 1, XpReward = 100 },
        new() { Id = "night_owl", TitleKey = "AchNightOwl", DescriptionKey = "AchNightOwlDesc",
                Icon = MaterialIconKind.WeatherNight, Category = AchievementCategory.Special,
                TargetValue = 1, XpReward = 25 },
        new() { Id = "early_bird", TitleKey = "AchEarlyBird", DescriptionKey = "AchEarlyBirdDesc",
                Icon = MaterialIconKind.WeatherSunny, Category = AchievementCategory.Special,
                TargetValue = 1, XpReward = 25 },
    ];

    private HashSet<string> LoadUnlockedIds()
    {
        var json = _preferences.Get(UnlockedKey, "[]");
        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(json);
            return ids != null ? new HashSet<string>(ids) : [];
        }
        catch { return []; }
    }

    private Dictionary<string, int> LoadProgressData()
    {
        var json = _preferences.Get(ProgressKey, "{}");
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            return data ?? new Dictionary<string, int>();
        }
        catch { return new Dictionary<string, int>(); }
    }

    private void SaveUnlockedIds()
    {
        var json = JsonSerializer.Serialize(_unlockedIds.ToList());
        _preferences.Set(UnlockedKey, json);
    }

    private void SaveProgressData()
    {
        var json = JsonSerializer.Serialize(_progressData);
        _preferences.Set(ProgressKey, json);
    }
}
