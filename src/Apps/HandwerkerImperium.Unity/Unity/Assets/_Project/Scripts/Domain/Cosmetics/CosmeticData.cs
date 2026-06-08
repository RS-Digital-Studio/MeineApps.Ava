using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Cosmetics
{
    /// <summary>
    /// Kosmetische Daten (Themes, Skins). Rein visuell, keine Gameplay-Auswirkungen.
    /// 1:1-Port aus dem Avalonia-Original (Models/CosmeticData.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class CosmeticData
    {
        [JsonProperty("unlockedCosmetics")]
        public List<string> UnlockedCosmetics { get; set; } = new List<string> { "ct_default" };

        [JsonProperty("activeCityThemeId")]
        public string ActiveCityThemeId { get; set; } = "ct_default";

        [JsonProperty("activeWorkshopSkins")]
        public Dictionary<string, string> ActiveWorkshopSkins { get; set; } = new Dictionary<string, string>();
    }
}
