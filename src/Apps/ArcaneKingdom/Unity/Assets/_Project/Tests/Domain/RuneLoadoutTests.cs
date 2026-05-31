#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Runes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer die Deck-Runen-Aggregation (Spielplan v5 Kap. 7.2, Brocken K12):
    /// RuneLoadout.Add je Typ + Additivitaet, RuneLoadoutBuilder Slot-Gate + Kombo-Bedingungen.
    /// </summary>
    [TestFixture]
    public sealed class RuneLoadoutTests
    {
        private static RuneDefinition MakeRune(string id, RuneType type, float baseMag,
                                               float perLevel = 0f, Element elem = Element.Natur)
        {
            var so = ScriptableObject.CreateInstance<RuneDefinition>();
            using var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = id;
            sObj.FindProperty("type").enumValueIndex = (int)type;
            sObj.FindProperty("baseMagnitude").floatValue = baseMag;
            sObj.FindProperty("magnitudePerLevel").floatValue = perLevel;
            sObj.FindProperty("elementTarget").enumValueIndex = (int)elem;
            sObj.ApplyModifiedPropertiesWithoutUndo();
            return so;
        }

        private static CardDefinition MakeCard(string id, Race race)
        {
            var so = ScriptableObject.CreateInstance<CardDefinition>();
            using var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = id;
            sObj.FindProperty("race").enumValueIndex = (int)race;
            sObj.ApplyModifiedPropertiesWithoutUndo();
            return so;
        }

        // ---------------- RuneLoadout ----------------

        [Test]
        public void Add_Angriff_und_additiv()
        {
            var lo = new RuneLoadout();
            lo.Add(MakeRune("a1", RuneType.Angriff, 10f), 1);
            lo.Add(MakeRune("a2", RuneType.Angriff, 5f), 1);
            Assert.AreEqual(15f, lo.AttackPercent);
        }

        [Test]
        public void Add_LevelSkalierung()
        {
            var lo = new RuneLoadout();
            lo.Add(MakeRune("a", RuneType.Angriff, 5f, 1f), 3);   // 5 + 1*(3-1) = 7
            Assert.AreEqual(7f, lo.AttackPercent);
        }

        [Test]
        public void Add_AlleTypen()
        {
            var lo = new RuneLoadout();
            lo.Add(MakeRune("v", RuneType.Verteidigung, 8f), 1);
            lo.Add(MakeRune("g", RuneType.Geschwindigkeit, 1f), 1);
            lo.Add(MakeRune("h", RuneType.Hero, 1500f), 1);
            lo.Add(MakeRune("m", RuneType.Mana, 1f), 1);
            lo.Add(MakeRune("e", RuneType.Element, 30f, 0f, Element.Feuer), 1);
            Assert.AreEqual(8f, lo.HealthPercent);
            Assert.AreEqual(1, lo.SpecialTurnReduction);
            Assert.AreEqual(1500, lo.HeroHpFlat);
            Assert.AreEqual(1, lo.BonusStartMana);
            Assert.AreEqual(30f, lo.ElementBonusFor(Element.Feuer));
            Assert.AreEqual(0f, lo.ElementBonusFor(Element.Wasser));
        }

        [Test]
        public void IsEmpty()
        {
            var lo = new RuneLoadout();
            Assert.IsTrue(lo.IsEmpty);
            lo.Add(MakeRune("a", RuneType.Angriff, 10f), 1);
            Assert.IsFalse(lo.IsEmpty);
        }

        // ---------------- RuneLoadoutBuilder ----------------

        private static (Deck deck, PlayerSave save, Dictionary<string, RuneDefinition> runes) Scenario(
            int playerLevel, params (int slot, string runeDefId, RuneType type, float mag)[] runesInSlots)
        {
            var save = new PlayerSave(new PlayerProfile("u", "Test", "Poseidon", DateTime.UtcNow));
            save.Profile.Level = playerLevel;
            var deck = new Deck(0, "Test");
            var runeDefs = new Dictionary<string, RuneDefinition>();
            foreach (var (slot, runeDefId, type, mag) in runesInSlots)
            {
                if (!runeDefs.ContainsKey(runeDefId))
                    runeDefs[runeDefId] = MakeRune(runeDefId, type, mag);
                var instId = $"ri_{slot}_{runeDefId}";
                save.RuneInventory[instId] = new RuneInstance(instId, runeDefId, 1, DateTime.UtcNow);
                deck.RuneInstanceIds[slot] = instId;
            }
            return (deck, save, runeDefs);
        }

        [Test]
        public void Build_NurFreigeschalteteSlots()
        {
            // LV1 -> nur Slot 0 frei. Rune in Slot 1 (LV20) wird ignoriert.
            var (deck, save, runes) = Scenario(1, (1, "angriff", RuneType.Angriff, 10f));
            var lo = RuneLoadoutBuilder.Build(deck, save, id => runes.GetValueOrDefault(id), _ => null);
            Assert.IsNull(lo, "Slot 1 ist bei LV1 gesperrt -> kein wirksames Loadout.");
        }

        [Test]
        public void Build_FreigeschalteterSlotWirkt()
        {
            var (deck, save, runes) = Scenario(1, (0, "angriff", RuneType.Angriff, 10f));
            var lo = RuneLoadoutBuilder.Build(deck, save, id => runes.GetValueOrDefault(id), _ => null);
            Assert.IsNotNull(lo);
            Assert.AreEqual(10f, lo!.AttackPercent);
        }

        [Test]
        public void Build_FehlendeRuneInstanz_uebersprungen()
        {
            var save = new PlayerSave(new PlayerProfile("u", "Test", "Poseidon", DateTime.UtcNow));
            save.Profile.Level = 50;
            var deck = new Deck(0, "Test");
            deck.RuneInstanceIds[0] = "missing";
            var lo = RuneLoadoutBuilder.Build(deck, save, _ => null, _ => null);
            Assert.IsNull(lo);
        }

        [Test]
        public void Build_ComboDaemon_nurAb3Daemonen()
        {
            var (deck, save, runes) = Scenario(50, (0, "kombo_daemon", RuneType.Kombo, 20f));
            var cards = new Dictionary<string, CardDefinition>
            {
                ["d1"] = MakeCard("d1", Race.Daemonen),
                ["d2"] = MakeCard("d2", Race.Daemonen),
                ["d3"] = MakeCard("d3", Race.Daemonen)
            };
            foreach (var id in new[] { "d1", "d2", "d3" })
            {
                save.CardInventory[id] = new CardInstance(id, id, 0, 0, DateTime.UtcNow);
                deck.CardInstanceIds.Add(id);
            }
            var lo = RuneLoadoutBuilder.Build(deck, save,
                id => runes.GetValueOrDefault(id),
                id => cards.GetValueOrDefault(id));
            Assert.IsNotNull(lo);
            Assert.IsTrue(lo!.ComboDaemonActive, "3 Daemonen -> Kombo aktiv.");

            // Nur 2 Daemonen -> inaktiv
            deck.CardInstanceIds.RemoveAt(2);
            var lo2 = RuneLoadoutBuilder.Build(deck, save,
                id => runes.GetValueOrDefault(id),
                id => cards.GetValueOrDefault(id));
            Assert.IsFalse(lo2 == null ? false : lo2.ComboDaemonActive, "2 Daemonen -> Kombo inaktiv.");
        }

        [Test]
        public void Build_ComboDrache_nurAb2Drachen()
        {
            var (deck, save, runes) = Scenario(50, (0, "kombo_drache", RuneType.Kombo, 50f));
            var cards = new Dictionary<string, CardDefinition>
            {
                ["fruehlingsdrache_verdant"] = MakeCard("fruehlingsdrache_verdant", Race.Tiergeister),
                ["urdrachenlord_tiamat"] = MakeCard("urdrachenlord_tiamat", Race.Tiergeister)
            };
            foreach (var id in new[] { "fruehlingsdrache_verdant", "urdrachenlord_tiamat" })
            {
                save.CardInventory[id] = new CardInstance(id, id, 0, 0, DateTime.UtcNow);
                deck.CardInstanceIds.Add(id);
            }
            var lo = RuneLoadoutBuilder.Build(deck, save,
                id => runes.GetValueOrDefault(id),
                id => cards.GetValueOrDefault(id));
            Assert.IsNotNull(lo);
            Assert.IsTrue(lo!.ComboDracheActive, "2 Drachen-IDs -> Kombo aktiv.");
        }
    }
}
