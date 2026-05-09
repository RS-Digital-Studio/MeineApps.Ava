using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

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

    /// <summary>
    /// Pro MiniGame-Typ Performance-Statistiken (v2.0.36 Risk/Reward-Anzeige).
    /// Sliding-Window-Daten dienen als Basis fuer die Erfolgsquote der drei Strategie-Buttons.
    /// </summary>
    [JsonPropertyName("miniGamePerformance")]
    public Dictionary<MiniGameType, MiniGameStats> MiniGamePerformance { get; set; } = new();
}

/// <summary>
/// Performance-Tracker pro MiniGame-Typ (v2.0.36).
/// Sliding-Window auf den letzten 20 Plays ergibt eine personalisierte Erfolgsquote.
/// "Erfolg" = mindestens 4 Sterne (also nicht-Miss + nicht-Standard).
/// </summary>
public sealed class MiniGameStats
{
    /// <summary>Gesamtzahl Plays dieses Typs.</summary>
    [JsonPropertyName("totalPlays")]
    public int TotalPlays { get; set; }

    /// <summary>Anzahl 5-Sterne (Perfect-Rating) Plays.</summary>
    [JsonPropertyName("perfectRatings")]
    public int PerfectRatings { get; set; }

    /// <summary>Anzahl Misses (0 Sterne — Hard-Fail oder Risk-Strategy mit zu wenig Score).</summary>
    [JsonPropertyName("misses")]
    public int Misses { get; set; }

    /// <summary>
    /// Letzte bis zu 20 Plays (true = Erfolg = ≥4 Sterne).
    /// Liste statt Queue, damit JSON-Roundtrip stabil ist.
    /// Reihenfolge: aelteste zuerst, neuestes Element ist <c>RollingResults[Count-1]</c>.
    /// </summary>
    [JsonPropertyName("rollingResults")]
    public List<bool> RollingResults { get; set; } = [];

    /// <summary>Zeitpunkt des letzten Plays (UTC). Hilft bei Cleanup verwaister Eintraege.</summary>
    [JsonPropertyName("lastPlayedAt")]
    public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Konstanter Sliding-Window-Cap.</summary>
    public const int RollingWindowSize = 20;
}
