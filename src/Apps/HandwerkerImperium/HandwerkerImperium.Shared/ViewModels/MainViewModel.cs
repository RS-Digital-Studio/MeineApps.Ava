using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;
using HandwerkerImperium.ViewModels.MiniGames;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Haupt-ViewModel für den Spielbildschirm.
/// Aufgeteilt in Partial Classes:
///   MainViewModel.cs          - Felder, Constructor, Properties, Event-Handlers, Helpers, Dispose
///   MainViewModel.Navigation.cs - Tab-Auswahl, Child-Navigation, Back-Button
///   MainViewModel.Dialogs.cs    - Weiterleitungen an DialogVM, Prestige-Durchführung
///   MainViewModel.Economy.cs    - Workshop-Kauf/Upgrade, Orders, Rush, Delivery, Prestige-Banner
///   MainViewModel.Missions.cs   - Daily Challenges, Weekly Missions, Quick Jobs, Lucky Spin
///   MainViewModel.Init.cs       - InitializeAsync, Offline-Earnings, Daily Reward, Cloud-Save
/// Dialog-Logik extrahiert nach DialogViewModel.cs (Alert, Confirm, Story, Hint, Achievement, Prestige-Dialog).
/// </summary>
public sealed partial class MainViewModel : ViewModelBase, IDisposable
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
    private readonly IContextualHintService _contextualHintService;
    private readonly IReviewService? _reviewService;
    private readonly IPrestigeService _prestigeService;
    private readonly IChallengeConstraintService? _challengeConstraints;
    private readonly INotificationService? _notificationService;
    private readonly IPlayGamesService? _playGamesService;
    private readonly IWeeklyMissionService _weeklyMissionService;
    private readonly IWelcomeBackService _welcomeBackService;
    private readonly ILuckySpinService _luckySpinService;
    private readonly IEquipmentService _equipmentService;
    private readonly IGoalService _goalService;
    private readonly IWorkerService _workerService;
    private readonly IRebirthService? _rebirthService;
    private bool _disposed;
    private decimal _pendingOfflineEarnings;
    private QuickJob? _activeQuickJob;
    private bool _quickJobMiniGamePlayed;

    // Dialog-Kaskaden-Begrenzung: Verzögerte Dialoge nach Startup
    private bool _hasDeferredDailyReward;
    private bool _hasDeferredStory;
    private bool _hasDeferredWelcomeHint;

    // Gecachtes Dictionary vermeidet Allokation bei jedem MoneyChanged-Tick im Max-Modus
    private readonly Dictionary<WorkshopType, Workshop> _workshopLookupCache = new(10);

    // Statisches Array vermeidet Allokation bei jedem RefreshWorkshops()-Aufruf
    internal static readonly WorkshopType[] _workshopTypes = Enum.GetValues<WorkshopType>();

    // Workshop-Level-Meilensteine (statisch, vermeidet Array-Allokation pro Workshop-Upgrade)
    private static readonly (int level, int screws)[] s_workshopMilestones =
        [(50, 2), (100, 5), (250, 10), (500, 25), (1000, 50)];

    // Zaehler fuer FloatingText-Anzeige (nur alle 3 Ticks, nicht jeden)
    private int _floatingTextCounter;

    // Zaehler fuer Ziel-Aktualisierung (alle 60 Ticks)
    private int _tickForGoal;

    // QuickJob-Timer → extrahiert nach MissionsFeatureViewModel

    // Dirty-Flag fuer RefreshChallenges → extrahiert nach MissionsFeatureViewModel

    // Gecachter lokalisierter "Netto"-Label (aendert sich nur bei Sprachwechsel)
    private string _cachedNetIncomeLabel = "Netto";

    // Phase 9: Smooth Money-Counter Animation (vom MainView Render-Timer getrieben)
    internal decimal _displayedMoney;
    internal decimal _targetMoney;
    private bool _moneyAnimActive;
    private const decimal MoneyAnimSpeed = 0.15m; // Interpolations-Faktor pro Frame

    // Wiederverwendbarer Timer für Level-Up Pulse (verhindert Timer-Leak bei rapidem Level-Up)
    private DispatcherTimer? _levelPulseTimer;

    // Cache für saisonalen Modifikator (ändert sich nur monatlich)
    private int _cachedSeasonMonth;

    // Cache für aktiven Event-Namen (aendert sich nur bei Event-Wechsel, nicht pro Tick)
    private string? _cachedActiveEventKey;
    private string? _cachedActiveEventName;

    // Gespeicherte Delegate-Referenzen fuer EconomyVM Events (Dispose-sicher)
    private Action<string, string>? _economyFloatingTextHandler;
    private Action? _economyCelebrationHandler;

    // Gespeicherte Delegate-Referenzen fuer Alert/Confirmation Events (fuer Dispose-Unsubscribe)
    private Action? _guildCelebrationHandler;
    private readonly Action<string, string> _guildFloatingTextHandler;
    private readonly Action<string, string> _workerProfileFloatingTextHandler;

    // Gespeicherte Delegate-Referenzen fuer Lambda-Subscriptions (fuer Dispose-Unsubscribe)
    private readonly Action _adUnavailableHandler;
    private readonly Action<string, string> _saveGameErrorHandler;
    private readonly Action<string> _luckySpinNavHandler;

    // Gespeicherte Delegate-Referenz fuer BuildingsVM FloatingText (Event-Leak-Fix)
    private readonly Action<string, string> _buildingsFloatingTextHandler;

    // Gespeicherte Delegate-Referenzen fuer AscensionVM Events (Dispose-sicher)
    private readonly Action<string, string> _ascensionFloatingTextHandler;
    private readonly Action _ascensionCelebrationHandler;

    // Gespeicherte Delegate-Referenzen fuer MissionsVM Events (Dispose-sicher)
    private readonly Action<string, string> _missionsFloatingTextHandler;
    private readonly Action _missionsCelebrationHandler;
    private readonly Action _missionsStreakRescuedHandler;

    // Gespeicherte Delegate-Referenzen fuer DialogVM Events (Dispose-sicher)
    private readonly Action _dialogPrestigeSummaryGoToShopHandler;
    private readonly Action<string, string> _dialogFloatingTextHandler;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS FOR NAVIGATION AND DIALOGS
    // ═══════════════════════════════════════════════════════════════════════

    public event EventHandler<OfflineEarningsEventArgs>? ShowOfflineEarnings;
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

    /// <summary>
    /// True wenn der Soft-Cap auf den Einkommens-Multiplikator aktiv ist.
    /// Zeigt dem Spieler dass Boni ab 10x gedeckelt werden.
    /// </summary>
    [ObservableProperty]
    private bool _isSoftCapActive;

    /// <summary>
    /// Wie viel Prozent durch den Soft-Cap verloren gehen (z.B. "-25%").
    /// </summary>
    [ObservableProperty]
    private string _softCapText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCraftingResearch))]
    [NotifyPropertyChangedFor(nameof(ShowManagerSection))]
    [NotifyPropertyChangedFor(nameof(ShowMasterToolsSection))]
    [NotifyPropertyChangedFor(nameof(IsQuickJobsUnlocked))]
    [NotifyPropertyChangedFor(nameof(ShowBannerStrip))]
    [NotifyPropertyChangedFor(nameof(QuickAccessColumns))]
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

    /// <summary>
    /// Dynamische Höhe des Workshop-Canvas basierend auf Anzahl freigeschalteter Workshops.
    /// 2 Spalten, ~160dp pro Reihe + 8dp Gap.
    /// </summary>
    public double WorkshopCanvasHeight
    {
        get
        {
            var count = Workshops.Count;
            if (count == 0) return 160;
            var rows = (int)Math.Ceiling(count / 2.0);
            return rows * 160 + (rows - 1) * 8;
        }
    }

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

    // Quick Jobs + Daily Challenges → extrahiert nach MissionsFeatureViewModel

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

    // AllQuickJobsDone → extrahiert nach MissionsFeatureViewModel

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

    /// <summary>Ob ein nächsthöherer Prestige-Tier existiert (für Fortschritts-Anzeige).</summary>
    [ObservableProperty]
    private bool _hasNextPrestigeTier;

    /// <summary>Anzahl aktiver Challenges (für Badge-Anzeige im Prestige-Banner).</summary>
    [ObservableProperty]
    private int _activeChallengeCount;

    /// <summary>Text-Anzeige aktiver Challenges (z.B. "Spartaner +40%, Sprint +35%").</summary>
    [ObservableProperty]
    private string _activeChallengesText = "";

    // PP-2: Challenge-Chip aktiv/inaktiv State
    [ObservableProperty] private bool _isChallengeSpartanerActive;
    [ObservableProperty] private bool _isChallengeOhneForschungActive;
    [ObservableProperty] private bool _isChallengeInflationszeitActive;
    [ObservableProperty] private bool _isChallengeSoloMeisterActive;
    [ObservableProperty] private bool _isChallengeSprintActive;
    [ObservableProperty] private bool _isChallengeKeinNetzActive;

    /// <summary>Aktuelle Run-Dauer als Text (für Prestige-Banner).</summary>
    [ObservableProperty]
    private string _currentRunDuration = "";

    /// <summary>Kompakter Fortschrittstext zum nächsten Tier (z.B. "Lv. 45/100 → Silver").</summary>
    [ObservableProperty]
    private string _nextPrestigeTierHint = "";

    /// <summary>Fortschritt zum nächsten Tier (0.0 - 1.0).</summary>
    [ObservableProperty]
    private double _nextPrestigeTierProgress;

    // Prestige-Tier-Dialog Properties sind jetzt in DialogVM

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

    // Weekly Missions, Welcome Back Offer → extrahiert nach MissionsFeatureViewModel

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

    // Lucky Spin, Streak-Rettung → extrahiert nach MissionsFeatureViewModel

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

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE-TIER BADGE (Dashboard-Header)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Kurztext des höchsten abgeschlossenen Prestige-Tiers (z.B. "G" für Gold).</summary>
    [ObservableProperty]
    private string _prestigeTierBadgeText = "";

    /// <summary>Farbe des Prestige-Tier-Badges (Hex).</summary>
    [ObservableProperty]
    private string _prestigeTierBadgeColor = "#9E9E9E";

    /// <summary>Sichtbar wenn mindestens ein Prestige abgeschlossen wurde.</summary>
    [ObservableProperty]
    private bool _showPrestigeBadge;

    // ═══════════════════════════════════════════════════════════════════════
    // BOOST-INDIKATOR (Dashboard-Header)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Sichtbar wenn Rush oder SpeedBoost aktiv ist.</summary>
    [ObservableProperty]
    private bool _showBoostIndicator;

    /// <summary>Multiplikator-Text (z.B. "2x", "4x").</summary>
    [ObservableProperty]
    private string _boostIndicatorText = "";

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

    // Meisterwerkzeuge → extrahiert nach MissionsFeatureViewModel

    /// <summary>
    /// Prestige-Shop ist ab Level 50 freigeschaltet (oder wenn bereits prestigiert).
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
    // DIALOG STATE (delegiert an DialogVM, außer Offline/DailyReward/Starter)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Eigenständiges ViewModel für Alert, Confirm, Story, Achievement, LevelUp, Hint, Prestige-Dialoge.</summary>
    public DialogViewModel DialogVM { get; private set; } = null!;

    [ObservableProperty]
    private bool _isOfflineEarningsDialogVisible;

    [ObservableProperty]
    private string _offlineEarningsAmountText = "";

    [ObservableProperty]
    private string _offlineEarningsDurationText = "";

    [ObservableProperty]
    private bool _isOfflineNewRecord;

    /// <summary>Nächstes Ziel als Wiedereinsteiger-Tipp (nur bei >48h Offline-Pause).</summary>
    [ObservableProperty]
    private string _offlineGoalText = "";

    [ObservableProperty]
    private bool _isDailyRewardDialogVisible;

    [ObservableProperty]
    private string _dailyRewardDayText = "";

    [ObservableProperty]
    private string _dailyRewardStreakText = "";

    [ObservableProperty]
    private string _dailyRewardAmountText = "";

    // Starter-Offer (einmaliges zeitlich begrenztes Premium-Angebot)
    [ObservableProperty]
    private bool _isStarterOfferVisible;

    [ObservableProperty]
    private string _starterOfferCountdown = string.Empty;

    /// <summary>
    /// True wenn irgendein Overlay-Dialog sichtbar ist.
    /// Kombiniert MainViewModel-eigene Dialoge mit DialogVM.IsAnyDialogVisible.
    /// </summary>
    private bool IsAnyDialogVisible =>
        IsOfflineEarningsDialogVisible || IsCombinedWelcomeDialogVisible ||
        MissionsVM.IsWelcomeOfferVisible || IsDailyRewardDialogVisible ||
        IsStarterOfferVisible || DialogVM.IsAnyDialogVisible;

    /// <summary>
    /// Indicates whether ads should be shown (not premium).
    /// </summary>
    public bool ShowAds => !_purchaseService.IsPremium;

    // Login-Streak (Daily Reward Streak)
    public int LoginStreak => _gameStateService.State.DailyRewardStreak;
    public bool HasLoginStreak => LoginStreak >= 2;

    // Dashboard-Header: Nur bei relevantem Status anzeigen (Entschlackung)
    public bool ShowStreakBadge => LoginStreak >= 5;
    public bool ShowReputationBadge => ReputationScore < LevelThresholds.ReputationWarningThreshold
                                      || ReputationScore >= LevelThresholds.ReputationHighlightThreshold;

    // Progressive Disclosure: Sections erst ab bestimmtem Level anzeigen.
    // Nach dem ersten Prestige sind ALLE Features permanent freigeschaltet
    // (Spieler verliert sonst Zugang zu Gilde, Forschung etc. nach dem Reset).
    private bool HasEverPrestiged => _gameStateService.State.Prestige.TotalPrestigeCount > 0;
    public bool ShowCraftingResearch => HasEverPrestiged || PlayerLevel >= LevelThresholds.CraftingResearch;
    public bool ShowManagerSection => HasEverPrestiged || PlayerLevel >= LevelThresholds.ManagerSection;
    public bool ShowMasterToolsSection => HasEverPrestiged || PlayerLevel >= LevelThresholds.MasterToolsSection;
    public bool IsQuickJobsUnlocked => HasEverPrestiged || PlayerLevel >= LevelThresholds.QuickJobs;
    public bool ShowBannerStrip => HasEverPrestiged || PlayerLevel >= LevelThresholds.BannerStrip;
    public int QuickAccessColumns => 1 + (ShowManagerSection ? 1 : 0) + (ShowMasterToolsSection ? 1 : 0);

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

    public bool AutoAssignWorkers
    {
        get => _gameStateService.State.Automation.AutoAssignWorkers;
        set
        {
            if (_gameStateService.State.Automation.AutoAssignWorkers == value) return;
            _gameStateService.State.Automation.AutoAssignWorkers = value;
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
    // ZENTRALES NAVIGATION-STATE (ActivePage Enum statt 35+ Booleans)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ActivePage _activePage = ActivePage.Dashboard;

    /// <summary>
    /// Zentrale Seitenumschaltung mit Seiteneffekten (GuildChat stoppen, PropertyChanged).
    /// Wird von CommunityToolkit automatisch aufgerufen wenn ActivePage sich ändert.
    /// </summary>
    partial void OnActivePageChanged(ActivePage oldValue, ActivePage newValue)
    {
        // GuildChat-Polling stoppen wenn Chat verlassen wird
        if (oldValue == ActivePage.GuildChat)
            GuildViewModel.StopChatPolling();

        // PropertyChanged für die berechneten IsXxxActive-Properties (nur die 2 geänderten)
        var oldProp = ActivePagePropertyName(oldValue);
        var newProp = ActivePagePropertyName(newValue);
        if (oldProp != null) OnPropertyChanged(oldProp);
        if (newProp != null) OnPropertyChanged(newProp);
        OnPropertyChanged(nameof(IsTabBarVisible));
        OnPropertyChanged(nameof(BreadcrumbText));

        // MiniGame-ContentControl aktualisieren (ein einziges statt 10 separate)
        OnPropertyChanged(nameof(ActiveMiniGameViewModel));
        OnPropertyChanged(nameof(IsAnyMiniGameActive));
    }

    // Berechnete Navigation-Properties (XAML-Bindings bleiben unverändert)
    public bool IsDashboardActive => ActivePage == ActivePage.Dashboard;
    public bool IsShopActive => ActivePage == ActivePage.Shop;
    public bool IsStatisticsActive => ActivePage == ActivePage.Statistics;
    public bool IsAchievementsActive => ActivePage == ActivePage.Achievements;
    public bool IsSettingsActive => ActivePage == ActivePage.Settings;
    public bool IsWorkshopDetailActive => ActivePage == ActivePage.WorkshopDetail;
    public bool IsOrderDetailActive => ActivePage == ActivePage.OrderDetail;
    public bool IsSawingGameActive => ActivePage == ActivePage.SawingGame;
    public bool IsPipePuzzleActive => ActivePage == ActivePage.PipePuzzle;
    public bool IsWiringGameActive => ActivePage == ActivePage.WiringGame;
    public bool IsPaintingGameActive => ActivePage == ActivePage.PaintingGame;
    public bool IsRoofTilingGameActive => ActivePage == ActivePage.RoofTilingGame;
    public bool IsBlueprintGameActive => ActivePage == ActivePage.BlueprintGame;
    public bool IsDesignPuzzleGameActive => ActivePage == ActivePage.DesignPuzzleGame;
    public bool IsInspectionGameActive => ActivePage == ActivePage.InspectionGame;
    public bool IsWorkerMarketActive => ActivePage == ActivePage.WorkerMarket;
    public bool IsBuildingsActive => ActivePage == ActivePage.Buildings;
    public bool IsResearchActive => ActivePage == ActivePage.Research;
    public bool IsManagerActive => ActivePage == ActivePage.Manager;
    public bool IsTournamentActive => ActivePage == ActivePage.Tournament;
    public bool IsSeasonalEventActive => ActivePage == ActivePage.SeasonalEvent;
    public bool IsBattlePassActive => ActivePage == ActivePage.BattlePass;
    public bool IsGuildActive => ActivePage == ActivePage.Guild;
    public bool IsMissionenActive => ActivePage == ActivePage.Missionen;
    public bool IsGuildResearchActive => ActivePage == ActivePage.GuildResearch;
    public bool IsGuildMembersActive => ActivePage == ActivePage.GuildMembers;
    public bool IsGuildInviteActive => ActivePage == ActivePage.GuildInvite;
    public bool IsGuildWarSeasonActive => ActivePage == ActivePage.GuildWarSeason;
    public bool IsGuildBossActive => ActivePage == ActivePage.GuildBoss;
    public bool IsGuildHallActive => ActivePage == ActivePage.GuildHall;
    public bool IsGuildAchievementsActive => ActivePage == ActivePage.GuildAchievements;
    public bool IsGuildChatActive => ActivePage == ActivePage.GuildChat;
    public bool IsGuildWarActive => ActivePage == ActivePage.GuildWar;
    public bool IsCraftingActive => ActivePage == ActivePage.Crafting;
    public bool IsForgeGameActive => ActivePage == ActivePage.ForgeGame;
    public bool IsInventGameActive => ActivePage == ActivePage.InventGame;
    public bool IsAscensionActive => ActivePage == ActivePage.Ascension;

    /// <summary>
    /// Gibt das aktuell aktive MiniGame-ViewModel zurück, oder null wenn kein MiniGame aktiv.
    /// Ermöglicht ein einziges ContentControl statt 10 separate (spart ~9 View-Instanzen + Renderer).
    /// </summary>
    public BaseMiniGameViewModel? ActiveMiniGameViewModel => ActivePage switch
    {
        ActivePage.SawingGame => MiniGames.Sawing,
        ActivePage.PipePuzzle => MiniGames.PipePuzzle,
        ActivePage.WiringGame => MiniGames.Wiring,
        ActivePage.PaintingGame => MiniGames.Painting,
        ActivePage.RoofTilingGame => MiniGames.RoofTiling,
        ActivePage.BlueprintGame => MiniGames.Blueprint,
        ActivePage.DesignPuzzleGame => MiniGames.DesignPuzzle,
        ActivePage.InspectionGame => MiniGames.Inspection,
        ActivePage.ForgeGame => MiniGames.Forge,
        ActivePage.InventGame => MiniGames.Invent,
        _ => null
    };

    public bool IsAnyMiniGameActive => ActiveMiniGameViewModel != null;

    // Overlay-States (überlagern die aktuelle Seite, ActivePage bleibt unverändert)
    [ObservableProperty]
    private bool _isWorkerProfileActive;

    partial void OnIsWorkerProfileActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(IsTabBarVisible));
    }

    [ObservableProperty]
    private bool _isLuckySpinVisible;

    /// <summary>Turnier-Button sichtbar ab Level 50 (Progressive Disclosure).</summary>
    [ObservableProperty]
    private bool _showTournamentSection;

    /// <summary>Saison-Event-Button sichtbar ab Level 60.</summary>
    [ObservableProperty]
    private bool _showSeasonalEventSection;

    /// <summary>Battle-Pass-Button sichtbar ab Level 70.</summary>
    [ObservableProperty]
    private bool _showBattlePassSection;

    // ═══════════════════════════════════════════════════════════════════════
    // PROGRESSIVE TAB-FREISCHALTUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Minimales Spieler-Level pro Tab-Index (0=Werkstatt, 1=Imperium, 2=Missionen, 3=Gilde, 4=Shop).
    /// Zentralisiert in <see cref="LevelThresholds"/>.
    /// </summary>
    public static int[] TabUnlockLevels => LevelThresholds.TabUnlockLevels;

    /// <summary>
    /// Gibt zurück ob der Tab bei gegebenem Index für das aktuelle Level gesperrt ist.
    /// </summary>
    public bool IsTabLocked(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= TabUnlockLevels.Length) return false;
        // Nach dem ersten Prestige alle Tabs permanent freigeschaltet
        if (HasEverPrestiged) return false;
        return PlayerLevel < TabUnlockLevels[tabIndex];
    }

    /// <summary>
    /// Gibt ein gecachtes Array zurück das angibt welche Tabs gesperrt sind (für Tab-Bar-Renderer).
    /// Cache wird nur bei Level-Änderung invalidiert (statt 25x/s neu allokiert).
    /// </summary>
    private bool[]? _cachedLockedTabs;
    private int _lockedTabsCacheLevel = -1;

    public bool[] GetLockedTabs()
    {
        if (_cachedLockedTabs != null && _lockedTabsCacheLevel == PlayerLevel)
            return _cachedLockedTabs;

        _cachedLockedTabs = new bool[5];
        for (int i = 0; i < 5; i++)
            _cachedLockedTabs[i] = IsTabLocked(i);
        _lockedTabsCacheLevel = PlayerLevel;
        return _cachedLockedTabs;
    }

    /// <summary>
    /// Haupt-Tabs bei denen die Tab-Bar sichtbar ist (5 Hauptseiten).
    /// </summary>
    private static readonly HashSet<ActivePage> s_mainTabs =
    [
        ActivePage.Dashboard, ActivePage.Buildings, ActivePage.Missionen,
        ActivePage.Guild, ActivePage.Shop
    ];

    /// <summary>
    /// Tab-Bar sichtbar nur auf den 5 Haupt-Tabs und wenn kein Overlay aktiv ist.
    /// </summary>
    public bool IsTabBarVisible => s_mainTabs.Contains(ActivePage) && !IsWorkerProfileActive;

    /// <summary>
    /// NAV-3: Breadcrumb-Text für Sub-Views (zeigt den Parent-Tab wenn Tab-Bar versteckt ist).
    /// </summary>
    public string BreadcrumbText => ActivePage switch
    {
        ActivePage.WorkshopDetail or ActivePage.OrderDetail => _localizationService.GetString("TabWorkshop") ?? "Werkstatt",
        ActivePage.WorkerMarket or ActivePage.Research or ActivePage.Manager or
        ActivePage.Crafting or ActivePage.Ascension => _localizationService.GetString("TabImperium") ?? "Imperium",
        ActivePage.Tournament or ActivePage.SeasonalEvent or ActivePage.BattlePass or
        ActivePage.Statistics or ActivePage.Achievements => _localizationService.GetString("TabMissions") ?? "Missionen",
        ActivePage.GuildResearch or ActivePage.GuildMembers or ActivePage.GuildInvite or
        ActivePage.GuildWarSeason or ActivePage.GuildBoss or ActivePage.GuildHall or
        ActivePage.GuildAchievements or ActivePage.GuildChat or ActivePage.GuildWar => _localizationService.GetString("TabGuild") ?? "Gilde",
        ActivePage.Settings => _localizationService.GetString("Settings") ?? "Einstellungen",
        _ => ""
    };

    /// <summary>
    /// Mapping ActivePage → Property-Name für gezielte PropertyChanged-Benachrichtigungen.
    /// Nur 2 Notifications pro Seitenwechsel statt 36 (alter Ansatz mit DeactivateAllTabs).
    /// </summary>
    private static string? ActivePagePropertyName(ActivePage page) => page switch
    {
        ActivePage.Dashboard => nameof(IsDashboardActive),
        ActivePage.Shop => nameof(IsShopActive),
        ActivePage.Statistics => nameof(IsStatisticsActive),
        ActivePage.Achievements => nameof(IsAchievementsActive),
        ActivePage.Settings => nameof(IsSettingsActive),
        ActivePage.WorkshopDetail => nameof(IsWorkshopDetailActive),
        ActivePage.OrderDetail => nameof(IsOrderDetailActive),
        ActivePage.SawingGame => nameof(IsSawingGameActive),
        ActivePage.PipePuzzle => nameof(IsPipePuzzleActive),
        ActivePage.WiringGame => nameof(IsWiringGameActive),
        ActivePage.PaintingGame => nameof(IsPaintingGameActive),
        ActivePage.RoofTilingGame => nameof(IsRoofTilingGameActive),
        ActivePage.BlueprintGame => nameof(IsBlueprintGameActive),
        ActivePage.DesignPuzzleGame => nameof(IsDesignPuzzleGameActive),
        ActivePage.InspectionGame => nameof(IsInspectionGameActive),
        ActivePage.WorkerMarket => nameof(IsWorkerMarketActive),
        ActivePage.Buildings => nameof(IsBuildingsActive),
        ActivePage.Research => nameof(IsResearchActive),
        ActivePage.Manager => nameof(IsManagerActive),
        ActivePage.Tournament => nameof(IsTournamentActive),
        ActivePage.SeasonalEvent => nameof(IsSeasonalEventActive),
        ActivePage.BattlePass => nameof(IsBattlePassActive),
        ActivePage.Guild => nameof(IsGuildActive),
        ActivePage.Missionen => nameof(IsMissionenActive),
        ActivePage.GuildResearch => nameof(IsGuildResearchActive),
        ActivePage.GuildMembers => nameof(IsGuildMembersActive),
        ActivePage.GuildInvite => nameof(IsGuildInviteActive),
        ActivePage.GuildWarSeason => nameof(IsGuildWarSeasonActive),
        ActivePage.GuildBoss => nameof(IsGuildBossActive),
        ActivePage.GuildHall => nameof(IsGuildHallActive),
        ActivePage.GuildAchievements => nameof(IsGuildAchievementsActive),
        ActivePage.GuildChat => nameof(IsGuildChatActive),
        ActivePage.GuildWar => nameof(IsGuildWarActive),
        ActivePage.Crafting => nameof(IsCraftingActive),
        ActivePage.ForgeGame => nameof(IsForgeGameActive),
        ActivePage.InventGame => nameof(IsInventGameActive),
        ActivePage.Ascension => nameof(IsAscensionActive),
        _ => null
    };

    // ═══════════════════════════════════════════════════════════════════════
    // CHILD VIEWMODELS
    // ═══════════════════════════════════════════════════════════════════════

    public ShopViewModel ShopViewModel { get; }
    public StatisticsViewModel StatisticsViewModel { get; }
    public AchievementsViewModel AchievementsViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public WorkshopViewModel WorkshopViewModel { get; }
    public OrderViewModel OrderViewModel { get; }
    /// <summary>Alle 10 MiniGame-VMs als Container (Zugriff via MiniGames.Sawing etc.).</summary>
    internal MiniGameViewModels MiniGames { get; }
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
    // ForgeGameVM + InventGameVM → in MiniGames Container
    public AscensionViewModel AscensionViewModel { get; }

    /// <summary>Eigenstaendiges ViewModel fuer Daily Challenges, Weekly Missions, Quick Jobs, Lucky Spin etc.</summary>
    public MissionsFeatureViewModel MissionsVM { get; }

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
        MiniGameViewModels miniGames,
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
        AscensionViewModel ascensionViewModel,
        IWeeklyMissionService weeklyMissionService,
        IWelcomeBackService welcomeBackService,
        ILuckySpinService luckySpinService,
        IEquipmentService equipmentService,
        LuckySpinViewModel luckySpinViewModel,
        GameJuiceEngine gameJuiceEngine,
        IGoalService goalService,
        IWorkerService workerService,
        IContextualHintService contextualHintService,
        DialogViewModel dialogViewModel,
        MissionsFeatureViewModel missionsFeatureViewModel,
        IRebirthService? rebirthService = null,
        IStoryService? storyService = null,
        IReviewService? reviewService = null,
        IPrestigeService? prestigeService = null,
        INotificationService? notificationService = null,
        IPlayGamesService? playGamesService = null,
        IChallengeConstraintService? challengeConstraints = null)
    {
        _gameStateService = gameStateService;
        _gameLoopService = gameLoopService;
        _offlineProgressService = offlineProgressService;
        _orderGeneratorService = orderGeneratorService;
        _audioService = audioService;
        _localizationService = localizationService;
        _cachedNetIncomeLabel = _localizationService.GetString("NetIncome") ?? "Netto";
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
        _workerService = workerService;
        _rebirthService = rebirthService;
        GameJuiceEngine = gameJuiceEngine;

        // Delegate-Felder zuweisen (statt anonymer Lambdas, damit Dispose() abmelden kann)
        _adUnavailableHandler = () => DialogVM.ShowAlertDialog(
            _localizationService.GetString("AdVideoNotAvailableTitle"),
            _localizationService.GetString("AdVideoNotAvailableMessage"),
            _localizationService.GetString("OK"));
        _rewardedAdService.AdUnavailable += _adUnavailableHandler;

        // SaveGame-Fehler an den Benutzer weiterleiten
        _saveGameErrorHandler = (titleKey, msgKey) =>
            Dispatcher.UIThread.Post(() => DialogVM.ShowAlertDialog(
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
        MiniGames = miniGames;
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
        AscensionViewModel = ascensionViewModel;
        LuckySpinViewModel = luckySpinViewModel;
        // Forge + Invent sind bereits in MiniGames Container
        // Delegate-Feld zuweisen (statt anonymem Lambda). MissionsVM wird weiter unten gesetzt,
        // aber das Lambda captured `this` und liest MissionsVM erst bei Aufruf (nach dem Konstruktor).
        _luckySpinNavHandler = _ => { MissionsVM?.HideLuckySpinCommand.Execute(null); IsLuckySpinVisible = false; };
        LuckySpinViewModel.NavigationRequested += _luckySpinNavHandler;

        // Wire up child VM navigation events
        ShopViewModel.NavigationRequested += OnChildNavigation;
        StatisticsViewModel.NavigationRequested += OnChildNavigation;
        AchievementsViewModel.NavigationRequested += OnChildNavigation;
        SettingsViewModel.NavigationRequested += OnChildNavigation;
        WorkshopViewModel.NavigationRequested += OnChildNavigation;
        OrderViewModel.NavigationRequested += OnChildNavigation;
        foreach (var mg in MiniGames.All)
            mg.NavigationRequested += OnChildNavigation;
        ManagerViewModel.NavigationRequested += OnChildNavigation;
        TournamentViewModel.NavigationRequested += OnChildNavigation;
        SeasonalEventViewModel.NavigationRequested += OnChildNavigation;
        BattlePassViewModel.NavigationRequested += OnChildNavigation;
        GuildViewModel.NavigationRequested += OnChildNavigation;
        CraftingViewModel.NavigationRequested += OnChildNavigation;
        AscensionViewModel.NavigationRequested += OnChildNavigation;

        WorkerMarketViewModel.NavigationRequested += OnChildNavigation;
        WorkerProfileViewModel.NavigationRequested += OnChildNavigation;
        BuildingsViewModel.NavigationRequested += OnChildNavigation;
        ResearchViewModel.NavigationRequested += OnChildNavigation;

        // Child-VM Events verdrahten (benannte Delegates fuer Dispose-Unsubscribe)
        _guildCelebrationHandler = () => CelebrationRequested?.Invoke();
        _guildFloatingTextHandler = (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        _workerProfileFloatingTextHandler = (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        // Benanntes Delegate-Feld fuer BuildingsVM FloatingText (statt anonymem Lambda -> Dispose-sicher)
        _buildingsFloatingTextHandler = (text, cat) => FloatingTextRequested?.Invoke(text, cat);

        StatisticsViewModel.ShowPrestigeDialog += OnShowPrestigeDialog;
        WorkerProfileViewModel.FloatingTextRequested += _workerProfileFloatingTextHandler;
        BuildingsViewModel.FloatingTextRequested += _buildingsFloatingTextHandler;
        GuildViewModel.CelebrationRequested += _guildCelebrationHandler;
        GuildViewModel.FloatingTextRequested += _guildFloatingTextHandler;

        // AscensionVM Events verdrahten (benannte Delegates fuer Dispose)
        _ascensionFloatingTextHandler = (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        _ascensionCelebrationHandler = () => CelebrationRequested?.Invoke();
        AscensionViewModel.FloatingTextRequested += _ascensionFloatingTextHandler;
        AscensionViewModel.CelebrationRequested += _ascensionCelebrationHandler;

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
        _gameLoopService.OrderExpired += OnOrderExpired;
        _gameLoopService.AutoCollectedDelivery += OnAutoCollectedDelivery;
        _gameLoopService.AutoAcceptedOrder += OnAutoAcceptedOrder;
        _localizationService.LanguageChanged += OnLanguageChanged;
        _eventService.EventStarted += OnEventStarted;
        _eventService.EventEnded += OnEventEnded;

        // Kontextuelles Hint-System (wird an DialogVM delegiert)
        _contextualHintService = contextualHintService;

        // ReviewService + PrestigeService verdrahten (per Constructor Injection)
        _reviewService = reviewService;
        _prestigeService = prestigeService
                           ?? throw new InvalidOperationException("IPrestigeService required");

        // MissionsFeatureViewModel per DI injiziert und Events verdrahten (benannte Delegates fuer Dispose)
        MissionsVM = missionsFeatureViewModel;
        _missionsFloatingTextHandler = (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        _missionsCelebrationHandler = () => CelebrationRequested?.Invoke();
        _missionsStreakRescuedHandler = () =>
        {
            OnPropertyChanged(nameof(LoginStreak));
            OnPropertyChanged(nameof(HasLoginStreak));
            OnPropertyChanged(nameof(ShowStreakBadge));
        };
        MissionsVM.FloatingTextRequested += _missionsFloatingTextHandler;
        MissionsVM.CelebrationRequested += _missionsCelebrationHandler;
        MissionsVM.NavigateToMiniGameRequested += OnMissionsNavigateToMiniGame;
        MissionsVM.CheckDeferredDialogsRequested += CheckDeferredDialogs;
        MissionsVM.StreakRescued += _missionsStreakRescuedHandler;

        // DialogViewModel per DI injiziert und Events verdrahten (benannte Delegates fuer Dispose)
        DialogVM = dialogViewModel;

        // EconomyFeatureViewModel initialisieren (nach DialogVM, da es DialogVM als IDialogService nutzt)
        InitializeEconomyVM();
        DialogVM.DeferredDialogCheckRequested += CheckDeferredDialogs;
        _dialogPrestigeSummaryGoToShopHandler = () => SelectBuildingsTab();
        _dialogFloatingTextHandler = (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        DialogVM.PrestigeSummaryGoToShopRequested += _dialogPrestigeSummaryGoToShopHandler;
        DialogVM.FloatingTextRequested += _dialogFloatingTextHandler;
        _prestigeService.PrestigeCompleted += OnPrestigeCompleted;
        _prestigeService.MilestoneReached += OnPrestigeMilestoneReached;

        // Rebirth-Event fuer First-Star-Hint
        if (_rebirthService != null)
            _rebirthService.RebirthCompleted += OnRebirthCompleted;

        // Worker-Level-Up Feedback (Sound + FloatingText)
        _workerService.WorkerLevelUp += OnWorkerLevelUp;

        // Notification + PlayGames Services (per Constructor Injection)
        _notificationService = notificationService;
        _playGamesService = playGamesService;
        _challengeConstraints = challengeConstraints;

        // Back-Press Helper verdrahten (benannte Methode statt Lambda fuer Dispose-Abmeldung)
        _backPressHelper.ExitHintRequested += OnBackPressExitHint;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Benannter Handler fuer BackPressHelper.ExitHintRequested (statt anonymem Lambda, damit Dispose abmelden kann).
    /// </summary>
    private void OnBackPressExitHint(string msg) => ExitHintRequested?.Invoke(msg);

    // Debounce für teure Max-Modus-Berechnung (GetMaxAffordableUpgrades iteriert durch hunderte Levels)
    private DateTime _lastMaxModeCalc = DateTime.MinValue;

    private void OnMoneyChanged(object? sender, MoneyChangedEventArgs e)
    {
        Money = e.NewAmount;
        // Phase 9: Smooth animierter Geld-Counter
        AnimateMoneyTo(e.NewAmount);

        // Geld-abhängige Workshop-Flags aktualisieren (CanAfford, BulkCost bei Max-Modus)
        // Bei x1/x10/x100: BulkUpgradeCost hängt nur vom Level ab (ändert sich nicht pro Tick),
        // daher nur CanAffordUpgrade aktualisieren statt teure Math.Pow-Schleife
        bool isMaxMode = BulkBuyAmount == 0;

        if (isMaxMode)
        {
            // Max-Modus: Nur alle 2s neu berechnen wenn Dashboard nicht sichtbar,
            // oder sofort wenn Dashboard sichtbar (dort sieht der User die Werte)
            var now = DateTime.UtcNow;
            bool shouldRecalc = IsDashboardActive || (now - _lastMaxModeCalc).TotalSeconds >= 2.0;
            if (!shouldRecalc)
            {
                // Nur CanAfford-Flags billig aktualisieren (Vergleiche statt Math.Pow-Schleifen)
                foreach (var workshop in Workshops)
                {
                    workshop.CanAffordUpgrade = e.NewAmount >= workshop.BulkUpgradeCost;
                    workshop.CanAffordWorker = e.NewAmount >= workshop.HireWorkerCost;
                }
                return;
            }
            _lastMaxModeCalc = now;

            // Max-Modus: Anzahl leistbarer Upgrades hängt vom Geld ab → muss neu berechnet werden
            var stateWorkshops = _gameStateService.State.Workshops;
            _workshopLookupCache.Clear();
            for (int i = 0; i < stateWorkshops.Count; i++)
                _workshopLookupCache[stateWorkshops[i].Type] = stateWorkshops[i];

            foreach (var workshop in Workshops)
            {
                _workshopLookupCache.TryGetValue(workshop.Type, out var ws);
                SetBulkUpgradeCost(workshop, ws, e.NewAmount);
                workshop.CanAffordWorker = e.NewAmount >= workshop.HireWorkerCost;
            }
        }
        else
        {
            // x1/x10/x100: BulkUpgradeCost ist level-abhängig (invariant bei Geld-Tick),
            // nur CanAfford-Flags aktualisieren (reine Vergleiche, kein Math.Pow)
            foreach (var workshop in Workshops)
            {
                workshop.CanAffordUpgrade = e.NewAmount >= workshop.BulkUpgradeCost;
                workshop.CanAffordUnlock = workshop.CanBuyUnlock && e.NewAmount >= workshop.UnlockCost;
                workshop.CanAffordWorker = e.NewAmount >= workshop.HireWorkerCost;
            }
        }
    }

    private void OnGoldenScrewsChanged(object? sender, GoldenScrewsChangedEventArgs e)
    {
        GoldenScrewsDisplay = e.NewAmount.ToString("N0");

        // Goldschrauben-Erklärung beim allerersten Erhalt
        if (e.OldAmount == 0 && e.NewAmount > 0)
            _contextualHintService.TryShowHint(ContextualHints.GoldenScrews);

        // PP-3: FloatingText bei Goldschrauben-Ausgaben
        int diff = e.NewAmount - e.OldAmount;
        if (diff < 0)
            FloatingTextRequested?.Invoke($"{diff} GS", "warning");
        else if (diff > 0)
            FloatingTextRequested?.Invoke($"+{diff} GS", "goldscrews");
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

        // Progressive Disclosure: Wird automatisch via [NotifyPropertyChangedFor] auf _playerLevel ausgelöst

        // Pulse-Animation bei JEDEM Level-Up (dezent, kein Dialog)
        DialogVM.IsLevelUpPulsing = true;
        _levelPulseTimer?.Stop();
        _levelPulseTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _levelPulseTimer.Tick -= OnLevelPulseTimeout;
        _levelPulseTimer.Tick += OnLevelPulseTimeout;
        _levelPulseTimer.Start();

        // Sound + FloatingText bei jedem Level-Up
        _audioService.PlaySoundAsync(GameSound.ButtonTap).FireAndForget();
        FloatingTextRequested?.Invoke($"Level {e.NewLevel}!", "level");

        // Milestone-Bonus prüfen (10/25/50/100/250/500/1000)
        foreach (var (level, screws) in _milestones)
        {
            if (e.NewLevel == level)
            {
                _gameStateService.AddGoldenScrews(screws);

                // Sound + Celebration nur bei Milestones
                _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();
                _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
                CelebrationRequested?.Invoke();
                CeremonyRequested?.Invoke(CeremonyType.LevelMilestone,
                    $"Level {e.NewLevel}!", $"+{screws} Goldschrauben");

                // FloatingText mit Level + Goldschrauben-Bonus
                FloatingTextRequested?.Invoke(
                    $"Level {e.NewLevel}! +{screws} \u2699", "level");
                break;
            }
        }

        // Tab-Freischaltung: Hinweis wenn ein neuer Tab verfügbar wird
        CheckTabUnlockNotification(e.NewLevel);

        // Kontextuelle Hints bei Level-Meilensteinen (passend zu Progressive Disclosure)
        // Nicht anzeigen wenn ein anderer Dialog offen ist (z.B. Prestige-Summary)
        if (IsAnyDialogVisible) return;

        if (e.NewLevel == LevelThresholds.HintWorkerUnlock)
            _contextualHintService.TryShowHint(ContextualHints.WorkerUnlock);
        else if (e.NewLevel == LevelThresholds.HintQuickJobs)
            _contextualHintService.TryShowHint(ContextualHints.QuickJobs);
        else if (e.NewLevel == LevelThresholds.HintCrafting)
            _contextualHintService.TryShowHint(ContextualHints.CraftingHint);
        else if (e.NewLevel == LevelThresholds.HintManagerUnlock)
            _contextualHintService.TryShowHint(ContextualHints.ManagerUnlock);
        else if (e.NewLevel == LevelThresholds.HintAutomation)
            _contextualHintService.TryShowHint(ContextualHints.Automation);
        else if (e.NewLevel == LevelThresholds.HintMasterTools)
            _contextualHintService.TryShowHint(ContextualHints.MasterToolsUnlock);
        else if (e.NewLevel == LevelThresholds.HintPrestige)
            _contextualHintService.TryShowHint(ContextualHints.PrestigeHint);

        // Story-Kapitel prüfen
        CheckForNewStoryChapter();

        // Review-Milestone prüfen
        _reviewService?.OnMilestone("level", e.NewLevel);
        CheckReviewPrompt();

        // Leaderboard-Score aktualisieren (fire-and-forget)
        if (_playGamesService?.IsSignedIn == true)
            _playGamesService.SubmitScoreAsync("leaderboard_player_level", e.NewLevel).SafeFireAndForget();
    }

    private void OnLevelPulseTimeout(object? sender, EventArgs e)
    {
        DialogVM.IsLevelUpPulsing = false;
        _levelPulseTimer?.Stop();
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

        // Ascension-Hint: Prüfen ob erstmals 3+ Legende-Prestiges erreicht
        if (_gameStateService.State.Prestige.LegendeCount >= 3)
            _contextualHintService.TryShowHint(ContextualHints.AscensionAvailable);

        _reviewService?.OnMilestone("prestige", prestigeCount);
        CheckReviewPrompt();

        // Story-Kapitel prüfen (Prestige-bezogene Kapitel sofort triggern)
        CheckForNewStoryChapter();
    }

    private void OnPrestigeMilestoneReached(object? sender, PrestigeMilestoneEventArgs e)
    {
        var text = string.Format(
            _localizationService.GetString("PrestigeMilestoneReached") ?? "Prestige-Meilenstein! +{0} Goldschrauben",
            e.GoldenScrewReward);
        FloatingTextRequested?.Invoke(text, "currency");
        CelebrationRequested?.Invoke();
        _audioService.PlaySoundAsync(GameSound.Perfect).FireAndForget();
    }

    private void OnRebirthCompleted(object? sender, WorkshopType type)
    {
        // Erster-Stern-Hint nach erstem Rebirth (erklärt Stern-Boni)
        _contextualHintService.TryShowHint(ContextualHints.FirstStar);
    }

    private void CheckReviewPrompt()
    {
        if (_reviewService?.ShouldPromptReview() == true)
        {
            _reviewService.MarkReviewPrompted();
            App.ReviewPromptRequested?.Invoke();
        }
    }

    /// <summary>
    /// Worker-Level-Up: FloatingText mit Name + neuem Level und Sound.
    /// </summary>
    private void OnWorkerLevelUp(object? sender, Worker worker)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var levelUpText = string.Format(
                _localizationService.GetString("WorkerLevelUp") ?? "{0} ist jetzt Level {1}!",
                worker.Name, worker.ExperienceLevel);
            FloatingTextRequested?.Invoke(levelUpText, "level");
            _audioService.PlaySoundAsync(GameSound.MoneyEarned).FireAndForget();
        });
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

        // Workshop-Detail-Hint nach erstem Upgrade zeigen
        if (!_contextualHintService.HasSeenHint(ContextualHints.WorkshopDetail.Id))
        {
            ShowTutorialHint = false;
            _contextualHintService.TryShowHint(ContextualHints.WorkshopDetail);
        }

        // Rebirth-Hint: Erster Workshop erreicht Level 1000
        if (e.NewLevel >= Workshop.MaxLevel)
            _contextualHintService.TryShowHint(ContextualHints.RebirthReady);

        // Multiplikator-Meilensteine (Bumpy Progression)
        if (!IsHoldingUpgrade && Workshop.IsMilestoneLevel(e.NewLevel))
        {
            decimal milestoneMultiplier = Workshop.GetMilestoneMultiplierForLevel(e.NewLevel);
            var workshopName = _localizationService.GetString(e.WorkshopType.GetLocalizationKey());
            string boostText = $"x{milestoneMultiplier:0.#} {_localizationService.GetString("IncomeBoost") ?? "EINKOMMENS-BOOST"}!";

            FloatingTextRequested?.Invoke(boostText, "golden_screws");
            _audioService.PlaySoundAsync(GameSound.LevelUp).FireAndForget();

            // Größere Zeremonien bei höheren Meilensteinen
            if (e.NewLevel >= LevelThresholds.WorkshopCeremonyThreshold)
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
            foreach (var (level, screws) in s_workshopMilestones)
            {
                if (e.NewLevel == level)
                {
                    _gameStateService.AddGoldenScrews(screws);
                    var workshopName = _localizationService.GetString(e.WorkshopType.GetLocalizationKey());
                    FloatingTextRequested?.Invoke(
                        $"{workshopName} Lv.{e.NewLevel}! +{screws} \u2699", "level");
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

        // Hint beim ersten Auftragsabschluss
        if (_gameStateService.State.Statistics.TotalOrdersCompleted == 1)
            _contextualHintService.TryShowHint(ContextualHints.OrderCompleted);

        // Story-Kapitel prüfen
        CheckForNewStoryChapter();

        // Review-Milestone prüfen
        _reviewService?.OnMilestone("orders", _gameStateService.State.Statistics.TotalOrdersCompleted);
        CheckReviewPrompt();

        // Ziel-Cache invalidieren (Auftragsabschluss könnte Ziel erfüllen)
        _goalService.Invalidate();
    }

    // OnChallengeProgressChanged → extrahiert nach MissionsFeatureViewModel

    private async void OnShowPrestigeDialog(object? sender, EventArgs e)
    {
        try
        {
            await ShowPrestigeConfirmationAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] {nameof(OnShowPrestigeDialog)} Fehler: {ex.Message}");
        }
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

            MissionsVM.MasterToolsCollected = _gameStateService.State.CollectedMasterTools.Count;
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

    /// <summary>
    /// Handler: Aktiver Auftrag ist abgelaufen (Deadline überschritten).
    /// Setzt UI-State zurück, damit kein "Geister-Auftrag" angezeigt wird.
    /// </summary>
    private void OnOrderExpired(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveOrder = false;
            ActiveOrder = null;
            var msg = _localizationService.GetString("OrderExpiredNotification") ?? "Auftrag abgelaufen!";
            FloatingTextRequested?.Invoke(msg, "warning");
            _audioService.PlaySoundAsync(GameSound.Miss).FireAndForget();
            RefreshOrders();
        });
    }

    /// <summary>
    /// Handler: Automation hat eine Lieferung automatisch eingesammelt.
    /// Aktualisiert die Lieferungs-Anzeige in der UI.
    /// </summary>
    private void OnAutoCollectedDelivery(object? sender, SupplierDelivery delivery)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasPendingDelivery = false;
            FloatingTextRequested?.Invoke(
                $"{_localizationService.GetString("DeliveryCollected") ?? "Lieferung eingesammelt"}!", "Delivery");
        });
    }

    /// <summary>
    /// Handler: Automation hat einen Auftrag automatisch angenommen.
    /// Aktualisiert Auftrags-Anzeige und verfügbare Aufträge.
    /// </summary>
    private void OnAutoAcceptedOrder(object? sender, Order order)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HasActiveOrder = true;
            ActiveOrder = order;
            RefreshOrders();
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

            // Event-Name nur neu laden wenn sich der Event-Key geaendert hat
            if (_cachedActiveEventKey != activeEvent.NameKey)
            {
                _cachedActiveEventKey = activeEvent.NameKey;
                _cachedActiveEventName = _localizationService.GetString(activeEvent.NameKey);
            }
            ActiveEventName = _cachedActiveEventName ?? string.Empty;
            UpdateEventTimer();
        }
        else if (HasActiveEvent)
        {
            HasActiveEvent = false;
            _cachedActiveEventKey = null;
        }

        // Saisonaler Modifikator (nur bei Monatswechsel neu berechnen)
        var month = DateTime.UtcNow.Month;
        if (month != _cachedSeasonMonth)
        {
            _cachedSeasonMonth = month;
            SeasonalModifierText = month switch
            {
                3 or 4 or 5 => _localizationService.GetString("SeasonSpring"),
                6 or 7 or 8 => _localizationService.GetString("SeasonSummer"),
                9 or 10 or 11 => _localizationService.GetString("SeasonAutumn"),
                _ => _localizationService.GetString("SeasonWinter")
            };
        }
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
        DialogVM.AchievementName = string.IsNullOrEmpty(title) ? achievement.TitleFallback : title;
        var desc = _localizationService.GetString(achievement.DescriptionKey);
        DialogVM.AchievementDescription = string.IsNullOrEmpty(desc) ? achievement.DescriptionFallback : desc;
        DialogVM.IsAchievementDialogVisible = true;
        CelebrationRequested?.Invoke();

        ShowAchievementUnlocked?.Invoke(this, achievement);
    }

    private void OnPremiumStatusChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ShowAds));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Lokalisierungs-Caches aktualisieren
        _cachedNetIncomeLabel = _localizationService.GetString("NetIncome") ?? "Netto";
        _cachedActiveEventKey = null; // Event-Name bei Sprachwechsel neu laden
        EconomyVM.InvalidatePrestigeBannerCache(); // Prestige-Banner mit neuen Texten neu berechnen

        // Statische Renderer-Strings aktualisieren
        WorkshopGameCardRenderer.UpdateLocalizedStrings(
            _localizationService.GetString("TapToUnlock") ?? "Tap to unlock",
            _localizationService.GetString("AtLevelShort") ?? "From Level {0}");

        // Alle lokalisierten Display-Texte aktualisieren
        MissionsVM.RefreshQuickJobs();
        MissionsVM.MarkChallengesDirty();
        MissionsVM.RefreshChallenges();
        RefreshWorkshops();

        // Child-VMs aktualisieren
        WorkerMarketViewModel.UpdateLocalizedTexts();
        WorkerProfileViewModel.UpdateLocalizedTexts();
        BuildingsViewModel.UpdateLocalizedTexts();
        ResearchViewModel.UpdateLocalizedTexts();
        ShopViewModel.LoadShopData();
        ShopViewModel.LoadTools();
        GuildViewModel.UpdateLocalizedTexts();
        ManagerViewModel.UpdateLocalizedTexts();
        CraftingViewModel.UpdateLocalizedTexts();
        LuckySpinViewModel.UpdateLocalizedTexts();
        BattlePassViewModel.UpdateLocalizedTexts();
        TournamentViewModel.UpdateLocalizedTexts();
        SeasonalEventViewModel.UpdateLocalizedTexts();
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

        // Tick-Counter für zeitgesteuerte UI-Updates (Worker-Warnung, etc.)
        _floatingTextCounter++;

        // QuickJob-Timer aktualisieren + Rotation (delegiert an MissionsVM)
        MissionsVM.UpdateQuickJobTimer();

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
        // Boost-Indikator separat prüfen (SpeedBoost kann unabhängig von Rush ablaufen)
        else if (ShowBoostIndicator && !_gameStateService.State.IsSpeedBoostActive && !_gameStateService.State.IsRushBoostActive)
        {
            UpdateBoostIndicator();
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

            // Dashboard/Missionen-spezifische Updates nur wenn sichtbar (spart ~20 PropertyChanged)
            if (IsDashboardActive || IsMissionenActive)
            {
                MissionsVM.RefreshChallenges();
            }

            // Imperium-spezifische Updates nur wenn sichtbar
            if (IsBuildingsActive || IsDashboardActive)
            {
                RefreshReputation(state);
                RefreshPrestigeBanner(state);
            }
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
            // Lucky Spin + Welcome Back Timer (delegiert an MissionsVM)
            if (IsMissionenActive || IsDashboardActive)
            {
                MissionsVM.UpdatePeriodicState();
            }

            // Worker-Warnung nur aktualisieren wenn Imperium/Dashboard sichtbar
            if (IsBuildingsActive || IsDashboardActive)
                UpdateWorkerWarning(state);

            // Soft-Cap-Indikator aktualisieren (nur wenn Dashboard sichtbar)
            if (IsDashboardActive)
            {
                // Einmaliger Hinweis beim ersten Erreichen des Soft-Caps
                if (state.IsSoftCapActive && !IsSoftCapActive)
                {
                    FloatingTextRequested?.Invoke(
                        _localizationService.GetString("SoftCapReached") ?? "Bonus-Decke erreicht!",
                        "warning");
                }

                IsSoftCapActive = state.IsSoftCapActive;
                if (state.IsSoftCapActive && state.SoftCapReductionPercent > 0)
                    SoftCapText = $"-{state.SoftCapReductionPercent}%";
                else if (!state.IsSoftCapActive)
                    SoftCapText = "";
            }

            // Welcome Back Timer ist jetzt in MissionsVM.UpdatePeriodicState()
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
    // TAB-UNLOCK BENACHRICHTIGUNGEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob beim neuen Level ein Tab freigeschaltet wird und zeigt einen Hinweis.
    /// </summary>
    private void CheckTabUnlockNotification(int newLevel)
    {
        string[] tabNames = [
            _localizationService.GetString("TabWerkstatt") ?? "Workshop",
            _localizationService.GetString("TabImperium") ?? "Imperium",
            _localizationService.GetString("TabMissionen") ?? "Missionen",
            _localizationService.GetString("TabGilde") ?? "Gilde",
            _localizationService.GetString("TabShop") ?? "Shop"
        ];

        for (int i = 0; i < TabUnlockLevels.Length; i++)
        {
            if (TabUnlockLevels[i] == newLevel)
            {
                var unlockText = string.Format(
                    _localizationService.GetString("TabUnlocked") ?? "{0} freigeschaltet!",
                    tabNames[i]);
                FloatingTextRequested?.Invoke(unlockText, "golden_screws");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    internal static string FormatMoney(decimal amount) => MoneyFormatter.FormatCompact(amount);

    /// <summary>Interner Wrapper fuer PropertyChanged-Benachrichtigung (EconomyFeatureVM Zugriff).</summary>
    internal new void OnPropertyChanged(string? propertyName = null)
        => base.OnPropertyChanged(propertyName);

    /// <summary>
    /// Aktualisiert die Netto-Einkommen-Anzeige im Dashboard-Header.
    /// Zeigt Brutto minus Kosten mit Farbindikator (rot wenn negativ).
    /// </summary>
    internal void UpdateNetIncomeHeader(GameState state)
    {
        var netIncome = state.TotalIncomePerSecond - state.TotalCostsPerSecond;
        IsNetIncomeNegative = netIncome < 0;
        NetIncomeColor = netIncome < 0 ? "#FF5722" : "#FFFFFFAA";

        // Gecachter Label-Text (Invalidierung in OnLanguageChanged)
        NetIncomeHeaderDisplay = $"{_cachedNetIncomeLabel}: {MoneyFormatter.FormatPerSecond(netIncome, 1)}";
    }

    /// <summary>
    /// Prüft alle Worker auf Erschöpfung (Fatigue>80), Unzufriedenheit (Mood kleiner 30) und Kündigungsrisiko (Mood kleiner 15).
    /// Zeigt die dringendste Warnung im Dashboard-Banner.
    /// </summary>
    internal void UpdateWorkerWarning(GameState state)
    {
        int tiredCount = 0, unhappyCount = 0, quitRisk = 0;
        string? worstWorkshopName = null;
        decimal worstScore = decimal.MaxValue;

        foreach (var ws in state.Workshops)
        {
            if (!ws.IsUnlocked) continue;
            foreach (var w in ws.Workers)
            {
                if (w.Fatigue > 80) tiredCount++;
                if (w.Mood < 30) unhappyCount++;
                if (w.Mood < 15) quitRisk++;

                // Schlimmsten Workshop tracken (niedrigste Mood oder höchste Fatigue)
                decimal score = w.Mood - w.Fatigue * 0.5m;
                if (score < worstScore && (w.Fatigue > 80 || w.Mood < 30))
                {
                    worstScore = score;
                    worstWorkshopName = _localizationService.GetString(ws.Type.GetLocalizationKey()) ?? ws.Type.ToString();
                }
            }
        }

        HasWorkerWarning = tiredCount > 0 || unhappyCount > 0;

        // Kontext-Info: Welcher Workshop ist betroffen
        string context = worstWorkshopName != null ? $" ({worstWorkshopName})" : "";

        if (quitRisk > 0)
        {
            WorkerWarningText = string.Format(
                _localizationService.GetString("WorkerQuitRisk") ?? "{0} Arbeiter drohen zu kündigen!",
                quitRisk) + context;
        }
        else if (unhappyCount > 0)
        {
            WorkerWarningText = string.Format(
                _localizationService.GetString("WorkerUnhappy") ?? "{0} Arbeiter unzufrieden",
                unhappyCount) + context;
        }
        else if (tiredCount > 0)
        {
            WorkerWarningText = string.Format(
                _localizationService.GetString("WorkerTired") ?? "{0} Arbeiter erschöpft",
                tiredCount) + context;
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
            _moneyAnimActive = false;
            return;
        }

        _moneyAnimActive = true;
    }

    /// <summary>
    /// Wird vom MainView Render-Timer aufgerufen (25fps).
    /// Ersetzt den separaten Timer und reduziert UI-Thread-Callbacks.
    /// </summary>
    public void UpdateMoneyAnimation()
    {
        if (!_moneyAnimActive) return;

        var diff = _targetMoney - _displayedMoney;

        if (Math.Abs(diff) < 1m)
        {
            _displayedMoney = _targetMoney;
            MoneyDisplay = FormatMoney(_displayedMoney);
            _moneyAnimActive = false;
            return;
        }

        // Exponentielles Easing: schnell am Anfang, langsamer am Ende
        _displayedMoney += diff * MoneyAnimSpeed;
        MoneyDisplay = FormatMoney(_displayedMoney);
    }

    internal static GameIconKind GetWorkshopIconKind(WorkshopType type, int level = 1) => type switch
    {
        WorkshopType.Carpenter when level >= 26 => GameIconKind.Factory,
        WorkshopType.Carpenter when level >= 11 => GameIconKind.TableFurniture,
        WorkshopType.Carpenter => GameIconKind.HandSaw,
        WorkshopType.Plumber when level >= 26 => GameIconKind.WaterPump,
        WorkshopType.Plumber when level >= 11 => GameIconKind.Pipe,
        WorkshopType.Plumber => GameIconKind.Pipe,
        WorkshopType.Electrician when level >= 26 => GameIconKind.TransmissionTower,
        WorkshopType.Electrician when level >= 11 => GameIconKind.LightningBolt,
        WorkshopType.Electrician => GameIconKind.Flash,
        WorkshopType.Painter when level >= 26 => GameIconKind.Draw,
        WorkshopType.Painter when level >= 11 => GameIconKind.SprayBottle,
        WorkshopType.Painter => GameIconKind.Palette,
        WorkshopType.Roofer when level >= 26 => GameIconKind.HomeGroup,
        WorkshopType.Roofer when level >= 11 => GameIconKind.HomeRoof,
        WorkshopType.Roofer => GameIconKind.HomeRoof,
        WorkshopType.Contractor when level >= 26 => GameIconKind.DomainPlus,
        WorkshopType.Contractor when level >= 11 => GameIconKind.OfficeBuilding,
        WorkshopType.Contractor => GameIconKind.OfficeBuildingOutline,
        WorkshopType.Architect => GameIconKind.Compass,
        WorkshopType.GeneralContractor => GameIconKind.HardHat,
        _ => GameIconKind.Wrench
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
        if (_gameStateService.State.Settings.NotificationsEnabled)
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

        // Phase 9: Money-Animation Flag zurücksetzen
        _moneyAnimActive = false;
        _levelPulseTimer?.Stop();

        // Stop the game loop and save
        _gameLoopService.Stop();

        // Unsubscribe child VM navigation events
        ShopViewModel.NavigationRequested -= OnChildNavigation;
        StatisticsViewModel.NavigationRequested -= OnChildNavigation;
        AchievementsViewModel.NavigationRequested -= OnChildNavigation;
        SettingsViewModel.NavigationRequested -= OnChildNavigation;
        WorkshopViewModel.NavigationRequested -= OnChildNavigation;
        OrderViewModel.NavigationRequested -= OnChildNavigation;
        foreach (var mg in MiniGames.All)
            mg.NavigationRequested -= OnChildNavigation;
        ManagerViewModel.NavigationRequested -= OnChildNavigation;
        TournamentViewModel.NavigationRequested -= OnChildNavigation;
        SeasonalEventViewModel.NavigationRequested -= OnChildNavigation;
        BattlePassViewModel.NavigationRequested -= OnChildNavigation;
        GuildViewModel.NavigationRequested -= OnChildNavigation;
        CraftingViewModel.NavigationRequested -= OnChildNavigation;
        AscensionViewModel.NavigationRequested -= OnChildNavigation;
        LuckySpinViewModel.NavigationRequested -= _luckySpinNavHandler;
        WorkerMarketViewModel.NavigationRequested -= OnChildNavigation;
        WorkerProfileViewModel.NavigationRequested -= OnChildNavigation;
        BuildingsViewModel.NavigationRequested -= OnChildNavigation;
        ResearchViewModel.NavigationRequested -= OnChildNavigation;

        // Unsubscribe child VM events
        StatisticsViewModel.ShowPrestigeDialog -= OnShowPrestigeDialog;
        WorkerProfileViewModel.FloatingTextRequested -= _workerProfileFloatingTextHandler;
        BuildingsViewModel.FloatingTextRequested -= _buildingsFloatingTextHandler;
        GuildViewModel.CelebrationRequested -= _guildCelebrationHandler;
        GuildViewModel.FloatingTextRequested -= _guildFloatingTextHandler;
        AscensionViewModel.FloatingTextRequested -= _ascensionFloatingTextHandler;
        AscensionViewModel.CelebrationRequested -= _ascensionCelebrationHandler;

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
        _gameLoopService.OrderExpired -= OnOrderExpired;
        _gameLoopService.AutoCollectedDelivery -= OnAutoCollectedDelivery;
        _gameLoopService.AutoAcceptedOrder -= OnAutoAcceptedOrder;
        _achievementService.AchievementUnlocked -= OnAchievementUnlocked;
        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _eventService.EventStarted -= OnEventStarted;
        _eventService.EventEnded -= OnEventEnded;
        // Daily/Weekly/WelcomeBack Event-Unsubscribes sind in MissionsFeatureViewModel.Dispose()

        DialogVM.DeferredDialogCheckRequested -= CheckDeferredDialogs;
        DialogVM.PrestigeSummaryGoToShopRequested -= _dialogPrestigeSummaryGoToShopHandler;
        DialogVM.FloatingTextRequested -= _dialogFloatingTextHandler;
        DialogVM.Cleanup();

        _prestigeService.PrestigeCompleted -= OnPrestigeCompleted;
        _prestigeService.MilestoneReached -= OnPrestigeMilestoneReached;
        if (_rebirthService != null)
            _rebirthService.RebirthCompleted -= OnRebirthCompleted;
        _workerService.WorkerLevelUp -= OnWorkerLevelUp;
        _backPressHelper.ExitHintRequested -= OnBackPressExitHint;

        // EconomyFeatureVM Events abmelden
        EconomyVM.FloatingTextRequested -= _economyFloatingTextHandler;
        EconomyVM.CelebrationRequested -= _economyCelebrationHandler;

        // MissionsFeatureVM Events abmelden + disposen
        MissionsVM.FloatingTextRequested -= _missionsFloatingTextHandler;
        MissionsVM.CelebrationRequested -= _missionsCelebrationHandler;
        MissionsVM.StreakRescued -= _missionsStreakRescuedHandler;
        MissionsVM.NavigateToMiniGameRequested -= OnMissionsNavigateToMiniGame;
        MissionsVM.CheckDeferredDialogsRequested -= CheckDeferredDialogs;
        MissionsVM.Dispose();

        GuildViewModel.Dispose();
        ShopViewModel.Dispose();
        WorkshopViewModel.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
