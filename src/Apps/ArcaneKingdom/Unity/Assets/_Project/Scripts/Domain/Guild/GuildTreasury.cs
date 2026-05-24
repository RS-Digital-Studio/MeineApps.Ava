#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Guild
{
    /// <summary>
    /// Gilden-Kasse: aggregierte Boni aus kontrollierten Gebieten (DESIGN 13.3).
    /// Wird taeglich um 00:00 UTC durch Cloud Function gefuellt.
    /// </summary>
    [Serializable]
    public sealed class GuildTreasury
    {
        public long Gold { get; set; }
        public long Diamonds { get; set; }
        public Dictionary<string, long> ScrapsByType { get; } = new();
        public DateTime LastTickAtUtc { get; set; }
    }

    public static class TerritoryBonusEngine
    {
        public sealed class DailyTickResult
        {
            public long AddedGold { get; init; }
            public long AddedDiamonds { get; init; }
            public IReadOnlyDictionary<string, long> AddedScraps { get; init; } = new Dictionary<string, long>();
        }

        public static DailyTickResult ComputeDailyBonus(IReadOnlyList<Territory> ownedTerritories, int memberCount)
        {
            if (memberCount < 1) memberCount = 1;
            var addedGold = 0L;
            var addedDiamonds = 0L;
            var addedScraps = new Dictionary<string, long>();
            foreach (var t in ownedTerritories)
            {
                addedGold += (long)t.DailyGoldPerMember * memberCount;
                addedDiamonds += (long)t.DailyDiamondsPerMember * memberCount;
                var scrapKind = t.Rarity switch
                {
                    TerritoryRarity.Rare => "Common",
                    TerritoryRarity.Epic => "Rare",
                    TerritoryRarity.Legendaer => "Epic",
                    _ => null
                };
                if (scrapKind != null)
                {
                    var scraps = (long)t.DailyScrapsPerMember * memberCount;
                    if (!addedScraps.ContainsKey(scrapKind)) addedScraps[scrapKind] = 0;
                    addedScraps[scrapKind] += scraps;
                }
            }
            return new DailyTickResult
            {
                AddedGold = addedGold,
                AddedDiamonds = addedDiamonds,
                AddedScraps = addedScraps
            };
        }

        public static void Apply(GuildTreasury treasury, DailyTickResult tick, DateTime nowUtc)
        {
            treasury.Gold += tick.AddedGold;
            treasury.Diamonds += tick.AddedDiamonds;
            foreach (var kv in tick.AddedScraps)
            {
                if (!treasury.ScrapsByType.ContainsKey(kv.Key)) treasury.ScrapsByType[kv.Key] = 0;
                treasury.ScrapsByType[kv.Key] += kv.Value;
            }
            treasury.LastTickAtUtc = nowUtc;
        }
    }
}
