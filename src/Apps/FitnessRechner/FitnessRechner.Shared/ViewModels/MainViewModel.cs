using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using FitnessRechner.ViewModels.Calculators;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace FitnessRechner.ViewModels;

/// <summary>
/// Haupt-ViewModel: Konstruktor, Tab-Navigation, Calculator-Navigation, Back-Navigation, Dispose.
/// Dashboard-Daten, Labels, Gamification, Quick-Add → siehe MainViewModel.Dashboard.cs
/// </summary>
public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
    private readonly ITrackingService _trackingService;
    private readonly IFoodSearchService _foodSearchService;
    private readonly IPreferencesService _preferences;
    private readonly ILocalizationService _localization;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IStreakService _streakService;
    private readonly IAchievementService _achievementService;
    private readonly ILevelService _levelService;
    private readonly IChallengeService _challengeService;
    private readonly IHapticService _hapticService;
    private readonly IFitnessSoundService _soundService;

    // Factories fuer Calculator-VMs (statt Service-Locator via App.Services)
    private readonly Func<BmiViewModel> _bmiVmFactory;
    private readonly Func<CaloriesViewModel> _caloriesVmFactory;
    private readonly Func<WaterViewModel> _waterVmFactory;
    private readonly Func<IdealWeightViewModel> _idealWeightVmFactory;
    private readonly Func<BodyFatViewModel> _bodyFatVmFactory;
    private readonly Func<BarcodeScannerViewModel> _barcodeScannerVmFactory;

    [ObservableProperty]
    private bool _isAdBannerVisible;

    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;
    public event Action<string>? ExitHintRequested;

    public MainViewModel(
        IPurchaseService purchaseService,
        IAdService adService,
        IRewardedAdService rewardedAdService,
        ITrackingService trackingService,
        IFoodSearchService foodSearchService,
        IPreferencesService preferences,
        ILocalizationService localization,
        IStreakService streakService,
        IAchievementService achievementService,
        ILevelService levelService,
        IChallengeService challengeService,
        IHapticService hapticService,
        IFitnessSoundService soundService,
        SettingsViewModel settingsViewModel,
        ProgressViewModel progressViewModel,
        FoodSearchViewModel foodSearchViewModel,
        RecipeViewModel recipeViewModel,
        FastingViewModel fastingViewModel,
        ActivityViewModel activityViewModel,
        Func<BmiViewModel> bmiVmFactory,
        Func<CaloriesViewModel> caloriesVmFactory,
        Func<WaterViewModel> waterVmFactory,
        Func<IdealWeightViewModel> idealWeightVmFactory,
        Func<BodyFatViewModel> bodyFatVmFactory,
        Func<BarcodeScannerViewModel> barcodeScannerVmFactory)
    {
        _purchaseService = purchaseService;
        _adService = adService;
        _rewardedAdService = rewardedAdService;
        _trackingService = trackingService;
        _foodSearchService = foodSearchService;
        _preferences = preferences;
        _localization = localization;
        _streakService = streakService;
        _achievementService = achievementService;
        _levelService = levelService;
        _challengeService = challengeService;
        _hapticService = hapticService;
        _soundService = soundService;
        _bmiVmFactory = bmiVmFactory;
        _caloriesVmFactory = caloriesVmFactory;
        _waterVmFactory = waterVmFactory;
        _idealWeightVmFactory = idealWeightVmFactory;
        _bodyFatVmFactory = bodyFatVmFactory;
        _barcodeScannerVmFactory = barcodeScannerVmFactory;

        _rewardedAdService.AdUnavailable += OnAdUnavailable;

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += OnAdsStateChanged;

        // Banner beim Start anzeigen (fuer Desktop + Fallback falls AdMobHelper fehlschlaegt)
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();

        SettingsViewModel = settingsViewModel;
        ProgressViewModel = progressViewModel;
        FoodSearchViewModel = foodSearchViewModel;
        RecipeViewModel = recipeViewModel;
        FastingViewModel = fastingViewModel;
        ActivityViewModel = activityViewModel;

        // Child-VM Events verdrahten
        progressViewModel.FloatingTextRequested += OnProgressFloatingText;
        progressViewModel.CelebrationRequested += OnProgressCelebration;
        recipeViewModel.FloatingTextRequested += OnRecipeFloatingText;
        recipeViewModel.MessageRequested += OnRecipeMessage;
        recipeViewModel.NavigationRequested += OnRecipeNavigation;
        activityViewModel.FloatingTextRequested += OnActivityFloatingText;
        activityViewModel.MessageRequested += OnActivityMessage;
        activityViewModel.NavigationRequested += OnActivityNavigation;
        fastingViewModel.FloatingTextRequested += OnFastingFloatingText;
        fastingViewModel.CelebrationRequested += OnFastingCelebration;
        foodSearchViewModel.NavigationRequested += OnFoodSearchNavigation;

        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;
        settingsViewModel.LanguageChanged += OnLanguageChanged;

        // Streak bei jeder Logging-Aktivität aktualisieren
        _trackingService.EntryAdded += RecordStreakActivity;
        _foodSearchService.FoodLogAdded += RecordStreakActivity;

        // Gamification Events verdrahten
        _achievementService.AchievementUnlocked += OnAchievementUnlocked;
        _levelService.LevelUp += OnLevelUp;
        _challengeService.ChallengeCompleted += OnChallengeCompleted;

        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);
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
        OnPropertyChanged(nameof(StreakTitleText));
        OnPropertyChanged(nameof(GreetingText));
        OnPropertyChanged(nameof(QuickAddWeightLabel));
        OnPropertyChanged(nameof(MotivationalQuote));
        OnPropertyChanged(nameof(DailyProgressLabel));
        OnPropertyChanged(nameof(DailyChallengeLabel));
        OnPropertyChanged(nameof(ChallengeCompletedLabel));
        OnPropertyChanged(nameof(AchievementsTitleLabel));
        OnPropertyChanged(nameof(BadgesLabel));
        OnPropertyChanged(nameof(ShowAllLabel));
        OnPropertyChanged(nameof(WeeklyComparisonLabel));
        OnPropertyChanged(nameof(ThisWeekLabel));
        OnPropertyChanged(nameof(LastWeekLabel));
        OnPropertyChanged(nameof(EveningSummaryLabel));
        OnPropertyChanged(nameof(HeatmapHintText));
        OnPropertyChanged(nameof(LevelLabel));
        OnPropertyChanged(nameof(ChallengeTitleText));
        OnPropertyChanged(nameof(ChallengeDescText));
        UpdateStreakDisplay();

        // Child-VMs aktualisieren
        FastingViewModel.UpdateLocalizedTexts();
        ActivityViewModel.UpdateLocalizedTexts();
        RecipeViewModel.UpdateLocalizedTexts();

        if (CurrentCalculatorVm is CaloriesViewModel cal)
            cal.UpdateLocalizedTexts();
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

        if (value == 0)
            _ = OnAppearingAsync();
        else if (value == 1)
            _ = ProgressViewModel.OnAppearingAsync();
        else if (value == 2)
            FoodSearchViewModel.OnAppearing();
    }

    [RelayCommand]
    private void SelectHomeTab() { CurrentPage = null; SelectedTab = 0; }

    [RelayCommand]
    private void SelectProgressTab() { CurrentPage = null; SelectedTab = 1; }

    [RelayCommand]
    private void SelectFoodTab() { CurrentPage = null; SelectedTab = 2; }

    [RelayCommand]
    private void SelectSettingsTab() { CurrentPage = null; SelectedTab = 3; }

    public string NavHomeText => _localization.GetString("TodayDashboard");
    public string NavProgressText => _localization.GetString("Progress");
    public string NavFoodText => _localization.GetString("FoodSearch");
    public string NavSettingsText => _localization.GetString("SettingsTitle");

    #endregion

    #region Child ViewModels

    public SettingsViewModel SettingsViewModel { get; }
    public ProgressViewModel ProgressViewModel { get; }
    public FoodSearchViewModel FoodSearchViewModel { get; }
    public RecipeViewModel RecipeViewModel { get; }
    public FastingViewModel FastingViewModel { get; }
    public ActivityViewModel ActivityViewModel { get; }

    #endregion

    #region Premium Status

    [ObservableProperty]
    private bool _isPremium;

    #endregion

    #region Calculator Page Navigation

    [ObservableProperty]
    private string? _currentPage;

    [ObservableProperty]
    private ObservableObject? _currentCalculatorVm;

    public bool IsCalculatorOpen => CurrentPage != null;

    partial void OnCurrentPageChanged(string? value)
    {
        OnPropertyChanged(nameof(IsCalculatorOpen));
        CleanupCurrentCalculatorVm();
        CurrentCalculatorVm = value != null ? CreateCalculatorVm(value) : null;
    }

    private void CleanupCurrentCalculatorVm()
    {
        switch (CurrentCalculatorVm)
        {
            case BmiViewModel bmi:
                bmi.NavigationRequested -= OnCalculatorGoBack;
                bmi.MessageRequested -= OnCalculatorMessage;
                break;
            case CaloriesViewModel cal:
                cal.NavigationRequested -= OnCalculatorGoBack;
                cal.MessageRequested -= OnCalculatorMessage;
                break;
            case WaterViewModel water:
                water.NavigationRequested -= OnCalculatorGoBack;
                water.MessageRequested -= OnCalculatorMessage;
                break;
            case IdealWeightViewModel iw:
                iw.NavigationRequested -= OnCalculatorGoBack;
                iw.MessageRequested -= OnCalculatorMessage;
                break;
            case BodyFatViewModel bf:
                bf.NavigationRequested -= OnCalculatorGoBack;
                bf.MessageRequested -= OnCalculatorMessage;
                break;
            case BarcodeScannerViewModel scanner:
                scanner.NavigationRequested -= OnCalculatorGoBack;
                scanner.FoodSelected -= OnFoodSelectedFromScanner;
                scanner.Dispose();
                break;
        }
    }

    private ObservableObject? CreateCalculatorVm(string page)
    {
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
            "BmiPage" => _bmiVmFactory(),
            "CaloriesPage" => _caloriesVmFactory(),
            "WaterPage" => _waterVmFactory(),
            "IdealWeightPage" => _idealWeightVmFactory(),
            "BodyFatPage" => _bodyFatVmFactory(),
            "BarcodeScannerPage" => _barcodeScannerVmFactory(),
            _ => null
        };

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
                if (!string.IsNullOrEmpty(barcodeParam))
                    _ = scanner.OnBarcodeDetected(barcodeParam);
                break;
        }

        return vm;
    }

    private void OnCalculatorGoBack(string route)
    {
        if (route == "..")
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentPage = null;
                _ = LoadDashboardDataAsync();
            });
    }

    private void OnCalculatorMessage(string title, string message) =>
        MessageRequested?.Invoke(title, message);

    private void OnFoodSearchNavigation(string route)
    {
        if (route.StartsWith("BarcodeScannerPage"))
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentPage = route);
        else if (route == "..")
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentPage = null);
    }

    private void OnFoodSelectedFromScanner(FoodItem food)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentPage = null;
            SelectedTab = 2;
            FoodSearchViewModel.ApplyParameters(new Dictionary<string, object>
            {
                { "SelectedFood", food }
            });
        });
    }

    #endregion

    #region Back-Navigation (Double-Back-to-Exit)

    private readonly BackPressHelper _backPressHelper = new();

    public bool HandleBackPressed()
    {
        if (ShowAchievements) { ShowAchievements = false; return true; }
        if (ShowWeightQuickAdd) { ShowWeightQuickAdd = false; return true; }
        if (ShowWaterQuickAdd) { ShowWaterQuickAdd = false; return true; }

        if (CurrentPage != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentPage = null);
            return true;
        }

        // ProgressVM Overlays
        if (SelectedTab == 1)
        {
            var vm = ProgressViewModel;
            if (vm.ShowAnalysisOverlay) { vm.ShowAnalysisOverlay = false; return true; }
            if (vm.ShowAnalysisAdOverlay) { vm.ShowAnalysisAdOverlay = false; return true; }
            if (vm.ShowExportAdOverlay) { vm.ShowExportAdOverlay = false; return true; }
            if (vm.ShowFoodSearch) { vm.ShowFoodSearch = false; return true; }
            if (vm.ShowAddForm) { vm.ShowAddForm = false; return true; }
            if (vm.ShowAddFoodPanel) { vm.ShowAddFoodPanel = false; return true; }
        }

        // ActivityVM/RecipeVM Overlays
        if (ActivityViewModel.ShowAddForm) { ActivityViewModel.ShowAddForm = false; return true; }
        if (RecipeViewModel.IsEditing) { RecipeViewModel.GoBackCommand.Execute(null); return true; }
        if (RecipeViewModel.ShowLogPanel) { RecipeViewModel.CancelUseRecipeCommand.Execute(null); return true; }

        if (SelectedTab != 0)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedTab = 0);
            return true;
        }

        var msg = _localization.GetString("PressBackAgainToExit") ?? "Erneut drücken zum Beenden";
        return _backPressHelper.HandleDoubleBack(msg);
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void OpenSettings() => SelectedTab = 3;

    [RelayCommand]
    private void OpenBmi() { CurrentPage = "BmiPage"; TrackCalculatorUsed(0); }

    [RelayCommand]
    private void OpenCalories() { CurrentPage = "CaloriesPage"; TrackCalculatorUsed(1); }

    [RelayCommand]
    private void OpenWater() { CurrentPage = "WaterPage"; TrackCalculatorUsed(2); }

    [RelayCommand]
    private void OpenIdealWeight() { CurrentPage = "IdealWeightPage"; TrackCalculatorUsed(3); }

    [RelayCommand]
    private void OpenBodyFat() { CurrentPage = "BodyFatPage"; TrackCalculatorUsed(4); }

    private void TrackCalculatorUsed(int calcIndex)
    {
        var mask = _preferences.Get(PreferenceKeys.CalculatorsUsedMask, 0);
        var bit = 1 << calcIndex;
        if ((mask & bit) == 0)
        {
            mask |= bit;
            _preferences.Set(PreferenceKeys.CalculatorsUsedMask, mask);
        }
        _levelService.AddXp(2);
    }

    [RelayCommand]
    private void OpenAchievements() => ShowAchievements = true;

    [RelayCommand]
    private void CloseAchievements() => ShowAchievements = false;

    [RelayCommand]
    private void OpenProgress() { CurrentPage = null; SelectedTab = 1; }

    #endregion

    #region Event-Handler (Child-VM Weiterleitung)

    private void OnAdUnavailable() =>
        MessageRequested?.Invoke(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage);

    private void OnAdsStateChanged(object? sender, EventArgs e) =>
        IsAdBannerVisible = _adService.BannerVisible;

    private void OnProgressFloatingText(string text, string category) =>
        FloatingTextRequested?.Invoke(text, category);

    private void OnProgressCelebration() =>
        CelebrationRequested?.Invoke();

    private void OnRecipeFloatingText(string text, string category) =>
        FloatingTextRequested?.Invoke(text, category);

    private void OnRecipeMessage(string title, string message) =>
        MessageRequested?.Invoke(title, message);

    private void OnRecipeNavigation(string route)
    {
        if (route == "..") CurrentPage = null;
    }

    private void OnActivityFloatingText(string text, string category) =>
        FloatingTextRequested?.Invoke(text, category);

    private void OnActivityMessage(string title, string message) =>
        MessageRequested?.Invoke(title, message);

    private void OnActivityNavigation(string route)
    {
        if (route == "..") CurrentPage = null;
    }

    private void OnFastingFloatingText(string text, string category) =>
        FloatingTextRequested?.Invoke(text, category);

    private void OnFastingCelebration() =>
        CelebrationRequested?.Invoke();

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => IsPremium = _purchaseService.IsPremium);
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed) return;

        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        SettingsViewModel.LanguageChanged -= OnLanguageChanged;
        _trackingService.EntryAdded -= RecordStreakActivity;
        _foodSearchService.FoodLogAdded -= RecordStreakActivity;
        _rewardedAdService.AdUnavailable -= OnAdUnavailable;
        _adService.AdsStateChanged -= OnAdsStateChanged;
        _achievementService.AchievementUnlocked -= OnAchievementUnlocked;
        _levelService.LevelUp -= OnLevelUp;
        _challengeService.ChallengeCompleted -= OnChallengeCompleted;
        ProgressViewModel.FloatingTextRequested -= OnProgressFloatingText;
        ProgressViewModel.CelebrationRequested -= OnProgressCelebration;
        FoodSearchViewModel.NavigationRequested -= OnFoodSearchNavigation;
        RecipeViewModel.FloatingTextRequested -= OnRecipeFloatingText;
        RecipeViewModel.MessageRequested -= OnRecipeMessage;
        RecipeViewModel.NavigationRequested -= OnRecipeNavigation;
        ActivityViewModel.FloatingTextRequested -= OnActivityFloatingText;
        ActivityViewModel.MessageRequested -= OnActivityMessage;
        ActivityViewModel.NavigationRequested -= OnActivityNavigation;
        FastingViewModel.FloatingTextRequested -= OnFastingFloatingText;
        FastingViewModel.CelebrationRequested -= OnFastingCelebration;

        ProgressViewModel.Dispose();
        FoodSearchViewModel.Dispose();
        SettingsViewModel.Dispose();
        FastingViewModel.Dispose();
        ActivityViewModel.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
