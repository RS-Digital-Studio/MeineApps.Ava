using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

// ═══════════════════════════════════════════════════════════════════════
// FIREBASE-MODELS (Gilden-Achievements)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Firebase-Zustand eines Gilden-Achievements.
/// Pfad: guild_achievements/{guildId}/{achievementId}
/// </summary>
public class GuildAchievementState
{
    /// <summary>Aktueller Fortschritt zum Ziel.</summary>
    [JsonPropertyName("progress")]
    public long Progress { get; set; }

    /// <summary>Ob das Achievement abgeschlossen und eingelöst wurde.</summary>
    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    /// <summary>Wann das Achievement abgeschlossen wurde (UTC ISO 8601).</summary>
    [JsonPropertyName("completedAt")]
    public string CompletedAt { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════════════════
// DEFINITION (statisch, 30 Achievements = 10 Typen × 3 Tiers)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Statische Definition eines Gilden-Achievements.
/// </summary>
public class GuildAchievementDefinition
{
    /// <summary>Eindeutige Achievement-ID (z.B. "guild_ach_money_bronze").</summary>
    public string Id { get; init; } = "";

    /// <summary>Lokalisierungs-Key für den Namen.</summary>
    public string NameKey { get; init; } = "";

    /// <summary>Lokalisierungs-Key für die Beschreibung.</summary>
    public string DescKey { get; init; } = "";

    /// <summary>GameIconKind-Name.</summary>
    public string Icon { get; init; } = "";

    /// <summary>Kategorie des Achievements.</summary>
    public GuildAchievementCategory Category { get; init; }

    /// <summary>Stufe (Bronze/Silver/Gold).</summary>
    public AchievementTier Tier { get; init; }

    /// <summary>Zielwert zum Abschließen.</summary>
    public long Target { get; init; }

    /// <summary>Goldschrauben-Belohnung.</summary>
    public int GoldenScrewReward { get; init; }

    /// <summary>Kosmetik-Belohnung (leer bei Bronze, Banner/Emblem bei Silver/Gold).</summary>
    public string CosmeticReward { get; init; } = "";

    /// <summary>
    /// Kategorie-Farbe für UI-Darstellung.
    /// </summary>
    public static string GetCategoryColor(GuildAchievementCategory category) => category switch
    {
        GuildAchievementCategory.StrongerTogether => "#4CAF50",
        GuildAchievementCategory.WarHeroes => "#DC2626",
        GuildAchievementCategory.DragonSlayers => "#D97706",
        GuildAchievementCategory.Builders => "#2196F3",
        _ => "#888888"
    };

    /// <summary>
    /// Tier-Farbe für UI-Darstellung.
    /// </summary>
    public static string GetTierColor(AchievementTier tier) => tier switch
    {
        AchievementTier.Bronze => "#CD7F32",
        AchievementTier.Silver => "#C0C0C0",
        AchievementTier.Gold => "#FFD700",
        _ => "#888888"
    };

    /// <summary>
    /// Gibt alle 30 Gilden-Achievements zurück (10 Typen × 3 Tiers, gecacht).
    /// </summary>
    private static readonly List<GuildAchievementDefinition> _allDefinitions =
    [
        // ── Gemeinsam stark: Gildengeld ──
        new()
        {
            Id = "guild_ach_money_bronze", NameKey = "GuildAch_Money_Bronze",
            DescKey = "GuildAchDesc_Money_Bronze", Icon = "CurrencyEur",
            Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Bronze,
            Target = 100_000, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_money_silver", NameKey = "GuildAch_Money_Silver",
            DescKey = "GuildAchDesc_Money_Silver", Icon = "CurrencyEur",
            Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Silver,
            Target = 1_000_000, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_money_gold", NameKey = "GuildAch_Money_Gold",
            DescKey = "GuildAchDesc_Money_Gold", Icon = "CurrencyEur",
            Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Gold,
            Target = 10_000_000, GoldenScrewReward = 50
        },

        // ── Gemeinsam stark: Forschungen ──
        new()
        {
            Id = "guild_ach_research_bronze", NameKey = "GuildAch_Research_Bronze",
            DescKey = "GuildAchDesc_Research_Bronze", Icon = "FlaskOutline",
            Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Bronze,
            Target = 3, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_research_silver", NameKey = "GuildAch_Research_Silver",
            DescKey = "GuildAchDesc_Research_Silver", Icon = "FlaskOutline",
            Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Silver,
            Target = 9, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_research_gold", NameKey = "GuildAch_Research_Gold",
            DescKey = "GuildAchDesc_Research_Gold", Icon = "FlaskOutline",
            Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Gold,
            Target = 18, GoldenScrewReward = 50
        },

        // ── Gemeinsam stark: Mitglieder ──
        new()
        {
            Id = "guild_ach_members_bronze", NameKey = "GuildAch_Members_Bronze",
            DescKey = "GuildAchDesc_Members_Bronze", Icon = "AccountGroup",
            Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Bronze,
            Target = 5, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_members_silver", NameKey = "GuildAch_Members_Silver",
            DescKey = "GuildAchDesc_Members_Silver", Icon = "AccountGroup",
            Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Silver,
            Target = 10, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_members_gold", NameKey = "GuildAch_Members_Gold",
            DescKey = "GuildAchDesc_Members_Gold", Icon = "AccountGroup",
            Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Gold,
            Target = 20, GoldenScrewReward = 50
        },

        // ── Kriegshelden: Kriege gewinnen ──
        new()
        {
            Id = "guild_ach_wars_bronze", NameKey = "GuildAch_Wars_Bronze",
            DescKey = "GuildAchDesc_Wars_Bronze", Icon = "SwordCross",
            Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Bronze,
            Target = 3, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_wars_silver", NameKey = "GuildAch_Wars_Silver",
            DescKey = "GuildAchDesc_Wars_Silver", Icon = "SwordCross",
            Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Silver,
            Target = 10, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_wars_gold", NameKey = "GuildAch_Wars_Gold",
            DescKey = "GuildAchDesc_Wars_Gold", Icon = "SwordCross",
            Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Gold,
            Target = 50, GoldenScrewReward = 50
        },

        // ── Kriegshelden: Saisons abschließen ──
        new()
        {
            Id = "guild_ach_seasons_bronze", NameKey = "GuildAch_Seasons_Bronze",
            DescKey = "GuildAchDesc_Seasons_Bronze", Icon = "CalendarStar",
            Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Bronze,
            Target = 1, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_seasons_silver", NameKey = "GuildAch_Seasons_Silver",
            DescKey = "GuildAchDesc_Seasons_Silver", Icon = "CalendarStar",
            Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Silver,
            Target = 4, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_seasons_gold", NameKey = "GuildAch_Seasons_Gold",
            DescKey = "GuildAchDesc_Seasons_Gold", Icon = "CalendarStar",
            Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Gold,
            Target = 12, GoldenScrewReward = 50
        },

        // ── Kriegshelden: Liga erreichen ──
        new()
        {
            Id = "guild_ach_league_bronze", NameKey = "GuildAch_League_Bronze",
            DescKey = "GuildAchDesc_League_Bronze", Icon = "MedalOutline",
            Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Bronze,
            Target = 1, GoldenScrewReward = 5 // Silver Liga = 1
        },
        new()
        {
            Id = "guild_ach_league_silver", NameKey = "GuildAch_League_Silver",
            DescKey = "GuildAchDesc_League_Silver", Icon = "MedalOutline",
            Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Silver,
            Target = 2, GoldenScrewReward = 25 // Gold Liga = 2
        },
        new()
        {
            Id = "guild_ach_league_gold", NameKey = "GuildAch_League_Gold",
            DescKey = "GuildAchDesc_League_Gold", Icon = "MedalOutline",
            Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Gold,
            Target = 3, GoldenScrewReward = 50 // Diamond Liga = 3
        },

        // ── Drachentöter: Bosse besiegen ──
        new()
        {
            Id = "guild_ach_boss_bronze", NameKey = "GuildAch_Boss_Bronze",
            DescKey = "GuildAchDesc_Boss_Bronze", Icon = "Skull",
            Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Bronze,
            Target = 3, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_boss_silver", NameKey = "GuildAch_Boss_Silver",
            DescKey = "GuildAchDesc_Boss_Silver", Icon = "Skull",
            Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Silver,
            Target = 10, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_boss_gold", NameKey = "GuildAch_Boss_Gold",
            DescKey = "GuildAchDesc_Boss_Gold", Icon = "Skull",
            Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Gold,
            Target = 50, GoldenScrewReward = 50
        },

        // ── Drachentöter: Boss-MVP ──
        new()
        {
            Id = "guild_ach_mvp_bronze", NameKey = "GuildAch_Mvp_Bronze",
            DescKey = "GuildAchDesc_Mvp_Bronze", Icon = "StarShooting",
            Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Bronze,
            Target = 1, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_mvp_silver", NameKey = "GuildAch_Mvp_Silver",
            DescKey = "GuildAchDesc_Mvp_Silver", Icon = "StarShooting",
            Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Silver,
            Target = 5, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_mvp_gold", NameKey = "GuildAch_Mvp_Gold",
            DescKey = "GuildAchDesc_Mvp_Gold", Icon = "StarShooting",
            Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Gold,
            Target = 20, GoldenScrewReward = 50
        },

        // ── Drachentöter: Boss unter 24h besiegen ──
        new()
        {
            Id = "guild_ach_speedkill_bronze", NameKey = "GuildAch_Speedkill_Bronze",
            DescKey = "GuildAchDesc_Speedkill_Bronze", Icon = "TimerOutline",
            Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Bronze,
            Target = 1, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_speedkill_silver", NameKey = "GuildAch_Speedkill_Silver",
            DescKey = "GuildAchDesc_Speedkill_Silver", Icon = "TimerOutline",
            Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Silver,
            Target = 3, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_speedkill_gold", NameKey = "GuildAch_Speedkill_Gold",
            DescKey = "GuildAchDesc_Speedkill_Gold", Icon = "TimerOutline",
            Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Gold,
            Target = 10, GoldenScrewReward = 50
        },

        // ── Baumeister: Gebäude auf Max-Level ──
        new()
        {
            Id = "guild_ach_maxbuilding_bronze", NameKey = "GuildAch_MaxBuilding_Bronze",
            DescKey = "GuildAchDesc_MaxBuilding_Bronze", Icon = "OfficeBuilding",
            Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Bronze,
            Target = 1, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_maxbuilding_silver", NameKey = "GuildAch_MaxBuilding_Silver",
            DescKey = "GuildAchDesc_MaxBuilding_Silver", Icon = "OfficeBuilding",
            Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Silver,
            Target = 5, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_maxbuilding_gold", NameKey = "GuildAch_MaxBuilding_Gold",
            DescKey = "GuildAchDesc_MaxBuilding_Gold", Icon = "OfficeBuilding",
            Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Gold,
            Target = 10, GoldenScrewReward = 50
        },

        // ── Baumeister: Hallen-Level (3 Tiers, max Hall Level = 10) ──
        // Bronze: Hallen-Level 3, Silver: Hallen-Level 6, Gold: Hallen-Level 10
        new()
        {
            Id = "guild_ach_hall_bronze", NameKey = "GuildAch_Hall_Bronze",
            DescKey = "GuildAchDesc_Hall_Bronze", Icon = "HomeCity",
            Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Bronze,
            Target = 3, GoldenScrewReward = 5
        },
        new()
        {
            Id = "guild_ach_hall_silver", NameKey = "GuildAch_Hall_Silver",
            DescKey = "GuildAchDesc_Hall_Silver", Icon = "HomeCity",
            Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Silver,
            Target = 6, GoldenScrewReward = 25
        },
        new()
        {
            Id = "guild_ach_hall_gold", NameKey = "GuildAch_Hall_Gold",
            DescKey = "GuildAchDesc_Hall_Gold", Icon = "HomeCity",
            Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Gold,
            Target = 10, GoldenScrewReward = 50
        }
    ];

    public static List<GuildAchievementDefinition> GetAll() => _allDefinitions;
}

// ═══════════════════════════════════════════════════════════════════════
// DISPLAY (UI-Daten für ViewModel)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Aufbereitete Anzeige-Daten für ein Gilden-Achievement.
/// </summary>
public class GuildAchievementDisplay
{
    /// <summary>Achievement-ID.</summary>
    public string Id { get; set; } = "";

    /// <summary>Lokalisierter Name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Lokalisierte Beschreibung.</summary>
    public string Description { get; set; } = "";

    /// <summary>Kategorie.</summary>
    public GuildAchievementCategory Category { get; set; }

    /// <summary>Stufe (Bronze/Silver/Gold).</summary>
    public AchievementTier Tier { get; set; }

    /// <summary>Zielwert.</summary>
    public long Target { get; set; }

    /// <summary>Aktueller Fortschritt.</summary>
    public long Progress { get; set; }

    /// <summary>Ob abgeschlossen.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Goldschrauben-Belohnung.</summary>
    public int GoldenScrewReward { get; set; }

    /// <summary>Kosmetik-Belohnung (leer wenn keine).</summary>
    public string CosmeticReward { get; set; } = "";

    /// <summary>GameIconKind-Name.</summary>
    public string Icon { get; set; } = "";

    /// <summary>Fortschritt in Prozent (0.0 - 1.0).</summary>
    [JsonIgnore]
    public double ProgressPercent => Target > 0
        ? Math.Clamp((double)Progress / Target, 0.0, 1.0) : 0.0;

    /// <summary>Tier-Farbe für UI-Darstellung.</summary>
    [JsonIgnore]
    public string TierColor => Tier switch
    {
        AchievementTier.Bronze => "#CD7F32",
        AchievementTier.Silver => "#C0C0C0",
        AchievementTier.Gold => "#FFD700",
        _ => "#888888"
    };

    /// <summary>Fortschritts-Anzeige (z.B. "42 / 100").</summary>
    [JsonIgnore]
    public string ProgressDisplay => $"{Progress:N0} / {Target:N0}";

    /// <summary>Belohnungs-Anzeige (Goldschrauben + optionale Kosmetik).</summary>
    [JsonIgnore]
    public string RewardDisplay
    {
        get
        {
            var gs = GoldenScrewReward > 0 ? $"{GoldenScrewReward} GS" : "";
            var cosmetic = !string.IsNullOrEmpty(CosmeticReward) ? CosmeticReward : "";
            if (gs.Length > 0 && cosmetic.Length > 0)
                return $"{gs} + {cosmetic}";
            return gs.Length > 0 ? gs : cosmetic;
        }
    }
}
