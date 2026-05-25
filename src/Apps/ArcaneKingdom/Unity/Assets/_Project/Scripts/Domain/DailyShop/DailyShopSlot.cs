#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.DailyShop
{
    public enum DailyShopItemKind
    {
        Pack = 0,
        Scrap = 1,
        Card = 2,
        Energy = 3,
        Rune = 4
    }

    [Serializable]
    public sealed class DailyShopSlot
    {
        public DailyShopItemKind Kind { get; init; }
        public string SubType { get; init; } = string.Empty;
        public long Quantity { get; init; } = 1;
        public string PriceCurrency { get; init; } = "Diamond";    // "Diamond" oder "Gold"
        public long PriceAmount { get; init; }
        public bool DiscountedFromDaily { get; init; }              // wahr für 1 Slot pro Tag
        public string? DisplayNameKey { get; init; }
    }

    /// <summary>
    /// Deterministische Tages-Rotation. Seed = Server-UTC-Tag — alle Spieler weltweit
    /// sehen dasselbe Sortiment für 24h.
    /// </summary>
    public static class DailyShopRotation
    {
        public const int SlotsPerDay = 6;

        public static IReadOnlyList<DailyShopSlot> RotationForDay(DateTime utcDay)
        {
            var seed = utcDay.Year * 10000 + utcDay.Month * 100 + utcDay.Day;
            var rng = new Random(seed);
            var pool = BuildPool();
            var slots = new List<DailyShopSlot>(SlotsPerDay);
            var discountSlot = rng.Next(SlotsPerDay);

            for (var i = 0; i < SlotsPerDay; i++)
            {
                var template = pool[rng.Next(pool.Count)];
                slots.Add(new DailyShopSlot
                {
                    Kind = template.Kind,
                    SubType = template.SubType,
                    Quantity = template.Quantity,
                    PriceCurrency = template.PriceCurrency,
                    PriceAmount = i == discountSlot ? template.PriceAmount / 2 : template.PriceAmount,
                    DiscountedFromDaily = i == discountSlot,
                    DisplayNameKey = template.DisplayNameKey
                });
            }
            return slots;
        }

        private static List<DailyShopSlot> BuildPool() => new()
        {
            new() { Kind = DailyShopItemKind.Pack,   SubType = "common_pack",  Quantity = 1,   PriceCurrency = "Diamond", PriceAmount = 50,   DisplayNameKey = "pack.common_pack.name" },
            new() { Kind = DailyShopItemKind.Pack,   SubType = "rare_pack",    Quantity = 1,   PriceCurrency = "Diamond", PriceAmount = 250,  DisplayNameKey = "pack.rare_pack.name" },
            new() { Kind = DailyShopItemKind.Scrap,  SubType = "Common",       Quantity = 50,  PriceCurrency = "Gold",    PriceAmount = 5000 },
            new() { Kind = DailyShopItemKind.Scrap,  SubType = "Rare",         Quantity = 10,  PriceCurrency = "Diamond", PriceAmount = 80 },
            new() { Kind = DailyShopItemKind.Scrap,  SubType = "Epic",         Quantity = 3,   PriceCurrency = "Diamond", PriceAmount = 150 },
            new() { Kind = DailyShopItemKind.Energy, SubType = "30",           Quantity = 30,  PriceCurrency = "Diamond", PriceAmount = 50 },
            new() { Kind = DailyShopItemKind.Energy, SubType = "60",           Quantity = 60,  PriceCurrency = "Diamond", PriceAmount = 90 },
            new() { Kind = DailyShopItemKind.Rune,   SubType = "angriff_klein",Quantity = 1,   PriceCurrency = "Diamond", PriceAmount = 100 }
        };
    }
}
