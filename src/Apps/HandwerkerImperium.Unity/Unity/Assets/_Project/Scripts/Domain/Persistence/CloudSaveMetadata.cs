using System;
using System.Globalization;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Persistence
{
    /// <summary>
    /// Header-Infos eines Cloud-Spielstands (ohne den vollen State). Dient der Konflikt-Auswahl
    /// zwischen lokalem und Cloud-Stand.
    /// 1:1-Port aus dem Avalonia-Original (Models/CloudSaveMetadata.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class CloudSaveMetadata
    {
        [JsonProperty("level")]
        public int PlayerLevel { get; set; }

        [JsonProperty("money")]
        public decimal Money { get; set; }

        [JsonProperty("goldenScrews")]
        public int GoldenScrews { get; set; }

        [JsonProperty("prestigePoints")]
        public decimal PrestigePoints { get; set; }

        [JsonProperty("ascensionLevel")]
        public int AscensionLevel { get; set; }

        [JsonProperty("savedAt")]
        public string SavedAtIso { get; set; } = "";

        [JsonProperty("version")]
        public int StateVersion { get; set; }

        [JsonProperty("appVersion")]
        public string AppVersion { get; set; } = "";

        /// <summary>Parst <see cref="SavedAtIso"/> als UTC-DateTime. DateTime.MinValue bei Parse-Fehlern.</summary>
        [JsonIgnore]
        public DateTime SavedAtUtc
        {
            get
            {
                if (string.IsNullOrEmpty(SavedAtIso)) return DateTime.MinValue;
                return DateTime.TryParse(
                    SavedAtIso,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var dt) ? dt : DateTime.MinValue;
            }
        }
    }
}
