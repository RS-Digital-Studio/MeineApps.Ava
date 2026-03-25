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

    /// <summary>Dauer des Durchlaufs in Ticks (0 = unbekannt, alte Einträge).</summary>
    [JsonPropertyName("runDurationTicks")]
    public long RunDurationTicks { get; set; }

    /// <summary>Dauer als TimeSpan (null bei alten Einträgen ohne Tracking).</summary>
    [JsonIgnore]
    public TimeSpan? RunDuration => RunDurationTicks > 0 ? TimeSpan.FromTicks(RunDurationTicks) : null;

    /// <summary>Aktive Challenges während dieses Durchlaufs (leer = keine).</summary>
    [JsonPropertyName("challenges")]
    public List<PrestigeChallengeType> Challenges { get; set; } = [];

    /// <summary>Bonus-PP aus Spielleistung (flat, nach Tier-Multi).</summary>
    [JsonPropertyName("bonusPp")]
    public int BonusPrestigePoints { get; set; }
}
