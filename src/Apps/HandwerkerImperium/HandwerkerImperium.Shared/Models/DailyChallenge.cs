using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Typ einer taeglichen Herausforderung.
/// </summary>
public enum DailyChallengeType
{
    CompleteOrders,
    EarnMoney,
    UpgradeWorkshop,
    HireWorker,
    CompleteQuickJob,
    PlayMiniGames,
    AchieveMinigameScore
}

/// <summary>
/// Eine einzelne taegliche Herausforderung mit Fortschritt und Belohnung.
/// </summary>
public class DailyChallenge : IProgressProvider
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    public DailyChallengeType Type { get; set; }

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

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }

    [JsonPropertyName("isClaimed")]
    public bool IsClaimed { get; set; }

    /// <summary>
    /// Ob der Spieler bereits per Rewarded Ad einen Retry genutzt hat (max 1x pro Challenge).
    /// </summary>
    [JsonPropertyName("hasRetriedWithAd")]
    public bool HasRetriedWithAd { get; set; }

    /// <summary>
    /// Ob ein Retry per Video-Ad moeglich ist: Nicht geschafft, noch nicht genutzt, Fortschritt > 0.
    /// </summary>
    [JsonIgnore]
    public bool CanRetryWithAd => !IsCompleted && !HasRetriedWithAd && CurrentValue > 0;

    [JsonIgnore]
    public double Progress => TargetValue > 0 ? Math.Clamp((double)CurrentValue / TargetValue, 0, 1) : 0;

    [JsonIgnore]
    public string ProgressText => $"{CurrentValue}/{TargetValue}";

    [JsonIgnore]
    public string DisplayDescription { get; set; } = string.Empty;

    [JsonIgnore]
    public string RewardDisplay { get; set; } = string.Empty;

    [JsonIgnore]
    public bool HasGoldenScrewReward => GoldenScrewReward > 0;
}

/// <summary>
/// Zustand aller taeglichen Herausforderungen (gespeichert im GameState).
/// </summary>
public class DailyChallengeState
{
    [JsonPropertyName("challenges")]
    public List<DailyChallenge> Challenges { get; set; } = [];

    [JsonPropertyName("lastResetDate")]
    public DateTime LastResetDate { get; set; } = DateTime.MinValue;

    [JsonPropertyName("allCompletedBonusClaimed")]
    public bool AllCompletedBonusClaimed { get; set; }
}
