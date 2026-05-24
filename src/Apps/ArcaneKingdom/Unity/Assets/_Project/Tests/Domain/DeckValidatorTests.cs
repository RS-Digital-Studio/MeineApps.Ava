#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using ArcaneKingdom.Domain.Cards;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class DeckValidatorTests
    {
        private static CardDefinition NewCardDef(string id, int cost, DeckLimit limit)
        {
            var so = ScriptableObject.CreateInstance<CardDefinition>();
            // ScriptableObject-Felder via Reflection setzen, da CardDefinition keinen
            // oeffentlichen Setter-Konstruktor hat (richtigerweise — Pflege via Inspector/Importer).
            Set(so, "id", id);
            Set(so, "cost", cost);
            Set(so, "baseAttack", 100);
            Set(so, "baseHealth", 200);
            Set(so, "turnsToSpecial", 3);
            Set(so, "deckLimit", limit);
            return so;
        }

        private static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Feld '{field}' nicht gefunden.");
            f!.SetValue(target, value);
        }

        private static (Deck deck, Dictionary<string, CardInstance> instances, Dictionary<string, CardDefinition> defs) BuildScenario(params (string defId, DeckLimit limit, int copies)[] composition)
        {
            var deck = new Deck(0, "test");
            var defs = new Dictionary<string, CardDefinition>();
            var instances = new Dictionary<string, CardInstance>();
            var counter = 0;
            foreach (var (defId, limit, copies) in composition)
            {
                if (!defs.ContainsKey(defId)) defs[defId] = NewCardDef(defId, 3, limit);
                for (var i = 0; i < copies; i++)
                {
                    var instId = $"{defId}_{counter++}";
                    instances[instId] = new CardInstance(instId, defId, 0, 0, DateTime.UtcNow);
                    deck.CardInstanceIds.Add(instId);
                }
            }
            return (deck, instances, defs);
        }

        [Test]
        public void LeeresDeckIstUngueltig()
        {
            var (deck, inst, defs) = BuildScenario();
            var r = DeckValidator.Validate(deck, inst, defs);
            Assert.AreEqual(DeckValidator.ValidationCode.EmptyDeck, r.Code);
        }

        [Test]
        public void GenauZehnKartenIstGueltig()
        {
            var (deck, inst, defs) = BuildScenario(("a", DeckLimit.Unlimited, 3), ("b", DeckLimit.Unlimited, 3), ("c", DeckLimit.Unlimited, 3), ("d", DeckLimit.OneOnly, 1));
            var r = DeckValidator.Validate(deck, inst, defs);
            Assert.AreEqual(DeckValidator.ValidationCode.Valid, r.Code);
            Assert.AreEqual(10, r.CardCount);
        }

        [Test]
        public void OneOnlyMehrAlsEinmalIstUngueltig()
        {
            var (deck, inst, defs) = BuildScenario(("unique", DeckLimit.OneOnly, 2));
            var r = DeckValidator.Validate(deck, inst, defs);
            Assert.AreEqual(DeckValidator.ValidationCode.UniqueCardDuplicated, r.Code);
            Assert.AreEqual("unique", r.OffendingCardId);
        }

        [Test]
        public void MaxTwoDritteKartenIstUngueltig()
        {
            var (deck, inst, defs) = BuildScenario(("limited", DeckLimit.MaxTwo, 3));
            var r = DeckValidator.Validate(deck, inst, defs);
            Assert.AreEqual(DeckValidator.ValidationCode.CardLimitExceeded, r.Code);
            Assert.AreEqual("limited", r.OffendingCardId);
        }

        [Test]
        public void UnlimitedDarfMaxDreiKopien()
        {
            var (deck, inst, defs) = BuildScenario(("normal", DeckLimit.Unlimited, 3));
            var r = DeckValidator.Validate(deck, inst, defs);
            Assert.AreEqual(DeckValidator.ValidationCode.Valid, r.Code);
        }

        [Test]
        public void UnlimitedVierteKopieIstUngueltig()
        {
            var (deck, inst, defs) = BuildScenario(("normal", DeckLimit.Unlimited, 4));
            var r = DeckValidator.Validate(deck, inst, defs);
            Assert.AreEqual(DeckValidator.ValidationCode.CopyLimitExceeded, r.Code);
        }

        [Test]
        public void MehrAlsZehnKartenIstUngueltig()
        {
            var (deck, inst, defs) = BuildScenario(
                ("a", DeckLimit.Unlimited, 3), ("b", DeckLimit.Unlimited, 3),
                ("c", DeckLimit.Unlimited, 3), ("d", DeckLimit.Unlimited, 2));
            var r = DeckValidator.Validate(deck, inst, defs);
            Assert.AreEqual(DeckValidator.ValidationCode.TooManyCards, r.Code);
        }

        [Test]
        public void CostWirdAufsummiert()
        {
            var (deck, inst, defs) = BuildScenario(("a", DeckLimit.Unlimited, 3));
            var r = DeckValidator.Validate(deck, inst, defs);
            Assert.AreEqual(9, r.TotalCost, "3 Karten je Cost 3 = 9.");
        }
    }
}
