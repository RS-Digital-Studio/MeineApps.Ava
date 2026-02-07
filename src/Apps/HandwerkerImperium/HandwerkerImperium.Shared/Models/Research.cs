using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// A single research node in the skill tree.
/// </summary>
public class Research
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("branch")]
    public ResearchBranch Branch { get; set; }

    /// <summary>
    /// Level within the branch (1-15).
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = string.Empty;

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = string.Empty;

    [JsonPropertyName("cost")]
    public decimal Cost { get; set; }

    /// <summary>
    /// Real-time duration to complete the research.
    /// </summary>
    [JsonPropertyName("durationTicks")]
    public long DurationTicks { get; set; }

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);

    [JsonPropertyName("isResearched")]
    public bool IsResearched { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("effect")]
    public ResearchEffect Effect { get; set; } = new();

    /// <summary>
    /// IDs of prerequisite research nodes.
    /// </summary>
    [JsonPropertyName("prerequisites")]
    public List<string> Prerequisites { get; set; } = [];

    /// <summary>
    /// Remaining time until research completes.
    /// </summary>
    [JsonIgnore]
    public TimeSpan? RemainingTime
    {
        get
        {
            if (!IsActive || StartedAt == null) return null;
            var elapsed = DateTime.UtcNow - StartedAt.Value;
            var remaining = Duration - elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Goldschrauben-Kosten fuer Sofortfertigstellung (ab Level 8).
    /// </summary>
    [JsonIgnore]
    public int InstantFinishScrewCost => Level switch
    {
        >= 15 => 120,
        >= 14 => 100,
        >= 13 => 80,
        >= 12 => 60,
        >= 11 => 40,
        >= 10 => 25,
        >= 9 => 15,
        >= 8 => 10,
        _ => 0
    };

    /// <summary>
    /// Kann mit Goldschrauben sofort abgeschlossen werden (nur ab Level 8).
    /// </summary>
    [JsonIgnore]
    public bool CanInstantFinish => IsActive && InstantFinishScrewCost > 0;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    [JsonIgnore]
    public double Progress
    {
        get
        {
            if (IsResearched) return 100.0;
            if (!IsActive || StartedAt == null) return 0.0;
            var elapsed = DateTime.UtcNow - StartedAt.Value;
            return Math.Clamp(elapsed / Duration * 100.0, 0.0, 100.0);
        }
    }
}
