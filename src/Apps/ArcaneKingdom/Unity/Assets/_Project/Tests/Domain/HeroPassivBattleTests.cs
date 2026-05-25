#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using ArcaneKingdom.Domain.Hero;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer die 5 Helden-Passivs in der BattleEngine (Designplan v4 Kap. 2.1).
    /// </summary>
    [TestFixture]
    public sealed class HeroPassivBattleTests
    {
        private static CardDefinition MakeDef(string id, Race race, Element element,
                                              int atk = 100, int hp = 500, int cost = 5)
        {
            var so = ScriptableObject.CreateInstance<CardDefinition>();
            using var sObj = new SerializedObject(so);
            sObj.FindProperty("id").stringValue = id;
            sObj.FindProperty("race").enumValueIndex = (int)race;
            sObj.FindProperty("element").enumValueIndex = (int)element;
            sObj.FindProperty("baseAttack").intValue = atk;
            sObj.FindProperty("baseHealth").intValue = hp;
            sObj.FindProperty("cost").intValue = cost;
            sObj.FindProperty("turnsToSpecial").intValue = 3;
            sObj.ApplyModifiedPropertiesWithoutUndo();
            return so;
        }

        // --------------------------------------------------------------------
        // Koenigliche Aura (Ritter): +5% HP fuer eigene Karten
        // --------------------------------------------------------------------

        [Test]
        public void KoeniglicheAura_addiert_HP_Bonus()
        {
            var def = MakeDef("ritter", Race.Ritter, Element.Licht, hp: 1000);
            var defs = new Dictionary<string, CardDefinition> { ["ritter"] = def, ["i1"] = def };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.PlayerHeroPassiv = new HeroPassivContext(HeroFaehigkeitsTyp.KoeniglicheAura, magnitude: 5);
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i1" }, new[] { "i1" });
            engine.PlayCard(forPlayer: true, "i1");
            Assert.AreEqual(1, state.PlayerField.Count);
            // 1000 * 1.05 = 1050
            Assert.AreEqual(1050, state.PlayerField[0].CurrentHealth);
        }

        // --------------------------------------------------------------------
        // Waldlaeufer (Elfen): erste Karte jeder Runde kostet 0
        // --------------------------------------------------------------------

        [Test]
        public void Waldlaeufer_erste_Karte_kostet_0()
        {
            var def = MakeDef("elf", Race.Elfen, Element.Natur, cost: 5);
            var defs = new Dictionary<string, CardDefinition> { ["elf"] = def, ["i1"] = def, ["i2"] = def };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.PlayerMana = 3;        // weniger als Cost — sollte ausreichen wegen Passiv
            state.PlayerHeroPassiv = new HeroPassivContext(HeroFaehigkeitsTyp.Waldlaeufer, magnitude: 0);
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i1", "i2" }, new[] { "i1" });

            // Erste Karte: gratis (kostet 0)
            var played = engine.PlayCard(forPlayer: true, "i1");
            Assert.IsTrue(played);
            Assert.AreEqual(3, state.PlayerMana);   // unveraendert!
            Assert.IsTrue(state.PlayerHeroPassiv.FirstCardThisTurnPlayed);

            // Zweite Karte: voller Preis
            var played2 = engine.PlayCard(forPlayer: true, "i2");
            Assert.IsTrue(played2);
            Assert.AreEqual(3 - 5, state.PlayerMana);    // = -2, wir lassen es passieren (Mana-Underflow in Tests)
        }

        // --------------------------------------------------------------------
        // Rudelbund (Tiergeister): +3% ATK pro Tiergeist im Deck
        // --------------------------------------------------------------------

        [Test]
        public void Rudelbund_skaliert_ATK_mit_Tiergeister_Anzahl()
        {
            var tg = MakeDef("tg", Race.Tiergeister, Element.Natur, atk: 100);
            var def = MakeDef("d1", Race.Tiergeister, Element.Natur, atk: 200);
            var defs = new Dictionary<string, CardDefinition> { ["tg"] = tg, ["d1"] = def, ["i_play"] = def };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.PlayerHeroPassiv = new HeroPassivContext(HeroFaehigkeitsTyp.Rudelbund, magnitude: 3);
            var engine = new BattleEngine(state, defs);
            // Deck: 4 Tiergeister
            engine.Setup(new[] { "tg", "tg", "tg", "i_play" }, new[] { "tg" });
            engine.PlayCard(forPlayer: true, "i_play");

            // 4 Tiergeister im Deck × 3% = 12% ATK-Bonus
            // 200 * 1.12 = 224
            Assert.AreEqual(224, state.PlayerField[0].CurrentAttack);
            Assert.AreEqual(4, state.PlayerHeroPassiv.BeastSpiritCountInDeck);
        }

        // --------------------------------------------------------------------
        // LebensraubAura (Daemonen): 20% Schaden heilt Helden-HP
        // --------------------------------------------------------------------

        [Test]
        public void LebensraubAura_heilt_Held_bei_Karten_Schaden()
        {
            var attacker = MakeDef("dem", Race.Daemonen, Element.Dunkel, atk: 100, hp: 500);
            var defs = new Dictionary<string, CardDefinition> { ["dem"] = attacker, ["i_atk"] = attacker, ["i_def"] = attacker };
            var state = new BattleState(seed: 1, playerHeroHp: 500, enemyHeroHp: 1000);
            state.PlayerHeroPassiv = new HeroPassivContext(HeroFaehigkeitsTyp.LebensraubAura, magnitude: 20);
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i_atk" }, new[] { "i_def" });
            engine.PlayCard(forPlayer: true, "i_atk");
            engine.EndTurn();   // Spieler greift an

            // 100 Schaden -> 20% = 20 HP-Heal fuer Spieler-Held
            Assert.AreEqual(520, state.PlayerHeroHp);
        }

        // --------------------------------------------------------------------
        // GoettlicherSegen (Goetter): 1x pro Kampf Tod verhindern
        // --------------------------------------------------------------------

        [Test]
        public void GoettlicherSegen_rettet_eigene_Karte_einmal()
        {
            var weak = MakeDef("weak", Race.Ritter, Element.Licht, atk: 10, hp: 1);    // 1 HP -> stirbt sofort
            var strong = MakeDef("str", Race.Daemonen, Element.Dunkel, atk: 1000);
            var defs = new Dictionary<string, CardDefinition> { ["weak"] = weak, ["str"] = strong, ["w"] = weak, ["s"] = strong };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.EnemyHeroPassiv = new HeroPassivContext(HeroFaehigkeitsTyp.GoettlicherSegen, magnitude: 1);
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "s" }, new[] { "w" });
            engine.PlayCard(forPlayer: true, "s");
            engine.PlayCard(forPlayer: false, "w");
            engine.EndTurn();   // Spieler greift an: 1000 Schaden auf weak

            // Goettlicher Segen sollte die Karte mit 1 HP retten — sie bleibt im Feld
            Assert.AreEqual(1, state.EnemyField.Count);
            Assert.AreEqual(1, state.EnemyField[0].CurrentHealth);
            Assert.AreEqual(0, state.EnemyHeroPassiv.DivineBlessingsRemaining);   // verbraucht
        }
    }
}
