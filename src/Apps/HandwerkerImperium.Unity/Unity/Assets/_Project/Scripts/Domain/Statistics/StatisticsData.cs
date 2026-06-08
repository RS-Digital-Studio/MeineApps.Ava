using System.Collections.Generic;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Orders;

namespace HandwerkerImperium.Domain.Statistics
{
    /// <summary>
    /// Lifetime-Statistiken und Tracking-Zähler. 1:1-Port aus dem Avalonia-Original
    /// (Models/StatisticsData.cs). MiniGameStats liegt separat (Schicht 11). Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class StatisticsData
    {
        [JsonProperty("totalMiniGamesPlayed")]
        public int TotalMiniGamesPlayed { get; set; }

        [JsonProperty("perfectRatings")]
        public int PerfectRatings { get; set; }

        [JsonProperty("perfectStreak")]
        public int PerfectStreak { get; set; }

        [JsonProperty("bestPerfectStreak")]
        public int BestPerfectStreak { get; set; }

        [JsonProperty("totalPlayTimeSeconds")]
        public long TotalPlayTimeSeconds { get; set; }

        [JsonProperty("totalOrdersCompleted")]
        public int TotalOrdersCompleted { get; set; }

        [JsonProperty("ordersCompletedToday")]
        public int OrdersCompletedToday { get; set; }

        [JsonProperty("ordersCompletedThisWeek")]
        public int OrdersCompletedThisWeek { get; set; }

        [JsonProperty("totalWorkersHired")]
        public int TotalWorkersHired { get; set; }

        [JsonProperty("totalWorkersFired")]
        public int TotalWorkersFired { get; set; }

        [JsonProperty("totalWorkersTrained")]
        public int TotalWorkersTrained { get; set; }

        [JsonProperty("totalItemsCrafted")]
        public int TotalItemsCrafted { get; set; }

        [JsonProperty("totalItemsAutoProduced")]
        public long TotalItemsAutoProduced { get; set; }

        [JsonProperty("totalMaterialOrdersCompleted")]
        public int TotalMaterialOrdersCompleted { get; set; }

        [JsonProperty("materialOrdersCompletedToday")]
        public int MaterialOrdersCompletedToday { get; set; }

        [JsonProperty("totalTournamentsPlayed")]
        public int TotalTournamentsPlayed { get; set; }

        [JsonProperty("totalTournamentsWon")]
        public int TotalTournamentsWon { get; set; }

        [JsonProperty("totalDeliveriesClaimed")]
        public int TotalDeliveriesClaimed { get; set; }

        /// <summary>Pro MiniGame-Typ Performance-Statistiken (Risk/Reward-Anzeige, Sliding-Window).</summary>
        [JsonProperty("miniGamePerformance")]
        public Dictionary<MiniGameType, MiniGameStats> MiniGamePerformance { get; set; } = new Dictionary<MiniGameType, MiniGameStats>();
    }
}
