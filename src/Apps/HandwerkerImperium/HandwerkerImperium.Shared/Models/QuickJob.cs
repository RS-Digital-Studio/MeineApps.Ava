using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Schnell-Auftrag: Direkter Minigame-Zugang mit kleiner Belohnung.
/// Rotiert alle 15 Minuten.
/// </summary>
public class QuickJob
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("workshopType")]
    public WorkshopType WorkshopType { get; set; }

    [JsonPropertyName("difficulty")]
    public OrderDifficulty Difficulty { get; set; } = OrderDifficulty.Easy;

    [JsonPropertyName("miniGameType")]
    public MiniGameType MiniGameType { get; set; }

    [JsonPropertyName("reward")]
    public decimal Reward { get; set; }

    [JsonPropertyName("xpReward")]
    public int XpReward { get; set; }

    [JsonPropertyName("titleKey")]
    public string TitleKey { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("isCompleted")]
    public bool IsCompleted { get; set; }

    /// <summary>Ob die Belohnung per Rewarded-Ad verdoppelt wurde.</summary>
    [JsonIgnore] public bool IsScoreDoubled { get; set; }

    // Display-Properties (nicht serialisiert)
    [JsonIgnore] public string DisplayTitle { get; set; } = string.Empty;
    [JsonIgnore] public string DisplayWorkshopName { get; set; } = string.Empty;
    [JsonIgnore] public string RewardDisplay { get; set; } = string.Empty;
}
