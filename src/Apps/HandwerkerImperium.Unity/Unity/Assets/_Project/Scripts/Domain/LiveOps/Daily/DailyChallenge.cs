using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Abstractions;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Eine einzelne tägliche Herausforderung mit Fortschritt und Belohnung.
    /// 1:1-Port aus dem Avalonia-Original (Models/DailyChallenge.cs). DailyChallengeType-Enum ist in
    /// LiveOpsEnums.cs (Schicht 10). Display-Felder (DisplayDescription/RewardDisplay/ProgressText)
    /// wandern in die Präsentationsschicht. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class DailyChallenge : IProgressProvider
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("type")]
        public DailyChallengeType Type { get; set; }

        [JsonProperty("targetValue")]
        public long TargetValue { get; set; }

        [JsonProperty("currentValue")]
        public long CurrentValue { get; set; }

        [JsonProperty("moneyReward")]
        public decimal MoneyReward { get; set; }

        [JsonProperty("xpReward")]
        public int XpReward { get; set; }

        [JsonProperty("goldenScrewReward")]
        public int GoldenScrewReward { get; set; }

        [JsonProperty("isCompleted")]
        public bool IsCompleted { get; set; }

        [JsonProperty("isClaimed")]
        public bool IsClaimed { get; set; }

        /// <summary>Ob der Spieler bereits per Rewarded Ad einen Retry genutzt hat (max 1x pro Challenge).</summary>
        [JsonProperty("hasRetriedWithAd")]
        public bool HasRetriedWithAd { get; set; }

        /// <summary>Ob ein Retry per Video-Ad möglich ist: Nicht geschafft, noch nicht genutzt, Fortschritt > 0.</summary>
        [JsonIgnore]
        public bool CanRetryWithAd => !IsCompleted && !HasRetriedWithAd && CurrentValue > 0;

        [JsonIgnore]
        public double Progress => TargetValue > 0 ? Math.Clamp((double)CurrentValue / TargetValue, 0, 1) : 0;

        [JsonIgnore]
        public bool HasGoldenScrewReward => GoldenScrewReward > 0;
    }

    /// <summary>
    /// Zustand aller täglichen Herausforderungen (gespeichert im GameState).
    /// 1:1-Port aus dem Avalonia-Original. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class DailyChallengeState
    {
        [JsonProperty("challenges")]
        public List<DailyChallenge> Challenges { get; set; } = new List<DailyChallenge>();

        [JsonProperty("lastResetDate")]
        public DateTime LastResetDate { get; set; } = DateTime.MinValue;

        [JsonProperty("allCompletedBonusClaimed")]
        public bool AllCompletedBonusClaimed { get; set; }
    }
}
