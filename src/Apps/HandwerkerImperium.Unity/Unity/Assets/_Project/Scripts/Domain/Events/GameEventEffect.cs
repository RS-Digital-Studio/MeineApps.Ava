#nullable enable
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Events
{
    /// <summary>
    /// Effekte eines aktiven Spiel-Events. Multiplikatoren default 1.0 (keine Änderung).
    /// 1:1-Port aus dem Avalonia-Original (Models/GameEventEffect.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class GameEventEffect
    {
        /// <summary>Einkommens-Multiplikator (1.0 = keine Änderung, 1.5 = +50%).</summary>
        [JsonProperty("incomeMultiplier")]
        public decimal IncomeMultiplier { get; set; } = 1.0m;

        /// <summary>Material-/Laufkosten-Multiplikator (1.0 = keine Änderung, 2.0 = doppelt).</summary>
        [JsonProperty("costMultiplier")]
        public decimal CostMultiplier { get; set; } = 1.0m;

        /// <summary>Auftragsbelohnungs-Multiplikator.</summary>
        [JsonProperty("rewardMultiplier")]
        public decimal RewardMultiplier { get; set; } = 1.0m;

        /// <summary>Reputations-Änderung (positiv = Gewinn, negativ = Verlust).</summary>
        [JsonProperty("reputationChange")]
        public decimal ReputationChange { get; set; }

        /// <summary>Beschränkt den Worker-Markt auf bestimmte Tiers (null = keine Beschränkung).</summary>
        [JsonProperty("marketRestriction")]
        public WorkerTier? MarketRestriction { get; set; }

        /// <summary>Betrifft nur einen bestimmten Workshop-Typ (null = alle).</summary>
        [JsonProperty("affectedWorkshop")]
        public WorkshopType? AffectedWorkshop { get; set; }

        /// <summary>Spezial-Effekt-Bezeichner für komplexe Events (z.B. "tax_10_percent", "mood_drop_all_20").</summary>
        [JsonProperty("specialEffect")]
        public string? SpecialEffect { get; set; }
    }
}
