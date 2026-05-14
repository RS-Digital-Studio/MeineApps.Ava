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
///   MainViewModel.cs - Felder, Constructor, Properties, Event-Handlers, Helpers, Dispose
///   MainViewModel.Navigation.cs - Tab-Auswahl, Child-Navigation, Back-Button
///   MainViewModel.Dialogs.cs - Weiterleitungen an DialogVM, Prestige-Durchführung
///   MainViewModel.Economy.cs - Workshop-Kauf/Upgrade, Orders, Rush, Delivery, Prestige-Banner
///   MainViewModel.Missions.cs - Daily Challenges, Weekly Missions, Quick Jobs, Lucky Spin
///   MainViewModel.Init.cs - InitializeAsync, Offline-Earnings, Daily Reward, Cloud-Save
/// Dialog-Logik extrahiert nach DialogViewModel.cs (Alert, Confirm, Story, Hint, Achievement, Prestige-Dialog).
/// </summary>
public sealed partial class MainViewModel : ViewModelBase, IDisposable, Services.Interfaces.INavigationHost, Services.Interfaces.IWelcomeFlowHost, Services.Interfaces.IStartupHost
{
    // Phase-1-Services (Refactoring 17.04.2026 — Plan velvety-booping-peacock).
    // Delegieren vorerst zurueck an MainViewModel. /3 zieht Logik in die Services um.
    private readonly Services.Interfaces.INavigationService? _navigationService;
    private readonly Services.Interfaces.IDialogOrchestrator? _dialogOrchestrator;
    private readonly Services.Interfaces.IMiniGameNavigator? _miniGameNavigator;
    /// <summary>Zentraler UI-Effekt-Bus (FloatingText/Celebration/Ceremony).</summary>
    private readonly Services.Interfaces.IUiEffectBus _uiEffectBus;
    /// <summary>Spielstart-Sequenz (Load, Cloud-Save, Welcome-Flow, GameLoop-Start).</summary>
    private readonly Services.Interfaces.IGameStartupCoordinator _startupCoordinator;

    // INavigationHost-Implementierung: siehe MainViewModel.Host.cs ()

    private readonly IGameStateService _gameStateService;
    private readonly IGameLoopService _gameLoopService;
    private readonly IOrderGeneratorService _orderGeneratorService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IDailyRewardService _dailyRewardService;
    private readonly IAchievementService _achievementService;
    private readonly ISaveGameService _saveGameService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAdService _adService;
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
    // Telemetrie (REST via FirebaseService, keine nativen SDKs noetig)
    private readonly IAnalyticsService? _analyticsService;
    /// <summary>Cinematic-Logik aus MainViewModel extrahiert.</summary>
    private readonly ICinematicCoordinator? _cinematicCoordinator;
    /// <summary>Reputation-Tier-Up-Effekte extrahiert.</summary>
    private readonly IReputationTierEffects? _reputationTierEffects;
    /// <summary>Optionales Spotlight-Overlay-VM (per DI injiziert).</summary>
    public FtueOverlayViewModel? FtueOverlayVM { get; }
    private readonly IRemoteConfigService? _remoteConfigService;
    private readonly IWeeklyMissionService _weeklyMissionService;
    private readonly IEquipmentService _equipmentService;
    private readonly IGoalService _goalService;
    private readonly IWorkerService _workerService;
    private readonly IRebirthService? _rebirthService;
    private readonly ITournamentService? _tournamentService;
    private readonly INotificationCenterService _notificationCenterService;
    private bool _disposed;
    private QuickJob? _activeQuickJob;
    private bool _quickJobMiniGamePlayed;
    private bool _isTournamentRound;

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
    private const decimal MoneyAnimSpeed = GameBalanceConstants.MoneyAnimationInterpolationFactor;

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
    // HeaderVM PropertyChanged-Forward fuer computed Properties (PlayerLevel → ShowCraftingResearch etc.)
    private readonly System.ComponentModel.PropertyChangedEventHandler _headerVmPropertyChangedHandler;

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

    /// <summary>
    /// Alle Child-VMs die NavigationRequested per OnChildNavigation verdrahten.
    /// Ermoeglicht Subscribe/Unsubscribe per Schleife statt 19+ Einzelzeilen.
    /// LuckySpinViewModel ist NICHT enthalten (hat eigenen Handler).
    /// </summary>
    private readonly INavigable[] _navigableChildren;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public MainViewModel(
        IGameStateService gameStateService,
        IGameLoopService gameLoopService,
        IOrderGeneratorService orderGeneratorService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IDailyRewardService dailyRewardService,
        IAchievementService achievementService,
        IPurchaseService purchaseService,
        IAdService adService,
        ISaveGameService saveGameService,
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
        WarehouseSectionViewModel warehouseSectionViewModel,
        MarketViewModel marketViewModel,
        AscensionViewModel ascensionViewModel,
        IWeeklyMissionService weeklyMissionService,
        IEquipmentService equipmentService,
        LuckySpinViewModel luckySpinViewModel,
        GameJuiceEngine gameJuiceEngine,
        IGoalService goalService,
        IWorkerService workerService,
        IContextualHintService contextualHintService,
        DialogViewModel dialogViewModel,
        MissionsFeatureViewModel missionsFeatureViewModel,
        HeaderViewModel headerViewModel,
        PrestigeBannerViewModel prestigeBannerViewModel,
        GoalBannerViewModel goalBannerViewModel,
        WelcomeFlowViewModel welcomeFlowViewModel,
        NotificationCenterViewModel notificationCenterViewModel,
        INotificationCenterService notificationCenterService,
        Services.Interfaces.IUiEffectBus uiEffectBus,
        Services.Interfaces.IGameStartupCoordinator startupCoordinator,
        ITournamentService? tournamentService = null,
        IRebirthService? rebirthService = null,
        IStoryService? storyService = null,
        IReviewService? reviewService = null,
        IPrestigeService? prestigeService = null,
        INotificationService? notificationService = null,
        IPlayGamesService? playGamesService = null,
        IChallengeConstraintService? challengeConstraints = null,
        Services.Interfaces.INavigationService? navigationService = null,
        Services.Interfaces.IDialogOrchestrator? dialogOrchestrator = null,
        Services.Interfaces.IMiniGameNavigator? miniGameNavigator = null,
        IAnalyticsService? analyticsService = null,
        IRemoteConfigService? remoteConfigService = null,
        ICinematicCoordinator? cinematicCoordinator = null,
        IReputationTierEffects? reputationTierEffects = null,
        FtueOverlayViewModel? ftueOverlayVm = null)
    {
        _navigationService = navigationService;
        _dialogOrchestrator = dialogOrchestrator;
        _miniGameNavigator = miniGameNavigator;
        _uiEffectBus = uiEffectBus;
        _startupCoordinator = startupCoordinator;
        // Startup-Sequenz an GameStartupCoordinator delegiert — MainViewModel ist nur noch
        // die schmale IStartupHost-Bruecke (IsLoading + EconomyVM-Refreshes).
        _startupCoordinator.AttachHost(this);
        _analyticsService = analyticsService;
        _remoteConfigService = remoteConfigService;
        _cinematicCoordinator = cinematicCoordinator;
        _reputationTierEffects = reputationTierEffects;
        FtueOverlayVM = ftueOverlayVm;
        // Host-Attach damit Services Navigation/Dialog-Kaskade an MainViewModel delegieren koennen
        _navigationService?.AttachHost(this);
        _dialogOrchestrator?.AttachHost(this);
        _miniGameNavigator?.AttachHost(this);

        _gameStateService = gameStateService;
        _gameLoopService = gameLoopService;
        _orderGeneratorService = orderGeneratorService;

        // v2.0.35 Feature D: Toast/FloatingText bei neuem Live-Auftrag (OrderSpawned).
        // Premium-Auftraege (IsPremium=true) bekommen den "gold"-Stil (sichtbarer), Standard-Live
        // den neutralen "info"-Stil.
        _orderGeneratorService.OrderSpawned += OnLiveOrderSpawned;
        _audioService = audioService;
        _localizationService = localizationService;
        _cachedNetIncomeLabel = _localizationService.GetString("NetIncome") ?? "Net Income";
        _dailyRewardService = dailyRewardService;
        _achievementService = achievementService;
        _purchaseService = purchaseService;
        _adService = adService;
        _saveGameService = saveGameService;
        _dailyChallengeService = dailyChallengeService;
        _rewardedAdService = rewardedAdService;
        _eventService = eventService;
        _storyService = storyService;
        _weeklyMissionService = weeklyMissionService;
        _equipmentService = equipmentService;
        _goalService = goalService;
        _workerService = workerService;
        _rebirthService = rebirthService;
        _tournamentService = tournamentService;
        GameJuiceEngine = gameJuiceEngine;
        // ReduceMotion an GameJuiceEngine durchreichen,
        // damit Confetti/CoinFly/Sparkle/RadialBurst gedaempft werden.
        GameJuiceEngine.ReduceMotion = ReduceMotion;

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
        WarehouseVM = warehouseSectionViewModel;
        WarehouseVM.OpenMarketRequested += () => ActivePage = ActivePage.Market;
        MarketVM = marketViewModel;
        AscensionViewModel = ascensionViewModel;
        LuckySpinViewModel = luckySpinViewModel;

        // Feature-VMs ( der MainViewModel-Zerlegung, 17.04.2026)
        HeaderVM = headerViewModel;
        PrestigeBannerVM = prestigeBannerViewModel;
        GoalBannerVM = goalBannerViewModel;
        WelcomeFlowVM = welcomeFlowViewModel;
        // WelcomeFlowVM haelt die gesamte Welcome-Flow-Logik — MainViewModel ist nur noch
        // die schmale IWelcomeFlowHost-Bruecke (IsHoldingUpgrade + NavigateToShop).
        WelcomeFlowVM.AttachHost(this);

        // PropertyChanged-Forward: HeaderVM.PlayerLevel triggert computed Properties auf MainViewModel.
        // Delegaten werden in Feldern gehalten (in Dispose() abmeldbar, keine Lambda-Leaks).
        // AXAML-Bindings zeigen direkt auf `HeaderVM.X` etc. — kein Forward der VM-Properties selbst noetig.
        _headerVmPropertyChangedHandler = OnHeaderVmPropertyChanged;
        HeaderVM.PropertyChanged += _headerVmPropertyChangedHandler;
        // Forge + Invent sind bereits in MiniGames Container
        // Delegate-Feld zuweisen (statt anonymem Lambda). MissionsVM wird weiter unten gesetzt,
        // aber das Lambda captured `this` und liest MissionsVM erst bei Aufruf (nach dem Konstruktor).
        _luckySpinNavHandler = _ => { MissionsVM?.HideLuckySpinCommand.Execute(null); IsLuckySpinVisible = false; };
        LuckySpinViewModel.NavigationRequested += _luckySpinNavHandler;

        // NavigationRequested per Schleife verdrahten (statt 19+ Einzelzeilen)
        // LuckySpinViewModel hat eigenen Handler (_luckySpinNavHandler), daher nicht in der Liste
        _navigableChildren =
        [
            ShopViewModel, StatisticsViewModel, AchievementsViewModel, SettingsViewModel,
            WorkshopViewModel, OrderViewModel, WorkerMarketViewModel, WorkerProfileViewModel,
            BuildingsViewModel, ResearchViewModel, ManagerViewModel, TournamentViewModel,
            SeasonalEventViewModel, BattlePassViewModel, GuildViewModel, CraftingViewModel,
            AscensionViewModel,
            ..MiniGames.All // 10 MiniGame-VMs (alle erben INavigable via BaseMiniGameViewModel)
        ];
        foreach (var child in _navigableChildren)
            child.NavigationRequested += OnChildNavigation;

        // Child-VM Events verdrahten (benannte Delegates fuer Dispose-Unsubscribe)
        _guildCelebrationHandler = () => _uiEffectBus.RaiseCelebration();
        _guildFloatingTextHandler = (text, cat) => _uiEffectBus.RaiseFloatingText(text, cat);
        _workerProfileFloatingTextHandler = (text, cat) => _uiEffectBus.RaiseFloatingText(text, cat);
        _buildingsFloatingTextHandler = (text, cat) => _uiEffectBus.RaiseFloatingText(text, cat);
        _ascensionFloatingTextHandler = (text, cat) => _uiEffectBus.RaiseFloatingText(text, cat);
        _ascensionCelebrationHandler = () => _uiEffectBus.RaiseCelebration();

        WorkerProfileViewModel.FloatingTextRequested += _workerProfileFloatingTextHandler;
        BuildingsViewModel.FloatingTextRequested += _buildingsFloatingTextHandler;
        GuildViewModel.CelebrationRequested += _guildCelebrationHandler;
        GuildViewModel.FloatingTextRequested += _guildFloatingTextHandler;
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
        _gameStateService.ReputationTierChanged += OnReputationTierChanged;
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
        _missionsFloatingTextHandler = (text, cat) => _uiEffectBus.RaiseFloatingText(text, cat);
        _missionsCelebrationHandler = () => _uiEffectBus.RaiseCelebration();
        _missionsStreakRescuedHandler = () =>
        {
            OnPropertyChanged(nameof(LoginStreak));
            OnPropertyChanged(nameof(HasLoginStreak));
            OnPropertyChanged(nameof(ShowStreakBadge));
        };
        MissionsVM.FloatingTextRequested += _missionsFloatingTextHandler;
        MissionsVM.CelebrationRequested += _missionsCelebrationHandler;
        MissionsVM.NavigateToMiniGameRequested += OnMissionsNavigateToMiniGame;
        MissionsVM.CheckDeferredDialogsRequested += WelcomeFlowVM.CheckDeferredDialogs;
        MissionsVM.StreakRescued += _missionsStreakRescuedHandler;

        // DialogViewModel per DI injiziert und Events verdrahten (benannte Delegates fuer Dispose)
        DialogVM = dialogViewModel;

        // EconomyFeatureViewModel initialisieren (nach DialogVM, da es DialogVM als IDialogService nutzt)
        InitializeEconomyVM();
        DialogVM.DeferredDialogCheckRequested += WelcomeFlowVM.CheckDeferredDialogs;
        // Story-Skip-Tracking fuer Onboarding-Funnel-Analyse.
        DialogVM.StorySkipRequested += chapterId => _analyticsService?.TrackEvent(
            AnalyticsEvents.OnboardingStorySkipped,
            new Dictionary<string, object?> { ["chapter_id"] = chapterId });
        _dialogPrestigeSummaryGoToShopHandler = () => SelectBuildingsTab();
        _dialogFloatingTextHandler = (text, cat) => _uiEffectBus.RaiseFloatingText(text, cat);
        DialogVM.PrestigeSummaryGoToShopRequested += _dialogPrestigeSummaryGoToShopHandler;
        DialogVM.FloatingTextRequested += _dialogFloatingTextHandler;
        _prestigeService.PrestigeCompleted += OnPrestigeCompleted;
        _prestigeService.MilestoneReached += OnPrestigeMilestoneReached;
        // Cinematic-Subscription an Coordinator delegiert.
        // CinematicCoordinator broadcastet die UI-thread-resolvte Daten an den hier registrierten Hook.
        if (_cinematicCoordinator != null)
        {
            _cinematicCoordinator.CinematicReady += OnCinematicReadyFromCoordinator;
            (_cinematicCoordinator as CinematicCoordinator)?.StartListening();
        }
        else
        {
            // Fallback: alter Pfad fuer Tests die ohne Coordinator instanziieren.
            _prestigeService.CinematicReady += OnPrestigeCinematicReady;
        }

        // Rebirth-Event fuer First-Star-Hint
        if (_rebirthService != null)
            _rebirthService.RebirthCompleted += OnRebirthCompleted;

        // Worker-Level-Up Feedback (Sound + FloatingText)
        _workerService.WorkerLevelUp += OnWorkerLevelUp;
        _workerService.InternReadyForPromotion += OnInternReadyForPromotion;

        // Notification + PlayGames Services (per Constructor Injection)
        _notificationService = notificationService;
        _playGamesService = playGamesService;
        _challengeConstraints = challengeConstraints;

        // Back-Press Helper verdrahten (benannte Methode statt Lambda fuer Dispose-Abmeldung)
        _backPressHelper.ExitHintRequested += OnBackPressExitHint;

        // v2.0.36: Notification-Center (Bell-UI). VM haengt am NotificationCenterService und
        // feuert ItemActivated wenn der Spieler eine Karte antippt — wir routen die Aktion
        // ueber NotificationKind in den richtigen Handler.
        _notificationCenterService = notificationCenterService;
        NotificationCenterVM = notificationCenterViewModel;
        NotificationCenterVM.ItemActivated += OnNotificationItemActivated;

        // v2.1.0: Saison-Storyline-Trigger an BP-Tier-Up.
        BattlePassViewModel.Service.TierUpReached += OnBattlePassTierUp;
    }

    /// <summary>
    /// v2.1.0: Bei BP-Tier-Up das Saison-Storyline-Kapitel triggern (1/10/25/40/50).
    /// CheckForNewStoryChapter pruefte intern, ob ein Saison-Kapitel passt und zeigt es an.
    /// </summary>
    private void OnBattlePassTierUp(int oldTier, int newTier, int seasonNumber)
    {
        Dispatcher.UIThread.Post(() => CheckForNewStoryChapter());
    }


    // EVENT HANDLERS → MainViewModel.EventHandlers.cs
    // GAME TICK + NAECHSTES ZIEL + TAB-UNLOCK → MainViewModel.GameTick.cs
    // HELPERS → MainViewModel.Helpers.cs
    // LIFECYCLE + DISPOSE → MainViewModel.Lifecycle.cs
}
