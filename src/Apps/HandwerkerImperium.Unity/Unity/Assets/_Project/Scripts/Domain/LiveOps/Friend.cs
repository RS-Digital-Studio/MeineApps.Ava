using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Ein simulierter Freund der täglich kleine Geschenke sendet.
    /// 1:1-Port aus dem Avalonia-Original (Models/Friend.cs). Persistenz: Newtonsoft.Json.
    /// Hinweis: <see cref="CreateSimulatedFriends"/> nahm im Original Random.Shared (nicht in Unity/
    /// netstandard2.1) — hier wird eine deterministische <see cref="System.Random"/>-Instanz übergeben.
    /// </summary>
    public class Friend
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("name")]
        public string Name { get; set; } = "";

        [JsonProperty("level")]
        public int Level { get; set; } = 1;

        [JsonProperty("lastGiftSent")]
        public DateTime LastGiftSent { get; set; } = DateTime.MinValue;

        [JsonProperty("lastGiftReceived")]
        public DateTime LastGiftReceived { get; set; } = DateTime.MinValue;

        /// <summary>Freundschafts-Level (1-5, steigt durch Geschenke).</summary>
        [JsonProperty("friendshipLevel")]
        public int FriendshipLevel { get; set; } = 1;

        /// <summary>Ob heute ein Geschenk verfügbar ist.</summary>
        [JsonIgnore]
        public bool HasGiftAvailable => LastGiftSent.Date < DateTime.UtcNow.Date;

        /// <summary>Ob heute bereits zurückgeschenkt wurde.</summary>
        [JsonIgnore]
        public bool HasSentGiftToday => LastGiftReceived.Date >= DateTime.UtcNow.Date;

        /// <summary>Goldschrauben-Geschenk basierend auf Freundschafts-Level.</summary>
        [JsonIgnore]
        public int GiftAmount => FriendshipLevel switch
        {
            1 => 1,
            2 => 1,
            3 => 2,
            4 => 2,
            _ => 3
        };

        /// <summary>
        /// Erstellt 5 simulierte Freunde. <paramref name="rng"/> liefert die Level-Streuung
        /// (deterministisch je Spielstand; ersetzt Random.Shared des Originals).
        /// </summary>
        public static List<Friend> CreateSimulatedFriends(Random rng)
        {
            var names = new[] { "MaxBuilder", "LisaCraft", "TomHammer", "SarahPro", "OttoMeister" };
            var friends = new List<Friend>();

            for (int i = 0; i < 5; i++)
            {
                friends.Add(new Friend
                {
                    Id = $"friend_{i}",
                    Name = names[i],
                    Level = rng.Next(5, 50),
                    FriendshipLevel = 1
                });
            }

            return friends;
        }
    }
}
