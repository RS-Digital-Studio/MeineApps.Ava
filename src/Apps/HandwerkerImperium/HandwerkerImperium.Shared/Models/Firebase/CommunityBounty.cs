using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Globales Community-Ziel das alle Spieler gemeinsam erreichen.
/// </summary>
public class CommunityBounty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("target")]
    public long Target { get; set; }

    [JsonPropertyName("current")]
    public long Current { get; set; }

    [JsonPropertyName("reward")]
    public int Reward { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = "";

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active"; // "active" oder "completed"
}

/// <summary>
/// Individueller Beitrag eines Spielers zu einer Community-Bounty.
/// </summary>
public class BountyContribution
{
    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";
}
