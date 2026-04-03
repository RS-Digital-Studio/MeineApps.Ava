using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Lifetime-Statistiken und Tracking-Zaehler.
/// Extrahiert aus GameState (V4) fuer bessere Strukturierung.
/// </summary>
public sealed class StatisticsData
{
    [JsonPropertyName("totalMiniGamesPlayed")]
    public int TotalMiniGamesPlayed { get; set; }

    [JsonPropertyName("perfectRatings")]
    public int PerfectRatings { get; set; }

    [JsonPropertyName("perfectStreak")]
    public int PerfectStreak { get; set; }

    [JsonPropertyName("bestPerfectStreak")]
    public int BestPerfectStreak { get; set; }

    [JsonPropertyName("totalPlayTimeSeconds")]
    public long TotalPlayTimeSeconds { get; set; }

    [JsonPropertyName("totalOrdersCompleted")]
    public int TotalOrdersCompleted { get; set; }

    [JsonPropertyName("ordersCompletedToday")]
    public int OrdersCompletedToday { get; set; }

    [JsonPropertyName("ordersCompletedThisWeek")]
    public int OrdersCompletedThisWeek { get; set; }

    [JsonPropertyName("totalWorkersHired")]
    public int TotalWorkersHired { get; set; }

    [JsonPropertyName("totalWorkersFired")]
    public int TotalWorkersFired { get; set; }

    [JsonPropertyName("totalWorkersTrained")]
    public int TotalWorkersTrained { get; set; }

    [JsonPropertyName("totalItemsCrafted")]
    public int TotalItemsCrafted { get; set; }

    [JsonPropertyName("totalItemsAutoProduced")]
    public long TotalItemsAutoProduced { get; set; }

    [JsonPropertyName("totalMaterialOrdersCompleted")]
    public int TotalMaterialOrdersCompleted { get; set; }

    [JsonPropertyName("materialOrdersCompletedToday")]
    public int MaterialOrdersCompletedToday { get; set; }

    [JsonPropertyName("totalTournamentsPlayed")]
    public int TotalTournamentsPlayed { get; set; }

    [JsonPropertyName("totalTournamentsWon")]
    public int TotalTournamentsWon { get; set; }

    [JsonPropertyName("totalDeliveriesClaimed")]
    public int TotalDeliveriesClaimed { get; set; }
}
