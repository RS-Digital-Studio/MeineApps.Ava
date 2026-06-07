using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Spezialisierungstyp für Workshops (ab Level 50).
    /// Quality + Economy haben sichtbare Eigen-Vorteile (Aura-Verdopplung bzw. Order-Reward-Bonus).
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/WorkshopSpecialization.cs). Numerische Werte save-relevant.
    /// </summary>
    public enum SpecializationType
    {
        /// <summary>+30% Einkommen, -1 Worker-Slot.</summary>
        Efficiency,
        /// <summary>+20% Worker-Effizienz, +15% Kosten, +100% Aura-Bonus.</summary>
        Quality,
        /// <summary>-25% Kosten, -5% Einkommen, +15% Auftragsbelohnung.</summary>
        Economy
    }

    /// <summary>
    /// Eine gewählte Workshop-Spezialisierung. Reine Spiellogik-Modifikatoren —
    /// UI-Methoden (NameKey/Color) leben in der Unity-Präsentationsschicht.
    /// </summary>
    public class WorkshopSpecialization
    {
        [JsonProperty("type")]
        public SpecializationType Type { get; set; }

        /// <summary>Einkommens-Modifikator (Efficiency +30%, Economy -5%).</summary>
        [JsonIgnore]
        public decimal IncomeModifier => Type switch
        {
            SpecializationType.Efficiency => 0.30m,
            SpecializationType.Quality => 0m,
            SpecializationType.Economy => -0.05m,
            _ => 0m
        };

        /// <summary>Kosten-Modifikator (Quality +15%, Economy -25%).</summary>
        [JsonIgnore]
        public decimal CostModifier => Type switch
        {
            SpecializationType.Efficiency => 0m,
            SpecializationType.Quality => 0.15m,
            SpecializationType.Economy => -0.25m,
            _ => 0m
        };

        /// <summary>Worker-Effizienz-Modifikator (Quality +20%).</summary>
        [JsonIgnore]
        public decimal EfficiencyModifier => Type switch
        {
            SpecializationType.Efficiency => 0m,
            SpecializationType.Quality => 0.20m,
            SpecializationType.Economy => 0m,
            _ => 0m
        };

        /// <summary>Worker-Slot-Modifikator (Efficiency -1).</summary>
        [JsonIgnore]
        public int WorkerCapacityModifier => Type switch
        {
            SpecializationType.Efficiency => -1,
            _ => 0
        };

        /// <summary>
        /// Multiplikator auf den Aura-Bonus von S+-Tier-Workern. Quality verdoppelt die effektive
        /// Aura über den Standard-Cap hinweg. Ohne Spezialisierung: 1.0m.
        /// </summary>
        [JsonIgnore]
        public decimal AuraBonusMultiplier => Type switch
        {
            SpecializationType.Quality => 2.0m,
            _ => 1.0m
        };

        /// <summary>Bonus auf Auftrags-Belohnungen aus diesem Workshop (Economy +15%).</summary>
        [JsonIgnore]
        public decimal OrderRewardBonus => Type switch
        {
            SpecializationType.Economy => 0.15m,
            _ => 0m
        };
    }
}
