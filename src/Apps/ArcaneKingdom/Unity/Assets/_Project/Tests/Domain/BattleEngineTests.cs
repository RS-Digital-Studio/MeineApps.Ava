#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Cards;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class BattleEngineTests
    {
        [Test]
        public void SetupZiehtVierKartenProSeite()
        {
            var state = new BattleState(seed: 42, playerHeroHp: 1000, enemyHeroHp: 1000);
            var defs = new Dictionary<string, CardDefinition>();
            var engine = new BattleEngine(state, defs);

            engine.Setup(
                playerDeckInstanceIds: new[] { "p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8", "p9", "p10" },
                enemyDeckInstanceIds:  new[] { "e1", "e2", "e3", "e4", "e5", "e6", "e7", "e8", "e9", "e10" }
            );

            Assert.AreEqual(4, state.PlayerHand.Count);
            Assert.AreEqual(4, state.EnemyHand.Count);
            Assert.AreEqual(6, state.PlayerDeckQueue.Count);
            Assert.AreEqual(6, state.EnemyDeckQueue.Count);
            Assert.AreEqual(BattlePhase.PlayerTurn, state.Phase);
        }

        [Test]
        public void DeterministischesShuffleMitGleichemSeed()
        {
            var defs = new Dictionary<string, CardDefinition>();

            var s1 = new BattleState(seed: 123, playerHeroHp: 1000, enemyHeroHp: 1000);
            new BattleEngine(s1, defs).Setup(new[] { "a", "b", "c", "d", "e" }, new[] { "1", "2", "3", "4", "5" });

            var s2 = new BattleState(seed: 123, playerHeroHp: 1000, enemyHeroHp: 1000);
            new BattleEngine(s2, defs).Setup(new[] { "a", "b", "c", "d", "e" }, new[] { "1", "2", "3", "4", "5" });

            CollectionAssert.AreEqual(s1.PlayerHand, s2.PlayerHand);
        }

        [Test]
        public void SiegBedingungWennGegnerheldAufNull()
        {
            var state = new BattleState(seed: 1, playerHeroHp: 100, enemyHeroHp: 0);
            var engine = new BattleEngine(state, new Dictionary<string, CardDefinition>());
            Assert.AreEqual(BattleResult.PlayerWins, engine.CheckVictoryCondition());
        }

        [Test]
        public void NiederlageWennEigenerHeldAufNull()
        {
            var state = new BattleState(seed: 1, playerHeroHp: 0, enemyHeroHp: 100);
            var engine = new BattleEngine(state, new Dictionary<string, CardDefinition>());
            Assert.AreEqual(BattleResult.EnemyWins, engine.CheckVictoryCondition());
        }

        [Test]
        public void UnentschiedenWennBeideHeldenAufNull()
        {
            var state = new BattleState(seed: 1, playerHeroHp: 0, enemyHeroHp: 0);
            var engine = new BattleEngine(state, new Dictionary<string, CardDefinition>());
            Assert.AreEqual(BattleResult.Draw, engine.CheckVictoryCondition());
        }

        [Test]
        public void SuddenDeathBeiRunde50()
        {
            var state = new BattleState(seed: 1, playerHeroHp: 200, enemyHeroHp: 100);
            state.CurrentTurn = 50;
            var engine = new BattleEngine(state, new Dictionary<string, CardDefinition>());
            Assert.AreEqual(BattleResult.PlayerWins, engine.CheckVictoryCondition());
        }
    }
}
