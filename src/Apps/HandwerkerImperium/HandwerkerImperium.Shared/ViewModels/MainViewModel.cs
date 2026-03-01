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
/// Event args für den Daily-Reward-Dialog.
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
/// Haupt-ViewModel für den Spielbildschirm.
/// Aufgeteilt in Partial Classes:
///   MainViewModel.cs          - Felder, Constructor, Properties, Event-Handlers, Helpers, Dispose
///   MainViewModel.Navigation.cs - Tab-Auswahl, Child-Navigation, Back-Button
///   MainViewModel.Dialogs.cs    - Alert/Confirm, Story, Tutorial, Prestige-Bestätigung
///   MainViewModel.Economy.cs    - Workshop-Kauf/Upgrade, Orders, Rush, Delivery, Prestige-Banner
///   MainViewModel.Missions.cs   - Daily Challenges, Weekly Missions, Quick Jobs, Lucky Spin
///   MainViewModel.Init.cs       - InitializeAsync, Offline-Earnings, Daily Reward, Cloud-Save
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
    private readonly IGoalService _goalService;
    private bool _disposed;
    private decimal _pendingOfflineEarnings;
    private QuickJob? _activeQuickJob;
    private bool _quickJobMiniGamePlayed;

    // Statisches Array vermeidet Allokation bei jedem RefreshWorkshops()-Aufruf
    private static readonly WorkshopType[] _workshopTypes = Enum.GetValues<WorkshopType>();

    // Zaehler fuer FloatingText-Anzeige (nur alle 3 Ticks, nicht jeden)
    private int _floatingTextCounter;

    // Zaehler fuer Ziel-Aktualisierung (alle 60 Ticks)
    private int _tickForGoal;

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
    private Action? _guildCelebrationHandler;
    private readonly Action<string, string> _workerProfileFloatingTextHandler;

    // Gespeicherte Delegate-Referenzen fuer Lambda-Subscriptions (fuer Dispose-Unsubscribe)
    private readonly Action _adUnavailableHandler;
    private readonly Action<string, string> _saveGameErrorHandler;
    private readonly Action<string> _luckySpinNavHandler;

    // Gespeicherte Delegate-Referenz fuer BuildingsVM FloatingText (Event-Leak-Fix)
    private readonly Action<string, string> _buildingsFloatingTextHandler;

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
    private decimal _money;

    [ObservableProperty]
    private string _moneyDisplay = "0 €";

    [ObservableProperty]
    private decimal _incomePerSecond;

    [ObservableProperty]
    private string _incomeDisplay = "0 €/s";

    /// <summary>
    /// Netto-Einkommen (Brutto - Kosten) formatiert für Dashboard-Anzeige.
    /// </summary>
    [ObservableProperty]
    private string _netIncomeHeaderDisplay = "";

    /// <summary>
    /// True wenn Netto-Einkommen negativ ist (für rote Farbe im Dashboard-Header).
    /// </summary>
    [ObservableProperty]
    private bool _isNetIncomeNegative;

    /// <summary>
    /// Farbe des Netto-Einkommens: Rot bei Verlust, halbtransparentes Weiß bei Gewinn.
    /// </summary>
    [ObservableProperty]
    private string _netIncomeColor = "#FFFFFFAA";

    /// <summary>
    /// Worker-Warnungstext (erschöpfte/unzufriedene/kündigungsgefährdete Arbeiter).
    /// </summary>
    [ObservableProperty]
    private string _workerWarningText = "";

    /// <summary>
    /// True wenn mindestens ein Worker erschöpft oder unzufrieden ist.
    /// </summary>
    [ObservableProperty]
    private bool _hasWorkerWarning;

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

    // Dashboard Aufträge/Schnelljobs Umschalter
    [ObservableProperty]
    private bool _isOrdersTabActive = true;

    [ObservableProperty]
    private bool _isQuickJobsTabActive;

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE-BANNER (Task #14)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isPrestigeAvailable;

    [ObservableProperty]
    private string _prestigePointsPreview = "";

    [ObservableProperty]
    private string _prestigePreviewGains = "";

    [ObservableProperty]
    private string _prestigePreviewLosses = "";

    [ObservableProperty]
    private string _prestigePreviewSpeedUp = "";

    [ObservableProperty]
    private string _prestigePreviewTierName = "";

    // ═══════════════════════════════════════════════════════════════════════
    // NÄCHSTES ZIEL (GoalService)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _currentGoalDescription = "";

    [ObservableProperty]
    private string _currentGoalReward = "";

    [ObservableProperty]
    private double _currentGoalProgress;

    [ObservableProperty]
    private string _currentGoalIcon = "TrendingUp";

    [ObservableProperty]
    private bool _hasCurrentGoal;

    private string? _currentGoalRoute;

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

    /// <summary>
    /// Anzahl claimbarer Daily Challenges + Weekly Missions (für Tab-Bar Badge).
    /// </summary>
    [ObservableProperty]
    private int _claimableMissionsCount;

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

    /// <summary>
    /// Gesamt-Einkommensbonus durch gesammelte Meisterwerkzeuge (z.B. "+12%").
    /// </summary>
    [ObservableProperty]
    private string _masterToolsBonusDisplay = "";

    /// <summary>
    /// Prestige-Shop ist ab Level 500 freigeschaltet.
    /// </summary>
    [ObservableProperty]
    private bool _isPrestigeShopUnlocked;

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

    // Level-Gates für Automatisierung (delegiert an GameStateService)
    public bool IsAutoCollectUnlocked => _gameStateService.IsAutoCollectUnlocked;
    public bool IsAutoAcceptUnlocked => _gameStateService.IsAutoAcceptUnlocked;
    public bool IsAutoAssignUnlocked => _gameStateService.IsAutoAssignUnlocked;
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
    private bool _isMissionenActive;

    [ObservableProperty]
    private bool _isGuildResearchActive;

    [ObservableProperty]
    private bool _isGuildMembersActive;

    [ObservableProperty]
    private bool _isGuildInviteActive;

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
                                    !IsBattlePassActive && !IsCraftingActive &&
                                    !IsGuildResearchActive && !IsGuildMembersActive &&
                                    !IsGuildInviteActive;

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
        IsMissionenActive = false;
        IsResearchActive = false;
        IsManagerActive = false;
        IsTournamentActive = false;
        IsSeasonalEventActive = false;
        IsBattlePassActive = false;
        IsGuildActive = false;
        IsGuildResearchActive = false;
        IsGuildMembersActive = false;
        IsGuildInviteActive = false;
        IsCraftingActive = false;
        IsForgeGameActive = false;
        IsInventGameActive = false;
    }

    private void NotifyTabBarVisibility()
    {
        OnPropertyChanged(nameof(IsTabBarVisible));
    }

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

    /// <summary>
    /// Zentrale Effekt-Engine (Singleton aus DI). Wird von DashboardView direkt genutzt.
    /// </summary>
    public GameJuiceEngine GameJuiceEngine { get; }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

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
        GameJuiceEngine gameJuiceEngine,
        IGoalService goalService,
        IStoryService? storyService = null,
        ITutorialService? tutorialService = null,
        IReviewService? reviewService = null,
        IPrestigeService? prestigeService = null,
        INotificationService? notificationService = null,
        IPlayGamesService? playGamesService = null)
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
        _goalService = goalService;
        GameJuiceEngine = gameJuiceEngine;

        // Delegate-Felder zuweisen (statt anonymer Lambdas, damit Dispose() abmelden kann)
        _adUnavailableHandler = () => ShowAlertDialog(
            _localizationService.GetString("AdVideoNotAvailableTitle"),
            _localizationService.GetString("AdVideoNotAvailableMessage"),
            _localizationService.GetString("OK"));
        _rewardedAdService.AdUnavailable += _adUnavailableHandler;

        // SaveGame-Fehler an den Benutzer weiterleiten
        _saveGameErrorHandler = (titleKey, msgKey) =>
            Dispatcher.UIThread.Post(() => ShowAlertDialog(
                _localizationService.GetString(titleKey),
                _localizationService.GetString(msgKey),
                _localizationService.GetString("OK")));
        _saveGameService.ErrorOccurred += _saveGameErrorHandler;

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
        // Delegate-Feld zuweisen (statt anonymem Lambda)
        _luckySpinNavHandler = _ => HideLuckySpin();
        LuckySpinViewModel.NavigationRequested += _luckySpinNavHandler;

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
        _guildCelebrationHandler = () => CelebrationRequested?.Invoke();
        _workerProfileFloatingTextHandler = (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        // Benanntes Delegate-Feld fuer BuildingsVM FloatingText (statt anonymem Lambda → Dispose-sicher)
        _buildingsFloatingTextHandler = (text, cat) => FloatingTextRequested?.Invoke(text, cat);

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
        WorkerProfileViewModel.FloatingTextRequested += _workerProfileFloatingTextHandler;
        BuildingsViewModel.AlertRequested += _alertHandler;
        BuildingsViewModel.FloatingTextRequested += _buildingsFloatingTextHandler;
        ResearchViewModel.AlertRequested += _alertHandler;
        ResearchViewModel.ConfirmationRequested += _confirmHandler;
        TournamentViewModel.AlertRequested += _alertHandler;
        BattlePassViewModel.AlertRequested += _alertHandler;
        GuildViewModel.ConfirmationRequested += _confirmHandler;
        GuildViewModel.CelebrationRequested += _guildCelebrationHandler;

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

        // Tutorial verdrahten (per Constructor Injection statt Service Locator)
        _tutorialService = tutorialService;
        if (_tutorialService != null)
        {
            _tutorialService.StepChanged += OnTutorialStep;
            _tutorialService.TutorialCompleted += OnTutorialDone;
        }

        // ReviewService + PrestigeService verdrahten (per Constructor Injection)
        _reviewService = reviewService;
        _prestigeService = prestigeService
                           ?? throw new InvalidOperationException("IPrestigeService required");
        _prestigeService.PrestigeCompleted += OnPrestigeCompleted;

        // Notification + PlayGames Services (per Constructor Injection)
        _notificationService = notificationService;
        _playGamesService = playGamesService;
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

        // Sound + FloatingText bei jedem Level-Up
        _audioService.PlaySoundAsync(GameSound.ButtonTap).FireAndForget();
        FloatingTextRequested?.Invoke($"Level {e.NewLevel}!", "level");

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

        // Zeremonie: Feuerwerk + Confetti + Sound
        CelebrationRequested?.Invoke();
        var tierName = _localizationService.GetString("PrestigeCompleted") ?? "Prestige!";
        CeremonyRequested?.Invoke(CeremonyType.Prestige, tierName, $"#{prestigeCount}");
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
        FloatingTextRequested?.Invoke($"Prestige #{prestigeCount}!", "level");

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

        // Multiplikator-Meilensteine (Bumpy Progression)
        if (!IsHoldingUpgrade && Workshop.IsMilestoneLevel(e.NewLevel))
        {
            decimal milestoneMultiplier = Workshop.GetMilestoneMultiplierForLevel(e.NewLevel);
            var workshopName = _localizationService.GetString(e.WorkshopType.GetLocalizationKey());
            string boostText = $"x{milestoneMultiplier:0.#} {_localizationService.GetString("IncomeBoost") ?? "EINKOMMENS-BOOST"}!";

            FloatingTextRequested?.Invoke(boostText, "golden_screws");
            _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

            // Größere Zeremonien bei höheren Meilensteinen
            if (e.NewLevel >= 50)
            {
                CeremonyRequested?.Invoke(CeremonyType.WorkshopMilestone,
                    $"{workshopName} Lv.{e.NewLevel}",
                    boostText);
            }
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

        // Ziel-Cache invalidieren (Workshop-Level könnte Ziel erfüllen)
        _goalService.Invalidate();
    }

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
    {
        // Nur den betroffenen Workshop aktualisieren statt alle
        RefreshSingleWorkshop(e.WorkshopType);

        // Ziel-Cache invalidieren (Worker-Einstellung könnte Ziel erfüllen)
        _goalService.Invalidate();
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

        // Ziel-Cache invalidieren (Auftragsabschluss könnte Ziel erfüllen)
        _goalService.Invalidate();
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

    // ═══════════════════════════════════════════════════════════════════════
    // GAME TICK
    // ═══════════════════════════════════════════════════════════════════════

    private void OnGameTick(object? sender, GameTickEventArgs e)
    {
        // Nur updaten wenn sich der Wert geaendert hat (vermeidet unnoetige UI-Updates)
        var state = _gameStateService.State;
        var newIncome = state.NetIncomePerSecond;
        if (newIncome != IncomePerSecond)
        {
            IncomePerSecond = newIncome;
            IncomeDisplay = $"{FormatMoney(IncomePerSecond)}/s";
            UpdateNetIncomeHeader(state);
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
            RefreshReputation(state);
            RefreshPrestigeBanner(state);
        }

        // Nächstes Ziel alle 60 Ticks aktualisieren
        if (_tickForGoal++ >= 60)
        {
            _tickForGoal = 0;
            RefreshCurrentGoal();
        }
        else if (HasActiveEvent)
        {
            UpdateEventTimer();
        }

        // Weekly Missions + Lucky Spin + Welcome Back + Worker-Warnung periodisch aktualisieren (alle 10 Ticks)
        if (_floatingTextCounter % 10 == 0)
        {
            HasFreeSpin = _luckySpinService.HasFreeSpin;

            // Worker-Warnung aktualisieren (Fatigue/Mood-Checks)
            UpdateWorkerWarning(state);

            // Welcome Back Timer aktualisieren
            if (IsWelcomeOfferVisible)
            {
                var offer = state.ActiveWelcomeBackOffer;
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
    // NÄCHSTES ZIEL
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert das nächste Ziel-Banner auf dem Dashboard.
    /// </summary>
    private void RefreshCurrentGoal()
    {
        var goal = _goalService.GetCurrentGoal();
        HasCurrentGoal = goal != null;
        if (goal != null)
        {
            CurrentGoalDescription = goal.Description;
            CurrentGoalReward = goal.RewardHint;
            CurrentGoalProgress = goal.Progress;
            CurrentGoalIcon = goal.IconKind;
            _currentGoalRoute = goal.NavigationRoute;
        }
    }

    [RelayCommand]
    private void NavigateToGoal()
    {
        if (_currentGoalRoute != null)
            OnChildNavigation(_currentGoalRoute);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static string FormatMoney(decimal amount) => MoneyFormatter.FormatCompact(amount);

    /// <summary>
    /// Aktualisiert die Netto-Einkommen-Anzeige im Dashboard-Header.
    /// Zeigt Brutto minus Kosten mit Farbindikator (rot wenn negativ).
    /// </summary>
    private void UpdateNetIncomeHeader(GameState state)
    {
        var netIncome = state.TotalIncomePerSecond - state.TotalCostsPerSecond;
        IsNetIncomeNegative = netIncome < 0;
        NetIncomeColor = netIncome < 0 ? "#FF5722" : "#FFFFFFAA";

        var netLabel = _localizationService.GetString("NetIncome") ?? "Netto";
        NetIncomeHeaderDisplay = $"{netLabel}: {MoneyFormatter.FormatPerSecond(netIncome, 1)}";
    }

    /// <summary>
    /// Prüft alle Worker auf Erschöpfung (Fatigue>80), Unzufriedenheit (Mood kleiner 30) und Kündigungsrisiko (Mood kleiner 15).
    /// Zeigt die dringendste Warnung im Dashboard-Banner.
    /// </summary>
    private void UpdateWorkerWarning(GameState state)
    {
        int tiredCount = 0, unhappyCount = 0, quitRisk = 0;
        foreach (var ws in state.Workshops)
        {
            foreach (var w in ws.Workers)
            {
                if (w.Fatigue > 80) tiredCount++;
                if (w.Mood < 30) unhappyCount++;
                if (w.Mood < 15) quitRisk++;
            }
        }

        HasWorkerWarning = tiredCount > 0 || unhappyCount > 0;

        if (quitRisk > 0)
        {
            WorkerWarningText = string.Format(
                _localizationService.GetString("WorkerQuitRisk") ?? "{0} Arbeiter drohen zu kündigen!",
                quitRisk);
        }
        else if (unhappyCount > 0)
        {
            WorkerWarningText = string.Format(
                _localizationService.GetString("WorkerUnhappy") ?? "{0} Arbeiter unzufrieden",
                unhappyCount);
        }
        else if (tiredCount > 0)
        {
            WorkerWarningText = string.Format(
                _localizationService.GetString("WorkerTired") ?? "{0} Arbeiter erschöpft",
                tiredCount);
        }
    }

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
        ForgeGameViewModel.NavigationRequested -= OnChildNavigation;
        InventGameViewModel.NavigationRequested -= OnChildNavigation;
        ManagerViewModel.NavigationRequested -= OnChildNavigation;
        TournamentViewModel.NavigationRequested -= OnChildNavigation;
        SeasonalEventViewModel.NavigationRequested -= OnChildNavigation;
        BattlePassViewModel.NavigationRequested -= OnChildNavigation;
        GuildViewModel.NavigationRequested -= OnChildNavigation;
        CraftingViewModel.NavigationRequested -= OnChildNavigation;
        LuckySpinViewModel.NavigationRequested -= _luckySpinNavHandler;
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
        WorkerProfileViewModel.FloatingTextRequested -= _workerProfileFloatingTextHandler;
        BuildingsViewModel.AlertRequested -= _alertHandler;
        // Benanntes Feld statt anonymem Lambda → korrekte Abmeldung
        BuildingsViewModel.FloatingTextRequested -= _buildingsFloatingTextHandler;
        ResearchViewModel.AlertRequested -= _alertHandler;
        ResearchViewModel.ConfirmationRequested -= _confirmHandler;
        TournamentViewModel.AlertRequested -= _alertHandler;
        BattlePassViewModel.AlertRequested -= _alertHandler;
        GuildViewModel.ConfirmationRequested -= _confirmHandler;
        GuildViewModel.CelebrationRequested -= _guildCelebrationHandler;

        // Lambda-basierte Service-Subscriptions abmelden
        _rewardedAdService.AdUnavailable -= _adUnavailableHandler;
        _saveGameService.ErrorOccurred -= _saveGameErrorHandler;

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
