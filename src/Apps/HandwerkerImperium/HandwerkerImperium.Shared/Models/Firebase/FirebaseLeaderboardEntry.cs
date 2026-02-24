using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Ein Eintrag auf einem Firebase-Leaderboard.
/// </summary>
public class FirebaseLeaderboardEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("score")]
    public long Score { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("weekId")]
    public string? WeekId { get; set; }
}

/// <summary>
/// Öffentliches Spieler-Profil für Leaderboard-Anzeige.
/// </summary>
public class FirebasePlayerProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("prestigeTier")]
    public string PrestigeTier { get; set; } = "None";

    [JsonPropertyName("guildName")]
    public string? GuildName { get; set; }

    [JsonPropertyName("lastSeen")]
    public string LastSeen { get; set; } = "";
}
