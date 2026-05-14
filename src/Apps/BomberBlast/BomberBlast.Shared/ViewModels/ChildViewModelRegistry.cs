using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using Microsoft.Extensions.Logging;

namespace BomberBlast.ViewModels;

/// <summary>
/// Verwaltet alle Child-ViewModels des Main-Compositors (11 Eager + 15 Lazy).
///
/// <para>
/// Eager-VMs werden im Ctor instanziiert und ueber <see cref="WireCommon"/> verdrahtet.
/// Lazy-VMs werden ueber <c>EnsureXxxVm()</c>-Methoden idempotent aufgeloest — jedes
/// erste Aufrufen instanziiert den VM, verdrahtet Common-Events (Navigation +
/// FloatingText + Celebration) und ruft <see cref="VmInstantiated"/>.
/// </para>
///
/// <para>
/// VM-spezifische Sub-Wirings (Shop.PurchaseSucceeded → Celebration, Dungeon.AdRunRequested
/// → Rewarded-Ad, BattlePass.PremiumPurchaseRequested → IAP, GemShop.ConfirmationRequested
/// → Dialog) leben hier in den jeweiligen Ensure-Methoden. So bleibt MainViewModel ein
/// reiner Compositor ohne VM-Detail-Wissen.
/// </para>
/// </summary>
public sealed class ChildViewModelRegistry : IChildViewModelRegistry
{
    // ─── Eager VMs ───────────────────────────────────────────────────────────
    public MainMenuViewModel MenuVm { get; }
    public LevelSelectViewModel LevelSelectVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public HighScoresViewModel HighScoresVm { get; }
    public GameOverViewModel GameOverVm { get; }
    public HelpViewModel HelpVm { get; }
    public VictoryViewModel VictoryVm { get; }
    public BossRushViewModel BossRushVm { get; }
    public WhatsNewViewModel WhatsNewVm { get; }
    public PlayHubViewModel PlayHubVm { get; }
    public BottomTabBarViewModel BottomTabVm { get; }

    // ─── Lazy VMs ────────────────────────────────────────────────────────────
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

    // Backing Fields fuer die nullable Properties — werden in EnsureXxx gesetzt.
    private GameViewModel? _gameVm;
    private ShopViewModel? _shopVm;
    private AchievementsViewModel? _achievementsVm;
    private DailyChallengeViewModel? _dailyChallengeVm;
    private LuckySpinViewModel? _luckySpinVm;
    private WeeklyChallengeViewModel? _weeklyChallengeVm;
    private StatisticsViewModel? _statisticsVm;
    private QuickPlayViewModel? _quickPlayVm;
    private DeckViewModel? _deckVm;
    private DungeonViewModel? _dungeonVm;
    private BattlePassViewModel? _battlePassVm;
    private CollectionViewModel? _collectionVm;
    private LeagueViewModel? _leagueVm;
    private ProfileViewModel? _profileVm;
    private GemShopViewModel? _gemShopVm;

    public GameViewModel? GameVm => _gameVm;
    public ShopViewModel? ShopVm => _shopVm;
    public AchievementsViewModel? AchievementsVm => _achievementsVm;
    public DailyChallengeViewModel? DailyChallengeVm => _dailyChallengeVm;
    public LuckySpinViewModel? LuckySpinVm => _luckySpinVm;
    public WeeklyChallengeViewModel? WeeklyChallengeVm => _weeklyChallengeVm;
    public StatisticsViewModel? StatisticsVm => _statisticsVm;
    public QuickPlayViewModel? QuickPlayVm => _quickPlayVm;
    public DeckViewModel? DeckVm => _deckVm;
    public DungeonViewModel? DungeonVm => _dungeonVm;
    public BattlePassViewModel? BattlePassVm => _battlePassVm;
    public CollectionViewModel? CollectionVm => _collectionVm;
    public LeagueViewModel? LeagueVm => _leagueVm;
    public ProfileViewModel? ProfileVm => _profileVm;
    public GemShopViewModel? GemShopVm => _gemShopVm;

    // ─── Cross-cutting Services ──────────────────────────────────────────────
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAnalyticsService _analytics;
    private readonly ILocalizationService _localization;
    private readonly IGameEventBus _eventBus;
    private readonly IDialogPresenter _dialogPresenter;
    private readonly ILogger<ChildViewModelRegistry> _logger;

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string>? VmInstantiated;

    public ChildViewModelRegistry(
        MainViewModelDependencies deps,
        IDialogPresenter dialogPresenter,
        ILogger<ChildViewModelRegistry> logger)
    {
        // Eager
        MenuVm = deps.MenuVm;
        LevelSelectVm = deps.LevelSelectVm;
        SettingsVm = deps.SettingsVm;
        HighScoresVm = deps.HighScoresVm;
        GameOverVm = deps.GameOverVm;
        HelpVm = deps.HelpVm;
        VictoryVm = deps.VictoryVm;
        BossRushVm = deps.BossRushVm;
        WhatsNewVm = deps.WhatsNewVm;
        PlayHubVm = deps.PlayHubVm;
        BottomTabVm = deps.BottomTabVm;

        // Lazy
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

        // Services
        _rewardedAdService = deps.RewardedAdService;
        _purchaseService = deps.PurchaseService;
        _analytics = deps.Analytics;
        _localization = deps.Localization;
        _eventBus = deps.EventBus;
        _dialogPresenter = dialogPresenter;
        _logger = logger;
    }

    /// <summary>
    /// Verdrahtet die Standard-Subscriptions: Navigation, FloatingText (via IFloatingTextEmitter),
    /// Celebration (via ICelebrationEmitter). Wird vom Compositor pro Eager-VM gerufen
    /// und intern in jeder Ensure-Methode pro Lazy-VM.
    ///
    /// <para>Logik ist in <see cref="ChildViewModelWiring.Wire"/> isoliert (testbar ohne DI-Aufbau).</para>
    /// </summary>
    public void WireCommon(INavigable vm)
        => ChildViewModelWiring.Wire(vm, request => NavigationRequested?.Invoke(request), _eventBus);

    public GameViewModel EnsureGame()
        => EnsureLazy(ref _gameVm, _gameVmLazy, nameof(GameVm));

    public ShopViewModel EnsureShop()
    {
        if (_shopVm is { } existing) return existing;
        var vm = _shopVmLazy.Value;
        WireCommon(vm);
        vm.PurchaseSucceeded += name =>
        {
            _eventBus.RaiseFloatingText(name, "success");
            _eventBus.RaiseCelebration();
        };
        vm.InsufficientFunds += () =>
        {
            var msg = _localization.GetString("ShopNotEnoughCoins") ?? "Not enough coins!";
            _eventBus.RaiseFloatingText(msg, "error");
        };
        vm.MessageRequested += (t, m) => _dialogPresenter.ShowAlert(t, m, "OK");
        vm.ConfirmationRequested += (t, m, a, c) => _dialogPresenter.ShowConfirmAsync(t, m, a, c);
        _shopVm = vm;
        VmInstantiated?.Invoke(nameof(ShopVm));
        return vm;
    }

    public AchievementsViewModel EnsureAchievements()
        => EnsureLazy(ref _achievementsVm, _achievementsVmLazy, nameof(AchievementsVm));

    public DailyChallengeViewModel EnsureDailyChallenge()
        => EnsureLazy(ref _dailyChallengeVm, _dailyChallengeVmLazy, nameof(DailyChallengeVm));

    public LuckySpinViewModel EnsureLuckySpin()
        => EnsureLazy(ref _luckySpinVm, _luckySpinVmLazy, nameof(LuckySpinVm));

    public WeeklyChallengeViewModel EnsureWeeklyChallenge()
        => EnsureLazy(ref _weeklyChallengeVm, _weeklyChallengeVmLazy, nameof(WeeklyChallengeVm));

    public StatisticsViewModel EnsureStatistics()
        => EnsureLazy(ref _statisticsVm, _statisticsVmLazy, nameof(StatisticsVm));

    public QuickPlayViewModel EnsureQuickPlay()
        => EnsureLazy(ref _quickPlayVm, _quickPlayVmLazy, nameof(QuickPlayVm));

    public DeckViewModel EnsureDeck()
        => EnsureLazy(ref _deckVm, _deckVmLazy, nameof(DeckVm));

    public DungeonViewModel EnsureDungeon()
    {
        if (_dungeonVm is { } existing) return existing;
        var vm = _dungeonVmLazy.Value;
        WireCommon(vm);
        // Rewarded Ad fuer Dungeon-Run (Cooldown-Tracker recorded den letzten Ad-Anzeige-Zeitpunkt).
        vm.AdRunRequested += async () =>
        {
            var result = await _rewardedAdService.ShowAdWithTelemetryAsync(_analytics, "dungeon_run");
            if (result)
            {
                RewardedAdCooldownTracker.RecordAdShown();
                vm.OnAdRunRewarded();
            }
        };
        // Dungeon Master Pass: IAP-Kauf (permanenter 2x DungeonCoin-Boost).
        vm.DungeonMasterPassRequested += async () =>
        {
            var success = await _purchaseService.PurchaseConsumableAsync("dungeon_master_pass");
            if (success)
                vm.OnDungeonMasterPassPurchased();
        };
        _dungeonVm = vm;
        VmInstantiated?.Invoke(nameof(DungeonVm));
        return vm;
    }

    public BattlePassViewModel EnsureBattlePass()
    {
        if (_battlePassVm is { } existing) return existing;
        var vm = _battlePassVmLazy.Value;
        WireCommon(vm);
        // Battle-Pass-Premium-Kauf inkl. Analytics-Funnel.
        vm.PremiumPurchaseRequested += async () =>
        {
            _analytics?.LogEvent(AnalyticsEvents.PurchaseFlowStart, new Dictionary<string, object>
            {
                [AnalyticsParams.Sku] = "battle_pass_premium",
            });
            var success = await _purchaseService.PurchaseConsumableAsync("battle_pass_premium");
            if (success)
            {
                _analytics?.LogEvent(AnalyticsEvents.PurchaseSuccess, new Dictionary<string, object>
                {
                    [AnalyticsParams.Sku] = "battle_pass_premium",
                });
                vm.OnPremiumPurchaseConfirmed();
            }
            else
            {
                _analytics?.LogEvent(AnalyticsEvents.PurchaseFail, new Dictionary<string, object>
                {
                    [AnalyticsParams.Sku] = "battle_pass_premium",
                });
            }
        };
        _battlePassVm = vm;
        VmInstantiated?.Invoke(nameof(BattlePassVm));
        return vm;
    }

    public CollectionViewModel EnsureCollection()
        => EnsureLazy(ref _collectionVm, _collectionVmLazy, nameof(CollectionVm));

    public LeagueViewModel EnsureLeague()
        => EnsureLazy(ref _leagueVm, _leagueVmLazy, nameof(LeagueVm));

    public ProfileViewModel EnsureProfile()
        => EnsureLazy(ref _profileVm, _profileVmLazy, nameof(ProfileVm));

    public GemShopViewModel EnsureGemShop()
    {
        if (_gemShopVm is { } existing) return existing;
        var vm = _gemShopVmLazy.Value;
        WireCommon(vm);
        vm.ConfirmationRequested += (t, m, a, c) => _dialogPresenter.ShowConfirmAsync(t, m, a, c);
        _gemShopVm = vm;
        VmInstantiated?.Invoke(nameof(GemShopVm));
        return vm;
    }

    /// <summary>
    /// Generischer Lazy-Resolver fuer Ensure-Methoden ohne Sub-Wirings.
    /// Idempotent, feuert <see cref="VmInstantiated"/> bei der ersten Aufloesung.
    /// </summary>
    private T EnsureLazy<T>(ref T? backing, Lazy<T> lazy, string propertyName)
        where T : class, INavigable
    {
        if (backing is { } existing) return existing;
        var vm = lazy.Value;
        WireCommon(vm);
        backing = vm;
        VmInstantiated?.Invoke(propertyName);
        return vm;
    }

    /// <summary>
    /// Routet Locale-Aenderungen an alle bereits instanziierten VMs. Nicht-instanziierte
    /// Lazy-VMs werden uebersprungen — sie holen die aktuelle Sprache beim ersten OnAppearing.
    /// </summary>
    public void RefreshAllLocalizedTexts()
    {
        InvokeLocalizable(MenuVm, () => MenuVm.OnAppearing());
        InvokeLocalizable(LevelSelectVm, () => LevelSelectVm.OnAppearing());
        InvokeLocalizable(SettingsVm, () => SettingsVm.OnAppearing());
        InvokeLocalizable(HighScoresVm, () => HighScoresVm.OnAppearing());
        InvokeLocalizable(BossRushVm, null);
        // HelpVm/GameOverVm/VictoryVm: keine OnAppearing-Hooks, XAML-only / SetParameters

        InvokeLocalizable(_shopVm, null);
        InvokeLocalizable(_quickPlayVm, null);
        InvokeLocalizable(_deckVm, null);
        InvokeLocalizable(_dungeonVm, null);
        InvokeLocalizable(_battlePassVm, null);
        InvokeLocalizable(_collectionVm, null);
        InvokeLocalizable(_leagueVm, null);
        InvokeLocalizable(_profileVm, null);
        InvokeLocalizable(_gemShopVm, null);
        InvokeLocalizable(_statisticsVm, null);
        InvokeLocalizable(_dailyChallengeVm, null);
        InvokeLocalizable(_weeklyChallengeVm, null);
        InvokeLocalizable(_achievementsVm, () => _achievementsVm?.OnAppearing());
        InvokeLocalizable(_luckySpinVm, () => _luckySpinVm?.OnAppearing());
    }

    /// <summary>
    /// Ruft <see cref="ILocalizable.UpdateLocalizedTexts"/> wenn implementiert,
    /// sonst optionalen Fallback. try/catch verhindert dass ein einzelner VM-Fehler
    /// die uebrigen Refresh-Aufrufe blockiert.
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
            _logger?.LogWarning(ex, "RefreshAllLocalizedTexts: {VmType} fehlgeschlagen", vm.GetType().Name);
        }
    }
}
