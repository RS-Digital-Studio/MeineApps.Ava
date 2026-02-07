using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// A loyal customer who provides bonus rewards on orders.
/// Becomes regular after 5 perfect orders.
/// </summary>
public class RegularCustomer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("preferredWorkshop")]
    public WorkshopType PreferredWorkshop { get; set; }

    /// <summary>
    /// Number of perfect orders completed. 5 = becomes regular.
    /// </summary>
    [JsonPropertyName("perfectOrderCount")]
    public int PerfectOrderCount { get; set; }

    /// <summary>
    /// Bonus multiplier for orders from this customer (1.1 = +10%).
    /// </summary>
    [JsonPropertyName("bonusMultiplier")]
    public decimal BonusMultiplier { get; set; } = 1.1m;

    [JsonPropertyName("lastOrder")]
    public DateTime LastOrder { get; set; }

    /// <summary>
    /// Seed for deterministic avatar generation.
    /// </summary>
    [JsonPropertyName("avatarSeed")]
    public string AvatarSeed { get; set; } = Guid.NewGuid().ToString()[..8];

    [JsonIgnore]
    public bool IsRegular => PerfectOrderCount >= 5;
}
