using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Mitglieds-Daten in Firebase.
/// Pfad: guild_members/{guildId}/{uid}
/// </summary>
public class FirebaseGuildMember
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("contribution")]
    public long Contribution { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = "member";

    [JsonPropertyName("playerLevel")]
    public int PlayerLevel { get; set; }

    [JsonPropertyName("joinedAt")]
    public string JoinedAt { get; set; } = "";

    // ── Neue Properties für Gilden-Überarbeitung ──

    /// <summary>Letzte Aktivität des Spielers (UTC ISO 8601).</summary>
    [JsonPropertyName("lastActiveAt")]
    public string LastActiveAt { get; set; } = "";

    /// <summary>Kriegs-Punkte in der aktuellen Woche.</summary>
    [JsonPropertyName("weeklyWarScore")]
    public long WeeklyWarScore { get; set; }

    /// <summary>Gesamte Kriegs-Punkte aller Saisons.</summary>
    [JsonPropertyName("totalWarScore")]
    public long TotalWarScore { get; set; }
}
