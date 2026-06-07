using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Abstractions;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Eine einzelne wöchentliche Mission.
    /// 1:1-Port aus dem Avalonia-Original (Models/WeeklyMission.cs). WeeklyMissionType-Enum ist in
    /// LiveOpsEnums.cs (Schicht 10). Display-Felder wandern in die Präsentationsschicht. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class WeeklyMission : IProgressProvider
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("type")]
        public WeeklyMissionType Type { get; set; }

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

        [JsonProperty("isClaimed")]
        public bool IsClaimed { get; set; }

        [JsonIgnore]
        public bool IsCompleted => CurrentValue >= TargetValue;

        [JsonIgnore]
        public double Progress => TargetValue > 0 ? Math.Clamp((double)CurrentValue / TargetValue, 0.0, 1.0) : 0.0;

        [JsonIgnore]
        public bool HasGoldenScrewReward => GoldenScrewReward > 0;
    }

    /// <summary>
    /// Zustand aller wöchentlichen Missionen. 1:1-Port aus dem Avalonia-Original. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class WeeklyMissionState
    {
        [JsonProperty("missions")]
        public List<WeeklyMission> Missions { get; set; } = new List<WeeklyMission>();

        /// <summary>Letzter Montag-Reset (UTC).</summary>
        [JsonProperty("lastWeeklyReset")]
        public DateTime LastWeeklyReset { get; set; } = DateTime.MinValue;

        /// <summary>Bonus wenn alle 5 Missionen abgeschlossen (50 Goldschrauben).</summary>
        [JsonProperty("allCompletedBonusClaimed")]
        public bool AllCompletedBonusClaimed { get; set; }
    }
}
