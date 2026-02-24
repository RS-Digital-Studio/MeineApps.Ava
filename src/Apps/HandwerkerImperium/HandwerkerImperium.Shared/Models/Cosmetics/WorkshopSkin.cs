using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Cosmetics;

/// <summary>
/// Workshop-Skin der das Aussehen eines Workshop-Typs ändert (rein kosmetisch).
/// </summary>
public class WorkshopSkin
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = "";

    [JsonPropertyName("workshopType")]
    public string WorkshopType { get; set; } = "";

    [JsonPropertyName("colorOverride")]
    public string ColorOverride { get; set; } = "";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "Palette";

    /// <summary>
    /// Alle verfügbaren Workshop-Skins.
    /// </summary>
    public static List<WorkshopSkin> GetAllSkins() =>
    [
        new() { Id = "ws_carpenter_gold", NameKey = "SkinCarpenterGold", WorkshopType = "Carpenter", ColorOverride = "#FFD700", Icon = "Star" },
        new() { Id = "ws_plumber_chrome", NameKey = "SkinPlumberChrome", WorkshopType = "Plumber", ColorOverride = "#C0C0C0", Icon = "DiamondStone" },
        new() { Id = "ws_electrician_neon", NameKey = "SkinElectricianNeon", WorkshopType = "Electrician", ColorOverride = "#00FF88", Icon = "LightningBolt" },
        new() { Id = "ws_painter_rainbow", NameKey = "SkinPainterRainbow", WorkshopType = "Painter", ColorOverride = "#FF69B4", Icon = "Palette" },
        new() { Id = "ws_roofer_ruby", NameKey = "SkinRooferRuby", WorkshopType = "Roofer", ColorOverride = "#E0115F", Icon = "DiamondStone" },
        new() { Id = "ws_contractor_obsidian", NameKey = "SkinContractorObsidian", WorkshopType = "Contractor", ColorOverride = "#1A1A2E", Icon = "Shield" },
    ];
}
