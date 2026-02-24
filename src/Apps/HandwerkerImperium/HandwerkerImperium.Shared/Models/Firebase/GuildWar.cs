using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Daten eines Gilden-Kriegs (wöchentlicher Wettbewerb zwischen zwei Gilden).
/// </summary>
public class GuildWar
{
    [JsonPropertyName("guildAId")]
    public string GuildAId { get; set; } = "";

    [JsonPropertyName("guildBId")]
    public string GuildBId { get; set; } = "";

    [JsonPropertyName("guildAName")]
    public string GuildAName { get; set; } = "";

    [JsonPropertyName("guildBName")]
    public string GuildBName { get; set; } = "";

    [JsonPropertyName("scoreA")]
    public long ScoreA { get; set; }

    [JsonPropertyName("scoreB")]
    public long ScoreB { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = "";

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active"; // "active" oder "completed"
}

/// <summary>
/// Individueller Beitrag eines Spielers in einem Gilden-Krieg.
/// </summary>
public class GuildWarScore
{
    [JsonPropertyName("score")]
    public long Score { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";
}
