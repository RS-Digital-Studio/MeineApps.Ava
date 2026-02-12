using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using FitnessRechner.ViewModels.Calculators;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessRechner.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
    private readonly ITrackingService _trackingService;
    private readonly IFoodSearchService _foodSearchService;
    private readonly IPreferencesService _preferences;
    private readonly ILocalizationService _localization;
    private readonly IRewardedAdService _rewardedAdService;

    private const string CALORIE_GOAL_KEY = "daily_calorie_goal";
    private const string WATER_GOAL_KEY = "daily_water_goal";

    /// <summary>
    /// Raised when the VM wants to show a message (title, message).
    /// </summary>
    [ObservableProperty]
    private bool _isAdBannerVisible;

    public event Action<string, string>? MessageRequested;

    /// <summary>
    /// Floating Text anzeigen (text, category).
    /// </summary>
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>
    /// Confetti-Celebration ausloesen.
    /// </summary>
    public event Action? CelebrationRequested;

    public MainViewModel(
        IPurchaseService purchaseService,
        IAdService adService,
        IRewardedAdService rewardedAdService,
        ITrackingService trackingService,
        IFoodSearchService foodSearchService,
        IPreferencesService preferences,
        ILocalizationService localization,
        IThemeService themeService,
        SettingsViewModel settingsViewModel,
        ProgressViewModel progressViewModel,
        FoodSearchViewModel foodSearchViewModel)
    {
        _purchaseService = purchaseService;
        _adService = adService;
        _rewardedAdService = rewardedAdService;
        _trackingService = trackingService;
        _foodSearchService = foodSearchService;
        _preferences = preferences;
        _localization = localization;

        _rewardedAdService.AdUnavailable += () => MessageRequested?.Invoke(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage);

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += (_, _) => IsAdBannerVisible = _adService.BannerVisible;

        // Banner beim Start anzeigen (fuer Desktop + Fallback falls AdMobHelper fehlschlaegt)
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();

        SettingsViewModel = settingsViewModel;
        ProgressViewModel = progressViewModel;
        FoodSearchViewModel = foodSearchViewModel;

        // Game Juice Events vom ProgressViewModel weiterleiten
        progressViewModel.FloatingTextRequested += (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        progressViewModel.CelebrationRequested += () => CelebrationRequested?.Invoke();

        // FoodSearchViewModel Navigation verdrahten (Barcode-Scanner)
        foodSearchViewModel.NavigationRequested += OnFoodSearchNavigation;

        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;
        settingsViewModel.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(NavHomeText));
        OnPropertyChanged(nameof(NavProgressText));
        OnPropertyChanged(nameof(NavFoodText));
        OnPropertyChanged(nameof(NavSettingsText));
        OnPropertyChanged(nameof(AppDescription));
        OnPropertyChanged(nameof(CalcBmiLabel));
        OnPropertyChanged(nameof(CalcCaloriesLabel));
        OnPropertyChanged(nameof(CalcWaterLabel));
        OnPropertyChanged(nameof(CalcIdealWeightLabel));
        OnPropertyChanged(nameof(CalcBodyFatLabel));
        OnPropertyChanged(nameof(CalculatorsLabel));
        OnPropertyChanged(nameof(MyProgressLabel));
        OnPropertyChanged(nameof(RemoveAdsText));
        OnPropertyChanged(nameof(PremiumPriceText));
        OnPropertyChanged(nameof(SectionCalculatorsText));
    }

    #region Tab Navigation

    [ObservableProperty]
    private int _selectedTab;

    public bool IsHomeActive => SelectedTab == 0;
    public bool IsProgressActive => SelectedTab == 1;
    public bool IsFoodActive => SelectedTab == 2;
    public bool IsSettingsActive => SelectedTab == 3;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(IsProgressActive));
        OnPropertyChanged(nameof(IsFoodActive));
        OnPropertyChanged(nameof(IsSettingsActive));

        // Load data for the selected tab
        if (value == 1)
            _ = ProgressViewModel.OnAppearingAsync();
    }

    [RelayCommand]
    private void SelectHomeTab() { CurrentPage = null; SelectedTab = 0; }

    [RelayCommand]
    private void SelectProgressTab() { CurrentPage = null; SelectedTab = 1; }

    [RelayCommand]
    private void SelectFoodTab() { CurrentPage = null; SelectedTab = 2; }

    [RelayCommand]
    private void SelectSettingsTab() { CurrentPage = null; SelectedTab = 3; }

    // Tab text properties from localization
    public string NavHomeText => _localization.GetString("TodayDashboard");
    public string NavProgressText => _localization.GetString("Progress");
    public string NavFoodText => _localization.GetString("FoodSearch");
    public string NavSettingsText => _localization.GetString("SettingsTitle");

    #endregion

    #region Child ViewModels

    public SettingsViewModel SettingsViewModel { get; }
    public ProgressViewModel ProgressViewModel { get; }
    public FoodSearchViewModel FoodSearchViewModel { get; }

    #endregion

    #region Premium Status

    [ObservableProperty]
    private bool _isPremium;

    #endregion

    #region Dashboard Properties

    [ObservableProperty]
    private string _todayWeightDisplay = "-";

    [ObservableProperty]
    private string _todayBmiDisplay = "-";

    [ObservableProperty]
    private string _todayWaterDisplay = "-";

    [ObservableProperty]
    private string _todayCaloriesDisplay = "-";

    [ObservableProperty]
    private bool _hasDashboardData;

    #endregion

    #region Localized Labels

    public string AppDescription => _localization.GetString("AppDescription");
    public string CalcBmiLabel => _localization.GetString("CalcBmi");
    public string CalcCaloriesLabel => _localization.GetString("CalcCalories");
    public string CalcWaterLabel => _localization.GetString("CalcWater");
    public string CalcIdealWeightLabel => _localization.GetString("CalcIdealWeight");
    public string CalcBodyFatLabel => _localization.GetString("CalcBodyFat");
    public string CalculatorsLabel => _localization.GetString("Calculators");
    public string MyProgressLabel => _localization.GetString("MyProgress");

    // Design-Redesign Properties
    public string RemoveAdsText => _localization.GetString("RemoveAds") ?? "Go Ad-Free";
    public string PremiumPriceText => _localization.GetString("PremiumPrice") ?? "From 3.99 €";
    public string SectionCalculatorsText => _localization.GetString("SectionCalculators") ?? "Calculators";

    #endregion

    public async Task OnAppearingAsync()
    {
        IsPremium = _purchaseService.IsPremium;
        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            // Zurücksetzen damit veraltete Daten nicht fälschlich "true" halten
            HasDashboardData = false;

            // Weight
            var weightEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Weight);
            if (weightEntry != null && weightEntry.Date.Date >= DateTime.Today.AddDays(-7))
            {
                TodayWeightDisplay = $"{weightEntry.Value:F1}";
                HasDashboardData = true;
            }
            else
            {
                TodayWeightDisplay = "-";
            }

            // BMI
            var bmiEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Bmi);
            if (bmiEntry != null && bmiEntry.Date.Date >= DateTime.Today.AddDays(-7))
            {
                TodayBmiDisplay = $"{bmiEntry.Value:F1}";
                HasDashboardData = true;
            }
            else
            {
                TodayBmiDisplay = "-";
            }

            // Water (today only)
            var waterEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Water);
            if (waterEntry != null && waterEntry.Date.Date == DateTime.Today)
            {
                var waterGoal = _preferences.Get(WATER_GOAL_KEY, 2500.0);
                var progress = waterGoal > 0 ? (waterEntry.Value / waterGoal) * 100 : 0;
                TodayWaterDisplay = $"{progress:F0}%";
                HasDashboardData = true;
            }
            else
            {
                TodayWaterDisplay = "0%";
            }

            // Calories (today only)
            var summary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
            var calorieGoal = _preferences.Get(CALORIE_GOAL_KEY, 2000.0);
            if (summary.TotalCalories > 0 || calorieGoal > 0)
            {
                TodayCaloriesDisplay = $"{summary.TotalCalories:F0}";
                HasDashboardData = true;
            }
            else
            {
                TodayCaloriesDisplay = "-";
            }
        }
        catch (Exception)
        {
            // Silently handle - dashboard will show default values
        }
    }

    #region Calculator Page Navigation

    [ObservableProperty]
    private string? _currentPage;

    [ObservableProperty]
    private ObservableObject? _currentCalculatorVm;

    public bool IsCalculatorOpen => CurrentPage != null;

    partial void OnCurrentPageChanged(string? value)
    {
        OnPropertyChanged(nameof(IsCalculatorOpen));

        if (value != null)
        {
            CurrentCalculatorVm = CreateCalculatorVm(value);
        }
        else
        {
            CurrentCalculatorVm = null;
        }
    }

    private ObservableObject? CreateCalculatorVm(string page)
    {
        // Route-Parameter parsen (z.B. "BarcodeScannerPage?barcode=1234567890")
        string? barcodeParam = null;
        var basePage = page;
        var queryIndex = page.IndexOf('?');
        if (queryIndex >= 0)
        {
            basePage = page[..queryIndex];
            var query = page[(queryIndex + 1)..];
            foreach (var param in query.Split('&'))
            {
                var parts = param.Split('=', 2);
                if (parts.Length == 2 && parts[0] == "barcode")
                    barcodeParam = Uri.UnescapeDataString(parts[1]);
            }
        }

        ObservableObject? vm = basePage switch
        {
            "BmiPage" => App.Services.GetRequiredService<BmiViewModel>(),
            "CaloriesPage" => App.Services.GetRequiredService<CaloriesViewModel>(),
            "WaterPage" => App.Services.GetRequiredService<WaterViewModel>(),
            "IdealWeightPage" => App.Services.GetRequiredService<IdealWeightViewModel>(),
            "BodyFatPage" => App.Services.GetRequiredService<BodyFatViewModel>(),
            "BarcodeScannerPage" => App.Services.GetRequiredService<BarcodeScannerViewModel>(),
            _ => null
        };

        // Wire navigation and message events
        switch (vm)
        {
            case BmiViewModel bmi:
                bmi.NavigationRequested += OnCalculatorGoBack;
                bmi.MessageRequested += OnCalculatorMessage;
                break;
            case CaloriesViewModel cal:
                cal.NavigationRequested += OnCalculatorGoBack;
                cal.MessageRequested += OnCalculatorMessage;
                break;
            case WaterViewModel water:
                water.NavigationRequested += OnCalculatorGoBack;
                water.MessageRequested += OnCalculatorMessage;
                break;
            case IdealWeightViewModel iw:
                iw.NavigationRequested += OnCalculatorGoBack;
                iw.MessageRequested += OnCalculatorMessage;
                break;
            case BodyFatViewModel bf:
                bf.NavigationRequested += OnCalculatorGoBack;
                bf.MessageRequested += OnCalculatorMessage;
                break;
            case BarcodeScannerViewModel scanner:
                scanner.NavigationRequested += OnCalculatorGoBack;
                scanner.FoodSelected += OnFoodSelectedFromScanner;
                // Barcode-Parameter vorhanden → direkt API-Lookup starten
                if (!string.IsNullOrEmpty(barcodeParam))
                    _ = scanner.OnBarcodeDetected(barcodeParam);
                break;
        }

        return vm;
    }

    private void OnCalculatorGoBack(string route)
    {
        if (route == "..")
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentPage = null;
                // Refresh dashboard after calculator use
                _ = LoadDashboardDataAsync();
            });
        }
    }

    private void OnCalculatorMessage(string title, string message)
    {
        MessageRequested?.Invoke(title, message);
    }

    private void OnFoodSearchNavigation(string route)
    {
        // Barcode-Scanner und andere FoodSearch-Navigationen behandeln
        if (route.StartsWith("BarcodeScannerPage"))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentPage = route;
            });
        }
        else if (route == "..")
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentPage = null;
            });
        }
    }

    private void OnFoodSelectedFromScanner(FoodItem food)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Zurueck zum Food-Tab und gescanntes Lebensmittel anwenden
            CurrentPage = null;
            SelectedTab = 2;
            FoodSearchViewModel.ApplyParameters(new Dictionary<string, object>
            {
                { "SelectedFood", food }
            });
        });
    }

    #endregion

    [RelayCommand]
    private void OpenSettings() => SelectedTab = 3;

    [RelayCommand]
    private void OpenBmi() => CurrentPage = "BmiPage";

    [RelayCommand]
    private void OpenCalories() => CurrentPage = "CaloriesPage";

    [RelayCommand]
    private void OpenWater() => CurrentPage = "WaterPage";

    [RelayCommand]
    private void OpenIdealWeight() => CurrentPage = "IdealWeightPage";

    [RelayCommand]
    private void OpenBodyFat() => CurrentPage = "BodyFatPage";

    [RelayCommand]
    private void OpenProgress() { CurrentPage = null; SelectedTab = 1; }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsPremium = _purchaseService.IsPremium;
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;

        ProgressViewModel.Dispose();
        FoodSearchViewModel.Dispose();
        SettingsViewModel.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
