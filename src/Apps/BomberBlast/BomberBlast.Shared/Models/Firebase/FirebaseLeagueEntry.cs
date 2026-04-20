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

    /// <summary>
    /// Server-Timestamp in Millisekunden (Firebase ServerValue.TIMESTAMP).
    /// Beim Write: Dictionary-Sentinel <c>{".sv":"timestamp"}</c> setzen — Firebase löst
    /// das serverseitig zum Write-Zeitpunkt in die Server-Zeit in ms auf.
    /// Beim Read: Number (long). Wird clientseitig ignoriert — dient ausschließlich
    /// dem serverseitigen Rate-Limit (min. 60s zwischen Writes).
    /// </summary>
    [JsonPropertyName("updatedMs")]
    public object? UpdatedMs { get; set; }
}
