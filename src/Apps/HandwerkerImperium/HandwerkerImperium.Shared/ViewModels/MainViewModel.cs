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
    private bool _disposed;
    private decimal _pendingOfflineEarnings;
    private QuickJob? _activeQuickJob;

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
                                    !IsWorkerProfileActive && !IsBuildingsActive;

    // Zaehler fuer Seitenwechsel → Banner bei jedem 10. Aufruf
    private int _pageViewCount;
    private const int AdShowInterval = 10;

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
        IsWorkerMarketActive = false;
        IsWorkerProfileActive = false;
        IsBuildingsActive = false;
        IsResearchActive = false;

        // Seitenaufruf zaehlen → Banner bei jedem 10. Wechsel
        _pageViewCount++;
        if (_adService.AdsEnabled)
        {
            if (_pageViewCount % AdShowInterval == 0)
                _adService.ShowBanner();
            else
                _adService.HideBanner();
        }
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

        IsAdBannerVisible = _adService.BannerVisible;
        _adService.AdsStateChanged += (_, _) => IsAdBannerVisible = _adService.BannerVisible;

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
        _gameLoopService.OnTick += OnGameTick;
        _localizationService.LanguageChanged += OnLanguageChanged;
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

        // Sync language from game state to localization service
        var savedLang = _gameStateService.State.Language;
        if (!string.IsNullOrEmpty(savedLang))
        {
            _localizationService.SetLanguage(savedLang);
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
        OfflineEarningsDurationText = effectiveDuration.TotalHours >= 1
            ? $"{(int)effectiveDuration.TotalHours}h {effectiveDuration.Minutes}min"
            : $"{(int)effectiveDuration.TotalMinutes}min";
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
        CollectOfflineEarnings(true);
        IsOfflineEarningsDialogVisible = false;
        await Task.CompletedTask;
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

        // Refresh workshops
        RefreshWorkshops();

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
            CanUpgrade = workshop?.CanUpgrade ?? true,
            CanHireWorker = workshop?.CanHireWorker ?? false,
            CanAffordUpgrade = state.Money >= (workshop?.UpgradeCost ?? 100),
            CanAffordWorker = state.Money >= (workshop?.HireWorkerCost ?? 50)
        };
    }

    private static void UpdateWorkshopDisplay(WorkshopDisplayModel model, GameState state, WorkshopType type)
    {
        var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
        bool isUnlocked = state.IsWorkshopUnlocked(type);

        model.Level = workshop?.Level ?? 1;
        model.IconKind = GetWorkshopIconKind(type, model.Level);
        model.WorkerCount = workshop?.Workers.Count ?? 0;
        model.MaxWorkers = workshop?.MaxWorkers ?? 1;
        model.IncomePerSecond = workshop?.IncomePerSecond ?? 0;
        model.UpgradeCost = workshop?.UpgradeCost ?? 100;
        model.HireWorkerCost = workshop?.HireWorkerCost ?? 50;
        model.IsUnlocked = isUnlocked;
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
            // Show message about unlock level
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);
            return;
        }

        // Navigate to workshop detail page
        WorkshopViewModel.SetWorkshopType(workshop.Type);
        DeactivateAllTabs();
        IsWorkshopDetailActive = true;
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private async Task UpgradeWorkshopAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked || !workshop.CanUpgrade)
            return;

        if (_gameStateService.TryUpgradeWorkshop(workshop.Type))
        {
            await _audioService.PlaySoundAsync(GameSound.Upgrade);
            // Explizit aktualisieren (Event-Handler macht das auch, aber sicherheitshalber)
            RefreshWorkshops();
            // FloatingText fuer Level-Up Feedback
            FloatingTextRequested?.Invoke("+1 Level!", "level");
        }
    }

    [RelayCommand]
    private async Task HireWorkerAsync(WorkshopDisplayModel workshop)
    {
        if (!workshop.IsUnlocked || !workshop.CanHireWorker)
            return;

        // Ensure workshop exists in state
        _gameStateService.State.GetOrCreateWorkshop(workshop.Type);

        if (_gameStateService.TryHireWorker(workshop.Type))
        {
            await _audioService.PlaySoundAsync(GameSound.WorkerHired);
        }
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
    }

    [RelayCommand]
    private void SelectStatisticsTab()
    {
        DeactivateAllTabs();
        IsStatisticsActive = true;
        NotifyTabBarVisibility();
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

    // ═══════════════════════════════════════════════════════════════════════
    // QUICK JOB + DAILY CHALLENGE COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StartQuickJob(QuickJob? job)
    {
        if (job == null || job.IsCompleted) return;
        _activeQuickJob = job;
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

    private void RefreshQuickJobs()
    {
        QuickJobs = _quickJobService.GetAvailableJobs();
    }

    private void RefreshChallenges()
    {
        var state = _dailyChallengeService.GetState();
        DailyChallenges = state.Challenges;
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
            // QuickJob-Rueckkehr: Belohnung vergeben
            if (_activeQuickJob != null)
            {
                _activeQuickJob.IsCompleted = true;
                _gameStateService.AddMoney(_activeQuickJob.Reward);
                _gameStateService.AddXp(_activeQuickJob.XpReward);
                _gameStateService.State.TotalQuickJobsCompleted++;
                (_quickJobService as QuickJobService)?.NotifyJobCompleted(_activeQuickJob);
                (_dailyChallengeService as DailyChallengeService)?.OnQuickJobCompleted();
                _activeQuickJob = null;
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

        // "worker?id=X" = navigate to worker profile
        if (route.StartsWith("worker?id="))
        {
            var workerId = route.Replace("worker?id=", "");
            WorkerProfileViewModel.SetWorker(workerId);
            DeactivateAllTabs();
            IsWorkerProfileActive = true;
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

    private void OnLevelUp(object? sender, LevelUpEventArgs e)
    {
        PlayerLevel = e.NewLevel;
        _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

        RefreshWorkshops();

        // Show level up dialog overlay
        LevelUpNewLevel = e.NewLevel;
        if (e.NewlyUnlockedWorkshops.Count > 0)
        {
            var names = e.NewlyUnlockedWorkshops
                .Select(w => _localizationService.GetString(w.GetLocalizationKey()));
            LevelUpUnlockedText = string.Join(", ", names);
        }
        else
        {
            LevelUpUnlockedText = "";
        }
        IsLevelUpDialogVisible = true;
        CelebrationRequested?.Invoke();

        ShowLevelUp?.Invoke(this, e);
    }

    private void OnXpGained(object? sender, XpGainedEventArgs e)
    {
        CurrentXp = e.CurrentXp;
        XpForNextLevel = e.XpForNextLevel;
        LevelProgress = (double)e.CurrentXp / e.XpForNextLevel;
    }

    private void OnWorkshopUpgraded(object? sender, WorkshopUpgradedEventArgs e)
    {
        RefreshWorkshops();
    }

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
    {
        RefreshWorkshops();
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

        // QuickJob-Timer aktualisieren
        var remaining = _quickJobService.TimeUntilNextRotation;
        QuickJobTimerDisplay = remaining.TotalMinutes >= 1
            ? $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}"
            : $"0:{remaining.Seconds:D2}";

        // Forschungs-Timer aktualisieren (laeuft im Hintergrund weiter)
        if (ResearchViewModel.HasActiveResearch)
        {
            ResearchViewModel.UpdateTimer();
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
        _gameLoopService.OnTick -= OnGameTick;
        _achievementService.AchievementUnlocked -= OnAchievementUnlocked;
        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;

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
    public string UnlockDisplay => RequiredPrestige > 0 ? $"Prestige {RequiredPrestige}" : $"Lvl {UnlockLevel}";
    public string WorkerDisplay => $"👷×{WorkerCount}";
    public string IncomeDisplay => IncomePerSecond > 0 ? $"{IncomePerSecond:N0}€/s" : "-";
    public string UpgradeCostDisplay => $"{UpgradeCost:N0}€";
    public string HireCostDisplay => $"{HireWorkerCost:N0}€";
    public double LevelProgress => Level / 50.0;

    // Phase 10.2: Level-basierte Farb-Intensitaet fuer Workshop-Streifen
    // Wird als ConverterParameter im AXAML verwendet
    public double ColorIntensity => Level switch
    {
        >= 50 => 0.80, // Max Level → stark leuchtend
        >= 26 => 0.60, // Premium-Icons
        >= 11 => 0.40, // Erweiterte Icons
        _ => 0.20      // Basis
    };

    // Phase 10.3: Max Level Gold-Glow
    public bool IsMaxLevel => Level >= 50;
    public string MaxLevelGlow => IsMaxLevel ? "0 0 12 0 #60FFD700" : "0 0 0 0 #00000000";

    // Phase 12.2: "Fast geschafft" Puls wenn >= 80% des Upgrade-Preises vorhanden
    public bool IsAlmostAffordable => !CanAffordUpgrade && IsUnlocked && UpgradeCost > 0;

    /// <summary>
    /// Benachrichtigt die UI ueber alle Property-Aenderungen nach einem In-Place-Update.
    /// </summary>
    public void NotifyAllChanged()
    {
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(IconKind));
        OnPropertyChanged(nameof(WorkerCount));
        OnPropertyChanged(nameof(MaxWorkers));
        OnPropertyChanged(nameof(IncomePerSecond));
        OnPropertyChanged(nameof(UpgradeCost));
        OnPropertyChanged(nameof(HireWorkerCost));
        OnPropertyChanged(nameof(IsUnlocked));
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
