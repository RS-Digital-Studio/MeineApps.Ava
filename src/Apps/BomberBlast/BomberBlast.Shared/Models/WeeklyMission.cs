namespace BomberBlast.Models;

/// <summary>
/// Typ einer wöchentlichen Mission
/// </summary>
public enum WeeklyMissionType
{
    CompleteLevels,
    DefeatEnemies,
    CollectPowerUps,
    EarnCoins,
    SurvivalKills,
    UseSpecialBombs,
    AchieveCombo,
    WinBossFights,

    // Phase 9.4: Neue Missionstypen für Feature-Expansion
    CompleteDungeonFloors,
    CollectCards,
    EarnGems,
    PlayQuickPlay,
    SpinLuckyWheel,
    UpgradeCards
}

/// <summary>
/// Eine einzelne wöchentliche Mission mit Fortschritts-Tracking
/// </summary>
public class WeeklyMission
{
    public WeeklyMissionType Type { get; set; }
    public string NameKey { get; set; } = "";
    public string DescriptionKey { get; set; } = "";
    public int TargetCount { get; set; }
    public int CurrentCount { get; set; }
    public int CoinReward { get; set; }
    public bool IsCompleted => CurrentCount >= TargetCount;

    /// <summary>Fortschritt als Prozent (0.0 - 1.0)</summary>
    public float Progress => TargetCount > 0 ? Math.Min(1f, (float)CurrentCount / TargetCount) : 0f;
}
