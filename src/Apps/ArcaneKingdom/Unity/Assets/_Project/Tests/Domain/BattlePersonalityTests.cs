#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Tests fuer Karten-Persoenlichkeit-Events in der BattleEngine (Designplan v4 Kap. 8).
    /// </summary>
    [TestFixture]
    public sealed class BattlePersonalityTests
    {
        private static CardDefinition MakeDef(string id, Race race, Element element,
                                              string? onPlay = null, string? onVictory = null, string? onDeath = null,
                                              IEnumerable<string>? rivals = null, IEnumerable<string>? synergies = null,
                                              int atk = 100, int hp = 500, int cost = 1)
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
            if (onPlay != null)    sObj.FindProperty("onPlayLineKey").stringValue = onPlay;
            if (onVictory != null) sObj.FindProperty("onVictoryLineKey").stringValue = onVictory;
            if (onDeath != null)   sObj.FindProperty("onDeathLineKey").stringValue = onDeath;
            var rivalProp = sObj.FindProperty("rivalCardIds");
            var rivalsList = new List<string>(rivals ?? System.Array.Empty<string>());
            rivalProp.arraySize = rivalsList.Count;
            for (var i = 0; i < rivalsList.Count; i++) rivalProp.GetArrayElementAtIndex(i).stringValue = rivalsList[i];
            var synProp = sObj.FindProperty("synergyCardIds");
            var synList = new List<string>(synergies ?? System.Array.Empty<string>());
            synProp.arraySize = synList.Count;
            for (var i = 0; i < synList.Count; i++) synProp.GetArrayElementAtIndex(i).stringValue = synList[i];
            sObj.ApplyModifiedPropertiesWithoutUndo();
            return so;
        }

        [Test]
        public void OnPlay_Event_wird_emittiert()
        {
            var def = MakeDef("c1", Race.Goetter, Element.Licht, onPlay: "card.c1.play");
            var defs = new Dictionary<string, CardDefinition> { ["c1"] = def, ["i1"] = def };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i1" }, System.Array.Empty<string>());
            engine.PlayCard(forPlayer: true, "i1");

            Assert.AreEqual(1, state.Events.Count);
            Assert.AreEqual(BattleEventType.CardPlayed, state.Events[0].EventType);
            Assert.AreEqual("card.c1.play", state.Events[0].LocalizationKey);
        }

        [Test]
        public void OnDeath_Event_wird_emittiert()
        {
            var weak = MakeDef("w", Race.Ritter, Element.Licht, onDeath: "card.w.death", hp: 1);
            var strong = MakeDef("s", Race.Daemonen, Element.Dunkel, atk: 100);
            var defs = new Dictionary<string, CardDefinition> { ["w"] = weak, ["s"] = strong, ["i_w"] = weak, ["i_s"] = strong };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i_s" }, new[] { "i_w" });
            engine.PlayCard(forPlayer: true, "i_s");
            engine.PlayCard(forPlayer: false, "i_w");
            engine.EndTurn();   // Spieler greift an, weak stirbt

            var deathEvent = state.Events.Find(e => e.EventType == BattleEventType.CardDied);
            Assert.IsNotNull(deathEvent);
            Assert.AreEqual("card.w.death", deathEvent!.LocalizationKey);
        }

        [Test]
        public void Synergy_wird_erkannt_bei_passendem_Partner_im_Feld()
        {
            var lira = MakeDef("lira", Race.Elfen, Element.Wasser, synergies: new[] { "sakura" });
            var sakura = MakeDef("sakura", Race.Tiergeister, Element.Natur);
            var defs = new Dictionary<string, CardDefinition> { ["lira"] = lira, ["sakura"] = sakura, ["i_sak"] = sakura, ["i_lira"] = lira };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i_sak", "i_lira" }, System.Array.Empty<string>());
            // Sakura zuerst
            engine.PlayCard(forPlayer: true, "i_sak");
            // Lira jetzt — Synergy mit Sakura im selben Feld
            engine.PlayCard(forPlayer: true, "i_lira");

            var syn = state.Events.Find(e => e.EventType == BattleEventType.SynergyActivated);
            Assert.IsNotNull(syn);
            Assert.AreEqual("lira", syn!.CardDefinitionId);
            Assert.AreEqual("sakura", syn.PartnerCardId);
            Assert.AreEqual(5, syn.Magnitude);   // 5% Bonus (Designplan v4 Kap. 8.2)
        }

        [Test]
        public void Rivalry_wird_bei_Aufeinandertreffen_erkannt()
        {
            var lilith = MakeDef("lilith", Race.Daemonen, Element.Dunkel, rivals: new[] { "selene" });
            var selene = MakeDef("selene", Race.Goetter, Element.Licht);
            var defs = new Dictionary<string, CardDefinition> { ["lilith"] = lilith, ["selene"] = selene, ["i_sel"] = selene, ["i_lil"] = lilith };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            var engine = new BattleEngine(state, defs);
            engine.Setup(new[] { "i_lil" }, new[] { "i_sel" });
            // Gegner spielt Selene zuerst, dann spieler lilith
            engine.PlayCard(forPlayer: false, "i_sel");
            engine.PlayCard(forPlayer: true, "i_lil");

            var rivalry = state.Events.Find(e => e.EventType == BattleEventType.RivalryClashed);
            Assert.IsNotNull(rivalry);
            Assert.AreEqual("lilith", rivalry!.CardDefinitionId);
            Assert.AreEqual("selene", rivalry.PartnerCardId);
        }
    }
}
