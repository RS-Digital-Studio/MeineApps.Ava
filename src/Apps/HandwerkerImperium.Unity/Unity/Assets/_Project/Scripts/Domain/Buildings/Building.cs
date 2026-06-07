using System;
using Newtonsoft.Json;
using HandwerkerImperium.Domain;

namespace HandwerkerImperium.Domain.Buildings
{
    /// <summary>
    /// Hilfsgebäude, das passive Boni liefert. Kann von Level 1 bis 5 ausgebaut werden.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Building.cs). Datenklasse + reine Effekt-/
    /// Kosten-Logik. Die UI-Properties Name/Icon/Description (rufen UI-Extensions) wandern in die
    /// Präsentationsschicht. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class Building
    {
        [JsonProperty("type")]
        public BuildingType Type { get; set; }

        /// <summary>Aktuelles Level (1-5). 0 = nicht gebaut.</summary>
        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("isBuilt")]
        public bool IsBuilt { get; set; }

        /// <summary>
        /// Kosten zum Bauen (Level 1) oder Upgrade auf nächstes Level.
        /// Formel: BaseCost * 2^(Level) — sanftere Kurve als 3^Level.
        /// </summary>
        [JsonIgnore]
        public decimal NextLevelCost
        {
            get
            {
                if (!IsBuilt) return Type.GetBaseCost();
                if (Level >= Type.GetMaxLevel()) return 0m;
                return Type.GetBaseCost() * (decimal)Math.Pow(2, Level);
            }
        }

        [JsonIgnore]
        public bool CanUpgrade => IsBuilt && Level < Type.GetMaxLevel();

        // ── Effekte (variieren nach Gebäude-Typ und Level) ──

        /// <summary>Canteen: Passive Mood-Erholung pro Stunde. Level 1-5: 1%, 2%, 3%, 4%, 5%.</summary>
        [JsonIgnore]
        public decimal MoodRecoveryPerHour => Type == BuildingType.Canteen ? Level * 1.0m : 0m;

        /// <summary>Canteen: Ruhezeit-Reduktions-Multiplikator. Level 1-5: 50%, 55%, 60%, 70%, 80%.</summary>
        [JsonIgnore]
        public decimal RestTimeReduction => Type == BuildingType.Canteen ? Level switch
        {
            1 => 0.50m,
            2 => 0.55m,
            3 => 0.60m,
            4 => 0.70m,
            5 => 0.80m,
            _ => 0m
        } : 0m;

        /// <summary>Storage: Material-Kostenreduktion. Level 1-5: 15%, 25%, 35%, 45%, 50%.</summary>
        [JsonIgnore]
        public decimal MaterialCostReduction => Type == BuildingType.Storage ? Level switch
        {
            1 => 0.15m,
            2 => 0.25m,
            3 => 0.35m,
            4 => 0.45m,
            5 => 0.50m,
            _ => 0m
        } : 0m;

        /// <summary>Office: Extra Order-Slots. Level 1-5: 2, 3, 4, 5, 6.</summary>
        [JsonIgnore]
        public int ExtraOrderSlots => Type == BuildingType.Office ? Level + 1 : 0;

        /// <summary>Showroom: Tägliche passive Reputations-Gewinnung. Level 1-5: 0.5, 1.0, 1.5, 2.0, 2.5.</summary>
        [JsonIgnore]
        public decimal DailyReputationGain => Type == BuildingType.Showroom ? Level * 0.5m : 0m;

        /// <summary>
        /// TrainingCenter: Trainings-Geschwindigkeits-Multiplikator. Level 1-5: 2.5x..6.5x.
        /// Formel: 1.0 + Level * TrainingCenterSpeedPerLevel + 0.5 (Basis-Bonus für den Bau).
        /// </summary>
        [JsonIgnore]
        public decimal TrainingSpeedMultiplier => Type == BuildingType.TrainingCenter
            ? 1.0m + Level * GameBalanceConstants.TrainingCenterSpeedPerLevel + 0.5m
            : 1.0m;

        /// <summary>VehicleFleet: Auftragsbelohnungs-Bonus. Level 1-5: 20%, 30%, 40%, 50%, 60%.</summary>
        [JsonIgnore]
        public decimal OrderRewardBonus => Type == BuildingType.VehicleFleet ? Level switch
        {
            1 => 0.20m,
            2 => 0.30m,
            3 => 0.40m,
            4 => 0.50m,
            5 => 0.60m,
            _ => 0m
        } : 0m;

        /// <summary>WorkshopExtension: Extra Worker-Slots pro Workshop. Level 1-5: 2, 3, 4, 5, 6.</summary>
        [JsonIgnore]
        public int ExtraWorkerSlots => Type == BuildingType.WorkshopExtension ? Level + 1 : 0;

        public static Building Create(BuildingType type)
        {
            return new Building { Type = type };
        }
    }
}
