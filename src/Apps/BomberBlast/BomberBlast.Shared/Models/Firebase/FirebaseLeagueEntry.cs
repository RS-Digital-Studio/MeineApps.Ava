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

    /// <summary>
    /// Server-Timestamp in Millisekunden (Firebase ServerValue.TIMESTAMP).
    /// Beim Write: Dictionary-Sentinel <c>{".sv":"timestamp"}</c> setzen — Firebase löst
    /// das serverseitig zum Write-Zeitpunkt in die Server-Zeit in ms auf (nicht client-manipulierbar).
    /// Beim Read: Number (long). Einzige Source of Truth für "letzte Aktualisierung".
    /// Wird von Security-Rules für Rate-Limit verwendet (min. 60s zwischen Writes pro UID).
    /// <para>v2.0.34: UpdatedUtc-Feld (string, client-gesetzt) wurde entfernt. UpdatedMs ist
    /// jetzt das einzige Zeitstempel-Feld — server-authoritativ, nicht spoofbar.</para>
    /// </summary>
    [JsonPropertyName("updatedMs")]
    public object? UpdatedMs { get; set; }
}
