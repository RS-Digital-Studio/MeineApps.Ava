using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Typ einer wöchentlichen Mission.
/// </summary>
public enum WeeklyMissionType
{
    CompleteOrders,
    EarnMoney,
    UpgradeWorkshops,
    HireWorkers,
    PlayMiniGames,
    CompleteDailyChallenges,
    AchievePerfectRatings,

    /// <summary>Mehrere Arbeiter trainieren innerhalb einer Woche.</summary>
    TrainWorkers = 7,

    /// <summary>Viele Crafting-Aufträge in einer Woche abschließen.</summary>
    CompleteCraftings = 8,

    /// <summary>Lange Serie fehlerfreier Aufträge erreichen.</summary>
    AchievePerfectStreak = 9,

    /// <summary>Werkstätten auf bestimmtes Level bringen.</summary>
    ReachWorkshopLevels = 10,

    /// <summary>Viele Items durch Auto-Produktion herstellen.</summary>
    ProduceItems = 11
}

/// <summary>
/// Eine einzelne wöchentliche Mission.
/// </summary>
public class WeeklyMission : IProgressProvider
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public WeeklyMissionType Type { get; set; }

    [JsonPropertyName("targetValue")]
    public long TargetValue { get; set; }

    [JsonPropertyName("currentValue")]
    public long CurrentValue { get; set; }

    [JsonPropertyName("moneyReward")]
    public decimal MoneyReward { get; set; }

    [JsonPropertyName("xpReward")]
    public int XpReward { get; set; }

    [JsonPropertyName("goldenScrewReward")]
    public int GoldenScrewReward { get; set; }

    [JsonPropertyName("isClaimed")]
    public bool IsClaimed { get; set; }

    [JsonIgnore]
    public bool IsCompleted => CurrentValue >= TargetValue;

    [JsonIgnore]
    public double Progress => TargetValue > 0 ? Math.Clamp((double)CurrentValue / TargetValue, 0.0, 1.0) : 0.0;

    // Anzeige-Felder (werden von Service befüllt)
    [JsonIgnore]
    public string DisplayDescription { get; set; } = "";

    [JsonIgnore]
    public string RewardDisplay { get; set; } = "";

    [JsonIgnore]
    public string ProgressDisplay { get; set; } = "";

    [JsonIgnore]
    public bool HasGoldenScrewReward => GoldenScrewReward > 0;
}

/// <summary>
/// Zustand aller wöchentlichen Missionen.
/// </summary>
public class WeeklyMissionState
{
    [JsonPropertyName("missions")]
    public List<WeeklyMission> Missions { get; set; } = [];

    /// <summary>
    /// Letzter Montag-Reset (UTC).
    /// </summary>
    [JsonPropertyName("lastWeeklyReset")]
    public DateTime LastWeeklyReset { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Bonus wenn alle 5 Missionen abgeschlossen (50 Goldschrauben).
    /// </summary>
    [JsonPropertyName("allCompletedBonusClaimed")]
    public bool AllCompletedBonusClaimed { get; set; }
}
