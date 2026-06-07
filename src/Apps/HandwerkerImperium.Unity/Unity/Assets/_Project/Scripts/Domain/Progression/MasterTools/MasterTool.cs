using System.Collections.Generic;
using System.Linq;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Schmaler Eingabe-Kontext für <see cref="MasterTool.CheckEligibility"/>. Entkoppelt die
    /// Freischalt-Prüfung vom (noch nicht portierten) GameState — die Schwellen bleiben 1:1.
    /// Die Game-Schicht befüllt das aus dem realen State.
    /// </summary>
    public sealed class MasterToolEligibilityContext
    {
        /// <summary>Höchstes erreichtes Workshop-Level über alle Workshops.</summary>
        public int MaxWorkshopLevel { get; set; }
        public long TotalOrdersCompleted { get; set; }
        public long TotalMiniGamesPlayed { get; set; }
        public int PerfectRatings { get; set; }
        public int BronzeCount { get; set; }
        public int SilverCount { get; set; }
        public int GoldCount { get; set; }
        public int CollectedMasterToolsCount { get; set; }
    }

    /// <summary>
    /// Statische Definition eines Meisterwerkzeugs (unveränderlich).
    /// 1:1-Port aus dem Avalonia-Original (Models/MasterTool.cs). Original ist positional record;
    /// in Unity/netstandard2.1 kein IsExternalInit → Klasse mit Ctor.
    /// </summary>
    public sealed class MasterToolDefinition
    {
        public string Id { get; }
        public string NameKey { get; }
        public string DescriptionKey { get; }
        public string Icon { get; }
        public MasterToolRarity Rarity { get; }
        public decimal IncomeBonus { get; }

        public MasterToolDefinition(string id, string nameKey, string descriptionKey, string icon,
            MasterToolRarity rarity, decimal incomeBonus)
        {
            Id = id;
            NameKey = nameKey;
            DescriptionKey = descriptionKey;
            Icon = icon;
            Rarity = rarity;
            IncomeBonus = incomeBonus;
        }
    }

    /// <summary>
    /// Statische Helfer-Klasse für Meisterwerkzeuge mit passiven Einkommens-Boni (12 Artefakte).
    /// 1:1-Port aus dem Avalonia-Original. CheckEligibility nimmt jetzt einen schmalen Context
    /// (statt GameState); die Schwellen sind 1:1. MasterToolRarity-Enum ist in MasterToolRarity.cs (Schicht 10).
    /// </summary>
    public static class MasterTool
    {
        private static readonly List<MasterToolDefinition> _allDefinitions = new List<MasterToolDefinition>
        {
            // Common (4x, +2-3% Einkommen)
            new MasterToolDefinition("mt_golden_hammer", "MasterToolGoldenHammer", "MasterToolGoldenHammerDesc", "Hammer", MasterToolRarity.Common, 0.02m),
            new MasterToolDefinition("mt_diamond_saw", "MasterToolDiamondSaw", "MasterToolDiamondSawDesc", "Saw", MasterToolRarity.Common, 0.02m),
            new MasterToolDefinition("mt_titanium_pliers", "MasterToolTitaniumPliers", "MasterToolTitaniumPliersDesc", "Wrench", MasterToolRarity.Common, 0.03m),
            new MasterToolDefinition("mt_brass_level", "MasterToolBrassLevel", "MasterToolBrassLevelDesc", "RulerSquare", MasterToolRarity.Common, 0.03m),
            // Uncommon (3x, +5% Einkommen)
            new MasterToolDefinition("mt_silver_wrench", "MasterToolSilverWrench", "MasterToolSilverWrenchDesc", "Screwdriver", MasterToolRarity.Uncommon, 0.05m),
            new MasterToolDefinition("mt_jade_brush", "MasterToolJadeBrush", "MasterToolJadeBrushDesc", "Brush", MasterToolRarity.Uncommon, 0.05m),
            new MasterToolDefinition("mt_crystal_chisel", "MasterToolCrystalChisel", "MasterToolCrystalChiselDesc", "Pickaxe", MasterToolRarity.Uncommon, 0.05m),
            // Rare (2x, +7% Einkommen)
            new MasterToolDefinition("mt_obsidian_drill", "MasterToolObsidianDrill", "MasterToolObsidianDrillDesc", "Drill", MasterToolRarity.Rare, 0.07m),
            new MasterToolDefinition("mt_ruby_blade", "MasterToolRubyBlade", "MasterToolRubyBladeDesc", "DiamondStone", MasterToolRarity.Rare, 0.07m),
            // Epic (2x, +10% Einkommen)
            new MasterToolDefinition("mt_emerald_toolbox", "MasterToolEmeraldToolbox", "MasterToolEmeraldToolboxDesc", "Toolbox", MasterToolRarity.Epic, 0.10m),
            new MasterToolDefinition("mt_dragon_anvil", "MasterToolDragonAnvil", "MasterToolDragonAnvilDesc", "Anvil", MasterToolRarity.Epic, 0.10m),
            // Legendary (1x, +15% Einkommen)
            new MasterToolDefinition("mt_master_crown", "MasterToolMasterCrown", "MasterToolMasterCrownDesc", "Crown", MasterToolRarity.Legendary, 0.15m),
        };

        public static List<MasterToolDefinition> GetAllDefinitions() => _allDefinitions;

        private static readonly HashSet<string> _validIds = new HashSet<string>(_allDefinitions.Select(d => d.Id));

        /// <summary>Immutable View aller gültigen Tool-IDs (Unity/netstandard2.1 kennt kein IReadOnlySet).</summary>
        public static IReadOnlyCollection<string> GetValidIds() => _validIds;

        /// <summary>O(1)-Gültigkeitsprüfung einer Tool-ID.</summary>
        public static bool IsValidId(string id) => _validIds.Contains(id);

        /// <summary>
        /// Prüft ob ein Meisterwerkzeug basierend auf dem aktuellen Spielstand freigeschaltet werden kann.
        /// Schwellen 1:1 zum Original; Inputs via <see cref="MasterToolEligibilityContext"/> statt GameState.
        /// </summary>
        public static bool CheckEligibility(string toolId, MasterToolEligibilityContext ctx)
        {
            return toolId switch
            {
                // Common: Workshop-Meilensteine + Spielaktivität
                "mt_golden_hammer" => ctx.MaxWorkshopLevel >= 75,
                "mt_diamond_saw" => ctx.MaxWorkshopLevel >= 150,
                "mt_titanium_pliers" => ctx.TotalOrdersCompleted >= 150,
                "mt_brass_level" => ctx.TotalMiniGamesPlayed >= 300,
                // Uncommon: Höhere Meilensteine + Prestige
                "mt_silver_wrench" => ctx.MaxWorkshopLevel >= 300,
                "mt_jade_brush" => ctx.PerfectRatings >= 75,
                "mt_crystal_chisel" => ctx.BronzeCount >= 1,
                // Rare: Fortgeschrittene Meilensteine
                "mt_obsidian_drill" => ctx.MaxWorkshopLevel >= 750,
                "mt_ruby_blade" => ctx.SilverCount >= 1,
                // Epic: Endgame-Meilensteine
                "mt_emerald_toolbox" => ctx.MaxWorkshopLevel >= 500,
                "mt_dragon_anvil" => ctx.GoldCount >= 1,
                // Legendary: Alle anderen Tools gesammelt
                "mt_master_crown" => ctx.CollectedMasterToolsCount >= _allDefinitions.Count - 1,
                _ => false
            };
        }

        /// <summary>Berechnet den Gesamt-Einkommensbonus aller gesammelten Meisterwerkzeuge.</summary>
        public static decimal GetTotalIncomeBonus(List<string> collectedIds)
        {
            decimal total = 0m;
            foreach (var def in _allDefinitions)
            {
                if (collectedIds.Contains(def.Id))
                    total += def.IncomeBonus;
            }
            return total;
        }
    }
}
