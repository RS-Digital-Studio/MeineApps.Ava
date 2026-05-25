#nullable enable
using ArcaneKingdom.Domain.Economy;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Upgrade-Kosten-Tabelle für Karten-Leveling (DESIGN.md Kapitel 5.3).
    /// </summary>
    public static class CardUpgradeCurve
    {
        public readonly struct LevelUpCost
        {
            public int CopiesRequired { get; }
            public ScrapType ScrapKind { get; }
            public int ScrapAmount { get; }
            public long GoldCost { get; }

            public LevelUpCost(int copiesRequired, ScrapType scrapKind, int scrapAmount, long goldCost)
            {
                CopiesRequired = copiesRequired;
                ScrapKind = scrapKind;
                ScrapAmount = scrapAmount;
                GoldCost = goldCost;
            }
        }

        /// <summary>
        /// Liefert die Kosten für das Upgrade von <paramref name="fromLevel"/> auf <c>fromLevel+1</c>.
        /// </summary>
        public static LevelUpCost? GetCostForUpgrade(int fromLevel) => fromLevel switch
        {
            0  => new LevelUpCost(0, ScrapType.Common,    2,   500L),
            1  => new LevelUpCost(0, ScrapType.Common,    4,  1500L),
            2  => new LevelUpCost(0, ScrapType.Common,    8,  4000L),
            3  => new LevelUpCost(0, ScrapType.Common,   16, 10000L),
            4  => new LevelUpCost(1, ScrapType.Rare,      4, 25000L),
            5  => new LevelUpCost(0, ScrapType.Rare,      8, 50000L),
            6  => new LevelUpCost(0, ScrapType.Rare,     16, 90000L),
            7  => new LevelUpCost(0, ScrapType.Rare,     32, 150000L),
            8  => new LevelUpCost(0, ScrapType.Rare,     60, 250000L),
            9  => new LevelUpCost(2, ScrapType.Epic,     10, 500000L),
            10 => new LevelUpCost(0, ScrapType.Epic,     25, 800000L),
            11 => new LevelUpCost(0, ScrapType.Epic,     50, 1200000L),
            12 => new LevelUpCost(0, ScrapType.Epic,    100, 2000000L),
            13 => new LevelUpCost(0, ScrapType.Epic,    200, 3500000L),
            14 => new LevelUpCost(3, ScrapType.Legendary, 50, 8000000L),
            _  => null   // ab LV 15 nicht mehr aufruestbar
        };
    }
}
