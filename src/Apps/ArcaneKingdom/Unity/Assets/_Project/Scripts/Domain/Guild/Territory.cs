#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Guild
{
    /// <summary>
    /// Gebiete auf der Gilden-Weltkarte (DESIGN.md Kap. 13).
    /// </summary>
    public enum TerritoryRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendaer = 3
    }

    [Serializable]
    public sealed class Territory
    {
        public string Id { get; }
        public string DisplayNameKey { get; }
        public TerritoryRarity Rarity { get; }
        public string? OwnerGuildId { get; set; }
        public DateTime? CapturedAtUtc { get; set; }
        public Dictionary<string, long> ActiveBids { get; }   // guildId -> goldAmount
        public DateTime? BidPhaseEndsAtUtc { get; set; }
        public DateTime? NextMatchAtUtc { get; set; }

        public Territory(string id, string displayNameKey, TerritoryRarity rarity)
        {
            Id = id;
            DisplayNameKey = displayNameKey;
            Rarity = rarity;
            ActiveBids = new Dictionary<string, long>();
        }

        /// <summary>
        /// Täglicher Gold-Bonus pro Mitglied (DESIGN.md Kap. 13.3).
        /// </summary>
        public int DailyGoldPerMember => Rarity switch
        {
            TerritoryRarity.Common => 1_000,
            TerritoryRarity.Rare => 3_000,
            TerritoryRarity.Epic => 8_000,
            TerritoryRarity.Legendaer => 20_000,
            _ => 0
        };

        public int DailyScrapsPerMember => Rarity switch
        {
            TerritoryRarity.Common => 0,
            TerritoryRarity.Rare => 2,
            TerritoryRarity.Epic => 2,
            TerritoryRarity.Legendaer => 1,
            _ => 0
        };

        public int DailyDiamondsPerMember => Rarity switch
        {
            TerritoryRarity.Epic => 50,
            TerritoryRarity.Legendaer => 100,
            _ => 0
        };

        public long MinBidAmount => Rarity switch
        {
            TerritoryRarity.Common => 50_000,
            TerritoryRarity.Rare => 200_000,
            TerritoryRarity.Epic => 500_000,
            TerritoryRarity.Legendaer => 1_500_000,
            _ => 0
        };
    }
}
