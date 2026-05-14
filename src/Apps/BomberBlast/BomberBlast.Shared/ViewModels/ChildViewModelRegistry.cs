namespace BomberBlast.ViewModels;

/// <summary>
/// Default-Implementation von <see cref="IChildViewModelRegistry"/> (Welle 6 MainViewModel-Refactor).
///
/// <para>
/// Phase 1: Leeres Geruest. Die 22 VM-Felder + EnsureXxxVm-Methoden werden in Phase 3 aus
/// <see cref="MainViewModel"/> hier hin verschoben.
/// </para>
/// </summary>
public sealed class ChildViewModelRegistry : IChildViewModelRegistry
{
    public event Action<string>? VmInstantiated;

    // Phase 1: Alle Properties werfen NotImplementedException — werden in Phase 3 mit Backing-Fields versehen.
    public MainMenuViewModel MenuVm => throw new NotImplementedException("Phase 3.");
    public LevelSelectViewModel LevelSelectVm => throw new NotImplementedException("Phase 3.");
    public SettingsViewModel SettingsVm => throw new NotImplementedException("Phase 3.");
    public HighScoresViewModel HighScoresVm => throw new NotImplementedException("Phase 3.");
    public GameOverViewModel GameOverVm => throw new NotImplementedException("Phase 3.");
    public HelpViewModel HelpVm => throw new NotImplementedException("Phase 3.");
    public VictoryViewModel VictoryVm => throw new NotImplementedException("Phase 3.");
    public BossRushViewModel BossRushVm => throw new NotImplementedException("Phase 3.");
    public WhatsNewViewModel WhatsNewVm => throw new NotImplementedException("Phase 3.");
    public PlayHubViewModel PlayHubVm => throw new NotImplementedException("Phase 3.");
    public BottomTabBarViewModel BottomTabVm => throw new NotImplementedException("Phase 3.");

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

    public GameViewModel EnsureGame() => throw new NotImplementedException("Phase 3.");
    public ShopViewModel EnsureShop() => throw new NotImplementedException("Phase 3.");
    public AchievementsViewModel EnsureAchievements() => throw new NotImplementedException("Phase 3.");
    public DailyChallengeViewModel EnsureDailyChallenge() => throw new NotImplementedException("Phase 3.");
    public LuckySpinViewModel EnsureLuckySpin() => throw new NotImplementedException("Phase 3.");
    public WeeklyChallengeViewModel EnsureWeeklyChallenge() => throw new NotImplementedException("Phase 3.");
    public StatisticsViewModel EnsureStatistics() => throw new NotImplementedException("Phase 3.");
    public QuickPlayViewModel EnsureQuickPlay() => throw new NotImplementedException("Phase 3.");
    public DeckViewModel EnsureDeck() => throw new NotImplementedException("Phase 3.");
    public DungeonViewModel EnsureDungeon() => throw new NotImplementedException("Phase 3.");
    public BattlePassViewModel EnsureBattlePass() => throw new NotImplementedException("Phase 3.");
    public CollectionViewModel EnsureCollection() => throw new NotImplementedException("Phase 3.");
    public LeagueViewModel EnsureLeague() => throw new NotImplementedException("Phase 3.");
    public ProfileViewModel EnsureProfile() => throw new NotImplementedException("Phase 3.");
    public GemShopViewModel EnsureGemShop() => throw new NotImplementedException("Phase 3.");

    public void RefreshAllLocalizedTexts() => throw new NotImplementedException("Phase 3.");
    public void WireCommon(INavigable vm) => throw new NotImplementedException("Phase 3.");

    /// <summary>Helper-Schutz damit Event-Subscriber waehrend der Migration nicht crashen.</summary>
    internal void RaiseVmInstantiated(string propertyName) => VmInstantiated?.Invoke(propertyName);
}
