using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Statische Helfer-Klasse für Meisterwerkzeuge mit passiven Einkommens-Boni.
/// Werden durch Meilensteine, Prestige und Erfolge freigeschaltet.
/// GameState speichert nur die IDs der gesammelten Tools (CollectedMasterTools).
/// </summary>
public static class MasterTool
{
    // ═══════════════════════════════════════════════════════════════════════
    // STATISCHE TOOL-DEFINITIONEN
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly List<MasterToolDefinition> _allDefinitions =
    [
        // Common (4x, +2-3% Einkommen)
        new("mt_golden_hammer", "MasterToolGoldenHammer", "MasterToolGoldenHammerDesc",
            "🔨", MasterToolRarity.Common, 0.02m),
        new("mt_diamond_saw", "MasterToolDiamondSaw", "MasterToolDiamondSawDesc",
            "🪚", MasterToolRarity.Common, 0.02m),
        new("mt_titanium_pliers", "MasterToolTitaniumPliers", "MasterToolTitaniumPliersDesc",
            "🔧", MasterToolRarity.Common, 0.03m),
        new("mt_brass_level", "MasterToolBrassLevel", "MasterToolBrassLevelDesc",
            "📐", MasterToolRarity.Common, 0.03m),

        // Uncommon (3x, +5% Einkommen)
        new("mt_silver_wrench", "MasterToolSilverWrench", "MasterToolSilverWrenchDesc",
            "🔩", MasterToolRarity.Uncommon, 0.05m),
        new("mt_jade_brush", "MasterToolJadeBrush", "MasterToolJadeBrushDesc",
            "🖌️", MasterToolRarity.Uncommon, 0.05m),
        new("mt_crystal_chisel", "MasterToolCrystalChisel", "MasterToolCrystalChiselDesc",
            "⛏️", MasterToolRarity.Uncommon, 0.05m),

        // Rare (2x, +7% Einkommen)
        new("mt_obsidian_drill", "MasterToolObsidianDrill", "MasterToolObsidianDrillDesc",
            "🔩", MasterToolRarity.Rare, 0.07m),
        new("mt_ruby_blade", "MasterToolRubyBlade", "MasterToolRubyBladeDesc",
            "💎", MasterToolRarity.Rare, 0.07m),

        // Epic (2x, +10% Einkommen)
        new("mt_emerald_toolbox", "MasterToolEmeraldToolbox", "MasterToolEmeraldToolboxDesc",
            "🧰", MasterToolRarity.Epic, 0.10m),
        new("mt_dragon_anvil", "MasterToolDragonAnvil", "MasterToolDragonAnvilDesc",
            "⚒️", MasterToolRarity.Epic, 0.10m),

        // Legendary (1x, +15% Einkommen)
        new("mt_master_crown", "MasterToolMasterCrown", "MasterToolMasterCrownDesc",
            "👑", MasterToolRarity.Legendary, 0.15m),
    ];

    /// <summary>
    /// Gibt alle Meisterwerkzeug-Definitionen zurück.
    /// </summary>
    public static List<MasterToolDefinition> GetAllDefinitions() => _allDefinitions;

    /// <summary>
    /// Prüft ob ein Meisterwerkzeug basierend auf dem aktuellen Spielstand freigeschaltet werden kann.
    /// </summary>
    public static bool CheckEligibility(string toolId, GameState state)
    {
        return toolId switch
        {
            // Common: Workshop-Meilensteine + Spielaktivität
            "mt_golden_hammer" => state.Workshops.Any(w => w.Level >= 25),
            "mt_diamond_saw" => state.Workshops.Any(w => w.Level >= 50),
            "mt_titanium_pliers" => state.TotalOrdersCompleted >= 50,
            "mt_brass_level" => state.TotalMiniGamesPlayed >= 100,

            // Uncommon: Höhere Meilensteine + Prestige
            "mt_silver_wrench" => state.Workshops.Any(w => w.Level >= 100),
            "mt_jade_brush" => state.PerfectRatings >= 25,
            "mt_crystal_chisel" => state.Prestige.BronzeCount >= 1,

            // Rare: Fortgeschrittene Meilensteine
            "mt_obsidian_drill" => state.Workshops.Any(w => w.Level >= 250),
            "mt_ruby_blade" => state.Prestige.SilverCount >= 1,

            // Epic: Endgame-Meilensteine
            "mt_emerald_toolbox" => state.Workshops.Any(w => w.Level >= 500),
            "mt_dragon_anvil" => state.Prestige.GoldCount >= 1,

            // Legendary: Alle anderen Tools gesammelt
            "mt_master_crown" => state.CollectedMasterTools.Count >= _allDefinitions.Count - 1,

            _ => false
        };
    }

    /// <summary>
    /// Berechnet den Gesamt-Einkommensbonus aller gesammelten Meisterwerkzeuge.
    /// </summary>
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

/// <summary>
/// Statische Definition eines Meisterwerkzeugs (unveränderlich).
/// </summary>
public record MasterToolDefinition(
    string Id,
    string NameKey,
    string DescriptionKey,
    string Icon,
    MasterToolRarity Rarity,
    decimal IncomeBonus);
