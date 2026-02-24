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

    /// <summary>
    /// Alle 6 Ascension-Perks.
    /// </summary>
    public static List<AscensionPerk> GetAll()
    {
        return
        [
            new AscensionPerk
            {
                Id = "asc_start_capital",
                NameKey = "AscStartCapital",
                DescriptionKey = "AscStartCapitalDesc",
                Icon = "Bank",
                MaxLevel = 5,
                CostsPerLevel = [1, 2, 3, 5, 8],
                // +50%, +100%, +200%, +500%, +1000% Startgeld nach Prestige
                ValuesPerLevel = [0.50m, 1.00m, 2.00m, 5.00m, 10.00m]
            },
            new AscensionPerk
            {
                Id = "asc_eternal_tools",
                NameKey = "AscEternalTools",
                DescriptionKey = "AscEternalToolsDesc",
                Icon = "Wrench",
                MaxLevel = 5,
                CostsPerLevel = [2, 3, 5, 7, 10],
                // Meisterwerkzeuge bleiben ab Bronze(1), immer(2), +1 Tool(3), +2(4), alle(5)
                ValuesPerLevel = [1m, 2m, 3m, 4m, 5m]
            },
            new AscensionPerk
            {
                Id = "asc_quick_start",
                NameKey = "AscQuickStart",
                DescriptionKey = "AscQuickStartDesc",
                Icon = "RocketLaunch",
                MaxLevel = 5,
                CostsPerLevel = [1, 2, 4, 6, 10],
                // Start mit 2/3/4/5/alle Workshops freigeschaltet
                ValuesPerLevel = [2m, 3m, 4m, 5m, 8m]
            },
            new AscensionPerk
            {
                Id = "asc_timeless_research",
                NameKey = "AscTimelessResearch",
                DescriptionKey = "AscTimelessResearchDesc",
                Icon = "FlaskOutline",
                MaxLevel = 5,
                CostsPerLevel = [1, 2, 3, 4, 6],
                // Research-Dauer -10%/-20%/-30%/-40%/-50%
                ValuesPerLevel = [0.10m, 0.20m, 0.30m, 0.40m, 0.50m]
            },
            new AscensionPerk
            {
                Id = "asc_golden_era",
                NameKey = "AscGoldenEra",
                DescriptionKey = "AscGoldenEraDesc",
                Icon = "Screwdriver",
                MaxLevel = 5,
                CostsPerLevel = [1, 2, 3, 5, 8],
                // Goldschrauben-Verdienst +10%/+20%/+30%/+50%/+100%
                ValuesPerLevel = [0.10m, 0.20m, 0.30m, 0.50m, 1.00m]
            },
            new AscensionPerk
            {
                Id = "asc_legendary_reputation",
                NameKey = "AscLegendaryReputation",
                DescriptionKey = "AscLegendaryReputationDesc",
                Icon = "StarCircle",
                MaxLevel = 5,
                CostsPerLevel = [1, 2, 3, 4, 6],
                // Reputation startet bei 60/70/80/90/100 statt 50
                ValuesPerLevel = [60m, 70m, 80m, 90m, 100m]
            }
        ];
    }
}
