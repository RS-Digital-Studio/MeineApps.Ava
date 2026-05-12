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
    // AUTOMATION (Forwarding zu GameState.Automation)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Lieferungen automatisch einsammeln (ab Level 15).</summary>
    public bool AutoCollectDelivery
    {
        get => _gameStateService.Automation.AutoCollectDelivery;
        set
        {
            if (_gameStateService.Automation.AutoCollectDelivery == value) return;
            _gameStateService.Automation.AutoCollectDelivery = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>Besten Auftrag automatisch annehmen (ab Level 25).</summary>
    public bool AutoAcceptOrder
    {
        get => _gameStateService.Automation.AutoAcceptOrder;
        set
        {
            if (_gameStateService.Automation.AutoAcceptOrder == value) return;
            _gameStateService.Automation.AutoAcceptOrder = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>Daily Reward automatisch einlösen (nur Premium).</summary>
    public bool AutoClaimDaily
    {
        get => _gameStateService.Automation.AutoClaimDaily;
        set
        {
            if (_gameStateService.Automation.AutoClaimDaily == value) return;
            _gameStateService.Automation.AutoClaimDaily = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    public bool AutoAssignWorkers
    {
        get => _gameStateService.Automation.AutoAssignWorkers;
        set
        {
            if (_gameStateService.Automation.AutoAssignWorkers == value) return;
            _gameStateService.Automation.AutoAssignWorkers = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>v2.0.36: Nur Standard-Auftraege automatisch annehmen (Live/VIP bleiben liegen).</summary>
    public bool AutoAcceptOnlyStandard
    {
        get => _gameStateService.Automation.AutoAcceptOnlyStandard;
        set
        {
            if (_gameStateService.Automation.AutoAcceptOnlyStandard == value) return;
            _gameStateService.Automation.AutoAcceptOnlyStandard = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    /// <summary>v2.0.36: MiniGame-Auto-Complete ueberspringt Live-/Premium-Auftraege.</summary>
    public bool AutoCompleteSkipLiveOrders
    {
        get => _gameStateService.Automation.AutoCompleteSkipLiveOrders;
        set
        {
            if (_gameStateService.Automation.AutoCompleteSkipLiveOrders == value) return;
            _gameStateService.Automation.AutoCompleteSkipLiveOrders = value;
            _saveGameService.SaveAsync().FireAndForget();
            OnPropertyChanged();
        }
    }

    // Level-Gates für Automatisierung (delegiert an GameStateService)
    public bool IsAutoCollectUnlocked => _gameStateService.IsAutoCollectUnlocked;
    public bool IsAutoAcceptUnlocked => _gameStateService.IsAutoAcceptUnlocked;
    public bool IsAutoAssignUnlocked => _gameStateService.IsAutoAssignUnlocked;
    public bool IsAutoClaimUnlocked => _purchaseService.IsPremium;

    /// <summary>
    /// v2.0.36: Wenn die Grafik-Qualitaet auf Low steht, schalten wir die Loop-Animationen
    /// (GoldenBadgeShimmer, TutorialHintPulse, BoostPulse) aus. Die wichtigen Event-getriebenen
    /// One-Shot-Animationen (LevelUpFlash, IncomePulse) bleiben — die geben Spieler-Feedback.
    /// </summary>
    public bool ReduceMotion => _gameStateService.Settings.GraphicsQuality == Models.Enums.GraphicsQuality.Low;

    // ═══════════════════════════════════════════════════════════════════════
    // REPUTATION-TIER (v2.0.37 — Header-Badge + Spawn-Boni)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Aktuelles Reputations-Tier (computed aus dem ReputationScore).</summary>
    public Models.Enums.CustomerReputationTier CurrentReputationTier
        => _gameStateService.State.Reputation.CurrentTier;

    /// <summary>True ab Tier CityKnown — Anfaenger-Tier wird nicht angezeigt (Spam-Schutz).</summary>
    public bool ShowReputationTierBadge
        => CurrentReputationTier > Models.Enums.CustomerReputationTier.Beginner;

    /// <summary>Lokalisierter Tier-Name fuer den Header-Badge.</summary>
    public string ReputationTierName
        => _localizationService.GetString(CurrentReputationTier.GetLocalizationKey())
           ?? CurrentReputationTier.ToString();

    /// <summary>Hex-Farbe fuer das Tier-Badge (Bronze/Silber/Gold).</summary>
    public string ReputationTierColor => CurrentReputationTier.GetBadgeColor();

    // ═══════════════════════════════════════════════════════════════════════
    // ZENTRALES NAVIGATION-STATE (ActivePage Enum statt 35+ Booleans)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ActivePage _activePage = ActivePage.Dashboard;

    /// <summary>
    /// Feuert VOR dem ActivePage-Wert-Wechsel → View setzt Opacity=0 bevor Bindings updaten.
    /// Verhindert Flimmern (schwarzer Blitz) beim Tab-Wechsel.
    /// </summary>
    partial void OnActivePageChanging(ActivePage value)
    {
        if (value != _activePage)
            PageTransitionStarting?.Invoke();
    }

    /// <summary>
    /// Navigations-History für kontextuelle Rück-Navigation.
    /// Merkt sich die vorherige Seite damit Back immer dorthin zurückkehrt woher man kam.
    /// Max 10 Einträge (reicht für tiefste Verschachtelung).
    /// </summary>
    // Code-Review-Fix [Finding 5]: O(1)-Cap-Handling statt O(n)-Rebuild.
    private readonly Helpers.CappedNavigationStack _navigationStack = new(MaxNavigationStackSize);
    private bool _isNavigatingBack;
    private const int MaxNavigationStackSize = 10;

    /// <summary>
    /// Zentrale Seitenumschaltung mit Seiteneffekten (GuildChat stoppen, PropertyChanged).
    /// Wird von CommunityToolkit automatisch aufgerufen wenn ActivePage sich ändert.
    /// </summary>
    partial void OnActivePageChanged(ActivePage oldValue, ActivePage newValue)
    {
        // P1.5 AAA-Audit: Stack-Management an Helper delegiert (Sprint A — Helper-Variante).
        Helpers.PageNavigationHelper.ManageStack(_navigationStack, oldValue, newValue, _isNavigatingBack);

        // GuildChat-Polling stoppen wenn Chat verlassen wird
        if (oldValue == ActivePage.GuildChat)
            GuildViewModel.StopChatPolling();

        // PropertyChanged für die berechneten IsXxxActive-Properties (nur die 2 geänderten)
        var oldProp = Helpers.PageNavigationHelper.GetPropertyNameFor(oldValue);
        var newProp = Helpers.PageNavigationHelper.GetPropertyNameFor(newValue);
        if (oldProp != null) OnPropertyChanged(oldProp);
        if (newProp != null) OnPropertyChanged(newProp);
        OnPropertyChanged(nameof(IsTabBarVisible));
        OnPropertyChanged(nameof(BreadcrumbText));

        // MiniGame-ContentControl aktualisieren (ein einziges statt 10 separate)
        OnPropertyChanged(nameof(ActiveMiniGameViewModel));
        OnPropertyChanged(nameof(IsAnyMiniGameActive));

        // AAA-Audit P0 Lazy-View-Loading: Zentrales ActivePageContent feuern, damit das
        // einzelne ContentControl in MainView die richtige Sub-View materialisiert.
        OnPropertyChanged(nameof(ActivePageContent));
        OnPropertyChanged(nameof(HasActivePageContent));
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
    public bool IsPrestigeActive => ActivePage == ActivePage.Prestige;

    // ═══════════════════════════════════════════════════════════════════════
    // IMPERIUM-SUB-TABS (v2.0.37)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImperiumWorkshopsActive))]
    [NotifyPropertyChangedFor(nameof(IsImperiumWorkersActive))]
    [NotifyPropertyChangedFor(nameof(IsImperiumResearchActive))]
    [NotifyPropertyChangedFor(nameof(IsImperiumEquipmentActive))]
    [NotifyPropertyChangedFor(nameof(IsImperiumAscensionActive))]
    private ImperiumSubTab _imperiumSubTab = ImperiumSubTab.Workshops;

    public bool IsImperiumWorkshopsActive => ImperiumSubTab == ImperiumSubTab.Workshops;
    public bool IsImperiumWorkersActive => ImperiumSubTab == ImperiumSubTab.Workers;
    public bool IsImperiumResearchActive => ImperiumSubTab == ImperiumSubTab.Research;
    public bool IsImperiumEquipmentActive => ImperiumSubTab == ImperiumSubTab.Equipment;
    public bool IsImperiumAscensionActive => ImperiumSubTab == ImperiumSubTab.Ascension;

    /// <summary>
    /// Imperium-Sub-Tab waehlen (per RelayCommand aus AXAML).
    /// Ascension-Sub-Tab nur sichtbar wenn Ascension verfuegbar (PrestigeData.LegendeCount &gt;= 3).
    /// </summary>
    [RelayCommand]
    private void SelectImperiumSubTab(string subTabName)
    {
        if (Enum.TryParse<ImperiumSubTab>(subTabName, ignoreCase: true, out var tab))
            ImperiumSubTab = tab;
    }

    /// <summary>True wenn Ascension-Sub-Tab freigeschaltet (3x Legende-Prestige).</summary>
    public bool IsImperiumAscensionUnlocked
        => _gameStateService.Prestige.LegendeCount >= 3;

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

    /// <summary>
    /// AAA-Audit P0 Lazy-View-Loading: Liefert das ViewModel der aktuell aktiven Seite,
    /// oder null fuer Direct-Bound-Views (Dashboard/Imperium/Missionen/Prestige), die im
    /// MainView weiter via IsVisible toggle’n.
    ///
    /// MainView nutzt ein einzelnes &lt;ContentControl Content="{Binding ActivePageContent}"/&gt;
    /// statt 25+ einzelner ContentControls — das spart bei Cold-Start die Materialisierung
    /// aller nicht-aktiven Sub-Views (~25 SkiaSharp-Renderer pro App-Start vermieden).
    /// </summary>
    public object? ActivePageContent => ActivePage switch
    {
        // Direct-Bound (kein ContentControl-Routing) — null = MainView's IsVisible-Bindings greifen.
        ActivePage.Dashboard or ActivePage.Buildings or ActivePage.Missionen or ActivePage.Prestige
            => null,

        // Top-Level-Tabs
        ActivePage.Shop => ShopViewModel,
        ActivePage.Statistics => StatisticsViewModel,
        ActivePage.Achievements => AchievementsViewModel,
        ActivePage.Settings => SettingsViewModel,
        ActivePage.WorkshopDetail => WorkshopViewModel,
        ActivePage.OrderDetail => OrderViewModel,
        ActivePage.WorkerMarket => WorkerMarketViewModel,
        ActivePage.Research => ResearchViewModel,
        ActivePage.Manager => ManagerViewModel,
        ActivePage.Tournament => TournamentViewModel,
        ActivePage.SeasonalEvent => SeasonalEventViewModel,
        ActivePage.BattlePass => BattlePassViewModel,
        ActivePage.Crafting => CraftingViewModel,
        ActivePage.Ascension => AscensionViewModel,
        ActivePage.Guild => GuildViewModel,

        // Gilden-Sub-Pages (Thin-Wrapper-VMs ueber GuildViewModel)
        ActivePage.GuildResearch => GuildViewModel.ResearchVM,
        ActivePage.GuildMembers => GuildViewModel.MembersVM,
        ActivePage.GuildInvite => GuildViewModel.InviteVM,
        ActivePage.GuildWarSeason => GuildViewModel.WarSeasonViewModel,
        ActivePage.GuildBoss => GuildViewModel.BossViewModel,
        ActivePage.GuildHall => GuildViewModel.HallViewModel,
        ActivePage.GuildAchievements => GuildViewModel.AchievementsVM,
        ActivePage.GuildChat => GuildViewModel.ChatVM,
        ActivePage.GuildWar => GuildViewModel.WarVM,

        // MiniGames (delegieren an ActiveMiniGameViewModel)
        ActivePage.SawingGame or ActivePage.PipePuzzle or ActivePage.WiringGame
            or ActivePage.PaintingGame or ActivePage.RoofTilingGame or ActivePage.BlueprintGame
            or ActivePage.DesignPuzzleGame or ActivePage.InspectionGame or ActivePage.ForgeGame
            or ActivePage.InventGame => ActiveMiniGameViewModel,

        _ => null
    };

    /// <summary>True wenn ActivePageContent ein VM liefert (= ContentControl wird sichtbar).</summary>
    public bool HasActivePageContent => ActivePageContent != null;

    // Overlay-States (überlagern die aktuelle Seite, ActivePage bleibt unverändert)
    [ObservableProperty]
    private bool _isWorkerProfileActive;

    partial void OnIsWorkerProfileActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(IsTabBarVisible));
    }

    /// <summary>
    /// Schliesst das Worker-Profile Bottom-Sheet. Wird von Backdrop-Klick + Close-Button verwendet.
    /// Ersetzt den Umweg ueber HandleBackPressed() (Code-Behind-Anti-Pattern).
    /// </summary>
    [RelayCommand]
    private void CloseWorkerProfile() => IsWorkerProfileActive = false;

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
        return HeaderVM.PlayerLevel < TabUnlockLevels[tabIndex];
    }

    /// <summary>
    /// Gibt ein gecachtes Array zurück das angibt welche Tabs gesperrt sind (für Tab-Bar-Renderer).
    /// Cache wird nur bei Level-Änderung invalidiert (statt 25x/s neu allokiert).
    /// </summary>
    private bool[]? _cachedLockedTabs;
    private int _lockedTabsCacheLevel = -1;

    public bool[] GetLockedTabs()
    {
        if (_cachedLockedTabs != null && _lockedTabsCacheLevel == HeaderVM.PlayerLevel)
            return _cachedLockedTabs;

        _cachedLockedTabs = new bool[5];
        for (int i = 0; i < 5; i++)
            _cachedLockedTabs[i] = IsTabLocked(i);
        _lockedTabsCacheLevel = HeaderVM.PlayerLevel;
        return _cachedLockedTabs;
    }

    /// <summary>
    /// Haupt-Tabs bei denen die Tab-Bar sichtbar ist (5 Hauptseiten).
    /// </summary>
    // P1.5 AAA-Audit: s_mainTabs wandert als statisches Set in den Helper.

    /// <summary>
    /// Tab-Bar sichtbar nur auf den 5 Haupt-Tabs und wenn kein Overlay aktiv ist.
    /// </summary>
    public bool IsTabBarVisible => Helpers.PageNavigationHelper.MainTabs.Contains(ActivePage) && !IsWorkerProfileActive;

    /// <summary>
    /// NAV-3: Breadcrumb-Text für Sub-Views (zeigt den Parent-Tab wenn Tab-Bar versteckt ist).
    /// </summary>
    public string BreadcrumbText => ActivePage switch
    {
        ActivePage.WorkshopDetail or ActivePage.OrderDetail or
        ActivePage.SawingGame or ActivePage.PipePuzzle or ActivePage.WiringGame or
        ActivePage.PaintingGame or ActivePage.RoofTilingGame or ActivePage.BlueprintGame or
        ActivePage.DesignPuzzleGame or ActivePage.InspectionGame or ActivePage.ForgeGame or
        ActivePage.InventGame => _localizationService.GetString("TabWorkshop") ?? "Workshop",
        ActivePage.WorkerMarket or ActivePage.Research or ActivePage.Manager or
        ActivePage.Crafting or ActivePage.Ascension => _localizationService.GetString("TabImperium") ?? "Empire",
        ActivePage.Tournament or ActivePage.SeasonalEvent or ActivePage.BattlePass or
        ActivePage.Statistics or ActivePage.Achievements => _localizationService.GetString("TabMissions") ?? "Missions",
        ActivePage.GuildResearch or ActivePage.GuildMembers or ActivePage.GuildInvite or
        ActivePage.GuildWarSeason or ActivePage.GuildBoss or ActivePage.GuildHall or
        ActivePage.GuildAchievements or ActivePage.GuildChat or ActivePage.GuildWar => _localizationService.GetString("TabGuild") ?? "Guild",
        ActivePage.Settings => _localizationService.GetString("Settings") ?? "Settings",
        _ => ""
    };

    // P1.5 AAA-Audit: ActivePagePropertyName ist als Helper extrahiert
    // (Helpers/PageNavigationHelper.GetPropertyNameFor). 41 Zeilen weniger im MainViewModel.

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
