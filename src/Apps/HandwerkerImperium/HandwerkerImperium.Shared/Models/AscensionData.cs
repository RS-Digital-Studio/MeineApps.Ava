using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Speichert alle Ascension-bezogenen Daten (Meta-Prestige).
/// Freigeschaltet nach 3x Legende Prestige.
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

    /// <summary>Gekaufte Perks mit Stufe: PerkId → Level (1-5).</summary>
    [JsonPropertyName("perks")]
    public Dictionary<string, int> Perks { get; set; } = new();

    /// <summary>Gibt die Stufe eines Perks zurück (0 = nicht gekauft).</summary>
    public int GetPerkLevel(string perkId)
    {
        return Perks.TryGetValue(perkId, out var level) ? level : 0;
    }
}
