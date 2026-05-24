#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;

namespace ArcaneKingdom.Domain.Shop
{
    /// <summary>
    /// Karten-Pack-Definition (DESIGN.md Kap. 17.2). Pity-Counter wird pro Spieler im
    /// PlayerSave persistiert (Map<packId, missesSinceLastLegendary>).
    /// </summary>
    [Serializable]
    public sealed class CardPackDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayNameKey { get; init; } = string.Empty;
        public int CardCount { get; init; } = 10;
        public long DiamondCost { get; init; }

        /// <summary>
        /// Wahrscheinlichkeits-Verteilung pro Karte (muss sich zu 1 summieren).
        /// </summary>
        public Dictionary<Rarity, float> DropRates { get; init; } = new();

        /// <summary>
        /// Mindest-Rarity-Garantie (z.B. mind. 1 Epic pro Rare-Pack). Wenn das normale
        /// Rolling weniger liefert, wird die letzte Karte hochgepromoted.
        /// </summary>
        public Rarity GuaranteedMinRarity { get; init; }

        /// <summary>
        /// Pity-Counter: Nach so vielen Packs ohne Legendaer wird die naechste Legendaere garantiert.
        /// </summary>
        public int LegendaryPityThreshold { get; init; } = 30;

        /// <summary>
        /// Max-Anzahl Legendaere pro Pack (z.B. 1, damit kein Whale-Stacking).
        /// </summary>
        public int MaxLegendaryPerPack { get; init; } = 1;
    }

    /// <summary>
    /// Reine Rolling-Logik fuer Card-Packs. Deterministisch via Random-Seed → testbar.
    /// </summary>
    public static class CardPackRoller
    {
        public sealed class RollContext
        {
            public CardPackDefinition Pack { get; init; } = default!;
            public int PityCounter { get; init; }     // wie viele Packs in Folge ohne Legendary
            public Random Random { get; init; } = new();
        }

        public sealed class RollResult
        {
            public List<Rarity> Rarities { get; } = new();
            public bool PityTriggered { get; set; }
            public int NewPityCounter { get; set; }
        }

        public static RollResult Roll(RollContext ctx)
        {
            var result = new RollResult();
            var legendaryThisPack = 0;
            var highestThisPack = Rarity.Gewoehnlich;

            for (var i = 0; i < ctx.Pack.CardCount; i++)
            {
                var rarity = RollOne(ctx.Pack, ctx.Random);

                if (rarity == Rarity.Legendaer)
                {
                    if (legendaryThisPack >= ctx.Pack.MaxLegendaryPerPack) rarity = Rarity.Epic;
                    else legendaryThisPack++;
                }
                if ((int)rarity > (int)highestThisPack) highestThisPack = rarity;
                result.Rarities.Add(rarity);
            }

            // Mindest-Rarity-Garantie
            if (highestThisPack < ctx.Pack.GuaranteedMinRarity)
            {
                result.Rarities[result.Rarities.Count - 1] = ctx.Pack.GuaranteedMinRarity;
                highestThisPack = ctx.Pack.GuaranteedMinRarity;
            }

            // Pity-Counter
            var pityAboutToTrigger = (ctx.PityCounter + 1) >= ctx.Pack.LegendaryPityThreshold;
            if (legendaryThisPack == 0 && pityAboutToTrigger)
            {
                result.Rarities[result.Rarities.Count - 1] = Rarity.Legendaer;
                legendaryThisPack = 1;
                result.PityTriggered = true;
            }

            result.NewPityCounter = legendaryThisPack > 0 ? 0 : ctx.PityCounter + 1;
            return result;
        }

        private static Rarity RollOne(CardPackDefinition pack, Random rng)
        {
            var roll = (float)rng.NextDouble();
            var cumulative = 0f;
            foreach (var kv in pack.DropRates)
            {
                cumulative += kv.Value;
                if (roll <= cumulative) return kv.Key;
            }
            return Rarity.Gewoehnlich;
        }
    }
}
