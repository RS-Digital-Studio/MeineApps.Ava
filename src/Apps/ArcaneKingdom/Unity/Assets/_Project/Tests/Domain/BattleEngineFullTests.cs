#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class BattleEngineFullTests
    {
        private static CardDefinition MakeDef(string id, int cost, int atk, int hp, Element element = Element.Natur, int turnsToSpecial = 99)
        {
            var so = ScriptableObject.CreateInstance<CardDefinition>();
            SetField(so, "id", id);
            SetField(so, "cost", cost);
            SetField(so, "baseAttack", atk);
            SetField(so, "baseHealth", hp);
            SetField(so, "turnsToSpecial", turnsToSpecial);
            SetField(so, "element", element);
            return so;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
        }

        [Test]
        public void PlayCardZiehtManaAbUndLegtKarteInsField()
        {
            var defs = new Dictionary<string, CardDefinition> { ["a"] = MakeDef("a", cost: 2, atk: 100, hp: 200) };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.PlayerHand.Add("a");
            var engine = new BattleEngine(state, defs);
            Assert.IsTrue(engine.PlayCard(forPlayer: true, "a"));
            Assert.AreEqual(1, state.PlayerField.Count);
            Assert.AreEqual(0, state.PlayerHand.Count);
            Assert.AreEqual(1, state.PlayerMana, "Start-Mana 3 - Cost 2 = 1.");
        }

        [Test]
        public void PlayCardOhneManaWirdAbgelehnt()
        {
            var defs = new Dictionary<string, CardDefinition> { ["a"] = MakeDef("a", cost: 5, atk: 100, hp: 200) };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.PlayerHand.Add("a");
            var engine = new BattleEngine(state, defs);
            Assert.IsFalse(engine.PlayCard(forPlayer: true, "a"), "Cost 5 > Mana 3 → ablehnen.");
        }

        [Test]
        public void PlayCardMitVollemFieldWirdAbgelehnt()
        {
            var defs = new Dictionary<string, CardDefinition> { ["a"] = MakeDef("a", cost: 1, atk: 50, hp: 50) };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.PlayerMana = 99;
            for (var i = 0; i < BattleEngine.MaxFieldSlots; i++)
                state.PlayerField.Add(new CardFieldSlot($"x{i}", 1, 1, 1));
            state.PlayerHand.Add("a");
            var engine = new BattleEngine(state, defs);
            Assert.IsFalse(engine.PlayCard(forPlayer: true, "a"));
        }

        [Test]
        public void EndTurnGreiftKartenAnUndDekrementiertCooldown()
        {
            var defs = new Dictionary<string, CardDefinition>
            {
                ["atk"] = MakeDef("atk", cost: 1, atk: 200, hp: 100, turnsToSpecial: 3),
                ["def"] = MakeDef("def", cost: 1, atk: 50, hp: 300)
            };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.PlayerField.Add(new CardFieldSlot("atk", currentAttack: 200, currentHealth: 100, turnsUntilSpecial: 3));
            state.EnemyField.Add(new CardFieldSlot("def", currentAttack: 50, currentHealth: 300, turnsUntilSpecial: 3));
            var engine = new BattleEngine(state, defs);
            engine.EndTurn();
            Assert.AreEqual(100, state.EnemyField[0].CurrentHealth, "300 - 200 = 100 HP.");
            Assert.AreEqual(2, state.PlayerField[0].TurnsUntilSpecial, "Cooldown dekrementiert.");
        }

        [Test]
        public void EndTurnOhneGegnerKarteSchaedigtDenHelden()
        {
            var defs = new Dictionary<string, CardDefinition>
            {
                ["atk"] = MakeDef("atk", cost: 1, atk: 250, hp: 100)
            };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.PlayerField.Add(new CardFieldSlot("atk", 250, 100, 99));
            var engine = new BattleEngine(state, defs);
            engine.EndTurn();
            Assert.AreEqual(750, state.EnemyHeroHp);
        }

        [Test]
        public void ElementVorteilErhoehtSchaden()
        {
            var defs = new Dictionary<string, CardDefinition>
            {
                ["natur"] = MakeDef("natur", cost: 1, atk: 100, hp: 100, element: Element.Natur),
                ["wasser"] = MakeDef("wasser", cost: 1, atk: 50, hp: 200, element: Element.Wasser)
            };
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            state.PlayerField.Add(new CardFieldSlot("natur", 100, 100, 99));
            state.EnemyField.Add(new CardFieldSlot("wasser", 50, 200, 99));
            var engine = new BattleEngine(state, defs);
            engine.EndTurn();
            // Natur stark gegen Wasser: 100 * 1.5 = 150 → 200 - 150 = 50
            Assert.AreEqual(50, state.EnemyField[0].CurrentHealth);
        }

        [Test]
        public void TurnWechseltZwischenPlayerUndEnemy()
        {
            var defs = new Dictionary<string, CardDefinition>();
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            var engine = new BattleEngine(state, defs);
            Assert.AreEqual(BattlePhase.Setup, state.Phase);
            engine.Setup(Array.Empty<string>(), Array.Empty<string>());
            Assert.AreEqual(BattlePhase.PlayerTurn, state.Phase);
            engine.EndTurn();
            Assert.AreEqual(BattlePhase.EnemyTurn, state.Phase);
            engine.EndTurn();
            Assert.AreEqual(BattlePhase.PlayerTurn, state.Phase);
        }

        [Test]
        public void ManaSteigtProRundeBisMax()
        {
            var defs = new Dictionary<string, CardDefinition>();
            var state = new BattleState(seed: 1, playerHeroHp: 1000, enemyHeroHp: 1000);
            var engine = new BattleEngine(state, defs);
            engine.Setup(Array.Empty<string>(), Array.Empty<string>());
            Assert.AreEqual(3, state.PlayerMana);
            engine.EndTurn();  // enemy turn
            engine.EndTurn();  // back to player
            Assert.AreEqual(4, state.PlayerMaxMana);
        }
    }
}
