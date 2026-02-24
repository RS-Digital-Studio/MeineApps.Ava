using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Gilden-Daten wie sie in Firebase gespeichert werden.
/// Pfad: guilds/{guildId}
/// </summary>
public class FirebaseGuildData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "ShieldHome";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#D97706";

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    [JsonPropertyName("weeklyGoal")]
    public long WeeklyGoal { get; set; } = 500_000;

    [JsonPropertyName("weeklyProgress")]
    public long WeeklyProgress { get; set; }

    [JsonPropertyName("weekStartUtc")]
    public string WeekStartUtc { get; set; } = "";

    [JsonPropertyName("totalWeeksCompleted")]
    public int TotalWeeksCompleted { get; set; }

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";
}
