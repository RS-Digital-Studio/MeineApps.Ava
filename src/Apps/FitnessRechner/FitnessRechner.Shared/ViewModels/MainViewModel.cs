using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitnessRechner.Models;
using FitnessRechner.Resources.Strings;
using FitnessRechner.Services;
using FitnessRechner.ViewModels.Calculators;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

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

    /// <summary>Wird ausgelöst um einen Exit-Hinweis anzuzeigen (z.B. Toast "Nochmal drücken zum Beenden").</summary>
    public event Action<string>? ExitHintRequested;

    public MainViewModel(
        IPurchaseService purchaseService,
        IAdService adService,
        IRewardedAdService rewardedAdService,
        ITrackingService trackingService,
        IFoodSearchService foodSearchService,
        IPreferencesService preferences,
        ILocalizationService localization,
        IThemeService themeService,
        IStreakService streakService,
        IAchievementService achievementService,
        ILevelService levelService,
        IChallengeService challengeService,
        IHapticService hapticService,
        IFitnessSoundService soundService,
        SettingsViewModel settingsViewModel,
        ProgressViewModel progressViewModel,
        FoodSearchViewModel foodSearchViewModel,
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

        // Game Juice Events vom ProgressViewModel weiterleiten
        progressViewModel.FloatingTextRequested += OnProgressFloatingText;
        progressViewModel.CelebrationRequested += OnProgressCelebration;

        // FoodSearchViewModel Navigation verdrahten (Barcode-Scanner)
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

        // Aktiven Calculator aktualisieren falls nötig
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

        // Daten für ausgewählten Tab laden
        if (value == 0)
            _ = OnAppearingAsync(); // Dashboard-Daten neu laden (Wasser, Kalorien etc. könnten sich geändert haben)
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

    // Fortschritt (0-100)
    [ObservableProperty]
    private double _calorieProgress;

    [ObservableProperty]
    private double _waterProgress;

    // ProgressBar nur anzeigen wenn Wert > 0 (vermeidet sichtbare Track-Striche bei 0)
    public bool HasWaterProgress => WaterProgress > 0;
    public bool HasCalorieProgress => CalorieProgress > 0;

    // CircularProgress Fraction (0.0-1.0) für Dashboard-Ringe
    public double WaterProgressFraction => Math.Min(WaterProgress / 100.0, 1.0);
    public double CalorieProgressFraction => Math.Min(CalorieProgress / 100.0, 1.0);

    // Tages-Score: Kombinierter Fortschritt (Wasser + Kalorien + Gewicht geloggt)
    [ObservableProperty]
    private bool _hasLoggedWeightToday;

    public double DailyScoreFraction
    {
        get
        {
            var waterPart = Math.Min(WaterProgress, 100);
            var caloriePart = Math.Min(CalorieProgress, 100);
            var weightPart = HasLoggedWeightToday ? 100.0 : 0.0;
            return (waterPart + caloriePart + weightPart) / 300.0;
        }
    }

    public string DailyScoreDisplay => $"{DailyScoreFraction * 100:F0}%";

    partial void OnWaterProgressChanged(double value)
    {
        OnPropertyChanged(nameof(HasWaterProgress));
        OnPropertyChanged(nameof(WaterProgressFraction));
        OnPropertyChanged(nameof(DailyScoreFraction));
        OnPropertyChanged(nameof(DailyScoreDisplay));
    }

    partial void OnCalorieProgressChanged(double value)
    {
        OnPropertyChanged(nameof(HasCalorieProgress));
        OnPropertyChanged(nameof(CalorieProgressFraction));
        OnPropertyChanged(nameof(DailyScoreFraction));
        OnPropertyChanged(nameof(DailyScoreDisplay));
    }

    partial void OnHasLoggedWeightTodayChanged(bool value)
    {
        OnPropertyChanged(nameof(DailyScoreFraction));
        OnPropertyChanged(nameof(DailyScoreDisplay));
    }

    // Quick-Add Properties
    [ObservableProperty]
    private bool _showWeightQuickAdd;

    [ObservableProperty]
    private double _quickAddWeight = 70.0;

    private bool _wasWaterGoalReachedOnDashboard;

    // Streak
    [ObservableProperty]
    private int _currentStreak;

    [ObservableProperty]
    private string _streakDisplay = "";

    [ObservableProperty]
    private string _streakBestDisplay = "";

    [ObservableProperty]
    private bool _hasStreak;

    // Heatmap-Kalender Daten
    [ObservableProperty]
    private Dictionary<DateTime, int> _heatmapData = new();

    [ObservableProperty]
    private bool _hasHeatmapData;

    // Hinweistext wenn wenige Aktivitätstage (< 7)
    [ObservableProperty]
    private bool _showHeatmapHint;

    public string HeatmapHintText => _localization.GetString("HeatmapHint") ?? "Keep tracking to fill your activity calendar!";

    // Empty-State: Wird angezeigt wenn keine Tracking-Daten vorhanden sind
    [ObservableProperty]
    private bool _showEmptyState = true;

    // Abend-Zusammenfassung (nach 20 Uhr)
    [ObservableProperty]
    private bool _showEveningSummary;

    [ObservableProperty]
    private string _eveningSummaryText = "";

    [ObservableProperty]
    private string _eveningSummaryRating = "";

    [ObservableProperty]
    private string _eveningSummaryRatingColor = "#EAB308";

    #endregion

    #region Gamification Properties

    // Level/XP
    [ObservableProperty]
    private int _currentGamificationLevel;

    [ObservableProperty]
    private double _levelProgress;

    [ObservableProperty]
    private string _xpDisplay = "";

    // Challenge
    [ObservableProperty]
    private bool _isChallengeCompleted;

    // Achievements Overlay
    [ObservableProperty]
    private bool _showAchievements;

    // Weekly Comparison
    [ObservableProperty]
    private string _weeklyCaloriesChange = "";

    [ObservableProperty]
    private string _weeklyWaterChange = "";

    [ObservableProperty]
    private string _weeklyWeightChange = "";

    [ObservableProperty]
    private string _weeklyLogDays = "";

    [ObservableProperty]
    private string _weeklyCaloriesColor = "#EAB308";

    [ObservableProperty]
    private string _weeklyWaterColor = "#EAB308";

    [ObservableProperty]
    private string _weeklyWeightColor = "#EAB308";

    [ObservableProperty]
    private bool _hasWeeklyComparison;

    // Computed Gamification Properties
    public DailyChallenge TodayChallenge => _challengeService.TodayChallenge;

    public string ChallengeTitleText =>
        _localization.GetString(TodayChallenge.TitleKey) ?? TodayChallenge.TitleKey;

    public string ChallengeDescText =>
        _localization.GetString(TodayChallenge.DescriptionKey) ?? TodayChallenge.DescriptionKey;

    public double ChallengeProgressValue => TodayChallenge.Progress;

    public string ChallengeProgressText =>
        $"{TodayChallenge.CurrentValue}/{TodayChallenge.TargetValue}";

    public string ChallengeXpText => $"+{TodayChallenge.XpReward} XP";

    public IReadOnlyList<FitnessAchievement> RecentAchievements =>
        _achievementService.RecentUnlocked;

    public IReadOnlyList<FitnessAchievement> AllAchievements =>
        _achievementService.Achievements;

    public string AchievementCountDisplay =>
        $"{_achievementService.UnlockedCount}/{_achievementService.Achievements.Count}";

    public bool HasRecentAchievements =>
        _achievementService.RecentUnlocked.Count > 0;

    public string LevelLabel =>
        string.Format(_localization.GetString("Level") ?? "Level {0}", CurrentGamificationLevel);

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
    public string StreakTitleText => _localization.GetString("StreakTitle") ?? "Logging Streak";
    public string QuickAddWeightLabel => _localization.GetString("QuickAddWeight") ?? "Enter weight";

    // Motivations-Zitat (wechselt täglich)
    public string MotivationalQuote
    {
        get
        {
            var index = DateTime.Today.DayOfYear % 10;
            var key = $"MotivQuote{index + 1}";
            return _localization.GetString(key) ?? "Every step counts!";
        }
    }

    public string DailyProgressLabel => _localization.GetString("DailyProgress") ?? "Daily Progress";
    public string DailyChallengeLabel => _localization.GetString("DailyChallenge") ?? "Daily Challenge";
    public string ChallengeCompletedLabel => _localization.GetString("ChallengeCompleted") ?? "Completed!";
    public string AchievementsTitleLabel => _localization.GetString("AchievementsTitle") ?? "Achievements";
    public string BadgesLabel => _localization.GetString("Badges") ?? "Badges";
    public string ShowAllLabel => _localization.GetString("ShowAll") ?? "Show All";
    public string WeeklyComparisonLabel => _localization.GetString("WeeklyComparison") ?? "Weekly Comparison";
    public string ThisWeekLabel => _localization.GetString("ThisWeek") ?? "This Week";
    public string LastWeekLabel => _localization.GetString("LastWeek") ?? "Last Week";
    public string EveningSummaryLabel => _localization.GetString("EveningSummary") ?? "Today's Summary";
    public string EmptyStateTitle => _localization.GetString("EmptyStateTitle") ?? "Start your fitness journey!";
    public string EmptyStateSubtitle => _localization.GetString("EmptyStateSubtitle") ?? "Track your weight, water and calories to unlock all dashboard features.";
    public string EmptyStateHint => _localization.GetString("EmptyStateHint") ?? "Use the buttons above to get started";

    // Tageszeit-Begrüßung
    public string GreetingText
    {
        get
        {
            var hour = DateTime.Now.Hour;
            var key = hour switch
            {
                < 12 => "GreetingMorning",
                < 18 => "GreetingAfternoon",
                _ => "GreetingEvening"
            };
            return _localization.GetString(key) ?? "Hello!";
        }
    }

    #endregion

    public async Task OnAppearingAsync()
    {
        IsPremium = _purchaseService.IsPremium;
        UpdateStreakDisplay();

        try
        {
            await LoadDashboardDataAsync();
            await LoadHeatmapDataAsync();
            await CheckGamificationProgressAsync();
            await LoadWeeklyComparisonAsync();
            await CheckEveningSummaryAsync();
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                $"Dashboard: {ex.Message}");
        }
        finally
        {
            // Empty-State IMMER berechnen (auch bei Exception), sonst leerer Bereich
            ShowEmptyState = !HasDashboardData && !HasHeatmapData && !HasWeeklyComparison && !HasRecentAchievements;
        }
    }

    /// <summary>
    /// Streak-Aktivität registrieren (wird von Child-VMs aufgerufen).
    /// </summary>
    public void RecordStreakActivity()
    {
        var wasLoggedToday = _streakService.IsLoggedToday;
        var isMilestone = _streakService.RecordActivity();
        UpdateStreakDisplay();

        if (isMilestone)
        {
            // Meilenstein → Confetti + spezielle Nachricht
            var milestone = _streakService.CurrentStreak;
            var text = string.Format(_localization.GetString("StreakMilestone") ?? "{0} day streak!", milestone);
            FloatingTextRequested?.Invoke(text, "streak");
            CelebrationRequested?.Invoke();
            _hapticService.HeavyClick();
            _soundService.PlaySuccess();
        }
        else if (!wasLoggedToday && _streakService.IsLoggedToday)
        {
            // Erster Log des Tages (kein Meilenstein) → einfaches Feedback
            var streak = _streakService.CurrentStreak;
            var text = string.Format(_localization.GetString("StreakIncreased") ?? "+1! {0} day streak", streak);
            FloatingTextRequested?.Invoke(text, "streak");
        }

        // XP für Logging-Aktivität (+5 für Mahlzeiten/Tracking)
        _levelService.AddXp(5);

        // Meal-Counter inkrementieren (für Achievements)
        var meals = _preferences.Get(PreferenceKeys.TotalMealsLogged, 0);
        _preferences.Set(PreferenceKeys.TotalMealsLogged, meals + 1);

        // Gamification aktuell halten
        _ = CheckGamificationProgressAsync();
    }

    private void UpdateStreakDisplay()
    {
        CurrentStreak = _streakService.CurrentStreak;
        HasStreak = CurrentStreak > 0;

        if (CurrentStreak == 0)
        {
            StreakDisplay = _localization.GetString("StreakStart") ?? "Start your streak!";
        }
        else if (CurrentStreak == 1)
        {
            StreakDisplay = _localization.GetString("StreakDay") ?? "1 day";
        }
        else
        {
            StreakDisplay = string.Format(_localization.GetString("StreakDays") ?? "{0} days", CurrentStreak);
        }

        var best = _streakService.BestStreak;
        StreakBestDisplay = string.Format(_localization.GetString("StreakBest") ?? "Best: {0}", best);
    }

    private async Task LoadDashboardDataAsync()
    {
        try
        {
            // Zurücksetzen damit veraltete Daten nicht fälschlich "true" halten
            HasDashboardData = false;

            // Weight
            var weightEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Weight);
            HasLoggedWeightToday = weightEntry != null && weightEntry.Date.Date == DateTime.Today;
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
            var waterGoal = _preferences.Get(PreferenceKeys.WaterGoal, 2500.0);
            if (waterEntry != null && waterEntry.Date.Date == DateTime.Today)
            {
                var progress = waterGoal > 0 ? (waterEntry.Value / waterGoal) * 100 : 0;
                TodayWaterDisplay = $"{progress:F0}%";
                WaterProgress = Math.Min(progress, 100);
                HasDashboardData = true;
            }
            else
            {
                TodayWaterDisplay = "0%";
                WaterProgress = 0;
            }

            // Calories (today only)
            var summary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
            var calorieGoal = _preferences.Get(PreferenceKeys.CalorieGoal, 2000.0);
            if (summary.TotalCalories > 0)
            {
                TodayCaloriesDisplay = $"{summary.TotalCalories:F0}";
                CalorieProgress = calorieGoal > 0
                    ? Math.Min((summary.TotalCalories / calorieGoal) * 100, 100)
                    : 0;
                HasDashboardData = true;
            }
            else
            {
                TodayCaloriesDisplay = "-";
                CalorieProgress = 0;
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

        // Alte Events abmelden bevor neues VM erstellt wird
        CleanupCurrentCalculatorVm();

        if (value != null)
        {
            CurrentCalculatorVm = CreateCalculatorVm(value);
        }
        else
        {
            CurrentCalculatorVm = null;
        }
    }

    /// <summary>
    /// Meldet Event-Handler des aktuellen Calculator-VMs ab um Speicherlecks zu vermeiden.
    /// </summary>
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
            "BmiPage" => _bmiVmFactory(),
            "CaloriesPage" => _caloriesVmFactory(),
            "WaterPage" => _waterVmFactory(),
            "IdealWeightPage" => _idealWeightVmFactory(),
            "BodyFatPage" => _bodyFatVmFactory(),
            "BarcodeScannerPage" => _barcodeScannerVmFactory(),
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

    #region Back-Navigation (Double-Back-to-Exit)

    private DateTime _lastBackPress = DateTime.MinValue;
    private const int BackPressIntervalMs = 2000;

    /// <summary>
    /// Behandelt die Zurück-Taste. Gibt true zurück wenn konsumiert (App bleibt offen),
    /// false wenn die App geschlossen werden darf (Double-Back).
    /// Reihenfolge: Overlays → Calculator → ProgressVM Overlays → Tab zurück → Double-Back-to-Exit.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Achievements-Overlay offen → schließen
        if (ShowAchievements)
        {
            ShowAchievements = false;
            return true;
        }

        // 2. Weight Quick-Add Panel offen → schließen
        if (ShowWeightQuickAdd)
        {
            ShowWeightQuickAdd = false;
            return true;
        }

        // 3. Calculator-Page offen → schließen
        if (CurrentPage != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentPage = null);
            return true;
        }

        // 4. ProgressVM Overlays prüfen
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

        // 5. Nicht auf Home-Tab → zum Home-Tab wechseln
        if (SelectedTab != 0)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectedTab = 0);
            return true;
        }

        // 6. Auf Startseite: Double-Back-to-Exit
        var now = DateTime.UtcNow;
        if ((now - _lastBackPress).TotalMilliseconds < BackPressIntervalMs)
            return false; // App beenden lassen

        _lastBackPress = now;
        var msg = _localization.GetString("PressBackAgainToExit") ?? "Erneut drücken zum Beenden";
        ExitHintRequested?.Invoke(msg);
        return true; // Konsumiert
    }

    #endregion

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

    /// <summary>
    /// Trackt welche Rechner verwendet wurden (Bitmask) + XP.
    /// </summary>
    private void TrackCalculatorUsed(int calcIndex)
    {
        var mask = _preferences.Get(PreferenceKeys.CalculatorsUsedMask, 0);
        var bit = 1 << calcIndex;
        if ((mask & bit) == 0)
        {
            mask |= bit;
            _preferences.Set(PreferenceKeys.CalculatorsUsedMask, mask);
        }
        _levelService.AddXp(2); // XP für Rechner benutzen
    }

    [RelayCommand]
    private void OpenAchievements() => ShowAchievements = true;

    [RelayCommand]
    private void CloseAchievements() => ShowAchievements = false;

    [RelayCommand]
    private void OpenProgress() { CurrentPage = null; SelectedTab = 1; }

    #region Dashboard Quick-Add

    [RelayCommand]
    private void OpenWeightQuickAdd()
    {
        // Letztes Gewicht laden als Startwert
        _ = LoadLastWeightAsync();
        ShowWeightQuickAdd = true;
    }

    private async Task LoadLastWeightAsync()
    {
        try
        {
            var lastWeight = await _trackingService.GetLatestEntryAsync(FitnessRechner.Models.TrackingType.Weight);
            if (lastWeight != null)
                QuickAddWeight = lastWeight.Value;
        }
        catch
        {
            // Standardwert beibehalten bei Fehler
        }
    }

    [RelayCommand]
    private async Task SaveWeightQuickAdd()
    {
        try
        {
            if (QuickAddWeight < 20 || QuickAddWeight > 500) return;

            var entry = new FitnessRechner.Models.TrackingEntry
            {
                Type = FitnessRechner.Models.TrackingType.Weight,
                Value = QuickAddWeight,
                Date = DateTime.Today
            };
            await _trackingService.AddEntryAsync(entry);

            ShowWeightQuickAdd = false;
            FloatingTextRequested?.Invoke($"+{QuickAddWeight:F1} kg", "info");
            _hapticService.Click(); // Mittleres Feedback bei Speicherung
            _levelService.AddXp(10); // XP für Gewicht loggen
            await LoadDashboardDataAsync();
            _ = CheckGamificationProgressAsync();
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorSavingData") ?? "Error saving data");
        }
    }

    [RelayCommand]
    private void CancelWeightQuickAdd() => ShowWeightQuickAdd = false;

    [RelayCommand]
    private async Task QuickAddWater(string amountStr)
    {
        if (!int.TryParse(amountStr, out var amount)) return;
        try
        {
            var today = await _trackingService.GetLatestEntryAsync(FitnessRechner.Models.TrackingType.Water);

            if (today != null && today.Date.Date == DateTime.Today)
            {
                today.Value += amount;
                await _trackingService.UpdateEntryAsync(today);
            }
            else
            {
                var entry = new FitnessRechner.Models.TrackingEntry
                {
                    Type = FitnessRechner.Models.TrackingType.Water,
                    Value = amount,
                    Date = DateTime.Today
                };
                await _trackingService.AddEntryAsync(entry);
            }

            FloatingTextRequested?.Invoke($"+{amount} ml", "info");
            _hapticService.Tick(); // Leichtes Feedback bei Quick-Add
            _levelService.AddXp(3); // XP für Wasser loggen
            await LoadDashboardDataAsync();
            _ = CheckGamificationProgressAsync();

            // Wasser-Ziel Celebration (einmal pro Session)
            if (!_wasWaterGoalReachedOnDashboard && WaterProgress >= 100)
            {
                _wasWaterGoalReachedOnDashboard = true;
                FloatingTextRequested?.Invoke(
                    _localization.GetString("GoalReached") ?? "Goal reached!", "success");
                CelebrationRequested?.Invoke();
                _hapticService.HeavyClick();
                _soundService.PlaySuccess();
            }
        }
        catch (Exception)
        {
            MessageRequested?.Invoke(
                _localization.GetString("Error") ?? "Error",
                _localization.GetString("ErrorSavingData") ?? "Error saving data");
        }
    }

    [RelayCommand]
    private void OpenFoodQuickAdd()
    {
        CurrentPage = null;
        SelectedTab = 2;
        // Quick-Add Panel im FoodSearch öffnen
        FoodSearchViewModel.ShowQuickAddPanel = true;
    }

    #endregion

    /// <summary>
    /// Laedt Aktivitaets-Daten fuer die Heatmap (letzte 3 Monate).
    /// Kombiniert Tracking-Eintraege und Food-Logs zu einem Aktivitaets-Level pro Tag.
    /// Nutzt Batch-Query (GetFoodLogsInRangeAsync) statt N+1 Einzelabfragen.
    /// </summary>
    private async Task LoadHeatmapDataAsync()
    {
        try
        {
            var startDate = DateTime.Today.AddMonths(-3);
            var endDate = DateTime.Today;

            // Tracking-Eintraege laden (alle Typen)
            var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight, startDate, endDate);
            var waterEntries = await _trackingService.GetEntriesAsync(TrackingType.Water, startDate, endDate);
            var bmiEntries = await _trackingService.GetEntriesAsync(TrackingType.Bmi, startDate, endDate);
            var bodyFatEntries = await _trackingService.GetEntriesAsync(TrackingType.BodyFat, startDate, endDate);

            // Food-Log-Tage per Batch laden (1 Query statt ~90 Einzelabfragen)
            var foodLogs = await _foodSearchService.GetFoodLogsInRangeAsync(startDate, endDate);
            var foodLogDates = foodLogs.Keys.ToHashSet();

            // Aktivitaets-Level berechnen
            var data = new Dictionary<DateTime, int>();
            var totalDays = (endDate - startDate).Days + 1;

            for (var i = 0; i < totalDays; i++)
            {
                var date = startDate.AddDays(i).Date;

                // Punkte sammeln
                var hasWeight = weightEntries.Any(e => e.Date.Date == date);
                var hasWater = waterEntries.Any(e => e.Date.Date == date);
                var hasFood = foodLogDates.Contains(date);
                var hasBmi = bmiEntries.Any(e => e.Date.Date == date) || bodyFatEntries.Any(e => e.Date.Date == date);

                var score = (hasWeight ? 1 : 0) + (hasWater ? 1 : 0) + (hasFood ? 1 : 0) + (hasBmi ? 1 : 0);
                var level = score switch
                {
                    >= 4 => 4,
                    3 => 3,
                    2 => 2,
                    1 => 1,
                    _ => 0
                };

                if (level > 0)
                    data[date] = level;
            }

            HeatmapData = data;
            HasHeatmapData = data.Count > 0;
            ShowHeatmapHint = data.Count > 0 && data.Count < 7;
        }
        catch
        {
            // Heatmap ist optional - Fehler ignorieren
        }
    }

    #region Gamification Methods

    /// <summary>
    /// Gamification-Event: Achievement freigeschaltet.
    /// </summary>
    private void OnAchievementUnlocked(string titleKey, int xpReward)
    {
        _levelService.AddXp(xpReward);
        var title = _localization.GetString(titleKey) ?? titleKey;
        var text = $"{_localization.GetString("AchievementUnlockedText") ?? "Achievement!"} {title}";
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FloatingTextRequested?.Invoke(text, "achievement");
            CelebrationRequested?.Invoke();
            _hapticService.HeavyClick();
            _soundService.PlaySuccess();
            UpdateGamificationDisplay();
        });
    }

    /// <summary>
    /// Gamification-Event: Level Up.
    /// </summary>
    private void OnLevelUp(int newLevel)
    {
        var text = $"{_localization.GetString("LevelUpText") ?? "Level Up!"} → Level {newLevel}";
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FloatingTextRequested?.Invoke(text, "levelup");
            CelebrationRequested?.Invoke();
            _hapticService.HeavyClick();
            _soundService.PlaySuccess();
            UpdateGamificationDisplay();
        });
    }

    /// <summary>
    /// Gamification-Event: Challenge abgeschlossen.
    /// </summary>
    private void OnChallengeCompleted(int xpReward)
    {
        _levelService.AddXp(xpReward);
        var text = $"{ChallengeCompletedLabel} +{xpReward} XP";
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            FloatingTextRequested?.Invoke(text, "challenge");
            CelebrationRequested?.Invoke();
            _hapticService.HeavyClick();
            _soundService.PlaySuccess();
            IsChallengeCompleted = true;
            UpdateGamificationDisplay();
        });
    }

    /// <summary>
    /// Aktualisiert alle Gamification-UI-Properties.
    /// </summary>
    private void UpdateGamificationDisplay()
    {
        CurrentGamificationLevel = _levelService.CurrentLevel;
        LevelProgress = _levelService.LevelProgress;
        XpDisplay = _levelService.XpDisplay;
        IsChallengeCompleted = TodayChallenge.IsCompleted;

        OnPropertyChanged(nameof(LevelLabel));
        OnPropertyChanged(nameof(ChallengeTitleText));
        OnPropertyChanged(nameof(ChallengeDescText));
        OnPropertyChanged(nameof(ChallengeProgressValue));
        OnPropertyChanged(nameof(ChallengeProgressText));
        OnPropertyChanged(nameof(ChallengeXpText));
        OnPropertyChanged(nameof(RecentAchievements));
        OnPropertyChanged(nameof(AllAchievements));
        OnPropertyChanged(nameof(AchievementCountDisplay));
        OnPropertyChanged(nameof(HasRecentAchievements));
    }

    /// <summary>
    /// Baut Check-Kontexte und prüft Achievement- und Challenge-Fortschritt.
    /// </summary>
    private async Task CheckGamificationProgressAsync()
    {
        try
        {
            // Achievement-Kontext aufbauen
            var startDate = DateTime.Today.AddYears(-1);
            var weightEntries = await _trackingService.GetEntriesAsync(TrackingType.Weight, startDate, DateTime.Today);
            var waterEntries = await _trackingService.GetEntriesAsync(TrackingType.Water, startDate, DateTime.Today);
            var totalWaterMl = waterEntries.Sum(e => e.Value);

            var recipes = await _foodSearchService.GetRecipesAsync();
            var weightGoal = _preferences.Get(PreferenceKeys.WeightGoal, 0.0);
            var latestWeight = await _trackingService.GetLatestEntryAsync(TrackingType.Weight);
            var hasReachedGoal = weightGoal > 0 && latestWeight != null &&
                                 Math.Abs(latestWeight.Value - weightGoal) < 0.5;

            // Bitmask: Wie viele verschiedene Rechner wurden benutzt?
            var calcMask = _preferences.Get(PreferenceKeys.CalculatorsUsedMask, 0);
            var calcsUsed = 0;
            for (int i = 0; i < 5; i++)
                if ((calcMask & (1 << i)) != 0) calcsUsed++;

            // Kalorienziel-Tage in Folge berechnen (max letzte 14 Tage)
            var calorieGoal = _preferences.Get(PreferenceKeys.CalorieGoal, 2000.0);
            var waterGoal = _preferences.Get(PreferenceKeys.WaterGoal, 2500.0);
            var calorieDaysInRow = 0;
            var waterDaysInRow = 0;

            for (int i = 1; i <= 14; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                if (calorieGoal > 0)
                {
                    var summary = await _foodSearchService.GetDailySummaryAsync(date);
                    if (summary.TotalCalories > 0 && summary.TotalCalories <= calorieGoal)
                        calorieDaysInRow++;
                    else break;
                }
            }

            for (int i = 1; i <= 14; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                var dayWater = waterEntries.Where(e => e.Date.Date == date).Sum(e => e.Value);
                if (waterGoal > 0 && dayWater >= waterGoal)
                    waterDaysInRow++;
                else break;
            }

            var achievementCtx = new AchievementCheckContext
            {
                CurrentStreak = _streakService.CurrentStreak,
                TotalWeightEntries = weightEntries.Count,
                TotalWaterMl = totalWaterMl,
                TotalMealsLogged = _preferences.Get(PreferenceKeys.TotalMealsLogged, 0),
                TotalBarcodesScanned = _preferences.Get(PreferenceKeys.TotalBarcodesScanned, 0),
                TotalRecipesCreated = recipes.Count,
                DistinctFoodsLogged = _preferences.Get(PreferenceKeys.DistinctFoodsTracked, 0),
                CalculatorsUsed = calcsUsed,
                HasReachedWeightGoal = hasReachedGoal,
                IsPremium = _purchaseService.IsPremium,
                CurrentHour = DateTime.Now.Hour,
                CalorieGoalDaysInRow = calorieDaysInRow,
                WaterGoalDaysInRow = waterDaysInRow
            };

            _achievementService.CheckProgress(achievementCtx);

            // Challenge-Kontext aufbauen (heutige Daten)
            var todaySummary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
            var todayFoodLog = await _foodSearchService.GetFoodLogAsync(DateTime.Today);
            var todayWater = waterEntries.Where(e => e.Date.Date == DateTime.Today).Sum(e => e.Value);
            var todayWeight = weightEntries.Any(e => e.Date.Date == DateTime.Today);
            var bmiEntries = await _trackingService.GetEntriesAsync(TrackingType.Bmi,
                DateTime.Today, DateTime.Today.AddDays(1));

            var challengeCtx = new ChallengeCheckContext
            {
                TodayWaterMl = todayWater,
                TodayMealsCount = todayFoodLog.Count,
                HasWeightEntry = todayWeight,
                TodayCalories = todaySummary.TotalCalories,
                CalorieGoal = calorieGoal,
                TodayFoodsTracked = todayFoodLog.Count,
                TodayProtein = todaySummary.TotalProtein,
                HasUsedBmi = bmiEntries.Count > 0,
                HasScannedBarcode = _preferences.Get(PreferenceKeys.TotalBarcodesScanned, 0) > 0,
                HasBreakfast = todayFoodLog.Any(f => f.Meal == MealType.Breakfast),
                HasLunch = todayFoodLog.Any(f => f.Meal == MealType.Lunch),
                HasDinner = todayFoodLog.Any(f => f.Meal == MealType.Dinner)
            };

            _challengeService.CheckProgress(challengeCtx);

            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateGamificationDisplay);
        }
        catch
        {
            // Gamification ist optional - Fehler ignorieren
        }
    }

    /// <summary>
    /// Prüft ob Abend-Zusammenfassung angezeigt werden soll (nach 20 Uhr, wenn Einträge vorhanden).
    /// </summary>
    private async Task CheckEveningSummaryAsync()
    {
        try
        {
            var hour = DateTime.Now.Hour;
            if (hour < 20)
            {
                ShowEveningSummary = false;
                return;
            }

            var summary = await _foodSearchService.GetDailySummaryAsync(DateTime.Today);
            var waterEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Water);
            var todayWater = (waterEntry != null && waterEntry.Date.Date == DateTime.Today) ? waterEntry.Value : 0;
            var weightEntry = await _trackingService.GetLatestEntryAsync(TrackingType.Weight);
            var todayWeight = (weightEntry != null && weightEntry.Date.Date == DateTime.Today) ? weightEntry.Value : 0;

            // Nur anzeigen wenn heute etwas geloggt wurde
            if (summary.TotalCalories <= 0 && todayWater <= 0 && todayWeight <= 0)
            {
                ShowEveningSummary = false;
                return;
            }

            // Zusammenfassungs-Text
            var parts = new List<string>();
            if (summary.TotalCalories > 0) parts.Add($"{summary.TotalCalories:F0} kcal");
            if (todayWater > 0) parts.Add($"{todayWater:F0} ml");
            if (todayWeight > 0) parts.Add($"{todayWeight:F1} kg");
            EveningSummaryText = string.Join(" | ", parts);

            // Bewertung basierend auf Zielerreichung
            var score = DailyScoreFraction * 100;
            if (score >= 90)
            {
                EveningSummaryRating = _localization.GetString("RatingGreatDay") ?? "Great day!";
                EveningSummaryRatingColor = "#22C55E"; // Grün
            }
            else if (score >= 50)
            {
                EveningSummaryRating = _localization.GetString("RatingGoodDay") ?? "Good day!";
                EveningSummaryRatingColor = "#3B82F6"; // Blau
            }
            else
            {
                EveningSummaryRating = _localization.GetString("RatingTomorrowBetter") ?? "Tomorrow will be better!";
                EveningSummaryRatingColor = "#EAB308"; // Gelb
            }

            ShowEveningSummary = true;
        }
        catch
        {
            ShowEveningSummary = false;
        }
    }

    /// <summary>
    /// Laedt Wochenvergleichs-Daten (diese Woche vs. letzte Woche).
    /// Nutzt Batch-Query (GetDailySummariesInRangeAsync) statt 14 Einzelabfragen.
    /// </summary>
    private async Task LoadWeeklyComparisonAsync()
    {
        try
        {
            var today = DateTime.Today;
            var thisWeekStart = today.AddDays(-6);
            var lastWeekStart = today.AddDays(-13);
            var lastWeekEnd = today.AddDays(-7);

            // Kalorien per Batch laden (1 Query statt 14 Einzelabfragen)
            var summaries = await _foodSearchService.GetDailySummariesInRangeAsync(lastWeekStart, today);

            double thisWeekCal = 0, lastWeekCal = 0;
            int thisWeekDays = 0, lastWeekDays = 0;
            for (int i = 0; i < 7; i++)
            {
                var thisDate = thisWeekStart.AddDays(i);
                if (summaries.TryGetValue(thisDate, out var s1) && s1.TotalCalories > 0)
                { thisWeekCal += s1.TotalCalories; thisWeekDays++; }

                var lastDate = lastWeekStart.AddDays(i);
                if (summaries.TryGetValue(lastDate, out var s2) && s2.TotalCalories > 0)
                { lastWeekCal += s2.TotalCalories; lastWeekDays++; }
            }

            // Wasser
            var waterEntries = await _trackingService.GetEntriesAsync(
                TrackingType.Water, lastWeekStart, today.AddDays(1));
            var thisWeekWater = waterEntries.Where(e => e.Date.Date >= thisWeekStart).Sum(e => e.Value);
            var lastWeekWater = waterEntries.Where(e => e.Date.Date >= lastWeekStart && e.Date.Date <= lastWeekEnd).Sum(e => e.Value);
            var thisWeekWaterDays = waterEntries.Where(e => e.Date.Date >= thisWeekStart).Select(e => e.Date.Date).Distinct().Count();
            var lastWeekWaterDays = waterEntries.Where(e => e.Date.Date >= lastWeekStart && e.Date.Date <= lastWeekEnd).Select(e => e.Date.Date).Distinct().Count();

            // Gewicht
            var weightEntries = await _trackingService.GetEntriesAsync(
                TrackingType.Weight, lastWeekStart, today.AddDays(1));
            var thisWeekWeight = weightEntries.Where(e => e.Date.Date >= thisWeekStart).OrderByDescending(e => e.Date).FirstOrDefault();
            var lastWeekWeight = weightEntries.Where(e => e.Date.Date >= lastWeekStart && e.Date.Date <= lastWeekEnd).OrderByDescending(e => e.Date).FirstOrDefault();

            // Logging-Tage
            var trackingDates = await _trackingService.GetEntriesAsync(TrackingType.Weight, lastWeekStart, today.AddDays(1));
            var allLogDates = trackingDates.Select(e => e.Date.Date)
                .Concat(waterEntries.Select(e => e.Date.Date))
                .Distinct().ToList();
            var thisWeekLogCount = allLogDates.Count(d => d >= thisWeekStart);
            var lastWeekLogCount = allLogDates.Count(d => d >= lastWeekStart && d <= lastWeekEnd);

            // UI aktualisieren
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Kalorien
                if (thisWeekDays > 0 && lastWeekDays > 0)
                {
                    var avgThis = thisWeekCal / thisWeekDays;
                    var avgLast = lastWeekCal / lastWeekDays;
                    var diff = avgThis - avgLast;
                    WeeklyCaloriesChange = $"{avgThis:F0} vs {avgLast:F0} kcal";
                    WeeklyCaloriesColor = diff > 50 ? "#EF4444" : diff < -50 ? "#22C55E" : "#EAB308";
                }
                else
                {
                    WeeklyCaloriesChange = "-";
                }

                // Wasser
                if (thisWeekWaterDays > 0 && lastWeekWaterDays > 0)
                {
                    var avgThis = thisWeekWater / thisWeekWaterDays / 1000.0;
                    var avgLast = lastWeekWater / lastWeekWaterDays / 1000.0;
                    var diff = avgThis - avgLast;
                    WeeklyWaterChange = $"{avgThis:F1} vs {avgLast:F1} L";
                    WeeklyWaterColor = diff > 0.2 ? "#22C55E" : diff < -0.2 ? "#EF4444" : "#EAB308";
                }
                else
                {
                    WeeklyWaterChange = "-";
                }

                // Gewicht
                if (thisWeekWeight != null && lastWeekWeight != null)
                {
                    var diff = thisWeekWeight.Value - lastWeekWeight.Value;
                    var arrow = diff > 0 ? "↑" : diff < 0 ? "↓" : "→";
                    WeeklyWeightChange = $"{arrow} {Math.Abs(diff):F1} kg";
                    // Bei Gewicht ist ↓ oft positiv (Abnahme erwünscht)
                    WeeklyWeightColor = diff < -0.1 ? "#22C55E" : diff > 0.1 ? "#EF4444" : "#EAB308";
                }
                else
                {
                    WeeklyWeightChange = "-";
                }

                WeeklyLogDays = $"{thisWeekLogCount}/7 vs {lastWeekLogCount}/7";
                HasWeeklyComparison = thisWeekDays > 0 || thisWeekWaterDays > 0 || thisWeekWeight != null;
            });
        }
        catch
        {
            // Weekly Comparison ist optional
        }
    }

    #endregion

    private void OnAdUnavailable() =>
        MessageRequested?.Invoke(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage);

    private void OnAdsStateChanged(object? sender, EventArgs e) =>
        IsAdBannerVisible = _adService.BannerVisible;

    private void OnProgressFloatingText(string text, string category) =>
        FloatingTextRequested?.Invoke(text, category);

    private void OnProgressCelebration() =>
        CelebrationRequested?.Invoke();

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

        ProgressViewModel.Dispose();
        FoodSearchViewModel.Dispose();
        SettingsViewModel.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
