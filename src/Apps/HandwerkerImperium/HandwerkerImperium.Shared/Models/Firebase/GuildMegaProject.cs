using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// V7 (Phase 4 Ressourcen-Plan, Plan Section 3.9): Mega-Projekt-Template.
/// </summary>
public enum GuildMegaProjectType
{
    /// <summary>Gilden-Kathedrale — mittlere Schwierigkeit, ~2 Wochen.</summary>
    Cathedral = 0,

    /// <summary>Gilden-Hauptquartier — End-Game-Ziel, ~4 Wochen.</summary>
    Headquarters = 1
}

/// <summary>
/// V7 (Phase 4 Ressourcen-Plan): Statisches Template einer Mega-Projekt-Anforderung.
/// Verbraucht Materialien aller Tiers ueber Wochen, belohnt mit permanentem Gildenbonus.
/// </summary>
public static class GuildMegaProjectTemplates
{
    /// <summary>Soft-Cap: ab diesem Tag wird ein verlassenes Projekt mit Refund gesonnsuet (Plan Section 4 Risiken).</summary>
    public const int AbandonmentSunsetDays = 30;

    /// <summary>Liefert die Material-Anforderung eines Mega-Projekt-Typs.</summary>
    public static Dictionary<string, int> GetRequirements(GuildMegaProjectType type) => type switch
    {
        GuildMegaProjectType.Cathedral => new Dictionary<string, int>
        {
            ["luxury_furniture"] = 50,
            ["roof_structure"] = 40,
            ["artwork"] = 30,
            ["smart_home"] = 20,
            ["villa"] = 1,
        },
        GuildMegaProjectType.Headquarters => new Dictionary<string, int>
        {
            ["skyscraper_frame"] = 80,
            ["smart_home"] = 60,
            ["bathroom_installation"] = 50,
            ["master_blueprint"] = 30,
            ["masterpiece_fittings"] = 30,
            ["villa"] = 2,
            ["skyscraper"] = 1,
        },
        _ => new Dictionary<string, int>()
    };

    /// <summary>Permanenter Gilden-Bonus den das Projekt freischaltet.</summary>
    public static GuildMegaProjectReward GetReward(GuildMegaProjectType type) => type switch
    {
        GuildMegaProjectType.Cathedral => new GuildMegaProjectReward
        {
            CraftingSpeedBonus = 0.05m,
            AutoSellPriceBonus = 0.10m,
            BonusWarehouseSlots = 3
        },
        GuildMegaProjectType.Headquarters => new GuildMegaProjectReward
        {
            CraftingSpeedBonus = 0.10m,
            AutoSellPriceBonus = 0.20m,
            BonusWarehouseSlots = 5
        },
        _ => new GuildMegaProjectReward()
    };

    public static string GetNameKey(GuildMegaProjectType type) => type switch
    {
        GuildMegaProjectType.Cathedral => "GuildMegaProjectCathedral",
        GuildMegaProjectType.Headquarters => "GuildMegaProjectHeadquarters",
        _ => "GuildMegaProject"
    };
}

/// <summary>
/// Permanenter Gilden-Bonus den ein abgeschlossenes Mega-Projekt freischaltet.
/// Wird nach Abschluss als GuildMembership-Bonus angerechnet.
/// </summary>
public sealed class GuildMegaProjectReward
{
    /// <summary>Permanenter +X Crafting-Speed-Bonus.</summary>
    [JsonPropertyName("craftingSpeedBonus")]
    public decimal CraftingSpeedBonus { get; set; }

    /// <summary>Permanenter +X auf Auto-Verkaufs-Preis.</summary>
    [JsonPropertyName("autoSellPriceBonus")]
    public decimal AutoSellPriceBonus { get; set; }

    /// <summary>Permanente +X Lager-Slots.</summary>
    [JsonPropertyName("bonusWarehouseSlots")]
    public int BonusWarehouseSlots { get; set; }
}

/// <summary>
/// V7 (Phase 4 Ressourcen-Plan): Aktuelles Mega-Projekt einer Gilde (Firebase-Pfad
/// <c>guilds/{guildId}/megaProjects/{projectId}</c>).
/// </summary>
public sealed class GuildMegaProject
{
    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = "";

    [JsonPropertyName("type")]
    public GuildMegaProjectType Type { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("contributions")]
    public Dictionary<string, int> Contributions { get; set; } = new();

    [JsonPropertyName("donations")]
    public Dictionary<string, GuildMegaProjectDonation> Donations { get; set; } = new();

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>HMAC-SHA256 ueber stabile Felder (ProjectId, Type, CreatedAt).</summary>
    [JsonPropertyName("hmac")]
    public string? Hmac { get; set; }

    [JsonIgnore]
    public bool IsCompleted => CompletedAt.HasValue;
}

/// <summary>
/// Donation-Eintrag eines Spielers im Mega-Projekt (Top-Spender-Leaderboard).
/// </summary>
public sealed class GuildMegaProjectDonation
{
    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = "";

    [JsonPropertyName("totalValue")]
    public decimal TotalValue { get; set; }

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }

    [JsonPropertyName("lastDonatedAt")]
    public DateTime LastDonatedAt { get; set; } = DateTime.UtcNow;
}
