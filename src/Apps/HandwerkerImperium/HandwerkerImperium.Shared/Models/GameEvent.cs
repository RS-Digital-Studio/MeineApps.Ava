using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// A random or seasonal event that temporarily modifies game parameters.
/// </summary>
public class GameEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    public GameEventType Type { get; set; }

    [JsonPropertyName("nameKey")]
    public string NameKey => Type.GetLocalizationKey();

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey => Type.GetDescriptionKey();

    [JsonIgnore]
    public string Icon => Type.GetIcon();

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("durationTicks")]
    public long DurationTicks { get; set; }

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);

    [JsonIgnore]
    public bool IsActive => DateTime.UtcNow < StartedAt + Duration;

    [JsonIgnore]
    public TimeSpan RemainingTime
    {
        get
        {
            var remaining = StartedAt + Duration - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    [JsonPropertyName("effect")]
    public GameEventEffect Effect { get; set; } = new();

    public static GameEvent Create(GameEventType type)
    {
        var evt = new GameEvent
        {
            Type = type,
            StartedAt = DateTime.UtcNow,
            DurationTicks = type.GetDefaultDuration().Ticks,
            Effect = GetDefaultEffect(type)
        };
        return evt;
    }

    private static GameEventEffect GetDefaultEffect(GameEventType type) => type switch
    {
        GameEventType.MaterialSale => new GameEventEffect { CostMultiplier = 0.7m },
        GameEventType.MaterialShortage => new GameEventEffect { CostMultiplier = 1.5m },
        GameEventType.HighDemand => new GameEventEffect { RewardMultiplier = 1.5m },
        GameEventType.EconomicDownturn => new GameEventEffect { RewardMultiplier = 0.7m, ReputationChange = 2m },
        GameEventType.TaxAudit => new GameEventEffect { SpecialEffect = "tax_10_percent" },
        GameEventType.WorkerStrike => new GameEventEffect { SpecialEffect = "mood_drop_all_20" },
        GameEventType.InnovationFair => new GameEventEffect { IncomeMultiplier = 1.3m },
        GameEventType.CelebrityEndorsement => new GameEventEffect { IncomeMultiplier = 1.2m, ReputationChange = 5m },
        GameEventType.SpringSeason => new GameEventEffect { IncomeMultiplier = 1.15m },
        GameEventType.SummerBoom => new GameEventEffect { RewardMultiplier = 1.2m },
        GameEventType.AutumnSurge => new GameEventEffect { IncomeMultiplier = 1.1m, RewardMultiplier = 1.1m },
        GameEventType.WinterSlowdown => new GameEventEffect { IncomeMultiplier = 0.9m },
        _ => new GameEventEffect()
    };
}
