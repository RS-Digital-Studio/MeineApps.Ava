using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

// ═══════════════════════════════════════════════════════════════════════
// FIREBASE-MODELS (Gilden-Hauptquartier)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Firebase-Zustand eines Gilden-Gebäudes.
/// Pfad: guild_buildings/{guildId}/{buildingId}
/// </summary>
public class GuildBuildingState
{
    /// <summary>Aktuelles Level des Gebäudes (0 = nicht gebaut).</summary>
    [JsonPropertyName("level")]
    public int Level { get; set; }

    /// <summary>Wann das Upgrade fertig ist (UTC ISO 8601, leer wenn kein Upgrade läuft).</summary>
    [JsonPropertyName("upgradingUntil")]
    public string UpgradingUntil { get; set; } = "";

    /// <summary>Wann das Gebäude freigeschaltet wurde (UTC ISO 8601).</summary>
    [JsonPropertyName("unlockedAt")]
    public string UnlockedAt { get; set; } = "";

    /// <summary>Ob gerade ein Upgrade läuft.</summary>
    [JsonIgnore]
    public bool IsUpgrading
    {
        get
        {
            if (string.IsNullOrEmpty(UpgradingUntil)) return false;
            if (DateTime.TryParse(UpgradingUntil, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var until))
            {
                return DateTime.UtcNow < until;
            }
            return false;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// KOSTEN
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Kosten für ein Gebäude-Upgrade (Goldschrauben + Gildengeld).
/// </summary>
/// <param name="GoldenScrews">Benötigte Goldschrauben.</param>
/// <param name="GuildMoney">Benötigtes Gildengeld (EUR aus der Gildenkasse).</param>
public record GuildBuildingCost(int GoldenScrews, long GuildMoney);

// ═══════════════════════════════════════════════════════════════════════
// DEFINITION (statisch, 10 Gebäude)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Statische Definition eines Gilden-Gebäudes.
/// </summary>
public class GuildBuildingDefinition
{
    /// <summary>Gebäude-ID (Enum).</summary>
    public GuildBuildingId BuildingId { get; init; }

    /// <summary>Lokalisierungs-Key für den Namen.</summary>
    public string NameKey { get; init; } = "";

    /// <summary>Lokalisierungs-Key für die Beschreibung.</summary>
    public string DescKey { get; init; } = "";

    /// <summary>Lokalisierungs-Key für den Effekt (mit {0} Platzhalter für den Wert).</summary>
    public string EffectKey { get; init; } = "";

    /// <summary>GameIconKind-Name.</summary>
    public string Icon { get; init; } = "";

    /// <summary>Effekt-Wert pro Level (z.B. 0.02 = +2% pro Level).</summary>
    public decimal EffectPerLevel { get; init; }

    /// <summary>Maximales Level.</summary>
    public int MaxLevel { get; init; }

    /// <summary>Ab welchem Hallen-Level dieses Gebäude freigeschaltet wird.</summary>
    public int UnlockHallLevel { get; init; }

    /// <summary>Farbe für UI-Darstellung (Hex).</summary>
    public string Color { get; init; } = "#888888";

    /// <summary>
    /// Berechnet die Upgrade-Kosten für ein bestimmtes Ziel-Level.
    /// Kosten steigen exponentiell pro Level.
    /// </summary>
    public GuildBuildingCost GetUpgradeCost(int targetLevel)
    {
        // Basiskosten: 10 GS + 500K Gildengeld, eskalierende Faktor 2.0x pro Level
        var screws = (int)(10 * Math.Pow(2.0, targetLevel - 1));
        var money = (long)(500_000 * Math.Pow(2.5, targetLevel - 1));
        return new GuildBuildingCost(screws, money);
    }

    /// <summary>
    /// Gibt alle 10 Gilden-Gebäude-Definitionen zurück (gecacht).
    /// </summary>
    private static readonly List<GuildBuildingDefinition> _allDefinitions =
    [
        // Werkstatt: +2% Crafting-Geschwindigkeit pro Level
        new()
        {
            BuildingId = GuildBuildingId.Workshop,
            NameKey = "GuildBuilding_Workshop",
            DescKey = "GuildBuildingDesc_Workshop",
            EffectKey = "GuildBuildingEffect_CraftingSpeed",
            Icon = "Hammer",
            EffectPerLevel = 0.02m,
            MaxLevel = 5,
            UnlockHallLevel = 1,
            Color = "#D97706"
        },

        // Forschungslabor: -5% Forschungszeit pro Level
        new()
        {
            BuildingId = GuildBuildingId.ResearchLab,
            NameKey = "GuildBuilding_ResearchLab",
            DescKey = "GuildBuildingDesc_ResearchLab",
            EffectKey = "GuildBuildingEffect_ResearchTime",
            Icon = "FlaskOutline",
            EffectPerLevel = 0.05m,
            MaxLevel = 5,
            UnlockHallLevel = 2,
            Color = "#2196F3"
        },

        // Handelsposten: +3% Einkommen pro Level
        new()
        {
            BuildingId = GuildBuildingId.TradingPost,
            NameKey = "GuildBuilding_TradingPost",
            DescKey = "GuildBuildingDesc_TradingPost",
            EffectKey = "GuildBuildingEffect_Income",
            Icon = "StorefrontOutline",
            EffectPerLevel = 0.03m,
            MaxLevel = 5,
            UnlockHallLevel = 3,
            Color = "#4CAF50"
        },

        // Schmiede: +2% Auftragsbelohnung pro Level
        new()
        {
            BuildingId = GuildBuildingId.Smithy,
            NameKey = "GuildBuilding_Smithy",
            DescKey = "GuildBuildingDesc_Smithy",
            EffectKey = "GuildBuildingEffect_OrderReward",
            Icon = "Anvil",
            EffectPerLevel = 0.02m,
            MaxLevel = 5,
            UnlockHallLevel = 4,
            Color = "#EA580C"
        },

        // Wachturm: +5% Kriegs-Punkte pro Level
        new()
        {
            BuildingId = GuildBuildingId.Watchtower,
            NameKey = "GuildBuilding_Watchtower",
            DescKey = "GuildBuildingDesc_Watchtower",
            EffectKey = "GuildBuildingEffect_WarPoints",
            Icon = "TowerFire",
            EffectPerLevel = 0.05m,
            MaxLevel = 5,
            UnlockHallLevel = 5,
            Color = "#DC2626"
        },

        // Versammlungshalle: +2 Max-Mitglieder pro Level
        new()
        {
            BuildingId = GuildBuildingId.AssemblyHall,
            NameKey = "GuildBuilding_AssemblyHall",
            DescKey = "GuildBuildingDesc_AssemblyHall",
            EffectKey = "GuildBuildingEffect_MaxMembers",
            Icon = "AccountGroup",
            EffectPerLevel = 2m,
            MaxLevel = 3,
            UnlockHallLevel = 6,
            Color = "#0E7490"
        },

        // Schatzkammer: +5% Wochenziel-Belohnung pro Level
        new()
        {
            BuildingId = GuildBuildingId.Treasury,
            NameKey = "GuildBuilding_Treasury",
            DescKey = "GuildBuildingDesc_Treasury",
            EffectKey = "GuildBuildingEffect_WeeklyReward",
            Icon = "TreasureChest",
            EffectPerLevel = 0.05m,
            MaxLevel = 3,
            UnlockHallLevel = 7,
            Color = "#FFD700"
        },

        // Festung: +5% Verteidigungsbonus pro Level
        new()
        {
            BuildingId = GuildBuildingId.Fortress,
            NameKey = "GuildBuilding_Fortress",
            DescKey = "GuildBuildingDesc_Fortress",
            EffectKey = "GuildBuildingEffect_Defense",
            Icon = "ShieldLock",
            EffectPerLevel = 0.05m,
            MaxLevel = 3,
            UnlockHallLevel = 8,
            Color = "#475569"
        },

        // Trophäenhalle: Zeigt Achievements, 1 Level
        new()
        {
            BuildingId = GuildBuildingId.TrophyHall,
            NameKey = "GuildBuilding_TrophyHall",
            DescKey = "GuildBuildingDesc_TrophyHall",
            EffectKey = "GuildBuildingEffect_Trophies",
            Icon = "Trophy",
            EffectPerLevel = 0m,
            MaxLevel = 1,
            UnlockHallLevel = 9,
            Color = "#9C27B0"
        },

        // Meisterthron: +5% auf alles pro Level, 1 Level
        new()
        {
            BuildingId = GuildBuildingId.MasterThrone,
            NameKey = "GuildBuilding_MasterThrone",
            DescKey = "GuildBuildingDesc_MasterThrone",
            EffectKey = "GuildBuildingEffect_Everything",
            Icon = "Crown",
            EffectPerLevel = 0.05m,
            MaxLevel = 1,
            UnlockHallLevel = 10,
            Color = "#B91C1C"
        }
    ];

    public static List<GuildBuildingDefinition> GetAll() => _allDefinitions;
}

// ═══════════════════════════════════════════════════════════════════════
// DISPLAY (UI-Daten für ViewModel)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Aufbereitete Anzeige-Daten für ein Gilden-Gebäude.
/// </summary>
public class GuildBuildingDisplay
{
    /// <summary>Gebäude-ID.</summary>
    public GuildBuildingId BuildingId { get; set; }

    /// <summary>Lokalisierter Name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Lokalisierte Beschreibung.</summary>
    public string Description { get; set; } = "";

    /// <summary>Lokalisierte Effekt-Beschreibung (z.B. "+6% Crafting-Geschwindigkeit").</summary>
    public string EffectDescription { get; set; } = "";

    /// <summary>Aktuelles Level.</summary>
    public int CurrentLevel { get; set; }

    /// <summary>Maximales Level.</summary>
    public int MaxLevel { get; set; }

    /// <summary>Ab welchem Hallen-Level freigeschaltet.</summary>
    public int UnlockHallLevel { get; set; }

    /// <summary>Ob das Gebäude freigeschaltet ist (Hallen-Level erreicht).</summary>
    public bool IsUnlocked { get; set; }

    /// <summary>Ob gerade ein Upgrade läuft.</summary>
    public bool IsUpgrading { get; set; }

    /// <summary>Wann das aktuelle Upgrade fertig ist (UTC ISO 8601).</summary>
    public string UpgradeCompleteAt { get; set; } = "";

    /// <summary>Kosten für das nächste Upgrade (null wenn Max-Level erreicht).</summary>
    public GuildBuildingCost? NextUpgradeCost { get; set; }

    /// <summary>GameIconKind-Name.</summary>
    public string Icon { get; set; } = "";

    /// <summary>Farbe für UI-Darstellung.</summary>
    public string Color { get; set; } = "#888888";

    /// <summary>Ob das maximale Level erreicht ist.</summary>
    [JsonIgnore]
    public bool IsMaxLevel => CurrentLevel >= MaxLevel;

    /// <summary>BuildingId als String für XAML CommandParameter.</summary>
    [JsonIgnore]
    public string BuildingIdStr => BuildingId.ToString();
}

// ═══════════════════════════════════════════════════════════════════════
// EFFEKTE (berechnete Gesamteffekte aller Gilden-Gebäude)
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Berechnete Gesamteffekte aller Gilden-Gebäude.
/// Wird aus den aktuellen Gebäude-Leveln berechnet und auf GuildMembership gecacht.
/// </summary>
public class GuildHallEffects
{
    /// <summary>Crafting-Geschwindigkeitsbonus (Workshop: +2% pro Lv, max +10%).</summary>
    public decimal CraftingSpeedBonus { get; set; }

    /// <summary>Forschungszeit-Reduktion (ResearchLab: -5% pro Lv, max -25%). Negativ = Reduktion.</summary>
    public decimal ResearchTimeReduction { get; set; }

    /// <summary>Einkommensbonus (TradingPost: +3% pro Lv, max +15%).</summary>
    public decimal IncomeBonus { get; set; }

    /// <summary>Auftragsbelohnungs-Bonus (Smithy: +2% pro Lv, max +10%).</summary>
    public decimal OrderRewardBonus { get; set; }

    /// <summary>Kriegs-Punkte-Bonus (Watchtower: +5% pro Lv, max +25%).</summary>
    public decimal WarPointsBonus { get; set; }

    /// <summary>Zusätzliche Max-Mitglieder (AssemblyHall: +2 pro Lv, max +6).</summary>
    public int MaxMembersBonus { get; set; }

    /// <summary>Wochenziel-Belohnungs-Bonus (Treasury: +5% pro Lv, max +15%).</summary>
    public decimal WeeklyRewardBonus { get; set; }

    /// <summary>Verteidigungsbonus im Krieg (Fortress: +5% pro Lv, max +15%).</summary>
    public decimal DefenseBonus { get; set; }

    /// <summary>Universalbonus auf alles (MasterThrone: +5%, max +5%).</summary>
    public decimal EverythingBonus { get; set; }

    /// <summary>
    /// Berechnet die Gesamteffekte aus einer Map von Gebäude-Leveln.
    /// </summary>
    public static GuildHallEffects Calculate(Dictionary<GuildBuildingId, int> buildingLevels)
    {
        var effects = new GuildHallEffects();

        foreach (var def in GuildBuildingDefinition.GetAll())
        {
            if (!buildingLevels.TryGetValue(def.BuildingId, out var level) || level <= 0)
                continue;

            var clampedLevel = Math.Min(level, def.MaxLevel);
            var totalEffect = def.EffectPerLevel * clampedLevel;

            switch (def.BuildingId)
            {
                case GuildBuildingId.Workshop:
                    effects.CraftingSpeedBonus = totalEffect;
                    break;
                case GuildBuildingId.ResearchLab:
                    effects.ResearchTimeReduction = totalEffect;
                    break;
                case GuildBuildingId.TradingPost:
                    effects.IncomeBonus = totalEffect;
                    break;
                case GuildBuildingId.Smithy:
                    effects.OrderRewardBonus = totalEffect;
                    break;
                case GuildBuildingId.Watchtower:
                    effects.WarPointsBonus = totalEffect;
                    break;
                case GuildBuildingId.AssemblyHall:
                    effects.MaxMembersBonus = (int)totalEffect;
                    break;
                case GuildBuildingId.Treasury:
                    effects.WeeklyRewardBonus = totalEffect;
                    break;
                case GuildBuildingId.Fortress:
                    effects.DefenseBonus = totalEffect;
                    break;
                case GuildBuildingId.TrophyHall:
                    // Zeigt nur Achievements, kein numerischer Effekt
                    break;
                case GuildBuildingId.MasterThrone:
                    effects.EverythingBonus = totalEffect;
                    break;
            }
        }

        return effects;
    }
}
