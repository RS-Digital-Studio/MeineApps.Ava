using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Represents a daily reward for a specific day in the 30-day cycle.
/// </summary>
public class DailyReward
{
    /// <summary>
    /// Day number in the cycle (1-30).
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
    /// Gets the reward schedule for a 30-day cycle.
    /// Woche 1: Grundbelohnungen. Woche 2-3: Steigerung. Woche 4: Jackpot-Finale.
    /// </summary>
    public static List<DailyReward> GetRewardSchedule()
    {
        return
        [
            // Woche 1: Einstieg
            new() { Day = 1, Money = 500m, Xp = 0, GoldenScrews = 0 },
            new() { Day = 2, Money = 750m, Xp = 0, GoldenScrews = 1 },
            new() { Day = 3, Money = 1_000m, Xp = 25, GoldenScrews = 0 },
            new() { Day = 4, Money = 1_500m, Xp = 0, GoldenScrews = 2 },
            new() { Day = 5, Money = 2_000m, Xp = 50, GoldenScrews = 0 },
            new() { Day = 6, Money = 2_500m, Xp = 0, GoldenScrews = 3 },
            new() { Day = 7, Money = 5_000m, Xp = 100, GoldenScrews = 5, BonusType = DailyBonusType.SpeedBoost },
            // Woche 2: Aufbau
            new() { Day = 8, Money = 3_000m, Xp = 50, GoldenScrews = 0 },
            new() { Day = 9, Money = 3_500m, Xp = 0, GoldenScrews = 3 },
            new() { Day = 10, Money = 4_000m, Xp = 75, GoldenScrews = 0 },
            new() { Day = 11, Money = 5_000m, Xp = 0, GoldenScrews = 4 },
            new() { Day = 12, Money = 6_000m, Xp = 100, GoldenScrews = 0 },
            new() { Day = 13, Money = 7_000m, Xp = 0, GoldenScrews = 5 },
            new() { Day = 14, Money = 10_000m, Xp = 200, GoldenScrews = 8, BonusType = DailyBonusType.XpBoost },
            // Woche 3: Steigerung
            new() { Day = 15, Money = 8_000m, Xp = 100, GoldenScrews = 0 },
            new() { Day = 16, Money = 9_000m, Xp = 0, GoldenScrews = 5 },
            new() { Day = 17, Money = 10_000m, Xp = 150, GoldenScrews = 0 },
            new() { Day = 18, Money = 12_000m, Xp = 0, GoldenScrews = 6 },
            new() { Day = 19, Money = 15_000m, Xp = 200, GoldenScrews = 0 },
            new() { Day = 20, Money = 18_000m, Xp = 0, GoldenScrews = 8 },
            new() { Day = 21, Money = 25_000m, Xp = 300, GoldenScrews = 10, BonusType = DailyBonusType.SpeedBoost },
            // Woche 4: Jackpot-Finale
            new() { Day = 22, Money = 15_000m, Xp = 150, GoldenScrews = 0 },
            new() { Day = 23, Money = 18_000m, Xp = 0, GoldenScrews = 8 },
            new() { Day = 24, Money = 20_000m, Xp = 200, GoldenScrews = 0 },
            new() { Day = 25, Money = 25_000m, Xp = 0, GoldenScrews = 10 },
            new() { Day = 26, Money = 30_000m, Xp = 300, GoldenScrews = 0 },
            new() { Day = 27, Money = 35_000m, Xp = 0, GoldenScrews = 12 },
            new() { Day = 28, Money = 40_000m, Xp = 400, GoldenScrews = 15, BonusType = DailyBonusType.XpBoost },
            new() { Day = 29, Money = 50_000m, Xp = 500, GoldenScrews = 15 },
            new() { Day = 30, Money = 100_000m, Xp = 1_000, GoldenScrews = 25, BonusType = DailyBonusType.SpeedBoost },
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
