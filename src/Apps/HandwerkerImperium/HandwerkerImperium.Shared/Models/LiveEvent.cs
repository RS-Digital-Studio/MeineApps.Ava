using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// AAA-Audit P1: Limited-Time-Event-Datenmodell. RemoteConfig-getrieben, dauert
/// 7 Tage, hat eigenen Reward-Track. Habby/Coin Master leben von solchen Events.
/// </summary>
public sealed class LiveEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("startsAtIso")]
    public string StartsAtIso { get; set; } = "";

    [JsonPropertyName("endsAtIso")]
    public string EndsAtIso { get; set; } = "";

    /// <summary>Punkte/Fortschritt im Event (Spieler sammelt durch Aktivitaet).</summary>
    [JsonPropertyName("playerScore")]
    public long PlayerScore { get; set; }

    /// <summary>Eingeloeste Belohnungs-Tiers (verhindert Doppelung).</summary>
    [JsonPropertyName("claimedRewardTiers")]
    public List<int> ClaimedRewardTiers { get; set; } = [];
}

/// <summary>4 Event-Templates (Audit-Empfehlung).</summary>
public enum LiveEventTemplate
{
    /// <summary>Doppelte Auftrags-Belohnungen.</summary>
    DoubleReward,
    /// <summary>Boss-Rush mit besonderem Spawn-Boost.</summary>
    BossRush,
    /// <summary>Co-op-Marathon: doppelte Co-op-Auftrags-Belohnungen.</summary>
    CoopMarathon,
    /// <summary>Mini-Game-Mastery: Perfekte Ratings geben Bonus-GS.</summary>
    MiniGameMastery,
}
