using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace FitnessRechner.ViewModels;

public partial class FoodSearchViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IFoodSearchService _foodSearchService;
    private readonly IPurchaseService _purchaseService;
    private readonly IPreferencesService _preferences;
    private readonly IScanLimitService _scanLimitService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IBarcodeService _barcodeService;
    private CancellationTokenSource? _searchCancellationTokenSource;

    // Preference-Keys zentral in PreferenceKeys.cs

    /// <summary>
    /// Raised when the VM wants to navigate
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Raised when the VM wants to show a message (title, message).
    /// </summary>
    public event Action<string, string>? MessageRequested;

    public FoodSearchViewModel(
        IFoodSearchService foodSearchService,
        IPurchaseService purchaseService,
        IPreferencesService preferences,
        IScanLimitService scanLimitService,
        IRewardedAdService rewardedAdService,
        IBarcodeService barcodeService)
    {
        _foodSearchService = foodSearchService;
        _purchaseService = purchaseService;
        _preferences = preferences;
        _scanLimitService = scanLimitService;
        _rewardedAdService = rewardedAdService;
        _barcodeService = barcodeService;
        LoadCalorieGoal();
        LoadMacroGoals();
        UpdateShowAds();
        UpdateRemainingScans();
        CheckExtendedFoodAccess();
    }

    /// <summary>
    /// Apply parameters passed from navigation (e.g. scanned food from barcode scanner)
    /// </summary>
    public void ApplyParameters(Dictionary<string, object> parameters)
    {
        if (parameters == null) return;

        if (parameters.TryGetValue("SelectedFood", out var foodObj) && foodObj is FoodItem scannedFood)
        {
            SelectedFood = scannedFood;
        }
    }

    // Search
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private ObservableCollection<FoodSearchResult> _searchResults = [];
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private bool _hasMoreResults;

    // Erweiterte Food-Datenbank (24h Online-Zugang via Rewarded Ad)
    [ObservableProperty] private bool _hasExtendedFoodAccess;
    [ObservableProperty] private bool _showExtendedDbOverlay;
    [ObservableProperty] private bool _showExtendedDbHint;

    // Extended Food DB Key zentral in PreferenceKeys.cs

    // Pagination
    private List<FoodSearchResult> _allSearchResults = [];
    private const int RESULTS_PAGE_SIZE = 15;

    // Ads
    [ObservableProperty] private bool _showAds;

    // Scan-Limit (3 kostenlose Scans pro Tag, +5 via Rewarded Ad)
    [ObservableProperty] private bool _showScanLimitOverlay;
    [ObservableProperty] private int _remainingScansDisplay;
    [ObservableProperty] private string _remainingScansText = "";

    // Undo functionality
    [ObservableProperty] private bool _showUndoBanner;
    [ObservableProperty] private string _undoMessage = string.Empty;
    private FoodLogEntry? _recentlyDeletedLogEntry;
    private CancellationTokenSource? _undoCancellation;

    // Selected Food
    [ObservableProperty] private FoodItem? _selectedFood;
    [ObservableProperty] private double _portionGrams = 100;
    [ObservableProperty] private int _selectedMeal;
    [ObservableProperty] private bool _showAddPanel;

    private const double MIN_PORTION_GRAMS = 1;
    private const double MAX_PORTION_GRAMS = 10000;
    private const double HIGH_CALORIE_WARNING_THRESHOLD = 2000;
    private const int SEARCH_DEBOUNCE_MS = 300;
    // Undo-Timeout zentral in PreferenceKeys.cs

    // Calculated values for selected portion
    [ObservableProperty] private double _calculatedCalories;
    [ObservableProperty] private double _calculatedProtein;
    [ObservableProperty] private double _calculatedCarbs;
    [ObservableProperty] private double _calculatedFat;

    // Today's Log
    [ObservableProperty] private ObservableCollection<FoodLogEntry> _todayLog = [];
    [ObservableProperty] private DailyNutritionSummary? _todaySummary;
    [ObservableProperty] private bool _hasLogEntries;

    // Calorie Goal & Remaining Calories
    [ObservableProperty] private double _dailyCalorieGoal;
    [ObservableProperty] private double _remainingCalories;
    [ObservableProperty] private bool _hasCalorieDeficit;
    [ObservableProperty] private string _calorieStatusText = "";
    [ObservableProperty] private bool _hasCalorieGoal;

    // Macro Goals
    [ObservableProperty] private MacroGoals? _macroGoals;
    [ObservableProperty] private DailyMacroSummary? _dailyMacroSummary;
    [ObservableProperty] private bool _hasMacroGoals;

    // Quick-Add Kalorien
    [ObservableProperty] private bool _showQuickAddPanel;
    [ObservableProperty] private double _quickAddCalories;
    [ObservableProperty] private string _quickAddName = "";
    [ObservableProperty] private int _quickAddMeal;

    // Favorites
    [ObservableProperty] private ObservableCollection<FavoriteFoodEntry> _favorites = [];
    [ObservableProperty] private bool _hasFavorites;
    [ObservableProperty] private bool _isSelectedFoodFavorite;

    // Letzte Einträge (Chips über Suchergebnissen)
    [ObservableProperty] private ObservableCollection<FoodItem> _recentFoods = [];
    [ObservableProperty] private bool _hasRecentFoods;

    public List<string> Meals =>
    [
        AppStrings.Breakfast,
        AppStrings.Lunch,
        AppStrings.Dinner,
        AppStrings.Snack
    ];

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            HasResults = false;
            HasMoreResults = false;
            _allSearchResults.Clear();
            return;
        }

        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();
        var token = _searchCancellationTokenSource.Token;
        var capturedQuery = value.Trim(); // Query-Wert capturen statt Property zu lesen

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SEARCH_DEBOUNCE_MS, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            PerformSearch(capturedQuery);
                        }
                    });
                }
            }
            catch (TaskCanceledException)
            {
                // Erwartet wenn Suche abgebrochen wird
            }
        }, token);
    }

    partial void OnPortionGramsChanged(double value)
    {
        if (value < MIN_PORTION_GRAMS)
        {
            PortionGrams = MIN_PORTION_GRAMS;
            return;
        }

        if (value > MAX_PORTION_GRAMS)
        {
            PortionGrams = MAX_PORTION_GRAMS;
            return;
        }

        UpdateCalculatedValues();
    }

    partial void OnSelectedFoodChanged(FoodItem? value)
    {
        if (value != null)
        {
            PortionGrams = value.DefaultPortionGrams;
            UpdateCalculatedValues();
            ShowAddPanel = true;
            _ = SafeUpdateSelectedFoodFavoriteStatusAsync();
        }
        else
        {
            ShowAddPanel = false;
            IsSelectedFoodFavorite = false;
        }
    }

    private async Task SafeUpdateSelectedFoodFavoriteStatusAsync()
    {
        try
        {
            await UpdateSelectedFoodFavoriteStatus();
        }
        catch (Exception)
        {
            // Silently handle - favorite status will default to false
            IsSelectedFoodFavorite = false;
        }
    }

    private void PerformSearch(string query)
    {
        IsSearching = true;

        _allSearchResults = _foodSearchService.Search(query, 200).ToList();

        SearchResults.Clear();
        var firstPage = _allSearchResults.Take(RESULTS_PAGE_SIZE);
        foreach (var result in firstPage)
        {
            SearchResults.Add(result);
        }

        HasResults = SearchResults.Count > 0;
        HasMoreResults = _allSearchResults.Count > RESULTS_PAGE_SIZE;
        IsSearching = false;
        UpdateExtendedDbHint();
    }

    [RelayCommand]
    private void LoadMoreResults()
    {
        if (!HasMoreResults || _allSearchResults.Count == 0) return;

        var currentCount = SearchResults.Count;
        var nextPage = _allSearchResults.Skip(currentCount).Take(RESULTS_PAGE_SIZE);

        foreach (var result in nextPage)
        {
            SearchResults.Add(result);
        }

        HasMoreResults = SearchResults.Count < _allSearchResults.Count;
    }

    private void UpdateCalculatedValues()
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

    [RelayCommand]
    private void SelectFood(FoodSearchResult result)
    {
        SelectedFood = result.Food;
    }

    [RelayCommand]
    private async Task AddToLog()
    {
        if (SelectedFood == null) return;

        // High calorie warning
        if (CalculatedCalories > HIGH_CALORIE_WARNING_THRESHOLD)
        {
            MessageRequested?.Invoke(AppStrings.Warning, string.Format(AppStrings.HighCalorieWarning, $"{CalculatedCalories:F0}"));
        }

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

        try
        {
            await _foodSearchService.SaveFoodLogAsync(entry);

            // Increment favorite usage if applicable
            if (IsSelectedFoodFavorite)
            {
                await _foodSearchService.IncrementFavoriteUsageAsync(SelectedFood.Name);
            }

            // Reset
            SelectedFood = null;
            SearchQuery = "";
            ShowAddPanel = false;

            // Refresh today's log
            await LoadTodaySummaryAsync();
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ErrorSavingData);
        }
    }

    [RelayCommand]
    private void CancelAdd()
    {
        SelectedFood = null;
        ShowAddPanel = false;
    }

    [RelayCommand]
    private void OpenQuickAdd()
    {
        ShowQuickAddPanel = true;
        QuickAddCalories = 0;
        QuickAddName = "";
        QuickAddMeal = 3; // Snack als Default
    }

    [RelayCommand]
    private void CancelQuickAdd()
    {
        ShowQuickAddPanel = false;
    }

    [RelayCommand]
    private async Task ConfirmQuickAdd()
    {
        if (QuickAddCalories <= 0) return;

        var name = string.IsNullOrWhiteSpace(QuickAddName)
            ? AppStrings.QuickAddCalories
            : QuickAddName.Trim();

        var entry = new FoodLogEntry
        {
            Date = DateTime.Today,
            FoodName = name,
            Grams = 0,
            Calories = QuickAddCalories,
            Protein = 0,
            Carbs = 0,
            Fat = 0,
            Meal = (MealType)QuickAddMeal
        };

        try
        {
            await _foodSearchService.SaveFoodLogAsync(entry);
            ShowQuickAddPanel = false;
            await LoadTodaySummaryAsync();
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(AppStrings.Error, AppStrings.ErrorSavingData);
        }
    }

    [RelayCommand]
    private async Task DeleteLogEntry(FoodLogEntry entry)
    {
        _undoCancellation?.Cancel();
        _undoCancellation = new CancellationTokenSource();

        _recentlyDeletedLogEntry = entry;

        TodayLog.Remove(entry);
        HasLogEntries = TodayLog.Count > 0;

        // Summary aus aktueller UI-Kollektion berechnen (nicht aus DB,
        // da Eintrag dort noch existiert bis Undo-Timeout abläuft)
        RecalculateSummaryFromTodayLog();

        UndoMessage = string.Format(AppStrings.ItemDeleted, entry.FoodName);
        ShowUndoBanner = true;

        try
        {
            await Task.Delay(PreferenceKeys.UndoTimeoutMs, _undoCancellation.Token);
            await _foodSearchService.DeleteFoodLogAsync(entry.Id);
            _recentlyDeletedLogEntry = null;
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
    private async Task UndoDeleteLog()
    {
        if (_recentlyDeletedLogEntry != null)
        {
            _undoCancellation?.Cancel();

            var logs = TodayLog.ToList();
            logs.Add(_recentlyDeletedLogEntry);
            TodayLog = new ObservableCollection<FoodLogEntry>(logs.OrderByDescending(e => e.Date));
            HasLogEntries = TodayLog.Count > 0;

            // Summary aus wiederhergestellter UI-Kollektion neu berechnen
            RecalculateSummaryFromTodayLog();

            _recentlyDeletedLogEntry = null;
            ShowUndoBanner = false;
        }
    }

    [RelayCommand]
    private async Task RefreshLog()
    {
        await LoadTodaySummaryAsync();
    }

    private async Task LoadTodaySummaryAsync()
    {
        if (IsLoading) return;

        IsLoading = true;

        try
        {
            var entries = await _foodSearchService.GetFoodLogAsync(DateTime.Today);
            TodayLog = new ObservableCollection<FoodLogEntry>(entries);
            HasLogEntries = TodayLog.Count > 0;

            TodaySummary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);

            UpdateRemainingCalories();
            UpdateDailyMacroSummary();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadCalorieGoal()
    {
        DailyCalorieGoal = _preferences.Get(PreferenceKeys.CalorieGoal, 0.0);
        HasCalorieGoal = DailyCalorieGoal > 0;
        UpdateRemainingCalories();
    }

    /// <summary>
    /// Berechnet Summary aus der aktuellen TodayLog-Kollektion statt aus der DB.
    /// Wird bei Delete/Undo verwendet, da der DB-Zustand während der Undo-Phase
    /// nicht dem UI-Zustand entspricht.
    /// </summary>
    private void RecalculateSummaryFromTodayLog()
    {
        var entries = TodayLog.ToList();
        TodaySummary = new DailyNutritionSummary(
            DateTime.Today,
            entries.Sum(e => e.Calories),
            entries.Sum(e => e.Protein),
            entries.Sum(e => e.Carbs),
            entries.Sum(e => e.Fat),
            entries.Count);

        UpdateRemainingCalories();
        UpdateDailyMacroSummary();
    }

    private void UpdateRemainingCalories()
    {
        if (TodaySummary == null || !HasCalorieGoal)
        {
            RemainingCalories = 0;
            CalorieStatusText = "";
            return;
        }

        var consumed = TodaySummary.TotalCalories;
        var remaining = DailyCalorieGoal - consumed;
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

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void UseDefaultPortion()
    {
        if (SelectedFood != null)
        {
            PortionGrams = SelectedFood.DefaultPortionGrams;
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
            HasCalorieGoal = true;
            _preferences.Set(PreferenceKeys.CalorieGoal, goal);
            UpdateRemainingCalories();
        }
    }

    [RelayCommand]
    private async Task OpenBarcodeScanner()
    {
        // Scan-Limit pruefen (Premium-Nutzer sind unbegrenzt)
        if (!_scanLimitService.CanScan)
        {
            ShowScanLimitOverlay = true;
            return;
        }

        // Nativen Barcode-Scanner starten (Android: CameraX, Desktop: null)
        var barcode = await _barcodeService.ScanBarcodeAsync();

        _scanLimitService.UseOneScan();
        UpdateRemainingScans();

        if (!string.IsNullOrEmpty(barcode))
        {
            // Barcode erkannt → direkt zur Scanner-Ergebnis-Seite mit Barcode
            NavigationRequested?.Invoke($"BarcodeScannerPage?barcode={barcode}");
        }
        else
        {
            // Kein Barcode (abgebrochen oder Desktop) → manuelle Eingabe
            NavigationRequested?.Invoke("BarcodeScannerPage");
        }
    }

    /// <summary>
    /// Rewarded Ad schauen um +5 Scans zu erhalten, danach wird der blockierte Scan ausgefuehrt.
    /// </summary>
    [RelayCommand]
    private async Task WatchAdForScansAsync()
    {
        ShowScanLimitOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("barcode_scan");
        if (success)
        {
            _scanLimitService.AddScans(5);
            UpdateRemainingScans();

            // Blockierten Scan jetzt ausfuehren
            var barcode = await _barcodeService.ScanBarcodeAsync();

            _scanLimitService.UseOneScan();
            UpdateRemainingScans();

            if (!string.IsNullOrEmpty(barcode))
            {
                NavigationRequested?.Invoke($"BarcodeScannerPage?barcode={barcode}");
            }
            else
            {
                NavigationRequested?.Invoke("BarcodeScannerPage");
            }
        }
    }

    /// <summary>
    /// Scan-Limit Overlay schliessen ohne Ad zu schauen.
    /// </summary>
    [RelayCommand]
    private void CancelScanLimit()
    {
        ShowScanLimitOverlay = false;
    }

    /// <summary>
    /// Aktualisiert die Anzeige der verbleibenden Scans.
    /// </summary>
    private void UpdateRemainingScans()
    {
        RemainingScansDisplay = _scanLimitService.RemainingScans;
        RemainingScansText = string.Format(AppStrings.RemainingScans, RemainingScansDisplay);
    }

    private void LoadMacroGoals()
    {
        var protein = _preferences.Get(PreferenceKeys.MacroProteinGoal, 0.0);
        var carbs = _preferences.Get(PreferenceKeys.MacroCarbsGoal, 0.0);
        var fat = _preferences.Get(PreferenceKeys.MacroFatGoal, 0.0);

        if (protein > 0 || carbs > 0 || fat > 0)
        {
            MacroGoals = new MacroGoals
            {
                ProteinGrams = protein,
                CarbsGrams = carbs,
                FatGrams = fat
            };
            HasMacroGoals = true;
        }
        else
        {
            HasMacroGoals = false;
        }

        UpdateDailyMacroSummary();
    }

    private void UpdateDailyMacroSummary()
    {
        if (TodaySummary == null || MacroGoals == null)
        {
            DailyMacroSummary = null;
            return;
        }

        DailyMacroSummary = new DailyMacroSummary(
            TodaySummary.TotalProtein,
            TodaySummary.TotalCarbs,
            TodaySummary.TotalFat,
            TodaySummary.TotalCalories,
            MacroGoals
        );
    }

    /// <summary>
    /// Saves macro goals from user-entered values
    /// </summary>
    public void SaveMacroGoals(double protein, double carbs, double fat)
    {
        if (protein < 0 || carbs < 0 || fat < 0) return;

        MacroGoals = new MacroGoals
        {
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat
        };

        _preferences.Set(PreferenceKeys.MacroProteinGoal, protein);
        _preferences.Set(PreferenceKeys.MacroCarbsGoal, carbs);
        _preferences.Set(PreferenceKeys.MacroFatGoal, fat);

        HasMacroGoals = true;

        // Kalorienziel nur automatisch setzen wenn noch keins manuell definiert wurde
        if (!HasCalorieGoal)
        {
            DailyCalorieGoal = MacroGoals.TotalCalories;
            HasCalorieGoal = true;
            _preferences.Set(PreferenceKeys.CalorieGoal, DailyCalorieGoal);
        }

        UpdateDailyMacroSummary();
        UpdateRemainingCalories();
    }

    /// <summary>
    /// Calculates macro grams from calorie goal and preset distribution.
    /// Protein: 4 kcal/g, Carbs: 4 kcal/g, Fat: 9 kcal/g
    /// </summary>
    public static (double protein, double carbs, double fat) CalculateMacrosFromPreset(double calories, string preset)
    {
        double proteinPercent, carbsPercent, fatPercent;

        if (preset.Contains("High-Protein"))
        {
            proteinPercent = 0.40;
            carbsPercent = 0.30;
            fatPercent = 0.30;
        }
        else if (preset.Contains("Low-Carb"))
        {
            proteinPercent = 0.30;
            carbsPercent = 0.20;
            fatPercent = 0.50;
        }
        else if (preset.Contains("Muscle"))
        {
            proteinPercent = 0.35;
            carbsPercent = 0.45;
            fatPercent = 0.20;
        }
        else // Balanced (default)
        {
            proteinPercent = 0.30;
            carbsPercent = 0.40;
            fatPercent = 0.30;
        }

        double proteinGrams = Math.Round(calories * proteinPercent / 4);
        double carbsGrams = Math.Round(calories * carbsPercent / 4);
        double fatGrams = Math.Round(calories * fatPercent / 9);

        return (proteinGrams, carbsGrams, fatGrams);
    }

    private void UpdateShowAds()
    {
        ShowAds = !_purchaseService.IsPremium;
    }

    public void OnAppearing()
    {
        UpdateShowAds();
        UpdateRemainingScans();
        CheckExtendedFoodAccess();
        _ = SafeLoadOnAppearingAsync();
    }

    private async Task SafeLoadOnAppearingAsync()
    {
        try
        {
            await LoadFavoritesAsync();
            await LoadRecentFoodsAsync();
            await LoadTodaySummaryAsync();
        }
        catch (Exception)
        {
            // Silently handle - data will be loaded on next appearance
        }
    }

    /// <summary>
    /// Lädt die letzten 5 geloggten Lebensmittel (distinct by Name) für Quick-Access Chips.
    /// </summary>
    private async Task LoadRecentFoodsAsync()
    {
        try
        {
            // Letzte 7 Tage durchsuchen für Recent Foods
            var allEntries = new List<FoodLogEntry>();
            for (var i = 0; i < 7; i++)
            {
                var entries = await _foodSearchService.GetFoodLogAsync(DateTime.Today.AddDays(-i));
                allEntries.AddRange(entries);
            }

            var recentNames = allEntries
                .OrderByDescending(e => e.Date)
                .Select(e => e.FoodName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .Take(5)
                .ToList();

            // FoodItems aus der Datenbank holen
            var recentItems = new ObservableCollection<FoodItem>();
            foreach (var name in recentNames)
            {
                var results = _foodSearchService.Search(name, 1);
                var match = results.FirstOrDefault(r => r.Food.Name == name)?.Food;
                if (match != null)
                    recentItems.Add(match);
            }

            RecentFoods = recentItems;
            HasRecentFoods = RecentFoods.Count > 0;
        }
        catch
        {
            // Silently handle
        }
    }

    private async Task LoadFavoritesAsync()
    {
        var favorites = await _foodSearchService.GetFavoritesAsync();
        Favorites = new ObservableCollection<FavoriteFoodEntry>(favorites);
        HasFavorites = Favorites.Count > 0;
    }

    [RelayCommand]
    private async Task ToggleFavorite()
    {
        if (SelectedFood == null) return;

        if (IsSelectedFoodFavorite)
        {
            var favorite = Favorites.FirstOrDefault(f => f.Food.Name == SelectedFood.Name);
            if (favorite != null)
            {
                await _foodSearchService.RemoveFavoriteAsync(favorite.Id);
            }
        }
        else
        {
            await _foodSearchService.SaveFavoriteAsync(SelectedFood);
        }

        await LoadFavoritesAsync();
        await UpdateSelectedFoodFavoriteStatus();
    }

    [RelayCommand]
    private async Task RemoveFavorite(FavoriteFoodEntry favorite)
    {
        await _foodSearchService.RemoveFavoriteAsync(favorite.Id);
        await LoadFavoritesAsync();
    }

    [RelayCommand]
    private void SelectFavorite(FavoriteFoodEntry favorite)
    {
        SelectedFood = favorite.Food;
    }

    [RelayCommand]
    private void SelectRecentFood(FoodItem food)
    {
        SelectedFood = food;
    }

    private async Task UpdateSelectedFoodFavoriteStatus()
    {
        if (SelectedFood == null)
        {
            IsSelectedFoodFavorite = false;
            return;
        }
        IsSelectedFoodFavorite = await _foodSearchService.IsFavoriteAsync(SelectedFood.Name);
    }

    #region Extended Food Database

    /// <summary>
    /// Prueft ob 24h erweiterter Zugang aktiv ist.
    /// </summary>
    private void CheckExtendedFoodAccess()
    {
        if (_purchaseService.IsPremium)
        {
            HasExtendedFoodAccess = true;
            return;
        }

        var expiryStr = _preferences.Get(PreferenceKeys.ExtendedFoodDbExpiry, "");
        if (!string.IsNullOrEmpty(expiryStr) &&
            DateTime.TryParse(expiryStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiry) &&
            DateTime.UtcNow < expiry)
        {
            HasExtendedFoodAccess = true;
        }
        else
        {
            HasExtendedFoodAccess = false;
        }
    }

    /// <summary>
    /// Zeigt "Mehr laden" Hint wenn lokale Suche wenig Ergebnisse hat.
    /// </summary>
    private void UpdateExtendedDbHint()
    {
        // Hint zeigen wenn: nicht Premium, kein Extended Access, und wenige Ergebnisse
        ShowExtendedDbHint = !_purchaseService.IsPremium
            && !HasExtendedFoodAccess
            && HasResults
            && _allSearchResults.Count <= 5;
    }

    /// <summary>
    /// User will erweiterte Datenbank nutzen. Zeigt Ad-Overlay.
    /// </summary>
    [RelayCommand]
    private void RequestExtendedDb()
    {
        ShowExtendedDbOverlay = true;
    }

    /// <summary>
    /// User bestaetigt: Video schauen fuer 24h erweiterten Zugang.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmExtendedDbAdAsync()
    {
        ShowExtendedDbOverlay = false;

        var success = await _rewardedAdService.ShowAdAsync("extended_food_db");
        if (success)
        {
            // 24h Zugang freischalten
            var expiry = DateTime.UtcNow.AddHours(24);
            _preferences.Set(PreferenceKeys.ExtendedFoodDbExpiry, expiry.ToString("O"));
            HasExtendedFoodAccess = true;
            ShowExtendedDbHint = false;

            // Suche mit erweiterter DB wiederholen (alle 114 Foods statt Top-Ergebnisse)
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                PerformExtendedSearch(SearchQuery);
            }
        }
    }

    /// <summary>
    /// Extended DB Overlay schliessen.
    /// </summary>
    [RelayCommand]
    private void CancelExtendedDb()
    {
        ShowExtendedDbOverlay = false;
    }

    /// <summary>
    /// Erweiterte Suche nach Ad-Freischaltung: Nutzt die gemeinsame PerformSearch-Methode.
    /// </summary>
    private void PerformExtendedSearch(string query) => PerformSearch(query);

    #endregion

    // IDisposable instead of Finalizer to avoid GC-Thread issues
    public void Dispose()
    {
        if (_disposed) return;

        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        _searchCancellationTokenSource = null;

        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _undoCancellation = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
