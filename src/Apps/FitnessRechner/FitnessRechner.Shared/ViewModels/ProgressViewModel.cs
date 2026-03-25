using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Graphics;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace FitnessRechner.ViewModels;

public enum ProgressTab
{
    Weight,
    Body,
    Water,
    Calories,
    Activity
}

/// <summary>
/// ViewModel für den Progress-Tab (Tracking, Charts, Food-Log).
/// Aufgeteilt in Partial-Classes:
///   - ProgressViewModel.cs          → Felder, Constructor, Properties, Lifecycle
///   - ProgressViewModel.Tracking.cs → Add/Edit/Delete, Undo, Load/Refresh
///   - ProgressViewModel.Charts.cs   → Chart-Daten, Statistik, Zeitraum-Filter
///   - ProgressViewModel.Food.cs     → Food-Search, Quick-Add, Kopier-Funktionen
/// </summary>
public sealed partial class ProgressViewModel : ViewModelBase, IDisposable
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
    /// Confetti-Celebration auslösen.
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
    public bool IsActivityTab => SelectedTab == ProgressTab.Activity;

    partial void OnSelectedTabChanged(ProgressTab value)
    {
        OnPropertyChanged(nameof(IsWeightTab));
        OnPropertyChanged(nameof(IsBodyTab));
        OnPropertyChanged(nameof(IsWaterTab));
        OnPropertyChanged(nameof(IsCaloriesTab));
        OnPropertyChanged(nameof(IsActivityTab));

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
    private HealthTrendVisualization.DataPoint[] _weightChartData = [];

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
    private HealthTrendVisualization.MilestoneLine[] _weightMilestoneLines = [];

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
    private HealthTrendVisualization.DataPoint[] _bmiChartData = [];

    [ObservableProperty]
    private HealthTrendVisualization.DataPoint[] _bodyFatChartData = [];

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

    #region Premium-Features (Trendprognose + Makro-Donut)

    /// <summary>Trendprognose: "Bei diesem Tempo erreichst du dein Ziel am..."</summary>
    [ObservableProperty] private string _trendForecastDisplay = "";
    [ObservableProperty] private bool _hasTrendForecast;
    [ObservableProperty] private bool _showPremiumUpgradeHint;

    /// <summary>Makro-Verteilung als Prozent-Werte für Donut-Chart</summary>
    [ObservableProperty] private double _macroProteinPercent;
    [ObservableProperty] private double _macroCarbsPercent;
    [ObservableProperty] private double _macroFatPercent;
    [ObservableProperty] private bool _hasMacroDistribution;

    /// <summary>
    /// Berechnet die Gewichts-Trendprognose basierend auf den letzten 14 Tagen.
    /// Premium-Only Feature.
    /// </summary>
    public void CalculateTrendForecast()
    {
        if (!_purchaseService.IsPremium)
        {
            ShowPremiumUpgradeHint = true;
            HasTrendForecast = false;
            return;
        }

        ShowPremiumUpgradeHint = false;

        var weightGoal = _preferences.Get(PreferenceKeys.WeightGoal, 0.0);
        if (weightGoal <= 0 || WeightEntries.Count < 3)
        {
            HasTrendForecast = false;
            return;
        }

        // Lineare Regression über die letzten Einträge
        var sorted = WeightEntries.OrderBy(e => e.Date).ToList();
        var recent = sorted.TakeLast(Math.Min(14, sorted.Count)).ToList();

        if (recent.Count < 3)
        {
            HasTrendForecast = false;
            return;
        }

        var firstDate = recent[0].Date;
        var xs = recent.Select(e => (e.Date - firstDate).TotalDays).ToList();
        var ys = recent.Select(e => e.Value).ToList();

        var n = xs.Count;
        var sumX = xs.Sum();
        var sumY = ys.Sum();
        var sumXY = xs.Zip(ys, (x, y) => x * y).Sum();
        var sumXX = xs.Select(x => x * x).Sum();

        var slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
        var intercept = (sumY - slope * sumX) / n;

        if (Math.Abs(slope) < 0.01) // Kein nennenswerter Trend
        {
            TrendForecastDisplay = AppStrings.ResourceManager.GetString("TrendStable") ?? "Weight is stable";
            HasTrendForecast = true;
            return;
        }

        // Tage bis Ziel
        var currentWeight = ys.Last();
        var daysToGoal = (weightGoal - currentWeight) / slope;

        if (daysToGoal <= 0) // Ziel bereits überschritten oder falsche Richtung
        {
            var direction = slope > 0
                ? (AppStrings.ResourceManager.GetString("TrendGaining") ?? "Gaining weight")
                : (AppStrings.ResourceManager.GetString("TrendLosing") ?? "Losing weight");
            TrendForecastDisplay = direction;
            HasTrendForecast = true;
            return;
        }

        var goalDate = DateTime.Today.AddDays(daysToGoal);
        TrendForecastDisplay = string.Format(
            AppStrings.ResourceManager.GetString("TrendForecast") ?? "Goal by {0:d}",
            goalDate);
        HasTrendForecast = true;
    }

    /// <summary>
    /// Berechnet die Makro-Verteilung des Tages als Prozent-Werte.
    /// Premium-Only Feature.
    /// </summary>
    public void CalculateMacroDistribution()
    {
        if (!_purchaseService.IsPremium)
        {
            HasMacroDistribution = false;
            return;
        }

        var totalMacroKcal = (ProteinConsumed * 4) + (CarbsConsumed * 4) + (FatConsumed * 9);
        if (totalMacroKcal <= 0)
        {
            HasMacroDistribution = false;
            return;
        }

        MacroProteinPercent = (ProteinConsumed * 4) / totalMacroKcal * 100;
        MacroCarbsPercent = (CarbsConsumed * 4) / totalMacroKcal * 100;
        MacroFatPercent = (FatConsumed * 9) / totalMacroKcal * 100;
        HasMacroDistribution = true;
    }

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
    private float[] _weeklyCaloriesValues = [];

    [ObservableProperty]
    private string[] _weeklyDayLabels = [];

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

    #region Navigation Commands

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

    #endregion

    #region Lifecycle

    /// <summary>
    /// Wird beim Tab-Wechsel aufgerufen - lädt Einstellungen und aktuelle Tab-Daten.
    /// </summary>
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

        // Premium-Features berechnen
        CalculateTrendForecast();
        CalculateMacroDistribution();
    }

    // IDisposable: Chart-Daten freigeben
    public void Dispose()
    {
        if (_disposed) return;

        WeightChartData = [];
        BmiChartData = [];
        BodyFatChartData = [];
        WeeklyCaloriesValues = [];

        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _undoCancellation = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
