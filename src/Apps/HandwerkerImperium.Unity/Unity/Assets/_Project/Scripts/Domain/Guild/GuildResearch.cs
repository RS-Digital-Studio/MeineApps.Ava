using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Guild
{
    /// <summary>Kategorie einer Gilden-Forschung.</summary>
    public enum GuildResearchCategory
    {
        Infrastructure,
        Economy,
        Knowledge,
        Logistics,
        Workforce,
        Mastery
    }

    /// <summary>Art des Effekts einer Gilden-Forschung.</summary>
    public enum GuildResearchEffectType
    {
        MaxMembers,
        IncomeBonus,
        CostReduction,
        RewardBonus,
        XpBonus,
        EfficiencyBonus,
        MiniGameBonus,
        OrderSlotBonus,
        OrderQualityBonus,
        WorkerSlotBonus,
        TrainingSpeedBonus,
        FatigueReduction,
        ResearchSpeedBonus,
        PrestigePointBonus
    }

    /// <summary>
    /// Statische Definition einer Gilden-Forschung. Wird nicht persistiert - kommt aus GetAll().
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/GuildResearch.cs). init-only Properties → set
    /// (IsExternalInit ist .NET 5+, fehlt in netstandard2.1). UI-Methoden (Kategorie-Farbe/-NameKey)
    /// wandern in die Präsentationsschicht. 18 Forschungen in 6 Kategorien.
    /// </summary>
    public class GuildResearchDefinition
    {
        public string Id { get; set; } = "";
        public string NameKey { get; set; } = "";
        public string DescKey { get; set; } = "";
        public string Icon { get; set; } = "FlaskOutline";
        public long Cost { get; set; }
        public int Order { get; set; }
        public GuildResearchCategory Category { get; set; }
        public GuildResearchEffectType EffectType { get; set; }
        public decimal EffectValue { get; set; }

        private static readonly List<GuildResearchDefinition> _allDefinitions = new List<GuildResearchDefinition>
        {
            // ── Infrastruktur (Gilden-Ausbau) ──
            new GuildResearchDefinition
            {
                Id = "guild_expand_1", NameKey = "GuildResearch_guild_expand_1",
                DescKey = "GuildResearchDesc_guild_expand_1", Icon = "AccountMultiplePlus",
                Cost = 50_000_000, Order = 1, Category = GuildResearchCategory.Infrastructure,
                EffectType = GuildResearchEffectType.MaxMembers, EffectValue = 5
            },
            new GuildResearchDefinition
            {
                Id = "guild_expand_2", NameKey = "GuildResearch_guild_expand_2",
                DescKey = "GuildResearchDesc_guild_expand_2", Icon = "AccountMultiplePlus",
                Cost = 500_000_000, Order = 2, Category = GuildResearchCategory.Infrastructure,
                EffectType = GuildResearchEffectType.MaxMembers, EffectValue = 5
            },
            new GuildResearchDefinition
            {
                Id = "guild_expand_3", NameKey = "GuildResearch_guild_expand_3",
                DescKey = "GuildResearchDesc_guild_expand_3", Icon = "AccountMultiplePlus",
                Cost = 5_000_000_000, Order = 3, Category = GuildResearchCategory.Infrastructure,
                EffectType = GuildResearchEffectType.MaxMembers, EffectValue = 10
            },

            // ── Wirtschaft (Einkommens-Boni) ──
            new GuildResearchDefinition
            {
                Id = "guild_income_1", NameKey = "GuildResearch_guild_income_1",
                DescKey = "GuildResearchDesc_guild_income_1", Icon = "Handshake",
                Cost = 10_000_000, Order = 1, Category = GuildResearchCategory.Economy,
                EffectType = GuildResearchEffectType.IncomeBonus, EffectValue = 0.05m
            },
            new GuildResearchDefinition
            {
                Id = "guild_income_2", NameKey = "GuildResearch_guild_income_2",
                DescKey = "GuildResearchDesc_guild_income_2", Icon = "CartArrowDown",
                Cost = 100_000_000, Order = 2, Category = GuildResearchCategory.Economy,
                EffectType = GuildResearchEffectType.CostReduction, EffectValue = 0.10m
            },
            new GuildResearchDefinition
            {
                Id = "guild_income_3", NameKey = "GuildResearch_guild_income_3",
                DescKey = "GuildResearchDesc_guild_income_3", Icon = "TruckDelivery",
                Cost = 1_000_000_000, Order = 3, Category = GuildResearchCategory.Economy,
                EffectType = GuildResearchEffectType.RewardBonus, EffectValue = 0.10m
            },
            new GuildResearchDefinition
            {
                Id = "guild_income_4", NameKey = "GuildResearch_guild_income_4",
                DescKey = "GuildResearchDesc_guild_income_4", Icon = "CurrencyEur",
                Cost = 10_000_000_000, Order = 4, Category = GuildResearchCategory.Economy,
                EffectType = GuildResearchEffectType.IncomeBonus, EffectValue = 0.15m
            },

            // ── Wissen (XP + Effizienz) ──
            new GuildResearchDefinition
            {
                Id = "guild_knowledge_1", NameKey = "GuildResearch_guild_knowledge_1",
                DescKey = "GuildResearchDesc_guild_knowledge_1", Icon = "BookOpenVariant",
                Cost = 25_000_000, Order = 1, Category = GuildResearchCategory.Knowledge,
                EffectType = GuildResearchEffectType.XpBonus, EffectValue = 0.10m
            },
            new GuildResearchDefinition
            {
                Id = "guild_knowledge_2", NameKey = "GuildResearch_guild_knowledge_2",
                DescKey = "GuildResearchDesc_guild_knowledge_2", Icon = "Cog",
                Cost = 250_000_000, Order = 2, Category = GuildResearchCategory.Knowledge,
                EffectType = GuildResearchEffectType.EfficiencyBonus, EffectValue = 0.05m
            },
            new GuildResearchDefinition
            {
                Id = "guild_knowledge_3", NameKey = "GuildResearch_guild_knowledge_3",
                DescKey = "GuildResearchDesc_guild_knowledge_3", Icon = "SchoolOutline",
                Cost = 2_500_000_000, Order = 3, Category = GuildResearchCategory.Knowledge,
                EffectType = GuildResearchEffectType.MiniGameBonus, EffectValue = 0.15m
            },

            // ── Logistik (Aufträge) ──
            new GuildResearchDefinition
            {
                Id = "guild_logistics_1", NameKey = "GuildResearch_guild_logistics_1",
                DescKey = "GuildResearchDesc_guild_logistics_1", Icon = "ClipboardTextMultiple",
                Cost = 75_000_000, Order = 1, Category = GuildResearchCategory.Logistics,
                EffectType = GuildResearchEffectType.OrderSlotBonus, EffectValue = 1
            },
            new GuildResearchDefinition
            {
                Id = "guild_logistics_2", NameKey = "GuildResearch_guild_logistics_2",
                DescKey = "GuildResearchDesc_guild_logistics_2", Icon = "AccountTie",
                Cost = 750_000_000, Order = 2, Category = GuildResearchCategory.Logistics,
                EffectType = GuildResearchEffectType.OrderQualityBonus, EffectValue = 0.15m
            },
            new GuildResearchDefinition
            {
                Id = "guild_logistics_3", NameKey = "GuildResearch_guild_logistics_3",
                DescKey = "GuildResearchDesc_guild_logistics_3", Icon = "RocketLaunch",
                Cost = 3_000_000_000, Order = 3, Category = GuildResearchCategory.Logistics,
                EffectType = GuildResearchEffectType.RewardBonus, EffectValue = 0.20m
            },

            // ── Arbeitsmarkt (Workers) ──
            new GuildResearchDefinition
            {
                Id = "guild_workforce_1", NameKey = "GuildResearch_guild_workforce_1",
                DescKey = "GuildResearchDesc_guild_workforce_1", Icon = "DomainPlus",
                Cost = 150_000_000, Order = 1, Category = GuildResearchCategory.Workforce,
                EffectType = GuildResearchEffectType.WorkerSlotBonus, EffectValue = 1
            },
            new GuildResearchDefinition
            {
                Id = "guild_workforce_2", NameKey = "GuildResearch_guild_workforce_2",
                DescKey = "GuildResearchDesc_guild_workforce_2", Icon = "HumanMaleBoard",
                Cost = 1_000_000_000, Order = 2, Category = GuildResearchCategory.Workforce,
                EffectType = GuildResearchEffectType.TrainingSpeedBonus, EffectValue = 0.25m
            },
            new GuildResearchDefinition
            {
                Id = "guild_workforce_3", NameKey = "GuildResearch_guild_workforce_3",
                DescKey = "GuildResearchDesc_guild_workforce_3", Icon = "ShieldAccount",
                Cost = 5_000_000_000, Order = 3, Category = GuildResearchCategory.Workforce,
                EffectType = GuildResearchEffectType.FatigueReduction, EffectValue = 0.20m
            },

            // ── Meisterschaft (Endgame) ──
            new GuildResearchDefinition
            {
                Id = "guild_mastery_1", NameKey = "GuildResearch_guild_mastery_1",
                DescKey = "GuildResearchDesc_guild_mastery_1", Icon = "FlashOutline",
                Cost = 500_000_000, Order = 1, Category = GuildResearchCategory.Mastery,
                EffectType = GuildResearchEffectType.ResearchSpeedBonus, EffectValue = 0.20m
            },
            new GuildResearchDefinition
            {
                Id = "guild_mastery_2", NameKey = "GuildResearch_guild_mastery_2",
                DescKey = "GuildResearchDesc_guild_mastery_2", Icon = "Crown",
                Cost = 7_500_000_000, Order = 2, Category = GuildResearchCategory.Mastery,
                EffectType = GuildResearchEffectType.PrestigePointBonus, EffectValue = 0.10m
            }
        };

        public static List<GuildResearchDefinition> GetAll() => _allDefinitions;

        /// <summary>
        /// Forschungsdauer in Stunden. Tier 1 (&lt;100M): 1h, Tier 2 (100M-2B): 4h, Tier 3 (&gt;2B): 12h.
        /// </summary>
        public static double GetResearchDurationHours(long cost)
        {
            if (cost < 100_000_000) return 1.0;
            if (cost <= 2_000_000_000) return 4.0;
            return 12.0;
        }
    }

    /// <summary>
    /// Berechnete Gesamteffekte aller abgeschlossenen Gilden-Forschungen.
    /// 1:1-Port aus dem Avalonia-Original.
    /// </summary>
    public class GuildResearchEffects
    {
        public decimal IncomeBonus { get; set; }
        public decimal CostReduction { get; set; }
        public decimal RewardBonus { get; set; }
        public decimal XpBonus { get; set; }
        public decimal EfficiencyBonus { get; set; }
        public decimal MiniGameBonus { get; set; }
        public int MaxMembersBonus { get; set; }
        public int OrderSlotBonus { get; set; }
        public decimal OrderQualityBonus { get; set; }
        public int WorkerSlotBonus { get; set; }
        public decimal TrainingSpeedBonus { get; set; }
        public decimal FatigueReduction { get; set; }
        public decimal ResearchSpeedBonus { get; set; }
        public decimal PrestigePointBonus { get; set; }

        /// <summary>Berechnet Gesamteffekte aus einer Liste abgeschlossener Forschungs-IDs.</summary>
        public static GuildResearchEffects Calculate(HashSet<string> completedIds)
        {
            var effects = new GuildResearchEffects();
            if (completedIds.Count == 0) return effects;

            foreach (var def in GuildResearchDefinition.GetAll())
            {
                if (!completedIds.Contains(def.Id)) continue;

                switch (def.EffectType)
                {
                    case GuildResearchEffectType.MaxMembers:
                        effects.MaxMembersBonus += (int)def.EffectValue;
                        break;
                    case GuildResearchEffectType.IncomeBonus:
                        effects.IncomeBonus += def.EffectValue;
                        break;
                    case GuildResearchEffectType.CostReduction:
                        effects.CostReduction += def.EffectValue;
                        break;
                    case GuildResearchEffectType.RewardBonus:
                        effects.RewardBonus += def.EffectValue;
                        break;
                    case GuildResearchEffectType.XpBonus:
                        effects.XpBonus += def.EffectValue;
                        break;
                    case GuildResearchEffectType.EfficiencyBonus:
                        effects.EfficiencyBonus += def.EffectValue;
                        break;
                    case GuildResearchEffectType.MiniGameBonus:
                        effects.MiniGameBonus += def.EffectValue;
                        break;
                    case GuildResearchEffectType.OrderSlotBonus:
                        effects.OrderSlotBonus += (int)def.EffectValue;
                        break;
                    case GuildResearchEffectType.OrderQualityBonus:
                        effects.OrderQualityBonus += def.EffectValue;
                        break;
                    case GuildResearchEffectType.WorkerSlotBonus:
                        effects.WorkerSlotBonus += (int)def.EffectValue;
                        break;
                    case GuildResearchEffectType.TrainingSpeedBonus:
                        effects.TrainingSpeedBonus += def.EffectValue;
                        break;
                    case GuildResearchEffectType.FatigueReduction:
                        effects.FatigueReduction += def.EffectValue;
                        break;
                    case GuildResearchEffectType.ResearchSpeedBonus:
                        effects.ResearchSpeedBonus += def.EffectValue;
                        break;
                    case GuildResearchEffectType.PrestigePointBonus:
                        effects.PrestigePointBonus += def.EffectValue;
                        break;
                }
            }

            return effects;
        }
    }
}
