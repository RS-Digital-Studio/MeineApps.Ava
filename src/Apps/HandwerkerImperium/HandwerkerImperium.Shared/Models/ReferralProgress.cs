using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// (Friend-Invite Reward-Loop): Persistenter Tracking-State fuer das
/// Empfehlungs-System. Ein eingeladener Spieler ist "referred" wenn er den Invite-
/// Code beim ersten Start eingibt; nach 24h Aktivitaet zaehlt er zur SuccessfulReferrals.
///
/// 3-Tier-Reward bei 1/5/10 erfolgreichen Empfehlungen:
/// - 1: 50 GS + Achievement
/// - 5: 200 GS + exklusiver Workshop-Skin
/// - 10: 500 GS + Permanent +5% Income-Boost
/// </summary>
public sealed class ReferralProgress
{
    /// <summary>Der eigene Referral-Code (6-stellig). Wird einmalig beim ersten Start generiert.</summary>
    [JsonPropertyName("ownCode")]
    public string OwnCode { get; set; } = "";

    /// <summary>Anzahl Spieler die diesen Code beim ersten Start eingegeben und 24h gespielt haben.</summary>
    [JsonPropertyName("successfulReferrals")]
    public int SuccessfulReferrals { get; set; }

    /// <summary>Liste der eingeloesten Reward-Tiers (1, 5, 10) — verhindert doppeltes Auszahlen.</summary>
    [JsonPropertyName("claimedTiers")]
    public List<int> ClaimedTiers { get; set; } = [];

    /// <summary>Hat dieser Spieler selbst einen anderen Code eingegeben? (One-Shot beim ersten Start.)</summary>
    [JsonPropertyName("usedReferralCode")]
    public string? UsedReferralCode { get; set; }

    /// <summary>Permanenter Income-Bonus aus erreichten Tiers (max 5% bei 10 Empfehlungen).</summary>
    [JsonIgnore]
    public decimal PermanentIncomeBonus => ClaimedTiers.Contains(10) ? 0.05m : 0m;
}
