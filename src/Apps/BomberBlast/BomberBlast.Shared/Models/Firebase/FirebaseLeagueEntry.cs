using System.Text.Json.Serialization;

namespace BomberBlast.Models.Firebase;

/// <summary>
/// Ein Spieler-Eintrag in der Firebase-Liga-Rangliste.
/// Pfad: league/s{season}/{tier}/{uid}
/// </summary>
public class FirebaseLeagueEntry
{
    /// <summary>Anzeigename des Spielers.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Punkte in der aktuellen Saison.</summary>
    [JsonPropertyName("points")]
    public int Points { get; set; }

    /// <summary>Letzte Aktualisierung (ISO 8601 UTC).</summary>
    [JsonPropertyName("updatedUtc")]
    public string UpdatedUtc { get; set; } = "";
}
