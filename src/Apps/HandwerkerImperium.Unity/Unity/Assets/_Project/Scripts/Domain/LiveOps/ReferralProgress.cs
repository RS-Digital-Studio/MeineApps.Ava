#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Persistenter Tracking-State für das Empfehlungs-System. 3-Tier-Reward bei 1/5/10
    /// erfolgreichen Empfehlungen (50/200/500 GS; ab 10 permanent +5% Income).
    /// 1:1-Port aus dem Avalonia-Original (Models/ReferralProgress.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class ReferralProgress
    {
        /// <summary>Der eigene Referral-Code (6-stellig). Einmalig beim ersten Start generiert.</summary>
        [JsonProperty("ownCode")]
        public string OwnCode { get; set; } = "";

        /// <summary>Anzahl Spieler die diesen Code eingegeben und 24h gespielt haben.</summary>
        [JsonProperty("successfulReferrals")]
        public int SuccessfulReferrals { get; set; }

        /// <summary>Liste der eingelösten Reward-Tiers (1, 5, 10) — verhindert doppeltes Auszahlen.</summary>
        [JsonProperty("claimedTiers")]
        public List<int> ClaimedTiers { get; set; } = new List<int>();

        /// <summary>Hat dieser Spieler selbst einen anderen Code eingegeben? (One-Shot beim ersten Start.)</summary>
        [JsonProperty("usedReferralCode")]
        public string? UsedReferralCode { get; set; }

        /// <summary>Permanenter Income-Bonus aus erreichten Tiers (max 5% bei 10 Empfehlungen).</summary>
        [JsonIgnore]
        public decimal PermanentIncomeBonus => ClaimedTiers.Contains(10) ? 0.05m : 0m;
    }
}
