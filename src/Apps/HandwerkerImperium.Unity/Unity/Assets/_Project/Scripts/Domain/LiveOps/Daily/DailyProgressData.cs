#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Orders;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Täglicher Fortschritt (Daily Rewards, Quick Jobs, Welcome Back, Weekly Missions).
    /// 1:1-Port aus dem Avalonia-Original (Models/DailyProgressData.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class DailyProgressData
    {
        // ── Daily Rewards ──
        [JsonProperty("lastDailyRewardClaim")]
        public DateTime LastDailyRewardClaim { get; set; } = DateTime.MinValue;

        [JsonProperty("dailyRewardStreak")]
        public int DailyRewardStreak { get; set; }

        /// <summary>Streak-Wert vor dem letzten Unterbruch (für Streak-Rettung).</summary>
        [JsonProperty("streakBeforeBreak")]
        public int StreakBeforeBreak { get; set; }

        /// <summary>Ob die Streak-Rettung bereits verwendet wurde (nur 1x pro Unterbrechung).</summary>
        [JsonProperty("streakRescueUsed")]
        public bool StreakRescueUsed { get; set; }

        // ── Quick Jobs ──
        [JsonProperty("quickJobs")]
        public List<QuickJob> QuickJobs { get; set; } = new List<QuickJob>();

        [JsonProperty("lastQuickJobRotation")]
        public DateTime LastQuickJobRotation { get; set; } = DateTime.MinValue;

        [JsonProperty("totalQuickJobsCompleted")]
        public int TotalQuickJobsCompleted { get; set; }

        [JsonProperty("quickJobsCompletedToday")]
        public int QuickJobsCompletedToday { get; set; }

        [JsonProperty("lastQuickJobDailyReset")]
        public DateTime LastQuickJobDailyReset { get; set; } = DateTime.MinValue;

        // ── Welcome Back ──
        [JsonProperty("activeWelcomeBackOffer")]
        public WelcomeBackOffer? ActiveWelcomeBackOffer { get; set; }

        [JsonProperty("claimedStarterPack")]
        public bool ClaimedStarterPack { get; set; }

        // ── Weekly Missions ──
        [JsonProperty("weeklyMissionState")]
        public WeeklyMissionState WeeklyMissionState { get; set; } = new WeeklyMissionState();
    }
}
