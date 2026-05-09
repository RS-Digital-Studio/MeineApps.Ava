using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Status eines Co-op-Auftrags zwischen zwei Gildenmitgliedern (v2.1.0).
/// </summary>
public enum CoopOrderStatus
{
    /// <summary>Eingeladener Spieler hat noch nicht geantwortet (5min Timeout).</summary>
    Pending,
    /// <summary>Beide Spieler aktiv im MiniGame.</summary>
    Active,
    /// <summary>Beide haben fertig — Reward verteilt.</summary>
    Completed,
    /// <summary>Pending-Timeout abgelaufen oder einer hat abgelehnt.</summary>
    Expired
}

/// <summary>
/// Firebase-State eines Co-op-Auftrags (v2.1.0).
/// Pfad: <c>guilds/{guildId}/coopOrders/{orderId}</c>.
/// HMAC-Signatur via GameIntegrityService verhindert Score-Cheating.
/// </summary>
public sealed class CoopOrderState
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = "";

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = "";

    [JsonPropertyName("invitedPlayer")]
    public string InvitedPlayer { get; set; } = "";

    [JsonPropertyName("status")]
    public CoopOrderStatus Status { get; set; } = CoopOrderStatus.Pending;

    /// <summary>
    /// Ablauf-Zeit. Bei Pending: Annahme-Frist (5min). Bei Active: MiniGame-Deadline.
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("miniGameType")]
    public MiniGameType MiniGameType { get; set; }

    /// <summary>Score von Spieler 1 (createdBy). null = noch nicht abgeschlossen.</summary>
    [JsonPropertyName("player1Score")]
    public int? Player1Score { get; set; }

    /// <summary>Score von Spieler 2 (invitedPlayer). null = noch nicht abgeschlossen.</summary>
    [JsonPropertyName("player2Score")]
    public int? Player2Score { get; set; }

    /// <summary>Reward-Split (Default 0.5 = 50/50). +25% Bonus bei beidseitigem Perfect.</summary>
    [JsonPropertyName("rewardSplit")]
    public double RewardSplit { get; set; } = 0.5;

    /// <summary>Basis-Reward in EUR (gesamt, vor Split).</summary>
    [JsonPropertyName("baseReward")]
    public decimal BaseReward { get; set; }

    /// <summary>HMAC-Signatur gegen Score-Cheating.</summary>
    [JsonPropertyName("hmac")]
    public string? Hmac { get; set; }
}
