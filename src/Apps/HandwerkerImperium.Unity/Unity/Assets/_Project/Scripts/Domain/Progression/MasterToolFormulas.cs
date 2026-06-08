#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>Seltenheits-Stufe eines Meisterwerkzeugs (rein optisch/sortierend).</summary>
    public enum MasterToolRarity { Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4 }

    /// <summary>Bezugsgröße einer Freischalt-Bedingung (auf die neue 3D-Idle-Mechanik gemappt, GDD §6.6).</summary>
    public enum MasterToolRequirementKind
    {
        MaxStationLevel = 0,
        OrdersServed = 1,
        RestorationPhases = 2,
        PrestigeCount = 3,
        CollectedTools = 4
    }

    /// <summary>Eingabe-Kontext für den Eligibility-Check (vom Game-Layer aus dem State befüllt).</summary>
    public sealed class MasterToolContext
    {
        public int MaxStationLevel;
        public long OrdersServed;
        public int RestorationPhases;
        public int PrestigeCount;
        public int CollectedTools;
    }

    /// <summary>Definition eines Meisterwerkzeugs: permanenter Income-Bonus + eine Freischalt-Bedingung.</summary>
    public sealed class MasterToolDefinition
    {
        public string Id;
        public MasterToolRarity Rarity;
        public decimal IncomeBonus;
        public MasterToolRequirementKind RequirementKind;
        public int RequirementThreshold;

        public MasterToolDefinition(string id, MasterToolRarity rarity, decimal incomeBonus,
            MasterToolRequirementKind kind, int threshold)
        {
            Id = id;
            Rarity = rarity;
            IncomeBonus = incomeBonus;
            RequirementKind = kind;
            RequirementThreshold = threshold;
        }
    }

    /// <summary>
    /// Meisterwerkzeuge (GDD §6.6 / PROGRESSION §6): 12 permanente Sammler-Einkommens-Boosts (Summe +74 %),
    /// Freischalt-Bedingungen über alle Akte gestreut. Werte 1:1 aus dem Original geborgen, Bedingungen
    /// sinngemäß auf die 3D-Idle-Mechanik gemappt. Reine, Unity-freie Logik + Default-Katalog.
    /// </summary>
    public static class MasterToolFormulas
    {
        /// <summary>Default-Katalog der 12 Meisterwerkzeuge (Original-Boni + neu gemappte Bedingungen).</summary>
        public static List<MasterToolDefinition> DefaultCatalog()
        {
            return new List<MasterToolDefinition>
            {
                new MasterToolDefinition("mt_golden_hammer",   MasterToolRarity.Common,    0.02m, MasterToolRequirementKind.MaxStationLevel,  75),
                new MasterToolDefinition("mt_diamond_saw",     MasterToolRarity.Common,    0.02m, MasterToolRequirementKind.MaxStationLevel, 150),
                new MasterToolDefinition("mt_titanium_pliers", MasterToolRarity.Common,    0.03m, MasterToolRequirementKind.OrdersServed,    150),
                new MasterToolDefinition("mt_brass_level",     MasterToolRarity.Common,    0.03m, MasterToolRequirementKind.RestorationPhases, 5),
                new MasterToolDefinition("mt_silver_wrench",   MasterToolRarity.Uncommon,  0.05m, MasterToolRequirementKind.MaxStationLevel, 300),
                new MasterToolDefinition("mt_jade_brush",      MasterToolRarity.Uncommon,  0.05m, MasterToolRequirementKind.RestorationPhases, 10),
                new MasterToolDefinition("mt_crystal_chisel",  MasterToolRarity.Uncommon,  0.05m, MasterToolRequirementKind.PrestigeCount,     1),
                new MasterToolDefinition("mt_obsidian_drill",  MasterToolRarity.Rare,      0.07m, MasterToolRequirementKind.MaxStationLevel, 750),
                new MasterToolDefinition("mt_ruby_blade",      MasterToolRarity.Rare,      0.07m, MasterToolRequirementKind.PrestigeCount,     2),
                new MasterToolDefinition("mt_emerald_toolbox", MasterToolRarity.Epic,      0.10m, MasterToolRequirementKind.MaxStationLevel, 500),
                new MasterToolDefinition("mt_dragon_anvil",    MasterToolRarity.Epic,      0.10m, MasterToolRequirementKind.PrestigeCount,     3),
                new MasterToolDefinition("mt_master_crown",    MasterToolRarity.Legendary, 0.15m, MasterToolRequirementKind.CollectedTools,   11),
            };
        }

        /// <summary>True, wenn die Freischalt-Bedingung eines Werkzeugs im aktuellen Kontext erfüllt ist.</summary>
        public static bool IsEligible(MasterToolDefinition def, MasterToolContext ctx)
        {
            if (def == null || ctx == null) return false;
            switch (def.RequirementKind)
            {
                case MasterToolRequirementKind.MaxStationLevel: return ctx.MaxStationLevel >= def.RequirementThreshold;
                case MasterToolRequirementKind.OrdersServed: return ctx.OrdersServed >= def.RequirementThreshold;
                case MasterToolRequirementKind.RestorationPhases: return ctx.RestorationPhases >= def.RequirementThreshold;
                case MasterToolRequirementKind.PrestigeCount: return ctx.PrestigeCount >= def.RequirementThreshold;
                case MasterToolRequirementKind.CollectedTools: return ctx.CollectedTools >= def.RequirementThreshold;
                default: return false;
            }
        }

        /// <summary>Summe der Income-Boni aller gesammelten Werkzeuge (max. +74 % bei voller Sammlung).</summary>
        public static decimal TotalIncomeBonus(IReadOnlyCollection<string>? collectedIds, List<MasterToolDefinition> catalog)
        {
            if (collectedIds == null || collectedIds.Count == 0 || catalog == null) return 0m;
            decimal total = 0m;
            foreach (var def in catalog)
                if (def != null && Contains(collectedIds, def.Id))
                    total += def.IncomeBonus;
            return total;
        }

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (var x in ids)
                if (x == id) return true;
            return false;
        }
    }
}
