using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Aufzeichnung eines einzelnen Prestige-Durchlaufs.
    /// Wird in PrestigeData.History gespeichert (max. 20 Einträge).
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/PrestigeHistoryEntry.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class PrestigeHistoryEntry
    {
        /// <summary>Gewählter Prestige-Tier.</summary>
        [JsonProperty("tier")]
        public PrestigeTier Tier { get; set; }

        /// <summary>Zeitpunkt des Prestiges (UTC).</summary>
        [JsonProperty("date")]
        public DateTime Date { get; set; }

        /// <summary>Erhaltene Prestige-Punkte.</summary>
        [JsonProperty("points")]
        public int PointsEarned { get; set; }

        /// <summary>Spieler-Level beim Prestige.</summary>
        [JsonProperty("level")]
        public int PlayerLevel { get; set; }

        /// <summary>Permanenter Multiplikator nach diesem Prestige.</summary>
        [JsonProperty("multiplier")]
        public decimal MultiplierAfter { get; set; }

        /// <summary>Insgesamt verdientes Geld in diesem Durchlauf.</summary>
        [JsonProperty("moneyEarned")]
        public decimal TotalMoneyEarned { get; set; }

        /// <summary>Dauer des Durchlaufs in Ticks (0 = unbekannt, alte Einträge).</summary>
        [JsonProperty("runDurationTicks")]
        public long RunDurationTicks { get; set; }

        /// <summary>Dauer als TimeSpan (null bei alten Einträgen ohne Tracking).</summary>
        [JsonIgnore]
        public TimeSpan? RunDuration => RunDurationTicks > 0 ? TimeSpan.FromTicks(RunDurationTicks) : (TimeSpan?)null;

        /// <summary>Aktive Challenges während dieses Durchlaufs (leer = keine).</summary>
        [JsonProperty("challenges")]
        public List<PrestigeChallengeType> Challenges { get; set; } = new List<PrestigeChallengeType>();

        /// <summary>Bonus-PP aus Spielleistung (flat, nach Tier-Multi).</summary>
        [JsonProperty("bonusPp")]
        public int BonusPrestigePoints { get; set; }
    }
}
