#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using HandwerkerImperium.Domain;
using HandwerkerImperium.Domain.Orders;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Repräsentiert eine Werkstatt/ein Gewerk im Spiel.
    /// Jede Werkstatt kann aufgewertet (1-1000), mit Workern besetzt werden und hat laufende Kosten.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Workshop.cs). Berechnete Properties delegieren an
    /// <see cref="WorkshopFormulas"/>. Persistenz über Newtonsoft.Json. UI-Methoden (Icon) in der
    /// Präsentationsschicht. Unity-sicher (C# 9, netstandard2.1).
    /// </summary>
    public class Workshop
    {
        [JsonProperty("type")]
        public WorkshopType Type { get; set; }

        /// <summary>Aktuelles Level (1-1000). Höher = mehr Einkommen, mehr Worker-Slots, höhere Kosten.</summary>
        [JsonProperty("level")]
        public int Level { get; set; } = 1;

        [JsonProperty("workers")]
        public List<Worker> Workers { get; set; } = new List<Worker>();

        [JsonProperty("totalEarned")]
        public decimal TotalEarned { get; set; }

        [JsonProperty("ordersCompleted")]
        public int OrdersCompleted { get; set; }

        /// <summary>Ob diese Werkstatt gekauft/freigeschaltet wurde.</summary>
        [JsonProperty("isUnlocked")]
        public bool IsUnlocked { get; set; }

        /// <summary>Gewählte Spezialisierung (ab Level 50, null = keine).</summary>
        [JsonProperty("specialization")]
        public WorkshopSpecialization? WorkshopSpecialization { get; set; }

        /// <summary>
        /// Persistente Risk/Reward-Default-Strategie pro Workshop (Sticky-Pattern, reduziert Choice-Fatigue).
        /// Wird beim Auftrag-Spawn in die Order kopiert.
        /// </summary>
        [JsonProperty("defaultRiskStrategy")]
        public OrderStrategy DefaultRiskStrategy { get; set; } = OrderStrategy.Standard;

        /// <summary>Maximales Workshop-Level.</summary>
        public const int MaxLevel = GameBalanceConstants.WorkshopMaxLevel;

        /// <summary>
        /// Maximale Worker auf aktuellem Level. +1 alle 50 Level (max 20 bei Level 1000).
        /// </summary>
        [JsonIgnore]
        public int BaseMaxWorkers => Math.Min(20, 1 + (Level - 1) / 50);

        /// <summary>
        /// Gesamt-Max-Worker inkl. Gebäude-Bonus + Ad-Bonus + Rebirth-Sterne + Spezialisierung.
        /// </summary>
        [JsonIgnore]
        public int MaxWorkers => Math.Max(1, BaseMaxWorkers + ExtraWorkerSlots + AdBonusWorkerSlots
            + RebirthExtraWorkers + (WorkshopSpecialization?.WorkerCapacityModifier ?? 0));

        /// <summary>Extra Worker-Slots aus Gebäuden/Forschung (extern gesetzt).</summary>
        [JsonIgnore]
        public int ExtraWorkerSlots { get; set; }

        /// <summary>Maximale Anzahl Ad-Bonus-Slots pro Workshop (Exploit-Schutz).</summary>
        public const int MaxAdBonusWorkerSlots = 3;

        /// <summary>Extra Worker-Slots durch Rewarded Ads (persistent, max 3).</summary>
        [JsonProperty("adBonusWorkerSlots")]
        public int AdBonusWorkerSlots { get; set; }

        /// <summary>Level-Resistenz-Bonus aus Forschung (0.0-0.5, extern gesetzt).</summary>
        [JsonIgnore]
        public decimal LevelResistanceBonus { get; set; }

        /// <summary>Upgrade-Kosten-Rabatt aus Prestige-Shop (0.0-1.0, extern gesetzt).</summary>
        [JsonIgnore]
        public decimal UpgradeDiscount { get; set; }

        // ═══════════════════════════════════════════════════════════════════
        // REBIRTH (Late-Game Prestige pro Workshop, 0-5 Sterne)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Anzahl der Rebirth-Sterne (0-5). Permanent, überlebt Prestige + Ascension (extern gesetzt).</summary>
        [JsonIgnore]
        public int RebirthStars { get; set; }

        /// <summary>Einkommens-Bonus durch Rebirth-Sterne (0% bis +150%).</summary>
        [JsonIgnore]
        public decimal RebirthIncomeBonus => RebirthStars >= 1 && RebirthStars <= 5
            ? GameBalanceConstants.RebirthIncomeBonuses[RebirthStars - 1]
            : 0m;

        /// <summary>Upgrade-Kosten-Rabatt durch Rebirth-Sterne (0% bis -25%).</summary>
        [JsonIgnore]
        public decimal RebirthUpgradeDiscount => RebirthStars >= 1 && RebirthStars <= 5
            ? GameBalanceConstants.RebirthUpgradeDiscounts[RebirthStars - 1]
            : 0m;

        /// <summary>Extra Worker-Slots durch Rebirth-Sterne (0 bis +2).</summary>
        [JsonIgnore]
        public int RebirthExtraWorkers => RebirthStars < GameBalanceConstants.RebirthExtraWorkers.Length
            ? GameBalanceConstants.RebirthExtraWorkers[RebirthStars]
            : GameBalanceConstants.RebirthExtraWorkers[GameBalanceConstants.RebirthExtraWorkers.Length - 1];

        // ═══════════════════════════════════════════════════════════════════
        // BERECHNETE PROPERTIES (delegieren an WorkshopFormulas für Testbarkeit)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Basis-Einkommen pro Worker pro Sekunde.</summary>
        [JsonIgnore]
        public decimal BaseIncomePerWorker => WorkshopFormulas.CalculateBaseIncomePerWorker(Level, Type);

        /// <summary>Brutto-Einkommen pro Sekunde (alle Worker, Aura, Rebirth, Spezialisierung).</summary>
        [JsonIgnore]
        public decimal GrossIncomePerSecond
        {
            get
            {
                var gross = WorkshopFormulas.CalculateGrossIncome(Level, Type, Workers, LevelResistanceBonus, RebirthIncomeBonus);
                if (WorkshopSpecialization != null)
                {
                    // Quality: +20% Worker-Effizienz als Einkommensboost
                    if (WorkshopSpecialization.EfficiencyModifier != 0m)
                        gross *= (1m + WorkshopSpecialization.EfficiencyModifier);
                    // Efficiency: +30% / Economy: -5%
                    if (WorkshopSpecialization.IncomeModifier != 0m)
                        gross *= (1m + WorkshopSpecialization.IncomeModifier);
                }
                return gross;
            }
        }

        /// <summary>Level-Anforderungsfaktor für einen Worker (Tier-Resistenz + Forschung).</summary>
        public decimal GetWorkerLevelFitFactor(Worker worker) =>
            WorkshopFormulas.CalculateLevelFitFactor(Level, worker.Tier.GetLevelResistance(), LevelResistanceBonus);

        /// <summary>Kumulativer Meilenstein-Multiplikator für das aktuelle Level.</summary>
        public decimal GetMilestoneMultiplier() => WorkshopFormulas.CalculateMilestoneMultiplier(Level);

        /// <summary>Prüft ob ein Level ein Meilenstein ist.</summary>
        public static bool IsMilestoneLevel(int level) => WorkshopFormulas.IsMilestoneLevel(level);

        /// <summary>Einzel-Multiplikator für ein bestimmtes Meilenstein-Level.</summary>
        public static decimal GetMilestoneMultiplierForLevel(int level) => WorkshopFormulas.GetMilestoneMultiplierForLevel(level);

        /// <summary>Miete pro Stunde (skaliert mit Level).</summary>
        [JsonIgnore]
        public decimal RentPerHour => WorkshopFormulas.CalculateRentPerHour(Level);

        /// <summary>Materialkosten pro Stunde (hybrid: linear bis Lv.100, dann exponentiell).</summary>
        [JsonIgnore]
        public decimal MaterialCostPerHour => WorkshopFormulas.CalculateMaterialCostPerHour(Level, Type);

        /// <summary>Total worker wages per hour (For-Schleife, kein LINQ).</summary>
        [JsonIgnore]
        public decimal TotalWagesPerHour
        {
            get
            {
                decimal total = 0m;
                for (int i = 0; i < Workers.Count; i++)
                {
                    if (!Workers[i].IsResting)
                        total += Workers[i].WagePerHour;
                }
                return total;
            }
        }

        /// <summary>Total running costs per hour (rent + material + wages + Spezialisierung).</summary>
        [JsonIgnore]
        public decimal TotalCostsPerHour
        {
            get
            {
                var costs = RentPerHour + MaterialCostPerHour + TotalWagesPerHour;
                // Quality: +15% Kosten / Economy: -25% Kosten
                if (WorkshopSpecialization?.CostModifier is > 0m or < 0m)
                    costs *= (1m + WorkshopSpecialization!.CostModifier);
                return Math.Max(0m, costs);
            }
        }

        /// <summary>
        /// Roher Netto-Einkommenswert OHNE Prestige-Multiplikator und Research-Boni.
        /// Die tatsächliche Berechnung mit allen Modifikatoren erfolgt im GameLoopService.
        /// </summary>
        [JsonIgnore]
        public decimal NetIncomePerSecond => GrossIncomePerSecond - TotalCostsPerHour / 3600m;

        /// <summary>Legacy IncomePerSecond (nutzt EffectiveEfficiency).</summary>
        [JsonIgnore]
        public decimal IncomePerSecond => GrossIncomePerSecond;

        /// <summary>Upgrade-Kosten mit allen Rabatten (Rebirth, Prestige-Shop, VIP).</summary>
        [JsonIgnore]
        public decimal UpgradeCost =>
            WorkshopFormulas.CalculateUpgradeCost(Level, RebirthUpgradeDiscount, UpgradeDiscount, VipCostReduction, ChallengeUpgradeCostMultiplier);

        /// <summary>VIP-Kosten-Reduktion (0.0-0.10, extern gesetzt).</summary>
        [JsonIgnore]
        public decimal VipCostReduction { get; set; }

        /// <summary>
        /// Challenge-"Inflationszeit"-Kosten-Multiplikator (1.0 = aus, 2.0 = aktiv, extern gesetzt),
        /// damit angezeigte Upgrade-Kosten exakt den abgezogenen entsprechen.
        /// </summary>
        [JsonIgnore]
        public decimal ChallengeUpgradeCostMultiplier { get; set; } = 1.0m;

        /// <summary>Kombinierter Rabattfaktor aus Rebirth + Prestige-Shop + VIP.</summary>
        private decimal GetCombinedDiscountFactor() =>
            WorkshopFormulas.CalculateDiscountFactor(RebirthUpgradeDiscount, UpgradeDiscount, VipCostReduction);

        /// <summary>Bulk-Upgrade Gesamtkosten für N Level.</summary>
        public decimal GetBulkUpgradeCost(int count) =>
            WorkshopFormulas.CalculateBulkUpgradeCost(Level, count, GetCombinedDiscountFactor(), ChallengeUpgradeCostMultiplier);

        /// <summary>Maximale leistbare Upgrades bei gegebenem Budget.</summary>
        public (int count, decimal cost) GetMaxAffordableUpgrades(decimal budget) =>
            WorkshopFormulas.CalculateMaxAffordableUpgrades(Level, budget, GetCombinedDiscountFactor(), ChallengeUpgradeCostMultiplier);

        /// <summary>Kosten zum Freischalten dieser Werkstatt (einmalig).</summary>
        [JsonIgnore]
        public decimal UnlockCost => Type.GetUnlockCost();

        [JsonIgnore]
        public bool CanUpgrade => Level < MaxLevel;

        [JsonIgnore]
        public bool CanHireWorker => Workers.Count < MaxWorkers;

        /// <summary>Einstellungskosten für den nächsten Worker.</summary>
        [JsonIgnore]
        public decimal HireWorkerCost => WorkshopFormulas.CalculateHireWorkerCost(Workers.Count);

        public static Workshop Create(WorkshopType type)
        {
            return new Workshop
            {
                Type = type,
                IsUnlocked = type == WorkshopType.Carpenter // Carpenter ist immer freigeschaltet
            };
        }
    }
}
