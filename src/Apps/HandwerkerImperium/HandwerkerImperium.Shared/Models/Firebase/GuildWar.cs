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

    // ── Neue Properties für Saison-System ──

    /// <summary>Level von Gilde A (für Anzeige ohne zusätzlichen Firebase-Abruf).</summary>
    [JsonPropertyName("guildALevel")]
    public int GuildALevel { get; set; }

    /// <summary>Level von Gilde B (für Anzeige ohne zusätzlichen Firebase-Abruf).</summary>
    [JsonPropertyName("guildBLevel")]
    public int GuildBLevel { get; set; }

    /// <summary>Aktuelle Kriegsphase: "attack", "defense", "evaluation", "completed".</summary>
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "attack";

    /// <summary>Wann die aktuelle Phase endet (UTC ISO 8601).</summary>
    [JsonPropertyName("phaseEndsAt")]
    public string PhaseEndsAt { get; set; } = "";
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
