using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Aufzeichnung eines einzelnen Prestige-Durchlaufs.
/// Wird in PrestigeData.History gespeichert (max. 20 Einträge).
/// </summary>
public class PrestigeHistoryEntry
{
    /// <summary>Gewählter Prestige-Tier.</summary>
    [JsonPropertyName("tier")]
    public PrestigeTier Tier { get; set; }

    /// <summary>Zeitpunkt des Prestiges (UTC).</summary>
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    /// <summary>Erhaltene Prestige-Punkte.</summary>
    [JsonPropertyName("points")]
    public int PointsEarned { get; set; }

    /// <summary>Spieler-Level beim Prestige.</summary>
    [JsonPropertyName("level")]
    public int PlayerLevel { get; set; }

    /// <summary>Permanenter Multiplikator nach diesem Prestige.</summary>
    [JsonPropertyName("multiplier")]
    public decimal MultiplierAfter { get; set; }

    /// <summary>Insgesamt verdientes Geld in diesem Durchlauf.</summary>
    [JsonPropertyName("moneyEarned")]
    public decimal TotalMoneyEarned { get; set; }
}
