#nullable enable
using ArcaneKingdom.Domain.Battle;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class BattleStateSerializerTests
    {
        [Test]
        public void RoundtripPreservesPrimitives()
        {
            var original = new BattleState(seed: 42, playerHeroHp: 1234, enemyHeroHp: 5678)
            {
                CurrentTurn = 7,
                PlayerMana = 4,
                EnemyMana = 5,
                PlayerMaxMana = 7,
                EnemyMaxMana = 8,
                Phase = BattlePhase.EnemyTurn,
                Result = BattleResult.Undecided
            };

            var json = BattleStateSerializer.Serialize(original);
            var restored = BattleStateSerializer.Deserialize(json);

            Assert.AreEqual(original.Seed, restored.Seed);
            Assert.AreEqual(original.CurrentTurn, restored.CurrentTurn);
            Assert.AreEqual(original.PlayerHeroHp, restored.PlayerHeroHp);
            Assert.AreEqual(original.EnemyHeroHp, restored.EnemyHeroHp);
            Assert.AreEqual(original.PlayerMana, restored.PlayerMana);
            Assert.AreEqual(original.EnemyMana, restored.EnemyMana);
            Assert.AreEqual(original.PlayerMaxMana, restored.PlayerMaxMana);
            Assert.AreEqual(original.EnemyMaxMana, restored.EnemyMaxMana);
            Assert.AreEqual(original.Phase, restored.Phase);
            Assert.AreEqual(original.Result, restored.Result);
        }

        [Test]
        public void RoundtripPreservesCollections()
        {
            var original = new BattleState(1, 1000, 1000);
            original.PlayerField.Add(new CardFieldSlot("ci1", 100, 200, 3));
            original.EnemyField.Add(new CardFieldSlot("ci2", 150, 250, 4));
            original.PlayerHand.Add("h1");
            original.PlayerHand.Add("h2");
            original.EnemyHand.Add("h3");
            original.PlayerDeckQueue.Enqueue("d1");
            original.PlayerDeckQueue.Enqueue("d2");
            original.EnemyDeckQueue.Enqueue("d3");

            var json = BattleStateSerializer.Serialize(original);
            var restored = BattleStateSerializer.Deserialize(json);

            Assert.AreEqual(1, restored.PlayerField.Count);
            Assert.AreEqual("ci1", restored.PlayerField[0].CardInstanceId);
            Assert.AreEqual(200, restored.PlayerField[0].CurrentHealth);
            Assert.AreEqual(1, restored.EnemyField.Count);
            CollectionAssert.AreEqual(new[] { "h1", "h2" }, restored.PlayerHand);
            CollectionAssert.AreEqual(new[] { "h3" }, restored.EnemyHand);
            CollectionAssert.AreEqual(new[] { "d1", "d2" }, restored.PlayerDeckQueue);
            CollectionAssert.AreEqual(new[] { "d3" }, restored.EnemyDeckQueue);
        }

        [Test]
        public void SerializationIsKomptaktOhneFormatting()
        {
            var state = new BattleState(1, 100, 100);
            var json = BattleStateSerializer.Serialize(state);
            Assert.IsFalse(json.Contains("\n"));
        }
    }
}
