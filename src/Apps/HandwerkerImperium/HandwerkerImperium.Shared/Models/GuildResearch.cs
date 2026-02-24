using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

// ═══════════════════════════════════════════════════════════════════════
// ENUMS
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Kategorie einer Gilden-Forschung.
/// </summary>
public enum GuildResearchCategory
{
    Infrastructure,
    Economy,
    Knowledge,
    Logistics,
    Workforce,
    Mastery
}

/// <summary>
/// Art des Effekts einer Gilden-Forschung.
/// </summary>
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

// ═══════════════════════════════════════════════════════════════════════
// DEFINITION (statisch, 18 Forschungen)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Statische Definition einer Gilden-Forschung.
/// Wird nicht persistiert - kommt aus GetAll().
/// </summary>
public class GuildResearchDefinition
{
    public string Id { get; init; } = "";
    public string NameKey { get; init; } = "";
    public string DescKey { get; init; } = "";
    public string Icon { get; init; } = "FlaskOutline";
    public long Cost { get; init; }
    public int Order { get; init; }
    public GuildResearchCategory Category { get; init; }
    public GuildResearchEffectType EffectType { get; init; }
    public decimal EffectValue { get; init; }

    /// <summary>
    /// Gibt alle 18 Gilden-Forschungen in 6 Kategorien zurück.
    /// </summary>
    public static List<GuildResearchDefinition> GetAll() =>
    [
        // ── Infrastruktur (Gilden-Ausbau) ──
        new()
        {
            Id = "guild_expand_1", NameKey = "GuildResearch_guild_expand_1",
            DescKey = "GuildResearchDesc_guild_expand_1", Icon = "AccountMultiplePlus",
            Cost = 50_000_000, Order = 1, Category = GuildResearchCategory.Infrastructure,
            EffectType = GuildResearchEffectType.MaxMembers, EffectValue = 5
        },
        new()
        {
            Id = "guild_expand_2", NameKey = "GuildResearch_guild_expand_2",
            DescKey = "GuildResearchDesc_guild_expand_2", Icon = "AccountMultiplePlus",
            Cost = 500_000_000, Order = 2, Category = GuildResearchCategory.Infrastructure,
            EffectType = GuildResearchEffectType.MaxMembers, EffectValue = 5
        },
        new()
        {
            Id = "guild_expand_3", NameKey = "GuildResearch_guild_expand_3",
            DescKey = "GuildResearchDesc_guild_expand_3", Icon = "AccountMultiplePlus",
            Cost = 5_000_000_000, Order = 3, Category = GuildResearchCategory.Infrastructure,
            EffectType = GuildResearchEffectType.MaxMembers, EffectValue = 10
        },

        // ── Wirtschaft (Einkommens-Boni) ──
        new()
        {
            Id = "guild_income_1", NameKey = "GuildResearch_guild_income_1",
            DescKey = "GuildResearchDesc_guild_income_1", Icon = "Handshake",
            Cost = 10_000_000, Order = 1, Category = GuildResearchCategory.Economy,
            EffectType = GuildResearchEffectType.IncomeBonus, EffectValue = 0.05m
        },
        new()
        {
            Id = "guild_income_2", NameKey = "GuildResearch_guild_income_2",
            DescKey = "GuildResearchDesc_guild_income_2", Icon = "CartArrowDown",
            Cost = 100_000_000, Order = 2, Category = GuildResearchCategory.Economy,
            EffectType = GuildResearchEffectType.CostReduction, EffectValue = 0.10m
        },
        new()
        {
            Id = "guild_income_3", NameKey = "GuildResearch_guild_income_3",
            DescKey = "GuildResearchDesc_guild_income_3", Icon = "TruckDelivery",
            Cost = 1_000_000_000, Order = 3, Category = GuildResearchCategory.Economy,
            EffectType = GuildResearchEffectType.RewardBonus, EffectValue = 0.10m
        },
        new()
        {
            Id = "guild_income_4", NameKey = "GuildResearch_guild_income_4",
            DescKey = "GuildResearchDesc_guild_income_4", Icon = "CurrencyEur",
            Cost = 10_000_000_000, Order = 4, Category = GuildResearchCategory.Economy,
            EffectType = GuildResearchEffectType.IncomeBonus, EffectValue = 0.15m
        },

        // ── Wissen (XP + Effizienz) ──
        new()
        {
            Id = "guild_knowledge_1", NameKey = "GuildResearch_guild_knowledge_1",
            DescKey = "GuildResearchDesc_guild_knowledge_1", Icon = "BookOpenVariant",
            Cost = 25_000_000, Order = 1, Category = GuildResearchCategory.Knowledge,
            EffectType = GuildResearchEffectType.XpBonus, EffectValue = 0.10m
        },
        new()
        {
            Id = "guild_knowledge_2", NameKey = "GuildResearch_guild_knowledge_2",
            DescKey = "GuildResearchDesc_guild_knowledge_2", Icon = "Cog",
            Cost = 250_000_000, Order = 2, Category = GuildResearchCategory.Knowledge,
            EffectType = GuildResearchEffectType.EfficiencyBonus, EffectValue = 0.05m
        },
        new()
        {
            Id = "guild_knowledge_3", NameKey = "GuildResearch_guild_knowledge_3",
            DescKey = "GuildResearchDesc_guild_knowledge_3", Icon = "SchoolOutline",
            Cost = 2_500_000_000, Order = 3, Category = GuildResearchCategory.Knowledge,
            EffectType = GuildResearchEffectType.MiniGameBonus, EffectValue = 0.15m
        },

        // ── Logistik (Aufträge) ──
        new()
        {
            Id = "guild_logistics_1", NameKey = "GuildResearch_guild_logistics_1",
            DescKey = "GuildResearchDesc_guild_logistics_1", Icon = "ClipboardTextMultiple",
            Cost = 75_000_000, Order = 1, Category = GuildResearchCategory.Logistics,
            EffectType = GuildResearchEffectType.OrderSlotBonus, EffectValue = 1
        },
        new()
        {
            Id = "guild_logistics_2", NameKey = "GuildResearch_guild_logistics_2",
            DescKey = "GuildResearchDesc_guild_logistics_2", Icon = "AccountTie",
            Cost = 750_000_000, Order = 2, Category = GuildResearchCategory.Logistics,
            EffectType = GuildResearchEffectType.OrderQualityBonus, EffectValue = 0.15m
        },
        new()
        {
            Id = "guild_logistics_3", NameKey = "GuildResearch_guild_logistics_3",
            DescKey = "GuildResearchDesc_guild_logistics_3", Icon = "RocketLaunch",
            Cost = 3_000_000_000, Order = 3, Category = GuildResearchCategory.Logistics,
            EffectType = GuildResearchEffectType.RewardBonus, EffectValue = 0.20m
        },

        // ── Arbeitsmarkt (Workers) ──
        new()
        {
            Id = "guild_workforce_1", NameKey = "GuildResearch_guild_workforce_1",
            DescKey = "GuildResearchDesc_guild_workforce_1", Icon = "DomainPlus",
            Cost = 150_000_000, Order = 1, Category = GuildResearchCategory.Workforce,
            EffectType = GuildResearchEffectType.WorkerSlotBonus, EffectValue = 1
        },
        new()
        {
            Id = "guild_workforce_2", NameKey = "GuildResearch_guild_workforce_2",
            DescKey = "GuildResearchDesc_guild_workforce_2", Icon = "HumanMaleBoard",
            Cost = 1_000_000_000, Order = 2, Category = GuildResearchCategory.Workforce,
            EffectType = GuildResearchEffectType.TrainingSpeedBonus, EffectValue = 0.25m
        },
        new()
        {
            Id = "guild_workforce_3", NameKey = "GuildResearch_guild_workforce_3",
            DescKey = "GuildResearchDesc_guild_workforce_3", Icon = "ShieldAccount",
            Cost = 5_000_000_000, Order = 3, Category = GuildResearchCategory.Workforce,
            EffectType = GuildResearchEffectType.FatigueReduction, EffectValue = 0.20m
        },

        // ── Meisterschaft (Endgame) ──
        new()
        {
            Id = "guild_mastery_1", NameKey = "GuildResearch_guild_mastery_1",
            DescKey = "GuildResearchDesc_guild_mastery_1", Icon = "FlashOutline",
            Cost = 500_000_000, Order = 1, Category = GuildResearchCategory.Mastery,
            EffectType = GuildResearchEffectType.ResearchSpeedBonus, EffectValue = 0.20m
        },
        new()
        {
            Id = "guild_mastery_2", NameKey = "GuildResearch_guild_mastery_2",
            DescKey = "GuildResearchDesc_guild_mastery_2", Icon = "Crown",
            Cost = 7_500_000_000, Order = 2, Category = GuildResearchCategory.Mastery,
            EffectType = GuildResearchEffectType.PrestigePointBonus, EffectValue = 0.10m
        }
    ];

    /// <summary>
    /// Kategorie-Farbe für UI-Darstellung.
    /// </summary>
    public static string GetCategoryColor(GuildResearchCategory category) => category switch
    {
        GuildResearchCategory.Infrastructure => "#D97706",
        GuildResearchCategory.Economy => "#4CAF50",
        GuildResearchCategory.Knowledge => "#2196F3",
        GuildResearchCategory.Logistics => "#9C27B0",
        GuildResearchCategory.Workforce => "#0E7490",
        GuildResearchCategory.Mastery => "#FFD700",
        _ => "#888888"
    };

    /// <summary>
    /// Kategorie-NameKey für Lokalisierung.
    /// </summary>
    public static string GetCategoryNameKey(GuildResearchCategory category) => category switch
    {
        GuildResearchCategory.Infrastructure => "GuildResearchCatInfrastructure",
        GuildResearchCategory.Economy => "GuildResearchCatEconomy",
        GuildResearchCategory.Knowledge => "GuildResearchCatKnowledge",
        GuildResearchCategory.Logistics => "GuildResearchCatLogistics",
        GuildResearchCategory.Workforce => "GuildResearchCatWorkforce",
        GuildResearchCategory.Mastery => "GuildResearchCatMastery",
        _ => "GuildResearchCatInfrastructure"
    };
}

// ═══════════════════════════════════════════════════════════════════════
// STATE (Firebase-Daten pro Forschung)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Firebase-Zustand einer einzelnen Gilden-Forschung.
/// Pfad: /guild_research/{guildId}/{researchId}
/// </summary>
public class GuildResearchState
{
    [JsonPropertyName("progress")]
    public long Progress { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("completedAt")]
    public string? CompletedAt { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════
// DISPLAY (UI-Daten für ViewModel/View)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// UI-Anzeige-Daten einer Gilden-Forschung.
/// Kombiniert statische Definition mit Firebase-Zustand.
/// </summary>
public class GuildResearchDisplay
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "FlaskOutline";
    public long Cost { get; set; }
    public long Progress { get; set; }
    public GuildResearchCategory Category { get; set; }
    public GuildResearchEffectType EffectType { get; set; }
    public decimal EffectValue { get; set; }
    public string CategoryColor { get; set; } = "#888888";

    public double ProgressPercent => Cost > 0
        ? Math.Clamp((double)Progress / Cost, 0.0, 1.0) : 0.0;

    public bool IsCompleted { get; set; }

    /// <summary>Erste nicht-abgeschlossene Forschung pro Kategorie = aktiv.</summary>
    public bool IsActive { get; set; }

    /// <summary>Nicht aktiv und nicht abgeschlossen = gesperrt.</summary>
    public bool IsLocked => !IsCompleted && !IsActive;
}

// ═══════════════════════════════════════════════════════════════════════
// EFFECTS (berechnete Gesamteffekte aller abgeschlossenen Forschungen)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Berechnete Gesamteffekte aller abgeschlossenen Gilden-Forschungen.
/// Wird vom GuildService gecacht und im GameLoop abgefragt.
/// </summary>
public class GuildResearchEffects
{
    /// <summary>+20% Einkommen (guild_income_1 5% + guild_income_4 15%)</summary>
    public decimal IncomeBonus { get; set; }

    /// <summary>-10% Kosten (guild_income_2)</summary>
    public decimal CostReduction { get; set; }

    /// <summary>+30% Auftragsbelohnungen (guild_income_3 10% + guild_logistics_3 20%)</summary>
    public decimal RewardBonus { get; set; }

    /// <summary>+10% XP (guild_knowledge_1)</summary>
    public decimal XpBonus { get; set; }

    /// <summary>+5% Worker-Effizienz (guild_knowledge_2)</summary>
    public decimal EfficiencyBonus { get; set; }

    /// <summary>+15% MiniGame-Belohnungen (guild_knowledge_3)</summary>
    public decimal MiniGameBonus { get; set; }

    /// <summary>+20 Max-Mitglieder (guild_expand_1..3: 5+5+10)</summary>
    public int MaxMembersBonus { get; set; }

    /// <summary>+1 Auftragsslot (guild_logistics_1)</summary>
    public int OrderSlotBonus { get; set; }

    /// <summary>+15% Chance auf bessere Aufträge (guild_logistics_2)</summary>
    public decimal OrderQualityBonus { get; set; }

    /// <summary>+1 Worker-Slot pro Workshop (guild_workforce_1)</summary>
    public int WorkerSlotBonus { get; set; }

    /// <summary>+25% Training-Geschwindigkeit (guild_workforce_2)</summary>
    public decimal TrainingSpeedBonus { get; set; }

    /// <summary>-20% Ermüdungs-/Stimmungs-Abbau (guild_workforce_3)</summary>
    public decimal FatigueReduction { get; set; }

    /// <summary>+20% Forschungs-Geschwindigkeit (guild_mastery_1)</summary>
    public decimal ResearchSpeedBonus { get; set; }

    /// <summary>+10% Prestige-Punkte (guild_mastery_2)</summary>
    public decimal PrestigePointBonus { get; set; }

    /// <summary>
    /// Berechnet Gesamteffekte aus einer Liste abgeschlossener Forschungs-IDs.
    /// </summary>
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
