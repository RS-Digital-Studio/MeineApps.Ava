using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Header-Infos eines Cloud-Spielstands (ohne den vollen State).
/// Dient der Konflikt-Auswahl zwischen lokalem und Cloud-Stand.
/// </summary>
public sealed class CloudSaveMetadata
{
    [JsonPropertyName("level")]
    public int PlayerLevel { get; set; }

    [JsonPropertyName("money")]
    public decimal Money { get; set; }

    [JsonPropertyName("goldenScrews")]
    public int GoldenScrews { get; set; }

    [JsonPropertyName("prestigePoints")]
    public decimal PrestigePoints { get; set; }

    [JsonPropertyName("ascensionLevel")]
    public int AscensionLevel { get; set; }

    [JsonPropertyName("savedAt")]
    public string SavedAtIso { get; set; } = "";

    [JsonPropertyName("version")]
    public int StateVersion { get; set; }

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = "";

    /// <summary>Parst <see cref="SavedAtIso"/> als UTC-DateTime. Liefert DateTime.MinValue bei Parse-Fehlern.</summary>
    [JsonIgnore]
    public DateTime SavedAtUtc
    {
        get
        {
            if (string.IsNullOrEmpty(SavedAtIso)) return DateTime.MinValue;
            return DateTime.TryParse(
                SavedAtIso,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var dt) ? dt : DateTime.MinValue;
        }
    }
}
