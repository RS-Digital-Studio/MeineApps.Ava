using BomberBlast.Core;
using BomberBlast.Navigation;
using BomberBlast.Resources.Strings;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;
using Microsoft.Extensions.Logging;

namespace BomberBlast.ViewModels;

/// <summary>
/// Haupt-Compositor-ViewModel. Buendelt die fuenf Feature-Module
/// (<see cref="IChildViewModelRegistry"/>, <see cref="BomberBlast.Navigation.IBottomTabController"/>,
/// <see cref="BomberBlast.Navigation.INavigationCoordinator"/>,
/// <see cref="BomberBlast.Services.IDialogPresenter"/>, <see cref="ILifecycleHub"/>) und
/// forwarded deren State an die AXAML-Bindings von MainView.axaml.
///
/// <para>Eigene Logik beschraenkt sich auf: Event-Forwarding (FloatingText/Celebration/ExitHint),
/// Property-Forwarder mit PropertyChanged-Routing, RelayCommand-Delegation und das
/// Achievement-Toast-Wiring.</para>
///
/// <para><b>Lifetime-Hinweis:</b> Singleton mit App-Lifetime. Alle Event-Subscriptions
/// im Konstruktor sind bewusst per Lambda registriert — Unsubscribe ist nicht nötig,
/// weil Module + Child-ViewModels (ebenfalls Singleton) gemeinsam bis Prozess-Ende leben.</para>
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

    // VM-Properties sind Forwarder auf den IChildViewModelRegistry. Die Registry
    // verwaltet Eager + Lazy + Sub-Wirings. MainView.axaml bindet weiterhin direkt
    // auf MainViewModel.{Vm-Name} — kein AXAML-Change noetig.

    public MainMenuViewModel MenuVm => _registry.MenuVm;
    public LevelSelectViewModel LevelSelectVm => _registry.LevelSelectVm;
    public SettingsViewModel SettingsVm => _registry.SettingsVm;
    public HighScoresViewModel HighScoresVm => _registry.HighScoresVm;
    public GameOverViewModel GameOverVm => _registry.GameOverVm;
    public HelpViewModel HelpVm => _registry.HelpVm;
    public VictoryViewModel VictoryVm => _registry.VictoryVm;
    public WhatsNewViewModel WhatsNewVm => _registry.WhatsNewVm;
    public BossRushViewModel BossRushVm => _registry.BossRushVm;
    public PlayHubViewModel PlayHubVm => _registry.PlayHubVm;
    public BottomTabBarViewModel BottomTabVm => _registry.BottomTabVm;

    /// <summary>
    /// True solange das What's-New-Modal sichtbar ist (gesteuert vom Closed-Event des VMs).
    /// State liegt im DialogPresenter (Aggregat-Beitrag fuer IsAnyDialogOpen).
    /// </summary>
    public bool IsWhatsNewVisible => _dialogPresenter.IsWhatsNewVisible;

    // Lazy-VMs (spaet-unlocked). Forwarder auf die Registry — bei Lazy-Resolve feuert
    // Registry.VmInstantiated den Property-Namen, MainViewModel ruft daraufhin
    // OnPropertyChanged(name) damit XAML-ContentControls den neuen VM einbinden.
    public GameViewModel? GameVm => _registry.GameVm;
    public ShopViewModel? ShopVm => _registry.ShopVm;
    public AchievementsViewModel? AchievementsVm => _registry.AchievementsVm;
    public DailyChallengeViewModel? DailyChallengeVm => _registry.DailyChallengeVm;
    public LuckySpinViewModel? LuckySpinVm => _registry.LuckySpinVm;
    public WeeklyChallengeViewModel? WeeklyChallengeVm => _registry.WeeklyChallengeVm;
    public StatisticsViewModel? StatisticsVm => _registry.StatisticsVm;
    public QuickPlayViewModel? QuickPlayVm => _registry.QuickPlayVm;
    public DeckViewModel? DeckVm => _registry.DeckVm;
    public DungeonViewModel? DungeonVm => _registry.DungeonVm;
    public BattlePassViewModel? BattlePassVm => _registry.BattlePassVm;
    public CollectionViewModel? CollectionVm => _registry.CollectionVm;
    public LeagueViewModel? LeagueVm => _registry.LeagueVm;
    public ProfileViewModel? ProfileVm => _registry.ProfileVm;
    public GemShopViewModel? GemShopVm => _registry.GemShopVm;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktiv sichtbare Page. Ersetzt 17 frueher separate IsXxxActive-Booleans.
    /// MainView.axaml bindet ueber <see cref="Converters.ActiveViewEqualsConverter"/> auf
    /// <c>Classes.Active</c> und <c>IsVisible</c> der einzelnen PageView-Border.
    ///
    /// <para>Source-of-Truth liegt im <see cref="INavigationCoordinator"/> — diese Property ist
    /// nur ein Forwarder. PropertyChanged-Notifications laufen ueber
    /// <see cref="OnNavigationActiveViewChanged"/> (Subscription auf ActiveViewChanged).</para>
    /// </summary>
    public ActiveView ActiveView => _navigationCoordinator.ActiveView;

    // Backward-compat Computed-Properties — werden von Logik in HandleBackPressed()
    // weiter genutzt (Game-Lifecycle). Notifications via OnNavigationActiveViewChanged.
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
    public bool IsPlayHubActive => ActiveView == ActiveView.PlayHub;

    /// <summary>
    /// Bottom-Tab-Bar ist sichtbar auf den 4 konsolidierten Tab-Haupt-Views
    /// (Home/Play/Shop/Profile) — nicht im Game, LevelSelect, Dialogen etc.
    /// Wird vom <see cref="IBottomTabController"/> berechnet und ueber StateChanged propagiert.
    /// </summary>
    public bool IsBottomTabBarVisible => _tabController.IsBottomTabBarVisible;

    /// <summary>
    /// <see cref="INavigationCoordinator.ActiveViewChanged"/>-Handler. Synct den
    /// BottomTabController und feuert OnPropertyChanged fuer ActiveView + alle 18
    /// IsXxxActive-Computed-Properties.
    /// </summary>
    private void OnNavigationActiveViewChanged(ActiveView view)
    {
        _tabController.OnActiveViewChanged(view);
        OnPropertyChanged(nameof(ActiveView));
        OnPropertyChanged(nameof(IsMainMenuActive));
        OnPropertyChanged(nameof(IsGameActive));
        OnPropertyChanged(nameof(IsLevelSelectActive));
        OnPropertyChanged(nameof(IsSettingsActive));
        OnPropertyChanged(nameof(IsHighScoresActive));
        OnPropertyChanged(nameof(IsGameOverActive));
        OnPropertyChanged(nameof(IsShopActive));
        OnPropertyChanged(nameof(IsVictoryActive));
        OnPropertyChanged(nameof(IsStatisticsActive));
        OnPropertyChanged(nameof(IsQuickPlayActive));
        OnPropertyChanged(nameof(IsDungeonActive));
        OnPropertyChanged(nameof(IsBattlePassActive));
        OnPropertyChanged(nameof(IsLeagueActive));
        OnPropertyChanged(nameof(IsProfileActive));
        OnPropertyChanged(nameof(IsGemShopActive));
        OnPropertyChanged(nameof(IsCardsActive));
        OnPropertyChanged(nameof(IsChallengesActive));
        OnPropertyChanged(nameof(IsPlayHubActive));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TAB-STATE PROPERTIES (Forwarder auf IBottomTabController)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Shop: false=Shop-Tab, true=Glücksrad-Tab</summary>
    public bool IsShopSpinTab { get => _tabController.IsShopSpinTab; set => _tabController.IsShopSpinTab = value; }

    /// <summary>Profil: false=Profil-Tab, true=Erfolge-Tab</summary>
    public bool IsProfileAchievementsTab { get => _tabController.IsProfileAchievementsTab; set => _tabController.IsProfileAchievementsTab = value; }

    /// <summary>Einstellungen: false=Settings-Tab, true=Hilfe-Tab</summary>
    public bool IsSettingsHelpTab { get => _tabController.IsSettingsHelpTab; set => _tabController.IsSettingsHelpTab = value; }

    /// <summary>Karten: false=Deck-Tab, true=Sammlung-Tab</summary>
    public bool IsCardsCollectionTab { get => _tabController.IsCardsCollectionTab; set => _tabController.IsCardsCollectionTab = value; }

    /// <summary>Herausforderungen: false=Daily-Tab, true=Missions-Tab</summary>
    public bool IsChallengesMissionsTab { get => _tabController.IsChallengesMissionsTab; set => _tabController.IsChallengesMissionsTab = value; }

    /// <summary>
    /// Ad-Banner-Spacer: sichtbar in Menü-Views, versteckt im Game-View
    /// </summary>
    [ObservableProperty]
    private bool _isAdBannerVisible;

    // ═══════════════════════════════════════════════════════════════════════
    // DIALOG PROPERTIES (Forwarder auf IDialogPresenter)
    // ═══════════════════════════════════════════════════════════════════════
    // State + Logik liegen im DialogPresenter. PropertyChanged-Notifications
    // werden ueber dialogPresenter.StateChanged geroutet (im Ctor verdrahtet).

    public bool IsAlertDialogVisible => _dialogPresenter.IsAlertDialogVisible;
    public string AlertDialogTitle => _dialogPresenter.AlertDialogTitle;
    public string AlertDialogMessage => _dialogPresenter.AlertDialogMessage;
    public string AlertDialogButtonText => _dialogPresenter.AlertDialogButtonText;

    public bool IsConfirmDialogVisible => _dialogPresenter.IsConfirmDialogVisible;
    public string ConfirmDialogTitle => _dialogPresenter.ConfirmDialogTitle;
    public string ConfirmDialogMessage => _dialogPresenter.ConfirmDialogMessage;
    public string ConfirmDialogAcceptText => _dialogPresenter.ConfirmDialogAcceptText;
    public string ConfirmDialogCancelText => _dialogPresenter.ConfirmDialogCancelText;

    /// <summary>
    /// Audit M18: Aggregat-Flag fuer alle modalen Dialoge. View bindet darunterliegende Page-Views
    /// IsHitTestVisible="{Binding !IsAnyDialogOpen}" → Android-ZIndex-Hit-Test-Problem entschaerft.
    /// Aggregat lebt jetzt im DialogPresenter (inkl. WhatsNew-Flag).
    /// </summary>
    public bool IsAnyDialogOpen => _dialogPresenter.IsAnyDialogOpen;

    private readonly IAchievementService _achievementService;
    private readonly ILogger<MainViewModel> _logger;
    /// <summary>GameEventBus — VMs ueber den Service routen statt durch MainVM.</summary>
    private readonly IGameEventBus _eventBus;
    /// <summary>Dialog-State liegt im DialogPresenter — MainVM ist nur noch Forwarder.</summary>
    private readonly IDialogPresenter _dialogPresenter;

    /// <summary>Verwaltet alle Child-VMs (Eager + Lazy + Sub-Wirings).</summary>
    private readonly IChildViewModelRegistry _registry;

    /// <summary>Verwaltet Bottom-Tab-State und Sub-Tab-Switching.</summary>
    private readonly IBottomTabController _tabController;

    /// <summary>Haelt ActiveView + die komplette Routing-Logik.</summary>
    private readonly INavigationCoordinator _navigationCoordinator;

    /// <summary>Haelt BackPress, CloudSave-Init und Rewarded-Ad-Unavailable-Handling.</summary>
    private readonly ILifecycleHub _lifecycleHub;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// MainViewModel ist ein Compositor: er buendelt die fuenf Feature-Module
    /// (Registry, BottomTabController, NavigationCoordinator, DialogPresenter, LifecycleHub)
    /// plus die MainViewModelDependencies-Aggregat-Dependency und forwarded deren State an
    /// die AXAML-Bindings.
    /// </summary>
    public MainViewModel(
        MainViewModelDependencies deps,
        IChildViewModelRegistry registry,
        IBottomTabController tabController,
        INavigationCoordinator navigationCoordinator,
        ILifecycleHub lifecycleHub)
    {
        // Registry haelt alle Child-VMs + Sub-Wirings. Events routen Navigation + VM-Lazy-Instantiation
        // an MainViewModel zurueck, damit AXAML-Bindings (ContentControl auf MenuVm/GameVm/ShopVm etc.)
        // beim ersten Resolve eine PropertyChanged-Notification bekommen.
        _registry = registry;
        _registry.NavigationRequested += NavigateTo;
        _registry.VmInstantiated += OnRegistryVmInstantiated;

        // BottomTabController haelt Tab-State + Sub-Tab-Switching. StateChanged routet
        // OnPropertyChanged auf MainViewModel — Forwarder-Properties reagieren auf Tab-Toggles.
        _tabController = tabController;
        _tabController.StateChanged += OnTabControllerStateChanged;

        // NavigationCoordinator haelt ActiveView + Routing-Logik. ActiveViewChanged routet
        // OnPropertyChanged fuer ActiveView + alle IsXxxActive-Computed-Properties.
        _navigationCoordinator = navigationCoordinator;
        _navigationCoordinator.ActiveViewChanged += OnNavigationActiveViewChanged;

        // LifecycleHub haelt BackPress + CloudSave-Init + Rewarded-Ad-Unavailable.
        // ExitHintRequested wird an das MainViewModel-Event geforwarded (MainActivity-Subscription).
        _lifecycleHub = lifecycleHub;
        _lifecycleHub.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // DialogPresenter haelt den Dialog-State.
        // StateChanged → alle Dialog-Properties + IsAnyDialogOpen neu feuern, damit Bindings reagieren.
        _dialogPresenter = deps.DialogPresenter;
        _dialogPresenter.StateChanged += OnDialogPresenterStateChanged;

        _achievementService = deps.AchievementService;
        _logger = deps.Logger;
        _eventBus = deps.EventBus;

        // Lokale Aliase fuer den Konstruktor-Body.
        var localization = deps.Localization;
        var adService = deps.AdService;
        var menuVm = registry.MenuVm;
        var settingsVm = registry.SettingsVm;

        // GameEventBus → MainVM-Events forwarden.
        // Andere ViewModels koennen direkt _eventBus.RaiseFloatingText() rufen,
        // ohne durch MainViewModel routen zu muessen — God-VM-Abhaengigkeit reduziert.
        _eventBus.FloatingTextRequested += (t, s) => FloatingTextRequested?.Invoke(t, s);
        _eventBus.CelebrationRequested += () => CelebrationRequested?.Invoke();
        _eventBus.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);

        // ───────────────────────────────────────────────────────────────────
        // Eager-VMs verdrahten — Registry.WireCommon haengt Navigation + Game-Juice
        // (FloatingText + Celebration ueber EventBus) pro VM ein. Lazy-VMs werden in
        // den Ensure-Methoden der Registry verdrahtet beim ersten Zugriff.
        // ───────────────────────────────────────────────────────────────────
        registry.WireCommon(menuVm);
        registry.WireCommon(registry.LevelSelectVm);
        registry.WireCommon(settingsVm);
        registry.WireCommon(registry.HighScoresVm);
        registry.WireCommon(registry.GameOverVm);
        registry.WireCommon(registry.HelpVm);
        registry.WireCommon(registry.VictoryVm);
        registry.WireCommon(registry.BossRushVm);
        registry.WireCommon(registry.PlayHubVm);

        // GameOverVm-spezifische Events (Confirmation Dialog)
        registry.GameOverVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);

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

        // PauseViewModel entfernt — Resume/Restart laufen direkt ueber GameVm.{Resume,Restart}Command,
        // gebunden im GameView.axaml Pause-Overlay. SkiaSharp-Canvas rendert die Pause-Anzeige.

        // Dialog-Events von SettingsVM verdrahten (SettingsVm ist eager)
        settingsVm.AlertRequested += (t, m, b) => ShowAlertDialog(t, m, b);
        settingsVm.ConfirmationRequested += (t, m, a, c) => ShowConfirmDialog(t, m, a, c);

        // LanguageChanged: Registry routet an alle instanziierten VMs.
        localization.LanguageChanged += (_, _) => registry.RefreshAllLocalizedTexts();

        // What's-New-Modal: Closed-Event verdrahten + Initial-Check ob anzeigen.
        // ShouldShow prueft Service-State (CurrentVersion > LastSeenVersion + Eintraege vorhanden).
        // Sichtbarkeit lebt im DialogPresenter (Aggregat-Beitrag).
        WhatsNewVm.Closed += () => _dialogPresenter.SetWhatsNewVisible(false);
        if (deps.WhatsNewService.ShouldShow)
            _dialogPresenter.SetWhatsNewVisible(true);

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

    // EnsureXxxVm-Methoden delegieren an die Registry. Bleibt als duenne private Member
    // damit alle bestehenden Aufruf-Stellen (Navigation, Tab-Switch, BackPress) unveraendert
    // funktionieren — und Registry's VmInstantiated-Event triggert OnPropertyChanged hier
    // (siehe OnRegistryVmInstantiated).
    private GameViewModel EnsureGameVm() => _registry.EnsureGame();
    private ShopViewModel EnsureShopVm() => _registry.EnsureShop();
    private AchievementsViewModel EnsureAchievementsVm() => _registry.EnsureAchievements();
    private DailyChallengeViewModel EnsureDailyChallengeVm() => _registry.EnsureDailyChallenge();
    private LuckySpinViewModel EnsureLuckySpinVm() => _registry.EnsureLuckySpin();
    private WeeklyChallengeViewModel EnsureWeeklyChallengeVm() => _registry.EnsureWeeklyChallenge();
    private StatisticsViewModel EnsureStatisticsVm() => _registry.EnsureStatistics();
    private QuickPlayViewModel EnsureQuickPlayVm() => _registry.EnsureQuickPlay();
    private DeckViewModel EnsureDeckVm() => _registry.EnsureDeck();
    private DungeonViewModel EnsureDungeonVm() => _registry.EnsureDungeon();
    private BattlePassViewModel EnsureBattlePassVm() => _registry.EnsureBattlePass();
    private CollectionViewModel EnsureCollectionVm() => _registry.EnsureCollection();
    private LeagueViewModel EnsureLeagueVm() => _registry.EnsureLeague();
    private ProfileViewModel EnsureProfileVm() => _registry.EnsureProfile();
    private GemShopViewModel EnsureGemShopVm() => _registry.EnsureGemShop();

    // ═══════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Typsichere Navigation: Konvertiert NavigationRequest in Route-String
    /// und delegiert an die bestehende String-basierte NavigateTo-Methode.
    /// Audit H09: async void mit try/catch — wird auch aus HandleBackPressed (synchron, fire-and-forget) gerufen,
    /// ungefangene Exceptions wuerden sonst TaskScheduler.UnobservedTaskException ausloesen.
    /// </summary>
    // Navigation delegiert komplett an den INavigationCoordinator. Diese Methoden bleiben
    // als public/private Member damit alle bestehenden Aufruf-Stellen (Registry-Events,
    // HandleBackPressed, MainView.axaml-Bindings auf NavigateToRouteAsync) unveraendert sind.

    public void NavigateTo(NavigationRequest request) => _navigationCoordinator.NavigateTo(request);

    public Task NavigateToRouteAsync(string route) => _navigationCoordinator.NavigateToRouteAsync(route);

    // ═══════════════════════════════════════════════════════════════════════
    // TAB-SWITCHING (für kombinierte Views)
    // ═══════════════════════════════════════════════════════════════════════

    // Tab-Switch-RelayCommands delegieren an den IBottomTabController.
    [RelayCommand] private void SwitchToShopTab() => _tabController.SwitchToShopTab();
    [RelayCommand] private void SwitchToSpinTab() => _tabController.SwitchToSpinTab();
    [RelayCommand] private void SwitchToProfileTab() => _tabController.SwitchToProfileTab();
    [RelayCommand] private void SwitchToAchievementsTab() => _tabController.SwitchToAchievementsTab();
    [RelayCommand] private void SwitchToSettingsTab() => _tabController.SwitchToSettingsTab();
    [RelayCommand] private void SwitchToHelpTab() => _tabController.SwitchToHelpTab();
    [RelayCommand] private void SwitchToDeckTab() => _tabController.SwitchToDeckTab();
    [RelayCommand] private void SwitchToCollectionTab() => _tabController.SwitchToCollectionTab();
    [RelayCommand] private void SwitchToDailyChallengeTab() => _tabController.SwitchToDailyChallengeTab();
    [RelayCommand] private void SwitchToMissionsTab() => _tabController.SwitchToMissionsTab();

    // ═══════════════════════════════════════════════════════════════════════
    // BACK-NAVIGATION (Android Hardware-Zurücktaste)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Hierarchische Back-Navigation. Gibt true zurück wenn das Event behandelt wurde,
    /// false wenn die App geschlossen werden soll.
    /// </summary>
    public bool HandleBackPressed() => _lifecycleHub.HandleBackPressed();

    // ═══════════════════════════════════════════════════════════════════════
    // DIALOGS
    // ═══════════════════════════════════════════════════════════════════════

    // Diese Methoden delegieren an den IDialogPresenter.
    // Sie bleiben als private Member damit die bestehenden Subscriptions im Ctor
    // (settingsVm.AlertRequested, GameOverVm.ConfirmationRequested usw.) sowie die
    // RelayCommand-Bindings in MainView.axaml (DismissAlertCommand, AcceptConfirmCommand,
    // CancelConfirmCommand) unveraendert weiter funktionieren.

    private void ShowAlertDialog(string title, string message, string buttonText)
        => _dialogPresenter.ShowAlert(title, message, buttonText);

    [RelayCommand]
    private void DismissAlert() => _dialogPresenter.DismissAlert();

    private Task<bool> ShowConfirmDialog(string title, string message, string acceptText, string cancelText)
        => _dialogPresenter.ShowConfirmAsync(title, message, acceptText, cancelText);

    [RelayCommand]
    private void AcceptConfirm() => _dialogPresenter.AcceptConfirm();

    [RelayCommand]
    private void CancelConfirm() => _dialogPresenter.CancelConfirm();

    /// <summary>
    /// Wird vom <see cref="IChildViewModelRegistry.VmInstantiated"/>-Event gerufen wenn ein Lazy-VM
    /// gerade aufgeloest wurde. Triggert OnPropertyChanged auf der entsprechenden MainViewModel-Property
    /// damit AXAML-ContentControl den neuen VM einbindet.
    /// </summary>
    private void OnRegistryVmInstantiated(string propertyName)
    {
        OnPropertyChanged(propertyName);
    }

    /// <summary>
    /// <see cref="IBottomTabController.StateChanged"/>-Handler.
    /// Feuert alle Tab-Forwarder-Properties + IsBottomTabBarVisible neu.
    /// </summary>
    private void OnTabControllerStateChanged()
    {
        OnPropertyChanged(nameof(IsShopSpinTab));
        OnPropertyChanged(nameof(IsProfileAchievementsTab));
        OnPropertyChanged(nameof(IsSettingsHelpTab));
        OnPropertyChanged(nameof(IsCardsCollectionTab));
        OnPropertyChanged(nameof(IsChallengesMissionsTab));
        OnPropertyChanged(nameof(IsBottomTabBarVisible));
    }

    /// <summary>
    /// <see cref="IDialogPresenter.StateChanged"/>-Handler.
    /// Feuert alle Dialog-Bindings + Aggregat neu — MainView-Bindings reagieren.
    /// </summary>
    private void OnDialogPresenterStateChanged()
    {
        OnPropertyChanged(nameof(IsAlertDialogVisible));
        OnPropertyChanged(nameof(AlertDialogTitle));
        OnPropertyChanged(nameof(AlertDialogMessage));
        OnPropertyChanged(nameof(AlertDialogButtonText));
        OnPropertyChanged(nameof(IsConfirmDialogVisible));
        OnPropertyChanged(nameof(ConfirmDialogTitle));
        OnPropertyChanged(nameof(ConfirmDialogMessage));
        OnPropertyChanged(nameof(ConfirmDialogAcceptText));
        OnPropertyChanged(nameof(ConfirmDialogCancelText));
        OnPropertyChanged(nameof(IsWhatsNewVisible));
        OnPropertyChanged(nameof(IsAnyDialogOpen));
    }
}
