namespace BomberBlast.ViewModels;

/// <summary>
/// Zentrale Verwaltung aller Child-ViewModels von <see cref="MainViewModel"/>
/// (10 Eager + 15 Lazy). Bietet idempotente <c>EnsureXxxVm()</c>-Methoden und routet
/// Sprache-Aenderungen + Common-Wirings.
///
/// <para>
/// MainViewModel haelt keine eigenen VM-Felder mehr — Property-Getter sind Forwarder
/// auf die Registry. Lazy-Properties werden beim ersten <c>EnsureXxxVm()</c> instanziiert
/// und ueber <see cref="VmInstantiated"/> propagiert (MainView-Bindings reagieren).
/// </para>
/// </summary>
public interface IChildViewModelRegistry
{
    // Eager VMs
    MainMenuViewModel MenuVm { get; }
    LevelSelectViewModel LevelSelectVm { get; }
    SettingsViewModel SettingsVm { get; }
    HighScoresViewModel HighScoresVm { get; }
    GameOverViewModel GameOverVm { get; }
    HelpViewModel HelpVm { get; }
    VictoryViewModel VictoryVm { get; }
    BossRushViewModel BossRushVm { get; }
    WhatsNewViewModel WhatsNewVm { get; }
    PlayHubViewModel PlayHubVm { get; }
    BottomTabBarViewModel BottomTabVm { get; }

    // Lazy VMs (nullable bis EnsureXxx aufgerufen wird)
    GameViewModel? GameVm { get; }
    ShopViewModel? ShopVm { get; }
    AchievementsViewModel? AchievementsVm { get; }
    DailyChallengeViewModel? DailyChallengeVm { get; }
    LuckySpinViewModel? LuckySpinVm { get; }
    WeeklyChallengeViewModel? WeeklyChallengeVm { get; }
    StatisticsViewModel? StatisticsVm { get; }
    QuickPlayViewModel? QuickPlayVm { get; }
    DeckViewModel? DeckVm { get; }
    DungeonViewModel? DungeonVm { get; }
    BattlePassViewModel? BattlePassVm { get; }
    CollectionViewModel? CollectionVm { get; }
    LeagueViewModel? LeagueVm { get; }
    ProfileViewModel? ProfileVm { get; }
    GemShopViewModel? GemShopVm { get; }

    // Ensure*-Methoden (idempotent, Lazy<T>-Init)
    GameViewModel EnsureGame();
    ShopViewModel EnsureShop();
    AchievementsViewModel EnsureAchievements();
    DailyChallengeViewModel EnsureDailyChallenge();
    LuckySpinViewModel EnsureLuckySpin();
    WeeklyChallengeViewModel EnsureWeeklyChallenge();
    StatisticsViewModel EnsureStatistics();
    QuickPlayViewModel EnsureQuickPlay();
    DeckViewModel EnsureDeck();
    DungeonViewModel EnsureDungeon();
    BattlePassViewModel EnsureBattlePass();
    CollectionViewModel EnsureCollection();
    LeagueViewModel EnsureLeague();
    ProfileViewModel EnsureProfile();
    GemShopViewModel EnsureGemShop();

    /// <summary>
    /// Wird gefeuert wenn ein Lazy-VM gerade instanziiert wurde. Der String ist der Property-Name
    /// auf <see cref="MainViewModel"/> (z.B. "GameVm") — MainViewModel ruft daraufhin
    /// <c>OnPropertyChanged(name)</c>, damit AXAML-Bindings den neuen Wert sehen.
    /// </summary>
    event Action<string>? VmInstantiated;

    /// <summary>
    /// Routet die Locale-Aenderung an alle bereits instanziierten VMs (Eager + Lazy).
    /// Wird vom <see cref="MeineApps.Core.Ava.Localization.ILocalizationService.LanguageChanged"/> gefeuert.
    /// </summary>
    void RefreshAllLocalizedTexts();

    /// <summary>Verdrahtet die Common-Subscriptions (NavigationRequested, ConfirmationRequested, FloatingText, Celebration).</summary>
    void WireCommon(INavigable vm);
}
