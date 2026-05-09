using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models.Firebase;

/// <summary>
/// Status einer Worker-Markt-Auktion (v2.1.0, Sprint 3 Big Bet).
/// </summary>
public enum WorkerAuctionStatus
{
    /// <summary>30s Vorwarnung — Bid-Phase startet bald.</summary>
    Warming,
    /// <summary>Aktive 30s-Bid-Phase.</summary>
    Active,
    /// <summary>Hoechstbieter erhaelt Worker, Verlierer bekommen Geld zurueck.</summary>
    Settled
}

/// <summary>
/// Firebase-State einer Worker-Markt-Auktion (v2.1.0).
/// Pfad: <c>guilds/{guildId}/auctions/{auctionId}</c>.
/// Solo-Spieler bieten gegen NPC-Bots — Multiplayer-Spieler bieten gegen Gildenmitglieder.
/// </summary>
public sealed class WorkerAuctionState
{
    [JsonPropertyName("auctionId")]
    public string AuctionId { get; set; } = "";

    /// <summary>S/SS/SSS-Tier-Worker — generiert mit zufaelligen Stats beim Auktions-Start.</summary>
    [JsonPropertyName("workerTier")]
    public WorkerTier WorkerTier { get; set; }

    [JsonPropertyName("workerName")]
    public string WorkerName { get; set; } = "";

    [JsonPropertyName("status")]
    public WorkerAuctionStatus Status { get; set; } = WorkerAuctionStatus.Warming;

    /// <summary>Zeitpunkt zu dem die Auktion endet (active-Phase).</summary>
    [JsonPropertyName("endsAt")]
    public DateTime EndsAt { get; set; }

    /// <summary>Aktueller Hoechstbieter — PlayerId oder Bot-ID.</summary>
    [JsonPropertyName("highestBidderId")]
    public string? HighestBidderId { get; set; }

    /// <summary>Aktuelles Hoechstgebot.</summary>
    [JsonPropertyName("highestBid")]
    public decimal HighestBid { get; set; }

    /// <summary>
    /// Liste aller Bieter mit ihrem hoechsten Gebot (fuer Refund-Berechnung am Ende).
    /// Key = PlayerId/BotId, Value = Hoechstgebot.
    /// </summary>
    [JsonPropertyName("allBids")]
    public Dictionary<string, decimal> AllBids { get; set; } = new();

    /// <summary>HMAC-Signatur gegen Bid-Manipulation.</summary>
    [JsonPropertyName("hmac")]
    public string? Hmac { get; set; }
}
