using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Statistics
{
    /// <summary>
    /// Pro-MiniGame-Typ-Statistik (Plays/Perfects/Misses + Sliding-Window-Verlauf).
    /// 1:1-Port aus dem Avalonia-Original (Models/StatisticsData.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class MiniGameStats
    {
        /// <summary>Gesamtzahl Plays dieses Typs.</summary>
        [JsonProperty("totalPlays")]
        public int TotalPlays { get; set; }

        /// <summary>Anzahl 5-Sterne (Perfect-Rating) Plays.</summary>
        [JsonProperty("perfectRatings")]
        public int PerfectRatings { get; set; }

        /// <summary>Anzahl Misses (0 Sterne — Hard-Fail oder Risk-Strategy mit zu wenig Score).</summary>
        [JsonProperty("misses")]
        public int Misses { get; set; }

        /// <summary>
        /// Letzte bis zu 20 Plays (true = Erfolg = ≥4 Sterne). Liste statt Queue für stabilen JSON-Roundtrip.
        /// Reihenfolge: älteste zuerst, neuestes Element ist RollingResults[Count-1].
        /// </summary>
        [JsonProperty("rollingResults")]
        public List<bool> RollingResults { get; set; } = new List<bool>();

        /// <summary>Zeitpunkt des letzten Plays (UTC). Hilft bei Cleanup verwaister Einträge.</summary>
        [JsonProperty("lastPlayedAt")]
        public DateTime LastPlayedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Konstanter Sliding-Window-Cap.</summary>
        public const int RollingWindowSize = 20;
    }
}
