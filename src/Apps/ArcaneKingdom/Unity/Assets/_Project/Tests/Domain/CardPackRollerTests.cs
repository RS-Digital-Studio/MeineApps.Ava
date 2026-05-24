#nullable enable
using System;
using System.Linq;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Shop;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class CardPackRollerTests
    {
        private static CardPackDefinition StandardPack() => new()
        {
            Id = "test",
            CardCount = 10,
            DiamondCost = 250,
            GuaranteedMinRarity = Rarity.Epic,
            LegendaryPityThreshold = 30,
            MaxLegendaryPerPack = 1,
            DropRates = new()
            {
                [Rarity.Gewoehnlich] = 0.30f,
                [Rarity.Ungewoehnlich] = 0.40f,
                [Rarity.Selten] = 0.20f,
                [Rarity.Epic] = 0.09f,
                [Rarity.Legendaer] = 0.01f
            }
        };

        [Test]
        public void PackHat10Karten()
        {
            var ctx = new CardPackRoller.RollContext { Pack = StandardPack(), Random = new Random(42) };
            var result = CardPackRoller.Roll(ctx);
            Assert.AreEqual(10, result.Rarities.Count);
        }

        [Test]
        public void GuaranteedMinRarityWirdEingehalten()
        {
            // Mit Seed der ohne Garantie nur Common-Karten produzieren wuerde
            var pack = StandardPack();
            pack = new CardPackDefinition
            {
                Id = pack.Id, CardCount = pack.CardCount, DiamondCost = pack.DiamondCost,
                GuaranteedMinRarity = Rarity.Epic, LegendaryPityThreshold = 999,
                MaxLegendaryPerPack = pack.MaxLegendaryPerPack,
                DropRates = new() { [Rarity.Gewoehnlich] = 1.0f }
            };
            var result = CardPackRoller.Roll(new CardPackRoller.RollContext { Pack = pack, Random = new Random(1) });
            Assert.IsTrue(result.Rarities.Any(r => r >= Rarity.Epic), "Garantie muss greifen.");
        }

        [Test]
        public void MaxLegendaryProPackWirdEingehalten()
        {
            var pack = new CardPackDefinition
            {
                Id = "all_legendary", CardCount = 10, DiamondCost = 0,
                GuaranteedMinRarity = Rarity.Gewoehnlich,
                LegendaryPityThreshold = 999, MaxLegendaryPerPack = 1,
                DropRates = new() { [Rarity.Legendaer] = 1.0f }
            };
            var result = CardPackRoller.Roll(new CardPackRoller.RollContext { Pack = pack, Random = new Random(1) });
            var legendaryCount = result.Rarities.Count(r => r == Rarity.Legendaer);
            Assert.AreEqual(1, legendaryCount, "Max 1 Legendaer pro Pack.");
        }

        [Test]
        public void PityTriggertNachThreshold()
        {
            var pack = new CardPackDefinition
            {
                Id = "no_legendary", CardCount = 10, DiamondCost = 0,
                GuaranteedMinRarity = Rarity.Gewoehnlich,
                LegendaryPityThreshold = 5, MaxLegendaryPerPack = 1,
                DropRates = new() { [Rarity.Gewoehnlich] = 1.0f }
            };
            var ctx = new CardPackRoller.RollContext { Pack = pack, PityCounter = 4, Random = new Random(1) };
            var result = CardPackRoller.Roll(ctx);
            Assert.IsTrue(result.PityTriggered);
            Assert.IsTrue(result.Rarities.Any(r => r == Rarity.Legendaer));
            Assert.AreEqual(0, result.NewPityCounter, "Counter resetet nach Legendary.");
        }

        [Test]
        public void PityCounterIncrementiertOhneLegendary()
        {
            var pack = new CardPackDefinition
            {
                Id = "no_legendary", CardCount = 10, DiamondCost = 0,
                GuaranteedMinRarity = Rarity.Gewoehnlich,
                LegendaryPityThreshold = 999, MaxLegendaryPerPack = 1,
                DropRates = new() { [Rarity.Gewoehnlich] = 1.0f }
            };
            var result = CardPackRoller.Roll(new CardPackRoller.RollContext { Pack = pack, PityCounter = 7, Random = new Random(1) });
            Assert.AreEqual(8, result.NewPityCounter);
        }
    }
}
