using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Belohnung auf einem Battle-Pass-Tier.
    /// 1:1-Port aus dem Avalonia-Original (Models/BattlePass.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class BattlePassReward
    {
        [JsonProperty("tier")]
        public int Tier { get; set; }

        [JsonProperty("isFree")]
        public bool IsFree { get; set; }

        [JsonProperty("moneyReward")]
        public decimal MoneyReward { get; set; }

        [JsonProperty("xpReward")]
        public int XpReward { get; set; }

        [JsonProperty("goldenScrewReward")]
        public int GoldenScrewReward { get; set; }

        [JsonProperty("rewardType")]
        public BattlePassRewardType RewardType { get; set; } = BattlePassRewardType.Standard;

        [JsonProperty("descriptionKey")]
        public string DescriptionKey { get; set; } = "";

        /// <summary>Dauer des SpeedBoosts in Minuten (nur bei RewardType == SpeedBoost).</summary>
        [JsonProperty("speedBoostMinutes")]
        public int SpeedBoostMinutes { get; set; }
    }
}
