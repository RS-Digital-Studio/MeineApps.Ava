namespace HandwerkerImperium.Models;

/// <summary>
/// Definition eines Ascension-Perks.
/// </summary>
public class AscensionPerk
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public string DescriptionKey { get; set; } = "";
    public string Icon { get; set; } = "";
    public int MaxLevel { get; set; } = 5;

    /// <summary>Kosten pro Level (Index 0 = Level 1, etc.).</summary>
    public int[] CostsPerLevel { get; set; } = [];

    /// <summary>Effekt-Werte pro Level (Index 0 = Level 1, etc.).</summary>
    public decimal[] ValuesPerLevel { get; set; } = [];

    /// <summary>Kosten für ein bestimmtes Level (1-basiert).</summary>
    public int GetCost(int level)
    {
        if (level < 1 || level > CostsPerLevel.Length) return int.MaxValue;
        return CostsPerLevel[level - 1];
    }

    /// <summary>Effekt-Wert für ein bestimmtes Level (0 = nicht gekauft). Clampt auf MaxLevel für Save-Kompatibilität.</summary>
    public decimal GetValue(int level)
    {
        // Clamp: Alte Saves mit Level > MaxLevel bekommen den Max-Wert
        int clamped = Math.Min(level, ValuesPerLevel.Length);
        if (clamped < 1) return 0m;
        return ValuesPerLevel[clamped - 1];
    }

    /// <summary>
    /// Alle 6 Ascension-Perks.
    /// </summary>
    public static List<AscensionPerk> GetAll()
    {
        // MaxLevel 3 statt 5: Gesamt 61 AP (statt 120), erreichbar in 4-6 Ascensions
        return
        [
            new AscensionPerk
            {
                Id = "asc_start_capital",
                NameKey = "AscStartCapital",
                DescriptionKey = "AscStartCapitalDesc",
                Icon = "Bank",
                MaxLevel = 3,
                CostsPerLevel = [1, 3, 6],
                // +100%, +500%, +1000% Startgeld nach Prestige
                ValuesPerLevel = [1.00m, 5.00m, 10.00m]
            },
            new AscensionPerk
            {
                Id = "asc_eternal_tools",
                NameKey = "AscEternalTools",
                DescriptionKey = "AscEternalToolsDesc",
                Icon = "Wrench",
                MaxLevel = 3,
                CostsPerLevel = [2, 4, 8],
                // Erste 2 behalten, erste 4 behalten, alle behalten
                ValuesPerLevel = [2m, 4m, 5m]
            },
            new AscensionPerk
            {
                Id = "asc_quick_start",
                NameKey = "AscQuickStart",
                DescriptionKey = "AscQuickStartDesc",
                Icon = "RocketLaunch",
                MaxLevel = 3,
                CostsPerLevel = [1, 3, 7],
                // Start mit 2/4/alle Workshops freigeschaltet
                ValuesPerLevel = [2m, 4m, 8m]
            },
            new AscensionPerk
            {
                Id = "asc_timeless_research",
                NameKey = "AscTimelessResearch",
                DescriptionKey = "AscTimelessResearchDesc",
                Icon = "FlaskOutline",
                MaxLevel = 3,
                CostsPerLevel = [1, 2, 5],
                // Research-Dauer -15%/-30%/-50%
                ValuesPerLevel = [0.15m, 0.30m, 0.50m]
            },
            new AscensionPerk
            {
                Id = "asc_golden_era",
                NameKey = "AscGoldenEra",
                DescriptionKey = "AscGoldenEraDesc",
                Icon = "Screwdriver",
                MaxLevel = 3,
                CostsPerLevel = [1, 3, 6],
                // Goldschrauben-Verdienst +20%/+50%/+100%
                ValuesPerLevel = [0.20m, 0.50m, 1.00m]
            },
            new AscensionPerk
            {
                Id = "asc_legendary_reputation",
                NameKey = "AscLegendaryReputation",
                DescriptionKey = "AscLegendaryReputationDesc",
                Icon = "StarCircle",
                MaxLevel = 3,
                CostsPerLevel = [1, 2, 5],
                // Reputation startet bei 65/80/100 statt 50
                ValuesPerLevel = [65m, 80m, 100m]
            }
        ];
    }
}
