using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Icons;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.ViewModels.MiniGames;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// MainViewModel Properties-Cluster (AAA-Audit P0, 12.05.2026): ObservableProperties,
/// computed Properties, Tab-State-Bindings, Child-VM-Exposures. Aus MainViewModel.cs
/// extrahiert um die Haupt-Datei auf <500 Z. zu reduzieren.
/// </summary>
public sealed partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS FOR NAVIGATION AND DIALOGS
    // ═══════════════════════════════════════════════════════════════════════

    // ShowOfflineEarnings/ShowDailyReward/ShowAchievementUnlocked entfernt: Views nutzen Property-Bindings statt Events

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    // Header-Properties (Money/Level/XP/GoldenScrews etc.) sind in HeaderVM (Source-of-Truth).
    // Interner Code verwendet direkt HeaderVM.X. AXAML bindet direkt an HeaderVM.X.
    // (Phase 3 Migration 17.04.2026 - Delegate-Properties entfernt, echter Refactor.)

    /// <summary>ID des schlimmsten Workers (fuer Direktnavigation aus Warning-Chip).</summary>
    private string _worstWorkerId = "";

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

    /// <summary>
    /// Aktuell parallel laufende Auftraege (v2.0.35 Feature A) — pro Werkstatt max einer,
    /// insgesamt max <see cref="GameBalanceConstants.MaxParallelOrders"/>.
    /// UI zeigt daraus ein Fortsetzen-Panel im Dashboard.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasParallelOrders))]
    private ObservableCollection<Order> _parallelOrders = [];

    /// <summary>True wenn mindestens ein paralleler Auftrag aktiv ist (Fortsetzen-Panel sichtbar).</summary>
    public bool HasParallelOrders => ParallelOrders.Count > 0;

    [ObservableProperty]
    private bool _hasActiveOrder;

    [ObservableProperty]
    private Order? _activeOrder;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _hasDailyReward;

    // GoldenScrewsDisplay: HeaderVM ist Source-of-Truth (Phase 3).

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

    // Prestige-Banner-Properties sind in PrestigeBannerVM (Source-of-Truth, Phase 3).

    // Prestige-Tier-Dialog Properties sind jetzt in DialogVM

    // ═══════════════════════════════════════════════════════════════════════
    // NÄCHSTES ZIEL (GoalService)
    // ═══════════════════════════════════════════════════════════════════════

    // CurrentGoal-Properties sind nach GoalBannerViewModel umgezogen (Phase 3 Schritt 9, 17.04.2026).
    // AXAML-Bindings in DashboardView.axaml wurden auf `GoalBannerVM.X` aktualisiert.

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

    // Combined-Welcome-Properties sind in WelcomeFlowVM (Source-of-Truth, Phase 3).

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

    // Offline/DailyReward/StarterOffer sind in WelcomeFlowVM (Source-of-Truth, Phase 3).

    /// <summary>
    /// True wenn irgendein Overlay-Dialog sichtbar ist.
    /// Kombiniert MainViewModel-eigene Dialoge mit DialogVM.IsAnyDialogVisible.
    /// </summary>
    private bool IsAnyDialogVisible =>
        WelcomeFlowVM.IsOfflineEarningsDialogVisible || WelcomeFlowVM.IsCombinedWelcomeDialogVisible ||
        MissionsVM.IsWelcomeOfferVisible || WelcomeFlowVM.IsDailyRewardDialogVisible ||
        WelcomeFlowVM.IsStarterOfferVisible || DialogVM.IsAnyDialogVisible;

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
    private bool HasEverPrestiged => _gameStateService.Prestige.TotalPrestigeCount > 0;
    public bool ShowCraftingResearch => HasEverPrestiged || HeaderVM.PlayerLevel >= LevelThresholds.CraftingResearch;
    public bool ShowManagerSection => HasEverPrestiged || HeaderVM.PlayerLevel >= LevelThresholds.ManagerSection;
    public bool ShowMasterToolsSection => HasEverPrestiged || HeaderVM.PlayerLevel >= LevelThresholds.MasterToolsSection;
    public bool IsQuickJobsUnlocked => HasEverPrestiged || HeaderVM.PlayerLevel >= LevelThresholds.QuickJobs;
    public bool ShowBannerStrip => HasEverPrestiged || HeaderVM.PlayerLevel >= LevelThresholds.BannerStrip;
    public int QuickAccessColumns => 1 + (ShowManagerSection ? 1 : 0) + (ShowMasterToolsSection ? 1 : 0);

    // FloatingText Event fuer Dashboard-Animationen
    public event Action<string, string>? FloatingTextRequested;

    // Celebration Event fuer Confetti-Overlay (Level-Up, Achievement, Prestige)
    public event Action? CelebrationRequested;

    // Full-Screen Reward-Zeremonie (nur große Meilensteine)
    public event Action<CeremonyType, string, string>? CeremonyRequested;

    /// <summary>P0.3 AAA-Audit: Prestige-Cinematic-Trigger. View startet daraufhin den 14s-Renderer.</summary>
    public event Action<HandwerkerImperium.Models.PrestigeCinematicData>? PrestigeCinematicRequested;

    /// <summary>
    /// P0.3 AAA-Audit + Zerlegungs-Sprint: View meldet Tap-to-Skip — delegiert an Coordinator.
    /// </summary>
    public void OnPrestigeCinematicSkipped() => _cinematicCoordinator?.OnSkipped();

    /// <summary>
    /// P0.3 AAA-Audit + Zerlegungs-Sprint: View meldet Cinematic-Ende — delegiert an Coordinator.
    /// </summary>
    public void OnPrestigeCinematicDismissed() => _cinematicCoordinator?.OnDismissed();

    /// <summary>Wird ausgelöst um einen Exit-Hinweis anzuzeigen (z.B. Toast "Nochmal drücken zum Beenden").</summary>
    public event Action<string>? ExitHintRequested;

    /// <summary>Feuert VOR dem Seitenwechsel, damit die View Opacity=0 setzen kann bevor Bindings updaten.</summary>
    public event Action? PageTransitionStarting;

    // Navigation button texts
    public string NavHomeText => _localizationService.GetString("Home") ?? "Home";
    public string NavStatsText => _localizationService.GetString("Stats") ?? "Stats";
    public string NavShopText => _localizationService.GetString("Shop") ?? "Shop";
    public string NavSettingsText => _localizationService.GetString("Settings") ?? "Settings";

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
    /// <summary>Alle 10 MiniGame-VMs als Container (Zugriff via MiniGames.Sawing etc.).</summary>
    public MiniGameViewModels MiniGames { get; }
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

    // ═══════════════════════════════════════════════════════════════════════
    // FEATURE-VMs (Phase 3 der MainViewModel-Zerlegung, 17.04.2026)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Dashboard-Header-Daten: Geld, Einkommen, Level, GoldenScrews, Prestige-Badge, Boost/Rush/Delivery.</summary>
    public HeaderViewModel HeaderVM { get; }

    /// <summary>Prestige-Banner-Daten: Tier-Verfuegbarkeit, PP-Vorschau, aktive Challenges, Speedrun-Timer.</summary>
    public PrestigeBannerViewModel PrestigeBannerVM { get; }

    /// <summary>"Naechstes Ziel"-Banner auf dem Dashboard.</summary>
    public GoalBannerViewModel GoalBannerVM { get; }

    /// <summary>Welcome-Flow: CombinedWelcome, StarterOffer, OfflineEarnings, DailyReward.</summary>
    public WelcomeFlowViewModel WelcomeFlowVM { get; }

    /// <summary>
    /// Notification-Center (Bell-UI im Header, v2.0.36). Sammelt nicht-kritische
    /// Benachrichtigungen statt Modal-Stacking beim Re-Open.
    /// </summary>
    public NotificationCenterViewModel NotificationCenterVM { get; }

    /// <summary>
    /// Zentrale Effekt-Engine (Singleton aus DI). Wird von DashboardView direkt genutzt.
    /// </summary>
    public GameJuiceEngine GameJuiceEngine { get; }

}
