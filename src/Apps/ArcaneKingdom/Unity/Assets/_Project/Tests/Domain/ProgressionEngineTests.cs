#nullable enable
using System;
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Domain.Progression;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class ProgressionEngineTests
    {
        private static PlayerProfile NewProfile(int level, long expTotal)
        {
            return new PlayerProfile("u", "Test", "Poseidon", DateTime.UtcNow)
            {
                Level = level,
                ExpTotal = expTotal
            };
        }

        [Test]
        public void KeinLevelUpWennUnterSchwelle()
        {
            var p = NewProfile(level: 1, expTotal: 0);
            var result = ProgressionEngine.ApplyExp(p, 500);
            Assert.AreEqual(1, result.NewLevel);
            Assert.IsFalse(result.LeveledUp);
            Assert.AreEqual(0, result.EarnedRewards.Count);
        }

        [Test]
        public void EinLevelUpVergibtRichtigeBelohnungen()
        {
            // EXP fuer LV 5 erreichen
            var expForLevel5 = PlayerLevelCurve.ExpCumulativeForLevel(5);
            var p = NewProfile(level: 1, expTotal: 0);
            var result = ProgressionEngine.ApplyExp(p, expForLevel5);
            Assert.IsTrue(result.LeveledUp);
            Assert.AreEqual(5, result.NewLevel);
            Assert.AreEqual(1, result.EarnedRewards.Count, "Genau eine Reward-Schwelle (LV 5).");
            Assert.AreEqual(5, result.EarnedRewards[0].Level);
        }

        [Test]
        public void MehrereLevelUpsBeiGrossemExpSprung()
        {
            var expForLevel20 = PlayerLevelCurve.ExpCumulativeForLevel(20);
            var p = NewProfile(level: 1, expTotal: 0);
            var result = ProgressionEngine.ApplyExp(p, expForLevel20);
            Assert.AreEqual(20, result.NewLevel);
            // Rewards bei LV 5, 10, 15, 20 erwartet
            Assert.AreEqual(4, result.EarnedRewards.Count);
            CollectionAssert.AreEqual(new[] { 5, 10, 15, 20 }, System.Linq.Enumerable.Select(result.EarnedRewards, r => r.Level));
        }

        [Test]
        public void NegativeExpWirftException()
        {
            var p = NewProfile(level: 1, expTotal: 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => ProgressionEngine.ApplyExp(p, -10));
        }

        [Test]
        public void ProfileWirdNichtMutiert()
        {
            // ProgressionEngine ist pure — die Anwendung passiert im Service
            var p = NewProfile(level: 1, expTotal: 0);
            ProgressionEngine.ApplyExp(p, 50_000_000);
            Assert.AreEqual(1, p.Level, "Profile.Level darf von der Engine NICHT mutiert werden.");
            Assert.AreEqual(0, p.ExpTotal);
        }
    }
}
