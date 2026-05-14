namespace BomberBlast.ViewModels;

/// <summary>
/// Default-Implementation von <see cref="IChildViewModelRegistry"/> (Welle 6 MainViewModel-Refactor).
///
/// <para>
/// : Leeres Geruest. Die 22 VM-Felder + EnsureXxxVm-Methoden werden in aus
/// <see cref="MainViewModel"/> hier hin verschoben.
/// </para>
/// </summary>
public sealed class ChildViewModelRegistry : IChildViewModelRegistry
{
    public event Action<string>? VmInstantiated;

    // : Alle Properties werfen NotImplementedException — werden in mit Backing-Fields versehen.
    public MainMenuViewModel MenuVm => throw new NotImplementedException(".");
    public LevelSelectViewModel LevelSelectVm => throw new NotImplementedException(".");
    public SettingsViewModel SettingsVm => throw new NotImplementedException(".");
    public HighScoresViewModel HighScoresVm => throw new NotImplementedException(".");
    public GameOverViewModel GameOverVm => throw new NotImplementedException(".");
    public HelpViewModel HelpVm => throw new NotImplementedException(".");
    public VictoryViewModel VictoryVm => throw new NotImplementedException(".");
    public BossRushViewModel BossRushVm => throw new NotImplementedException(".");
    public WhatsNewViewModel WhatsNewVm => throw new NotImplementedException(".");
    public PlayHubViewModel PlayHubVm => throw new NotImplementedException(".");
    public BottomTabBarViewModel BottomTabVm => throw new NotImplementedException(".");

    public GameViewModel? GameVm => null;
    public ShopViewModel? ShopVm => null;
    public AchievementsViewModel? AchievementsVm => null;
    public DailyChallengeViewModel? DailyChallengeVm => null;
    public LuckySpinViewModel? LuckySpinVm => null;
    public WeeklyChallengeViewModel? WeeklyChallengeVm => null;
    public StatisticsViewModel? StatisticsVm => null;
    public QuickPlayViewModel? QuickPlayVm => null;
    public DeckViewModel? DeckVm => null;
    public DungeonViewModel? DungeonVm => null;
    public BattlePassViewModel? BattlePassVm => null;
    public CollectionViewModel? CollectionVm => null;
    public LeagueViewModel? LeagueVm => null;
    public ProfileViewModel? ProfileVm => null;
    public GemShopViewModel? GemShopVm => null;

    public GameViewModel EnsureGame() => throw new NotImplementedException(".");
    public ShopViewModel EnsureShop() => throw new NotImplementedException(".");
    public AchievementsViewModel EnsureAchievements() => throw new NotImplementedException(".");
    public DailyChallengeViewModel EnsureDailyChallenge() => throw new NotImplementedException(".");
    public LuckySpinViewModel EnsureLuckySpin() => throw new NotImplementedException(".");
    public WeeklyChallengeViewModel EnsureWeeklyChallenge() => throw new NotImplementedException(".");
    public StatisticsViewModel EnsureStatistics() => throw new NotImplementedException(".");
    public QuickPlayViewModel EnsureQuickPlay() => throw new NotImplementedException(".");
    public DeckViewModel EnsureDeck() => throw new NotImplementedException(".");
    public DungeonViewModel EnsureDungeon() => throw new NotImplementedException(".");
    public BattlePassViewModel EnsureBattlePass() => throw new NotImplementedException(".");
    public CollectionViewModel EnsureCollection() => throw new NotImplementedException(".");
    public LeagueViewModel EnsureLeague() => throw new NotImplementedException(".");
    public ProfileViewModel EnsureProfile() => throw new NotImplementedException(".");
    public GemShopViewModel EnsureGemShop() => throw new NotImplementedException(".");

    public void RefreshAllLocalizedTexts() => throw new NotImplementedException(".");
    public void WireCommon(INavigable vm) => throw new NotImplementedException(".");

    /// <summary>Helper-Schutz damit Event-Subscriber waehrend der Migration nicht crashen.</summary>
    internal void RaiseVmInstantiated(string propertyName) => VmInstantiated?.Invoke(propertyName);
}
