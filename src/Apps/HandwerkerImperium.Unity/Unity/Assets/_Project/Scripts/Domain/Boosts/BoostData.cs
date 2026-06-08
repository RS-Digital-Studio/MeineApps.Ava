using System;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Boosts
{
    /// <summary>
    /// Boost-Daten (Speed, XP, Rush, Soft-Cap). 1:1-Port aus dem Avalonia-Original (Models/BoostData.cs).
    /// Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class BoostData
    {
        [JsonProperty("speedBoostEndTime")]
        public DateTime SpeedBoostEndTime { get; set; } = DateTime.MinValue;

        [JsonProperty("xpBoostEndTime")]
        public DateTime XpBoostEndTime { get; set; } = DateTime.MinValue;

        /// <summary>Feierabend-Rush: 2h 2x-Boost, einmal täglich gratis.</summary>
        [JsonProperty("rushBoostEndTime")]
        public DateTime RushBoostEndTime { get; set; } = DateTime.MinValue;

        /// <summary>Letztes Datum an dem der gratis Rush verwendet wurde.</summary>
        [JsonProperty("lastFreeRushUsed")]
        public DateTime LastFreeRushUsed { get; set; } = DateTime.MinValue;

        [JsonIgnore]
        public bool IsSpeedBoostActive => SpeedBoostEndTime > DateTime.UtcNow;

        [JsonIgnore]
        public bool IsXpBoostActive => XpBoostEndTime > DateTime.UtcNow;

        [JsonIgnore]
        public bool IsRushBoostActive => RushBoostEndTime > DateTime.UtcNow;

        /// <summary>Ob der Soft-Cap auf den Einkommens-Multiplikator aktiv ist (> 10x). Pro Tick gesetzt.</summary>
        [JsonIgnore]
        public bool IsSoftCapActive { get; set; }

        /// <summary>Wie viel Prozent des Einkommens durch den Soft-Cap verloren gehen (0-100).</summary>
        [JsonIgnore]
        public int SoftCapReductionPercent { get; set; }

        /// <summary>Ob der tägliche Gratis-Rush verfügbar ist (zeitmanipulations-sicher).</summary>
        [JsonIgnore]
        public bool IsFreeRushAvailable => LastFreeRushUsed.Date < DateTime.UtcNow.Date;
    }
}
