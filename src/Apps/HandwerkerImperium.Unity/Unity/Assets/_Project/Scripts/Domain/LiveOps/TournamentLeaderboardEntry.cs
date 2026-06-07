using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Ein Eintrag in der Turnier-Bestenliste.
    /// 1:1-Port aus dem Avalonia-Original (Models/Tournament.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class TournamentLeaderboardEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("isPlayer")]
        public bool IsPlayer { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }
    }
}
