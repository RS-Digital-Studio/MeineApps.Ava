using BomberBlast.Core;
using BomberBlast.Resources.Strings;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// Haupt-ViewModel für Navigation zwischen Views (MainMenu, Game, LevelSelect, etc.).
/// Zeigt jeweils nur eine Child-View an.
/// Hält alle Child-ViewModels für den korrekten DataContext.
///
/// <para><b>Lifetime-Hinweis:</b> Singleton mit App-Lifetime. Alle Event-Subscriptions
/// im Konstruktor sind bewusst per Lambda registriert — Unsubscribe ist nicht nötig,
/// weil Child-ViewModels (ebenfalls Singleton) und Services gemeinsam bis Prozess-Ende
/// leben. <see cref="OnAdUnavailable"/> ist als benannter Handler ausgelegt als Referenz,
/// falls in Zukunft eine View- oder VM-Recycling-Strategie eingeführt wird.</para>
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS (Game Juice)
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    /// <summary>
    /// Event für Android-Toast bei Double-Back-to-Exit Hinweis
    /// </summary>
    public event Action<string>? ExitHintRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // CHILD VIEWMODELS (Eager)
    // ═══════════════════════════════════════════════════════════════════════
    // Diese VMs werden direkt beim Start gebraucht (Menü, erste Level, Pause,
    // GameOver, Settings). Sie bleiben Konstruktor-Parameter und werden sofort
    // verdrahtet.

    public MainMenuViewModel MenuVm { get; }
    public LevelSelectViewModel LevelSelectVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public HighScoresViewModel HighScoresVm { get; }
    public GameOverViewModel GameOverVm { get; }
    public HelpViewModel HelpVm { get; }
    public VictoryViewModel VictoryVm { get; }

    /// <summary>What's-New-Modal-VM (wird einmal pro App-Update angezeigt).</summary>
    public WhatsNewViewModel WhatsNewVm { get; }

    /// <summary>True solange das What's-New-Modal sichtbar ist (gesteuert vom Closed-Event des VMs).</summary>
    [ObservableProperty]
    private bool _isWhatsNewVisible;

    /// <summary>
    /// Game-VM ist nullable + Lazy: spart 100-200ms Startup auf Mid-Tier-Android,
    /// weil GameEngine + GameRenderer (mit allen SkPaint/SkFont/SkMaskFilter-Allokationen)
    /// erst beim ersten Game-Start aufgeloest werden — nicht waehrend Splash + MainMenu.
    /// EnsureGameVm() sorgt fuer idempotente Initialisierung samt Event-Wirings.
    /// </summary>
    [ObservableProperty] private GameViewModel? _gameVm;

    // ═══════════════════════════════════════════════════════════════════════
    // CHILD VIEWMODELS (Lazy - spät unlocked)
    // ═══════════════════════════════════════════════════════════════════════
    // Diese VMs werden erst bei progressivem Feature-Unlock (Level 3-30) gebraucht.
    // Sie werden über Lazy<T> injiziert und erst beim ersten Navigations-Ziel
    // instanziiert + verdrahtet (siehe EnsureXxxVm()-Methoden). Public Properties
    // sind nullable und feuern OnPropertyChanged bei erstem Zugriff → XAML
    // ContentControl bindet den VM erst dann ein.
    //
    // Startup-Ersparnis: ShopViewModel (~900 Zeilen), BattlePass, League,
    // Collection, Dungeon und ihre Services werden nicht beim App-Start geladen.

    [ObservableProperty] private ShopViewModel? _shopVm;
    [ObservableProperty] private AchievementsViewModel? _achievementsVm;
    [ObservableProperty] private DailyChallengeViewModel? _dailyChallengeVm;
    [ObservableProperty] private LuckySpinViewModel? _luckySpinVm;
    [ObservableProperty] private WeeklyChallengeViewModel? _weeklyChallengeVm;
    [ObservableProperty] private StatisticsViewModel? _statisticsVm;
    [ObservableProperty] private QuickPlayViewModel? _quickPlayVm;
    [ObservableProperty] private DeckViewModel? _deckVm;
    [ObservableProperty] private DungeonViewModel? _dungeonVm;
    [ObservableProperty] private BattlePassViewModel? _battlePassVm;
    [ObservableProperty] private CollectionViewModel? _collectionVm;
    [ObservableProperty] private LeagueViewModel? _leagueVm;
    [ObservableProperty] private ProfileViewModel? _profileVm;
    [ObservableProperty] private GemShopViewModel? _gemShopVm;

    // Boss-Rush (v2.0.42, Plan Task 3.3): Eager-Singleton — Modi-Strip-Tile im MainMenu zeigt
    // Wochen-Best direkt; BossRushView ist die Pre-Run-Page.
    // Daily-Hub aufgeloest (v2.0.43, Menu-Redesign) — Inhalte direkt im MainMenu-Dashboard.
    public BossRushViewModel BossRushVm { get; }

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktiv sichtbare Page (v2.0.37). Ersetzt 17 frueher separate IsXxxActive-Booleans.
    /// MainView.axaml bindet ueber <see cref="Converters.ActiveViewEqualsConverter"/> auf
    /// <c>Classes.Active</c> und <c>IsVisible</c> der einzelnen PageView-Border.
    /// </summary>
    [ObservableProperty]
    // Audit M01: NotifyPropertyChangedFor ersetzt 17 manuelle OnPropertyChanged() in partial OnActiveViewChanged.
    [NotifyPropertyChangedFor(nameof(IsMainMenuActive))]
    [NotifyPropertyChangedFor(nameof(IsGameActive))]
    [NotifyPropertyChangedFor(nameof(IsLevelSelectActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    [NotifyPropertyChangedFor(nameof(IsHighScoresActive))]
    [NotifyPropertyChangedFor(nameof(IsGameOverActive))]
    [NotifyPropertyChangedFor(nameof(IsShopActive))]
    [NotifyPropertyChangedFor(nameof(IsVictoryActive))]
    [NotifyPropertyChangedFor(nameof(IsStatisticsActive))]
    [NotifyPropertyChangedFor(nameof(IsQuickPlayActive))]
    [NotifyPropertyChangedFor(nameof(IsDungeonActive))]
    [NotifyPropertyChangedFor(nameof(IsBattlePassActive))]
    [NotifyPropertyChangedFor(nameof(IsLeagueActive))]
    [NotifyPropertyChangedFor(nameof(IsProfileActive))]
    [NotifyPropertyChangedFor(nameof(IsGemShopActive))]
    [NotifyPropertyChangedFor(nameof(IsCardsActive))]
    [NotifyPropertyChangedFor(nameof(IsChallengesActive))]
    private ActiveView _activeView = ActiveView.MainMenu;

    // Backward-compat Computed-Properties — werden von Logik in NavigateTo()/HandleBackPressed()
    // weiter genutzt (Tab-Status, Game-Lifecycle). Feuern OnPropertyChanged via partial method.
    public bool IsMainMenuActive => ActiveView == ActiveView.MainMenu;
    public bool IsGameActive => ActiveView == ActiveView.Game;
    public bool IsLevelSelectActive => ActiveView == ActiveView.LevelSelect;
    public bool IsSettingsActive => ActiveView == ActiveView.Settings;
    public bool IsHighScoresActive => ActiveView == ActiveView.HighScores;
    public bool IsGameOverActive => ActiveView == ActiveView.GameOver;
    public bool IsShopActive => ActiveView == ActiveView.Shop;
    public bool IsVictoryActive => ActiveView == ActiveView.Victory;
    public bool IsStatisticsActive => ActiveView == ActiveView.Statistics;
    public bool IsQuickPlayActive => ActiveView == ActiveView.QuickPlay;
    public bool IsDungeonActive => ActiveView == ActiveView.Dungeon;
    public bool IsBattlePassActive => ActiveView == ActiveView.BattlePass;
    public bool IsLeagueActive => ActiveView == ActiveView.League;
    public bool IsProfileActive => ActiveView == ActiveView.Profile;
    public bool IsGemShopActive => ActiveView == ActiveView.GemShop;
    public bool IsCardsActive => ActiveView == ActiveView.Cards;
    public bool IsChallengesActive => ActiveView == ActiveView.Challenges;

    // Audit M01: partial OnActiveViewChanged entfernt — NotifyPropertyChangedFor-Attribute am Field
    // erledigen die Benachrichtigung der 17 IsXxxActive-Properties automatisch.

    // ═══════════════════════════════════════════════════════════════════════
    // TAB-STATE PROPERTIES (für kombinierte Views)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Shop: false=Shop-Tab, true=Glücksrad-Tab</summary>
    [ObservableProperty]
    private bool _isShopSpinTab;

    /// <summary>Profil: false=Profil-Tab, true=Erfolge-Tab</summary>
    [ObservableProperty]
    private bool _isProfileAchievementsTab;

    /// <summary>Einstellungen: false=Settings-Tab, true=Hilfe-Tab</summary>
    [ObservableProperty]
    private bool _isSettingsHelpTab;

    /// <summary>Karten: false=Deck-Tab, true=Sammlung-Tab</summary>
    [ObservableProperty]
    private bool _isCardsCollectionTab;

    /// <summary>Herausforderungen: false=Daily-Tab, true=Missions-Tab</summary>
    [ObservableProperty]
    private bool _isChallengesMissionsTab;

    /// <summary>
    /// Ad-Banner-Spacer: sichtbar in Menü-Views, versteckt im Game-View
    /// </summary>
    [ObservableProperty]
    private bool _isAdBannerVisible;

    // ═══════════════════════════════════════════════════════════════════════
    // DIALOG PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyDialogOpen))]
    private bool _isAlertDialogVisible;

    [ObservableProperty]
    private string _alertDialogTitle = "";

    [ObservableProperty]
    private string _alertDialogMessage = "";

    [ObservableProperty]
    private string _alertDialogButtonText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyDialogOpen))]
    private bool _isConfirmDialogVisible;

    /// <summary>
    /// Audit M18: Aggregat-Flag fuer alle modalen Dialoge. View bindet darunterliegende Page-Views
    /// IsHitTestVisible="{Binding !IsAnyDialogOpen}" → Android-ZIndex-Hit-Test-Problem entschaerft.
    /// </summary>
    public bool IsAnyDialogOpen => IsAlertDialogVisible || IsConfirmDialogVisible;

    [ObservableProperty]
    private string _confirmDialogTitle = "";

    [ObservableProperty]
    private string _confirmDialogMessage = "";

    [ObservableProperty]
    private string _confirmDialogAcceptText = "";

    [ObservableProperty]
    private string _confirmDialogCancelText = "";

    private TaskCompletionSource<bool>? _confirmDialogTcs;

    /// <summary>
    /// Merkt ob Einstellungen aus dem Spiel geöffnet wurden (für Zurück-Navigation).
    /// </summary>
    private bool _returnToGameFromSettings;

    private readonly ILocalizationService _localizationService;
    private readonly IAdService _adService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IAchievementService _achievementService;
    private readonly ICoinService _coinService;
    private readonly IPurchaseService _purchaseService;
    private readonly ICloudSaveService _cloudSaveService;
    private readonly SoundManager _soundManager;
    private readonly IAppLogger _logger;
    /// <summary>Sprint 4.2 AAA-Audit #10: GameEventBus — neue Code nutzt diesen statt durch MainVM zu routen.</summary>
    private readonly IGameEventBus _eventBus;

    // Lazy-VM-Factories (werden beim ersten EnsureXxxVm() resolved)
    private readonly Lazy<GameViewModel> _gameVmLazy;
    private readonly Lazy<ShopViewModel> _shopVmLazy;
    private readonly Lazy<AchievementsViewModel> _achievementsVmLazy;
    private readonly Lazy<DailyChallengeViewModel> _dailyChallengeVmLazy;
    private readonly Lazy<LuckySpinViewModel> _luckySpinVmLazy;
    private readonly Lazy<WeeklyChallengeViewModel> _weeklyChallengeVmLazy;
    private readonly Lazy<StatisticsViewModel> _statisticsVmLazy;
    private readonly Lazy<QuickPlayViewModel> _quickPlayVmLazy;
    private readonly Lazy<DeckViewModel> _deckVmLazy;
    private readonly Lazy<DungeonViewModel> _dungeonVmLazy;
    private readonly Lazy<BattlePassViewModel> _battlePassVmLazy;
    private readonly Lazy<CollectionViewModel> _collectionVmLazy;
    private readonly Lazy<LeagueViewModel> _leagueVmLazy;
    private readonly Lazy<ProfileViewModel> _profileVmLazy;
    private readonly Lazy<GemShopViewModel> _gemShopVmLazy;

    /// <summary>
    /// Task für Cloud-Save-Initialisierung (kein Fire-and-Forget, vermeidet Race Conditions)
    /// </summary>
    private Task? _cloudSaveInitTask;

    private readonly BackPressHelper _backPressHelper = new();

    /// <summary>
    /// Zaehlt Fehlversuche pro Level (fuer Level-Skip nach 3x Game Over)
    /// </summary>
    private readonly Dictionary<int, int> _levelFailCounts = new();

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Audit M25: Konstruktor von 32 Parametern auf eine einzige Aggregat-Dependency reduziert.
    /// Die <see cref="MainViewModelDependencies"/>-Record buendelt alle 8 Eager-VMs, 15 Lazy-VMs
    /// und 10 Services.
    /// </summary>
    public MainViewModel(MainViewModelDependencies deps)
    {
        MenuVm = deps.MenuVm;
        LevelSelectVm = deps.LevelSelectVm;
        SettingsVm = deps.SettingsVm;
        HighScoresVm = deps.HighScoresVm;
        GameOverVm = deps.GameOverVm;
        HelpVm = deps.HelpVm;
        VictoryVm = deps.VictoryVm;
        WhatsNewVm = deps.WhatsNewVm;

        _gameVmLazy = deps.GameVmLazy;
        _shopVmLazy = deps.ShopVmLazy;
        _achievementsVmLazy = deps.AchievementsVmLazy;
        _dailyChallengeVmLazy = deps.DailyChallengeVmLazy;
        _luckySpinVmLazy = deps.LuckySpinVmLazy;
        _weeklyChallengeVmLazy = deps.WeeklyChallengeVmLazy;
        _statisticsVmLazy = deps.StatisticsVmLazy;
        _quickPlayVmLazy = deps.QuickPlayVmLazy;
        _deckVmLazy = deps.DeckVmLazy;
        _dungeonVmLazy = deps.DungeonVmLazy;
        _battlePassVmLazy = deps.BattlePassVmLazy;
        _collectionVmLazy = deps.CollectionVmLazy;
        _leagueVmLazy = deps.LeagueVmLazy;
        _profileVmLazy = deps.ProfileVmLazy;
        _gemShopVmLazy = deps.GemShopVmLazy;

        BossRushVm = deps.BossRushVm;
        WireCommon(deps.BossRushVm);

        _localizationService = deps.Localization;
        _adService = deps.AdService;
        _purchaseService = deps.PurchaseService;
        _rewardedAdService = deps.RewardedAdService;
        _achievementService = deps.AchievementService;
        _coinService = deps.CoinService;
        _cloudSaveService = deps.CloudSaveService;
        _soundManager = deps.SoundManager;
        _logger = deps.Logger;
        _eventBus = deps.EventBus;

        // Lokale Aliase fuer den Konstruktor-Body (Variable bleiben unchanged von der Original-Logic).
        var localization = deps.Localization;
        var adService = deps.AdService;
        var purchaseService = deps.PurchaseService;
        var rewardedAdService = deps.RewardedAdService;
        var achievementService = deps.AchievementService;
        var coinService = deps.CoinService;
        var cloudSaveService = deps.CloudSaveService;
        var soundManager = deps.SoundManager;
        var logger = deps.Logger;
        var eventBus = deps.EventBus;
        var menuVm = deps.MenuVm;
        var settingsVm = deps.SettingsVm;
        var gameOverVm = deps.GameOverVm;
        var helpVm = deps.HelpVm;
        var victoryVm = deps.VictoryVm;
        var highScoresVm = deps.HighScoresVm;
        var levelSelectVm = deps.LevelSelectVm;

        // Sprint 4.2 AAA-Audit #10: GameEventBus → MainVM-Events forwarden.
        // Andere ViewModels koennen jetzt direkt _eventBus.RaiseFloatingText() rufen,
        // ohne durch MainViewModel routen zu muessen — God-VM-Abhaengigkeit reduziert.
        _eventBus.FloatingTextRequested += (t, s) => FloatingTextRequested?.Invoke(t, s);
        _eventBus.CelebrationRequested += () => CelebrationRequested?.Invoke();
        _eventBus.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // ───────────────────────────────────────────────────────────────────
        // Eager-VMs verdrahten (Navigation + Game-Juice)
        // GameVm wird in EnsureGameVm() verdrahtet — siehe oben.
        // ───────────────────────────────────────────────────────────────────
        WireCommon(menuVm);
        WireCommon(levelSelectVm);
        WireCommon(settingsVm);
        WireCommon(highScoresVm);
        WireCommon(gameOverVm);
        WireCommon(helpVm);
        WireCommon(victoryVm);

        // GameOverVm-spezifische Events (Confirmation Dialog)
        GameOverVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);

        // Achievement-Toast bei Unlock (mit Coin-Belohnung)
        _achievementService.AchievementUnlocked += (_, achievement) =>
        {
            var name = localization.GetString(achievement.NameKey) ?? achievement.NameKey;
            string text = achievement.CoinReward > 0
                ? $"Achievement: {name}! +{achievement.CoinReward} Coins"
                : $"Achievement: {name}!";
            FloatingTextRequested?.Invoke(text, "gold");
        };

        // Ad-Banner: In BomberBlast (Landscape) kein Banner - nur Rewarded Ads
        IsAdBannerVisible = false;
        if (adService.AdsEnabled)
            adService.HideBanner();

        // Ad-Unavailable Meldung anzeigen (benannte Methode statt Lambda fuer Unsubscribe)
        _rewardedAdService.AdUnavailable += OnAdUnavailable;

        // Back-Press Helper verdrahten
        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // PauseViewModel entfernt — Resume/Restart laufen direkt ueber GameVm.{Resume,Restart}Command,
        // gebunden im GameView.axaml Pause-Overlay. SkiaSharp-Canvas rendert die Pause-Anzeige.

        // Dialog-Events von SettingsVM verdrahten (SettingsVm ist eager)
        settingsVm.AlertRequested += (t, m, b) => ShowAlertDialog(t, m, b);
        settingsVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);

        // LanguageChanged: Auto-Discovery via ILocalizable (Audit M23 + C13).
        // Iteriert alle instanziierten (lazy resolved) VMs, ruft UpdateLocalizedTexts.
        // VMs ohne ILocalizable bekommen OnAppearing() (fallback) — idempotent fuer inaktive Views.
        localization.LanguageChanged += (_, _) => RefreshAllLocalizedTexts();

        // Cloud Save: Bei App-Start Cloud-Stand laden (Task gespeichert, kein Fire-and-Forget)
        _cloudSaveInitTask = Task.Run(async () =>
        {
            try { await _cloudSaveService.TryLoadFromCloudAsync(); }
            catch (Exception ex) { _logger?.LogWarning($"CloudSave Init fehlgeschlagen: {ex.Message}"); }
        });

        // What's-New-Modal: Closed-Event verdrahten + Initial-Check ob anzeigen.
        // ShouldShow prueft Service-State (CurrentVersion > LastSeenVersion + Eintraege vorhanden).
        WhatsNewVm.Closed += () => IsWhatsNewVisible = false;
        if (deps.WhatsNewService.ShouldShow)
            IsWhatsNewVisible = true;

        // Menü initialisieren
        menuVm.OnAppearing();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LAZY-VM-ENSURE-METHODEN
    // ═══════════════════════════════════════════════════════════════════════
    // Jede EnsureXxxVm() löst Lazy<T>.Value auf (instanziiert den VM), verdrahtet
    // Navigation + Game-Juice-Events sowie VM-spezifische Dialog/IAP-Events,
    // setzt anschliessend die ObservableProperty (feuert OnPropertyChanged →
    // XAML ContentControl bindet den VM dann ein). Idempotent: Mehrfach-Aufrufe
    // liefern dieselbe Instanz.

    /// <summary>
    /// Gemeinsame Verdrahtung (Navigation + Game-Juice) für alle VMs.
    /// </summary>
    private void WireCommon(INavigable vm)
    {
        vm.NavigationRequested += NavigateTo;
        // Audit L23: Getrennte Interfaces — FloatingText und Celebration unabhaengig.
        if (vm is IFloatingTextEmitter floatingEmitter)
            floatingEmitter.FloatingTextRequested += (text, type) => FloatingTextRequested?.Invoke(text, type);
        if (vm is ICelebrationEmitter celebrationEmitter)
            celebrationEmitter.CelebrationRequested += () => CelebrationRequested?.Invoke();
    }

    /// <summary>
    /// Audit M23: Sprachwechsel-Forwarder. Iteriert alle instanziierten (resolved) VMs:
    /// - Implementiert <see cref="ILocalizable"/> → UpdateLocalizedTexts().
    /// - Sonst: OnAppearing() falls vorhanden (fallback fuer VMs ohne dediziertes Interface).
    /// Idempotent fuer inaktive Views.
    /// </summary>
    private void RefreshAllLocalizedTexts()
    {
        // Eager-VMs (immer instanziiert)
        InvokeLocalizable(MenuVm, fallback: () => MenuVm.OnAppearing());
        InvokeLocalizable(LevelSelectVm, fallback: () => LevelSelectVm.OnAppearing());
        InvokeLocalizable(SettingsVm, fallback: () => SettingsVm.OnAppearing());
        InvokeLocalizable(HighScoresVm, fallback: () => HighScoresVm.OnAppearing());
        InvokeLocalizable(BossRushVm, fallback: null);
        // HelpVm/GameOverVm/VictoryVm: keine OnAppearing, XAML-only / SetParameters

        // Lazy-VMs (nur wenn resolved)
        InvokeLocalizable(ShopVm, fallback: null);
        InvokeLocalizable(QuickPlayVm, fallback: null);
        InvokeLocalizable(DeckVm, fallback: null);
        InvokeLocalizable(DungeonVm, fallback: null);
        InvokeLocalizable(BattlePassVm, fallback: null);
        InvokeLocalizable(CollectionVm, fallback: null);
        InvokeLocalizable(LeagueVm, fallback: null);
        InvokeLocalizable(ProfileVm, fallback: null);
        InvokeLocalizable(GemShopVm, fallback: null);
        InvokeLocalizable(StatisticsVm, fallback: null);
        InvokeLocalizable(DailyChallengeVm, fallback: null);
        InvokeLocalizable(WeeklyChallengeVm, fallback: null);
        InvokeLocalizable(AchievementsVm, fallback: () => AchievementsVm?.OnAppearing());
        InvokeLocalizable(LuckySpinVm, fallback: () => LuckySpinVm?.OnAppearing());
    }

    /// <summary>
    /// Audit M23-Helper: ruft <see cref="ILocalizable.UpdateLocalizedTexts"/> wenn implementiert,
    /// sonst optionalen Fallback. try/catch verhindert dass ein VM-Fehler andere blockiert.
    /// </summary>
    private void InvokeLocalizable(object? vm, Action? fallback)
    {
        if (vm == null) return;
        try
        {
            if (vm is ILocalizable localizable)
                localizable.UpdateLocalizedTexts();
            else
                fallback?.Invoke();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"RefreshAllLocalizedTexts: {vm.GetType().Name} fehlgeschlagen — {ex.Message}");
        }
    }

    /// <summary>
    /// Loest GameViewModel lazy auf und verdrahtet Navigation + Game-Juice-Events.
    /// Wird beim ersten Game-Start aufgerufen (NavigateToGame, OnAppearing) — spart
    /// 100-200ms Startup-Zeit, weil GameEngine + GameRenderer (mit zahlreichen
    /// SkPaint/SkFont/SkMaskFilter-Allokationen) erst dann erzeugt werden.
    /// </summary>
    private GameViewModel EnsureGameVm()
    {
        if (GameVm is { } existing) return existing;
        var vm = _gameVmLazy.Value;
        WireCommon(vm);
        GameVm = vm;
        return vm;
    }

    private ShopViewModel EnsureShopVm()
    {
        if (ShopVm is { } existing) return existing;
        var vm = _shopVmLazy.Value;
        WireCommon(vm);
        vm.PurchaseSucceeded += name =>
        {
            FloatingTextRequested?.Invoke(name, "success");
            CelebrationRequested?.Invoke();
        };
        vm.InsufficientFunds += () =>
        {
            var msg = _localizationService.GetString("ShopNotEnoughCoins") ?? "Not enough coins!";
            FloatingTextRequested?.Invoke(msg, "error");
        };
        vm.MessageRequested += (t, m) => ShowAlertDialog(t, m, "OK");
        vm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);
        ShopVm = vm;
        return vm;
    }

    private AchievementsViewModel EnsureAchievementsVm()
    {
        if (AchievementsVm is { } existing) return existing;
        var vm = _achievementsVmLazy.Value;
        WireCommon(vm);
        AchievementsVm = vm;
        return vm;
    }

    private DailyChallengeViewModel EnsureDailyChallengeVm()
    {
        if (DailyChallengeVm is { } existing) return existing;
        var vm = _dailyChallengeVmLazy.Value;
        WireCommon(vm);
        DailyChallengeVm = vm;
        return vm;
    }

    private LuckySpinViewModel EnsureLuckySpinVm()
    {
        if (LuckySpinVm is { } existing) return existing;
        var vm = _luckySpinVmLazy.Value;
        WireCommon(vm);
        LuckySpinVm = vm;
        return vm;
    }

    private WeeklyChallengeViewModel EnsureWeeklyChallengeVm()
    {
        if (WeeklyChallengeVm is { } existing) return existing;
        var vm = _weeklyChallengeVmLazy.Value;
        WireCommon(vm);
        WeeklyChallengeVm = vm;
        return vm;
    }

    private StatisticsViewModel EnsureStatisticsVm()
    {
        if (StatisticsVm is { } existing) return existing;
        var vm = _statisticsVmLazy.Value;
        WireCommon(vm);
        StatisticsVm = vm;
        return vm;
    }

    private QuickPlayViewModel EnsureQuickPlayVm()
    {
        if (QuickPlayVm is { } existing) return existing;
        var vm = _quickPlayVmLazy.Value;
        WireCommon(vm);
        QuickPlayVm = vm;
        return vm;
    }

    private DeckViewModel EnsureDeckVm()
    {
        if (DeckVm is { } existing) return existing;
        var vm = _deckVmLazy.Value;
        WireCommon(vm);
        DeckVm = vm;
        return vm;
    }

    private DungeonViewModel EnsureDungeonVm()
    {
        if (DungeonVm is { } existing) return existing;
        var vm = _dungeonVmLazy.Value;
        WireCommon(vm);
        // Dungeon Ad-Run: Rewarded Ad zeigen und bei Erfolg melden (Cooldown beachten)
        vm.AdRunRequested += async () =>
        {
            var result = await _rewardedAdService.ShowAdAsync("dungeon_run");
            if (result)
            {
                RewardedAdCooldownTracker.RecordAdShown();
                vm.OnAdRunRewarded();
            }
        };
        // Dungeon Master Pass: IAP-Kauf (permanenter 2x DungeonCoin-Boost)
        vm.DungeonMasterPassRequested += async () =>
        {
            var success = await _purchaseService.PurchaseConsumableAsync("dungeon_master_pass");
            if (success)
                vm.OnDungeonMasterPassPurchased();
        };
        DungeonVm = vm;
        return vm;
    }

    private BattlePassViewModel EnsureBattlePassVm()
    {
        if (BattlePassVm is { } existing) return existing;
        var vm = _battlePassVmLazy.Value;
        WireCommon(vm);
        // Battle Pass Premium-Kauf anfordern
        vm.PremiumPurchaseRequested += async () =>
        {
            var success = await _purchaseService.PurchaseConsumableAsync("battle_pass_premium");
            if (success)
                vm.OnPremiumPurchaseConfirmed();
        };
        BattlePassVm = vm;
        return vm;
    }

    private CollectionViewModel EnsureCollectionVm()
    {
        if (CollectionVm is { } existing) return existing;
        var vm = _collectionVmLazy.Value;
        WireCommon(vm);
        CollectionVm = vm;
        return vm;
    }

    private LeagueViewModel EnsureLeagueVm()
    {
        if (LeagueVm is { } existing) return existing;
        var vm = _leagueVmLazy.Value;
        WireCommon(vm);
        LeagueVm = vm;
        return vm;
    }

    private ProfileViewModel EnsureProfileVm()
    {
        if (ProfileVm is { } existing) return existing;
        var vm = _profileVmLazy.Value;
        WireCommon(vm);
        ProfileVm = vm;
        return vm;
    }

    private GemShopViewModel EnsureGemShopVm()
    {
        if (GemShopVm is { } existing) return existing;
        var vm = _gemShopVmLazy.Value;
        WireCommon(vm);
        vm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);
        GemShopVm = vm;
        return vm;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Typsichere Navigation: Konvertiert NavigationRequest in Route-String
    /// und delegiert an die bestehende String-basierte NavigateTo-Methode.
    /// Audit H09: async void mit try/catch — wird auch aus HandleBackPressed (synchron, fire-and-forget) gerufen,
    /// ungefangene Exceptions wuerden sonst TaskScheduler.UnobservedTaskException ausloesen.
    /// </summary>
    public async void NavigateTo(NavigationRequest request)
    {
        try
        {
        var route = request switch
        {
            GoMainMenu => "MainMenu",
            GoLevelSelect => "LevelSelect",
            GoSettings => "Settings",
            GoShop => "Shop",
            GoAchievements => "Achievements",
            GoHighScores => "HighScores",
            GoHelp => "Help",
            GoStatistics => "Statistics",
            GoProfile => "Profile",
            GoDailyChallenge => "DailyChallenge",
            GoLuckySpin => "LuckySpin",
            GoQuickPlay => "QuickPlay",
            GoWeeklyChallenge => "WeeklyChallenge",
            GoCollection => "Collection",
            GoDeck => "Deck",
            GoDungeon => "Dungeon",
            GoBattlePass => "BattlePass",
            GoLeague => "League",
            GoGemShop => "GemShop",
            GoBossRush => "BossRush",
            GoDailyRace => "DailyRace",
            GoBack => "..",
            GoGame g => $"Game?mode={g.Mode}&level={g.Level}&difficulty={g.Difficulty}&continue={g.Continue}&boost={g.Boost}&floor={g.Floor}&seed={g.Seed}&master={g.MasterMode}",
            GoGameOver go => $"GameOver?score={go.Score}&level={go.Level}&highscore={go.IsHighScore}&mode={go.Mode}&coins={go.Coins}&levelcomplete={go.LevelComplete}&cancontinue={go.CanContinue}&enemypts={go.EnemyPoints}&timebonus={go.TimeBonus}&effbonus={go.EfficiencyBonus}&multiplier={go.Multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}&kills={go.Kills}&survivaltime={go.SurvivalTime.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            GoVictory v => $"Victory?score={v.Score}&coins={v.Coins}",
            GoResetThen r => $"//MainMenu/{NavigationRequestToRoute(r.Then)}",
            _ => "MainMenu"
        };
        await NavigateToRouteAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogError($"NavigateTo(NavigationRequest) unbehandelte Exception fuer {request?.GetType().Name}", ex);
        }
    }

    /// <summary>
    /// Hilfsmethode für GoResetThen: Konvertiert inneren Request in Route-String.
    /// </summary>
    private string NavigationRequestToRoute(NavigationRequest request)
    {
        // Rekursiv: gleiche Logik wie NavigateTo, aber gibt String zurück.
        // Aktuell wird GoResetThen nur mit GoGame/GoMainMenu aufgerufen.
        // Bei Erweiterung um neue Inner-Requests muss diese Methode mit-erweitert werden
        // — Logging fängt silent-fallbacks ab.
        switch (request)
        {
            case GoGame g:
                return $"Game?mode={g.Mode}&level={g.Level}&difficulty={g.Difficulty}&continue={g.Continue}&boost={g.Boost}&floor={g.Floor}&seed={g.Seed}&master={g.MasterMode}";
            case GoMainMenu:
                return "MainMenu";
            default:
                _logger.LogWarning($"NavigationRequestToRoute: Unsupported inner request {request.GetType().Name} → fallback MainMenu");
                return "MainMenu";
        }
    }

    /// <summary>
    /// Navigiert zu einer bestimmten View. Versteckt alle anderen.
    /// Unterstützt Routen wie "Game?mode=story&amp;level=5" und
    /// zusammengesetzte Routen wie "//MainMenu/Game?mode=story".
    /// </summary>
    public async Task NavigateToRouteAsync(string route)
    {
        try
        {
        // Zusammengesetzte Routen behandeln (z.B. "//MainMenu/Game?mode=story")
        if (route.StartsWith("//"))
        {
            var withoutPrefix = route[2..];
            var slashIndex = withoutPrefix.IndexOf('/');
            if (slashIndex >= 0)
                route = withoutPrefix[(slashIndex + 1)..];
            else
                route = withoutPrefix;
        }

        var baseRoute = route.Contains('?') ? route[..route.IndexOf('?')] : route;

        // Cloud-Save-Init MUSS abgeschlossen sein bevor wir in Game/LevelSelect navigieren —
        // sonst kann ein "Continue"-Tap auf frischem Geraet den leeren lokalen State
        // mit Cloud-Progress racen und die Cloud ueberschreiben.
        if (baseRoute is "Game" or "LevelSelect" or "Dungeon" or "DailyChallenge" or "WeeklyChallenge" or "Deck" or "Collection")
        {
            if (_cloudSaveInitTask is { IsCompleted: false } task)
            {
                try
                {
                    // 3s-Cap: Bei Netzproblemen kein endloses Blocken — lokaler State wird genutzt
                    var completed = await Task.WhenAny(task, Task.Delay(3000));
                    if (completed != task)
                        _logger?.LogWarning("CloudSave-Init dauert >3s - navigiere ohne Cloud-Sync");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"CloudSave-Init Fehler vor Navigation: {ex.Message}");
                }
            }
        }

        // Aktuellen Zustand merken (für Zurück-Navigation)
        var wasGameActive = IsGameActive;

        // Lifecycle: Game-Loop stoppen beim Verlassen der Game-View.
        // GameVm? — nullable wegen Lazy-Resolution; bei wasGameActive=true existiert GameVm garantiert.
        if (wasGameActive && baseRoute != "Game")
        {
            GameVm?.OnDisappearing();
        }

        HideAll();

        // Navigations-Sound (nicht beim Game-Start, das hat eigene Sounds)
        if (baseRoute != "Game")
            _soundManager.PlaySound(SoundManager.SFX_MENU_SELECT);

        // Kein Banner in BomberBlast (Landscape) - nur Rewarded Ads
        IsAdBannerVisible = false;

        switch (baseRoute)
        {
            case "MainMenu":
                _returnToGameFromSettings = false;
                ActiveView = ActiveView.MainMenu;
                MenuVm.OnAppearing();
                break;

            case "Game":
                // EnsureGameVm zuerst laden (loest Lazy<GameViewModel> auf + setzt GameVm),
                // erst DANACH ActiveView=Game setzen — sonst ein Frame mit IsGameActive=true + GameVm=null,
                // ContentControl-Binding wuerde einen leeren GameBorder zeigen.
                var gameVm = EnsureGameVm();
                ActiveView = ActiveView.Game;
                // Spiel-Parameter parsen
                if (route.Contains('?'))
                {
                    var query = route[(route.IndexOf('?') + 1)..];
                    var mode = "quick";
                    var level = 1;
                    var difficulty = 5;
                    var continueMode = false;
                    var boost = "";
                    var floor = 0;
                    var seed = 0;
                    var master = false;
                    foreach (var param in query.Split('&'))
                    {
                        var parts = param.Split('=');
                        if (parts.Length == 2)
                        {
                            if (parts[0] == "mode") mode = parts[1];
                            if (parts[0] == "level") int.TryParse(parts[1], out level);
                            if (parts[0] == "difficulty") int.TryParse(parts[1], out difficulty);
                            if (parts[0] == "continue") bool.TryParse(parts[1], out continueMode);
                            if (parts[0] == "boost") boost = parts[1];
                            if (parts[0] == "floor") int.TryParse(parts[1], out floor);
                            if (parts[0] == "seed") int.TryParse(parts[1], out seed);
                            if (parts[0] == "master") bool.TryParse(parts[1], out master);
                        }
                    }
                    gameVm.SetParameters(mode, level, continueMode, boost, difficulty, floor, seed, master);
                }
                // Spiel starten (Engine initialisieren + Render-Loop starten)
                await gameVm.OnAppearingAsync();
                break;

            case "LevelSelect":
                ActiveView = ActiveView.LevelSelect;
                LevelSelectVm.OnAppearing();
                break;

            case "HighScores":
                ActiveView = ActiveView.HighScores;
                HighScoresVm.OnAppearing();
                break;

            case "GameOver":
                ActiveView = ActiveView.GameOver;
                if (route.Contains('?'))
                {
                    var query = route[(route.IndexOf('?') + 1)..];
                    var score = 0;
                    var level = 0;
                    var isHighScore = false;
                    var mode = "story";
                    var coins = 0;
                    var levelComplete = false;
                    var canContinue = false;
                    var enemyPts = 0;
                    var timeBonus = 0;
                    var effBonus = 0;
                    var multiplier = 1f;
                    var survivalKills = 0;
                    var survivalTime = 0f;
                    foreach (var param in query.Split('&'))
                    {
                        var parts = param.Split('=');
                        if (parts.Length == 2)
                        {
                            switch (parts[0])
                            {
                                case "score": int.TryParse(parts[1], out score); break;
                                case "level": int.TryParse(parts[1], out level); break;
                                case "highscore": bool.TryParse(parts[1], out isHighScore); break;
                                case "mode": mode = parts[1]; break;
                                case "coins": int.TryParse(parts[1], out coins); break;
                                case "levelcomplete": bool.TryParse(parts[1], out levelComplete); break;
                                case "cancontinue": bool.TryParse(parts[1], out canContinue); break;
                                case "enemypts": int.TryParse(parts[1], out enemyPts); break;
                                case "timebonus": int.TryParse(parts[1], out timeBonus); break;
                                case "effbonus": int.TryParse(parts[1], out effBonus); break;
                                case "multiplier": float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out multiplier); break;
                                case "kills": int.TryParse(parts[1], out survivalKills); break;
                                case "survivaltime": float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out survivalTime); break;
                            }
                        }
                    }

                    // Fehlversuche pro Level tracken (fuer Level-Skip)
                    var fails = 0;
                    if (!levelComplete && mode == "story" && level > 0)
                    {
                        fails = _levelFailCounts.GetValueOrDefault(level) + 1;
                        _levelFailCounts[level] = fails;
                    }
                    else if (levelComplete && level > 0)
                    {
                        _levelFailCounts.Remove(level);
                    }

                    GameOverVm.SetParameters(score, level, isHighScore, mode, coins, levelComplete, canContinue, fails,
                        enemyPts, timeBonus, effBonus, multiplier, survivalKills, survivalTime);

                    // Quick-Play Score an QuickPlayVM für Challenge-Sharing weiterreichen
                    if (mode == "quick" && score > 0)
                        EnsureQuickPlayVm().SetLastScore(score);

                    // Daily Challenge: Score melden + Streak-Bonus vergeben
                    if (mode == "daily" && score > 0)
                    {
                        EnsureDailyChallengeVm().SubmitScore(score);
                    }

                    // Level Complete → Confetti + Floating Text
                    if (levelComplete)
                    {
                        CelebrationRequested?.Invoke();
                        FloatingTextRequested?.Invoke(
                            _localizationService.GetString("LevelComplete") ?? "Level Complete!",
                            "success");
                    }
                }
                break;

            // Shop (kombiniert mit Glücksrad)
            case "Shop":
                ActiveView = ActiveView.Shop;
                IsShopSpinTab = false;
                EnsureShopVm().OnAppearing();
                break;

            case "LuckySpin":
                ActiveView = ActiveView.Shop;
                IsShopSpinTab = true;
                EnsureLuckySpinVm().OnAppearing();
                break;

            // Profile-Hub (v2.0.43): 5 Tabs intern (Übersicht/Statistik/Achievements/Sammlung/Customize)
            case "Profile":
                ActiveView = ActiveView.Profile;
                EnsureProfileVm().OnAppearing();
                break;

            case "Achievements":
                ActiveView = ActiveView.Profile;
                {
                    var p = EnsureProfileVm();
                    p.OnAppearing();
                    p.SelectTabCommand.Execute("Achievements");
                }
                break;

            // Einstellungen (kombiniert mit Hilfe)
            case "Settings":
                _returnToGameFromSettings = wasGameActive;
                ActiveView = ActiveView.Settings;
                IsSettingsHelpTab = false;
                SettingsVm.OnAppearing();
                break;

            case "Help":
                ActiveView = ActiveView.Settings;
                IsSettingsHelpTab = true;
                break;

            // Karten/Deck (v2.0.43): Sammlung lebt jetzt im Profile-Hub
            case "Cards":
            case "Deck":
                ActiveView = ActiveView.Cards;
                EnsureDeckVm().OnAppearing();
                break;

            case "Collection":
                // Collection wandert in den Profile-Hub als Sammlung-Tab.
                ActiveView = ActiveView.Profile;
                {
                    var p = EnsureProfileVm();
                    p.OnAppearing();
                    p.SelectTabCommand.Execute("Collection");
                }
                break;

            // Herausforderungen (Daily + Missions kombiniert)
            case "Challenges":
            case "DailyChallenge":
                ActiveView = ActiveView.Challenges;
                IsChallengesMissionsTab = false;
                EnsureDailyChallengeVm().OnAppearing();
                break;

            case "WeeklyChallenge":
                ActiveView = ActiveView.Challenges;
                IsChallengesMissionsTab = true;
                EnsureWeeklyChallengeVm().OnAppearing();
                break;

            case "Statistics":
                // Statistics wandert in den Profile-Hub als Statistik-Tab.
                ActiveView = ActiveView.Profile;
                {
                    var p = EnsureProfileVm();
                    p.OnAppearing();
                    p.SelectTabCommand.Execute("Statistics");
                }
                break;

            case "QuickPlay":
                ActiveView = ActiveView.QuickPlay;
                EnsureQuickPlayVm().OnAppearing();
                break;

            case "Dungeon":
                ActiveView = ActiveView.Dungeon;
                EnsureDungeonVm().OnAppearing();
                break;

            case "BattlePass":
                ActiveView = ActiveView.BattlePass;
                EnsureBattlePassVm().OnAppearing();
                break;

            case "League":
                ActiveView = ActiveView.League;
                EnsureLeagueVm().OnAppearing();
                break;

            case "GemShop":
                ActiveView = ActiveView.GemShop;
                EnsureGemShopVm().OnAppearing();
                break;

            case "BossRush":
                ActiveView = ActiveView.BossRush;
                BossRushVm.OnAppearing();
                break;

            case "DailyRace":
                // Daily Race nutzt Daily-Challenge-Tab in der Liga-View, daher leite via League
                ActiveView = ActiveView.League;
                EnsureLeagueVm().OnAppearing();
                break;

            case "Victory":
                ActiveView = ActiveView.Victory;
                VictoryVm.OnAppearing();
                // Query-Parameter parsen (score, coins)
                if (route.Contains('?'))
                {
                    var vQuery = route[(route.IndexOf('?') + 1)..];
                    var vScore = 0;
                    var vCoins = 0;
                    foreach (var param in vQuery.Split('&'))
                    {
                        var parts = param.Split('=');
                        if (parts.Length == 2)
                        {
                            if (parts[0] == "score") int.TryParse(parts[1], out vScore);
                            if (parts[0] == "coins") int.TryParse(parts[1], out vCoins);
                        }
                    }
                    VictoryVm.SetScore(vScore);
                    // Coins gutschreiben
                    if (vCoins > 0) _coinService.AddCoins(vCoins);
                }
                CelebrationRequested?.Invoke();
                FloatingTextRequested?.Invoke(
                    _localizationService.GetString("VictoryTitle") ?? "Victory!",
                    "gold");
                break;

            case "..":
                // Zurück-Navigation: zum Spiel zurückkehren wenn Einstellungen aus dem Spiel geöffnet wurden.
                // EnsureGameVm() statt GameVm direkt — robuster falls Lazy-Resolution noch nicht passiert ist.
                if (_returnToGameFromSettings)
                {
                    _returnToGameFromSettings = false;
                    ActiveView = ActiveView.Game;
                    IsAdBannerVisible = false;
                    await EnsureGameVm().OnAppearingAsync();
                }
                else
                {
                    ActiveView = ActiveView.MainMenu;
                    MenuVm.OnAppearing();
                }
                break;

            default:
                ActiveView = ActiveView.MainMenu;
                MenuVm.OnAppearing();
                break;
        }
        }
        catch (Exception ex)
        {
            _logger.LogError($"NavigateTo Fehler bei Route '{route}'", ex);
            // Fallback: Zurück zum Hauptmenü damit die App nicht hängt
            try
            {
                HideAll();
                ActiveView = ActiveView.MainMenu;
                MenuVm.OnAppearing();
            }
            catch (Exception fallbackEx)
            {
                // Audit H09: Letzter Ausweg — App lebt weiter, aber Fehler wird geloggt (kein silent-fail).
                _logger.LogError($"NavigateTo Fallback fehlgeschlagen fuer Route '{route}'", fallbackEx);
            }
        }
    }

    /// <summary>
    /// Setzt alle View-States auf inaktiv. Mit der ActiveView-Enum reicht ein Setter,
    /// die Tab-States werden zusaetzlich zurueckgesetzt damit auf re-entry der Default greift.
    /// </summary>
    private void HideAll()
    {
        ActiveView = ActiveView.None;

        // Tab-States zurücksetzen
        IsShopSpinTab = false;
        IsProfileAchievementsTab = false;
        IsSettingsHelpTab = false;
        IsCardsCollectionTab = false;
        IsChallengesMissionsTab = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TAB-SWITCHING (für kombinierte Views)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SwitchToShopTab() { IsShopSpinTab = false; EnsureShopVm().OnAppearing(); }

    [RelayCommand]
    private void SwitchToSpinTab() { IsShopSpinTab = true; EnsureLuckySpinVm().OnAppearing(); }

    [RelayCommand]
    private void SwitchToProfileTab() { IsProfileAchievementsTab = false; EnsureProfileVm().OnAppearing(); }

    [RelayCommand]
    private void SwitchToAchievementsTab() { IsProfileAchievementsTab = true; EnsureAchievementsVm().OnAppearing(); }

    [RelayCommand]
    private void SwitchToSettingsTab() { IsSettingsHelpTab = false; SettingsVm.OnAppearing(); }

    [RelayCommand]
    private void SwitchToHelpTab() { IsSettingsHelpTab = true; }

    [RelayCommand]
    private void SwitchToDeckTab() { IsCardsCollectionTab = false; EnsureDeckVm().OnAppearing(); }

    [RelayCommand]
    private void SwitchToCollectionTab() { IsCardsCollectionTab = true; EnsureCollectionVm().OnAppearing(); }

    [RelayCommand]
    private void SwitchToDailyChallengeTab() { IsChallengesMissionsTab = false; EnsureDailyChallengeVm().OnAppearing(); }

    [RelayCommand]
    private void SwitchToMissionsTab() { IsChallengesMissionsTab = true; EnsureWeeklyChallengeVm().OnAppearing(); }

    // ═══════════════════════════════════════════════════════════════════════
    // BACK-NAVIGATION (Android Hardware-Zurücktaste)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Hierarchische Back-Navigation. Gibt true zurück wenn das Event behandelt wurde,
    /// false wenn die App geschlossen werden soll.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Offene Dialoge schließen (höchste Priorität)
        if (IsConfirmDialogVisible)
        {
            CancelConfirm();
            return true;
        }
        if (IsAlertDialogVisible)
        {
            DismissAlert();
            return true;
        }

        // 2. Score-Double Overlay → überspringen
        // GameVm ist nur initialisiert wenn der User im Spiel war/ist; Score-Double
        // erscheint ausschliesslich nach LevelComplete, also IST GameVm dann garantiert nicht null.
        if (GameVm is { ShowScoreDoubleOverlay: true })
        {
            GameVm.SkipDoubleScoreCommand.Execute(null);
            return true;
        }

        // 3. Im Spiel: Pause/Resume
        if (IsGameActive && GameVm is not null)
        {
            if (GameVm.IsPaused)
            {
                // Pause → Resume
                GameVm.ResumeCommand.Execute(null);
            }
            else if (GameVm.State == Core.GameState.Playing)
            {
                // Spielend → Pause
                GameVm.PauseCommand.Execute(null);
            }
            else
            {
                // Andere Game-States (Starting, PlayerDied etc.) → zum Menü
                GameVm.OnDisappearing();
                HideAll();
                ActiveView = ActiveView.MainMenu;
                MenuVm.OnAppearing();
            }
            return true;
        }

        // 4. Settings → zurück (zum Spiel oder Menü)
        if (IsSettingsActive)
        {
            NavigateTo(new GoBack());
            return true;
        }

        // 5. Alle anderen Sub-Views → zurück zum Hauptmenü
        if (ActiveView is not ActiveView.MainMenu and not ActiveView.None and not ActiveView.Game and not ActiveView.Settings)
        {
            HideAll();
            ActiveView = ActiveView.MainMenu;
            MenuVm.OnAppearing();
            return true;
        }

        // 6. Hauptmenü → Double-Back-to-Exit
        if (IsMainMenuActive)
        {
            var msg = _localizationService.GetString("PressBackAgainToExit") ?? "Press back again to exit";
            return _backPressHelper.HandleDoubleBack(msg);
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DIALOGS
    // ═══════════════════════════════════════════════════════════════════════

    private void ShowAlertDialog(string title, string message, string buttonText)
    {
        AlertDialogTitle = title;
        AlertDialogMessage = message;
        AlertDialogButtonText = buttonText;
        IsAlertDialogVisible = true;
    }

    [RelayCommand]
    private void DismissAlert()
    {
        IsAlertDialogVisible = false;
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
    private void AcceptConfirm()
    {
        IsConfirmDialogVisible = false;
        _confirmDialogTcs?.TrySetResult(true);
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        IsConfirmDialogVisible = false;
        _confirmDialogTcs?.TrySetResult(false);
    }

    /// <summary>
    /// Benannter Handler fuer AdUnavailable (statt Lambda, damit Unsubscribe moeglich)
    /// </summary>
    private void OnAdUnavailable()
    {
        ShowAlertDialog(AppStrings.AdVideoNotAvailableTitle, AppStrings.AdVideoNotAvailableMessage, AppStrings.OK);
    }
}
