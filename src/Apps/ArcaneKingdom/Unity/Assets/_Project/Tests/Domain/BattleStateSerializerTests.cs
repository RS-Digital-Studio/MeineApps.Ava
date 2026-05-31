#nullable enable
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Hero;
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

        [Test]
        public void RoundtripPreservesStatusEffectsHeroPassivBossAndEvents()
        {
            // Verifiziert den erweiterten, verlustfreien Serializer (vorher gingen StatusEffects,
            // MaxHealth, Hero-Passivs, Boss-State und Events beim Round-Trip verloren).
            var original = new BattleState(7, 1000, 800)
            {
                PlayerHeroMaxHp = 1000,
                EnemyHeroMaxHp = 800,
                EnemyStatMultiplier = 1.6f,
                PlayerCardsPlayedThisTurn = 2,
                EnemyCardsPlayedThisTurn = 1,
                IsBossEncounter = true,
                BossPhase2Active = true,
                BossPhase2PassiveKey = "boss.test.key",
                PlayerHeroPassiv = new HeroPassivContext(HeroFaehigkeitsTyp.GoettlicherSegen, 1)
                    { DivineBlessingsRemaining = 1, FirstCardThisTurnPlayed = true },
                EnemyHeroPassiv = new HeroPassivContext(HeroFaehigkeitsTyp.Rudelbund, 3, beastSpiritCountInDeck: 4),
            };
            original.BossPhase2ReinforcementCardIds.Add("reinf_a");
            original.BossPhase2ReinforcementCardIds.Add("reinf_b");

            var slot = new CardFieldSlot("ci1", 100, 150, 3) { MaxHealth = 300 };
            slot.StatusEffects.Add(new StatusEffect(StatusEffectType.Poisoned, 2, magnitude: 50, sourceCardId: "src1"));
            slot.StatusEffects.Add(new StatusEffect(StatusEffectType.Frozen, 1));
            original.PlayerField.Add(slot);

            original.Events.Add(new BattleEvent(BattleEventType.SynergyActivated, 3, forPlayer: true,
                cardInstanceId: "ci1", cardDefinitionId: "def1", partnerCardId: "def2", magnitude: 5));

            var restored = BattleStateSerializer.Deserialize(BattleStateSerializer.Serialize(original));

            Assert.AreEqual(1000, restored.PlayerHeroMaxHp);
            Assert.AreEqual(800, restored.EnemyHeroMaxHp);
            Assert.AreEqual(1.6f, restored.EnemyStatMultiplier, 0.001f);
            Assert.AreEqual(2, restored.PlayerCardsPlayedThisTurn);
            Assert.IsTrue(restored.IsBossEncounter);
            Assert.IsTrue(restored.BossPhase2Active);
            Assert.AreEqual("boss.test.key", restored.BossPhase2PassiveKey);
            CollectionAssert.AreEqual(new[] { "reinf_a", "reinf_b" }, restored.BossPhase2ReinforcementCardIds);

            Assert.IsNotNull(restored.PlayerHeroPassiv);
            Assert.AreEqual(HeroFaehigkeitsTyp.GoettlicherSegen, restored.PlayerHeroPassiv!.PassivType);
            Assert.AreEqual(1, restored.PlayerHeroPassiv.DivineBlessingsRemaining);
            Assert.IsTrue(restored.PlayerHeroPassiv.FirstCardThisTurnPlayed);
            Assert.AreEqual(4, restored.EnemyHeroPassiv!.BeastSpiritCountInDeck);

            var rs = restored.PlayerField[0];
            Assert.AreEqual(300, rs.MaxHealth, "MaxHealth muss erhalten bleiben (nicht auf CurrentHealth zurueckgesetzt).");
            Assert.AreEqual(2, rs.StatusEffects.Count);
            Assert.AreEqual(StatusEffectType.Poisoned, rs.StatusEffects[0].Type);
            Assert.AreEqual(50, rs.StatusEffects[0].Magnitude);
            Assert.AreEqual("src1", rs.StatusEffects[0].SourceCardId);

            Assert.AreEqual(1, restored.Events.Count);
            Assert.AreEqual(BattleEventType.SynergyActivated, restored.Events[0].EventType);
            Assert.AreEqual("def2", restored.Events[0].PartnerCardId);
        }
    }
}
