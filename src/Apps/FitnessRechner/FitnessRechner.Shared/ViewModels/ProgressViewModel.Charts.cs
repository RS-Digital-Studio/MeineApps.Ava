using System.Collections.ObjectModel;
using System.Globalization;
using FitnessRechner.Graphics;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using SkiaSharp;

namespace FitnessRechner.ViewModels;

/// <summary>
/// Chart-Daten-Berechnung, Statistik-Methoden, Zeitraum-Filter.
/// Enthält Update-Methoden für Weight/Body/Water/Calorie Charts und Meilensteine.
/// </summary>
public sealed partial class ProgressViewModel
{
    #region Weight Chart

    private void UpdateWeightChart()
    {
        if (WeightEntries.Count > 0)
        {
            WeightChartData = WeightEntries
                .OrderBy(e => e.Date)
                .Select(e => new HealthTrendVisualization.DataPoint { Date = e.Date, Value = (float)e.Value })
                .ToArray();
        }
        else
        {
            WeightChartData = [];
        }
    }

    /// <summary>
    /// Berechnet Meilenstein-Markierungen im Weight-Chart.
    /// Vertikale Linien an Tagen wo ein ganzes Kilo unterschritten wurde.
    /// </summary>
    private void UpdateWeightMilestones()
    {
        if (WeightEntries.Count < 2)
        {
            WeightMilestoneLines = [];
            return;
        }

        var sorted = WeightEntries.OrderBy(e => e.Date).ToList();
        var lines = new List<HealthTrendVisualization.MilestoneLine>();
        var markedKgs = new HashSet<int>();

        for (int i = 1; i < sorted.Count; i++)
        {
            var prev = sorted[i - 1].Value;
            var curr = sorted[i].Value;

            // Abwärts: ganzes Kilo unterschritten
            if (curr < prev)
            {
                var upperKg = (int)Math.Floor(prev);
                var lowerKg = (int)Math.Ceiling(curr);
                for (int kg = upperKg; kg > lowerKg && kg > 0; kg--)
                {
                    if (markedKgs.Add(kg))
                    {
                        lines.Add(new HealthTrendVisualization.MilestoneLine
                        {
                            Date = sorted[i].Date,
                            Color = new SKColor(139, 92, 246, 120)
                        });
                    }
                }
            }
        }

        WeightMilestoneLines = lines.ToArray();
    }

    #endregion

    #region Weight Goal Status

    private void UpdateWeightGoalStatus()
    {
        if (!HasWeightGoal || WeightStats == null)
        {
            WeightGoalProgress = 0;
            WeightGoalStatusText = "";
            return;
        }

        var current = WeightStats.CurrentValue;
        var goal = WeightGoal;
        var startWeight = WeightStats.MaxValue; // Höchstes Gewicht als Ausgangspunkt

        if (Math.Abs(startWeight - goal) > 0.1)
        {
            var progress = Math.Abs(startWeight - current) / Math.Abs(startWeight - goal);
            WeightGoalProgress = Math.Min(Math.Max(progress, 0), 1.0);
        }

        var remaining = Math.Abs(current - goal);
        if (remaining < 0.5)
        {
            WeightGoalStatusText = AppStrings.GoalReached;
            if (!_wasWeightGoalCelebrated)
            {
                _wasWeightGoalCelebrated = true;
                FloatingTextRequested?.Invoke(AppStrings.GoalReached, "success");
                CelebrationRequested?.Invoke();
            }
        }
        else
        {
            WeightGoalStatusText = string.Format(AppStrings.WeightRemaining, $"{remaining:F1}");
        }
    }

    #endregion

    #region Body Charts (BMI + BodyFat)

    private void UpdateBodyCharts()
    {
        // BMI Chart
        if (BmiEntries.Count > 0)
        {
            BmiChartData = BmiEntries
                .OrderBy(e => e.Date)
                .Select(e => new HealthTrendVisualization.DataPoint { Date = e.Date, Value = (float)e.Value })
                .ToArray();
        }
        else
        {
            BmiChartData = [];
        }

        // BodyFat Chart
        if (BodyFatEntries.Count > 0)
        {
            BodyFatChartData = BodyFatEntries
                .OrderBy(e => e.Date)
                .Select(e => new HealthTrendVisualization.DataPoint { Date = e.Date, Value = (float)e.Value })
                .ToArray();
        }
        else
        {
            BodyFatChartData = [];
        }
    }

    #endregion

    #region Water Status

    private void UpdateWaterStatus()
    {
        if (!HasWaterGoal)
        {
            WaterProgress = 0;
            WaterStatusText = AppStrings.SetWaterGoal;
            return;
        }

        WaterProgress = Math.Min(TodayWater / DailyWaterGoal, 1.0);

        var remaining = DailyWaterGoal - TodayWater;
        if (remaining > 0)
        {
            WaterStatusText = string.Format(AppStrings.WaterRemaining, $"{remaining:F0}");
        }
        else
        {
            WaterStatusText = AppStrings.GoalReached;

            // Wasser-Ziel erreicht - nur einmal Confetti pro Session
            if (!_wasWaterGoalReached)
            {
                _wasWaterGoalReached = true;
                FloatingTextRequested?.Invoke(AppStrings.GoalReached, "success");
                CelebrationRequested?.Invoke();
            }
        }
    }

    #endregion

    #region Calorie Status

    private void UpdateCalorieStatus()
    {
        if (DailyCalorieGoal <= 0)
        {
            CalorieStatusText = AppStrings.SetCalorieGoal;
            RemainingCalories = 0;
            HasCalorieDeficit = true;
            return;
        }

        var remaining = DailyCalorieGoal - ConsumedCalories;
        RemainingCalories = Math.Abs(remaining);
        HasCalorieDeficit = remaining >= 0;

        if (HasCalorieDeficit)
        {
            CalorieStatusText = string.Format(AppStrings.CaloriesRemaining, $"{RemainingCalories:F0}");
        }
        else
        {
            CalorieStatusText = string.Format(AppStrings.CaloriesOver, $"{RemainingCalories:F0}");
        }
    }

    /// <summary>
    /// Berechnet Kalorien/Macros lokal aus TodayMeals statt DB-Read.
    /// Wird bei Delete/Undo verwendet, da DB-Zustand während Undo-Phase
    /// nicht dem UI-Zustand entspricht.
    /// </summary>
    private void RecalculateCalorieDataFromMeals()
    {
        var meals = TodayMeals.ToList();
        ConsumedCalories = meals.Sum(m => m.Calories);
        ProteinConsumed = meals.Sum(m => m.Protein);
        CarbsConsumed = meals.Sum(m => m.Carbs);
        FatConsumed = meals.Sum(m => m.Fat);

        UpdateCalorieStatus();

        OnPropertyChanged(nameof(ProteinProgress));
        OnPropertyChanged(nameof(CarbsProgress));
        OnPropertyChanged(nameof(FatProgress));
        OnPropertyChanged(nameof(HasMacroGoals));
    }

    #endregion

    #region Weekly Calories Chart

    private async Task LoadWeeklyCaloriesAsync()
    {
        var startDate = DateTime.Today.AddDays(-6);
        var summaryTasks = Enumerable.Range(0, 7)
            .Select(i => _foodSearchService.GetDailySummaryAsync(startDate.AddDays(i)))
            .ToArray();
        var summaries = await Task.WhenAll(summaryTasks);

        HasWeeklyData = summaries.Any(s => s.TotalCalories > 0);
        if (!HasWeeklyData) return;

        WeeklyCaloriesValues = summaries.Select(s => (float)s.TotalCalories).ToArray();
        WeeklyDayLabels = Enumerable.Range(0, 7)
            .Select(i => startDate.AddDays(i).ToString("ddd", CultureInfo.CurrentCulture))
            .ToArray();
    }

    #endregion

    #region Meal Grouping

    private void GroupMealsByType(List<FoodLogEntry> meals)
    {
        BreakfastMeals = new ObservableCollection<FoodLogEntry>(
            meals.Where(m => m.Meal == MealType.Breakfast));
        LunchMeals = new ObservableCollection<FoodLogEntry>(
            meals.Where(m => m.Meal == MealType.Lunch));
        DinnerMeals = new ObservableCollection<FoodLogEntry>(
            meals.Where(m => m.Meal == MealType.Dinner));
        SnackMeals = new ObservableCollection<FoodLogEntry>(
            meals.Where(m => m.Meal == MealType.Snack));

        OnPropertyChanged(nameof(HasBreakfast));
        OnPropertyChanged(nameof(HasLunch));
        OnPropertyChanged(nameof(HasDinner));
        OnPropertyChanged(nameof(HasSnack));
        OnPropertyChanged(nameof(BreakfastCalories));
        OnPropertyChanged(nameof(LunchCalories));
        OnPropertyChanged(nameof(DinnerCalories));
        OnPropertyChanged(nameof(SnackCalories));
    }

    #endregion
}
