using FitnessRechner.Models;
using Material.Icons;
using MeineApps.Core.Ava.Services;

namespace FitnessRechner.Services;

/// <summary>
/// Preferences-basierter Challenge-Service.
/// 10 Challenges rotierend nach DayOfYear.
/// </summary>
public class ChallengeService : IChallengeService
{
    private const string CompletedDateKey = "challenge_completed_date";

    private readonly IPreferencesService _preferences;
    private readonly DailyChallenge[] _challenges;
    private DailyChallenge _todayChallenge;
    private bool _completed;

    public event Action<int>? ChallengeCompleted;

    public ChallengeService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _challenges = CreateChallenges();

        var index = DateTime.Today.DayOfYear % _challenges.Length;
        _todayChallenge = _challenges[index];

        // Prüfen ob heute schon abgeschlossen
        var completedDate = preferences.Get(CompletedDateKey, "");
        _completed = completedDate == DateTime.Today.ToString("yyyy-MM-dd");
        if (_completed)
            _todayChallenge.CurrentValue = _todayChallenge.TargetValue;
    }

    public DailyChallenge TodayChallenge => _todayChallenge;

    public void CheckProgress(ChallengeCheckContext ctx)
    {
        if (_completed) return;

        var newValue = GetChallengeValue(_todayChallenge.Id, ctx);
        _todayChallenge.CurrentValue = newValue;

        if (_todayChallenge.IsCompleted && !_completed)
        {
            _completed = true;
            _preferences.Set(CompletedDateKey, DateTime.Today.ToString("yyyy-MM-dd"));
            ChallengeCompleted?.Invoke(_todayChallenge.XpReward);
        }
    }

    private static int GetChallengeValue(string id, ChallengeCheckContext ctx) => id switch
    {
        "water_8" => (int)(ctx.TodayWaterMl / 250), // Gläser à 250ml
        "log_3_meals" => ctx.TodayMealsCount,
        "weigh_today" => ctx.HasWeightEntry ? 1 : 0,
        "under_calorie" => ctx.CalorieGoal > 0 && ctx.TodayCalories <= ctx.CalorieGoal && ctx.TodayCalories > 0 ? 1 : 0,
        "all_main_meals" => (ctx.HasBreakfast ? 1 : 0) + (ctx.HasLunch ? 1 : 0) + (ctx.HasDinner ? 1 : 0),
        "protein_100" => (int)ctx.TodayProtein,
        "track_5_foods" => ctx.TodayFoodsTracked,
        "water_before_16" => DateTime.Now.Hour < 16 && ctx.TodayWaterMl >= (ctx.CalorieGoal > 0 ? 2000 : 2500) ? 1 : 0,
        "calc_bmi" => ctx.HasUsedBmi ? 1 : 0,
        "scan_barcode" => ctx.HasScannedBarcode ? 1 : 0,
        _ => 0
    };

    private static DailyChallenge[] CreateChallenges() =>
    [
        new() { Id = "water_8", TitleKey = "ChallWater8", DescriptionKey = "ChallWater8Desc",
                Icon = MaterialIconKind.Water, TargetValue = 8, XpReward = 30 },
        new() { Id = "log_3_meals", TitleKey = "ChallLog3Meals", DescriptionKey = "ChallLog3MealsDesc",
                Icon = MaterialIconKind.Silverware, TargetValue = 3, XpReward = 25 },
        new() { Id = "weigh_today", TitleKey = "ChallWeighToday", DescriptionKey = "ChallWeighTodayDesc",
                Icon = MaterialIconKind.ScaleBathroom, TargetValue = 1, XpReward = 20 },
        new() { Id = "under_calorie", TitleKey = "ChallUnderCalorie", DescriptionKey = "ChallUnderCalorieDesc",
                Icon = MaterialIconKind.Fire, TargetValue = 1, XpReward = 35 },
        new() { Id = "all_main_meals", TitleKey = "ChallAllMainMeals", DescriptionKey = "ChallAllMainMealsDesc",
                Icon = MaterialIconKind.FoodDrumstick, TargetValue = 3, XpReward = 30 },
        new() { Id = "protein_100", TitleKey = "ChallProtein100", DescriptionKey = "ChallProtein100Desc",
                Icon = MaterialIconKind.ArmFlex, TargetValue = 100, XpReward = 35 },
        new() { Id = "track_5_foods", TitleKey = "ChallTrack5Foods", DescriptionKey = "ChallTrack5FoodsDesc",
                Icon = MaterialIconKind.FoodApple, TargetValue = 5, XpReward = 25 },
        new() { Id = "water_before_16", TitleKey = "ChallWaterBefore16", DescriptionKey = "ChallWaterBefore16Desc",
                Icon = MaterialIconKind.ClockCheck, TargetValue = 1, XpReward = 40 },
        new() { Id = "calc_bmi", TitleKey = "ChallCalcBmi", DescriptionKey = "ChallCalcBmiDesc",
                Icon = MaterialIconKind.Calculator, TargetValue = 1, XpReward = 20 },
        new() { Id = "scan_barcode", TitleKey = "ChallScanBarcode", DescriptionKey = "ChallScanBarcodeDesc",
                Icon = MaterialIconKind.BarcodeScanner, TargetValue = 1, XpReward = 25 },
    ];
}
