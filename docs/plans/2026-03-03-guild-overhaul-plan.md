# HandwerkerImperium - Gilden AAA-Überarbeitung - Implementierungsplan

> **Für Claude:** PFLICHT SUB-SKILL: Nutze superpowers:executing-plans um diesen Plan Task für Task umzusetzen.

**Ziel:** Komplette Gilden-Überarbeitung mit modularer Service-Architektur, Saison-Ligen-System, kooperativen Bossen, interaktivem Hauptquartier, 6 neuen SkiaSharp-Renderern, kontextuellen Tipps und 30 Achievements.

**Architektur:** God-Service (GuildService 1139 Zeilen) wird in 8 modulare Services aufgeteilt. Firebase-Datenstruktur wird um Saisons, Bosse, Hauptquartier und Achievements erweitert. GuildViewModel delegiert an Sub-ViewModels. 6 neue SkiaSharp-Renderer mit Offscreen-Caching und Struct-Pools.

**Tech Stack:** Avalonia 11.3, .NET 10, SkiaSharp 3.119.2, CommunityToolkit.Mvvm, Firebase Realtime Database (REST), sqlite-net-pcl

**Projekt-Pfad:** `src/Apps/HandwerkerImperium/HandwerkerImperium.Shared/`

**Abkürzungen:** HI = HandwerkerImperium, GS = Goldschrauben

---

## Übersicht: 14 Tasks in 4 Phasen

| Phase | Tasks | Beschreibung |
|-------|-------|-------------|
| **A: Models & Firebase** | 1-3 | Neue Datenmodelle, Firebase-Pfade, erweiterte bestehende Models |
| **B: Services** | 4-9 | Service-Refactoring + 5 neue Services |
| **C: ViewModel & UI** | 10-12 | ViewModel-Refactoring, Views, RESX |
| **D: Renderer** | 13-14 | Bestehende Fixes + 6 neue SkiaSharp-Renderer |

---

## Phase A: Models & Firebase-Datenstruktur

### Task 1: Erweiterte Firebase-Models & Enums

**Dateien:**
- Erstellen: `Models/GuildEnums.cs`
- Erstellen: `Models/GuildBoss.cs`
- Erstellen: `Models/GuildHall.cs`
- Erstellen: `Models/GuildAchievement.cs`
- Erstellen: `Models/GuildWarSeason.cs`
- Ändern: `Models/Firebase/FirebaseGuildData.cs`
- Ändern: `Models/Firebase/GuildWar.cs`
- Ändern: `Models/Guild.cs`

**Schritt 1: GuildEnums.cs erstellen**

Alle Gilden-Enums zentral sammeln (statt verstreut in verschiedenen Dateien):

```csharp
namespace HandwerkerImperium.Models;

/// <summary>Gilden-Rollen mit 3 Stufen</summary>
public enum GuildRole
{
    Member,
    Officer,
    Leader
}

/// <summary>Liga-Stufen für das Saison-System</summary>
public enum GuildLeague
{
    Bronze,
    Silver,
    Gold,
    Diamond
}

/// <summary>Kriegs-Phasen innerhalb einer Woche</summary>
public enum WarPhase
{
    Attack,
    Defense,
    Evaluation,
    Completed
}

/// <summary>Boss-Zustand</summary>
public enum BossStatus
{
    Active,
    Defeated,
    Expired
}

/// <summary>Gebäude-IDs im Hauptquartier</summary>
public enum GuildBuildingId
{
    Workshop,           // Werkstatt - Crafting-Speed
    ResearchLab,        // Forschungslabor - Forschungszeit
    TradingPost,        // Handelskontor - Einkommen
    Smithy,             // Schmiede - Auftragsbelohnung
    Watchtower,         // Wachturm - War-Punkte
    AssemblyHall,       // Versammlungshalle - Max-Mitglieder
    Treasury,           // Schatzkammer - Wochenziel-Belohnung
    Fortress,           // Festungsmauer - Verteidigungsbonus
    TrophyHall,         // Trophäenhalle - Achievements anzeigen
    MasterThrone         // Meisterthron - Capstone +5% alles
}

/// <summary>Boss-Typen (6 rotierende Bosse)</summary>
public enum GuildBossType
{
    StoneGolem,         // Steingolem - Standard
    IronTitan,          // Eisentitan - Crafting 2x
    MasterArchitect,    // Meisterarchitekt - Aufträge 2x
    RustDragon,         // Rostdrache - Mini-Games 2x
    ShadowTrader,       // Schattenhändler - Geldspenden 3x
    ClockworkColossus   // Uhrwerk-Koloss - 24h, alle 1.5x
}

/// <summary>Achievement-Kategorien</summary>
public enum GuildAchievementCategory
{
    StrongerTogether,   // Gemeinsam stark
    WarHeroes,          // Kriegshelden
    DragonSlayers,      // Drachentöter
    Builders            // Baumeister
}

/// <summary>Achievement-Tier</summary>
public enum AchievementTier
{
    Bronze,
    Silver,
    Gold
}
```

**Schritt 2: GuildWarSeason.cs erstellen**

```csharp
namespace HandwerkerImperium.Models;

using System.Text.Json.Serialization;

/// <summary>Firebase: guild_war_seasons/{seasonId}/</summary>
public class GuildWarSeasonData
{
    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = "";

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("week")]
    public int Week { get; set; } = 1;
}

/// <summary>Firebase: guild_war_seasons/{seasonId}/leagues/{leagueId}/{guildId}/</summary>
public class GuildLeagueEntry
{
    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("wins")]
    public int Wins { get; set; }

    [JsonPropertyName("losses")]
    public int Losses { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }
}

/// <summary>Firebase: guild_war_scores/{warId}/{guildId}/{uid}/</summary>
public class GuildWarPlayerScore
{
    [JsonPropertyName("attackScore")]
    public long AttackScore { get; set; }

    [JsonPropertyName("defenseScore")]
    public long DefenseScore { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";

    [JsonIgnore]
    public long TotalScore => AttackScore + DefenseScore;
}

/// <summary>Firebase: guild_war_log/{warId}/{pushKey}/</summary>
public class GuildWarLogEntry
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "score"; // "score" | "milestone" | "phase_change"

    [JsonPropertyName("guildId")]
    public string GuildId { get; set; } = "";

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = "";

    [JsonPropertyName("points")]
    public long Points { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

/// <summary>Tägliche Bonus-Mission im Gildenkrieg</summary>
public class WarBonusMission
{
    public string Id { get; init; } = "";
    public string NameKey { get; init; } = "";
    public string DescKey { get; init; } = "";
    public int Target { get; init; }
    public int Progress { get; set; }
    public int BonusPoints { get; init; }
    public bool IsCompleted => Progress >= Target;
}

/// <summary>Anzeige-Daten für den Gildenkrieg-Dashboard</summary>
public class WarSeasonDisplayData
{
    public string SeasonId { get; set; } = "";
    public int SeasonNumber { get; set; }
    public int WeekNumber { get; set; }
    public GuildLeague OwnLeague { get; set; } = GuildLeague.Bronze;
    public GuildLeagueEntry? OwnLeagueEntry { get; set; }

    // Aktueller Krieg
    public string? WarId { get; set; }
    public string OpponentName { get; set; } = "";
    public int OpponentLevel { get; set; }
    public long OwnScore { get; set; }
    public long OpponentScore { get; set; }
    public WarPhase CurrentPhase { get; set; } = WarPhase.Attack;
    public DateTime PhaseEndsAt { get; set; }
    public bool IsLeading => OwnScore > OpponentScore;
    public bool IsByeWeek => string.IsNullOrEmpty(WarId);

    // Bonus-Missionen
    public List<WarBonusMission> BonusMissions { get; set; } = [];

    // MVP
    public string MvpName { get; set; } = "";
    public long MvpScore { get; set; }
}
```

**Schritt 3: GuildBoss.cs erstellen**

```csharp
namespace HandwerkerImperium.Models;

using System.Text.Json.Serialization;

/// <summary>Firebase: guild_bosses/{guildId}/</summary>
public class FirebaseGuildBoss
{
    [JsonPropertyName("bossId")]
    public string BossId { get; set; } = "";

    [JsonPropertyName("bossHp")]
    public long BossHp { get; set; }

    [JsonPropertyName("currentHp")]
    public long CurrentHp { get; set; }

    [JsonPropertyName("bossLevel")]
    public int BossLevel { get; set; }

    [JsonPropertyName("startedAt")]
    public string StartedAt { get; set; } = "";

    [JsonPropertyName("expiresAt")]
    public string ExpiresAt { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";
}

/// <summary>Firebase: guild_boss_damage/{guildId}/{uid}/</summary>
public class GuildBossDamage
{
    [JsonPropertyName("totalDamage")]
    public long TotalDamage { get; set; }

    [JsonPropertyName("hits")]
    public int Hits { get; set; }

    [JsonPropertyName("lastHitAt")]
    public string LastHitAt { get; set; } = "";
}

/// <summary>Statische Boss-Definition</summary>
public class GuildBossDefinition
{
    public GuildBossType Type { get; init; }
    public string NameKey { get; init; } = "";
    public string DescKey { get; init; } = "";
    public int HpPerLevel { get; init; }
    public int DurationHours { get; init; } = 48;
    public float DamageMultiplierCrafting { get; init; } = 1f;
    public float DamageMultiplierOrders { get; init; } = 1f;
    public float DamageMultiplierMiniGames { get; init; } = 1f;
    public float DamageMultiplierDonations { get; init; } = 1f;
    public float DamageMultiplierAll { get; init; } = 1f;

    public long CalculateHp(int guildLevel) => (long)HpPerLevel * Math.Max(1, guildLevel);

    public static List<GuildBossDefinition> GetAll() =>
    [
        new() { Type = GuildBossType.StoneGolem, NameKey = "GuildBoss_StoneGolem", DescKey = "GuildBossDesc_StoneGolem",
                HpPerLevel = 5_000 },
        new() { Type = GuildBossType.IronTitan, NameKey = "GuildBoss_IronTitan", DescKey = "GuildBossDesc_IronTitan",
                HpPerLevel = 7_500, DamageMultiplierCrafting = 2f },
        new() { Type = GuildBossType.MasterArchitect, NameKey = "GuildBoss_MasterArchitect", DescKey = "GuildBossDesc_MasterArchitect",
                HpPerLevel = 6_000, DamageMultiplierOrders = 2f },
        new() { Type = GuildBossType.RustDragon, NameKey = "GuildBoss_RustDragon", DescKey = "GuildBossDesc_RustDragon",
                HpPerLevel = 8_000, DamageMultiplierMiniGames = 2f },
        new() { Type = GuildBossType.ShadowTrader, NameKey = "GuildBoss_ShadowTrader", DescKey = "GuildBossDesc_ShadowTrader",
                HpPerLevel = 5_500, DamageMultiplierDonations = 3f },
        new() { Type = GuildBossType.ClockworkColossus, NameKey = "GuildBoss_ClockworkColossus", DescKey = "GuildBossDesc_ClockworkColossus",
                HpPerLevel = 10_000, DurationHours = 24, DamageMultiplierAll = 1.5f },
    ];
}

/// <summary>Anzeige-Daten für den Boss-Renderer</summary>
public class GuildBossDisplayData
{
    public GuildBossType BossType { get; set; }
    public string BossName { get; set; } = "";
    public string BossDescription { get; set; } = "";
    public long MaxHp { get; set; }
    public long CurrentHp { get; set; }
    public DateTime ExpiresAt { get; set; }
    public BossStatus Status { get; set; } = BossStatus.Active;

    public float HpPercent => MaxHp > 0 ? (float)CurrentHp / MaxHp : 0f;
    public TimeSpan TimeRemaining => ExpiresAt > DateTime.UtcNow ? ExpiresAt - DateTime.UtcNow : TimeSpan.Zero;

    // Eigener Beitrag
    public long OwnDamage { get; set; }
    public int OwnHits { get; set; }
    public int OwnRank { get; set; }

    // Top-Schadensverursacher
    public List<BossDamageEntry> Leaderboard { get; set; } = [];
}

public class BossDamageEntry
{
    public string PlayerName { get; set; } = "";
    public long Damage { get; set; }
    public int Hits { get; set; }
}
```

**Schritt 4: GuildHall.cs erstellen**

```csharp
namespace HandwerkerImperium.Models;

using System.Text.Json.Serialization;

/// <summary>Firebase: guild_hall/{guildId}/buildings/{buildingId}/</summary>
public class GuildBuildingState
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("upgradingUntil")]
    public string? UpgradingUntil { get; set; }

    [JsonPropertyName("unlockedAt")]
    public string? UnlockedAt { get; set; }

    [JsonIgnore]
    public bool IsUpgrading => !string.IsNullOrEmpty(UpgradingUntil) &&
        DateTime.TryParse(UpgradingUntil, null, System.Globalization.DateTimeStyles.RoundtripKind, out var until) &&
        until > DateTime.UtcNow;
}

/// <summary>Statische Gebäude-Definition</summary>
public class GuildBuildingDefinition
{
    public GuildBuildingId Id { get; init; }
    public string NameKey { get; init; } = "";
    public string DescKey { get; init; } = "";
    public string EffectDescKey { get; init; } = "";
    public int MaxLevel { get; init; }
    public int UnlockHallLevel { get; init; }
    public decimal EffectPerLevel { get; init; }

    /// <summary>Kosten pro Level: [0]=Lv1, [1]=Lv2, etc.</summary>
    public GuildBuildingCost[] Costs { get; init; } = [];

    public static List<GuildBuildingDefinition> GetAll() =>
    [
        new()
        {
            Id = GuildBuildingId.Workshop, NameKey = "GuildHall_Workshop", DescKey = "GuildHallDesc_Workshop",
            EffectDescKey = "GuildHallEffect_CraftingSpeed", MaxLevel = 5, UnlockHallLevel = 1,
            EffectPerLevel = 0.02m,
            Costs = [
                new(5, 50_000), new(10, 150_000), new(20, 400_000), new(35, 800_000), new(50, 1_500_000)
            ]
        },
        new()
        {
            Id = GuildBuildingId.ResearchLab, NameKey = "GuildHall_ResearchLab", DescKey = "GuildHallDesc_ResearchLab",
            EffectDescKey = "GuildHallEffect_ResearchTime", MaxLevel = 5, UnlockHallLevel = 2,
            EffectPerLevel = 0.05m,
            Costs = [
                new(8, 80_000), new(15, 200_000), new(25, 500_000), new(40, 1_000_000), new(60, 2_000_000)
            ]
        },
        new()
        {
            Id = GuildBuildingId.TradingPost, NameKey = "GuildHall_TradingPost", DescKey = "GuildHallDesc_TradingPost",
            EffectDescKey = "GuildHallEffect_Income", MaxLevel = 5, UnlockHallLevel = 3,
            EffectPerLevel = 0.03m,
            Costs = [
                new(10, 100_000), new(18, 250_000), new(30, 600_000), new(45, 1_200_000), new(65, 2_500_000)
            ]
        },
        new()
        {
            Id = GuildBuildingId.Smithy, NameKey = "GuildHall_Smithy", DescKey = "GuildHallDesc_Smithy",
            EffectDescKey = "GuildHallEffect_OrderReward", MaxLevel = 5, UnlockHallLevel = 4,
            EffectPerLevel = 0.02m,
            Costs = [
                new(12, 120_000), new(20, 300_000), new(35, 700_000), new(50, 1_500_000), new(70, 3_000_000)
            ]
        },
        new()
        {
            Id = GuildBuildingId.Watchtower, NameKey = "GuildHall_Watchtower", DescKey = "GuildHallDesc_Watchtower",
            EffectDescKey = "GuildHallEffect_WarPoints", MaxLevel = 5, UnlockHallLevel = 5,
            EffectPerLevel = 0.05m,
            Costs = [
                new(15, 150_000), new(25, 400_000), new(40, 900_000), new(60, 1_800_000), new(80, 3_500_000)
            ]
        },
        new()
        {
            Id = GuildBuildingId.AssemblyHall, NameKey = "GuildHall_AssemblyHall", DescKey = "GuildHallDesc_AssemblyHall",
            EffectDescKey = "GuildHallEffect_MaxMembers", MaxLevel = 3, UnlockHallLevel = 6,
            EffectPerLevel = 2m,
            Costs = [
                new(20, 300_000), new(40, 800_000), new(80, 2_000_000)
            ]
        },
        new()
        {
            Id = GuildBuildingId.Treasury, NameKey = "GuildHall_Treasury", DescKey = "GuildHallDesc_Treasury",
            EffectDescKey = "GuildHallEffect_WeeklyReward", MaxLevel = 3, UnlockHallLevel = 7,
            EffectPerLevel = 0.05m,
            Costs = [
                new(25, 400_000), new(50, 1_000_000), new(100, 2_500_000)
            ]
        },
        new()
        {
            Id = GuildBuildingId.Fortress, NameKey = "GuildHall_Fortress", DescKey = "GuildHallDesc_Fortress",
            EffectDescKey = "GuildHallEffect_DefenseBonus", MaxLevel = 3, UnlockHallLevel = 8,
            EffectPerLevel = 0.05m,
            Costs = [
                new(30, 500_000), new(60, 1_200_000), new(120, 3_000_000)
            ]
        },
        new()
        {
            Id = GuildBuildingId.TrophyHall, NameKey = "GuildHall_TrophyHall", DescKey = "GuildHallDesc_TrophyHall",
            EffectDescKey = "GuildHallEffect_Trophies", MaxLevel = 1, UnlockHallLevel = 9,
            EffectPerLevel = 0m,
            Costs = [new(50, 1_000_000)]
        },
        new()
        {
            Id = GuildBuildingId.MasterThrone, NameKey = "GuildHall_MasterThrone", DescKey = "GuildHallDesc_MasterThrone",
            EffectDescKey = "GuildHallEffect_Everything", MaxLevel = 1, UnlockHallLevel = 10,
            EffectPerLevel = 0.05m,
            Costs = [new(100, 5_000_000)]
        },
    ];
}

/// <summary>Kosten für ein Gebäude-Upgrade</summary>
public record GuildBuildingCost(int GoldenScrews, long GuildMoney);

/// <summary>Anzeige-Daten für ein Gebäude in der Halle</summary>
public class GuildBuildingDisplay
{
    public GuildBuildingId BuildingId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string EffectDescription { get; set; } = "";
    public int CurrentLevel { get; set; }
    public int MaxLevel { get; set; }
    public int UnlockHallLevel { get; set; }
    public bool IsUnlocked { get; set; }
    public bool IsMaxLevel => CurrentLevel >= MaxLevel;
    public bool IsUpgrading { get; set; }
    public DateTime? UpgradeCompleteAt { get; set; }
    public GuildBuildingCost? NextUpgradeCost { get; set; }
}

/// <summary>Gesamt-Effekte aller Gebäude (berechnet)</summary>
public class GuildHallEffects
{
    public decimal CraftingSpeedBonus { get; set; }
    public decimal ResearchTimeReduction { get; set; }
    public decimal IncomeBonus { get; set; }
    public decimal OrderRewardBonus { get; set; }
    public decimal WarPointsBonus { get; set; }
    public int MaxMembersBonus { get; set; }
    public decimal WeeklyRewardBonus { get; set; }
    public decimal DefenseBonus { get; set; }
    public decimal EverythingBonus { get; set; }
}
```

**Schritt 5: GuildAchievement.cs erstellen**

```csharp
namespace HandwerkerImperium.Models;

using System.Text.Json.Serialization;

/// <summary>Firebase: guild_achievements/{guildId}/{achievementId}/</summary>
public class GuildAchievementState
{
    [JsonPropertyName("progress")]
    public long Progress { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("completedAt")]
    public string? CompletedAt { get; set; }
}

/// <summary>Statische Achievement-Definition</summary>
public class GuildAchievementDefinition
{
    public string Id { get; init; } = "";
    public string NameKey { get; init; } = "";
    public string DescKey { get; init; } = "";
    public GuildAchievementCategory Category { get; init; }
    public AchievementTier Tier { get; init; }
    public long Target { get; init; }
    public int GoldenScrewReward { get; init; }
    public string? CosmeticReward { get; init; }

    public static List<GuildAchievementDefinition> GetAll() =>
    [
        // ── Gemeinsam stark ──
        new() { Id = "contrib_bronze",  NameKey = "GuildAch_ContribBronze",  DescKey = "GuildAchDesc_ContribBronze",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Bronze,
                Target = 100_000, GoldenScrewReward = 5 },
        new() { Id = "contrib_silver",  NameKey = "GuildAch_ContribSilver",  DescKey = "GuildAchDesc_ContribSilver",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Silver,
                Target = 1_000_000, GoldenScrewReward = 15, CosmeticReward = "banner_silver_contrib" },
        new() { Id = "contrib_gold",    NameKey = "GuildAch_ContribGold",    DescKey = "GuildAchDesc_ContribGold",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Gold,
                Target = 10_000_000, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_contrib" },

        new() { Id = "research_bronze", NameKey = "GuildAch_ResearchBronze", DescKey = "GuildAchDesc_ResearchBronze",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Bronze,
                Target = 3, GoldenScrewReward = 5 },
        new() { Id = "research_silver", NameKey = "GuildAch_ResearchSilver", DescKey = "GuildAchDesc_ResearchSilver",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Silver,
                Target = 9, GoldenScrewReward = 15, CosmeticReward = "banner_silver_research" },
        new() { Id = "research_gold",   NameKey = "GuildAch_ResearchGold",   DescKey = "GuildAchDesc_ResearchGold",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Gold,
                Target = 18, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_research" },

        new() { Id = "members_bronze",  NameKey = "GuildAch_MembersBronze",  DescKey = "GuildAchDesc_MembersBronze",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Bronze,
                Target = 5, GoldenScrewReward = 5 },
        new() { Id = "members_silver",  NameKey = "GuildAch_MembersSilver",  DescKey = "GuildAchDesc_MembersSilver",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Silver,
                Target = 10, GoldenScrewReward = 15, CosmeticReward = "banner_silver_members" },
        new() { Id = "members_gold",    NameKey = "GuildAch_MembersGold",    DescKey = "GuildAchDesc_MembersGold",
                Category = GuildAchievementCategory.StrongerTogether, Tier = AchievementTier.Gold,
                Target = 20, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_members" },

        // ── Kriegshelden ──
        new() { Id = "wars_bronze",     NameKey = "GuildAch_WarsBronze",     DescKey = "GuildAchDesc_WarsBronze",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Bronze,
                Target = 3, GoldenScrewReward = 5 },
        new() { Id = "wars_silver",     NameKey = "GuildAch_WarsSilver",     DescKey = "GuildAchDesc_WarsSilver",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Silver,
                Target = 10, GoldenScrewReward = 15, CosmeticReward = "banner_silver_wars" },
        new() { Id = "wars_gold",       NameKey = "GuildAch_WarsGold",       DescKey = "GuildAchDesc_WarsGold",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Gold,
                Target = 50, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_wars" },

        new() { Id = "seasons_bronze",  NameKey = "GuildAch_SeasonsBronze",  DescKey = "GuildAchDesc_SeasonsBronze",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Bronze,
                Target = 1, GoldenScrewReward = 5 },
        new() { Id = "seasons_silver",  NameKey = "GuildAch_SeasonsSilver",  DescKey = "GuildAchDesc_SeasonsSilver",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Silver,
                Target = 4, GoldenScrewReward = 15, CosmeticReward = "banner_silver_seasons" },
        new() { Id = "seasons_gold",    NameKey = "GuildAch_SeasonsGold",    DescKey = "GuildAchDesc_SeasonsGold",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Gold,
                Target = 12, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_seasons" },

        new() { Id = "league_bronze",   NameKey = "GuildAch_LeagueBronze",   DescKey = "GuildAchDesc_LeagueBronze",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Bronze,
                Target = (long)GuildLeague.Silver, GoldenScrewReward = 5 },
        new() { Id = "league_silver",   NameKey = "GuildAch_LeagueSilver",   DescKey = "GuildAchDesc_LeagueSilver",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Silver,
                Target = (long)GuildLeague.Gold, GoldenScrewReward = 15, CosmeticReward = "banner_gold_league" },
        new() { Id = "league_gold",     NameKey = "GuildAch_LeagueGold",     DescKey = "GuildAchDesc_LeagueGold",
                Category = GuildAchievementCategory.WarHeroes, Tier = AchievementTier.Gold,
                Target = (long)GuildLeague.Diamond, GoldenScrewReward = 30, CosmeticReward = "emblem_diamond_league" },

        // ── Drachentöter ──
        new() { Id = "bosses_bronze",   NameKey = "GuildAch_BossesBronze",   DescKey = "GuildAchDesc_BossesBronze",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Bronze,
                Target = 3, GoldenScrewReward = 5 },
        new() { Id = "bosses_silver",   NameKey = "GuildAch_BossesSilver",   DescKey = "GuildAchDesc_BossesSilver",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Silver,
                Target = 10, GoldenScrewReward = 15, CosmeticReward = "banner_silver_bosses" },
        new() { Id = "bosses_gold",     NameKey = "GuildAch_BossesGold",     DescKey = "GuildAchDesc_BossesGold",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Gold,
                Target = 50, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_bosses" },

        new() { Id = "mvp_bronze",      NameKey = "GuildAch_MvpBronze",      DescKey = "GuildAchDesc_MvpBronze",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Bronze,
                Target = 1, GoldenScrewReward = 5 },
        new() { Id = "mvp_silver",      NameKey = "GuildAch_MvpSilver",      DescKey = "GuildAchDesc_MvpSilver",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Silver,
                Target = 5, GoldenScrewReward = 15, CosmeticReward = "banner_silver_mvp" },
        new() { Id = "mvp_gold",        NameKey = "GuildAch_MvpGold",        DescKey = "GuildAchDesc_MvpGold",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Gold,
                Target = 20, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_mvp" },

        new() { Id = "fastboss_bronze", NameKey = "GuildAch_FastBossBronze", DescKey = "GuildAchDesc_FastBossBronze",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Bronze,
                Target = 1, GoldenScrewReward = 5 },
        new() { Id = "fastboss_silver", NameKey = "GuildAch_FastBossSilver", DescKey = "GuildAchDesc_FastBossSilver",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Silver,
                Target = 3, GoldenScrewReward = 15, CosmeticReward = "banner_silver_fastboss" },
        new() { Id = "fastboss_gold",   NameKey = "GuildAch_FastBossGold",   DescKey = "GuildAchDesc_FastBossGold",
                Category = GuildAchievementCategory.DragonSlayers, Tier = AchievementTier.Gold,
                Target = 10, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_fastboss" },

        // ── Baumeister ──
        new() { Id = "maxbuilding_bronze", NameKey = "GuildAch_MaxBuildingBronze", DescKey = "GuildAchDesc_MaxBuildingBronze",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Bronze,
                Target = 1, GoldenScrewReward = 5 },
        new() { Id = "maxbuilding_silver", NameKey = "GuildAch_MaxBuildingSilver", DescKey = "GuildAchDesc_MaxBuildingSilver",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Silver,
                Target = 5, GoldenScrewReward = 15, CosmeticReward = "banner_silver_builder" },
        new() { Id = "maxbuilding_gold",   NameKey = "GuildAch_MaxBuildingGold",   DescKey = "GuildAchDesc_MaxBuildingGold",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Gold,
                Target = 10, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_builder" },

        new() { Id = "halllevel_bronze",   NameKey = "GuildAch_HallLevelBronze",   DescKey = "GuildAchDesc_HallLevelBronze",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Bronze,
                Target = 3, GoldenScrewReward = 5 },
        new() { Id = "halllevel_silver",   NameKey = "GuildAch_HallLevelSilver",   DescKey = "GuildAchDesc_HallLevelSilver",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Silver,
                Target = 6, GoldenScrewReward = 15, CosmeticReward = "banner_silver_hall" },
        new() { Id = "halllevel_gold",     NameKey = "GuildAch_HallLevelGold",     DescKey = "GuildAchDesc_HallLevelGold",
                Category = GuildAchievementCategory.Builders, Tier = AchievementTier.Gold,
                Target = 10, GoldenScrewReward = 30, CosmeticReward = "emblem_gold_hall" },
    ];
}

/// <summary>Anzeige-Daten für ein Achievement</summary>
public class GuildAchievementDisplay
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public GuildAchievementCategory Category { get; set; }
    public AchievementTier Tier { get; set; }
    public long Target { get; set; }
    public long Progress { get; set; }
    public bool IsCompleted { get; set; }
    public int GoldenScrewReward { get; set; }
    public string? CosmeticReward { get; set; }

    public float ProgressPercent => Target > 0 ? Math.Min(1f, (float)Progress / Target) : 0f;
}
```

**Schritt 6: FirebaseGuildData.cs erweitern**

Bestehende Datei um neue Felder ergänzen:

```csharp
// NEUE Properties zu FirebaseGuildData hinzufügen:
[JsonPropertyName("maxMembers")]
public int MaxMembers { get; set; } = 20;

[JsonPropertyName("leagueId")]
public string LeagueId { get; set; } = "bronze";

[JsonPropertyName("leaguePoints")]
public int LeaguePoints { get; set; }

[JsonPropertyName("hallLevel")]
public int HallLevel { get; set; } = 1;

[JsonPropertyName("description")]
public string Description { get; set; } = "";
```

**Schritt 7: GuildWar Firebase-Model erweitern**

```csharp
// NEUE Properties zu GuildWar hinzufügen:
[JsonPropertyName("guildALevel")]
public int GuildALevel { get; set; }

[JsonPropertyName("guildBLevel")]
public int GuildBLevel { get; set; }

[JsonPropertyName("phase")]
public string Phase { get; set; } = "attack";

[JsonPropertyName("phaseEndsAt")]
public string PhaseEndsAt { get; set; } = "";
```

**Schritt 8: Guild.cs - GuildMembership erweitern**

```csharp
// NEUE Properties zu GuildMembership hinzufügen:
[JsonPropertyName("guildHallLevel")]
public int GuildHallLevel { get; set; } = 1;

[JsonPropertyName("leagueId")]
public string LeagueId { get; set; } = "bronze";

// Hauptquartier-Boni (gecacht wie Research-Boni)
[JsonPropertyName("hallCraftingSpeedBonus")]
public decimal HallCraftingSpeedBonus { get; set; }

[JsonPropertyName("hallIncomeBonus")]
public decimal HallIncomeBonus { get; set; }

[JsonPropertyName("hallOrderRewardBonus")]
public decimal HallOrderRewardBonus { get; set; }

[JsonPropertyName("hallWarPointsBonus")]
public decimal HallWarPointsBonus { get; set; }

[JsonPropertyName("hallDefenseBonus")]
public decimal HallDefenseBonus { get; set; }

[JsonPropertyName("hallEverythingBonus")]
public decimal HallEverythingBonus { get; set; }

public void ApplyHallEffects(GuildHallEffects effects)
{
    HallCraftingSpeedBonus = effects.CraftingSpeedBonus;
    HallIncomeBonus = effects.IncomeBonus;
    HallOrderRewardBonus = effects.OrderRewardBonus;
    HallWarPointsBonus = effects.WarPointsBonus;
    HallDefenseBonus = effects.DefenseBonus;
    HallEverythingBonus = effects.EverythingBonus;
}
```

**Schritt 9: FirebaseGuildMember erweitern**

```csharp
// NEUE Properties zu FirebaseGuildMember hinzufügen:
[JsonPropertyName("joinedAt")]
public string JoinedAt { get; set; } = "";

[JsonPropertyName("lastActiveAt")]
public string LastActiveAt { get; set; } = "";

[JsonPropertyName("weeklyWarScore")]
public long WeeklyWarScore { get; set; }

[JsonPropertyName("totalWarScore")]
public long TotalWarScore { get; set; }
```

**Schritt 10: Build verifizieren**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
```

---

### Task 2: Service-Interfaces definieren

**Dateien:**
- Erstellen: `Services/IGuildResearchService.cs`
- Erstellen: `Services/IGuildWarSeasonService.cs`
- Erstellen: `Services/IGuildHallService.cs`
- Erstellen: `Services/IGuildBossService.cs`
- Erstellen: `Services/IGuildTipService.cs`
- Erstellen: `Services/IGuildAchievementService.cs`
- Ändern: `Services/IGuildService.cs` (abspecken)
- Ändern: `Services/IGuildWarService.cs` (durch IGuildWarSeasonService ersetzen)

**Wichtig:** Interfaces ZUERST definieren, dann Services implementieren. Jedes Interface spiegelt die Verantwortung aus dem Design-Dokument wider.

**Schritt 1: IGuildResearchService.cs**

```csharp
namespace HandwerkerImperium.Services;

using HandwerkerImperium.Models;

public interface IGuildResearchService
{
    Task<List<GuildResearchDisplay>> GetGuildResearchAsync();
    Task ContributeToResearchAsync(string researchId, long amount);
    Task CheckResearchCompletionAsync();
    GuildResearchEffects GetCachedEffects();
    Task RefreshResearchCacheAsync();
}
```

**Schritt 2: IGuildWarSeasonService.cs**

```csharp
namespace HandwerkerImperium.Services;

using HandwerkerImperium.Models;

public interface IGuildWarSeasonService
{
    Task InitializeAsync();
    Task<WarSeasonDisplayData?> GetCurrentWarDataAsync();
    Task ContributeScoreAsync(long points, string source);
    Task<List<GuildWarLogEntry>> GetWarLogAsync(int limit = 50);
    Task<List<WarBonusMission>> GetBonusMissionsAsync();
    Task CheckPhaseTransitionAsync();
    Task CheckSeasonEndAsync();
    GuildLeague GetCurrentLeague();
}
```

**Schritt 3: IGuildHallService.cs**

```csharp
namespace HandwerkerImperium.Services;

using HandwerkerImperium.Models;

public interface IGuildHallService
{
    Task<List<GuildBuildingDisplay>> GetBuildingsAsync();
    Task<bool> UpgradeBuildingAsync(GuildBuildingId buildingId);
    Task CheckUpgradeCompletionAsync();
    GuildHallEffects GetCachedEffects();
    Task RefreshHallCacheAsync();
    int GetHallLevel();
}
```

**Schritt 4: IGuildBossService.cs**

```csharp
namespace HandwerkerImperium.Services;

using HandwerkerImperium.Models;

public interface IGuildBossService
{
    Task<GuildBossDisplayData?> GetActiveBossAsync();
    Task DealDamageAsync(long damage, string source);
    Task CheckBossStatusAsync();
    Task<bool> SpawnBossIfNeededAsync();
    Task<List<BossDamageEntry>> GetLeaderboardAsync();
}
```

**Schritt 5: IGuildTipService.cs**

```csharp
namespace HandwerkerImperium.Services;

public interface IGuildTipService
{
    string? GetTipForContext(string context);
    void MarkTipSeen(string context);
    bool HasUnseenTip(string context);
}
```

**Schritt 6: IGuildAchievementService.cs**

```csharp
namespace HandwerkerImperium.Services;

using HandwerkerImperium.Models;

public interface IGuildAchievementService
{
    Task<List<GuildAchievementDisplay>> GetAchievementsAsync();
    Task UpdateProgressAsync(string achievementId, long progress);
    Task CheckAllAchievementsAsync();
    event Action<GuildAchievementDisplay>? AchievementCompleted;
}
```

**Schritt 7: IGuildService.cs anpassen**

Forschungs-Methoden entfernen (wandern nach IGuildResearchService), Rollen-Methoden hinzufügen:

```csharp
// ENTFERNEN aus IGuildService:
// - GetGuildResearchAsync()
// - ContributeToResearchAsync()
// - CheckResearchCompletionAsync()

// HINZUFÜGEN zu IGuildService:
Task<bool> PromoteToOfficerAsync(string targetUid);
Task<bool> DemoteToMemberAsync(string targetUid);
Task<bool> KickMemberAsync(string targetUid);
Task<bool> TransferLeadershipAsync(string targetUid);
Task UpdateLastActiveAsync();
GuildRole GetMemberRole(string uid);
```

**Schritt 8: Build verifizieren**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
```

Erwartet: Kompilier-Fehler weil GuildService noch die alten Methoden hat und GuildViewModel die alten Interfaces nutzt. Das ist OK - wird in den nächsten Tasks gefixt.

---

### Task 3: RESX-Strings für alle neuen Features

**Dateien:**
- Ändern: `Resources/Strings/AppStrings.resx` (EN)
- Ändern: `Resources/Strings/AppStrings.de.resx` (DE)
- Ändern: `Resources/Strings/AppStrings.es.resx` (ES)
- Ändern: `Resources/Strings/AppStrings.fr.resx` (FR)
- Ändern: `Resources/Strings/AppStrings.it.resx` (IT)
- Ändern: `Resources/Strings/AppStrings.pt.resx` (PT)

**Neue Keys (ca. 120+):**

Kategorien:
1. Gilden-Rollen (3 Keys)
2. Gildenkrieg-Saison (25 Keys)
3. Gilden-Bosse (20 Keys)
4. Hauptquartier (30 Keys)
5. Achievements (36 Keys Name + 36 Keys Desc)
6. Kontextuelle Tipps (8 Keys)
7. Info-Sheets (5 Keys)
8. Allgemeine Gilden-UI (10 Keys)

Zuerst EN (AppStrings.resx) vollständig, dann alle 5 anderen Sprachen. Detaillierte Key-Liste wird beim Implementieren erstellt - zu umfangreich für den Plan.

---

## Phase B: Services

### Task 4: GuildService refactoren (schlank machen)

**Dateien:**
- Ändern: `Services/GuildService.cs` (von ~1139 auf ~400 Zeilen)
- Ändern: `Services/IGuildService.cs`

**Was bleibt im GuildService:**
- `InitializeAsync()` (ohne Research-Teil)
- `BrowseGuildsAsync()`
- `CreateGuildAsync()` (+ neue Felder: maxMembers, leagueId, hallLevel, description)
- `JoinGuildAsync()` (+ joinedAt, lastActiveAt)
- `LeaveGuildAsync()`
- `ContributeAsync()`
- `RefreshGuildDetailsAsync()`
- `GetIncomeBonus()` (erweitert um Hall-Boni)
- `SetPlayerName()`
- `CheckWeeklyResetAsync()`
- Einladungs-System (GetOrCreateInviteCode, JoinByInviteCode, Browse/Register/Send/Accept/Decline)
- `UpdateLocalCache()` (erweitert um Hall-Daten)
- **NEU:** `PromoteToOfficerAsync()`, `DemoteToMemberAsync()`, `KickMemberAsync()`, `TransferLeadershipAsync()`
- **NEU:** `UpdateLastActiveAsync()` (bei jeder Aktion aufrufen)
- **NEU:** `GetMemberRole()`

**Was wird entfernt (nach GuildResearchService):**
- `GetGuildResearchAsync()`
- `ContributeToResearchAsync()`
- `CheckResearchCompletionAsync()`
- `_cachedResearchEffects` und zugehörige Logik
- Alle Research-spezifischen Hilfsmethoden

**Neue Rollen-Methoden:**

```csharp
public async Task<bool> PromoteToOfficerAsync(string targetUid)
{
    var membership = _gameStateService.State.GuildMembership;
    if (membership == null) return false;

    // Nur Leader darf befördern
    var ownRole = await GetMemberRoleAsync(_firebaseService.UserId);
    if (ownRole != GuildRole.Leader) return false;

    await _firebaseService.SetAsync(
        $"guild_members/{membership.GuildId}/{targetUid}/role", "officer");
    return true;
}

public async Task<bool> KickMemberAsync(string targetUid)
{
    var membership = _gameStateService.State.GuildMembership;
    if (membership == null) return false;

    var ownRole = await GetMemberRoleAsync(_firebaseService.UserId);
    var targetRole = await GetMemberRoleAsync(targetUid);

    // Leader kann alle kicken, Offizier nur Members
    if (ownRole == GuildRole.Member) return false;
    if (ownRole == GuildRole.Officer && targetRole != GuildRole.Member) return false;

    // Mitglied entfernen
    await _firebaseService.DeleteAsync($"guild_members/{membership.GuildId}/{targetUid}");
    await _firebaseService.DeleteAsync($"player_guilds/{targetUid}");

    // MemberCount verringern
    var guildData = await _firebaseService.GetAsync<FirebaseGuildData>($"guilds/{membership.GuildId}");
    if (guildData != null)
    {
        guildData.MemberCount = Math.Max(0, guildData.MemberCount - 1);
        await _firebaseService.SetAsync($"guilds/{membership.GuildId}/memberCount", guildData.MemberCount);
    }

    return true;
}

public async Task<bool> TransferLeadershipAsync(string targetUid)
{
    var membership = _gameStateService.State.GuildMembership;
    if (membership == null) return false;

    var ownRole = await GetMemberRoleAsync(_firebaseService.UserId);
    if (ownRole != GuildRole.Leader) return false;

    // Rollen tauschen
    await _firebaseService.SetAsync(
        $"guild_members/{membership.GuildId}/{targetUid}/role", "leader");
    await _firebaseService.SetAsync(
        $"guild_members/{membership.GuildId}/{_firebaseService.UserId}/role", "officer");

    return true;
}
```

**Build verifizieren:**

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
```

---

### Task 5: GuildResearchService implementieren

**Dateien:**
- Erstellen: `Services/GuildResearchService.cs`

Logik 1:1 aus GuildService extrahieren. Gleiche Firebase-Pfade, gleiche Timer-Mechanik. Zusätzlich: Hall-Effekt "Forschungslabor" berücksichtigen (-5% Zeit pro Level).

**Constructor:**
```csharp
public sealed class GuildResearchService : IGuildResearchService
{
    private readonly IGameStateService _gameStateService;
    private readonly IFirebaseService _firebaseService;
    private readonly IGuildHallService _hallService;
    private GuildResearchEffects _cachedEffects = new();

    public GuildResearchService(
        IGameStateService gameStateService,
        IFirebaseService firebaseService,
        IGuildHallService hallService)
    { ... }
}
```

**Hinweis:** Research-Timer-Dauer wird um Hall-Bonus reduziert:
```csharp
var baseHours = GuildResearchDefinition.GetResearchDurationHours(definition.Cost);
var reduction = _hallService.GetCachedEffects().ResearchTimeReduction;
var adjustedHours = baseHours * (1.0 - (double)reduction);
```

---

### Task 6: GuildWarSeasonService implementieren

**Dateien:**
- Erstellen: `Services/GuildWarSeasonService.cs`
- Löschen: `Services/GuildWarService.cs` (nach Migration)
- Löschen: `Services/IGuildWarService.cs` (nach Migration)

**Kern-Logik:**

1. **Saison-ID-Berechnung:**
```csharp
// Saison = 4 Wochen, Start bei KW 1 des Jahres
private static string GetCurrentSeasonId()
{
    var now = DateTime.UtcNow;
    var week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
        now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    var seasonNumber = (week - 1) / 4 + 1;
    return $"s_{now.Year}_{seasonNumber:D2}";
}
```

2. **Phasen-Erkennung:**
```csharp
private static WarPhase GetCurrentPhase()
{
    var now = DateTime.UtcNow;
    var dayOfWeek = now.DayOfWeek;
    return dayOfWeek switch
    {
        DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday => WarPhase.Attack,
        DayOfWeek.Thursday or DayOfWeek.Friday => WarPhase.Defense,
        _ => WarPhase.Evaluation
    };
}
```

3. **Matching (Liga-basiert, Level ±3):**
```csharp
// Alle Gilden derselben Liga laden
// Nach Level sortieren
// Paare bilden mit |levelA - levelB| <= 3
// Übrige: Toleranz auf ±5
// Letzte übrige: Bye-Woche
```

4. **Score-Beitrag (Race-Condition-frei):**
```csharp
// Jeder Spieler schreibt NUR seinen eigenen Score
await _firebase.SetAsync(
    $"guild_war_scores/{warId}/{guildId}/{uid}",
    new GuildWarPlayerScore { AttackScore = newAttack, DefenseScore = newDefense, UpdatedAt = ... });

// Gesamt-Score: Alle Member-Scores laden und summieren
var allScores = await _firebase.GetAsync<Dictionary<string, GuildWarPlayerScore>>(
    $"guild_war_scores/{warId}/{guildId}");
var totalScore = allScores?.Values.Sum(s => s.TotalScore) ?? 0;
```

5. **Verteidigungsphase:** Punkte × 0.5, Aufhol-Multiplikator 1.5x wenn zurückliegend

6. **Bonus-Missionen:** 3 täglich, werden in Preferences gespeichert (Tages-Key), Progress lokal getrackt

---

### Task 7: GuildBossService implementieren

**Dateien:**
- Erstellen: `Services/GuildBossService.cs`

**Kern-Logik:**

1. **Boss-Spawn:** Einmal pro Woche, Boss-Typ rotiert nach KW (`weekNumber % 6`)
2. **Schaden zufügen:** Schreibt auf eigenen Firebase-Pfad (`guild_boss_damage/{guildId}/{uid}`)
3. **HP berechnen:** `currentHp = bossHp - SUM(allDamage)` (aus allen Member-Damage-Einträgen)
4. **Boss besiegt:** Wenn `currentHp <= 0`, Status auf "defeated", Belohnungen verteilen
5. **Boss abgelaufen:** Wenn `expiresAt < UtcNow`, Status auf "expired"
6. **Leaderboard:** Top-Damage sortiert, eigenen Rang berechnen

---

### Task 8: GuildHallService implementieren

**Dateien:**
- Erstellen: `Services/GuildHallService.cs`

**Kern-Logik:**

1. **Gebäude laden:** `guild_hall/{guildId}/buildings/` → merge mit `GuildBuildingDefinition.GetAll()`
2. **Upgrade starten:** Goldschrauben + Gildengeld prüfen, `upgradingUntil` setzen
3. **Upgrade abschließen:** Timer-Check wie Research, Level erhöhen, `upgradingUntil` löschen
4. **Effekte berechnen:** Alle Gebäude-Level × EffectPerLevel summieren → `GuildHallEffects`
5. **Hall-Level:** Wird separat in `guilds/{guildId}/hallLevel` gespeichert, Leader kann erhöhen wenn genug Gildengeld
6. **Cache:** `GuildMembership.ApplyHallEffects()` für Offline-Nutzung

---

### Task 9: GuildTipService + GuildAchievementService implementieren

**Dateien:**
- Erstellen: `Services/GuildTipService.cs`
- Erstellen: `Services/GuildAchievementService.cs`

**GuildTipService:** Einfacher Preferences-basierter Service:
```csharp
// 8 Tip-Kontexte mit Preferences-Flag
private const string PrefPrefix = "guild_tip_seen_";

public string? GetTipForContext(string context)
{
    if (_preferences.Get(PrefPrefix + context, false)) return null;
    return _localization.GetString($"GuildTip_{context}");
}

public void MarkTipSeen(string context)
    => _preferences.Set(PrefPrefix + context, true);
```

**GuildAchievementService:** Firebase-basiert, Progress wird bei relevanten Aktionen aktualisiert:
```csharp
public event Action<GuildAchievementDisplay>? AchievementCompleted;

public async Task CheckAllAchievementsAsync()
{
    // Für jedes Achievement: aktuellen Fortschritt berechnen
    // Bei Erreichen: AchievementCompleted feuern + GS gutschreiben
}
```

---

## Phase C: ViewModel & UI

### Task 10: GuildViewModel refactoren + Sub-ViewModels

**Dateien:**
- Ändern: `ViewModels/GuildViewModel.cs` (Hub + Delegation)
- Erstellen: `ViewModels/GuildWarSeasonViewModel.cs`
- Erstellen: `ViewModels/GuildBossViewModel.cs`
- Erstellen: `ViewModels/GuildHallViewModel.cs`

**GuildViewModel wird zum Hub:**
- Behält: State-Machine (erweitert um neue States), Browse, Create, Name-Dialog, Übersicht
- Delegiert: War → `GuildWarSeasonViewModel`, Boss → `GuildBossViewModel`, Hall → `GuildHallViewModel`
- Research + Chat + Members bleiben im GuildViewModel (schon handhabbar)

**Neue ViewState-Erweiterung:**
```csharp
public enum GuildViewState
{
    Loading, Offline, NameDialog, CreateDialog, Browse, InGuild,
    // NEU:
    WarSeason, Boss, Hall, Achievements
}
```

**Quick-Status Properties (Hub):**
```csharp
[ObservableProperty] private string _warQuickStatus = "";      // "3:2 Angriff"
[ObservableProperty] private string _bossQuickStatus = "";     // "67% Rostdrache"
[ObservableProperty] private string _leagueQuickStatus = "";   // "Gold-Liga"
[ObservableProperty] private string _researchQuickStatus = ""; // "2/18 aktiv"
```

**Sub-ViewModel Injection:**
```csharp
public GuildViewModel(
    // bestehende Services...
    IGuildWarSeasonService warSeasonService,
    IGuildBossService bossService,
    IGuildHallService hallService,
    IGuildTipService tipService,
    IGuildAchievementService achievementService,
    // Sub-ViewModels:
    GuildWarSeasonViewModel warSeasonViewModel,
    GuildBossViewModel bossViewModel,
    GuildHallViewModel hallViewModel)
```

---

### Task 11: DI-Registrierung aktualisieren

**Dateien:**
- Ändern: `App.axaml.cs` (ConfigureServices)

**Neue Registrierungen:**
```csharp
// Neue Guild-Services (alle Singleton)
services.AddSingleton<IGuildResearchService, GuildResearchService>();
services.AddSingleton<IGuildWarSeasonService, GuildWarSeasonService>();
services.AddSingleton<IGuildHallService, GuildHallService>();
services.AddSingleton<IGuildBossService, GuildBossService>();
services.AddSingleton<IGuildTipService, GuildTipService>();
services.AddSingleton<IGuildAchievementService, GuildAchievementService>();

// Sub-ViewModels (Singleton, von MainViewModel gehalten)
services.AddSingleton<GuildWarSeasonViewModel>();
services.AddSingleton<GuildBossViewModel>();
services.AddSingleton<GuildHallViewModel>();

// ENTFERNEN:
// services.AddSingleton<IGuildWarService, GuildWarService>();
```

---

### Task 12: Views & Navigation erweitern

**Dateien:**
- Ändern: `Views/GuildView.axaml` (Hub mit Quick-Status)
- Erstellen: `Views/GuildWarSeasonView.axaml`
- Erstellen: `Views/GuildBossView.axaml`
- Erstellen: `Views/GuildHallView.axaml`
- Erstellen: `Views/GuildAchievementsView.axaml`
- Ändern: `ViewModels/MainViewModel.Navigation.cs` (neue Routen)

**Neue Routen:**
```csharp
"guild_war_season"   → GuildWarSeasonView
"guild_boss"         → GuildBossView
"guild_hall"         → GuildHallView
"guild_achievements" → GuildAchievementsView
```

**GuildView.axaml Hub-Layout:**
```xml
<!-- Quick-Status-Leiste -->
<UniformGrid Columns="4" Margin="8">
    <Button Command="{Binding NavigateToWarSeasonCommand}">
        <!-- Liga + War Score -->
    </Button>
    <Button Command="{Binding NavigateToBossCommand}">
        <!-- Boss HP% -->
    </Button>
    <Button Command="{Binding NavigateToResearchCommand}">
        <!-- Forschung X/18 -->
    </Button>
    <Button Command="{Binding NavigateToHallCommand}">
        <!-- Halle Lv.X -->
    </Button>
</UniformGrid>

<!-- Gildenhalle-Szene (SkiaSharp Canvas) -->
<SKCanvasView ... />

<!-- Tip-Banner -->
<Border IsVisible="{Binding HasActiveTip}" ... />
```

---

## Phase D: SkiaSharp-Renderer

### Task 13: Bestehende Renderer fixen

**Dateien:**
- Ändern: `Graphics/GuildHallHeaderRenderer.cs`
- Ändern: `Graphics/GuildResearchTreeRenderer.cs`

**GuildHallHeaderRenderer - Shader-Cache:**
```csharp
// STATT pro Frame:
// using var shader = SKShader.CreateRadialGradient(...);

// NEU: Shader als Feld, nur bei Bounds-Änderung neu erstellen
private SKShader? _torchGlowShader;
private SKRect _lastTorchBounds;

// In DrawTorchGlow():
// Opacity variiert per Frame, Shader bleibt gleich
// → _torchGlowPaint.Color = _torchGlowPaint.Color.WithAlpha((byte)(baseAlpha + pulse));
```

**GuildResearchTreeRenderer - Dash-Cache + Swap-Remove:**
```csharp
// STATT pro Frame:
// using var dash = SKPathEffect.CreateDash(...)

// NEU: Dash als Feld
private SKPathEffect? _lockedDash;

// Swap-Remove statt RemoveAt:
private void RemoveParticle(int index)
{
    _flowParticles[index] = _flowParticles[_flowParticleCount - 1];
    _flowParticleCount--;
}
```

---

### Task 14: Neue Renderer implementieren

**Dateien:**
- Erstellen: `Graphics/GuildHallSceneRenderer.cs`
- Erstellen: `Graphics/GuildBossRenderer.cs`
- Erstellen: `Graphics/GuildWarDashboardRenderer.cs`
- Erstellen: `Graphics/GuildLeagueBadgeRenderer.cs`
- Erstellen: `Graphics/GuildWarLogRenderer.cs`
- Erstellen: `Graphics/GuildAchievementRenderer.cs`

**Reihenfolge:** GuildLeagueBadgeRenderer zuerst (wird von WarDashboard eingebettet).

**Alle Renderer folgen dem Pattern:**
```csharp
public sealed class XxxRenderer : IDisposable
{
    private bool _disposed;
    private float _time;

    // Offscreen-Cache
    private SKBitmap? _cachedBackground;
    private bool _backgroundDirty = true;

    // Struct-Pools für Partikel
    private struct Particle { public float X, Y, Life, Speed; }
    private readonly Particle[] _particles = new Particle[MaxParticles];
    private int _particleCount;

    public void SetData(XxxDisplayData data) { ... _backgroundDirty = true; }
    public void Update(float deltaTime) { _time += deltaTime; ... }
    public void Render(SKCanvas canvas, SKRect bounds) { ... }
    public int HitTest(float x, float y) { return -1; } // für Touch
    public void Dispose() { ... }
}
```

**GuildHallSceneRenderer (größter Renderer):**
- Terrain: 8×6 isometrisches Grid, Gras/Stein-Tiles
- Gebäude: Prozedural gezeichnet, Y-sortiert für Überlappung
- Atmosphäre: Rauch (Struct-Pool, 10 max), Fenster-Glow, Fahne (Sinus)
- Touch: Gebäude-HitTest über vorberechnete Bounds
- Performance: Terrain + Gebäude auf Offscreen-Bitmap (Dirty-Flag)

**GuildBossRenderer:**
- Boss-Silhouette: Typ-spezifische Form, Atem-Animation (Scale 0.98-1.02)
- HP-Balken: Segmentiert, Farb-Gradient, Nachzieh-Effekt
- Damage-Feed: Letzte 8 Hits als Floating-Text
- Countdown: Pulsierend wenn <6h

**GuildWarDashboardRenderer:**
- Versus-Anzeige: Zwei Wappen, Score-Balken
- Phasen-Timeline: Angriff/Verteidigung/Auswertung
- Bonus-Missionen: 3 Fortschrittsbalken
- Eingebettet: GuildLeagueBadgeRenderer oben

**GuildLeagueBadgeRenderer:**
- Schild-Form: Bronze/Silber/Gold/Diamant mit Shimmer
- Compact: Kann als kleines Badge oder großes Wappen rendern

---

## Abschluss-Tasks (nach allen Phasen)

### Build & AppChecker

```bash
dotnet build src/Apps/HandwerkerImperium/HandwerkerImperium.Shared
dotnet run --project tools/AppChecker HandwerkerImperium
```

### CLAUDE.md aktualisieren

- `src/Apps/HandwerkerImperium/CLAUDE.md`: Neue Services, Models, Renderer, Routen dokumentieren
- `F:\Meine_Apps_Ava\CLAUDE.md`: Ggf. neue Patterns/Troubleshooting

### GameLoop-Integration

In `GameLoopService` (1-Sekunden-Takt) die neuen Services einbinden:
- `GuildBossService.DealDamageAsync()` bei Aufträgen/MiniGames/Crafting aufrufen
- `GuildWarSeasonService.ContributeScoreAsync()` bei Aktionen aufrufen
- `GuildHallService.CheckUpgradeCompletionAsync()` alle 60s
- `GuildBossService.CheckBossStatusAsync()` alle 60s
- `GuildAchievementService.CheckAllAchievementsAsync()` alle 300s
