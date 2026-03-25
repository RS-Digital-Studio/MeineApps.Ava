using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Speichert alle Ascension-bezogenen Daten (Meta-Prestige).
/// Reserviert für zukünftige Ascension-Funktionalität.
/// Klasse wird persistiert (JSON) - nicht löschen wegen Save-Kompatibilität.
/// </summary>
public class AscensionData
{
    /// <summary>Anzahl der durchgeführten Ascensions.</summary>
    [JsonPropertyName("ascensionLevel")]
    public int AscensionLevel { get; set; }

    /// <summary>Verfügbare Ascension-Punkte zum Ausgeben.</summary>
    [JsonPropertyName("ascensionPoints")]
    public int AscensionPoints { get; set; }

    /// <summary>Gesamt verdiente Ascension-Punkte (Lifetime).</summary>
    [JsonPropertyName("totalAscensionPoints")]
    public int TotalAscensionPoints { get; set; }

    /// <summary>Gekaufte Perks mit Stufe: PerkId → Level (1-3). Alte Saves mit Level >3 werden geclampt.</summary>
    [JsonPropertyName("perks")]
    public Dictionary<string, int> Perks { get; set; } = new();

    /// <summary>Zeitpunkt der letzten Ascension (UTC).</summary>
    [JsonPropertyName("lastAscensionDate")]
    public DateTime LastAscensionDate { get; set; } = DateTime.MinValue;

    /// <summary>Gibt die Stufe eines Perks zurück (0 = nicht gekauft).</summary>
    public int GetPerkLevel(string perkId)
    {
        return Perks.TryGetValue(perkId, out var level) ? level : 0;
    }
}
