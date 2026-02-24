using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Graphics;
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
    private readonly IStoryService? _storyService;
    private readonly ITutorialService? _tutorialService;
    private readonly IReviewService? _reviewService;
    private readonly IPrestigeService _prestigeService;
    private readonly INotificationService? _notificationService;
    private readonly IPlayGamesService? _playGamesService;
    private readonly IWeeklyMissionService _weeklyMissionService;
    private readonly IWelcomeBackService _welcomeBackService;
    private readonly ILuckySpinService _luckySpinService;
    private readonly IEquipmentService _equipmentService;
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
    private bool _isChallengesExpanded = false;

    [ObservableProperty]
    private bool _canClaimAllBonus;

    [ObservableProperty]
    private string _quickJobTimerDisplay = string.Empty;

    [ObservableProperty]
    private bool _isQuickJobsExpanded = true;

    [ObservableProperty]
    private string _quickJobsExpandIconKind = "ChevronUp";

    [ObservableProperty]
    private string _challengesExpandIconKind = "ChevronDown";

    // ═══════════════════════════════════════════════════════════════════════
    // REPUTATION (Task #6)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _reputationScore;

    [ObservableProperty]
    private string _reputationColor = "#808080";

    // ═══════════════════════════════════════════════════════════════════════
    // EMPTY STATES (Task #8)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _hasNoOrders;

    [ObservableProperty]
    private bool _allQuickJobsDone;

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE-BANNER (Task #14)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isPrestigeAvailable;

    [ObservableProperty]
    private string _prestigePointsPreview = "";

    // ═══════════════════════════════════════════════════════════════════════
    // BUILDINGS-ZUSAMMENFASSUNG (Task #5)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _buildingsSummary = "";

    // ═══════════════════════════════════════════════════════════════════════
    // FEATURE-BUTTON STATUS-TEXTE
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _workersStatusText = "";

    [ObservableProperty]
    private string _researchStatusText = "";

    [ObservableProperty]
    private string _managerStatusText = "";

    [ObservableProperty]
    private string _tournamentStatusText = "";

    [ObservableProperty]
    private string _seasonalEventStatusText = "";

    [ObservableProperty]
    private string _battlePassStatusText = "";

    [ObservableProperty]
    private string _craftingStatusText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // WEEKLY MISSIONS
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private List<WeeklyMission> _weeklyMissions = [];

    [ObservableProperty]
    private bool _hasWeeklyMissions;

    [ObservableProperty]
    private bool _allWeeklyMissionsCompleted;

    [ObservableProperty]
    private bool _canClaimWeeklyBonus;

    [ObservableProperty]
    private string _weeklyMissionResetDisplay = "";

    [ObservableProperty]
    private bool _isWeeklyMissionsExpanded = false;

    [ObservableProperty]
    private string _weeklyMissionsExpandIconKind = "ChevronDown";

    // ═══════════════════════════════════════════════════════════════════════
    // WELCOME BACK OFFER
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isWelcomeOfferVisible;

    [ObservableProperty]
    private string _welcomeOfferTitle = "";

    [ObservableProperty]
    private string _welcomeOfferDescription = "";

    [ObservableProperty]
    private string _welcomeOfferMoneyReward = "";

    [ObservableProperty]
    private string _welcomeOfferScrewReward = "";

    [ObservableProperty]
    private string _welcomeOfferTimerDisplay = "";

    /// <summary>
    /// Ob im Welcome-Back-Dialog auch Offline-Earnings angezeigt werden sollen.
    /// Wird gesetzt wenn sowohl ein Welcome-Angebot ALS AUCH Offline-Earnings vorliegen.
    /// </summary>
    [ObservableProperty]
    private bool _hasOfflineEarningsInWelcome;

    /// <summary>
    /// Formatierte Anzeige der Offline-Earnings im Welcome-Back-Dialog (z.B. "+1.5K").
    /// </summary>
    [ObservableProperty]
    private string _combinedOfflineDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // KOMBINIERTER WELCOME-BACK-DIALOG (Offline + Welcome in einem)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isCombinedWelcomeDialogVisible;

    [ObservableProperty]
    private string _combinedOfflineEarnings = "";

    [ObservableProperty]
    private string _combinedOfferMoney = "";

    [ObservableProperty]
    private string _combinedOfferScrews = "";

    [ObservableProperty]
    private string _combinedOfflineDuration = "";

    [ObservableProperty]
    private string _combinedOfferTimer = "";

    // ═══════════════════════════════════════════════════════════════════════
    // LUCKY SPIN
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isLuckySpinVisible;

    [ObservableProperty]
    private bool _hasFreeSpin;

    // ═══════════════════════════════════════════════════════════════════════
    // STREAK-RETTUNG
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _canRescueStreak;

    [ObservableProperty]
    private string _streakRescueText = "";

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
    private bool _isLevelUpPulsing;

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

    // ═══════════════════════════════════════════════════════════════════════
    // STORY-DIALOG (Meister Hans NPC)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isStoryDialogVisible;

    [ObservableProperty]
    private string _storyTitle = "";

    [ObservableProperty]
    private string _storyText = "";

    [ObservableProperty]
    private string _storyMood = "happy";

    [ObservableProperty]
    private string _storyRewardText = "";

    [ObservableProperty]
    private string _storyChapterId = "";

    [ObservableProperty]
    private bool _hasNewStory;

    [ObservableProperty]
    private int _storyChapterNumber;

    [ObservableProperty]
    private int _storyTotalChapters = 25;

    [ObservableProperty]
    private bool _isStoryTutorial;

    [ObservableProperty]
    private string _storyChapterBadge = "";

    // ═══════════════════════════════════════════════════════════════════════
    // TUTORIAL OVERLAY (interaktives Tutorial)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isTutorialVisible;

    [ObservableProperty]
    private string _tutorialTitle = "";

    [ObservableProperty]
    private string _tutorialDescription = "";

    [ObservableProperty]
    private string _tutorialIcon = "";

    [ObservableProperty]
    private string _tutorialStepDisplay = "";

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

    // Full-Screen Reward-Zeremonie (nur große Meilensteine)
    public event Action<CeremonyType, string, string>? CeremonyRequested;

    /// <summary>Wird ausgelöst um einen Exit-Hinweis anzuzeigen (z.B. Toast "Nochmal drücken zum Beenden").</summary>
    public event Action<string>? ExitHintRequested;

    // Navigation button texts
    public string NavHomeText => _localizationService.GetString("Home") ?? "Home";
    public string NavStatsText => _localizationService.GetString("Stats") ?? "Stats";
    public string NavShopText => _localizationService.GetString("Shop") ?? "Shop";
    public string NavSettingsText => _localizationService.GetString("Settings") ?? "Settings";

    // ═══════════════════════════════════════════════════════════════════════
    // AUTOMATION (Forwarding zu GameState.Automation)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Lieferungen automatisch einsammeln (ab Level 15).</summary>
    public bool AutoCollectDelivery
    {
        get => _gameStateService.State.Automation.AutoCollectDelivery;
        set
        {
            if (_gameStateService.State.Automation.AutoCollectDelivery == value) return;
            _gameStateService.State.Automation.AutoCollectDelivery = value;
            _gameStateService.MarkDirty();
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>Besten Auftrag automatisch annehmen (ab Level 25).</summary>
    public bool AutoAcceptOrder
    {
        get => _gameStateService.State.Automation.AutoAcceptOrder;
        set
        {
            if (_gameStateService.State.Automation.AutoAcceptOrder == value) return;
            _gameStateService.State.Automation.AutoAcceptOrder = value;
            _gameStateService.MarkDirty();
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>Daily Reward automatisch einlösen (nur Premium).</summary>
    public bool AutoClaimDaily
    {
        get => _gameStateService.State.Automation.AutoClaimDaily;
        set
        {
            if (_gameStateService.State.Automation.AutoClaimDaily == value) return;
            _gameStateService.State.Automation.AutoClaimDaily = value;
            _gameStateService.MarkDirty();
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    // Level-Gates für Automatisierung
    public bool IsAutoCollectUnlocked => _gameStateService.State.PlayerLevel >= 15;
    public bool IsAutoAcceptUnlocked => _gameStateService.State.PlayerLevel >= 25;
    public bool IsAutoAssignUnlocked => _gameStateService.State.PlayerLevel >= 50;
    public bool IsAutoClaimUnlocked => _purchaseService.IsPremium;

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

    [ObservableProperty]
    private bool _isManagerActive;

    [ObservableProperty]
    private bool _isTournamentActive;

    [ObservableProperty]
    private bool _isSeasonalEventActive;

    [ObservableProperty]
    private bool _isBattlePassActive;

    [ObservableProperty]
    private bool _isGuildActive;

    [ObservableProperty]
    private bool _isCraftingActive;

    [ObservableProperty]
    private bool _isForgeGameActive;

    [ObservableProperty]
    private bool _isInventGameActive;

    /// <summary>
    /// Whether the bottom tab bar should be visible (hidden during mini-games and detail views).
    /// </summary>
    public bool IsTabBarVisible => !IsWorkshopDetailActive && !IsOrderDetailActive &&
                                    !IsSawingGameActive && !IsPipePuzzleActive &&
                                    !IsWiringGameActive && !IsPaintingGameActive &&
                                    !IsRoofTilingGameActive && !IsBlueprintGameActive &&
                                    !IsDesignPuzzleGameActive && !IsInspectionGameActive &&
                                    !IsForgeGameActive && !IsInventGameActive &&
                                    !IsWorkerProfileActive && !IsWorkerMarketActive &&
                                    !IsResearchActive && !IsManagerActive &&
                                    !IsTournamentActive && !IsSeasonalEventActive &&
                                    !IsBattlePassActive && !IsCraftingActive;

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
        IsManagerActive = false;
        IsTournamentActive = false;
        IsSeasonalEventActive = false;
        IsBattlePassActive = false;
        IsGuildActive = false;
        IsCraftingActive = false;
        IsForgeGameActive = false;
        IsInventGameActive = false;
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
    public ManagerViewModel ManagerViewModel { get; }
    public TournamentViewModel TournamentViewModel { get; }
    public SeasonalEventViewModel SeasonalEventViewModel { get; }
    public BattlePassViewModel BattlePassViewModel { get; }
    public GuildViewModel GuildViewModel { get; }
    public CraftingViewModel CraftingViewModel { get; }
    public LuckySpinViewModel LuckySpinViewModel { get; }
    public ForgeGameViewModel ForgeGameViewModel { get; }
    public InventGameViewModel InventGameViewModel { get; }

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
        ResearchViewModel researchViewModel,
        ManagerViewModel managerViewModel,
        TournamentViewModel tournamentViewModel,
        SeasonalEventViewModel seasonalEventViewModel,
        BattlePassViewModel battlePassViewModel,
        GuildViewModel guildViewModel,
        CraftingViewModel craftingViewModel,
        IWeeklyMissionService weeklyMissionService,
        IWelcomeBackService welcomeBackService,
        ILuckySpinService luckySpinService,
        IEquipmentService equipmentService,
        LuckySpinViewModel luckySpinViewModel,
        ForgeGameViewModel forgeGameViewModel,
        InventGameViewModel inventGameViewModel,
        IStoryService? storyService = null)
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
        _storyService = storyService;
        _weeklyMissionService = weeklyMissionService;
        _welcomeBackService = welcomeBackService;
        _luckySpinService = luckySpinService;
        _equipmentService = equipmentService;
        _rewardedAdService.AdUnavailable += () => ShowAlertDialog(
            _localizationService.GetString("AdVideoNotAvailableTitle"),
            _localizationService.GetString("AdVideoNotAvailableMessage"),
            _localizationService.GetString("OK"));

        // SaveGame-Fehler an den Benutzer weiterleiten
        _saveGameService.ErrorOccurred += (titleKey, msgKey) =>
            Dispatcher.UIThread.Post(() => ShowAlertDialog(
                _localizationService.GetString(titleKey),
                _localizationService.GetString(msgKey),
                _localizationService.GetString("OK")));

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
        ManagerViewModel = managerViewModel;
        TournamentViewModel = tournamentViewModel;
        SeasonalEventViewModel = seasonalEventViewModel;
        BattlePassViewModel = battlePassViewModel;
        GuildViewModel = guildViewModel;
        CraftingViewModel = craftingViewModel;
        LuckySpinViewModel = luckySpinViewModel;
        ForgeGameViewModel = forgeGameViewModel;
        InventGameViewModel = inventGameViewModel;
        LuckySpinViewModel.NavigationRequested += _ => HideLuckySpin();

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
        ForgeGameViewModel.NavigationRequested += OnChildNavigation;
        InventGameViewModel.NavigationRequested += OnChildNavigation;
        ManagerViewModel.NavigationRequested += OnChildNavigation;
        TournamentViewModel.NavigationRequested += OnChildNavigation;
        SeasonalEventViewModel.NavigationRequested += OnChildNavigation;
        BattlePassViewModel.NavigationRequested += OnChildNavigation;
        GuildViewModel.NavigationRequested += OnChildNavigation;
        CraftingViewModel.NavigationRequested += OnChildNavigation;

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
        StatisticsViewModel.ShowPrestigeDialog += OnShowPrestigeDialog;
        WorkerMarketViewModel.AlertRequested += _alertHandler;
        WorkerProfileViewModel.AlertRequested += _alertHandler;
        WorkerProfileViewModel.ConfirmationRequested += _confirmHandler;
        BuildingsViewModel.AlertRequested += _alertHandler;
        ResearchViewModel.AlertRequested += _alertHandler;
        ResearchViewModel.ConfirmationRequested += _confirmHandler;
        TournamentViewModel.AlertRequested += _alertHandler;
        BattlePassViewModel.AlertRequested += _alertHandler;

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
        _weeklyMissionService.MissionProgressChanged += OnWeeklyMissionProgressChanged;
        _welcomeBackService.OfferGenerated += OnWelcomeOfferGenerated;

        // Tutorial verdrahten (optional, da ITutorialService als Singleton registriert)
        _tutorialService = App.Services?.GetService(typeof(ITutorialService)) as ITutorialService;
        if (_tutorialService != null)
        {
            _tutorialService.StepChanged += OnTutorialStep;
            _tutorialService.TutorialCompleted += OnTutorialDone;
        }

        // ReviewService + PrestigeService verdrahten
        _reviewService = App.Services?.GetService(typeof(IReviewService)) as IReviewService;
        _prestigeService = App.Services?.GetService(typeof(IPrestigeService)) as IPrestigeService
                           ?? throw new InvalidOperationException("IPrestigeService required");
        _prestigeService.PrestigeCompleted += OnPrestigeCompleted;

        // Notification + PlayGames Services
        _notificationService = App.Services?.GetService(typeof(INotificationService)) as INotificationService;
        _playGamesService = App.Services?.GetService(typeof(IPlayGamesService)) as IPlayGamesService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    public async void Initialize()
    {
        try
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

        // Cloud-Save prüfen (wenn Play Games angemeldet)
        await CheckCloudSaveAsync();

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

        // Weekly Missions initialisieren
        _weeklyMissionService.CheckAndResetIfNewWeek();
        RefreshWeeklyMissions();

        // Lucky Spin Status
        HasFreeSpin = _luckySpinService.HasFreeSpin;

        IsLoading = false;

        // Offline-Earnings berechnen (noch nicht anzeigen)
        CheckOfflineProgress();

        // Check for daily reward
        CheckDailyReward();

        // Welcome-Back-Offer prüfen und ggf. mit Offline-Earnings kombinieren
        _welcomeBackService.CheckAndGenerateOffer();
        CheckCombinedWelcomeDialog();

        // Story-Kapitel prüfen (z.B. pending aus letzter Session oder Sofort-Freischaltung)
        CheckForNewStoryChapter();

        // Tutorial starten wenn noch nicht abgeschlossen
        if (_tutorialService != null && !_tutorialService.IsCompleted)
        {
            _tutorialService.StartTutorial();
        }

        // Start the game loop for idle earnings
        _gameLoopService.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in Initialize: {ex}");
            IsLoading = false;
        }
    }

    /// <summary>
    /// Vergleicht Cloud-Spielstand mit lokalem und fragt Benutzer bei neuerem Cloud-Save.
    /// </summary>
    private async Task CheckCloudSaveAsync()
    {
        if (_playGamesService?.IsSignedIn != true || !_gameStateService.State.CloudSaveEnabled)
            return;

        try
        {
            var cloudJson = await _playGamesService.LoadCloudSaveAsync();
            if (string.IsNullOrEmpty(cloudJson)) return;

            var cloudState = System.Text.Json.JsonSerializer.Deserialize<GameState>(cloudJson);
            if (cloudState == null) return;

            // Cloud neuer als lokal?
            if (cloudState.LastSavedAt > _gameStateService.State.LastSavedAt)
            {
                var title = _localizationService.GetString("CloudSaveNewer") ?? "Cloud Save Found";
                var message = string.Format(
                    "{0} (Level {1})",
                    title, cloudState.PlayerLevel);
                var useCloud = _localizationService.GetString("UseCloudSave") ?? "Use Cloud";
                var useLocal = _localizationService.GetString("UseLocalSave") ?? "Use Local";

                var confirmed = await ShowConfirmDialog(
                    title, message, useCloud, useLocal);

                if (confirmed)
                {
                    await _saveGameService.ImportSaveAsync(cloudJson);
                    RefreshFromState();
                }
            }
        }
        catch
        {
            // Cloud-Sync-Fehler still ignorieren (lokaler Save funktioniert)
        }
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

        // Dialog wird NICHT sofort angezeigt - CheckCombinedWelcomeDialog() entscheidet
        // ob ein einzelner Offline-Dialog oder ein kombinierter Dialog gezeigt wird

        ShowOfflineEarnings?.Invoke(this, new OfflineEarningsEventArgs(
            earnings, effectiveDuration, wasCapped));
    }

    /// <summary>
    /// Prüft ob Offline-Earnings UND Welcome-Back-Offer gleichzeitig vorliegen.
    /// Wenn ja: Zeigt einen kombinierten Dialog statt zwei separate.
    /// </summary>
    private void CheckCombinedWelcomeDialog()
    {
        var hasOffline = _pendingOfflineEarnings > 0;
        var offer = _gameStateService.State.ActiveWelcomeBackOffer;
        var hasWelcome = offer != null && !offer.IsExpired;

        if (hasOffline && hasWelcome)
        {
            // Kombinierter Dialog: Offline-Earnings + Welcome-Back in einem
            CombinedOfflineEarnings = OfflineEarningsAmountText;
            CombinedOfflineDuration = OfflineEarningsDurationText;
            CombinedOfferMoney = MoneyFormatter.FormatCompact(offer!.MoneyReward);
            CombinedOfferScrews = offer.GoldenScrewReward > 0 ? $"+{offer.GoldenScrewReward}" : "";

            var remaining = offer.TimeRemaining;
            CombinedOfferTimer = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                : $"{remaining.Minutes}m";

            IsCombinedWelcomeDialogVisible = true;
        }
        else if (hasOffline)
        {
            // Nur Offline-Dialog
            IsOfflineEarningsDialogVisible = true;
        }
        else if (hasWelcome)
        {
            // Nur Welcome-Back-Dialog (wird durch OnWelcomeOfferGenerated angezeigt)
            OnWelcomeOfferGenerated();
        }
    }

    /// <summary>
    /// Sammelt alle Belohnungen aus dem kombinierten Welcome-Dialog ein (Offline + Welcome-Back).
    /// </summary>
    [RelayCommand]
    private void CollectCombinedRewards()
    {
        // Offline-Earnings einsammeln
        if (_pendingOfflineEarnings > 0)
        {
            _gameStateService.AddMoney(_pendingOfflineEarnings);
            FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}", "money");
            _pendingOfflineEarnings = 0;
        }

        // Welcome-Back-Offer einlösen
        _welcomeBackService.ClaimOffer();

        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CelebrationRequested?.Invoke();

        IsCombinedWelcomeDialogVisible = false;
    }

    /// <summary>
    /// Schließt den kombinierten Dialog und sammelt nur die Offline-Earnings ein.
    /// </summary>
    [RelayCommand]
    private void DismissCombinedDialog()
    {
        // Offline-Earnings trotzdem einsammeln (die hat der Spieler verdient)
        if (_pendingOfflineEarnings > 0)
        {
            _gameStateService.AddMoney(_pendingOfflineEarnings);
            FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}", "money");
            _pendingOfflineEarnings = 0;
        }

        // Welcome-Back-Offer ablehnen
        _welcomeBackService.DismissOffer();

        IsCombinedWelcomeDialogVisible = false;
    }

    public void CollectOfflineEarnings(bool withAdBonus)
    {
        var amount = _pendingOfflineEarnings;
        if (withAdBonus)
            amount *= 2;

        _gameStateService.AddMoney(amount);
        _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();

        // Muenz-Partikel Burst im Dashboard ausloesen
        if (amount > 0)
            FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(amount)}", "money");

        _pendingOfflineEarnings = 0;
    }

    private void CheckDailyReward()
    {
        HasDailyReward = _dailyRewardService.IsRewardAvailable;

        // Streak-Rettung prüfen: War der Streak unterbrochen und kann gerettet werden?
        var state = _gameStateService.State;
        CanRescueStreak = state.StreakBeforeBreak > 1
                          && state.DailyRewardStreak <= 1
                          && !state.StreakRescueUsed
                          && state.GoldenScrews >= 5;
        if (CanRescueStreak)
        {
            var costText = _localizationService.GetString("StreakRescueCost") ?? "Rescue streak ({0})";
            StreakRescueText = string.Format(costText, 5);
        }

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

    // ═══════════════════════════════════════════════════════════════════════
    // WEEKLY MISSIONS COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ClaimWeeklyMission(string missionId)
    {
        _weeklyMissionService.ClaimMission(missionId);
        _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
        RefreshWeeklyMissions();
    }

    [RelayCommand]
    private void ClaimAllWeeklyBonus()
    {
        _weeklyMissionService.ClaimAllCompletedBonus();
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CelebrationRequested?.Invoke();
        FloatingTextRequested?.Invoke($"+50 GS", "golden_screws");
        RefreshWeeklyMissions();
    }

    [RelayCommand]
    private void ToggleWeeklyMissionsExpanded()
    {
        IsWeeklyMissionsExpanded = !IsWeeklyMissionsExpanded;
        WeeklyMissionsExpandIconKind = IsWeeklyMissionsExpanded ? "ChevronUp" : "ChevronDown";
    }

    private void RefreshWeeklyMissions()
    {
        var state = _gameStateService.State.WeeklyMissionState;
        if (state?.Missions == null || state.Missions.Count == 0)
        {
            HasWeeklyMissions = false;
            return;
        }

        // Display-Properties befüllen (Lokalisierung + Formatierung)
        foreach (var mission in state.Missions)
        {
            // Lokalisierte Beschreibung mit TargetValue
            var descKey = $"WeeklyMission_{mission.Type}";
            var descTemplate = _localizationService.GetString(descKey) ?? mission.Type.ToString();
            mission.DisplayDescription = mission.Type == WeeklyMissionType.EarnMoney
                ? string.Format(descTemplate, MoneyFormatter.FormatCompact(mission.TargetValue))
                : string.Format(descTemplate, mission.TargetValue);

            // Belohnungs-Anzeige
            var rewardParts = new List<string>();
            if (mission.MoneyReward > 0)
                rewardParts.Add(MoneyFormatter.FormatCompact(mission.MoneyReward));
            if (mission.XpReward > 0)
                rewardParts.Add($"{mission.XpReward} XP");
            if (mission.GoldenScrewReward > 0)
                rewardParts.Add($"{mission.GoldenScrewReward} GS");
            mission.RewardDisplay = string.Join(" + ", rewardParts);

            // Fortschritts-Anzeige
            mission.ProgressDisplay = $"{mission.CurrentValue} / {mission.TargetValue}";
        }

        WeeklyMissions = new List<WeeklyMission>(state.Missions);
        HasWeeklyMissions = true;
        AllWeeklyMissionsCompleted = state.Missions.All(m => m.IsCompleted);
        CanClaimWeeklyBonus = AllWeeklyMissionsCompleted && !state.AllCompletedBonusClaimed;

        // Reset-Timer berechnen (nächster Montag)
        var now = DateTime.UtcNow;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        var resetLabel = _localizationService.GetString("WeeklyMissionReset") ?? "Resets in {0} days";
        WeeklyMissionResetDisplay = string.Format(resetLabel, daysUntilMonday);
    }

    private void OnWeeklyMissionProgressChanged()
    {
        Dispatcher.UIThread.Post(RefreshWeeklyMissions);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WELCOME BACK OFFER COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ClaimWelcomeOffer()
    {
        // Offline-Earnings miteinsammeln wenn im Dialog angezeigt
        if (HasOfflineEarningsInWelcome && _pendingOfflineEarnings > 0)
        {
            _gameStateService.AddMoney(_pendingOfflineEarnings);
            FloatingTextRequested?.Invoke($"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}", "money");
            _pendingOfflineEarnings = 0;
        }

        _welcomeBackService.ClaimOffer();
        HasOfflineEarningsInWelcome = false;
        CombinedOfflineDisplay = "";
        IsWelcomeOfferVisible = false;
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CelebrationRequested?.Invoke();
    }

    [RelayCommand]
    private void DismissWelcomeOffer()
    {
        _welcomeBackService.DismissOffer();
        IsWelcomeOfferVisible = false;
    }

    private void OnWelcomeOfferGenerated()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var offer = _gameStateService.State.ActiveWelcomeBackOffer;
            if (offer == null || offer.IsExpired) return;

            WelcomeOfferTitle = _localizationService.GetString("WelcomeBackTitle") ?? "Welcome Back!";
            WelcomeOfferDescription = offer.Type switch
            {
                WelcomeBackOfferType.Premium => _localizationService.GetString("WelcomeBackPremium") ?? "Premium welcome package!",
                WelcomeBackOfferType.StarterPack => _localizationService.GetString("StarterPackTitle") ?? "Starter pack bonus!",
                _ => _localizationService.GetString("WelcomeBackStandard") ?? "We missed you!"
            };
            WelcomeOfferMoneyReward = MoneyFormatter.FormatCompact(offer.MoneyReward);
            WelcomeOfferScrewReward = offer.GoldenScrewReward > 0 ? $"+{offer.GoldenScrewReward}" : "";

            var remaining = offer.TimeRemaining;
            WelcomeOfferTimerDisplay = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m"
                : $"{remaining.Minutes}m";

            // Wenn Offline-Earnings vorhanden → im Welcome-Dialog mit anzeigen
            HasOfflineEarningsInWelcome = _pendingOfflineEarnings > 0;
            CombinedOfflineDisplay = HasOfflineEarningsInWelcome
                ? $"+{MoneyFormatter.FormatCompact(_pendingOfflineEarnings)}"
                : "";

            IsWelcomeOfferVisible = true;
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LUCKY SPIN COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShowLuckySpin()
    {
        LuckySpinViewModel.Refresh();
        LuckySpinViewModel.StartCountdownTimer();
        IsLuckySpinVisible = true;
        _adService.HideBanner();
    }

    [RelayCommand]
    private void HideLuckySpin()
    {
        LuckySpinViewModel.StopCountdownTimer();
        IsLuckySpinVisible = false;
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STREAK-RETTUNG COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void RescueStreak()
    {
        var state = _gameStateService.State;
        if (state.GoldenScrews < 5) return;

        _gameStateService.AddGoldenScrews(-5);
        state.DailyRewardStreak = Math.Max(1, state.StreakBeforeBreak);
        state.StreakRescueUsed = true;
        _gameStateService.MarkDirty();

        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        CanRescueStreak = false;

        var rescuedMsg = _localizationService.GetString("StreakRescued") ?? "Streak rescued!";
        FloatingTextRequested?.Invoke(rescuedMsg, "golden_screws");
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

    // ═══════════════════════════════════════════════════════════════════════
    // STORY-DIALOG COMMANDS (Meister Hans NPC)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void DismissStoryDialog()
    {
        if (!string.IsNullOrEmpty(StoryChapterId))
        {
            // Belohnungen werden im StoryService.MarkChapterViewed() vergeben
            _storyService?.MarkChapterViewed(StoryChapterId);
            HasNewStory = false;
        }
        IsStoryDialogVisible = false;

        // FloatingText für Belohnungen
        if (!string.IsNullOrEmpty(StoryRewardText))
        {
            FloatingTextRequested?.Invoke(StoryRewardText, "golden_screws");
        }
    }

    /// <summary>
    /// Prüft ob ein neues Story-Kapitel freigeschaltet wurde.
    /// Wird nach Level-Up, Workshop-Upgrade und Auftragsabschluss aufgerufen.
    /// </summary>
    private void CheckForNewStoryChapter()
    {
        var chapter = _storyService?.CheckForNewChapter();
        if (chapter != null)
        {
            HasNewStory = true;
            // Dialog wird erst beim nächsten passenden Moment angezeigt
            // (nicht sofort, damit Level-Up/Achievement-Dialoge nicht kollidieren)
            Dispatcher.UIThread.Post(() =>
            {
                // Warte kurz damit andere Dialoge zuerst angezeigt werden
                if (!IsLevelUpDialogVisible && !IsAchievementDialogVisible && !IsDailyRewardDialogVisible && !IsHoldingUpgrade)
                {
                    ShowStoryDialog(chapter);
                }
            }, DispatcherPriority.Background);
        }
    }

    private void ShowStoryDialog(StoryChapter chapter)
    {
        var title = _localizationService.GetString(chapter.TitleKey);
        var text = _localizationService.GetString(chapter.TextKey);

        StoryTitle = string.IsNullOrEmpty(title) ? chapter.TitleFallback : title;
        StoryText = string.IsNullOrEmpty(text) ? chapter.TextFallback : text;
        StoryMood = chapter.Mood;
        StoryChapterId = chapter.Id;
        StoryChapterNumber = chapter.ChapterNumber;
        StoryTotalChapters = 25;
        IsStoryTutorial = chapter.IsTutorial;
        StoryChapterBadge = chapter.IsTutorial
            ? _localizationService.GetString("StoryTipFromHans") ?? "Tipp von Meister Hans"
            : $"Kap. {chapter.ChapterNumber}/25";

        // Belohnungs-Text zusammenstellen (skalierte Geldbelohnung anzeigen)
        var rewards = new List<string>();
        if (chapter.MoneyReward > 0)
        {
            var netIncome = _gameStateService.State.NetIncomePerSecond;
            var scaledReward = Math.Max(chapter.MoneyReward, netIncome * 600);
            rewards.Add($"+{MoneyFormatter.FormatCompact(scaledReward)}");
        }
        if (chapter.GoldenScrewReward > 0)
        {
            var screwsLabel = _localizationService.GetString("GoldenScrews") ?? "Goldschrauben";
            rewards.Add($"+{chapter.GoldenScrewReward} {screwsLabel}");
        }
        if (chapter.XpReward > 0)
            rewards.Add($"+{chapter.XpReward} XP");
        StoryRewardText = string.Join("  |  ", rewards);

        IsStoryDialogVisible = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TUTORIAL COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void TutorialNext()
    {
        _tutorialService?.NextStep();
    }

    [RelayCommand]
    private void TutorialSkip()
    {
        _tutorialService?.SkipTutorial();
    }

    private void OnTutorialStep(object? sender, TutorialStep step)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TutorialTitle = _localizationService.GetString(step.TitleKey) ?? step.TitleKey;
            TutorialDescription = _localizationService.GetString(step.DescriptionKey) ?? step.DescriptionKey;
            TutorialIcon = step.Icon;
            TutorialStepDisplay = $"{(_tutorialService?.CurrentStepIndex ?? 0) + 1}/{_tutorialService?.TotalSteps ?? 0}";
            IsTutorialVisible = true;
        });
    }

    private void OnTutorialDone(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsTutorialVisible = false;
        });
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

        // Ad-Banner wieder einblenden
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();
    }

    [RelayCommand]
    private void ConfirmDialogCancel()
    {
        IsConfirmDialogVisible = false;
        _confirmDialogTcs?.TrySetResult(false);

        // Ad-Banner wieder einblenden
        if (_adService.AdsEnabled && !_purchaseService.IsPremium)
            _adService.ShowBanner();
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

        // Ad-Banner ausblenden damit es nicht den Dialog verdeckt
        _adService.HideBanner();

        return _confirmDialogTcs.Task;
    }

    /// <summary>
    /// Zeigt den Prestige-Bestätigungsdialog und führt bei Bestätigung Prestige durch.
    /// Wird sowohl vom Dashboard-Banner als auch vom Statistik-Tab aufgerufen.
    /// </summary>
    private async Task ShowPrestigeConfirmationAsync()
    {
        var state = _gameStateService.State;
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);

        if (highestTier == PrestigeTier.None)
        {
            var minLevel = PrestigeTier.Bronze.GetRequiredLevel();
            ShowAlertDialog(
                _localizationService.GetString("PrestigeNotAvailable") ?? "Prestige nicht verfügbar",
                string.Format(
                    _localizationService.GetString("PrestigeNotAvailableDesc") ?? "Du benötigst Level {0} (aktuell Level {1})",
                    minLevel, state.PlayerLevel),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Prestige-Info zusammenstellen
        var tierName = _localizationService.GetString(highestTier.GetLocalizationKey()) ?? highestTier.ToString();
        var potentialPoints = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
        int tierPoints = (int)(potentialPoints * highestTier.GetPointMultiplier());

        var keepInfo = "";
        if (highestTier.KeepsResearch())
            keepInfo += $"\n\u2713 {_localizationService.GetString("Research") ?? "Forschung"}";
        if (highestTier.KeepsMasterTools())
            keepInfo += $"\n\u2713 {_localizationService.GetString("MasterTools") ?? "Meisterwerkzeuge"}";
        if (highestTier.KeepsBuildings())
            keepInfo += $"\n\u2713 {_localizationService.GetString("Buildings") ?? "Gebäude"}";
        if (highestTier.KeepsManagers())
            keepInfo += $"\n\u2713 {_localizationService.GetString("Managers") ?? "Vorarbeiter"}";

        var message = $"{highestTier.GetIcon()} {tierName}\n"
                    + $"+{tierPoints} PP | +{highestTier.GetPermanentMultiplierBonus():P0} {_localizationService.GetString("IncomeBonus") ?? "Einkommen"}\n\n"
                    + (_localizationService.GetString("PrestigeWarning") ?? "Dein Fortschritt wird zurückgesetzt!")
                    + (keepInfo.Length > 0 ? $"\n\n{_localizationService.GetString("PrestigeKeeps") ?? "Wird behalten:"}{keepInfo}" : "");

        var confirmed = await ShowConfirmDialog(
            _localizationService.GetString("Prestige") ?? "Prestige",
            message,
            _localizationService.GetString("PrestigeConfirm") ?? "Prestige durchführen",
            _localizationService.GetString("Cancel") ?? "Abbrechen");

        if (!confirmed) return;

        var success = await _prestigeService.DoPrestige(highestTier);
        if (success)
        {
            await _audioService.PlaySoundAsync(GameSound.LevelUp);

            // UI komplett neu laden
            SelectDashboardTab();
            OnStateLoaded(this, EventArgs.Empty);

            // Celebration
            FloatingTextRequested?.Invoke($"{highestTier.GetIcon()} {tierName}!", "level");
        }
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

        // Automation-Unlock-Properties aktualisieren (Level-abhängig, wichtig nach Init + Prestige)
        OnPropertyChanged(nameof(IsAutoCollectUnlocked));
        OnPropertyChanged(nameof(IsAutoAcceptUnlocked));
        OnPropertyChanged(nameof(IsAutoAssignUnlocked));
        OnPropertyChanged(nameof(IsAutoClaimUnlocked));

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
        }
        else
        {
            // Update: Bestehende Items aktualisieren (kein Clear/Add → weniger UI-Churn)
            for (int i = 0; i < _workshopTypes.Length && i < Workshops.Count; i++)
            {
                UpdateWorkshopDisplay(Workshops[i], state, _workshopTypes[i]);
            }
        }

        // Gebäude-Zusammenfassung aktualisieren (Task #5)
        RefreshBuildingsSummary(state);

        // Feature-Button Status-Texte aktualisieren
        RefreshFeatureStatusTexts(state);

        // Reputation aktualisieren (Task #6)
        RefreshReputation(state);

        // Prestige-Banner aktualisieren (Task #14)
        RefreshPrestigeBanner(state);
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
        var model = new WorkshopDisplayModel
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
            CanAffordUnlock = _gameStateService.CanPurchaseWorkshop(type) && state.Money >= type.GetUnlockCost(),
            UnlockDisplay = type.GetRequiredPrestige() > 0
                ? $"{_localizationService.GetString("Prestige")} {type.GetRequiredPrestige()}"
                : $"Lv. {type.GetUnlockLevel()}",
            CanUpgrade = workshop?.CanUpgrade ?? true,
            CanHireWorker = workshop?.CanHireWorker ?? false,
            CanAffordUpgrade = state.Money >= (workshop?.UpgradeCost ?? 100),
            CanAffordWorker = state.Money >= (workshop?.HireWorkerCost ?? 50)
        };
        // BulkBuy-Kosten berechnen
        SetBulkUpgradeCost(model, workshop, state.Money);

        // Upgrade-Preview + Net-Income berechnen (Task #10, #13)
        SetWorkshopFinancials(model, workshop);

        return model;
    }

    /// <summary>
    /// Setzt BulkUpgradeCost und BulkUpgradeLabel basierend auf aktuellem BulkBuyAmount.
    /// </summary>
    private void SetBulkUpgradeCost(WorkshopDisplayModel model, Workshop? workshop, decimal money)
    {
        if (workshop == null || !workshop.CanUpgrade)
        {
            model.BulkUpgradeCost = 0;
            model.BulkUpgradeLabel = "";
            return;
        }

        if (BulkBuyAmount == 0) // Max
        {
            var (count, cost) = workshop.GetMaxAffordableUpgrades(money);
            model.BulkUpgradeCost = cost;
            model.BulkUpgradeLabel = count > 0 ? $"Max ({count})" : "Max";
            model.CanAffordUpgrade = count > 0;
        }
        else if (BulkBuyAmount == 1)
        {
            model.BulkUpgradeCost = workshop.UpgradeCost;
            model.BulkUpgradeLabel = "";
            model.CanAffordUpgrade = money >= workshop.UpgradeCost;
        }
        else
        {
            model.BulkUpgradeCost = workshop.GetBulkUpgradeCost(BulkBuyAmount);
            model.BulkUpgradeLabel = $"x{BulkBuyAmount}";
            model.CanAffordUpgrade = money >= model.BulkUpgradeCost;
        }
    }

    /// <summary>
    /// Berechnet Upgrade-Income-Preview und Netto-Einkommen für eine Workshop-Anzeige (Task #10, #13).
    /// </summary>
    private void SetWorkshopFinancials(WorkshopDisplayModel model, Workshop? workshop)
    {
        if (workshop == null || !model.IsUnlocked)
        {
            model.UpgradeIncomePreview = "";
            model.NetIncomeDisplay = "";
            model.IsNetNegative = false;
            model.HasCosts = false;
            return;
        }

        // Netto-Einkommen (Brutto - Kosten)
        var netIncome = workshop.NetIncomePerSecond;
        model.NetIncomeDisplay = MoneyFormatter.FormatPerSecond(netIncome, 1);
        model.IsNetNegative = netIncome < 0;
        model.HasCosts = workshop.TotalCostsPerHour > 0;

        // Upgrade-Preview: Einkommensdifferenz nach Bulk-Upgrade berechnen
        if (workshop.CanUpgrade && workshop.Level < Workshop.MaxLevel)
        {
            int upgradeCount = BulkBuyAmount == 0 ? 10 : BulkBuyAmount; // Max → zeige Preview für ~10 Level
            int targetLevel = Math.Min(workshop.Level + upgradeCount, Workshop.MaxLevel);
            // Einkommen bei Ziel-Level basierend auf Base-Income-Formel berechnen
            decimal currentBase = (decimal)Math.Pow(1.025, workshop.Level - 1) * workshop.Type.GetBaseIncomeMultiplier();
            decimal targetBase = (decimal)Math.Pow(1.025, targetLevel - 1) * workshop.Type.GetBaseIncomeMultiplier();
            // Differenz berücksichtigt nur die Basis (Worker-Effekte skalieren proportional)
            decimal diff = (targetBase - currentBase) * Math.Max(1, workshop.Workers.Count);
            model.UpgradeIncomePreview = diff > 0 ? $"+{MoneyFormatter.FormatPerSecond(diff, 1)}" : "";
        }
        else
        {
            model.UpgradeIncomePreview = "";
        }
    }

    /// <summary>
    /// Aktualisiert die Gebäude-Zusammenfassung (Task #5).
    /// </summary>
    private void RefreshBuildingsSummary(GameState state)
    {
        int totalBuildings = Enum.GetValues<BuildingType>().Length;
        int builtCount = state.Buildings.Count(b => b.IsBuilt);
        var builtLabel = _localizationService.GetString("Built") ?? "gebaut";
        var buildingsLabel = _localizationService.GetString("Buildings") ?? "Gebäude";
        BuildingsSummary = $"{totalBuildings} {buildingsLabel}, {builtCount} {builtLabel}";
    }

    /// <summary>
    /// Aktualisiert die Feature-Button Status-Texte.
    /// </summary>
    private void RefreshFeatureStatusTexts(GameState state)
    {
        // Arbeiter
        var totalWorkers = state.Workshops.Sum(w => w.Workers.Count);
        WorkersStatusText = string.Format(
            _localizationService.GetString("WorkersStatus") ?? "{0} angestellt",
            totalWorkers);

        // Forschung
        var completedResearch = state.Researches.Count(r => r.IsResearched);
        if (!string.IsNullOrEmpty(state.ActiveResearchId))
        {
            var researchName = _localizationService.GetString($"Research_{state.ActiveResearchId}") ?? state.ActiveResearchId;
            ResearchStatusText = string.Format(
                _localizationService.GetString("ResearchActiveStatus") ?? "Erforscht: {0}",
                researchName);
        }
        else
        {
            ResearchStatusText = string.Format(
                _localizationService.GetString("ResearchStatus") ?? "{0}/45 erforscht",
                completedResearch);
        }

        // Vorarbeiter
        var activeManagers = state.Managers.Count(m => m.IsUnlocked);
        ManagerStatusText = string.Format(
            _localizationService.GetString("ManagerStatus") ?? "{0} aktiv",
            activeManagers);

        // Turnier
        if (state.CurrentTournament != null)
        {
            var remainingEntries = state.CurrentTournament.FreeEntriesRemaining;
            TournamentStatusText = string.Format(
                _localizationService.GetString("TournamentStatus") ?? "{0} Versuche",
                remainingEntries);
        }
        else
        {
            TournamentStatusText = "";
        }

        // Saison-Event
        if (state.CurrentSeasonalEvent != null)
        {
            var seasonKey = state.CurrentSeasonalEvent.Season.ToString();
            SeasonalEventStatusText = _localizationService.GetString(seasonKey) ?? seasonKey;
        }
        else
        {
            SeasonalEventStatusText = "";
        }

        // Saison-Pass
        BattlePassStatusText = string.Format(
            _localizationService.GetString("BattlePassStatus") ?? "Tier {0}/{1}",
            state.BattlePass.CurrentTier, 30);

        // Produktion
        var activeCrafts = state.ActiveCraftingJobs.Count;
        CraftingStatusText = string.Format(
            _localizationService.GetString("CraftingStatus") ?? "{0} in Produktion",
            activeCrafts);
    }

    /// <summary>
    /// Aktualisiert Reputation-Anzeige (Task #6).
    /// </summary>
    private void RefreshReputation(GameState state)
    {
        var score = state.Reputation.ReputationScore;
        ReputationScore = score;
        ReputationColor = score switch
        {
            < 30 => "#EF4444",  // Rot
            < 60 => "#F59E0B",  // Gelb
            < 80 => "#22C55E",  // Grün
            _ => "#FFD700"      // Gold
        };
    }

    /// <summary>
    /// Aktualisiert Prestige-Banner-Anzeige (Task #14).
    /// </summary>
    private void RefreshPrestigeBanner(GameState state)
    {
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);
        IsPrestigeAvailable = highestTier != PrestigeTier.None;

        if (IsPrestigeAvailable)
        {
            var potentialPoints = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
            int tierPoints = (int)(potentialPoints * highestTier.GetPointMultiplier());
            var pointsLabel = _localizationService.GetString("PrestigePoints") ?? "Prestige-Punkte";
            PrestigePointsPreview = $"+{tierPoints} {pointsLabel}";
        }
        else
        {
            PrestigePointsPreview = "";
        }
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
        model.CanAffordUnlock = model.CanBuyUnlock && state.Money >= type.GetUnlockCost();
        model.UnlockDisplay = type.GetRequiredPrestige() > 0
            ? $"{_localizationService.GetString("Prestige")} {type.GetRequiredPrestige()}"
            : $"Lv. {type.GetUnlockLevel()}";
        model.CanUpgrade = workshop?.CanUpgrade ?? true;
        model.CanHireWorker = workshop?.CanHireWorker ?? false;
        model.CanAffordUpgrade = state.Money >= (workshop?.UpgradeCost ?? 100);
        model.CanAffordWorker = state.Money >= (workshop?.HireWorkerCost ?? 50);

        // BulkBuy-Kosten aktualisieren
        SetBulkUpgradeCost(model, workshop, state.Money);

        // Upgrade-Preview + Net-Income berechnen (Task #10, #13)
        SetWorkshopFinancials(model, workshop);

        model.NotifyAllChanged();
    }

    private void RefreshOrders()
    {
        var state = _gameStateService.State;
        AvailableOrders.Clear();

        foreach (var order in state.AvailableOrders)
        {
            // Lokalisierte Display-Felder befüllen
            var localizedTitle = _localizationService.GetString(order.TitleKey);
            order.DisplayTitle = string.IsNullOrEmpty(localizedTitle) ? order.TitleFallback : localizedTitle;
            order.DisplayWorkshopName = _localizationService.GetString(order.WorkshopType.GetLocalizationKey());

            // Auftragstyp Display-Properties (Task #3)
            order.DisplayOrderType = _localizationService.GetString(order.OrderType.GetLocalizationKey())
                                     ?? order.OrderType.ToString();
            order.OrderTypeIcon = order.OrderType.GetIcon();
            order.OrderTypeBadgeColor = order.OrderType switch
            {
                OrderType.Large => "#EA580C",
                OrderType.Weekly => "#FFD700",
                OrderType.Cooperation => "#0E7490",
                _ => ""
            };
            order.ShowOrderTypeBadge = order.OrderType != OrderType.Standard && order.OrderType != OrderType.Quick;

            AvailableOrders.Add(order);
        }

        // Empty State (Task #8)
        HasNoOrders = AvailableOrders.Count == 0;
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
                    _localizationService.GetString("OK"));
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
                                _localizationService.GetString("OK"));
                            CelebrationRequested?.Invoke();
                        }
                        else
                        {
                            ShowAlertDialog(
                                _localizationService.GetString("NotEnoughMoney"),
                                $"{_localizationService.GetString("Required")}: {halfCostDisplay}",
                                _localizationService.GetString("OK"));
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
                            _localizationService.GetString("OK"));
                        CelebrationRequested?.Invoke();
                    }
                    else
                    {
                        ShowAlertDialog(
                            _localizationService.GetString("NotEnoughMoney"),
                            $"{_localizationService.GetString("Required")}: {costDisplay}",
                            _localizationService.GetString("OK"));
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
                        _localizationService.GetString("OK"));
                    CelebrationRequested?.Invoke();
                }
                else
                {
                    ShowAlertDialog(
                        _localizationService.GetString("NotEnoughMoney"),
                        $"{_localizationService.GetString("Required")}: {costDisplay}",
                        _localizationService.GetString("OK"));
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

    /// <summary>
    /// Flag: Hold-to-Upgrade aktiv → aufpoppende Dialoge unterdrücken.
    /// </summary>
    public bool IsHoldingUpgrade { get; set; }

    /// <summary>
    /// Stilles Upgrade ohne Sound/FloatingText - für Hold-to-Upgrade.
    /// </summary>
    public bool UpgradeWorkshopSilent(WorkshopType type)
    {
        return _gameStateService.TryUpgradeWorkshop(type);
    }

    /// <summary>
    /// Spielt den Upgrade-Sound ab (für Hold-to-Upgrade Ende).
    /// </summary>
    public void PlayUpgradeSound()
    {
        _audioService.PlaySoundAsync(GameSound.Upgrade).FireAndForget();
    }

    /// <summary>
    /// Aktualisiert eine einzelne Workshop-Anzeige (öffentlicher Zugang für Code-Behind).
    /// </summary>
    public void RefreshSingleWorkshopPublic(WorkshopType type)
    {
        RefreshSingleWorkshop(type);
    }

    /// <summary>
    /// Gibt den aktuellen GameState für SkiaSharp-Rendering zurück (City-Skyline im Header).
    /// </summary>
    public GameState? GetGameStateForRendering()
    {
        return _gameStateService.State;
    }

    /// <summary>
    /// Navigation zum Workshop-Detail direkt aus der City-Szene (Tap auf Gebäude).
    /// Nur für freigeschaltete Workshops.
    /// </summary>
    public void NavigateToWorkshopFromCity(WorkshopType type)
    {
        WorkshopViewModel.SetWorkshopType(type);
        DeactivateAllTabs();
        IsWorkshopDetailActive = true;
        NotifyTabBarVisibility();
    }

    /// <summary>
    /// Gibt die lokalisierten Tab-Labels für die SkiaSharp Tab-Bar zurück.
    /// </summary>
    public string[] GetTabLabels() =>
    [
        _localizationService.GetString("Home") ?? "Home",
        _localizationService.GetString("Buildings") ?? "Gebäude",
        _localizationService.GetString("GuildTitle") ?? "Gilde",
        _localizationService.GetString("Shop") ?? "Shop",
        _localizationService.GetString("Settings") ?? "Einstellungen"
    ];

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

    /// <summary>
    /// Navigiert zur Gebäude-Detail-Ansicht.
    /// </summary>
    [RelayCommand]
    private void NavigateToBuildings()
    {
        SelectBuildingsTab();
    }

    [RelayCommand]
    private void NavigateToWorkerMarket() => OnChildNavigation("workers");

    [RelayCommand]
    private void NavigateToResearch() => OnChildNavigation("research");

    [RelayCommand]
    private void NavigateToManager() => OnChildNavigation("manager");

    [RelayCommand]
    private void NavigateToTournament() => OnChildNavigation("tournament");

    [RelayCommand]
    private void NavigateToSeasonalEvent() => OnChildNavigation("seasonal_event");

    [RelayCommand]
    private void NavigateToBattlePass() => OnChildNavigation("battle_pass");

    [RelayCommand]
    private void NavigateToGuild() => OnChildNavigation("guild");

    [RelayCommand]
    private void NavigateToCrafting() => OnChildNavigation("crafting");

    /// <summary>
    /// Navigiert zum Prestige (Statistik-Tab wo Prestige angezeigt wird).
    /// </summary>
    [RelayCommand]
    private async Task NavigateToPrestigeAsync()
    {
        await ShowPrestigeConfirmationAsync();
    }

    /// <summary>
    /// Zeigt Reputations-Info-Dialog mit Level und Multiplikator.
    /// </summary>
    [RelayCommand]
    private void ShowReputationInfo()
    {
        var rep = _gameStateService.State.Reputation;
        var level = _localizationService.GetString(rep.ReputationLevelKey)
                    ?? rep.ReputationLevelKey;
        var multiplier = rep.ReputationMultiplier;
        AlertDialogTitle = _localizationService.GetString("Reputation") ?? "Reputation";
        AlertDialogMessage = $"{level} ({rep.ReputationScore}/100)\n\u00d7{multiplier:F1} {_localizationService.GetString("IncomeBonus") ?? "Einkommensbonus"}";
        AlertDialogButtonText = "OK";
        IsAlertDialogVisible = true;
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
        // Geldpakete-Beträge aktualisieren (basieren auf aktuellem Einkommen)
        ShopViewModel.LoadShopData();
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

    [RelayCommand]
    private void SelectGuildTab()
    {
        DeactivateAllTabs();
        IsGuildActive = true;
        GuildViewModel.RefreshGuild();
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
        // 1. Offene Dialoge/Overlays schließen (höchste Priorität)
        if (IsLuckySpinVisible) { HideLuckySpin(); return true; }
        if (IsCombinedWelcomeDialogVisible) { DismissCombinedDialog(); return true; }
        if (IsWelcomeOfferVisible) { DismissWelcomeOffer(); return true; }
        if (IsConfirmDialogVisible) { ConfirmDialogCancel(); return true; }
        if (IsAlertDialogVisible) { DismissAlertDialog(); return true; }
        if (IsAchievementDialogVisible) { DismissAchievementDialog(); return true; }
        if (IsLevelUpDialogVisible) { DismissLevelUpDialog(); return true; }
        if (IsOfflineEarningsDialogVisible) { CollectOfflineEarningsNormal(); return true; }
        if (IsDailyRewardDialogVisible) { IsDailyRewardDialogVisible = false; return true; }
        if (IsStoryDialogVisible) { DismissStoryDialog(); return true; }

        // 2. MiniGame aktiv → zurück zum Dashboard
        if (IsSawingGameActive || IsPipePuzzleActive || IsWiringGameActive || IsPaintingGameActive ||
            IsRoofTilingGameActive || IsBlueprintGameActive || IsDesignPuzzleGameActive || IsInspectionGameActive ||
            IsForgeGameActive || IsInventGameActive)
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

        // 5. Sub-Views (Feature-Views, von Dashboard aus erreichbar) → zurück zum Dashboard
        if (IsWorkerMarketActive || IsResearchActive ||
            IsManagerActive || IsTournamentActive || IsSeasonalEventActive ||
            IsBattlePassActive || IsCraftingActive)
        {
            SelectDashboardTab();
            return true;
        }

        // 6. Nicht-Dashboard-Tabs → zum Dashboard
        if (IsShopActive || IsStatisticsActive || IsAchievementsActive || IsSettingsActive ||
            IsBuildingsActive || IsGuildActive)
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
            int maxDaily = _quickJobService?.MaxDailyJobs ?? 20;
            var template = _localizationService.GetString("QuickJobDailyLimit");
            var limitText = !string.IsNullOrEmpty(template)
                ? string.Format(template, maxDaily)
                : $"Tageslimit erreicht ({maxDaily}/Tag)";
            FloatingTextRequested?.Invoke(limitText, "Warning");
            return;
        }

        _activeQuickJob = job;
        _quickJobMiniGamePlayed = false;
        _gameStateService.State.ActiveQuickJob = job;
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
    private void ShowMasterToolsInfo()
    {
        var state = _gameStateService.State;
        var allTools = MasterTool.GetAllDefinitions();
        var collected = state.CollectedMasterTools;
        var totalBonus = MasterTool.GetTotalIncomeBonus(collected);

        // Info-Text zusammenbauen
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{_localizationService.GetString("IncomeBonus") ?? "Einkommensbonus"}: +{totalBonus:P0}");
        sb.AppendLine();

        foreach (var tool in allTools)
        {
            bool isCollected = collected.Contains(tool.Id);
            var name = _localizationService.GetString(tool.NameKey) ?? tool.Id;
            var rarity = _localizationService.GetString(tool.Rarity.GetLocalizationKey()) ?? tool.Rarity.ToString();
            var status = isCollected ? "\u2713" : "\u2717";
            sb.AppendLine($"{status} {tool.Icon} {name}");
            sb.AppendLine($"   [{rarity}] +{tool.IncomeBonus:P0}");

            if (!isCollected)
            {
                var condition = GetMasterToolCondition(tool.Id);
                sb.AppendLine($"   \u2192 {condition}");
            }
            sb.AppendLine();
        }

        var title = _localizationService.GetString("MasterTools") ?? "Meisterwerkzeuge";
        ShowAlertDialog(title, sb.ToString().TrimEnd(), _localizationService.GetString("OK"));
    }

    /// <summary>
    /// Gibt die lokalisierte Freischaltbedingung für ein Meisterwerkzeug zurück.
    /// </summary>
    private string GetMasterToolCondition(string toolId)
    {
        return toolId switch
        {
            "mt_golden_hammer" => $"Workshop Lv. 25",
            "mt_diamond_saw" => $"Workshop Lv. 50",
            "mt_titanium_pliers" => $"50 {_localizationService.GetString("Orders") ?? "Aufträge"}",
            "mt_brass_level" => $"100 Mini-Games",
            "mt_silver_wrench" => $"Workshop Lv. 100",
            "mt_jade_brush" => $"25 {_localizationService.GetString("PerfectRating") ?? "Perfect"}",
            "mt_crystal_chisel" => $"1x {_localizationService.GetString("PrestigeBronze") ?? "Bronze-Prestige"}",
            "mt_obsidian_drill" => $"Workshop Lv. 250",
            "mt_ruby_blade" => $"1x {_localizationService.GetString("PrestigeSilver") ?? "Silber-Prestige"}",
            "mt_emerald_toolbox" => $"Workshop Lv. 500",
            "mt_dragon_anvil" => $"1x {_localizationService.GetString("PrestigeGold") ?? "Gold-Prestige"}",
            "mt_master_crown" => $"{_localizationService.GetString("AllToolsCollected") ?? "Alle anderen Werkzeuge"}",
            _ => "?"
        };
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
                _localizationService.GetString("OK"));
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
            FloatingTextRequested?.Invoke($"Rush 2x ({RushDurationHours}h)!", "Rush");
            CelebrationRequested?.Invoke();
        }
        else if (_gameStateService.TrySpendGoldenScrews(RushCostScrews))
        {
            // Bezahlter Rush (Goldschrauben)
            state.RushBoostEndTime = DateTime.UtcNow.AddHours(RushDurationHours);
            _gameStateService.MarkDirty();
            _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
            FloatingTextRequested?.Invoke($"Rush 2x ({RushDurationHours}h)!", "Rush");
        }
        else
        {
            ShowAlertDialog(
                _localizationService.GetString("NotEnoughScrews"),
                string.Format(_localizationService.GetString("RushCostScrews"), RushCostScrews),
                _localizationService.GetString("OK"));
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
            RushButtonText = RushTimeRemaining;
        }
        else
        {
            RushTimeRemaining = "";
            CanActivateRush = true;
            RushButtonText = state.IsFreeRushAvailable
                ? _localizationService.GetString("RushFreeActivation")
                : $"Rush ({RushCostScrews} GS)";
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
                FloatingTextRequested?.Invoke($"+{(int)delivery.Amount} GS", "screw");
                break;

            case Models.Enums.DeliveryType.Experience:
                _gameStateService.AddXp((int)delivery.Amount);
                FloatingTextRequested?.Invoke($"+{(int)delivery.Amount} XP", "xp");
                break;

            case Models.Enums.DeliveryType.MoodBoost:
                foreach (var ws in state.Workshops)
                foreach (var worker in ws.Workers)
                    worker.Mood = Math.Min(100m, worker.Mood + delivery.Amount);
                FloatingTextRequested?.Invoke($"+{(int)delivery.Amount} Mood", "mood");
                break;

            case Models.Enums.DeliveryType.SpeedBoost:
                state.SpeedBoostEndTime = DateTime.UtcNow.AddMinutes((double)delivery.Amount);
                FloatingTextRequested?.Invoke($"2x ({(int)delivery.Amount}min)", "speed");
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
            Models.Enums.DeliveryType.GoldenScrews => $"{(int)delivery.Amount} GS",
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
        // Empty State für Quick Jobs (Task #8)
        AllQuickJobsDone = QuickJobs.Count == 0 || QuickJobs.All(j => j.IsCompleted);
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
                _gameStateService.State.ActiveQuickJob = null;
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

        // Neue Feature-Views (Welle 1-8)
        if (route is "manager" or "tournament" or "seasonal_event" or "battle_pass" or "guild" or "crafting")
        {
            DeactivateAllTabs();
            switch (route)
            {
                case "manager": IsManagerActive = true; ManagerViewModel.RefreshManagers(); break;
                case "tournament": IsTournamentActive = true; TournamentViewModel.RefreshTournament(); break;
                case "seasonal_event": IsSeasonalEventActive = true; SeasonalEventViewModel.RefreshEvent(); break;
                case "battle_pass": IsBattlePassActive = true; BattlePassViewModel.RefreshBattlePass(); break;
                case "guild": IsGuildActive = true; GuildViewModel.RefreshGuild(); break;
                case "crafting": IsCraftingActive = true; CraftingViewModel.RefreshCrafting(); break;
            }
            NotifyTabBarVisibility();
            return;
        }

        // "research" = navigiere zum Forschungsbaum (von GuildView aus)
        if (route == "research")
        {
            SelectResearchTab();
            return;
        }

        // "workers" = navigiere zum Arbeitermarkt (von WorkshopView/GuildView aus)
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
            case "minigame/forge":
                ForgeGameViewModel.SetOrderId(orderId);
                IsForgeGameActive = true;
                break;
            case "minigame/invent":
                InventGameViewModel.SetOrderId(orderId);
                IsInventGameActive = true;
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

        // Update affordability für alle Workshops (BulkBuyAmount berücksichtigen)
        foreach (var workshop in Workshops)
        {
            var ws = _gameStateService.State.Workshops.FirstOrDefault(w => w.Type == workshop.Type);
            SetBulkUpgradeCost(workshop, ws, e.NewAmount);
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
        OnPropertyChanged(nameof(LevelProgress));

        RefreshWorkshops();

        // Automation-Unlock-Properties aktualisieren (Level-Gates können sich ändern)
        OnPropertyChanged(nameof(IsAutoCollectUnlocked));
        OnPropertyChanged(nameof(IsAutoAcceptUnlocked));
        OnPropertyChanged(nameof(IsAutoAssignUnlocked));

        // Pulse-Animation bei JEDEM Level-Up (dezent, kein Dialog)
        IsLevelUpPulsing = true;
        var pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        pulseTimer.Tick += (_, _) =>
        {
            IsLevelUpPulsing = false;
            pulseTimer.Stop();
        };
        pulseTimer.Start();

        // Milestone-Bonus prüfen (10/25/50/100/250/500/1000)
        bool isMilestone = false;
        foreach (var (level, screws) in _milestones)
        {
            if (e.NewLevel == level)
            {
                isMilestone = true;
                _gameStateService.AddGoldenScrews(screws);

                // Sound + Celebration nur bei Milestones
                _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
                _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
                CelebrationRequested?.Invoke();
                CeremonyRequested?.Invoke(CeremonyType.LevelMilestone,
                    $"Level {e.NewLevel}!", $"+{screws} Goldschrauben");

                // FloatingText mit Level + Goldschrauben-Bonus
                FloatingTextRequested?.Invoke(
                    $"Level {e.NewLevel}! +{screws} GS", "level");
                break;
            }
        }

        // Story-Kapitel prüfen
        CheckForNewStoryChapter();

        // Review-Milestone prüfen
        _reviewService?.OnMilestone("level", e.NewLevel);
        CheckReviewPrompt();

        // Leaderboard-Score aktualisieren (fire-and-forget)
        if (_playGamesService?.IsSignedIn == true)
            _ = _playGamesService.SubmitScoreAsync("leaderboard_player_level", e.NewLevel);
    }

    private void OnPrestigeCompleted(object? sender, EventArgs e)
    {
        var prestigeCount = _gameStateService.State.Prestige.TotalPrestigeCount;
        _reviewService?.OnMilestone("prestige", prestigeCount);
        CheckReviewPrompt();
    }

    private void CheckReviewPrompt()
    {
        if (_reviewService?.ShouldPromptReview() == true)
        {
            _reviewService.MarkReviewPrompted();
            App.ReviewPromptRequested?.Invoke();
        }
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

        // Workshop-Level-Milestone prüfen (nicht während Hold-to-Upgrade)
        // Schwellen weiter auseinander damit nicht bei jedem frühen Level Benachrichtigungen kommen
        if (!IsHoldingUpgrade)
        {
            (int level, int screws)[] workshopMilestones =
                [(50, 2), (100, 5), (250, 10), (500, 25), (1000, 50)];
            foreach (var (level, screws) in workshopMilestones)
            {
                if (e.NewLevel == level)
                {
                    _gameStateService.AddGoldenScrews(screws);
                    var workshopName = _localizationService.GetString(e.WorkshopType.GetLocalizationKey());
                    FloatingTextRequested?.Invoke(
                        $"{workshopName} Lv.{e.NewLevel}! +{screws} GS", "level");
                    CelebrationRequested?.Invoke();
                    CeremonyRequested?.Invoke(CeremonyType.WorkshopMilestone,
                        $"{workshopName} Lv.{e.NewLevel}!", $"+{screws} Goldschrauben");
                    _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
                    break;
                }
            }

            // Story-Kapitel prüfen
            CheckForNewStoryChapter();
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

        // Story-Kapitel prüfen
        CheckForNewStoryChapter();

        // Review-Milestone prüfen
        _reviewService?.OnMilestone("orders", _gameStateService.State.TotalOrdersCompleted);
        CheckReviewPrompt();
    }

    /// <summary>
    /// Wird vom DailyChallengeService bei jeder Fortschrittsänderung ausgelöst.
    /// Aktualisiert die Challenge-Anzeige sofort statt nur alle 5 Ticks.
    /// </summary>
    private void OnChallengeProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshChallenges);
    }

    private async void OnShowPrestigeDialog(object? sender, EventArgs e)
    {
        await ShowPrestigeConfirmationAsync();
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
            CeremonyRequested?.Invoke(CeremonyType.MasterTool, name, $"+{(int)(tool.IncomeBonus * 100)}% Einkommen");
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
                $"{_localizationService.GetString("DeliveryArrived")}!", "Delivery");
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
        // Während Hold-to-Upgrade keine Dialoge anzeigen
        if (IsHoldingUpgrade) return;

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
        ShopViewModel.LoadShopData();
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

            // Reputation + Prestige-Banner periodisch aktualisieren (Task #6, #14)
            var state = _gameStateService.State;
            RefreshReputation(state);
            RefreshPrestigeBanner(state);
        }
        else if (HasActiveEvent)
        {
            UpdateEventTimer();
        }

        // Weekly Missions + Lucky Spin + Welcome Back periodisch aktualisieren (alle 10 Ticks)
        if (_floatingTextCounter % 10 == 0)
        {
            HasFreeSpin = _luckySpinService.HasFreeSpin;

            // Welcome Back Timer aktualisieren
            if (IsWelcomeOfferVisible)
            {
                var offer = _gameStateService.State.ActiveWelcomeBackOffer;
                if (offer == null || offer.IsExpired)
                {
                    IsWelcomeOfferVisible = false;
                }
                else
                {
                    var offerRemaining = offer.TimeRemaining;
                    WelcomeOfferTimerDisplay = offerRemaining.TotalHours >= 1
                        ? $"{(int)offerRemaining.TotalHours}h {offerRemaining.Minutes:D2}m"
                        : $"{offerRemaining.Minutes}m";
                }
            }
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

        // Benachrichtigungen planen wenn aktiviert
        if (_gameStateService.State.NotificationsEnabled)
            _notificationService?.ScheduleGameNotifications(_gameStateService.State);
    }

    /// <summary>
    /// Resumes the game loop (e.g., when app is foregrounded).
    /// </summary>
    public void ResumeGameLoop()
    {
        // Geplante Benachrichtigungen stornieren
        _notificationService?.CancelAllNotifications();

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
        StatisticsViewModel.ShowPrestigeDialog -= OnShowPrestigeDialog;
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
        _weeklyMissionService.MissionProgressChanged -= OnWeeklyMissionProgressChanged;
        _welcomeBackService.OfferGenerated -= OnWelcomeOfferGenerated;

        if (_tutorialService != null)
        {
            _tutorialService.StepChanged -= OnTutorialStep;
            _tutorialService.TutorialCompleted -= OnTutorialDone;
        }

        _prestigeService.PrestigeCompleted -= OnPrestigeCompleted;

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
    /// <summary>
    /// Ob genug Geld für die Freischaltung vorhanden ist.
    /// </summary>
    [ObservableProperty]
    private bool _canAffordUnlock;
    /// <summary>
    /// Bulk-Buy Gesamtkosten (gesetzt von RefreshWorkshops basierend auf BulkBuyAmount).
    /// </summary>
    public decimal BulkUpgradeCost { get; set; }

    /// <summary>
    /// Beschriftung auf dem Upgrade-Button (z.B. "x10" oder "Max (47)").
    /// </summary>
    public string BulkUpgradeLabel { get; set; } = "";

    /// <summary>
    /// Vorschau der Einkommens-Steigerung nach Upgrade (z.B. "+1,5 €/s").
    /// </summary>
    public string UpgradeIncomePreview { get; set; } = "";

    /// <summary>
    /// Netto-Einkommen pro Sekunde (Brutto - Kosten), formatiert.
    /// </summary>
    public string NetIncomeDisplay { get; set; } = "";

    /// <summary>
    /// Ob das Netto-Einkommen negativ ist (Verlust).
    /// </summary>
    public bool IsNetNegative { get; set; }

    /// <summary>
    /// Ob der Workshop laufende Kosten hat (Worker vorhanden oder Level > 1).
    /// </summary>
    public bool HasCosts { get; set; }

    public string WorkerDisplay => $"{WorkerCount}x";
    public string IncomeDisplay => IncomePerSecond > 0 ? MoneyFormatter.FormatPerSecond(IncomePerSecond, 1) : "-";
    public string UpgradeCostDisplay => MoneyFormatter.FormatCompact(BulkUpgradeCost > 0 ? BulkUpgradeCost : UpgradeCost);
    public string HireCostDisplay => MoneyFormatter.FormatCompact(HireWorkerCost);
    public double LevelProgress => Level / (double)Workshop.MaxLevel;

    // Level-basierte Farb-Intensitaet fuer Workshop-Streifen
    // Freischaltbare gesperrte Workshops bekommen etwas mehr Farbe
    public double ColorIntensity => !IsUnlocked
        ? (CanBuyUnlock ? 0.30 : 0.10)
        : Level switch
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

    // Dynamischer BoxShadow: Max-Level Gold-Glow, Upgrade leistbar dezenter Glow, freischaltbar+leistbar Craft-Glow, sonst keiner
    public string GlowShadow => IsMaxLevel
        ? "0 0 12 0 #60FFD700"
        : CanAffordUpgrade && IsUnlocked
            ? "0 0 8 0 #40D97706"
            : CanAffordUnlock && !IsUnlocked
                ? "0 0 10 0 #50E8A00E"
                : "none";

    // Phase 12.2: "Fast geschafft" Puls wenn >= 80% des Upgrade-Preises vorhanden
    public bool IsAlmostAffordable => !CanAffordUpgrade && IsUnlocked && UpgradeCost > 0;

    // Milestone-System: Naechstes Milestone-Level und Fortschritt
    private static readonly int[] Milestones = [50, 100, 250, 500, 1000];

    public int NextMilestone
    {
        get
        {
            foreach (var m in Milestones)
                if (Level < m) return m;
            return 0; // Max erreicht
        }
    }

    public double MilestoneProgress
    {
        get
        {
            int prev = 1;
            foreach (var m in Milestones)
            {
                if (Level < m)
                    return (Level - prev) / (double)(m - prev);
                prev = m;
            }
            return 1.0;
        }
    }

    public string MilestoneDisplay => NextMilestone > 0 ? $"\u2192 Lv.{NextMilestone}" : "";
    public bool ShowMilestone => IsUnlocked && NextMilestone > 0;

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
        OnPropertyChanged(nameof(CanAffordUnlock));
        OnPropertyChanged(nameof(CanUpgrade));
        OnPropertyChanged(nameof(CanHireWorker));
        OnPropertyChanged(nameof(WorkerDisplay));
        OnPropertyChanged(nameof(IncomeDisplay));
        OnPropertyChanged(nameof(UpgradeCostDisplay));
        OnPropertyChanged(nameof(HireCostDisplay));
        OnPropertyChanged(nameof(LevelProgress));
        OnPropertyChanged(nameof(ColorIntensity));
        OnPropertyChanged(nameof(IsMaxLevel));
        OnPropertyChanged(nameof(GlowShadow));
        OnPropertyChanged(nameof(IsAlmostAffordable));
        OnPropertyChanged(nameof(NextMilestone));
        OnPropertyChanged(nameof(MilestoneProgress));
        OnPropertyChanged(nameof(MilestoneDisplay));
        OnPropertyChanged(nameof(ShowMilestone));
        OnPropertyChanged(nameof(BulkUpgradeCost));
        OnPropertyChanged(nameof(BulkUpgradeLabel));
        OnPropertyChanged(nameof(UpgradeIncomePreview));
        OnPropertyChanged(nameof(NetIncomeDisplay));
        OnPropertyChanged(nameof(IsNetNegative));
        OnPropertyChanged(nameof(HasCosts));
    }
}
