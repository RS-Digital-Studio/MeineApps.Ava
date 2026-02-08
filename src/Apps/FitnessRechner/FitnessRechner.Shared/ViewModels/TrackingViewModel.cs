using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace FitnessRechner.ViewModels;

public partial class TrackingViewModel : ObservableObject, IDisposable
{
    private readonly ITrackingService _trackingService;
    private readonly IPurchaseService _purchaseService;
    private readonly IFoodSearchService _foodSearchService;
    private readonly IPreferencesService _preferences;

    private const string CALORIE_GOAL_KEY = "daily_calorie_goal";
    private const string WATER_GOAL_KEY = "daily_water_goal";

    /// <summary>
    /// Raised when the VM wants to navigate
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Raised when the VM wants to show a message (title, message).
    /// </summary>
    public event Action<string, string>? MessageRequested;

    public TrackingViewModel(
        ITrackingService trackingService,
        IPurchaseService purchaseService,
        IFoodSearchService foodSearchService,
        IPreferencesService preferences)
    {
        _trackingService = trackingService;
        _purchaseService = purchaseService;
        _foodSearchService = foodSearchService;
        _preferences = preferences;
    }

    #region Properties

    [ObservableProperty]
    private TrackingType _selectedType = TrackingType.Weight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWeightOrBmiMode))]
    private bool _isCaloriesMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWeightOrBmiMode))]
    private bool _isWaterMode;

    public bool IsWeightOrBmiMode => !IsCaloriesMode && !IsWaterMode;

    [ObservableProperty]
    private ObservableCollection<TrackingEntry> _entries = [];

    [ObservableProperty]
    private TrackingStats? _stats;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _showAds;

    [ObservableProperty]
    private bool _showUndoBanner;

    [ObservableProperty]
    private string _undoMessage = string.Empty;

    private TrackingEntry? _recentlyDeletedEntry;
    private CancellationTokenSource? _undoCancellation;

    [ObservableProperty]
    private bool _showAddForm;

    [ObservableProperty]
    private double _newValue;

    [ObservableProperty]
    private DateTime _newDate = DateTime.Today;

    [ObservableProperty]
    private string? _newNote;

    // Calories Tab Properties
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

    [ObservableProperty]
    private bool _showFoodSearch;

    // Water Tab Properties
    [ObservableProperty]
    private double _dailyWaterGoal;

    [ObservableProperty]
    private double _todayWater;

    [ObservableProperty]
    private double _waterProgress;

    [ObservableProperty]
    private bool _hasWaterGoal;

    [ObservableProperty]
    private string _waterStatusText = "";

    // Food Search Properties
    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private ObservableCollection<FoodSearchResult> _searchResults = [];

    [ObservableProperty]
    private bool _hasSearchResults;

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

    public List<string> Meals { get; } =
    [
        AppStrings.Breakfast,
        AppStrings.Lunch,
        AppStrings.Dinner,
        AppStrings.Snack
    ];

    public string TypeTitle => SelectedType switch
    {
        TrackingType.Weight => AppStrings.TrackingWeight,
        TrackingType.Bmi => "BMI",
        TrackingType.Water => AppStrings.TrackingWater,
        _ => AppStrings.Tracking
    };

    public string TypeUnit => SelectedType switch
    {
        TrackingType.Weight => "kg",
        TrackingType.Bmi => "",
        TrackingType.Water => "ml",
        _ => ""
    };

    public string StatsDisplay => Stats != null
        ? $"{AppStrings.Current}: {Stats.CurrentValue:N1} {TypeUnit} | " +
          $"{AppStrings.Average}: {Stats.AverageValue:N1} {TypeUnit} | " +
          $"{AppStrings.Trend}: {(Stats.TrendValue >= 0 ? "+" : "")}{Stats.TrendValue:N1}"
        : AppStrings.TrackingNoEntries;

    public string TrendIcon => Stats?.TrendValue switch
    {
        > 0 when SelectedType == TrackingType.Weight => "up",
        < 0 when SelectedType == TrackingType.Weight => "down",
        > 0 when SelectedType == TrackingType.Water => "good",
        < 0 when SelectedType == TrackingType.Water => "bad",
        _ => "neutral"
    };

    partial void OnStatsChanged(TrackingStats? value)
    {
        OnPropertyChanged(nameof(StatsDisplay));
        OnPropertyChanged(nameof(TrendIcon));
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void SelectWeight()
    {
        SelectedType = TrackingType.Weight;
    }

    [RelayCommand]
    private void SelectBmi()
    {
        SelectedType = TrackingType.Bmi;
    }

    [RelayCommand]
    private void SelectWater()
    {
        SelectedType = TrackingType.Water;
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

        var entry = new TrackingEntry
        {
            Type = SelectedType,
            Value = NewValue,
            Date = NewDate,
            Note = string.IsNullOrWhiteSpace(NewNote) ? null : NewNote.Trim()
        };

        await _trackingService.AddEntryAsync(entry);
        ShowAddForm = false;
        ResetForm();
        await LoadEntriesAsync();
    }

    [RelayCommand]
    private async Task DeleteEntry(TrackingEntry entry)
    {
        _undoCancellation?.Cancel();
        _undoCancellation = new CancellationTokenSource();

        _recentlyDeletedEntry = entry;

        Entries.Remove(entry);

        UndoMessage = string.Format(AppStrings.EntryDeletedOn, entry.Date.ToString("dd.MM.yyyy"));
        ShowUndoBanner = true;

        try
        {
            await Task.Delay(5000, _undoCancellation.Token);
            await _trackingService.DeleteEntryAsync(entry.Id);
            _recentlyDeletedEntry = null;
        }
        catch (TaskCanceledException)
        {
            // Undo was triggered
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

            var entries = Entries.ToList();
            entries.Add(_recentlyDeletedEntry);
            Entries = new ObservableCollection<TrackingEntry>(entries.OrderByDescending(e => e.Date));

            _recentlyDeletedEntry = null;
            ShowUndoBanner = false;
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

            if (SelectedType == TrackingType.Water)
            {
                await LoadEntriesAsync();
                await LoadWaterDataAsync();
            }
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ErrorSavingData);
        }
    }

    [RelayCommand]
    private async Task SelectCalories()
    {
        IsCaloriesMode = true;
        ShowAddForm = false;
        await LoadCalorieDataAsync();
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
        }
    }

    [RelayCommand]
    private void PerformFoodSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            HasSearchResults = false;
            return;
        }

        var results = _foodSearchService.Search(SearchQuery, 15);
        SearchResults = new ObservableCollection<FoodSearchResult>(results);
        HasSearchResults = SearchResults.Count > 0;
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

        // Reset
        SelectedFood = null;
        SearchQuery = "";
        ShowAddFoodPanel = false;
        ShowFoodSearch = false;

        // Refresh
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
        await _foodSearchService.DeleteFoodLogAsync(entry.Id);
        await LoadCalorieDataAsync();
    }

    /// <summary>
    /// Saves the calorie goal from a user-entered value
    /// </summary>
    public void SaveCalorieGoal(double goal)
    {
        if (goal > 0)
        {
            DailyCalorieGoal = goal;
            _preferences.Set(CALORIE_GOAL_KEY, goal);
            UpdateCalorieStatus();
        }
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
            _preferences.Set(WATER_GOAL_KEY, goal);
            _ = LoadWaterDataAsync();
        }
    }

    #endregion

    #region Public Methods

    public async Task OnAppearingAsync()
    {
        IsPremium = _purchaseService.IsPremium;
        ShowAds = !IsPremium;

        DailyWaterGoal = _preferences.Get(WATER_GOAL_KEY, 0.0);
        HasWaterGoal = DailyWaterGoal > 0;

        if (IsCaloriesMode)
        {
            await LoadCalorieDataAsync();
        }
        else
        {
            await LoadEntriesAsync();

            if (SelectedType == TrackingType.Water)
            {
                await LoadWaterDataAsync();
            }
        }
    }

    #endregion

    #region Private Methods

    private async Task LoadEntriesAsync()
    {
        if (IsLoading) return;

        IsLoading = true;

        try
        {
            var entries = await _trackingService.GetEntriesAsync(SelectedType, 30);
            Entries = new ObservableCollection<TrackingEntry>(entries);

            Stats = await _trackingService.GetStatsAsync(SelectedType, 30);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        IsRefreshing = true;

        try
        {
            await LoadEntriesAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ResetForm()
    {
        NewValue = SelectedType switch
        {
            TrackingType.Weight => 70,
            TrackingType.Bmi => 22,
            TrackingType.Water => 250,
            _ => 0
        };
        NewDate = DateTime.Today;
        NewNote = null;
    }

    private async Task LoadWaterDataAsync()
    {
        var todayEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Water);
        TodayWater = todayEntry?.Date.Date == DateTime.Today ? todayEntry.Value : 0;

        UpdateWaterStatus();
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
            WaterStatusText = $"{remaining:F0} ml {AppStrings.Remaining.ToLower()}";
        }
        else
        {
            WaterStatusText = AppStrings.GoalReached;
        }
    }

    private async Task LoadCalorieDataAsync()
    {
        IsLoading = true;

        try
        {
            DailyCalorieGoal = _preferences.Get(CALORIE_GOAL_KEY, 0.0);

            var meals = await _foodSearchService.GetFoodLogAsync(DateTime.Today);
            TodayMeals = new ObservableCollection<FoodLogEntry>(meals);
            HasMeals = TodayMeals.Count > 0;

            var summary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
            ConsumedCalories = summary.TotalCalories;

            UpdateCalorieStatus();
        }
        finally
        {
            IsLoading = false;
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

    partial void OnSelectedTypeChanged(TrackingType value)
    {
        IsCaloriesMode = false;
        IsWaterMode = value == TrackingType.Water;
        OnPropertyChanged(nameof(TypeTitle));
        OnPropertyChanged(nameof(TypeUnit));
        _ = LoadEntriesAsync();

        if (IsWaterMode)
        {
            _ = LoadWaterDataAsync();
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        PerformFoodSearch();
    }

    partial void OnPortionGramsChanged(double value)
    {
        UpdateFoodCalculations();
    }

    #endregion

    // IDisposable to clean up undo cancellation token
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;

        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _undoCancellation = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
