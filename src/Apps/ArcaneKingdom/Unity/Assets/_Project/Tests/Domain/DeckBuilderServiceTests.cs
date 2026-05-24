#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Game.DeckBuilder;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class DeckBuilderServiceTests
    {
        private static CardDefinition MakeDef(string id, int cost, int atk, int hp, DeckLimit limit = DeckLimit.Unlimited, Element element = Element.Natur, Rarity rarity = Rarity.Gewoehnlich)
        {
            var so = ScriptableObject.CreateInstance<CardDefinition>();
            SetField(so, "id", id);
            SetField(so, "cost", cost);
            SetField(so, "baseAttack", atk);
            SetField(so, "baseHealth", hp);
            SetField(so, "deckLimit", limit);
            SetField(so, "element", element);
            SetField(so, "rarity", rarity);
            SetField(so, "turnsToSpecial", 3);
            return so;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
        }

        [Test]
        public void BleibtImCostBudget()
        {
            var defs = new Dictionary<string, CardDefinition>
            {
                ["a"] = MakeDef("a", cost: 4, atk: 500, hp: 700),
                ["b"] = MakeDef("b", cost: 5, atk: 700, hp: 1000)
            };
            var inst = new Dictionary<string, CardInstance>
            {
                ["i1"] = new("i1", "a", 0, 0, DateTime.UtcNow),
                ["i2"] = new("i2", "b", 0, 0, DateTime.UtcNow)
            };
            var result = new DeckBuilderService().Build(new()
            {
                Sammlung = inst, Definitions = defs, CostBudget = 4
            });
            Assert.AreEqual(1, result.CardInstanceIds.Count, "Nur 'a' passt ins Budget.");
            Assert.AreEqual("i1", result.CardInstanceIds[0]);
            Assert.LessOrEqual(result.TotalCost, 4);
        }

        [Test]
        public void RespektiertOneOnlyLimit()
        {
            var defs = new Dictionary<string, CardDefinition> { ["legend"] = MakeDef("legend", 5, 1000, 1500, DeckLimit.OneOnly, rarity: Rarity.Legendaer) };
            var inst = new Dictionary<string, CardInstance>();
            for (var i = 0; i < 5; i++)
                inst[$"i{i}"] = new($"i{i}", "legend", 0, 0, DateTime.UtcNow);

            var result = new DeckBuilderService().Build(new()
            {
                Sammlung = inst, Definitions = defs, CostBudget = 100
            });
            Assert.AreEqual(1, result.CardInstanceIds.Count, "OneOnly erlaubt max 1 Kopie.");
        }

        [Test]
        public void RespektiertMaxTwoLimit()
        {
            var defs = new Dictionary<string, CardDefinition> { ["rare"] = MakeDef("rare", 3, 400, 500, DeckLimit.MaxTwo, rarity: Rarity.Selten) };
            var inst = new Dictionary<string, CardInstance>();
            for (var i = 0; i < 5; i++)
                inst[$"i{i}"] = new($"i{i}", "rare", 0, 0, DateTime.UtcNow);

            var result = new DeckBuilderService().Build(new()
            {
                Sammlung = inst, Definitions = defs, CostBudget = 100
            });
            Assert.AreEqual(2, result.CardInstanceIds.Count, "MaxTwo erlaubt max 2 Kopien.");
        }

        [Test]
        public void UnlimitedRespektiertMaxCopies()
        {
            var defs = new Dictionary<string, CardDefinition> { ["common"] = MakeDef("common", 2, 200, 200) };
            var inst = new Dictionary<string, CardInstance>();
            for (var i = 0; i < 10; i++)
                inst[$"i{i}"] = new($"i{i}", "common", 0, 0, DateTime.UtcNow);

            var result = new DeckBuilderService().Build(new()
            {
                Sammlung = inst, Definitions = defs, CostBudget = 100
            });
            Assert.AreEqual(3, result.CardInstanceIds.Count, "Unlimited erlaubt max 3 (Farm-Cap).");
        }

        [Test]
        public void NieMehrAls10Karten()
        {
            var defs = new Dictionary<string, CardDefinition>();
            var inst = new Dictionary<string, CardInstance>();
            for (var i = 0; i < 20; i++)
            {
                var defId = $"def{i}";
                defs[defId] = MakeDef(defId, 1, 100, 100);
                inst[$"i{i}"] = new($"i{i}", defId, 0, 0, DateTime.UtcNow);
            }
            var result = new DeckBuilderService().Build(new()
            {
                Sammlung = inst, Definitions = defs, CostBudget = 100
            });
            Assert.LessOrEqual(result.CardInstanceIds.Count, Deck.MaxCards);
        }

        [Test]
        public void Element_Praeferenz_BevorzugtPassendeKarten()
        {
            var defs = new Dictionary<string, CardDefinition>
            {
                ["natur"] = MakeDef("natur", 3, 300, 300, element: Element.Natur),
                ["wasser"] = MakeDef("wasser", 3, 350, 350, element: Element.Wasser)
            };
            var inst = new Dictionary<string, CardInstance>
            {
                ["i_n"] = new("i_n", "natur", 0, 0, DateTime.UtcNow),
                ["i_w"] = new("i_w", "wasser", 0, 0, DateTime.UtcNow)
            };
            var result = new DeckBuilderService().Build(new()
            {
                Sammlung = inst, Definitions = defs, CostBudget = 3, PreferredElement = Element.Natur
            });
            Assert.AreEqual(1, result.CardInstanceIds.Count);
            Assert.AreEqual("i_n", result.CardInstanceIds[0], "Natur-Karte sollte trotz schwaecherer Werte wegen Praeferenz gewinnen.");
        }

        [Test]
        public void TruncatedWennBudgetUebersprungen()
        {
            var defs = new Dictionary<string, CardDefinition>
            {
                ["a"] = MakeDef("a", cost: 5, atk: 200, hp: 200),
                ["b"] = MakeDef("b", cost: 1, atk: 50, hp: 50)
            };
            var inst = new Dictionary<string, CardInstance>
            {
                ["i_a"] = new("i_a", "a", 0, 0, DateTime.UtcNow),
                ["i_b"] = new("i_b", "b", 0, 0, DateTime.UtcNow)
            };
            // Budget 3: a (5) passt nicht → Truncated, b (1) wird genommen
            var result = new DeckBuilderService().Build(new()
            {
                Sammlung = inst, Definitions = defs, CostBudget = 3
            });
            Assert.IsTrue(result.Truncated);
            Assert.AreEqual(1, result.CardInstanceIds.Count);
        }
    }
}
