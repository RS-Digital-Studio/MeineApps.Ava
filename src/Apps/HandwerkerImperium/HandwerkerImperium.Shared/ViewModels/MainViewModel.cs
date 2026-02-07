using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
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
    private bool _disposed;
    private decimal _pendingOfflineEarnings;

    // Statisches Array vermeidet Allokation bei jedem RefreshWorkshops()-Aufruf
    private static readonly WorkshopType[] _workshopTypes = Enum.GetValues<WorkshopType>();

    // EventHandler wrappers for new VMs (EventHandler<string> vs Action<string>)
    private readonly EventHandler<string> _workerMarketNavHandler;
    private readonly EventHandler<string> _workerProfileNavHandler;
    private readonly EventHandler<string> _buildingsNavHandler;
    private readonly EventHandler<string> _researchNavHandler;

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

    /// <summary>
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !_purchaseService.IsPremium;

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
                                    !IsWorkerMarketActive && !IsWorkerProfileActive &&
                                    !IsBuildingsActive && !IsResearchActive;

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

        // Subscribe to premium status changes
        _purchaseService.PremiumStatusChanged += OnPremiumStatusChanged;

        // Subscribe to achievement events
        _achievementService.AchievementUnlocked += OnAchievementUnlocked;

        // Subscribe to events
        _gameStateService.MoneyChanged += OnMoneyChanged;
        _gameStateService.LevelUp += OnLevelUp;
        _gameStateService.XpGained += OnXpGained;
        _gameStateService.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameStateService.WorkerHired += OnWorkerHired;
        _gameStateService.OrderCompleted += OnOrderCompleted;
        _gameStateService.StateLoaded += OnStateLoaded;
        _gameLoopService.OnTick += OnGameTick;
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
        MoneyDisplay = FormatMoney(state.Money);
        IncomePerSecond = state.NetIncomePerSecond;
        IncomeDisplay = $"{FormatMoney(state.NetIncomePerSecond)}/s";
        PlayerLevel = state.PlayerLevel;
        CurrentXp = state.CurrentXp;
        XpForNextLevel = state.XpForNextLevel;
        LevelProgress = state.LevelProgress;

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
        Workshops.Clear();

        // Add all workshop types (locked and unlocked)
        foreach (WorkshopType type in Enum.GetValues<WorkshopType>())
        {
            var workshop = state.Workshops.FirstOrDefault(w => w.Type == type);
            bool isUnlocked = state.IsWorkshopUnlocked(type);

            Workshops.Add(new WorkshopDisplayModel
            {
                Type = type,
                Icon = type.GetIcon(),
                IconKind = GetWorkshopIconKind(type),
                Name = _localizationService.GetString(type.GetLocalizationKey()),
                Level = workshop?.Level ?? 1,
                WorkerCount = workshop?.Workers.Count ?? 0,
                MaxWorkers = workshop?.MaxWorkers ?? 1,
                IncomePerSecond = workshop?.IncomePerSecond ?? 0,
                UpgradeCost = workshop?.UpgradeCost ?? 100,
                HireWorkerCost = workshop?.HireWorkerCost ?? 50,
                IsUnlocked = isUnlocked,
                UnlockLevel = type.GetUnlockLevel(),
                CanUpgrade = workshop?.CanUpgrade ?? true,
                CanHireWorker = workshop?.CanHireWorker ?? false,
                CanAffordUpgrade = state.Money >= (workshop?.UpgradeCost ?? 100),
                CanAffordWorker = state.Money >= (workshop?.HireWorkerCost ?? 50)
            });
        }
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
        MoneyDisplay = FormatMoney(e.NewAmount);

        // Update affordability for all workshops
        foreach (var workshop in Workshops)
        {
            workshop.CanAffordUpgrade = e.NewAmount >= workshop.UpgradeCost;
            workshop.CanAffordWorker = e.NewAmount >= workshop.HireWorkerCost;
        }
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

        ShowAchievementUnlocked?.Invoke(this, achievement);
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ShowAds));
    }

    private void OnGameTick(object? sender, GameTickEventArgs e)
    {
        // Update money display (already handled by MoneyChanged event)
        IncomePerSecond = _gameStateService.State.NetIncomePerSecond;
        IncomeDisplay = $"{FormatMoney(IncomePerSecond)}/s";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static string FormatMoney(decimal amount) => MoneyFormatter.FormatCompact(amount);

    private static Material.Icons.MaterialIconKind GetWorkshopIconKind(WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => Material.Icons.MaterialIconKind.HandSaw,
        WorkshopType.Plumber => Material.Icons.MaterialIconKind.Wrench,
        WorkshopType.Electrician => Material.Icons.MaterialIconKind.Flash,
        WorkshopType.Painter => Material.Icons.MaterialIconKind.Palette,
        WorkshopType.Roofer => Material.Icons.MaterialIconKind.HomeCity,
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

        _gameStateService.MoneyChanged -= OnMoneyChanged;
        _gameStateService.LevelUp -= OnLevelUp;
        _gameStateService.XpGained -= OnXpGained;
        _gameStateService.WorkshopUpgraded -= OnWorkshopUpgraded;
        _gameStateService.WorkerHired -= OnWorkerHired;
        _gameStateService.OrderCompleted -= OnOrderCompleted;
        _gameStateService.StateLoaded -= OnStateLoaded;
        _gameLoopService.OnTick -= OnGameTick;
        _achievementService.AchievementUnlocked -= OnAchievementUnlocked;
        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;

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

    public string WorkerDisplay => $"👷×{WorkerCount}";
    public string IncomeDisplay => IncomePerSecond > 0 ? $"{IncomePerSecond:N0}€/s" : "-";
    public string UpgradeCostDisplay => $"{UpgradeCost:N0}€";
    public string HireCostDisplay => $"{HireWorkerCost:N0}€";
    public double LevelProgress => Level / 50.0;
}
