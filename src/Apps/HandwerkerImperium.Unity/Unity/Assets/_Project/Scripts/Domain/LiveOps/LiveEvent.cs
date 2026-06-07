using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Limited-Time-Event-Datenmodell (RemoteConfig-getrieben, 7 Tage, eigener Reward-Track).
    /// 1:1-Port aus dem Avalonia-Original (Models/LiveEvent.cs). Das LiveEventTemplate-Enum ist in
    /// LiveOpsEnums.cs (Schicht 10). Persistenz: Newtonsoft.Json.
    /// </summary>
    public sealed class LiveEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("templateId")]
        public string TemplateId { get; set; } = "";

        [JsonProperty("startsAtIso")]
        public string StartsAtIso { get; set; } = "";

        [JsonProperty("endsAtIso")]
        public string EndsAtIso { get; set; } = "";

        /// <summary>Punkte/Fortschritt im Event (Spieler sammelt durch Aktivität).</summary>
        [JsonProperty("playerScore")]
        public long PlayerScore { get; set; }

        /// <summary>Eingelöste Belohnungs-Tiers (verhindert Doppelung).</summary>
        [JsonProperty("claimedRewardTiers")]
        public List<int> ClaimedRewardTiers { get; set; } = new List<int>();
    }
}
