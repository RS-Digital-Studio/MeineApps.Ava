using System;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Ein Welcome-Back-Angebot das nach längerer Abwesenheit angezeigt wird.
    /// 1:1-Port aus dem Avalonia-Original (Models/WelcomeBackOffer.cs). Das WelcomeBackOfferType-Enum
    /// ist in LiveOpsEnums.cs (Schicht 10). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class WelcomeBackOffer
    {
        [JsonProperty("type")]
        public WelcomeBackOfferType Type { get; set; }

        [JsonProperty("goldenScrewReward")]
        public int GoldenScrewReward { get; set; }

        [JsonProperty("moneyReward")]
        public decimal MoneyReward { get; set; }

        [JsonProperty("xpReward")]
        public int XpReward { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        [JsonIgnore]
        public TimeSpan TimeRemaining => IsExpired ? TimeSpan.Zero : ExpiresAt - DateTime.UtcNow;
    }
}
