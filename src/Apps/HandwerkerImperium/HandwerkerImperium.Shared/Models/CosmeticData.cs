using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Kosmetische Daten (Themes, Skins). Rein visuell, keine Gameplay-Auswirkungen.
/// Extrahiert aus GameState (V5) für bessere Strukturierung.
/// </summary>
public sealed class CosmeticData
{
    [JsonPropertyName("unlockedCosmetics")]
    public List<string> UnlockedCosmetics { get; set; } = ["ct_default"];

    [JsonPropertyName("activeCityThemeId")]
    public string ActiveCityThemeId { get; set; } = "ct_default";

    [JsonPropertyName("activeWorkshopSkins")]
    public Dictionary<string, string> ActiveWorkshopSkins { get; set; } = new();
}
