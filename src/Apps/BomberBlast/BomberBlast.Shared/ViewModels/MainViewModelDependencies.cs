using BomberBlast.Core;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// DI-Aggregat fuer <see cref="MainViewModel"/>-Dependencies (Audit M25).
///
/// <para>Buendelt die 9 Service- und 8 Eager-VM-Dependencies plus 15 Lazy-VM-Wrapper, damit
/// der MainViewModel-Konstruktor von 32 Parametern auf einen einzigen reduziert wird.
/// Die DI-Registrierung erzeugt diese Klasse via Auto-Construction (alle Properties sind
/// Constructor-Parameter, kein Setter).</para>
///
/// <para>Vorteile gegenueber dem alten 32-Parameter-Konstruktor:</para>
/// <list type="bullet">
///   <item>Auto-Refactoring: neuer Dependency = neue Property hier, MainViewModel-Ctor unveraendert.</item>
///   <item>Test-Setup leichter: ein Mock-Aggregat statt 32 Mock-Parameter.</item>
///   <item>Compile-Time-Sicherheit bleibt (kein Service-Locator-Pattern wie sp.GetService).</item>
/// </list>
///
/// <para>Code-Smell-Hinweis aus dem Audit: 31 Deps sind ein Code-Smell, der hier durch
/// Buendelung kaschiert wird statt eliminiert. Strukturelle Aufloesung (z. B. Feature-Module
/// statt Mega-MainViewModel) waere ein eigener Sprint.</para>
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

    // Services
    ILocalizationService Localization,
    IAdService AdService,
    IPurchaseService PurchaseService,
    IRewardedAdService RewardedAdService,
    IAchievementService AchievementService,
    ICoinService CoinService,
    ICloudSaveService CloudSaveService,
    SoundManager SoundManager,
    IAppLogger Logger,
    IGameEventBus EventBus,
    IWhatsNewService WhatsNewService);
