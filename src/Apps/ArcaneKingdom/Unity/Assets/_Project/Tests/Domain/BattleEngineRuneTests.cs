#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Runes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer die Deck-Runen-Wirkung in der BattleEngine (K12, Spielplan v5 Kap. 7.2):
    /// ATK/HP-Boni, Helden-HP, Start-Mana-Invariante, Geschwindigkeit.
    /// </summary>
    [TestFixture]
    public sealed class BattleEngineRuneTests
    {
        private static CardDefinition MakeDef(string id, Element element = Element.Licht,
                                              int atk = 100, int hp = 500, int cost = 5, int turns = 3)
        {
            var so = ScriptableObject.CreateInstance<CardDefinition>();
            using var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = id;
            sObj.FindProperty("element").enumValueIndex = (int)element;
            sObj.FindProperty("baseAttack").intValue = atk;
            sObj.FindProperty("baseHealth").intValue = hp;
            sObj.FindProperty("cost").intValue = cost;
            sObj.FindProperty("turnsToSpecial").intValue = turns;
            sObj.ApplyModifiedPropertiesWithoutUndo();
            return so;
        }

        private static RuneDefinition MakeRune(string id, RuneType type, float mag, Element elem = Element.Natur)
        {
            var so = ScriptableObject.CreateInstance<RuneDefinition>();
            using var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = id;
            sObj.FindProperty("type").enumValueIndex = (int)type;
            sObj.FindProperty("baseMagnitude").floatValue = mag;
            sObj.FindProperty("elementTarget").enumValueIndex = (int)elem;
            sObj.ApplyModifiedPropertiesWithoutUndo();
            return so;
        }

        private static RuneLoadout Loadout(params RuneDefinition[] runes)
        {
            var lo = new RuneLoadout();
            foreach (var r in runes) lo.Add(r, 1);
            return lo;
        }

        [Test]
        public void Angriffsrune_erhoeht_ATK()
        {
            var def = MakeDef("c", atk: 100, hp: 500);
            var defs = new Dictionary<string, CardDefinition> { ["c"] = def, ["i1"] = def };
            var state = new BattleState(1, 1000, 1000)
            {
                PlayerRuneLoadout = Loadout(MakeRune("a", RuneType.Angriff, 20f))
            };
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i1" }, new[] { "i1" });
            engine.PlayCard(true, "i1");
            Assert.AreEqual(120, state.PlayerField[0].CurrentAttack);   // 100 * 1.2
        }

        [Test]
        public void Verteidigungsrune_erhoeht_HP()
        {
            var def = MakeDef("c", atk: 100, hp: 500);
            var defs = new Dictionary<string, CardDefinition> { ["c"] = def, ["i1"] = def };
            var state = new BattleState(1, 1000, 1000)
            {
                PlayerRuneLoadout = Loadout(MakeRune("v", RuneType.Verteidigung, 20f))
            };
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i1" }, new[] { "i1" });
            engine.PlayCard(true, "i1");
            Assert.AreEqual(600, state.PlayerField[0].CurrentHealth);   // 500 * 1.2
        }

        [Test]
        public void Herorune_erhoeht_HeldenHP_und_MaxHP()
        {
            var def = MakeDef("c");
            var defs = new Dictionary<string, CardDefinition> { ["c"] = def, ["i1"] = def };
            var state = new BattleState(1, 1000, 1000)
            {
                PlayerRuneLoadout = Loadout(MakeRune("h", RuneType.Hero, 500f))
            };
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i1" }, new[] { "i1" });
            Assert.AreEqual(1500, state.PlayerHeroHp);
            Assert.AreEqual(1500, state.PlayerHeroMaxHp);
        }

        [Test]
        public void Manarune_gibt_Startmana_aber_haelt_Invariante()
        {
            var def = MakeDef("c");
            var defs = new Dictionary<string, CardDefinition> { ["c"] = def, ["i1"] = def, ["i2"] = def, ["e1"] = def };
            var state = new BattleState(1, 1000, 1000)
            {
                PlayerRuneLoadout = Loadout(MakeRune("m", RuneType.Mana, 1f))
            };
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i1", "i2" }, new[] { "e1" });
            Assert.AreEqual(4, state.PlayerMana, "Runde 1: 3 + 1 Bonus-Mana.");

            // Volle Runde: Player-EndTurn -> Enemy-Phase -> Enemy-EndTurn -> zurueck zu Player.
            engine.EndTurn();
            engine.EndTurn();
            Assert.AreEqual(3, state.PlayerMana, "Ab Runde 2 wieder 3 Orbs (MaxMana unveraendert).");
        }

        [Test]
        public void OhneRunen_keine_Aenderung()
        {
            var def = MakeDef("c", atk: 100, hp: 500);
            var defs = new Dictionary<string, CardDefinition> { ["c"] = def, ["i1"] = def };
            var state = new BattleState(1, 1000, 1000);   // kein Loadout
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i1" }, new[] { "i1" });
            engine.PlayCard(true, "i1");
            Assert.AreEqual(100, state.PlayerField[0].CurrentAttack);
            Assert.AreEqual(500, state.PlayerField[0].CurrentHealth);
            Assert.AreEqual(3, state.PlayerMana);
        }
    }
}
