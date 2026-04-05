using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Täglicher Fortschritt (Daily Rewards, Quick Jobs, Welcome Back, Weekly Missions).
/// Extrahiert aus GameState (V5) für bessere Strukturierung.
/// </summary>
public sealed class DailyProgressData
{
    // ═══════════════════════════════════════════════════════════════════════
    // DAILY REWARDS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("lastDailyRewardClaim")]
    public DateTime LastDailyRewardClaim { get; set; } = DateTime.MinValue;

    [JsonPropertyName("dailyRewardStreak")]
    public int DailyRewardStreak { get; set; }

    /// <summary>
    /// Streak-Wert vor dem letzten Unterbruch (für Streak-Rettung).
    /// </summary>
    [JsonPropertyName("streakBeforeBreak")]
    public int StreakBeforeBreak { get; set; }

    /// <summary>
    /// Ob die Streak-Rettung bereits verwendet wurde (nur 1x pro Unterbrechung).
    /// </summary>
    [JsonPropertyName("streakRescueUsed")]
    public bool StreakRescueUsed { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // QUICK JOBS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("quickJobs")]
    public List<QuickJob> QuickJobs { get; set; } = [];

    [JsonPropertyName("lastQuickJobRotation")]
    public DateTime LastQuickJobRotation { get; set; } = DateTime.MinValue;

    [JsonPropertyName("totalQuickJobsCompleted")]
    public int TotalQuickJobsCompleted { get; set; }

    [JsonPropertyName("quickJobsCompletedToday")]
    public int QuickJobsCompletedToday { get; set; }

    [JsonPropertyName("lastQuickJobDailyReset")]
    public DateTime LastQuickJobDailyReset { get; set; } = DateTime.MinValue;

    // ═══════════════════════════════════════════════════════════════════════
    // WELCOME BACK
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("activeWelcomeBackOffer")]
    public WelcomeBackOffer? ActiveWelcomeBackOffer { get; set; }

    [JsonPropertyName("claimedStarterPack")]
    public bool ClaimedStarterPack { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // WEEKLY MISSIONS
    // ═══════════════════════════════════════════════════════════════════════

    [JsonPropertyName("weeklyMissionState")]
    public WeeklyMissionState WeeklyMissionState { get; set; } = new();
}
