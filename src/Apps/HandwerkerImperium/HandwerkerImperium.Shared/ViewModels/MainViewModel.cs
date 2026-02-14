using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Event args for showing the daily reward dialog.
/// </summary>
public class DailyRewardEventArgs : EventArgs
{
    public List<DailyReward> Rewards { get; }
    public int CurrentDay { get; }
    public int CurrentStreak { get; }

    public DailyRewardEventArgs(List<DailyReward> rewards, int currentDay, int currentStreak)
    {
        Rewards = rewards;
        CurrentDay = currentDay;
        CurrentStreak = currentStreak;
    }
}

/// <summary>
/// ViewModel for the main game screen.
/// Displays workshops, money, level, and available orders.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IGameLoopService _gameLoopService;
    private readonly IOrderGeneratorService _orderGeneratorService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IOfflineProgressService _offlineProgressService;
    private readonly IDailyRewardService _dailyRewardService;
    private readonly IAchievementService _achievementService;
    private readonly ISaveGameService _saveGameService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
    private readonly IQuickJobService _quickJobService;
    private readonly IDailyChallengeService _dailyChallengeService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IEventService _eventService;
    private bool _disposed;
    private decimal _pendingOfflineEarnings;
    private QuickJob? _activeQuickJob;
    private bool _quickJobMiniGamePlayed;

    // Statisches Array vermeidet Allokation bei jedem RefreshWorkshops()-Aufruf
    private static readonly WorkshopType[] _workshopTypes = Enum.GetValues<WorkshopType>();

    // Zaehler fuer FloatingText-Anzeige (nur alle 3 Ticks, nicht jeden)
    private int _floatingTextCounter;

    // Phase 9: Smooth Money-Counter Animation
    private decimal _displayedMoney;
    private decimal _targetMoney;
    private DispatcherTimer? _moneyAnimTimer;
    private const int MoneyAnimIntervalMs = 33; // ~30fps fuer Counter
    private const decimal MoneyAnimSpeed = 0.15m; // Interpolations-Faktor pro Frame

    // EventHandler wrappers for new VMs (EventHandler<string> vs Action<string>)
    private readonly EventHandler<string> _workerMarketNavHandler;
    private readonly EventHandler<string> _workerProfileNavHandler;
    private readonly EventHandler<string> _buildingsNavHandler;
    private readonly EventHandler<string> _researchNavHandler;

    // Gespeicherte Delegate-Referenzen fuer Alert/Confirmation Events (fuer Dispose-Unsubscribe)
    private readonly Action<string, string, string> _alertHandler;
    private readonly Func<string, string, string, string, Task<bool>> _confirmHandler;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS FOR NAVIGATION AND DIALOGS
    // ═══════════════════════════════════════════════════════════════════════

    public event EventHandler<OfflineEarningsEventArgs>? ShowOfflineEarnings;
    public event EventHandler<LevelUpEventArgs>? ShowLevelUp;
    public event EventHandler<DailyRewardEventArgs>? ShowDailyReward;
    public event EventHandler<Achievement>? ShowAchievementUnlocked;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isAdBannerVisible;

    [ObservableProperty]
    private decimal _money;

    [ObservableProperty]
    private string _moneyDisplay = "0 €";

    [ObservableProperty]
    private decimal _incomePerSecond;

    [ObservableProperty]
    private string _incomeDisplay = "0 €/s";

    [ObservableProperty]
    private int _playerLevel = 1;

    [ObservableProperty]
    private int _currentXp;

    [ObservableProperty]
    private int _xpForNextLevel = 100;

    [ObservableProperty]
    private double _levelProgress;

    /// <summary>
    /// Zeigt pulsierenden Hint um erste Workshop-Karte (Level kleiner 3, noch kein Upgrade gemacht).
    /// </summary>
    [ObservableProperty]
    private bool _showTutorialHint;

    [ObservableProperty]
    private ObservableCollection<WorkshopDisplayModel> _workshops = [];

    [ObservableProperty]
    private ObservableCollection<Order> _availableOrders = [];

    [ObservableProperty]
    private bool _hasActiveOrder;

    [ObservableProperty]
    private Order? _activeOrder;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _hasDailyReward;

    [ObservableProperty]
    private string _goldenScrewsDisplay = "0";

    // Quick Jobs + Daily Challenges
    [ObservableProperty]
    private List<QuickJob> _quickJobs = [];

    [ObservableProperty]
    private List<DailyChallenge> _dailyChallenges = [];

    [ObservableProperty]
    private bool _hasDailyChallenges;

    [ObservableProperty]
    private bool _isChallengesExpanded = true;

    [ObservableProperty]
    private bool _canClaimAllBonus;

    [ObservableProperty]
    private string _quickJobTimerDisplay = string.Empty;

    [ObservableProperty]
    private bool _isQuickJobsExpanded = true;

    [ObservableProperty]
    private string _quickJobsExpandIconKind = "ChevronUp";

    [ObservableProperty]
    private string _challengesExpandIconKind = "ChevronUp";

    // Bulk Buy Multiplikator (1, 10, 100, 0=Max)
    [ObservableProperty]
    private int _bulkBuyAmount = 1;

    [ObservableProperty]
    private string _bulkBuyLabel = "x1";

    // Feierabend-Rush
    [ObservableProperty]
    private bool _isRushActive;

    [ObservableProperty]
    private string _rushTimeRemaining = "";

    [ObservableProperty]
    private bool _canActivateRush;

    [ObservableProperty]
    private string _rushButtonText = "";

    // Lieferant (Variable Rewards)
    [ObservableProperty]
    private bool _hasPendingDelivery;

    [ObservableProperty]
    private string _deliveryIcon = "";

    [ObservableProperty]
    private string _deliveryDescription = "";

    [ObservableProperty]
    private string _deliveryAmountText = "";

    [ObservableProperty]
    private string _deliveryTimeRemaining = "";

    // Meisterwerkzeuge
    [ObservableProperty]
    private int _masterToolsCollected;

    [ObservableProperty]
    private int _masterToolsTotal;

    // Aktives Event (Banner-Anzeige)
    [ObservableProperty]
    private bool _hasActiveEvent;

    [ObservableProperty]
    private string _activeEventIcon = "";

    [ObservableProperty]
    private string _activeEventName = "";

    [ObservableProperty]
    private string _activeEventDescription = "";

    [ObservableProperty]
    private string _activeEventTimeRemaining = "";

    [ObservableProperty]
    private string _seasonalModifierText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // DIALOG STATE
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isLevelUpDialogVisible;

    [ObservableProperty]
    private int _levelUpNewLevel;

    [ObservableProperty]
    private string _levelUpUnlockedText = "";

    [ObservableProperty]
    private bool _isOfflineEarningsDialogVisible;

    [ObservableProperty]
    private string _offlineEarningsAmountText = "";

    [ObservableProperty]
    private string _offlineEarningsDurationText = "";

    [ObservableProperty]
    private bool _isOfflineNewRecord;

    [ObservableProperty]
    private bool _isDailyRewardDialogVisible;

    [ObservableProperty]
    private string _dailyRewardDayText = "";

    [ObservableProperty]
    private string _dailyRewardStreakText = "";

    [ObservableProperty]
    private string _dailyRewardAmountText = "";

    [ObservableProperty]
    private bool _isAchievementDialogVisible;

    [ObservableProperty]
    private string _achievementName = "";

    [ObservableProperty]
    private string _achievementDescription = "";

    // Generic Alert/Confirm Dialog
    [ObservableProperty]
    private bool _isAlertDialogVisible;

    [ObservableProperty]
    private string _alertDialogTitle = "";

    [ObservableProperty]
    private string _alertDialogMessage = "";

    [ObservableProperty]
    private string _alertDialogButtonText = "OK";

    [ObservableProperty]
    private bool _isConfirmDialogVisible;

    [ObservableProperty]
    private string _confirmDialogTitle = "";

    [ObservableProperty]
    private string _confirmDialogMessage = "";

    [ObservableProperty]
    private string _confirmDialogAcceptText = "OK";

    [ObservableProperty]
    private string _confirmDialogCancelText = "";

    private TaskCompletionSource<bool>? _confirmDialogTcs;

    /// <summary>
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !_purchaseService.IsPremium;

    // Login-Streak (Daily Reward Streak)
    public int LoginStreak => _gameStateService.State.DailyRewardStreak;
    public bool HasLoginStreak => LoginStreak >= 2;

    // FloatingText Event fuer Dashboard-Animationen
    public event Action<string, string>? FloatingTextRequested;

    // Celebration Event fuer Confetti-Overlay (Level-Up, Achievement, Prestige)
    public event Action? CelebrationRequested;

    /// <summary>Wird ausgelöst um einen Exit-Hinweis anzuzeigen (z.B. Toast "Nochmal drücken zum Beenden").</summary>
    public event Action<string>? ExitHintRequested;

    // Navigation button texts
    public string NavHomeText => $"🏠\n{_localizationService.GetString("Home")}";
    public string NavStatsText => $"📊\n{_localizationService.GetString("Stats")}";
    public string NavShopText => $"🛒\n{_localizationService.GetString("Shop")}";
    public string NavSettingsText => $"⚙️\n{_localizationService.GetString("Settings")}";

    // ═══════════════════════════════════════════════════════════════════════
    // TAB NAVIGATION STATE
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isDashboardActive = true;

    [ObservableProperty]
    private bool _isShopActive;

    [ObservableProperty]
    private bool _isStatisticsActive;

    [ObservableProperty]
    private bool _isAchievementsActive;

    [ObservableProperty]
    private bool _isSettingsActive;

    [ObservableProperty]
    private bool _isWorkshopDetailActive;

    [ObservableProperty]
    private bool _isOrderDetailActive;

    [ObservableProperty]
    private bool _isSawingGameActive;

    [ObservableProperty]
    private bool _isPipePuzzleActive;

    [ObservableProperty]
    private bool _isWiringGameActive;

    [ObservableProperty]
    private bool _isPaintingGameActive;

    [ObservableProperty]
    private bool _isRoofTilingGameActive;

    [ObservableProperty]
    private bool _isBlueprintGameActive;

    [ObservableProperty]
    private bool _isDesignPuzzleGameActive;

    [ObservableProperty]
    private bool _isInspectionGameActive;

    [ObservableProperty]
    private bool _isWorkerMarketActive;

    [ObservableProperty]
    private bool _isWorkerProfileActive;

    [ObservableProperty]
    private bool _isBuildingsActive;

    [ObservableProperty]
    private bool _isResearchActive;

    /// <summary>
    /// Whether the bottom tab bar should be visible (hidden during mini-games and detail views).
    /// </summary>
    public bool IsTabBarVisible => !IsWorkshopDetailActive && !IsOrderDetailActive &&
                                    !IsSawingGameActive && !IsPipePuzzleActive &&
                                    !IsWiringGameActive && !IsPaintingGameActive &&
                                    !IsRoofTilingGameActive && !IsBlueprintGameActive &&
                                    !IsDesignPuzzleGameActive && !IsInspectionGameActive &&
                                    !IsWorkerProfileActive && !IsBuildingsActive;

    private void DeactivateAllTabs()
    {
        IsDashboardActive = false;
        IsShopActive = false;
        IsStatisticsActive = false;
        IsAchievementsActive = false;
        IsSettingsActive = false;
        IsWorkshopDetailActive = false;
        IsOrderDetailActive = false;
        IsSawingGameActive = false;
        IsPipePuzzleActive = false;
        IsWiringGameActive = false;
        IsPaintingGameActive = false;
        IsRoofTilingGameActive = false;
        IsBlueprintGameActive = false;
        IsDesignPuzzleGameActive = false;
        IsInspectionGameActive = false;
        IsWorkerMarketActive = false;
        IsWorkerProfileActive = false;
        IsBuildingsActive = false;
        IsResearchActive = false;
    }

    private void NotifyTabBarVisibility()
    {
        OnPropertyChanged(nameof(IsTabBarVisible));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════════════
    // CHILD VIEWMODELS
    // ═══════════════════════════════════════════════════════════════════════

    public ShopViewModel ShopViewModel { get; }
    public StatisticsViewModel StatisticsViewModel { get; }
    public AchievementsViewModel AchievementsViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public WorkshopViewModel WorkshopViewModel { get; }
    public OrderViewModel OrderViewModel { get; }
    public SawingGameViewModel SawingGameViewModel { get; }
    public PipePuzzleViewModel PipePuzzleViewModel { get; }
    public WiringGameViewModel WiringGameViewModel { get; }
    public PaintingGameViewModel PaintingGameViewModel { get; }
    public RoofTilingGameViewModel RoofTilingGameViewModel { get; }
    public BlueprintGameViewModel BlueprintGameViewModel { get; }
    public DesignPuzzleGameViewModel DesignPuzzleGameViewModel { get; }
    public InspectionGameViewModel InspectionGameViewModel { get; }
    public WorkerMarketViewModel WorkerMarketViewModel { get; }
    public WorkerProfileViewModel WorkerProfileViewModel { get; }
    public BuildingsViewModel BuildingsViewModel { get; }
    public ResearchViewModel ResearchViewModel { get; }

    public MainViewModel(
        IGameStateService gameStateService,
        IGameLoopService gameLoopService,
        IOrderGeneratorService orderGeneratorService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IOfflineProgressService offlineProgressService,
        IDailyRewardService dailyRewardService,
        IAchievementService achievementService,
        IPurchaseService purchaseService,
        IAdService adService,
        ISaveGameService saveGameService,
        IQuickJobService quickJobService,
        IDailyChallengeService dailyChallengeService,
        IRewardedAdService rewardedAdService,
        IEventService eventService,
        ShopViewModel shopViewModel,
        StatisticsViewModel statisticsViewModel,
        AchievementsViewModel achievementsViewModel,
        SettingsViewModel settingsViewModel,
        WorkshopViewModel workshopViewModel,
        OrderViewModel orderViewModel,
        SawingGameViewModel sawingGameViewModel,
        PipePuzzleViewModel pipePuzzleViewModel,
        WiringGameViewModel wiringGameViewModel,
        PaintingGameViewModel paintingGameViewModel,
        RoofTilingGameViewModel roofTilingGameViewModel,
        BlueprintGameViewModel blueprintGameViewModel,
        DesignPuzzleGameViewModel designPuzzleGameViewModel,
        InspectionGameViewModel inspectionGameViewModel,
        WorkerMarketViewModel workerMarketViewModel,
        WorkerProfileViewModel workerProfileViewModel,
        BuildingsViewModel buildingsViewModel,
        ResearchViewModel researchViewModel)
    {
        _gameStateService = gameStateService;
        _gameLoopService = gameLoopService;
        _offlineProgressService = offlineProgressService;
        _orderGeneratorService = orderGeneratorService;
        _audioService = audioService;
        _localizationService = localizationService;
        _dailyRewardService = dailyRewardService;
        _achievementService = achievementService;
        _purchaseService = purchaseService;
        _adService = adService;
        _saveGameService = saveGameService;
        _quickJobService = quickJobService;
        _dailyChallengeService = dailyChallengeService;
        _rewardedAdService = rewardedAdService;
        _eventService = eventService;
        _rewardedAdService.AdUnavailable += () => ShowAlertDialog(
            _localizationService.GetString("AdVideoNotAvailableTitle"),
            _localizationService.GetString("AdVideoNotAvailableMessage"),
            "OK");

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += (_, _) => IsAdBannerVisible = _adService.BannerVisible;

        // Banner beim Start anzeigen (fuer Desktop + Fallback falls AdMobHelper fehlschlaegt)
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();

        // Store child ViewModels
        ShopViewModel = shopViewModel;
        StatisticsViewModel = statisticsViewModel;
        AchievementsViewModel = achievementsViewModel;
        SettingsViewModel = settingsViewModel;
        WorkshopViewModel = workshopViewModel;
        OrderViewModel = orderViewModel;
        SawingGameViewModel = sawingGameViewModel;
        PipePuzzleViewModel = pipePuzzleViewModel;
        WiringGameViewModel = wiringGameViewModel;
        PaintingGameViewModel = paintingGameViewModel;
        RoofTilingGameViewModel = roofTilingGameViewModel;
        BlueprintGameViewModel = blueprintGameViewModel;
        DesignPuzzleGameViewModel = designPuzzleGameViewModel;
        InspectionGameViewModel = inspectionGameViewModel;
        WorkerMarketViewModel = workerMarketViewModel;
        WorkerProfileViewModel = workerProfileViewModel;
        BuildingsViewModel = buildingsViewModel;
        ResearchViewModel = researchViewModel;

        // Wire up child VM navigation events
        ShopViewModel.NavigationRequested += OnChildNavigation;
        StatisticsViewModel.NavigationRequested += OnChildNavigation;
        AchievementsViewModel.NavigationRequested += OnChildNavigation;
        SettingsViewModel.NavigationRequested += OnChildNavigation;
        WorkshopViewModel.NavigationRequested += OnChildNavigation;
        OrderViewModel.NavigationRequested += OnChildNavigation;
        SawingGameViewModel.NavigationRequested += OnChildNavigation;
        PipePuzzleViewModel.NavigationRequested += OnChildNavigation;
        WiringGameViewModel.NavigationRequested += OnChildNavigation;
        PaintingGameViewModel.NavigationRequested += OnChildNavigation;
        RoofTilingGameViewModel.NavigationRequested += OnChildNavigation;
        BlueprintGameViewModel.NavigationRequested += OnChildNavigation;
        DesignPuzzleGameViewModel.NavigationRequested += OnChildNavigation;
        InspectionGameViewModel.NavigationRequested += OnChildNavigation;

        _workerMarketNavHandler = (_, route) => OnChildNavigation(route);
        _workerProfileNavHandler = (_, route) => OnChildNavigation(route);
        _buildingsNavHandler = (_, route) => OnChildNavigation(route);
        _researchNavHandler = (_, route) => OnChildNavigation(route);
        WorkerMarketViewModel.NavigationRequested += _workerMarketNavHandler;
        WorkerProfileViewModel.NavigationRequested += _workerProfileNavHandler;
        BuildingsViewModel.NavigationRequested += _buildingsNavHandler;
        ResearchViewModel.NavigationRequested += _researchNavHandler;

        // Wire up child VM alert/confirmation events (gespeicherte Delegates fuer Dispose-Unsubscribe)
        _alertHandler = (t, m, b) => ShowAlertDialog(t, m, b);
        _confirmHandler = (t, m, a, c) => ShowConfirmDialog(t, m, a, c);

        SettingsViewModel.AlertRequested += _alertHandler;
        SettingsViewModel.ConfirmationRequested += _confirmHandler;
        ShopViewModel.AlertRequested += _alertHandler;
        ShopViewModel.ConfirmationRequested += _confirmHandler;
        OrderViewModel.ConfirmationRequested += _confirmHandler;
        StatisticsViewModel.AlertRequested += _alertHandler;
        WorkerMarketViewModel.AlertRequested += _alertHandler;
        WorkerProfileViewModel.AlertRequested += _alertHandler;
        WorkerProfileViewModel.ConfirmationRequested += _confirmHandler;
        BuildingsViewModel.AlertRequested += _alertHandler;
        ResearchViewModel.AlertRequested += _alertHandler;
        ResearchViewModel.ConfirmationRequested += _confirmHandler;

        // Subscribe to premium status changes
        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;

        // Subscribe to achievement events
        _achievementService.AchievementUnlocked += OnAchievementUnlocked;

        // Subscribe to events
        _gameStateService.MoneyChanged += OnMoneyChanged;
        _gameStateService.GoldenScrewsChanged += OnGoldenScrewsChanged;
        _gameStateService.LevelUp += OnLevelUp;
        _gameStateService.XpGained += OnXpGained;
        _gameStateService.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameStateService.WorkerHired += OnWorkerHired;
        _gameStateService.OrderCompleted += OnOrderCompleted;
        _gameStateService.StateLoaded += OnStateLoaded;
        _gameStateService.MiniGameResultRecorded += OnMiniGameResultRecorded;
        _gameLoopService.OnTick += OnGameTick;
        _gameLoopService.MasterToolUnlocked += OnMasterToolUnlocked;
        _gameLoopService.DeliveryArrived += OnDeliveryArrived;
        _localizationService.LanguageChanged += OnLanguageChanged;
        _eventService.EventStarted += OnEventStarted;
        _eventService.EventEnded += OnEventEnded;
        _dailyChallengeService.ChallengeProgressChanged += OnChallengeProgressChanged;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    public async void Initialize()
    {
        // Load saved game first
        if (!_gameStateService.IsInitialized)
        {
            await _saveGameService.LoadAsync();

            // If LoadAsync didn't initialize (no save file), create new state
            if (!_gameStateService.IsInitialized)
            {
                _gameStateService.Initialize();
            }
        }

        // Sprache synchronisieren: gespeicherte Sprache laden oder Gerätesprache übernehmen
        var savedLang = _gameStateService.State.Language;
        if (!string.IsNullOrEmpty(savedLang))
        {
            _localizationService.SetLanguage(savedLang);
        }
        else
        {
            // Neues Spiel: Gerätesprache in GameState übernehmen
            _gameStateService.State.Language = _localizationService.CurrentLanguage;
        }

        // Reload settings in SettingsVM now that game state is loaded
        SettingsViewModel.ReloadSettings();

        // Recover stuck active order from previous session
        // (mini-game state is not saved, so it cannot be resumed)
        if (_gameStateService.State.ActiveOrder != null)
        {
            _gameStateService.CancelActiveOrder();
        }

        RefreshFromState();

        // Generate orders if none or too few exist
        if (_gameStateService.State.AvailableOrders.Count < 3)
        {
            _orderGeneratorService.RefreshOrders();
            RefreshOrders();
        }

        // Quick Jobs initialisieren
        if (_gameStateService.State.QuickJobs.Count == 0)
            _quickJobService.GenerateJobs();
        RefreshQuickJobs();

        // Daily Challenges initialisieren
        _dailyChallengeService.CheckAndResetIfNewDay();
        RefreshChallenges();

        IsLoading = false;

        // Check for offline progress
        CheckOfflineProgress();

        // Check for daily reward
        CheckDailyReward();

        // Start the game loop for idle earnings
        _gameLoopService.Start();
    }

    private void CheckOfflineProgress()
    {
        var offlineDuration = _offlineProgressService.GetOfflineDuration();
        if (offlineDuration.TotalMinutes < 1)
            return;

        var earnings = _offlineProgressService.CalculateOfflineProgress();
        if (earnings <= 0)
            return;

        _pendingOfflineEarnings = earnings;
        var maxDuration = _offlineProgressService.GetMaxOfflineDuration();
        bool wasCapped = offlineDuration > maxDuration;
        var effectiveDuration = wasCapped ? maxDuration : offlineDuration;

        OfflineEarningsAmountText = MoneyFormatter.FormatCompact(earnings);
        var durationText = effectiveDuration.TotalHours >= 1
            ? $"{(int)effectiveDuration.TotalHours}h {effectiveDuration.Minutes}min"
            : $"{(int)effectiveDuration.TotalMinutes}min";
        // Hinweis wenn Offline-Dauer gekappt wurde
        if (wasCapped)
            durationText += $" (Max. {(int)maxDuration.TotalHours}h)";
        OfflineEarningsDurationText = durationText;

        // Neuer Rekord pruefen
        IsOfflineNewRecord = earnings > _gameStateService.State.MaxOfflineEarnings;
        if (IsOfflineNewRecord)
            _gameStateService.State.MaxOfflineEarnings = earnings;

        IsOfflineEarningsDialogVisible = true;

        ShowOfflineEarnings?.Invoke(this, new OfflineEarningsEventArgs(
            earnings, effectiveDuration, wasCapped));
    }

    public void CollectOfflineEarnings(bool withAdBonus)
    {
        var amount = _pendingOfflineEarnings;
        if (withAdBonus)
            amount *= 2;

        _gameStateService.AddMoney(amount);
        _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
        _pendingOfflineEarnings = 0;
    }

    private void CheckDailyReward()
    {
        HasDailyReward = _dailyRewardService.IsRewardAvailable;

        if (HasDailyReward)
        {
            var rewards = _dailyRewardService.GetRewardCycle();
            var currentDay = _dailyRewardService.CurrentDay;
            var currentStreak = _dailyRewardService.CurrentStreak;

            var todaysReward = _dailyRewardService.TodaysReward;
            DailyRewardDayText = string.Format(_localizationService.GetString("DayReward"), currentDay);
            DailyRewardStreakText = string.Format(_localizationService.GetString("DailyStreak"), currentStreak);
            DailyRewardAmountText = todaysReward != null
                ? MoneyFormatter.FormatCompact(todaysReward.Money)
                : "";
            IsDailyRewardDialogVisible = true;

            ShowDailyReward?.Invoke(this, new DailyRewardEventArgs(rewards, currentDay, currentStreak));
        }
    }

    [RelayCommand]
    public void ClaimDailyReward()
    {
        var reward = _dailyRewardService.ClaimReward();
        if (reward != null)
        {
            _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
            HasDailyReward = false;
            IsDailyRewardDialogVisible = false;
        }
    }

    [RelayCommand]
    private void DismissLevelUpDialog()
    {
        IsLevelUpDialogVisible = false;
    }

    [RelayCommand]
    private void DismissAchievementDialog()
    {
        IsAchievementDialogVisible = false;
    }

    [RelayCommand]
    private void DismissAlertDialog()
    {
        IsAlertDialogVisible = false;
    }

    [RelayCommand]
    private void ConfirmDialogAccept()
    {
        IsConfirmDialogVisible = false;
        _confirmDialogTcs?.TrySetResult(true);
    }

    [RelayCommand]
    private void ConfirmDialogCancel()
    {
        IsConfirmDialogVisible = false;
        _confirmDialogTcs?.TrySetResult(false);
    }

    private void ShowAlertDialog(string title, string message, string buttonText)
    {
        AlertDialogTitle = title;
        AlertDialogMessage = message;
        AlertDialogButtonText = buttonText;
        IsAlertDialogVisible = true;
    }

    private Task<bool> ShowConfirmDialog(string title, string message, string acceptText, string cancelText)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogMessage = message;
        ConfirmDialogAcceptText = acceptText;
        ConfirmDialogCancelText = cancelText;
        _confirmDialogTcs = new TaskCompletionSource<bool>();
        IsConfirmDialogVisible = true;
        return _confirmDialogTcs.Task;
    }

    [RelayCommand]
    private void CollectOfflineEarningsNormal()
    {
        CollectOfflineEarnings(false);
        IsOfflineEarningsDialogVisible = false;
    }

    [RelayCommand]
    private async Task CollectOfflineEarningsWithAdAsync()
    {
        var success = await _rewardedAdService.ShowAdAsync("offline_double");
        CollectOfflineEarnings(success);
        IsOfflineEarningsDialogVisible = false;
    }

    private void RefreshFromState()
    {
        var state = _gameStateService.State;

        // Update properties
        Money = state.Money;
        // Beim Start: sofort setzen, kein Ticken
        _displayedMoney = state.Money;
        _targetMoney = state.Money;
        MoneyDisplay = FormatMoney(state.Money);
        IncomePerSecond = state.NetIncomePerSecond;
        IncomeDisplay = $"{FormatMoney(state.NetIncomePerSecond)}/s";
        PlayerLevel = state.PlayerLevel;
        CurrentXp = state.CurrentXp;
        XpForNextLevel = state.XpForNextLevel;
        LevelProgress = state.LevelProgress;
        GoldenScrewsDisplay = state.GoldenScrews.ToString("N0");

        // Login-Streak aktualisieren
        OnPropertyChanged(nameof(LoginStreak));
        OnPropertyChanged(nameof(HasLoginStreak));

        // Rush/Delivery/MasterTools
        UpdateRushDisplay();
        UpdateDeliveryDisplay();
        MasterToolsCollected = state.CollectedMasterTools.Count;
        MasterToolsTotal = MasterTool.GetAllDefinitions().Count;

        // Refresh workshops
        RefreshWorkshops();

        // Tutorial-Hint: Pulsierender Rahmen wenn noch nie ein Upgrade gemacht wurde
        ShowTutorialHint = !state.HasSeenTutorialHint && state.PlayerLevel < 3;

        // Refresh orders
        RefreshOrders();

        // Check for active order
        HasActiveOrder = state.ActiveOrder != null;
        ActiveOrder = state.ActiveOrder;
    }

    private void RefreshWorkshops()
    {
        var state = _gameStateService.State;

        // Erste Initialisierung: Items erstellen
        if (Workshops.Count == 0)
        {
            foreach (var type in _workshopTypes)
            {
                Workshops.Add(CreateWorkshopDisplay(state, type));
            }
            return;
        }

        // Update: Bestehende Items aktualisieren (kein Clear/Add → weniger UI-Churn)
        for (int i = 0; i < _workshopTypes.Length && i < Workshops.Count; i++)
        {
            UpdateWorkshopDisplay(Workshops[i], state, _workshopTypes[i]);
        }
    }

    /// <summary>
    /// Aktualisiert nur einen einzelnen Workshop (statt alle) → weniger UI-Churn bei Upgrade/Hire.
    /// </summary>
    private void RefreshSingleWorkshop(WorkshopType type)
    {
        var state = _gameStateService.State;
        var index = Array.IndexOf(_workshopTypes, type);
        if (index >= 0 && index < Workshops.Count)
        {
            UpdateWorkshopDisplay(Workshops[index], state, type);
        }
    }

    private WorkshopDisplayModel CreateWorkshopDisplay(GameState state, WorkshopType type)
    {
        var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
        bool isUnlocked = state.IsWorkshopUnlocked(type);
        return new WorkshopDisplayModel
        {
            Type = type,
            Icon = type.GetIcon(),
            IconKind = GetWorkshopIconKind(type, workshop?.Level ?? 1),
            Name = _localizationService.GetString(type.GetLocalizationKey()),
            Level = workshop?.Level ?? 1,
            WorkerCount = workshop?.Workers.Count ?? 0,
            MaxWorkers = workshop?.MaxWorkers ?? 1,
            IncomePerSecond = workshop?.IncomePerSecond ?? 0,
            UpgradeCost = workshop?.UpgradeCost ?? 100,
            HireWorkerCost = workshop?.HireWorkerCost ?? 50,
            IsUnlocked = isUnlocked,
            UnlockLevel = type.GetUnlockLevel(),
            RequiredPrestige = type.GetRequiredPrestige(),
            UnlockCost = type.GetUnlockCost(),
            CanBuyUnlock = _gameStateService.CanPurchaseWorkshop(type),
            UnlockDisplay = type.GetRequiredPrestige() > 0
                ? $"{_localizationService.GetString("Prestige")} {type.GetRequiredPrestige()}"
                : $"Lv. {type.GetUnlockLevel()}",
            CanUpgrade = workshop?.CanUpgrade ?? true,
            CanHireWorker = workshop?.CanHireWorker ?? false,
            CanAffordUpgrade = state.Money >= (workshop?.UpgradeCost ?? 100),
            CanAffordWorker = state.Money >= (workshop?.HireWorkerCost ?? 50)
        };
    }

    private void UpdateWorkshopDisplay(WorkshopDisplayModel model, GameState state, WorkshopType type)
    {
        var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
        bool isUnlocked = state.IsWorkshopUnlocked(type);

        model.Name = _localizationService.GetString(type.GetLocalizationKey());
        model.Level = workshop?.Level ?? 1;
        model.IconKind = GetWorkshopIconKind(type, model.Level);
        model.WorkerCount = workshop?.Workers.Count ?? 0;
        model.MaxWorkers = workshop?.MaxWorkers ?? 1;
        model.IncomePerSecond = workshop?.IncomePerSecond ?? 0;
        model.UpgradeCost = workshop?.UpgradeCost ?? 100;
        model.HireWorkerCost = workshop?.HireWorkerCost ?? 50;
        model.IsUnlocked = isUnlocked;
        model.UnlockCost = type.GetUnlockCost();
        model.CanBuyUnlock = _gameStateService.CanPurchaseWorkshop(type);
        model.UnlockDisplay = type.GetRequiredPrestige() > 0
            ? $"{_localizationService.GetString("Prestige")} {type.GetRequiredPrestige()}"
            : $"Lv. {type.GetUnlockLevel()}";
        model.CanUpgrade = workshop?.CanUpgrade ?? true;
        model.CanHireWorker = workshop?.CanHireWorker ?? false;
        model.CanAffordUpgrade = state.Money >= (workshop?.UpgradeCost ?? 100);
        model.CanAffordWorker = state.Money >= (workshop?.HireWorkerCost ?? 50);

        model.NotifyAllChanged();
    }

    private void RefreshOrders()
    {
        var state = _gameStateService.State;
        AvailableOrders.Clear();

        foreach (var order in state.AvailableOrders)
        {
            // Populate localized display fields
            var localizedTitle = _localizationService.GetString(order.TitleKey);
            order.DisplayTitle = string.IsNullOrEmpty(localizedTitle) ? order.TitleFallback : localizedTitle;
            order.DisplayWorkshopName = _localizationService.GetString(order.WorkshopType.GetLocalizationKey());
            AvailableOrders.Add(order);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SelectWorkshopAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked)
        {
            // Level-Anforderung prüfen
            if (!_gameStateService.CanPurchaseWorkshop(workshop.Type))
            {
                var reqLevel = workshop.Type.GetUnlockLevel();
                var reqPrestige = workshop.Type.GetRequiredPrestige();
                string reason = reqPrestige > 0
                    ? $"{_localizationService.GetString("Prestige")} {reqPrestige}"
                    : $"Level {reqLevel}";
                ShowAlertDialog(
                    _localizationService.GetString("WorkshopLocked"),
                    $"{_localizationService.GetString("RequiresLevel")}: {reason}",
                    "OK");
                await _audioService.PlaySoundAsync(GameSound.ButtonTap);
                return;
            }

            // Level erreicht → Kauf anbieten
            var unlockCost = workshop.Type.GetUnlockCost();
            var costDisplay = MoneyFormatter.FormatCompact(unlockCost);

            // Video-Rabatt: 50% Kosten (nur wenn Werbung aktiv)
            if (ShowAds)
            {
                var halfCost = unlockCost / 2m;
                var halfCostDisplay = MoneyFormatter.FormatCompact(halfCost);

                var watchAd = await ShowConfirmDialog(
                    _localizationService.GetString("UnlockWorkshop"),
                    $"{_localizationService.GetString("UnlockWorkshopCost")}: {costDisplay}\n{_localizationService.GetString("WatchAdForHalfPrice")}: {halfCostDisplay}",
                    _localizationService.GetString("WatchAdForDiscount"),
                    $"{_localizationService.GetString("BuyFull")} ({costDisplay})");

                if (watchAd)
                {
                    // Video schauen → 50% Rabatt
                    var success = await _rewardedAdService.ShowAdAsync("workshop_unlock");
                    if (success)
                    {
                        if (_gameStateService.TryPurchaseWorkshop(workshop.Type, halfCost))
                        {
                            RefreshWorkshops();
                            ShowAlertDialog(
                                _localizationService.GetString("WorkshopUnlocked"),
                                _localizationService.GetString(workshop.Type.GetLocalizationKey()),
                                "OK");
                            CelebrationRequested?.Invoke();
                        }
                        else
                        {
                            ShowAlertDialog(
                                _localizationService.GetString("NotEnoughMoney"),
                                $"{_localizationService.GetString("Required")}: {halfCostDisplay}",
                                "OK");
                        }
                    }
                }
                else
                {
                    // Voll-Preis kaufen
                    if (_gameStateService.TryPurchaseWorkshop(workshop.Type))
                    {
                        RefreshWorkshops();
                        ShowAlertDialog(
                            _localizationService.GetString("WorkshopUnlocked"),
                            _localizationService.GetString(workshop.Type.GetLocalizationKey()),
                            "OK");
                        CelebrationRequested?.Invoke();
                    }
                    else
                    {
                        ShowAlertDialog(
                            _localizationService.GetString("NotEnoughMoney"),
                            $"{_localizationService.GetString("Required")}: {costDisplay}",
                            "OK");
                    }
                }
            }
            else
            {
                // Kein Werbung → direkt kaufen
                if (_gameStateService.TryPurchaseWorkshop(workshop.Type))
                {
                    RefreshWorkshops();
                    ShowAlertDialog(
                        _localizationService.GetString("WorkshopUnlocked"),
                        _localizationService.GetString(workshop.Type.GetLocalizationKey()),
                        "OK");
                    CelebrationRequested?.Invoke();
                }
                else
                {
                    ShowAlertDialog(
                        _localizationService.GetString("NotEnoughMoney"),
                        $"{_localizationService.GetString("Required")}: {costDisplay}",
                        "OK");
                }
            }
            return;
        }

        // Navigate to workshop detail page
        WorkshopViewModel.SetWorkshopType(workshop.Type);
        DeactivateAllTabs();
        IsWorkshopDetailActive = true;
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void CycleBulkBuy()
    {
        BulkBuyAmount = BulkBuyAmount switch
        {
            1 => 10,
            10 => 100,
            100 => 0, // Max
            _ => 1
        };
        BulkBuyLabel = BulkBuyAmount switch
        {
            0 => "Max",
            _ => $"x{BulkBuyAmount}"
        };
        RefreshWorkshops();
    }

    [RelayCommand]
    private async Task UpgradeWorkshopAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked || !workshop.CanUpgrade)
            return;

        if (BulkBuyAmount == 1)
        {
            if (_gameStateService.TryUpgradeWorkshop(workshop.Type))
            {
                await _audioService.PlaySoundAsync(GameSound.Upgrade);
                FloatingTextRequested?.Invoke("+1 Level!", "level");
            }
        }
        else
        {
            int upgraded = _gameStateService.TryUpgradeWorkshopBulk(workshop.Type, BulkBuyAmount);
            if (upgraded > 0)
            {
                await _audioService.PlaySoundAsync(GameSound.Upgrade);
                FloatingTextRequested?.Invoke($"+{upgraded} Level!", "level");
            }
        }
    }

    [RelayCommand]
    private async Task HireWorkerAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked || !workshop.CanHireWorker)
            return;

        // Zum Arbeitermarkt navigieren statt direkt zu hiren
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
        SelectWorkerMarketTab();
    }

    [RelayCommand]
    private async Task StartOrderAsync(Order order)
    {
        if (HasActiveOrder) return;

        _gameStateService.StartOrder(order);
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Show order detail
        OrderViewModel.SetOrder(order);
        DeactivateAllTabs();
        IsOrderDetailActive = true;
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private async Task RefreshOrdersAsync()
    {
        _orderGeneratorService.RefreshOrders();
        RefreshOrders();
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        SelectSettingsTab();
    }

    [RelayCommand]
    private void NavigateToShop()
    {
        SelectShopTab();
    }

    [RelayCommand]
    private void NavigateToStatistics()
    {
        SelectStatisticsTab();
    }

    [RelayCommand]
    private void NavigateToAchievements()
    {
        SelectAchievementsTab();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TAB SELECTION COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectDashboardTab()
    {
        DeactivateAllTabs();
        IsDashboardActive = true;
        NotifyTabBarVisibility();

        // Aufträge sicherstellen (falls leer z.B. nach Spielabbruch)
        if (_gameStateService.IsInitialized && _gameStateService.State.AvailableOrders.Count == 0)
        {
            _orderGeneratorService.RefreshOrders();
            RefreshOrders();
        }
    }

    [RelayCommand]
    private void SelectStatisticsTab()
    {
        DeactivateAllTabs();
        IsStatisticsActive = true;
        NotifyTabBarVisibility();
        StatisticsViewModel.RefreshStatisticsCommand.Execute(null);
    }

    [RelayCommand]
    private void SelectAchievementsTab()
    {
        DeactivateAllTabs();
        IsAchievementsActive = true;
        AchievementsViewModel.LoadAchievements();
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectShopTab()
    {
        DeactivateAllTabs();
        IsShopActive = true;
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectSettingsTab()
    {
        DeactivateAllTabs();
        IsSettingsActive = true;
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectWorkerMarketTab()
    {
        DeactivateAllTabs();
        IsWorkerMarketActive = true;
        WorkerMarketViewModel.LoadMarket();
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectBuildingsTab()
    {
        DeactivateAllTabs();
        IsBuildingsActive = true;
        BuildingsViewModel.LoadBuildings();
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectResearchTab()
    {
        DeactivateAllTabs();
        IsResearchActive = true;
        ResearchViewModel.LoadResearchTree();
        NotifyTabBarVisibility();
    }

    #region Back-Navigation (Double-Back-to-Exit)

    private DateTime _lastBackPress = DateTime.MinValue;
    private const int BackPressIntervalMs = 2000;

    /// <summary>
    /// Behandelt die Zurück-Taste. Gibt true zurück wenn konsumiert (App bleibt offen),
    /// false wenn die App geschlossen werden darf (Double-Back).
    /// Reihenfolge: Dialoge → MiniGame/Detail → Sub-Tabs → Dashboard → Double-Back-to-Exit.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Offene Dialoge schließen (höchste Priorität)
        if (IsConfirmDialogVisible) { ConfirmDialogCancel(); return true; }
        if (IsAlertDialogVisible) { DismissAlertDialog(); return true; }
        if (IsAchievementDialogVisible) { DismissAchievementDialog(); return true; }
        if (IsLevelUpDialogVisible) { DismissLevelUpDialog(); return true; }
        if (IsOfflineEarningsDialogVisible) { CollectOfflineEarningsNormal(); return true; }
        if (IsDailyRewardDialogVisible) { IsDailyRewardDialogVisible = false; return true; }

        // 2. MiniGame aktiv → zurück zum Dashboard
        if (IsSawingGameActive || IsPipePuzzleActive || IsWiringGameActive || IsPaintingGameActive ||
            IsRoofTilingGameActive || IsBlueprintGameActive || IsDesignPuzzleGameActive || IsInspectionGameActive)
        {
            SelectDashboardTab();
            return true;
        }

        // 3. Worker-Profile Bottom-Sheet → nur Sheet schließen (darunterliegende View bleibt)
        if (IsWorkerProfileActive)
        {
            IsWorkerProfileActive = false;
            if (_adService.AdsEnabled && !_purchaseService.IsPremium)
                _adService.ShowBanner();
            NotifyTabBarVisibility();
            return true;
        }

        // 4. Detail-Views → zurück zum Dashboard
        if (IsWorkshopDetailActive || IsOrderDetailActive)
        {
            SelectDashboardTab();
            return true;
        }

        // 5. Sub-Tabs (Markt, Gebäude, Research) → zurück zum Dashboard
        if (IsWorkerMarketActive || IsBuildingsActive || IsResearchActive)
        {
            SelectDashboardTab();
            return true;
        }

        // 6. Nicht-Dashboard-Tabs → zum Dashboard
        if (IsShopActive || IsStatisticsActive || IsAchievementsActive || IsSettingsActive)
        {
            SelectDashboardTab();
            return true;
        }

        // 7. Auf Dashboard: Double-Back-to-Exit
        var now = DateTime.UtcNow;
        if ((now - _lastBackPress).TotalMilliseconds < BackPressIntervalMs)
            return false; // App beenden lassen

        _lastBackPress = now;
        var msg = _localizationService.GetString("PressBackAgainToExit") ?? "Erneut drücken zum Beenden";
        ExitHintRequested?.Invoke(msg);
        return true; // Konsumiert
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // QUICK JOB + DAILY CHALLENGE COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StartQuickJob(QuickJob? job)
    {
        if (job == null || job.IsCompleted) return;

        // Tageslimit prüfen (verhindert Reward-Farming)
        if ((_quickJobService as QuickJobService)?.IsDailyLimitReached == true)
        {
            var template = _localizationService.GetString("QuickJobDailyLimit");
            var limitText = !string.IsNullOrEmpty(template)
                ? string.Format(template, QuickJobService.MaxQuickJobsPerDay)
                : $"Tageslimit erreicht ({QuickJobService.MaxQuickJobsPerDay}/Tag)";
            FloatingTextRequested?.Invoke(limitText, "Warning");
            return;
        }

        _activeQuickJob = job;
        _quickJobMiniGamePlayed = false;
        var route = job.MiniGameType.GetRoute();
        DeactivateAllTabs();
        NavigateToMiniGame(route, "");
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void ToggleChallengesExpanded()
    {
        IsChallengesExpanded = !IsChallengesExpanded;
        ChallengesExpandIconKind = IsChallengesExpanded ? "ChevronUp" : "ChevronDown";
    }

    [RelayCommand]
    private void ToggleQuickJobsExpanded()
    {
        IsQuickJobsExpanded = !IsQuickJobsExpanded;
        QuickJobsExpandIconKind = IsQuickJobsExpanded ? "ChevronUp" : "ChevronDown";
    }

    [RelayCommand]
    private void ClaimChallengeReward(DailyChallenge? challenge)
    {
        if (challenge == null) return;
        _dailyChallengeService.ClaimReward(challenge.Id);
        RefreshChallenges();
    }

    [RelayCommand]
    private void ClaimAllChallengesBonus()
    {
        _dailyChallengeService.ClaimAllCompletedBonus();
        RefreshChallenges();
    }

    [RelayCommand]
    private async Task RetryChallengeWithAdAsync(DailyChallenge? challenge)
    {
        if (challenge == null || challenge.IsCompleted || challenge.HasRetriedWithAd || challenge.CurrentValue == 0)
            return;

        var success = await _rewardedAdService.ShowAdAsync("daily_challenge_retry");
        if (success)
        {
            _dailyChallengeService.RetryChallenge(challenge.Id);
            RefreshChallenges();
            ShowAlertDialog(
                _localizationService.GetString("ChallengeRetried"),
                challenge.DisplayDescription,
                "OK");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FEIERABEND-RUSH COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    private const int RushCostScrews = 10;
    private const int RushDurationHours = 2;

    [RelayCommand]
    private void ActivateRush()
    {
        var state = _gameStateService.State;
        if (state.IsRushBoostActive) return;

        if (state.IsFreeRushAvailable)
        {
            // Täglicher Gratis-Rush
            state.RushBoostEndTime = DateTime.UtcNow.AddHours(RushDurationHours);
            state.LastFreeRushUsed = DateTime.UtcNow;
            _gameStateService.MarkDirty();
            _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
            FloatingTextRequested?.Invoke($"⚡ Rush 2x ({RushDurationHours}h)!", "Rush");
            CelebrationRequested?.Invoke();
        }
        else if (_gameStateService.TrySpendGoldenScrews(RushCostScrews))
        {
            // Bezahlter Rush (Goldschrauben)
            state.RushBoostEndTime = DateTime.UtcNow.AddHours(RushDurationHours);
            _gameStateService.MarkDirty();
            _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
            FloatingTextRequested?.Invoke($"⚡ Rush 2x ({RushDurationHours}h)!", "Rush");
        }
        else
        {
            ShowAlertDialog(
                _localizationService.GetString("NotEnoughScrews"),
                string.Format(_localizationService.GetString("RushCostScrews"), RushCostScrews),
                "OK");
        }

        UpdateRushDisplay();
    }

    private void UpdateRushDisplay()
    {
        var state = _gameStateService.State;
        IsRushActive = state.IsRushBoostActive;

        if (IsRushActive)
        {
            var remaining = state.RushBoostEndTime - DateTime.UtcNow;
            RushTimeRemaining = remaining.TotalMinutes >= 60
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                : $"{remaining.Minutes}m {remaining.Seconds:D2}s";
            CanActivateRush = false;
            RushButtonText = $"⚡ {RushTimeRemaining}";
        }
        else
        {
            RushTimeRemaining = "";
            CanActivateRush = true;
            RushButtonText = state.IsFreeRushAvailable
                ? _localizationService.GetString("RushFreeActivation")
                : $"⚡ Rush ({RushCostScrews} 🔩)";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIEFERANT COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ClaimDelivery()
    {
        var state = _gameStateService.State;
        var delivery = state.PendingDelivery;
        if (delivery == null || delivery.IsExpired)
        {
            HasPendingDelivery = false;
            state.PendingDelivery = null;
            return;
        }

        // Belohnung anwenden
        switch (delivery.Type)
        {
            case Models.Enums.DeliveryType.Money:
                _gameStateService.AddMoney(delivery.Amount);
                FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(delivery.Amount)}", "money");
                break;

            case Models.Enums.DeliveryType.GoldenScrews:
                _gameStateService.AddGoldenScrews((int)delivery.Amount);
                FloatingTextRequested?.Invoke($"+{(int)delivery.Amount} 🔩", "screw");
                break;

            case Models.Enums.DeliveryType.Experience:
                _gameStateService.AddXp((int)delivery.Amount);
                FloatingTextRequested?.Invoke($"+{(int)delivery.Amount} XP", "xp");
                break;

            case Models.Enums.DeliveryType.MoodBoost:
                foreach (var ws in state.Workshops)
                foreach (var worker in ws.Workers)
                    worker.Mood = Math.Min(100m, worker.Mood + delivery.Amount);
                FloatingTextRequested?.Invoke($"😊 +{(int)delivery.Amount} Mood", "mood");
                break;

            case Models.Enums.DeliveryType.SpeedBoost:
                state.SpeedBoostEndTime = DateTime.UtcNow.AddMinutes((double)delivery.Amount);
                FloatingTextRequested?.Invoke($"⚡ 2x ({(int)delivery.Amount}min)", "speed");
                break;
        }

        _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
        state.TotalDeliveriesClaimed++;
        state.PendingDelivery = null;
        HasPendingDelivery = false;
        _gameStateService.MarkDirty();
    }

    private void UpdateDeliveryDisplay()
    {
        var delivery = _gameStateService.State.PendingDelivery;
        if (delivery == null || delivery.IsExpired)
        {
            if (HasPendingDelivery)
            {
                HasPendingDelivery = false;
                _gameStateService.State.PendingDelivery = null;
            }
            return;
        }

        HasPendingDelivery = true;
        DeliveryIcon = delivery.Icon;
        DeliveryDescription = _localizationService.GetString(delivery.DescriptionKey);

        DeliveryAmountText = delivery.Type switch
        {
            Models.Enums.DeliveryType.Money => MoneyFormatter.FormatCompact(delivery.Amount),
            Models.Enums.DeliveryType.GoldenScrews => $"{(int)delivery.Amount} 🔩",
            Models.Enums.DeliveryType.Experience => $"{(int)delivery.Amount} XP",
            Models.Enums.DeliveryType.MoodBoost => $"+{(int)delivery.Amount} Mood",
            Models.Enums.DeliveryType.SpeedBoost => $"{(int)delivery.Amount}min 2x",
            _ => ""
        };

        var remaining = delivery.TimeRemaining;
        DeliveryTimeRemaining = $"{remaining.Minutes}:{remaining.Seconds:D2}";
    }

    private void RefreshQuickJobs()
    {
        QuickJobs = _quickJobService.GetAvailableJobs();
    }

    private void RefreshChallenges()
    {
        var state = _dailyChallengeService.GetState();
        // Neue Liste erstellen, damit PropertyChanged zuverlässig feuert
        // (gleiche Referenz wird vom CommunityToolkit-Setter ignoriert)
        DailyChallenges = new List<DailyChallenge>(state.Challenges);
        HasDailyChallenges = state.Challenges.Count > 0;
        CanClaimAllBonus = _dailyChallengeService.AreAllCompleted && !state.AllCompletedBonusClaimed;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHILD NAVIGATION HANDLER
    // ═══════════════════════════════════════════════════════════════════════

    private void OnChildNavigation(string route)
    {
        // Relative route: "../minigame/..." → strip "../" and handle as minigame navigation
        if (route.StartsWith("../") && route.Length > 3 && route[3] != '.')
        {
            OnChildNavigation(route[3..]);
            return;
        }

        // Pure back navigation: ".." or "../.."
        if (route is ".." or "../..")
        {
            // Worker-Profile Bottom-Sheet: nur schließen, darunterliegende View beibehalten
            if (IsWorkerProfileActive)
            {
                IsWorkerProfileActive = false;
                if (_adService.AdsEnabled && !_purchaseService.IsPremium)
                    _adService.ShowBanner();
                NotifyTabBarVisibility();
                return;
            }

            // QuickJob-Rückkehr: Belohnung nur vergeben wenn MiniGame tatsächlich gespielt wurde
            if (_activeQuickJob != null)
            {
                if (_quickJobMiniGamePlayed)
                {
                    _activeQuickJob.IsCompleted = true;
                    _gameStateService.AddMoney(_activeQuickJob.Reward);
                    _gameStateService.AddXp(_activeQuickJob.XpReward);
                    _gameStateService.State.TotalQuickJobsCompleted++;
                    (_quickJobService as QuickJobService)?.NotifyJobCompleted(_activeQuickJob);
                    (_dailyChallengeService as DailyChallengeService)?.OnQuickJobCompleted();
                }
                _activeQuickJob = null;
                _quickJobMiniGamePlayed = false;
                RefreshQuickJobs();
            }

            SelectDashboardTab();
            RefreshFromState();
            return;
        }

        // "//main" = reset to main (from settings)
        if (route == "//main")
        {
            SelectDashboardTab();
            RefreshFromState();
            return;
        }

        // "minigame/sawing?orderId=X" or "minigame/sawing?difficulty=X" = navigate to mini-game
        if (route.StartsWith("minigame/"))
        {
            var routePart = route;
            var orderId = "";
            var queryIndex = route.IndexOf('?');
            if (queryIndex >= 0)
            {
                routePart = route[..queryIndex];
                var queryString = route[(queryIndex + 1)..];
                foreach (var param in queryString.Split('&'))
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2 && parts[0] == "orderId")
                        orderId = parts[1];
                }
            }

            // If orderId not in query, get from active order (e.g. difficulty-only route from OrderVM)
            if (string.IsNullOrEmpty(orderId))
                orderId = _gameStateService.GetActiveOrder()?.Id ?? "";

            DeactivateAllTabs();
            NavigateToMiniGame(routePart, orderId);
            NotifyTabBarVisibility();
            return;
        }

        // "workers" = navigiere zum Arbeitermarkt (Bug 2: von WorkshopView aus)
        if (route == "workers")
        {
            SelectWorkerMarketTab();
            return;
        }

        // "worker?id=X" = Worker-Profile als Bottom-Sheet Overlay (ohne Tabs zu deaktivieren)
        if (route.StartsWith("worker?id="))
        {
            var workerId = route.Replace("worker?id=", "");
            WorkerProfileViewModel.SetWorker(workerId);
            IsWorkerProfileActive = true;
            _adService.HideBanner();
            NotifyTabBarVisibility();
            return;
        }

        // "workshop?type=X" = navigate to workshop detail
        if (route.StartsWith("workshop?type="))
        {
            var typeStr = route.Replace("workshop?type=", "");
            if (int.TryParse(typeStr, out var typeInt))
            {
                WorkshopViewModel.SetWorkshopType(typeInt);
                DeactivateAllTabs();
                IsWorkshopDetailActive = true;
                NotifyTabBarVisibility();
            }
            return;
        }
    }

    private void NavigateToMiniGame(string routePart, string orderId)
    {
        switch (routePart)
        {
            case "minigame/sawing":
                SawingGameViewModel.SetOrderId(orderId);
                IsSawingGameActive = true;
                break;
            case "minigame/pipes":
                PipePuzzleViewModel.SetOrderId(orderId);
                IsPipePuzzleActive = true;
                break;
            case "minigame/wiring":
                WiringGameViewModel.SetOrderId(orderId);
                IsWiringGameActive = true;
                break;
            case "minigame/painting":
                PaintingGameViewModel.SetOrderId(orderId);
                IsPaintingGameActive = true;
                break;
            case "minigame/rooftiling":
                RoofTilingGameViewModel.SetOrderId(orderId);
                IsRoofTilingGameActive = true;
                break;
            case "minigame/blueprint":
                BlueprintGameViewModel.SetOrderId(orderId);
                IsBlueprintGameActive = true;
                break;
            case "minigame/designpuzzle":
                DesignPuzzleGameViewModel.SetOrderId(orderId);
                IsDesignPuzzleGameActive = true;
                break;
            case "minigame/inspection":
                InspectionGameViewModel.SetOrderId(orderId);
                IsInspectionGameActive = true;
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    private void OnMoneyChanged(object? sender, MoneyChangedEventArgs e)
    {
        Money = e.NewAmount;
        // Phase 9: Smooth animierter Geld-Counter
        AnimateMoneyTo(e.NewAmount);

        // Update affordability for all workshops
        foreach (var workshop in Workshops)
        {
            workshop.CanAffordUpgrade = e.NewAmount >= workshop.UpgradeCost;
            workshop.CanAffordWorker = e.NewAmount >= workshop.HireWorkerCost;
        }
    }

    private void OnGoldenScrewsChanged(object? sender, GoldenScrewsChangedEventArgs e)
    {
        GoldenScrewsDisplay = e.NewAmount.ToString("N0");
    }

    // Milestone-Level mit Goldschrauben-Belohnung
    private static readonly (int level, int screws)[] _milestones =
    [
        (10, 3), (25, 5), (50, 10), (100, 20), (250, 50), (500, 100), (1000, 200)
    ];

    private void OnLevelUp(object? sender, LevelUpEventArgs e)
    {
        PlayerLevel = e.NewLevel;
        _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

        RefreshWorkshops();

        // Milestone-Bonus prüfen
        string milestoneText = "";
        foreach (var (level, screws) in _milestones)
        {
            if (e.NewLevel == level)
            {
                _gameStateService.AddGoldenScrews(screws);
                milestoneText = $"\n🔩 +{screws} {_localizationService.GetString("GoldenScrews")}";
                // Doppelte Celebration bei Milestone
                _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
                break;
            }
        }

        // Level-Up-Dialog anzeigen
        LevelUpNewLevel = e.NewLevel;
        if (e.NewlyUnlockedWorkshops.Count > 0)
        {
            var names = e.NewlyUnlockedWorkshops
                .Select(w => _localizationService.GetString(w.GetLocalizationKey()));
            LevelUpUnlockedText = string.Join(", ", names) + milestoneText;
        }
        else
        {
            LevelUpUnlockedText = milestoneText;
        }
        IsLevelUpDialogVisible = true;
        CelebrationRequested?.Invoke();

        ShowLevelUp?.Invoke(this, e);
    }

    private void OnXpGained(object? sender, XpGainedEventArgs e)
    {
        CurrentXp = e.CurrentXp;
        XpForNextLevel = e.XpForNextLevel;
        // Korrekte Formel aus GameState verwenden (berücksichtigt XP-Basis des aktuellen Levels)
        LevelProgress = _gameStateService.State.LevelProgress;
    }

    private void OnWorkshopUpgraded(object? sender, WorkshopUpgradedEventArgs e)
    {
        // Nur den betroffenen Workshop aktualisieren statt alle
        RefreshSingleWorkshop(e.WorkshopType);

        // Tutorial-Hint nach erstem Upgrade ausblenden
        if (ShowTutorialHint)
        {
            ShowTutorialHint = false;
            _gameStateService.State.HasSeenTutorialHint = true;
            _gameStateService.MarkDirty();
        }
    }

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
    {
        // Nur den betroffenen Workshop aktualisieren statt alle
        RefreshSingleWorkshop(e.WorkshopType);
    }

    private void OnOrderCompleted(object? sender, OrderCompletedEventArgs e)
    {
        HasActiveOrder = false;
        ActiveOrder = null;

        // Replenish orders if running low
        if (_gameStateService.State.AvailableOrders.Count < 2)
        {
            _orderGeneratorService.RefreshOrders();
        }

        RefreshOrders();
    }

    /// <summary>
    /// Wird vom DailyChallengeService bei jeder Fortschrittsänderung ausgelöst.
    /// Aktualisiert die Challenge-Anzeige sofort statt nur alle 5 Ticks.
    /// </summary>
    private void OnChallengeProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshChallenges);
    }

    private void OnMiniGameResultRecorded(object? sender, MiniGameResultRecordedEventArgs e)
    {
        // Flag setzen: MiniGame wurde tatsächlich gespielt (für QuickJob-Validierung)
        _quickJobMiniGamePlayed = true;
    }

    private void OnMasterToolUnlocked(object? sender, MasterToolDefinition tool)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var name = _localizationService.GetString(tool.NameKey);
            if (string.IsNullOrEmpty(name)) name = tool.Id;
            FloatingTextRequested?.Invoke($"{tool.Icon} {name}!", "MasterTool");
            CelebrationRequested?.Invoke();
            _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

            MasterToolsCollected = _gameStateService.State.CollectedMasterTools.Count;
        });
    }

    private void OnDeliveryArrived(object? sender, SupplierDelivery delivery)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateDeliveryDisplay();
            FloatingTextRequested?.Invoke(
                $"📦 {_localizationService.GetString("DeliveryArrived")}!", "Delivery");
        });
    }

    private void OnEventStarted(object? sender, GameEvent evt)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveEvent = true;
            ActiveEventIcon = evt.Icon;
            ActiveEventName = _localizationService.GetString(evt.NameKey);
            ActiveEventDescription = _localizationService.GetString(evt.DescriptionKey);
            UpdateEventTimer();

            // FloatingText-Benachrichtigung anzeigen
            FloatingTextRequested?.Invoke(
                $"{evt.Icon} {ActiveEventName}", "Event");
        });
    }

    private void OnEventEnded(object? sender, GameEvent evt)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveEvent = false;
            ActiveEventIcon = "";
            ActiveEventName = "";
            ActiveEventDescription = "";
            ActiveEventTimeRemaining = "";
        });
    }

    /// <summary>
    /// Aktualisiert Event-Timer und saisonalen Modifikator (wird im GameTick aufgerufen).
    /// </summary>
    private void UpdateEventDisplay()
    {
        var activeEvent = _eventService.ActiveEvent;
        if (activeEvent != null)
        {
            HasActiveEvent = true;
            ActiveEventIcon = activeEvent.Icon;
            ActiveEventName = _localizationService.GetString(activeEvent.NameKey);
            UpdateEventTimer();
        }
        else if (HasActiveEvent)
        {
            HasActiveEvent = false;
        }

        // Saisonaler Modifikator
        var month = DateTime.UtcNow.Month;
        SeasonalModifierText = month switch
        {
            3 or 4 or 5 => _localizationService.GetString("SeasonSpring"),
            6 or 7 or 8 => _localizationService.GetString("SeasonSummer"),
            9 or 10 or 11 => _localizationService.GetString("SeasonAutumn"),
            _ => _localizationService.GetString("SeasonWinter")
        };
    }

    private void UpdateEventTimer()
    {
        var activeEvent = _eventService.ActiveEvent;
        if (activeEvent == null) return;

        var remaining = activeEvent.RemainingTime;
        ActiveEventTimeRemaining = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
            : $"{remaining.Minutes}m {remaining.Seconds:D2}s";
    }

    private void OnStateLoaded(object? sender, EventArgs e)
    {
        _achievementService.Reset();
        RefreshFromState();
    }

    private void OnAchievementUnlocked(object? sender, Achievement achievement)
    {
        _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

        var title = _localizationService.GetString(achievement.TitleKey);
        AchievementName = string.IsNullOrEmpty(title) ? achievement.TitleFallback : title;
        var desc = _localizationService.GetString(achievement.DescriptionKey);
        AchievementDescription = string.IsNullOrEmpty(desc) ? achievement.DescriptionFallback : desc;
        IsAchievementDialogVisible = true;
        CelebrationRequested?.Invoke();

        ShowAchievementUnlocked?.Invoke(this, achievement);
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ShowAds));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Alle lokalisierten Display-Texte aktualisieren
        RefreshQuickJobs();
        RefreshChallenges();
        RefreshWorkshops();

        // Child-VMs aktualisieren
        WorkerMarketViewModel.UpdateLocalizedTexts();
        WorkerProfileViewModel.UpdateLocalizedTexts();
        BuildingsViewModel.UpdateLocalizedTexts();
        ResearchViewModel.UpdateLocalizedTexts();
        ShopViewModel.LoadTools();
    }

    private void OnGameTick(object? sender, GameTickEventArgs e)
    {
        // Nur updaten wenn sich der Wert geaendert hat (vermeidet unnoetige UI-Updates)
        var newIncome = _gameStateService.State.NetIncomePerSecond;
        if (newIncome != IncomePerSecond)
        {
            IncomePerSecond = newIncome;
            IncomeDisplay = $"{FormatMoney(IncomePerSecond)}/s";
        }

        // FloatingText: Nur alle 3 Ticks anzeigen, nur wenn Income > 0 und Dashboard aktiv
        _floatingTextCounter++;
        if (_floatingTextCounter % 3 == 0 && newIncome > 0 && IsDashboardActive)
        {
            FloatingTextRequested?.Invoke($"+{newIncome:N0}\u20AC", "money");
        }

        // QuickJob-Timer aktualisieren + Rotation wenn abgelaufen
        if (_quickJobService.NeedsRotation())
        {
            _quickJobService.RotateIfNeeded();
            RefreshQuickJobs();
        }
        var remaining = _quickJobService.TimeUntilNextRotation;
        QuickJobTimerDisplay = remaining.TotalMinutes >= 1
            ? $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}"
            : $"0:{remaining.Seconds:D2}";

        // Forschungs-Timer aktualisieren (laeuft im Hintergrund weiter)
        if (ResearchViewModel.HasActiveResearch)
        {
            ResearchViewModel.UpdateTimer();
        }

        // Rush-Timer aktualisieren
        if (IsRushActive || CanActivateRush != !_gameStateService.State.IsRushBoostActive)
        {
            UpdateRushDisplay();
        }

        // Lieferant-Anzeige aktualisieren
        if (_floatingTextCounter % 3 == 0)
        {
            UpdateDeliveryDisplay();
        }

        // Event-Anzeige aktualisieren (Timer + saisonaler Modifikator)
        if (_floatingTextCounter % 5 == 0)
        {
            UpdateEventDisplay();

            // DailyChallenge-Fortschritt aktualisieren (Service trackt intern, UI muss refreshen)
            RefreshChallenges();
        }
        else if (HasActiveEvent)
        {
            UpdateEventTimer();
        }

        // Arbeitsmarkt Rotations-Timer jede Sekunde aktualisieren
        if (IsWorkerMarketActive)
        {
            WorkerMarketViewModel.UpdateTimer();
        }

        // WorkerProfile-Fortschritt aktualisieren (Training/Rest-Balken in Echtzeit)
        if (IsWorkerProfileActive && _floatingTextCounter % 3 == 0)
        {
            WorkerProfileViewModel.RefreshDisplayProperties();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static string FormatMoney(decimal amount) => MoneyFormatter.FormatCompact(amount);

    /// <summary>
    /// Animierter Geld-Counter: Setzt neuen Zielwert und startet Interpolation.
    /// Die angezeigte Zahl "tickt" smooth von alt auf neu (Phase 9).
    /// </summary>
    private void AnimateMoneyTo(decimal target)
    {
        _targetMoney = target;

        // Kleiner Unterschied → direkt setzen (kein sichtbarer Tick)
        if (Math.Abs(_targetMoney - _displayedMoney) < 1m)
        {
            _displayedMoney = _targetMoney;
            MoneyDisplay = FormatMoney(_displayedMoney);
            return;
        }

        // Timer starten falls noch nicht laeuft
        if (_moneyAnimTimer == null)
        {
            _moneyAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(MoneyAnimIntervalMs) };
            _moneyAnimTimer.Tick += OnMoneyAnimTick;
        }

        if (!_moneyAnimTimer.IsEnabled)
            _moneyAnimTimer.Start();
    }

    private void OnMoneyAnimTick(object? sender, EventArgs e)
    {
        var diff = _targetMoney - _displayedMoney;

        if (Math.Abs(diff) < 1m)
        {
            // Ziel erreicht → stoppen
            _displayedMoney = _targetMoney;
            MoneyDisplay = FormatMoney(_displayedMoney);
            _moneyAnimTimer?.Stop();
            return;
        }

        // Exponentielles Easing: schnell am Anfang, langsamer am Ende
        _displayedMoney += diff * MoneyAnimSpeed;
        MoneyDisplay = FormatMoney(_displayedMoney);
    }

    private static Material.Icons.MaterialIconKind GetWorkshopIconKind(WorkshopType type, int level = 1) => type switch
    {
        WorkshopType.Carpenter when level >= 26 => Material.Icons.MaterialIconKind.Factory,
        WorkshopType.Carpenter when level >= 11 => Material.Icons.MaterialIconKind.TableFurniture,
        WorkshopType.Carpenter => Material.Icons.MaterialIconKind.HandSaw,
        WorkshopType.Plumber when level >= 26 => Material.Icons.MaterialIconKind.WaterPump,
        WorkshopType.Plumber when level >= 11 => Material.Icons.MaterialIconKind.Pipe,
        WorkshopType.Plumber => Material.Icons.MaterialIconKind.Pipe,
        WorkshopType.Electrician when level >= 26 => Material.Icons.MaterialIconKind.TransmissionTower,
        WorkshopType.Electrician when level >= 11 => Material.Icons.MaterialIconKind.LightningBolt,
        WorkshopType.Electrician => Material.Icons.MaterialIconKind.Flash,
        WorkshopType.Painter when level >= 26 => Material.Icons.MaterialIconKind.Draw,
        WorkshopType.Painter when level >= 11 => Material.Icons.MaterialIconKind.SprayBottle,
        WorkshopType.Painter => Material.Icons.MaterialIconKind.Palette,
        WorkshopType.Roofer when level >= 26 => Material.Icons.MaterialIconKind.HomeGroup,
        WorkshopType.Roofer when level >= 11 => Material.Icons.MaterialIconKind.HomeRoof,
        WorkshopType.Roofer => Material.Icons.MaterialIconKind.HomeRoof,
        WorkshopType.Contractor when level >= 26 => Material.Icons.MaterialIconKind.DomainPlus,
        WorkshopType.Contractor when level >= 11 => Material.Icons.MaterialIconKind.OfficeBuilding,
        WorkshopType.Contractor => Material.Icons.MaterialIconKind.OfficeBuildingOutline,
        WorkshopType.Architect => Material.Icons.MaterialIconKind.Compass,
        WorkshopType.GeneralContractor => Material.Icons.MaterialIconKind.HardHat,
        _ => Material.Icons.MaterialIconKind.Wrench
    };

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSAL
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Pauses the game loop (e.g., when app is backgrounded).
    /// </summary>
    public void PauseGameLoop()
    {
        if (_gameLoopService.IsRunning)
            _gameLoopService.Pause();
    }

    /// <summary>
    /// Resumes the game loop (e.g., when app is foregrounded).
    /// </summary>
    public void ResumeGameLoop()
    {
        if (!_gameLoopService.IsRunning)
            _gameLoopService.Resume();
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Phase 9: Money-Animation Timer stoppen
        _moneyAnimTimer?.Stop();

        // Stop the game loop and save
        _gameLoopService.Stop();

        // Unsubscribe child VM navigation events
        ShopViewModel.NavigationRequested -= OnChildNavigation;
        StatisticsViewModel.NavigationRequested -= OnChildNavigation;
        AchievementsViewModel.NavigationRequested -= OnChildNavigation;
        SettingsViewModel.NavigationRequested -= OnChildNavigation;
        WorkshopViewModel.NavigationRequested -= OnChildNavigation;
        OrderViewModel.NavigationRequested -= OnChildNavigation;
        SawingGameViewModel.NavigationRequested -= OnChildNavigation;
        PipePuzzleViewModel.NavigationRequested -= OnChildNavigation;
        WiringGameViewModel.NavigationRequested -= OnChildNavigation;
        PaintingGameViewModel.NavigationRequested -= OnChildNavigation;
        RoofTilingGameViewModel.NavigationRequested -= OnChildNavigation;
        BlueprintGameViewModel.NavigationRequested -= OnChildNavigation;
        DesignPuzzleGameViewModel.NavigationRequested -= OnChildNavigation;
        InspectionGameViewModel.NavigationRequested -= OnChildNavigation;
        WorkerMarketViewModel.NavigationRequested -= _workerMarketNavHandler;
        WorkerProfileViewModel.NavigationRequested -= _workerProfileNavHandler;
        BuildingsViewModel.NavigationRequested -= _buildingsNavHandler;
        ResearchViewModel.NavigationRequested -= _researchNavHandler;

        // Unsubscribe child VM alert/confirmation events
        SettingsViewModel.AlertRequested -= _alertHandler;
        SettingsViewModel.ConfirmationRequested -= _confirmHandler;
        ShopViewModel.AlertRequested -= _alertHandler;
        ShopViewModel.ConfirmationRequested -= _confirmHandler;
        OrderViewModel.ConfirmationRequested -= _confirmHandler;
        StatisticsViewModel.AlertRequested -= _alertHandler;
        WorkerMarketViewModel.AlertRequested -= _alertHandler;
        WorkerProfileViewModel.AlertRequested -= _alertHandler;
        WorkerProfileViewModel.ConfirmationRequested -= _confirmHandler;
        BuildingsViewModel.AlertRequested -= _alertHandler;
        ResearchViewModel.AlertRequested -= _alertHandler;
        ResearchViewModel.ConfirmationRequested -= _confirmHandler;

        _gameStateService.MoneyChanged -= OnMoneyChanged;
        _gameStateService.GoldenScrewsChanged -= OnGoldenScrewsChanged;
        _gameStateService.LevelUp -= OnLevelUp;
        _gameStateService.XpGained -= OnXpGained;
        _gameStateService.WorkshopUpgraded -= OnWorkshopUpgraded;
        _gameStateService.WorkerHired -= OnWorkerHired;
        _gameStateService.OrderCompleted -= OnOrderCompleted;
        _gameStateService.StateLoaded -= OnStateLoaded;
        _gameStateService.MiniGameResultRecorded -= OnMiniGameResultRecorded;
        _gameLoopService.OnTick -= OnGameTick;
        _gameLoopService.MasterToolUnlocked -= OnMasterToolUnlocked;
        _gameLoopService.DeliveryArrived -= OnDeliveryArrived;
        _achievementService.AchievementUnlocked -= OnAchievementUnlocked;
        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _eventService.EventStarted -= OnEventStarted;
        _eventService.EventEnded -= OnEventEnded;
        _dailyChallengeService.ChallengeProgressChanged -= OnChallengeProgressChanged;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Display model for workshops in the UI.
/// </summary>
public partial class WorkshopDisplayModel : ObservableObject
{
    public WorkshopType Type { get; set; }
    public string Icon { get; set; } = "";
    public Material.Icons.MaterialIconKind IconKind { get; set; } = Material.Icons.MaterialIconKind.Wrench;
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int WorkerCount { get; set; }
    public int MaxWorkers { get; set; }
    public decimal IncomePerSecond { get; set; }
    public decimal UpgradeCost { get; set; }
    public decimal HireWorkerCost { get; set; }
    public bool IsUnlocked { get; set; }
    public int UnlockLevel { get; set; }
    public bool CanUpgrade { get; set; }
    public bool CanHireWorker { get; set; }

    [ObservableProperty]
    private bool _canAffordUpgrade;

    [ObservableProperty]
    private bool _canAffordWorker;

    public int RequiredPrestige { get; set; }
    public decimal UnlockCost { get; set; }
    public string UnlockDisplay { get; set; } = "";
    public string UnlockCostDisplay => MoneyFormatter.FormatCompact(UnlockCost);
    /// <summary>
    /// Ob das Level für die Freischaltung erreicht ist (aber noch nicht gekauft).
    /// </summary>
    public bool CanBuyUnlock { get; set; }
    public string WorkerDisplay => $"👷×{WorkerCount}";
    public string IncomeDisplay => IncomePerSecond > 0 ? MoneyFormatter.FormatPerSecond(IncomePerSecond, 1) : "-";
    public string UpgradeCostDisplay => MoneyFormatter.FormatCompact(UpgradeCost);
    public string HireCostDisplay => MoneyFormatter.FormatCompact(HireWorkerCost);
    public double LevelProgress => Level / (double)Workshop.MaxLevel;

    // Level-basierte Farb-Intensitaet fuer Workshop-Streifen
    public double ColorIntensity => Level switch
    {
        >= 1000 => 1.00, // Max Level → voll leuchtend
        >= 500 => 0.85,
        >= 250 => 0.70,
        >= 100 => 0.55,
        >= 50 => 0.45,
        >= 25 => 0.35,
        _ => 0.20       // Basis
    };

    // Max Level Gold-Glow
    public bool IsMaxLevel => Level >= Workshop.MaxLevel;
    public string MaxLevelGlow => IsMaxLevel ? "0 0 12 0 #60FFD700" : "none";

    // Phase 12.2: "Fast geschafft" Puls wenn >= 80% des Upgrade-Preises vorhanden
    public bool IsAlmostAffordable => !CanAffordUpgrade && IsUnlocked && UpgradeCost > 0;

    /// <summary>
    /// Benachrichtigt die UI ueber alle Property-Aenderungen nach einem In-Place-Update.
    /// </summary>
    public void NotifyAllChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(IconKind));
        OnPropertyChanged(nameof(WorkerCount));
        OnPropertyChanged(nameof(MaxWorkers));
        OnPropertyChanged(nameof(IncomePerSecond));
        OnPropertyChanged(nameof(UpgradeCost));
        OnPropertyChanged(nameof(HireWorkerCost));
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(UnlockDisplay));
        OnPropertyChanged(nameof(UnlockCost));
        OnPropertyChanged(nameof(UnlockCostDisplay));
        OnPropertyChanged(nameof(CanBuyUnlock));
        OnPropertyChanged(nameof(CanUpgrade));
        OnPropertyChanged(nameof(CanHireWorker));
        OnPropertyChanged(nameof(WorkerDisplay));
        OnPropertyChanged(nameof(IncomeDisplay));
        OnPropertyChanged(nameof(UpgradeCostDisplay));
        OnPropertyChanged(nameof(HireCostDisplay));
        OnPropertyChanged(nameof(LevelProgress));
        OnPropertyChanged(nameof(ColorIntensity));
        OnPropertyChanged(nameof(IsMaxLevel));
        OnPropertyChanged(nameof(MaxLevelGlow));
        OnPropertyChanged(nameof(IsAlmostAffordable));
    }
}
