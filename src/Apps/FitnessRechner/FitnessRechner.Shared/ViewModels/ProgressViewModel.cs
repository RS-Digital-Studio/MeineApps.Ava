using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using SkiaSharp;

namespace FitnessRechner.ViewModels;

public enum ProgressTab
{
    Weight,
    Body,
    Water,
    Calories
}

public partial class ProgressViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly ITrackingService _trackingService;
    private readonly IPurchaseService _purchaseService;
    private readonly IFoodSearchService _foodSearchService;
    private readonly IPreferencesService _preferences;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IFileShareService _fileShareService;

    // Preference-Keys und Undo-Timeout zentral in PreferenceKeys.cs

    /// <summary>
    /// Raised when the VM wants to navigate
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Raised when the VM wants to show a message (title, message).
    /// </summary>
    public event Action<string, string>? MessageRequested;

    /// <summary>
    /// Floating Text anzeigen (text, category: "info"/"success").
    /// </summary>
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>
    /// Confetti-Celebration ausloesen.
    /// </summary>
    public event Action? CelebrationRequested;

    public ProgressViewModel(
        ITrackingService trackingService,
        IPurchaseService purchaseService,
        IFoodSearchService foodSearchService,
        IPreferencesService preferences,
        IRewardedAdService rewardedAdService,
        IFileShareService fileShareService)
    {
        _trackingService = trackingService;
        _purchaseService = purchaseService;
        _foodSearchService = foodSearchService;
        _preferences = preferences;
        _rewardedAdService = rewardedAdService;
        _fileShareService = fileShareService;
    }

    #region Tab Selection

    [ObservableProperty]
    private ProgressTab _selectedTab = ProgressTab.Weight;

    public bool IsWeightTab => SelectedTab == ProgressTab.Weight;
    public bool IsBodyTab => SelectedTab == ProgressTab.Body;
    public bool IsWaterTab => SelectedTab == ProgressTab.Water;
    public bool IsCaloriesTab => SelectedTab == ProgressTab.Calories;

    partial void OnSelectedTabChanged(ProgressTab value)
    {
        OnPropertyChanged(nameof(IsWeightTab));
        OnPropertyChanged(nameof(IsBodyTab));
        OnPropertyChanged(nameof(IsWaterTab));
        OnPropertyChanged(nameof(IsCaloriesTab));

        // Laufenden Undo committen um falsche Collection-Zuordnung zu vermeiden
        if (_recentlyDeletedEntry != null || _recentlyDeletedMeal != null)
        {
            _undoCancellation?.Cancel();
            _recentlyDeletedEntry = null;
            _recentlyDeletedMeal = null;
            ShowUndoBanner = false;
        }

        ShowAddForm = false;
        ShowFoodSearch = false;
        _ = LoadCurrentTabDataAsync();
    }

    #endregion

    #region Common Properties

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showAds;

    [ObservableProperty]
    private bool _showAddForm;

    [ObservableProperty]
    private double _newValue;

    [ObservableProperty]
    private DateTime _newDate = DateTime.Today;

    [ObservableProperty]
    private string? _newNote;

    // Undo
    [ObservableProperty]
    private bool _showUndoBanner;

    [ObservableProperty]
    private string _undoMessage = string.Empty;

    private TrackingEntry? _recentlyDeletedEntry;
    private FoodLogEntry? _recentlyDeletedMeal;
    private CancellationTokenSource? _undoCancellation;

    // Ziel-Flag: Confetti nur einmal pro Session
    private bool _wasWaterGoalReached;
    private bool _wasWeightGoalCelebrated;
    private DateTime _lastWaterCelebrationDate = DateTime.MinValue;

    #endregion

    #region Weight Tab

    [ObservableProperty]
    private ObservableCollection<TrackingEntry> _weightEntries = [];

    [ObservableProperty]
    private TrackingStats? _weightStats;

    [ObservableProperty]
    private IEnumerable<ISeries> _weightChartSeries = [];

    [ObservableProperty]
    private double _weightGoal;

    [ObservableProperty]
    private bool _hasWeightGoal;

    [ObservableProperty]
    private double _weightGoalProgress;

    [ObservableProperty]
    private string _weightGoalStatusText = "";

    public string WeightCurrentDisplay => WeightStats != null ? $"{WeightStats.CurrentValue:F1}" : "-";
    public string WeightAverageDisplay => WeightStats != null ? $"{WeightStats.AverageValue:F1}" : "-";
    public string WeightTrendDisplay => WeightStats != null
        ? (WeightStats.TrendValue >= 0 ? $"+{WeightStats.TrendValue:F1}" : $"{WeightStats.TrendValue:F1}")
        : "-";

    // Trend-Pfeil: Richtung, Icon und Farbe basierend auf TrendValue
    public string WeightTrendIcon => WeightStats == null ? "TrendingFlat" : WeightStats.TrendValue switch
    {
        > 0.1 => "TrendingUp",
        < -0.1 => "TrendingDown",
        _ => "TrendingFlat"
    };

    public string WeightTrendColor => WeightStats == null ? "#EAB308" : WeightStats.TrendValue switch
    {
        > 0.1 => "#EF4444",   // Rot = zunehmen
        < -0.1 => "#22C55E",  // Grün = abnehmen
        _ => "#EAB308"         // Gelb = stabil
    };

    public string WeightTrendLabel => WeightStats == null ? "" : WeightStats.TrendValue switch
    {
        > 0.1 => AppStrings.TrendUp,
        < -0.1 => AppStrings.TrendDown,
        _ => AppStrings.TrendFlat
    };

    // Meilenstein-Markierungen im Weight-Chart (vertikale Linien bei ganzen kg-Wechseln)
    [ObservableProperty]
    private RectangularSection[] _weightMilestoneSections = [];

    #endregion

    #region Body Tab (BMI + BodyFat)

    [ObservableProperty]
    private bool _isBmiSelected = true;

    public bool IsBodyFatSelected => !IsBmiSelected;

    partial void OnIsBmiSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBodyFatSelected));
    }

    [ObservableProperty]
    private ObservableCollection<TrackingEntry> _bmiEntries = [];

    [ObservableProperty]
    private ObservableCollection<TrackingEntry> _bodyFatEntries = [];

    [ObservableProperty]
    private TrackingStats? _bmiStats;

    [ObservableProperty]
    private TrackingStats? _bodyFatStats;

    [ObservableProperty]
    private IEnumerable<ISeries> _bmiChartSeries = [];

    [ObservableProperty]
    private IEnumerable<ISeries> _bodyFatChartSeries = [];

    public bool HasBmiEntries => BmiEntries.Count > 0;
    public bool HasBodyFatEntries => BodyFatEntries.Count > 0;

    public string BmiCurrentDisplay => BmiStats != null ? $"{BmiStats.CurrentValue:F1}" : "-";
    public string BmiAverageDisplay => BmiStats != null ? $"{BmiStats.AverageValue:F1}" : "-";
    public string BmiTrendDisplay => BmiStats != null
        ? (BmiStats.TrendValue >= 0 ? $"+{BmiStats.TrendValue:F1}" : $"{BmiStats.TrendValue:F1}")
        : "-";
    public string BmiMinDisplay => BmiStats != null ? $"{BmiStats.MinValue:F1}" : "-";
    public string BmiMaxDisplay => BmiStats != null ? $"{BmiStats.MaxValue:F1}" : "-";

    public string BodyFatCurrentDisplay => BodyFatStats != null ? $"{BodyFatStats.CurrentValue:F1}%" : "-";
    public string BodyFatAverageDisplay => BodyFatStats != null ? $"{BodyFatStats.AverageValue:F1}%" : "-";
    public string BodyFatTrendDisplay => BodyFatStats != null
        ? (BodyFatStats.TrendValue >= 0 ? $"+{BodyFatStats.TrendValue:F1}%" : $"{BodyFatStats.TrendValue:F1}%")
        : "-";
    public string BodyFatMinDisplay => BodyFatStats != null ? $"{BodyFatStats.MinValue:F1}%" : "-";
    public string BodyFatMaxDisplay => BodyFatStats != null ? $"{BodyFatStats.MaxValue:F1}%" : "-";

    #endregion

    #region Water Tab

    [ObservableProperty]
    private double _dailyWaterGoal;

    [ObservableProperty]
    private double _todayWater;

    [ObservableProperty]
    private double _waterProgress;

    [ObservableProperty]
    private string _waterStatusText = "";

    [ObservableProperty]
    private bool _hasWaterGoal;

    #endregion

    #region Calories Tab

    [ObservableProperty]
    private double _dailyCalorieGoal;

    [ObservableProperty]
    private double _consumedCalories;

    [ObservableProperty]
    private double _remainingCalories;

    [ObservableProperty]
    private bool _hasCalorieDeficit;

    [ObservableProperty]
    private string _calorieStatusText = "";

    [ObservableProperty]
    private ObservableCollection<FoodLogEntry> _todayMeals = [];

    [ObservableProperty]
    private bool _hasMeals;

    // Food Search
    [ObservableProperty]
    private bool _showFoodSearch;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private ObservableCollection<FoodSearchResult> _searchResults = [];

    [ObservableProperty]
    private FoodItem? _selectedFood;

    [ObservableProperty]
    private double _portionGrams = 100;

    [ObservableProperty]
    private int _selectedMeal;

    [ObservableProperty]
    private bool _showAddFoodPanel;

    [ObservableProperty]
    private double _calculatedCalories;

    [ObservableProperty]
    private double _calculatedProtein;

    [ObservableProperty]
    private double _calculatedCarbs;

    [ObservableProperty]
    private double _calculatedFat;

    // Macro Goals
    [ObservableProperty]
    private double _proteinGoal;

    [ObservableProperty]
    private double _carbsGoal;

    [ObservableProperty]
    private double _fatGoal;

    [ObservableProperty]
    private double _proteinConsumed;

    [ObservableProperty]
    private double _carbsConsumed;

    [ObservableProperty]
    private double _fatConsumed;

    public double ProteinProgress => ProteinGoal > 0 ? Math.Min(ProteinConsumed / ProteinGoal, 1.0) : 0;
    public double CarbsProgress => CarbsGoal > 0 ? Math.Min(CarbsConsumed / CarbsGoal, 1.0) : 0;
    public double FatProgress => FatGoal > 0 ? Math.Min(FatConsumed / FatGoal, 1.0) : 0;
    public bool HasMacroGoals => ProteinGoal > 0 || CarbsGoal > 0 || FatGoal > 0;

    public List<string> Meals =>
    [
        AppStrings.Breakfast,
        AppStrings.Lunch,
        AppStrings.Dinner,
        AppStrings.Snack
    ];

    #endregion

    #region Weekly Analysis (Wochenreport)

    [ObservableProperty] private bool _showAnalysisOverlay;
    [ObservableProperty] private bool _showAnalysisAdOverlay;
    [ObservableProperty] private string _avgWeightDisplay = "-";
    [ObservableProperty] private string _avgCaloriesDisplay = "-";
    [ObservableProperty] private string _avgWaterDisplay = "-";
    [ObservableProperty] private string _trendDisplay = "-";
    [ObservableProperty] private string _calorieTargetDisplay = "-";

    #endregion

    #region Tracking Export

    [ObservableProperty] private bool _showExportAdOverlay;

    #endregion

    #region Chart Range

    [ObservableProperty]
    private int _chartDays = PreferenceKeys.DefaultChartDays;

    public bool IsChart7Days => ChartDays == 7;
    public bool IsChart30Days => ChartDays == 30;
    public bool IsChart90Days => ChartDays == 90;

    partial void OnChartDaysChanged(int value)
    {
        OnPropertyChanged(nameof(IsChart7Days));
        OnPropertyChanged(nameof(IsChart30Days));
        OnPropertyChanged(nameof(IsChart90Days));
        _preferences.Set(PreferenceKeys.ChartDays, value);
        _ = LoadCurrentTabDataAsync();
    }

    #endregion

    #region Weekly Calories Chart

    [ObservableProperty]
    private IEnumerable<ISeries> _weeklyCaloriesSeries = [];

    [ObservableProperty]
    private Axis[] _weeklyXAxes = [];

    [ObservableProperty]
    private Axis[] _weeklyYAxes = [];

    [ObservableProperty]
    private bool _hasWeeklyData;

    #endregion

    #region Meal Grouping

    [ObservableProperty]
    private ObservableCollection<FoodLogEntry> _breakfastMeals = [];

    [ObservableProperty]
    private ObservableCollection<FoodLogEntry> _lunchMeals = [];

    [ObservableProperty]
    private ObservableCollection<FoodLogEntry> _dinnerMeals = [];

    [ObservableProperty]
    private ObservableCollection<FoodLogEntry> _snackMeals = [];

    public double BreakfastCalories => BreakfastMeals.Sum(m => m.Calories);
    public double LunchCalories => LunchMeals.Sum(m => m.Calories);
    public double DinnerCalories => DinnerMeals.Sum(m => m.Calories);
    public double SnackCalories => SnackMeals.Sum(m => m.Calories);

    public bool HasBreakfast => BreakfastMeals.Count > 0;
    public bool HasLunch => LunchMeals.Count > 0;
    public bool HasDinner => DinnerMeals.Count > 0;
    public bool HasSnack => SnackMeals.Count > 0;

    #endregion

    #region Chart Axes

    [ObservableProperty]
    private Axis[] _xAxes = [];

    [ObservableProperty]
    private Axis[] _yAxesWeight = [];

    [ObservableProperty]
    private Axis[] _yAxesBmi = [];

    [ObservableProperty]
    private Axis[] _yAxesBodyFat = [];

    // Chart-Rahmen: subtiler Rand, Fill kommt vom AXAML-Background
    public DrawMarginFrame ChartFrame { get; } = new()
    {
        Fill = new SolidColorPaint(SKColors.Transparent),
        Stroke = new SolidColorPaint(new SKColor(128, 128, 128, 30)) { StrokeThickness = 1 }
    };

    // Gesunde BMI-Zone (18.5-25) als grüner Bereich im Chart
    public RectangularSection[] BmiSections { get; } =
    [
        new RectangularSection
        {
            Yi = 18.5,
            Yj = 25,
            Fill = new SolidColorPaint(new SKColor(76, 175, 80, 30))
        }
    ];

    #endregion

    #region Commands

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void SelectWeightTab()
    {
        SelectedTab = ProgressTab.Weight;
    }

    [RelayCommand]
    private void SelectBodyTab()
    {
        SelectedTab = ProgressTab.Body;
    }

    [RelayCommand]
    private void SelectWaterTab()
    {
        SelectedTab = ProgressTab.Water;
    }

    [RelayCommand]
    private void SelectCaloriesTab()
    {
        SelectedTab = ProgressTab.Calories;
    }

    [RelayCommand]
    private void SelectBmi()
    {
        IsBmiSelected = true;
    }

    [RelayCommand]
    private void SelectBodyFat()
    {
        IsBmiSelected = false;
    }

    [RelayCommand]
    private void SetChartRange(string daysStr)
    {
        if (int.TryParse(daysStr, out var days) && (days == 7 || days == 30 || days == 90))
            ChartDays = days;
    }

    [RelayCommand]
    private void SetWeightGoalValue()
    {
        if (WeightGoal > 0 && WeightGoal <= 500)
        {
            _preferences.Set(PreferenceKeys.WeightGoal, WeightGoal);
            HasWeightGoal = true;
            UpdateWeightGoalStatus();
        }
    }

    [RelayCommand]
    private async Task CopyYesterdayMeals()
    {
        var yesterdayMeals = await _foodSearchService.GetFoodLogAsync(DateTime.Today.AddDays(-1));
        if (yesterdayMeals.Count == 0)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.NoMealsYesterday);
            return;
        }

        foreach (var meal in yesterdayMeals)
        {
            var copy = new FoodLogEntry
            {
                Date = DateTime.Today,
                FoodName = meal.FoodName,
                Grams = meal.Grams,
                Calories = meal.Calories,
                Protein = meal.Protein,
                Carbs = meal.Carbs,
                Fat = meal.Fat,
                Meal = meal.Meal
            };
            await _foodSearchService.SaveFoodLogAsync(copy);
        }

        FloatingTextRequested?.Invoke(
            string.Format(AppStrings.MealsCopied, yesterdayMeals.Count), "success");
        await LoadCalorieDataAsync();
    }

    [RelayCommand]
    private void ToggleAddForm()
    {
        ShowAddForm = !ShowAddForm;
        if (ShowAddForm)
        {
            ShowFoodSearch = false;
            ResetForm();
        }
    }

    [RelayCommand]
    private async Task AddEntry()
    {
        if (NewValue <= 0)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.InvalidValueEntered);
            return;
        }

        // Bereichsvalidierung je nach Tracking-Typ
        var maxValue = SelectedTab switch
        {
            ProgressTab.Weight => 500.0,
            ProgressTab.Body => IsBmiSelected ? 100.0 : 100.0,
            ProgressTab.Water => 20000.0,
            _ => double.MaxValue
        };

        if (NewValue > maxValue)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.InvalidValueEntered);
            return;
        }

        TrackingType type = SelectedTab switch
        {
            ProgressTab.Weight => TrackingType.Weight,
            ProgressTab.Body => IsBmiSelected ? TrackingType.Bmi : TrackingType.BodyFat,
            ProgressTab.Water => TrackingType.Water,
            _ => TrackingType.Weight
        };

        var entry = new TrackingEntry
        {
            Type = type,
            Value = NewValue,
            Date = NewDate,
            Note = string.IsNullOrWhiteSpace(NewNote) ? null : NewNote.Trim()
        };

        await _trackingService.AddEntryAsync(entry);
        ShowAddForm = false;
        ResetForm();
        await LoadCurrentTabDataAsync();

        // Floating Text Feedback je nach Tracking-Typ
        var displayText = type switch
        {
            TrackingType.Weight => $"{entry.Value:F1} kg",
            TrackingType.Bmi => $"BMI {entry.Value:F1}",
            TrackingType.BodyFat => $"{entry.Value:F1}%",
            TrackingType.Water => $"+{entry.Value:F0} ml",
            _ => $"{entry.Value:F1}"
        };
        FloatingTextRequested?.Invoke(displayText, "info");
    }

    [RelayCommand]
    private async Task DeleteEntry(TrackingEntry entry)
    {
        _undoCancellation?.Cancel();
        _undoCancellation = new CancellationTokenSource();

        _recentlyDeletedMeal = null; // Alten Meal-Undo verwerfen
        _recentlyDeletedEntry = entry;

        // Remove from appropriate collection
        switch (SelectedTab)
        {
            case ProgressTab.Weight:
                WeightEntries.Remove(entry);
                break;
            case ProgressTab.Body:
                if (IsBmiSelected)
                    BmiEntries.Remove(entry);
                else
                    BodyFatEntries.Remove(entry);
                break;
        }

        UndoMessage = string.Format(AppStrings.EntryDeletedOn, entry.Date.ToString("d", CultureInfo.CurrentCulture));
        ShowUndoBanner = true;

        try
        {
            await Task.Delay(PreferenceKeys.UndoTimeoutMs, _undoCancellation.Token);
            await _trackingService.DeleteEntryAsync(entry.Id);
            _recentlyDeletedEntry = null;
            await LoadCurrentTabDataAsync();
        }
        catch (TaskCanceledException)
        {
            // Undo triggered
        }
        finally
        {
            ShowUndoBanner = false;
        }
    }

    [RelayCommand]
    private void UndoDelete()
    {
        if (_recentlyDeletedEntry != null)
        {
            _undoCancellation?.Cancel();

            switch (SelectedTab)
            {
                case ProgressTab.Weight:
                    var weightList = WeightEntries.ToList();
                    weightList.Add(_recentlyDeletedEntry);
                    WeightEntries = new ObservableCollection<TrackingEntry>(weightList.OrderByDescending(e => e.Date));
                    break;
                case ProgressTab.Body:
                    if (IsBmiSelected)
                    {
                        var bmiList = BmiEntries.ToList();
                        bmiList.Add(_recentlyDeletedEntry);
                        BmiEntries = new ObservableCollection<TrackingEntry>(bmiList.OrderByDescending(e => e.Date));
                    }
                    else
                    {
                        var bfList = BodyFatEntries.ToList();
                        bfList.Add(_recentlyDeletedEntry);
                        BodyFatEntries = new ObservableCollection<TrackingEntry>(bfList.OrderByDescending(e => e.Date));
                    }
                    break;
            }

            _recentlyDeletedEntry = null;
            ShowUndoBanner = false;
        }

        if (_recentlyDeletedMeal != null)
        {
            _undoCancellation?.Cancel();
            var meals = TodayMeals.ToList();
            meals.Add(_recentlyDeletedMeal);
            TodayMeals = new ObservableCollection<FoodLogEntry>(meals.OrderBy(m => m.Date));
            HasMeals = TodayMeals.Count > 0;
            _recentlyDeletedMeal = null;
            ShowUndoBanner = false;
            // Lokal berechnen statt DB-Read (Eintrag existiert dort evtl. noch/nicht mehr)
            RecalculateCalorieDataFromMeals();
        }
    }

    [RelayCommand]
    private async Task QuickAddWater(string amountStr)
    {
        if (!int.TryParse(amountStr, out var amount)) return;
        try
        {
            var today = await _trackingService.GetLatestEntryAsync(TrackingType.Water);

            if (today != null && today.Date.Date == DateTime.Today)
            {
                today.Value += amount;
                await _trackingService.UpdateEntryAsync(today);
            }
            else
            {
                var entry = new TrackingEntry
                {
                    Type = TrackingType.Water,
                    Value = amount,
                    Date = DateTime.Today
                };
                await _trackingService.AddEntryAsync(entry);
            }

            await LoadWaterDataAsync();

            // Floating Text fuer Wasser-Hinzufuegung
            FloatingTextRequested?.Invoke($"+{amount} ml", "info");
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ErrorSavingData);
        }
    }

    [RelayCommand]
    private void SetWaterGoal()
    {
        // In Avalonia, water goal is set via UI binding directly
        // The view should provide an input mechanism
        // For now, use a default if not set
        if (DailyWaterGoal <= 0)
        {
            DailyWaterGoal = 2500;
        }
        HasWaterGoal = true;
        _preferences.Set(PreferenceKeys.WaterGoal, DailyWaterGoal);
        UpdateWaterStatus();
    }

    [RelayCommand]
    private void SetCalorieGoal()
    {
        // In Avalonia, calorie goal is set via UI binding directly
        if (DailyCalorieGoal <= 0)
        {
            DailyCalorieGoal = 2000;
        }
        _preferences.Set(PreferenceKeys.CalorieGoal, DailyCalorieGoal);
        UpdateCalorieStatus();
    }

    /// <summary>
    /// Saves the water goal from a user-entered value
    /// </summary>
    public void SaveWaterGoal(double goal)
    {
        if (goal > 0)
        {
            DailyWaterGoal = goal;
            HasWaterGoal = true;
            _preferences.Set(PreferenceKeys.WaterGoal, goal);
            UpdateWaterStatus();
        }
    }

    /// <summary>
    /// Saves the calorie goal from a user-entered value
    /// </summary>
    public void SaveCalorieGoal(double goal)
    {
        if (goal > 0)
        {
            DailyCalorieGoal = goal;
            _preferences.Set(PreferenceKeys.CalorieGoal, goal);
            UpdateCalorieStatus();
        }
    }

    [RelayCommand]
    private void ToggleFoodSearch()
    {
        ShowFoodSearch = !ShowFoodSearch;
        if (ShowFoodSearch)
        {
            ShowAddForm = false;
        }
        else
        {
            SearchQuery = "";
            SearchResults.Clear();
            SelectedFood = null;
            ShowAddFoodPanel = false;
        }
    }

    [RelayCommand]
    private void PerformFoodSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            return;
        }

        var results = _foodSearchService.Search(SearchQuery, 15);
        SearchResults = new ObservableCollection<FoodSearchResult>(results);
    }

    [RelayCommand]
    private void SelectFoodItem(FoodSearchResult result)
    {
        SelectedFood = result.Food;
        PortionGrams = result.Food.DefaultPortionGrams;
        UpdateFoodCalculations();
        ShowAddFoodPanel = true;
    }

    [RelayCommand]
    private async Task AddFoodToLog()
    {
        if (SelectedFood == null) return;

        var entry = new FoodLogEntry
        {
            Date = DateTime.Today,
            FoodName = SelectedFood.Name,
            Grams = PortionGrams,
            Calories = CalculatedCalories,
            Protein = CalculatedProtein,
            Carbs = CalculatedCarbs,
            Fat = CalculatedFat,
            Meal = (MealType)SelectedMeal
        };

        await _foodSearchService.SaveFoodLogAsync(entry);

        // Floating Text fuer hinzugefuegtes Essen
        FloatingTextRequested?.Invoke($"+{entry.Calories:F0} kcal", "info");

        SelectedFood = null;
        SearchQuery = "";
        ShowAddFoodPanel = false;
        ShowFoodSearch = false;

        await LoadCalorieDataAsync();
    }

    [RelayCommand]
    private void CancelAddFood()
    {
        SelectedFood = null;
        ShowAddFoodPanel = false;
    }

    [RelayCommand]
    private async Task DeleteMeal(FoodLogEntry entry)
    {
        _undoCancellation?.Cancel();
        _undoCancellation = new CancellationTokenSource();

        _recentlyDeletedEntry = null; // Alten Tracking-Undo verwerfen
        _recentlyDeletedMeal = entry;
        TodayMeals.Remove(entry);
        HasMeals = TodayMeals.Count > 0;

        // Summary lokal aus aktueller UI-Liste berechnen (Eintrag noch in DB)
        RecalculateCalorieDataFromMeals();

        UndoMessage = string.Format(AppStrings.ItemDeleted, entry.FoodName);
        ShowUndoBanner = true;

        try
        {
            await Task.Delay(PreferenceKeys.UndoTimeoutMs, _undoCancellation.Token);
            await _foodSearchService.DeleteFoodLogAsync(entry.Id);
            _recentlyDeletedMeal = null;
            await UpdateCalorieDataAsync();
        }
        catch (TaskCanceledException)
        {
            // Undo triggered
        }
        finally
        {
            ShowUndoBanner = false;
        }
    }

    #region Weekly Analysis Commands

    /// <summary>
    /// Analyse anfordern. Premium: direkt zeigen. Sonst: Ad-Overlay.
    /// </summary>
    [RelayCommand]
    private async Task RequestAnalysisAsync()
    {
        if (_purchaseService.IsPremium)
        {
            await GenerateAnalysisReportAsync();
            ShowAnalysisOverlay = true;
        }
        else
        {
            ShowAnalysisAdOverlay = true;
        }
    }

    /// <summary>
    /// User bestaetigt: Video schauen fuer Wochenreport.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmAnalysisAdAsync()
    {
        ShowAnalysisAdOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("detail_analysis");
        if (success)
        {
            await GenerateAnalysisReportAsync();
            ShowAnalysisOverlay = true;
        }
    }

    /// <summary>
    /// Analyse-Overlay schliessen.
    /// </summary>
    [RelayCommand]
    private void CloseAnalysis()
    {
        ShowAnalysisOverlay = false;
        ShowAnalysisAdOverlay = false;
    }

    /// <summary>
    /// Berechnet Durchschnittswerte der letzten 7 Tage.
    /// </summary>
    private async Task GenerateAnalysisReportAsync()
    {
        var startDate = DateTime.Today.AddDays(-6);
        var endDate = DateTime.Today;

        // Gewicht-Daten (letzte 7 Tage)
        var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight, startDate, endDate);
        if (weightEntries.Count > 0)
        {
            var avgWeight = weightEntries.Average(e => e.Value);
            AvgWeightDisplay = $"{avgWeight:F1} kg";

            // Trend: Differenz erstes und letztes Gewicht
            var sorted = weightEntries.OrderBy(e => e.Date).ToList();
            if (sorted.Count >= 2)
            {
                var diff = sorted[^1].Value - sorted[0].Value;
                TrendDisplay = diff >= 0 ? $"+{diff:F1} kg" : $"{diff:F1} kg";
            }
            else
            {
                TrendDisplay = "-";
            }
        }
        else
        {
            AvgWeightDisplay = "-";
            TrendDisplay = "-";
        }

        // Kalorien-Daten (letzte 7 Tage) - parallel laden
        var summaryTasks = Enumerable.Range(0, 7)
            .Select(i => _foodSearchService.GetDailySummaryAsync(startDate.AddDays(i)))
            .ToArray();
        var summaries = await Task.WhenAll(summaryTasks);
        double totalCals = 0;
        int daysWithCals = 0;
        foreach (var summary in summaries)
        {
            if (summary.TotalCalories > 0)
            {
                totalCals += summary.TotalCalories;
                daysWithCals++;
            }
        }
        AvgCaloriesDisplay = daysWithCals > 0 ? $"{totalCals / daysWithCals:F0} kcal" : "-";

        // Kalorienziel-Erreichung
        var calorieGoal = _preferences.Get(PreferenceKeys.CalorieGoal, 0.0);
        if (calorieGoal > 0 && daysWithCals > 0)
        {
            var avgCals = totalCals / daysWithCals;
            var percentage = avgCals / calorieGoal * 100;
            CalorieTargetDisplay = $"{percentage:F0}%";
        }
        else
        {
            CalorieTargetDisplay = "-";
        }

        // Wasser-Daten (letzte 7 Tage)
        var waterEntries = await _trackingService.GetEntriesAsync(TrackingType.Water, startDate, endDate);
        if (waterEntries.Count > 0)
        {
            var avgWater = waterEntries.Average(e => e.Value);
            AvgWaterDisplay = $"{avgWater:F0} ml";
        }
        else
        {
            AvgWaterDisplay = "-";
        }
    }

    #endregion

    #region Export Commands

    /// <summary>
    /// Tracking-Daten exportieren. Premium: direkt. Sonst: Ad-Overlay.
    /// </summary>
    [RelayCommand]
    private async Task ExportTrackingAsync()
    {
        if (_purchaseService.IsPremium)
        {
            await PerformExportAsync();
        }
        else
        {
            ShowExportAdOverlay = true;
        }
    }

    /// <summary>
    /// User bestaetigt: Video schauen fuer Export.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmExportAdAsync()
    {
        ShowExportAdOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("tracking_export");
        if (success)
        {
            await PerformExportAsync();
        }
    }

    /// <summary>
    /// Export-Ad-Overlay schliessen.
    /// </summary>
    [RelayCommand]
    private void CancelExport()
    {
        ShowExportAdOverlay = false;
    }

    /// <summary>
    /// Erstellt CSV-Export und teilt die Datei.
    /// </summary>
    private async Task PerformExportAsync()
    {
        try
        {
            var exportDir = _fileShareService.GetExportDirectory("FitnessRechner");
            var fileName = $"tracking_export_{DateTime.Today:yyyy-MM-dd}.csv";
            var filePath = Path.Combine(exportDir, fileName);

            var sb = new StringBuilder();
            // CSV Header
            sb.AppendLine("Date,Weight (kg),BMI,Water (ml),Calories");

            // Letzte 90 Tage exportieren
            var startDate = DateTime.Today.AddDays(-89);
            var endDate = DateTime.Today;

            var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight, startDate, endDate);
            var bmiEntries = await _trackingService.GetEntriesAsync(TrackingType.Bmi, startDate, endDate);
            var waterEntries = await _trackingService.GetEntriesAsync(TrackingType.Water, startDate, endDate);

            // Alle Daten nach Datum zusammenfuehren (letzter Eintrag pro Tag gewinnt)
            var weightByDate = weightEntries.GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.Last().Value);
            var bmiByDate = bmiEntries.GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.Last().Value);
            var waterByDate = waterEntries.GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.Last().Value);

            // Kalorien parallel laden (max 5 gleichzeitig um Thread Pool Starvation zu vermeiden)
            var dates = Enumerable.Range(0, 90).Select(i => startDate.AddDays(i)).ToArray();
            var throttle = new SemaphoreSlim(5);
            var summaryTasks = dates.Select(async d =>
            {
                await throttle.WaitAsync();
                try { return await _foodSearchService.GetDailySummaryAsync(d); }
                finally { throttle.Release(); }
            }).ToArray();
            var summaries = await Task.WhenAll(summaryTasks);

            for (int i = 0; i < 90; i++)
            {
                var date = dates[i];
                var weight = weightByDate.TryGetValue(date, out var w) ? w.ToString("F1", CultureInfo.InvariantCulture) : "";
                var bmi = bmiByDate.TryGetValue(date, out var b) ? b.ToString("F1", CultureInfo.InvariantCulture) : "";
                var water = waterByDate.TryGetValue(date, out var wa) ? wa.ToString("F0", CultureInfo.InvariantCulture) : "";
                var cals = summaries[i].TotalCalories > 0 ? summaries[i].TotalCalories.ToString("F0", CultureInfo.InvariantCulture) : "";

                // Nur Zeilen mit mindestens einem Wert
                if (!string.IsNullOrEmpty(weight) || !string.IsNullOrEmpty(bmi) ||
                    !string.IsNullOrEmpty(water) || !string.IsNullOrEmpty(cals))
                {
                    sb.AppendLine($"{date:yyyy-MM-dd},{weight},{bmi},{water},{cals}");
                }
            }

            await File.WriteAllTextAsync(filePath, sb.ToString());
            await _fileShareService.ShareFileAsync(filePath, AppStrings.ExportTracking, "text/csv");
            MessageRequested?.Invoke(AppStrings.AlertSuccess, AppStrings.ExportTracking);
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ErrorSavingData);
        }
    }

    #endregion

    #endregion

    #region Public Methods

    public async Task OnAppearingAsync()
    {
        ShowAds = !_purchaseService.IsPremium;

        // Wasser-Celebration Flag für neuen Tag zurücksetzen
        if (_lastWaterCelebrationDate != DateTime.Today)
        {
            _wasWaterGoalReached = false;
            _lastWaterCelebrationDate = DateTime.Today;
        }

        DailyWaterGoal = _preferences.Get(PreferenceKeys.WaterGoal, 0.0);
        HasWaterGoal = DailyWaterGoal > 0;

        DailyCalorieGoal = _preferences.Get(PreferenceKeys.CalorieGoal, 0.0);

        WeightGoal = _preferences.Get(PreferenceKeys.WeightGoal, 0.0);
        HasWeightGoal = WeightGoal > 0;

        ChartDays = _preferences.Get(PreferenceKeys.ChartDays, PreferenceKeys.DefaultChartDays);

        // Makro-Ziele laden
        ProteinGoal = _preferences.Get(PreferenceKeys.MacroProteinGoal, 0.0);
        CarbsGoal = _preferences.Get(PreferenceKeys.MacroCarbsGoal, 0.0);
        FatGoal = _preferences.Get(PreferenceKeys.MacroFatGoal, 0.0);
        OnPropertyChanged(nameof(HasMacroGoals));

        await LoadCurrentTabDataAsync();
    }

    #endregion

    #region Private Methods

    private async Task LoadCurrentTabDataAsync()
    {
        if (IsLoading) return;

        IsLoading = true;

        try
        {
            switch (SelectedTab)
            {
                case ProgressTab.Weight:
                    await LoadWeightDataAsync();
                    break;
                case ProgressTab.Body:
                    await LoadBodyDataAsync();
                    break;
                case ProgressTab.Water:
                    await LoadWaterDataAsync();
                    break;
                case ProgressTab.Calories:
                    await LoadCalorieDataAsync();
                    break;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadWeightDataAsync()
    {
        var entries = await _trackingService.GetEntriesAsync(TrackingType.Weight, ChartDays);
        // Pending-Delete-Eintrag filtern (verhindert Flicker während Undo-Phase)
        var pendingId = _recentlyDeletedEntry?.Id;
        WeightEntries = new ObservableCollection<TrackingEntry>(
            entries.Where(e => e.Id != pendingId).OrderByDescending(e => e.Date));
        WeightStats = await _trackingService.GetStatsAsync(TrackingType.Weight, ChartDays);

        OnPropertyChanged(nameof(WeightCurrentDisplay));
        OnPropertyChanged(nameof(WeightAverageDisplay));
        OnPropertyChanged(nameof(WeightTrendDisplay));
        OnPropertyChanged(nameof(WeightTrendIcon));
        OnPropertyChanged(nameof(WeightTrendColor));
        OnPropertyChanged(nameof(WeightTrendLabel));

        UpdateWeightGoalStatus();
        UpdateWeightChart();
        UpdateWeightMilestones();
    }

    private async Task LoadBodyDataAsync()
    {
        var pendingId = _recentlyDeletedEntry?.Id;

        var bmiEntries = await _trackingService.GetEntriesAsync(TrackingType.Bmi, ChartDays);
        BmiEntries = new ObservableCollection<TrackingEntry>(
            bmiEntries.Where(e => e.Id != pendingId).OrderByDescending(e => e.Date));
        BmiStats = await _trackingService.GetStatsAsync(TrackingType.Bmi, ChartDays);

        var bodyFatEntries = await _trackingService.GetEntriesAsync(TrackingType.BodyFat, ChartDays);
        BodyFatEntries = new ObservableCollection<TrackingEntry>(
            bodyFatEntries.Where(e => e.Id != pendingId).OrderByDescending(e => e.Date));
        BodyFatStats = await _trackingService.GetStatsAsync(TrackingType.BodyFat, ChartDays);

        OnPropertyChanged(nameof(HasBmiEntries));
        OnPropertyChanged(nameof(HasBodyFatEntries));
        OnPropertyChanged(nameof(BmiCurrentDisplay));
        OnPropertyChanged(nameof(BmiAverageDisplay));
        OnPropertyChanged(nameof(BmiTrendDisplay));
        OnPropertyChanged(nameof(BmiMinDisplay));
        OnPropertyChanged(nameof(BmiMaxDisplay));
        OnPropertyChanged(nameof(BodyFatCurrentDisplay));
        OnPropertyChanged(nameof(BodyFatAverageDisplay));
        OnPropertyChanged(nameof(BodyFatTrendDisplay));
        OnPropertyChanged(nameof(BodyFatMinDisplay));
        OnPropertyChanged(nameof(BodyFatMaxDisplay));

        UpdateBodyCharts();
    }

    private async Task LoadWaterDataAsync()
    {
        var todayEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Water);
        TodayWater = todayEntry?.Date.Date == DateTime.Today ? todayEntry.Value : 0;
        UpdateWaterStatus();
    }

    private async Task LoadCalorieDataAsync()
    {
        var meals = await _foodSearchService.GetFoodLogAsync(DateTime.Today);
        // Pending-Delete-Meal filtern (verhindert Flicker während Undo-Phase)
        var pendingMealId = _recentlyDeletedMeal?.Id;
        var filteredMeals = meals.Where(m => m.Id != pendingMealId).ToList();
        TodayMeals = new ObservableCollection<FoodLogEntry>(filteredMeals);
        HasMeals = TodayMeals.Count > 0;

        // Mahlzeiten nach Typ gruppieren
        GroupMealsByType(filteredMeals);

        await UpdateCalorieDataAsync();
        await LoadWeeklyCaloriesAsync();
    }

    private async Task UpdateCalorieDataAsync()
    {
        var summary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
        ConsumedCalories = summary.TotalCalories;
        ProteinConsumed = summary.TotalProtein;
        CarbsConsumed = summary.TotalCarbs;
        FatConsumed = summary.TotalFat;

        UpdateCalorieStatus();

        OnPropertyChanged(nameof(ProteinProgress));
        OnPropertyChanged(nameof(CarbsProgress));
        OnPropertyChanged(nameof(FatProgress));
        OnPropertyChanged(nameof(HasMacroGoals));
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

    private void ResetForm()
    {
        NewValue = SelectedTab switch
        {
            ProgressTab.Weight => 70,
            ProgressTab.Body => IsBmiSelected ? 22 : 20,
            ProgressTab.Water => 250,
            _ => 0
        };
        NewDate = DateTime.Today;
        NewNote = null;
    }

    private void UpdateFoodCalculations()
    {
        if (SelectedFood == null)
        {
            CalculatedCalories = 0;
            CalculatedProtein = 0;
            CalculatedCarbs = 0;
            CalculatedFat = 0;
            return;
        }

        var factor = PortionGrams / 100.0;
        CalculatedCalories = Math.Round(SelectedFood.CaloriesPer100g * factor, 1);
        CalculatedProtein = Math.Round(SelectedFood.ProteinPer100g * factor, 1);
        CalculatedCarbs = Math.Round(SelectedFood.CarbsPer100g * factor, 1);
        CalculatedFat = Math.Round(SelectedFood.FatPer100g * factor, 1);
    }

    partial void OnSearchQueryChanged(string value)
    {
        PerformFoodSearch();
    }

    partial void OnPortionGramsChanged(double value)
    {
        UpdateFoodCalculations();
    }

    private void UpdateWeightChart()
    {
        InitializeXAxes();

        if (WeightEntries.Count > 0)
        {
            var data = WeightEntries
                .OrderBy(e => e.Date)
                .Select(e => new DateTimePoint(e.Date, e.Value))
                .ToList();

            WeightChartSeries =
            [
                new LineSeries<DateTimePoint>
                {
                    Values = data,
                    Fill = new LinearGradientPaint(
                        [new SKColor(76, 175, 80, 150), new SKColor(76, 175, 80, 10)],
                        new SKPoint(0.5f, 0f),
                        new SKPoint(0.5f, 1f)),
                    Stroke = new SolidColorPaint(new SKColor(76, 175, 80)) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(new SKColor(76, 175, 80)),
                    GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                    GeometrySize = 12,
                    LineSmoothness = 0.5,
                    Name = AppStrings.TrackingWeight
                }
            ];

            var minVal = data.Min(d => d.Value) ?? 50;
            var maxVal = data.Max(d => d.Value) ?? 100;

            YAxesWeight =
            [
                new Axis
                {
                    MinLimit = Math.Floor(minVal - 5),
                    MaxLimit = Math.Ceiling(maxVal + 5),
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128, 40)) { StrokeThickness = 1 },
                    TextSize = 12,
                    Labeler = value => $"{value:F0}"
                }
            ];
        }
    }

    private void UpdateBodyCharts()
    {
        InitializeXAxes();

        // BMI Chart
        if (BmiEntries.Count > 0)
        {
            var bmiData = BmiEntries
                .OrderBy(e => e.Date)
                .Select(e => new DateTimePoint(e.Date, e.Value))
                .ToList();

            BmiChartSeries =
            [
                new LineSeries<DateTimePoint>
                {
                    Values = bmiData,
                    Fill = new LinearGradientPaint(
                        [new SKColor(33, 150, 243, 150), new SKColor(33, 150, 243, 10)],
                        new SKPoint(0.5f, 0f),
                        new SKPoint(0.5f, 1f)),
                    Stroke = new SolidColorPaint(new SKColor(33, 150, 243)) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(new SKColor(33, 150, 243)),
                    GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                    GeometrySize = 12,
                    LineSmoothness = 0.5,
                    Name = "BMI"
                }
            ];

            YAxesBmi =
            [
                new Axis
                {
                    MinLimit = 15,
                    MaxLimit = 40,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128, 40)) { StrokeThickness = 1 },
                    TextSize = 12,
                    Labeler = value => $"{value:F1}"
                }
            ];
        }

        // BodyFat Chart
        if (BodyFatEntries.Count > 0)
        {
            var bodyFatData = BodyFatEntries
                .OrderBy(e => e.Date)
                .Select(e => new DateTimePoint(e.Date, e.Value))
                .ToList();

            BodyFatChartSeries =
            [
                new LineSeries<DateTimePoint>
                {
                    Values = bodyFatData,
                    Fill = new LinearGradientPaint(
                        [new SKColor(255, 152, 0, 150), new SKColor(255, 152, 0, 10)],
                        new SKPoint(0.5f, 0f),
                        new SKPoint(0.5f, 1f)),
                    Stroke = new SolidColorPaint(new SKColor(255, 152, 0)) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(new SKColor(255, 152, 0)),
                    GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 },
                    GeometrySize = 12,
                    LineSmoothness = 0.5,
                    Name = AppStrings.BodyFat
                }
            ];

            YAxesBodyFat =
            [
                new Axis
                {
                    MinLimit = 5,
                    MaxLimit = 45,
                    LabelsPaint = new SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128, 40)) { StrokeThickness = 1 },
                    TextSize = 12,
                    Labeler = value => $"{value:F0}%"
                }
            ];
        }
    }

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

    /// <summary>
    /// Berechnet Meilenstein-Markierungen im Weight-Chart.
    /// Vertikale Linien an Tagen wo ein ganzes Kilo unterschritten wurde.
    /// </summary>
    private void UpdateWeightMilestones()
    {
        if (WeightEntries.Count < 2)
        {
            WeightMilestoneSections = [];
            return;
        }

        var sorted = WeightEntries.OrderBy(e => e.Date).ToList();
        var sections = new List<RectangularSection>();
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
                        sections.Add(new RectangularSection
                        {
                            Xi = sorted[i].Date.Ticks,
                            Xj = sorted[i].Date.Ticks,
                            Stroke = new SolidColorPaint(new SKColor(139, 92, 246, 60)) { StrokeThickness = 1 }
                        });
                    }
                }
            }
        }

        WeightMilestoneSections = sections.ToArray();
    }

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

    private async Task LoadWeeklyCaloriesAsync()
    {
        var startDate = DateTime.Today.AddDays(-6);
        var summaryTasks = Enumerable.Range(0, 7)
            .Select(i => _foodSearchService.GetDailySummaryAsync(startDate.AddDays(i)))
            .ToArray();
        var summaries = await Task.WhenAll(summaryTasks);

        HasWeeklyData = summaries.Any(s => s.TotalCalories > 0);
        if (!HasWeeklyData) return;

        var values = summaries.Select(s => s.TotalCalories).ToArray();
        var labels = Enumerable.Range(0, 7)
            .Select(i => startDate.AddDays(i).ToString("ddd", CultureInfo.CurrentCulture))
            .ToArray();

        WeeklyCaloriesSeries =
        [
            new ColumnSeries<double>
            {
                Values = values,
                Fill = new LinearGradientPaint(
                    [new SKColor(245, 158, 11, 200), new SKColor(239, 68, 68, 200)],
                    new SKPoint(0f, 0f), new SKPoint(0f, 1f)),
                Stroke = null,
                MaxBarWidth = 24,
                Rx = 4,
                Ry = 4
            }
        ];

        WeeklyXAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11
            }
        ];

        var maxVal = values.Max();
        WeeklyYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = maxVal > 0 ? maxVal * 1.2 : 2500,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11,
                Labeler = v => $"{v:F0}"
            }
        ];
    }

    private void InitializeXAxes()
    {
        XAxes =
        [
            new Axis
            {
                Labeler = value =>
                {
                    try
                    {
                        var ticks = (long)value;
                        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                            return "";
                        return new DateTime(ticks).ToString("d MMM", CultureInfo.CurrentCulture);
                    }
                    catch
                    {
                        return "";
                    }
                },
                LabelsRotation = -45,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                SeparatorsPaint = new SolidColorPaint(new SKColor(128, 128, 128, 25)) { StrokeThickness = 1 },
                TextSize = 10,
                UnitWidth = TimeSpan.FromDays(1).Ticks
            }
        ];
    }

    #endregion

    // IDisposable instead of Finalizer to avoid GC-Thread access to native SkiaSharp resources
    public void Dispose()
    {
        if (_disposed) return;

        // Clear charts first to release SKPaint references before GC runs
        WeightChartSeries = [];
        BmiChartSeries = [];
        BodyFatChartSeries = [];
        XAxes = [];
        YAxesWeight = [];
        YAxesBmi = [];
        YAxesBodyFat = [];

        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _undoCancellation = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
