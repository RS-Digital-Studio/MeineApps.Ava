#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Game.Battle;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class BattleAITests
    {
        private static CardDefinition MakeDef(string id, int cost, int atk, int hp, Element element = Element.Natur, Rarity rarity = Rarity.Gewoehnlich, int turns = 3)
        {
            var so = ScriptableObject.CreateInstance<CardDefinition>();
            Set(so, "id", id);
            Set(so, "cost", cost);
            Set(so, "baseAttack", atk);
            Set(so, "baseHealth", hp);
            Set(so, "turnsToSpecial", turns);
            Set(so, "element", element);
            Set(so, "rarity", rarity);
            return so;
        }

        private static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Feld '{field}' nicht gefunden.");
            f!.SetValue(target, value);
        }

        [Test]
        public void SpieltKeineKartenWennManaNichtReicht()
        {
            var defs = new Dictionary<string, CardDefinition> { ["a"] = MakeDef("a", cost: 5, atk: 100, hp: 100) };
            var instances = new Dictionary<string, CardInstance> { ["i1"] = new("i1", "a", 0, 0, DateTime.UtcNow) };
            var ai = new BattleAI(defs, instances);
            var pick = ai.ChooseCardsToPlay(new List<string> { "i1" }, availableMana: 2);
            Assert.AreEqual(0, pick.Count);
        }

        [Test]
        public void BevorzugtKarteMitBesseremStatPerCost()
        {
            var defs = new Dictionary<string, CardDefinition>
            {
                ["weak"] = MakeDef("weak",  cost: 3, atk: 50,  hp: 50),
                ["str"]  = MakeDef("str",   cost: 3, atk: 300, hp: 400)
            };
            var instances = new Dictionary<string, CardInstance>
            {
                ["w"] = new("w", "weak", 0, 0, DateTime.UtcNow),
                ["s"] = new("s", "str",  0, 0, DateTime.UtcNow)
            };
            var ai = new BattleAI(defs, instances);
            var pick = ai.ChooseCardsToPlay(new List<string> { "w", "s" }, availableMana: 3);
            Assert.AreEqual(1, pick.Count, "Nur eine passt ins Mana.");
            Assert.AreEqual("s", pick[0]);
        }

        [Test]
        public void SpieltMehrereKartenSolangeManaReicht()
        {
            var defs = new Dictionary<string, CardDefinition>
            {
                ["a"] = MakeDef("a", cost: 3, atk: 100, hp: 100),
                ["b"] = MakeDef("b", cost: 3, atk: 100, hp: 100),
                ["c"] = MakeDef("c", cost: 4, atk: 200, hp: 200)
            };
            var instances = new Dictionary<string, CardInstance>
            {
                ["i_a"] = new("i_a", "a", 0, 0, DateTime.UtcNow),
                ["i_b"] = new("i_b", "b", 0, 0, DateTime.UtcNow),
                ["i_c"] = new("i_c", "c", 0, 0, DateTime.UtcNow)
            };
            var ai = new BattleAI(defs, instances);
            var pick = ai.ChooseCardsToPlay(new List<string> { "i_a", "i_b", "i_c" }, availableMana: 7);
            // 7 Mana = c (4) + a (3) ODER c (4) + b (3). 2 Karten.
            Assert.AreEqual(2, pick.Count);
        }

        [Test]
        public void ElementVorteilSchlaegtBesserenStatPerCost()
        {
            var defs = new Dictionary<string, CardDefinition>
            {
                ["natur_avg"] = MakeDef("natur_avg",  cost: 3, atk: 200, hp: 200, element: Element.Natur),
                ["wasser_str"] = MakeDef("wasser_str", cost: 3, atk: 220, hp: 220, element: Element.Wasser)
            };
            var instances = new Dictionary<string, CardInstance>
            {
                ["n"] = new("n", "natur_avg", 0, 0, DateTime.UtcNow),
                ["w"] = new("w", "wasser_str", 0, 0, DateTime.UtcNow)
            };
            var ai = new BattleAI(defs, instances);
            // Gegner ist Wasser → Natur ist stark gegen Wasser
            var pick = ai.ChooseCardsToPlay(new List<string> { "n", "w" }, availableMana: 3, dominantEnemyElement: Element.Wasser);
            Assert.AreEqual(1, pick.Count);
            Assert.AreEqual("n", pick[0], "Natur-Karte sollte trotz schwaecherer Basis-Werte wegen Element-Bonus gewinnen.");
        }
    }
}
