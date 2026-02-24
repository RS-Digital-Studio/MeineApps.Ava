using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Cosmetics;

/// <summary>
/// Stadthema das die Farben der City-Szene ändert (rein kosmetisch).
/// </summary>
public class CityTheme
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = "";

    [JsonPropertyName("skyColorDay")]
    public string SkyColorDay { get; set; } = "#87CEEB";

    [JsonPropertyName("skyColorNight")]
    public string SkyColorNight { get; set; } = "#1A1A3E";

    [JsonPropertyName("groundColor")]
    public string GroundColor { get; set; } = "#4A7C59";

    [JsonPropertyName("buildingTint")]
    public string BuildingTint { get; set; } = "#FFFFFF";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "City";

    /// <summary>
    /// Alle verfügbaren City-Themes.
    /// </summary>
    public static List<CityTheme> GetAllThemes() =>
    [
        new() { Id = "ct_default", NameKey = "CityThemeDefault", Icon = "City", SkyColorDay = "#87CEEB", SkyColorNight = "#1A1A3E", GroundColor = "#4A7C59", BuildingTint = "#FFFFFF" },
        new() { Id = "ct_sunset", NameKey = "CityThemeSunset", Icon = "WeatherSunset", SkyColorDay = "#FF6B35", SkyColorNight = "#2D1B69", GroundColor = "#8B6914", BuildingTint = "#FFE4B5" },
        new() { Id = "ct_arctic", NameKey = "CityThemeArctic", Icon = "Snowflake", SkyColorDay = "#B0E0E6", SkyColorNight = "#0D1B2A", GroundColor = "#E8E8E8", BuildingTint = "#D6EAF8" },
        new() { Id = "ct_desert", NameKey = "CityThemeDesert", Icon = "WhiteBalanceSunny", SkyColorDay = "#F4A460", SkyColorNight = "#1A0F00", GroundColor = "#DEB887", BuildingTint = "#FFDEAD" },
        new() { Id = "ct_neon", NameKey = "CityThemeNeon", Icon = "LightningBolt", SkyColorDay = "#1A1A2E", SkyColorNight = "#0F0F23", GroundColor = "#16213E", BuildingTint = "#E94560" },
        new() { Id = "ct_spring", NameKey = "CityThemeSpring", Icon = "Flower", SkyColorDay = "#98FB98", SkyColorNight = "#0B3D0B", GroundColor = "#228B22", BuildingTint = "#F0FFF0" },
    ];
}
