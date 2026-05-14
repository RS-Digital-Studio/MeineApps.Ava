using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// DI-Transport-Aggregat fuer die 11 Eager-VMs, 15 Lazy-VM-Wrapper und die wenigen
/// Cross-Cutting-Services, die der <see cref="MainViewModel"/>-Compositor und der
/// <see cref="ChildViewModelRegistry"/> gemeinsam brauchen.
///
/// <para>
/// Die DI-Registrierung erzeugt das Record via Auto-Construction (alle Properties sind
/// Constructor-Parameter). Beide Consumer (Compositor + Registry) sind Singletons —
/// das Aggregat wird einmal aufgeloest und geteilt.
/// </para>
///
/// <para>
/// Die fruehere God-VM-Logik liegt jetzt in den Feature-Modulen (NavigationCoordinator,
/// BottomTabController, DialogPresenter, ChildViewModelRegistry, LifecycleHub) — diese
/// holen ihre Service-Dependencies direkt aus dem Container, nicht aus diesem Aggregat.
/// </para>
/// </summary>
public sealed record MainViewModelDependencies(
    // Eager VMs (sofort gebraucht)
    MainMenuViewModel MenuVm,
    LevelSelectViewModel LevelSelectVm,
    SettingsViewModel SettingsVm,
    HighScoresViewModel HighScoresVm,
    GameOverViewModel GameOverVm,
    HelpViewModel HelpVm,
    VictoryViewModel VictoryVm,
    BossRushViewModel BossRushVm,
    WhatsNewViewModel WhatsNewVm,
    PlayHubViewModel PlayHubVm,
    BottomTabBarViewModel BottomTabVm,

    // Lazy VMs (erst bei progressivem Unlock gebraucht)
    Lazy<GameViewModel> GameVmLazy,
    Lazy<ShopViewModel> ShopVmLazy,
    Lazy<AchievementsViewModel> AchievementsVmLazy,
    Lazy<DailyChallengeViewModel> DailyChallengeVmLazy,
    Lazy<LuckySpinViewModel> LuckySpinVmLazy,
    Lazy<WeeklyChallengeViewModel> WeeklyChallengeVmLazy,
    Lazy<StatisticsViewModel> StatisticsVmLazy,
    Lazy<QuickPlayViewModel> QuickPlayVmLazy,
    Lazy<DeckViewModel> DeckVmLazy,
    Lazy<DungeonViewModel> DungeonVmLazy,
    Lazy<BattlePassViewModel> BattlePassVmLazy,
    Lazy<CollectionViewModel> CollectionVmLazy,
    Lazy<LeagueViewModel> LeagueVmLazy,
    Lazy<ProfileViewModel> ProfileVmLazy,
    Lazy<GemShopViewModel> GemShopVmLazy,

    // Services — nur was Compositor + Registry direkt brauchen.
    ILocalizationService Localization,
    IAdService AdService,
    IPurchaseService PurchaseService,
    IRewardedAdService RewardedAdService,
    IAchievementService AchievementService,
    IGameEventBus EventBus,
    IWhatsNewService WhatsNewService,
    IAnalyticsService Analytics,
    IDialogPresenter DialogPresenter);
