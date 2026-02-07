using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Represents a daily reward for a specific day in the 7-day cycle.
/// </summary>
public class DailyReward
{
    /// <summary>
    /// Day number in the cycle (1-7).
    /// </summary>
    [JsonPropertyName("day")]
    public int Day { get; set; }

    /// <summary>
    /// Money reward amount.
    /// </summary>
    [JsonPropertyName("money")]
    public decimal Money { get; set; }

    /// <summary>
    /// XP reward amount (0 for days without XP).
    /// </summary>
    [JsonPropertyName("xp")]
    public int Xp { get; set; }

    /// <summary>
    /// Goldschrauben-Belohnung (0 fuer Tage ohne Schrauben).
    /// </summary>
    [JsonPropertyName("goldenScrews")]
    public int GoldenScrews { get; set; }

    /// <summary>
    /// Optional bonus type for special rewards.
    /// </summary>
    [JsonPropertyName("bonusType")]
    public DailyBonusType BonusType { get; set; } = DailyBonusType.None;

    /// <summary>
    /// Whether this reward has been claimed.
    /// </summary>
    [JsonIgnore]
    public bool IsClaimed { get; set; }

    /// <summary>
    /// Whether this is today's reward.
    /// </summary>
    [JsonIgnore]
    public bool IsToday { get; set; }

    /// <summary>
    /// Whether this reward is available (today and not claimed).
    /// </summary>
    [JsonIgnore]
    public bool IsAvailable => IsToday && !IsClaimed;

    /// <summary>
    /// Gets the reward schedule for a 7-day cycle.
    /// </summary>
    public static List<DailyReward> GetRewardSchedule()
    {
        return
        [
            new() { Day = 1, Money = 500m, Xp = 0, GoldenScrews = 0, BonusType = DailyBonusType.None },
            new() { Day = 2, Money = 750m, Xp = 0, GoldenScrews = 2, BonusType = DailyBonusType.None },
            new() { Day = 3, Money = 1000m, Xp = 0, GoldenScrews = 0, BonusType = DailyBonusType.None },
            new() { Day = 4, Money = 1500m, Xp = 50, GoldenScrews = 3, BonusType = DailyBonusType.None },
            new() { Day = 5, Money = 2000m, Xp = 100, GoldenScrews = 0, BonusType = DailyBonusType.None },
            new() { Day = 6, Money = 3000m, Xp = 150, GoldenScrews = 5, BonusType = DailyBonusType.None },
            new() { Day = 7, Money = 5000m, Xp = 300, GoldenScrews = 10, BonusType = DailyBonusType.SpeedBoost }
        ];
    }
}

/// <summary>
/// Special bonus types for daily rewards.
/// </summary>
public enum DailyBonusType
{
    /// <summary>No special bonus.</summary>
    None,

    /// <summary>2x income speed boost for 1 hour.</summary>
    SpeedBoost,

    /// <summary>50% more XP for 1 hour.</summary>
    XpBoost,

    /// <summary>Instant free worker.</summary>
    FreeWorker
}
