#nullable enable
using System;
using ArcaneKingdom.Domain.Cards;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class CardInstanceTests
    {
        [Test]
        public void StatBonusBeiLevel0Ist1()
        {
            var c = NewInstance(level: 0);
            Assert.AreEqual(1.00f, c.StatBonusMultiplier);
        }

        [Test]
        public void StatBonusBeiLevel5Ist125Prozent()
        {
            var c = NewInstance(level: 5);
            Assert.AreEqual(1.25f, c.StatBonusMultiplier);
        }

        [Test]
        public void StatBonusBeiMaxLevelIst180Prozent()
        {
            var c = NewInstance(level: 15);
            Assert.AreEqual(1.80f, c.StatBonusMultiplier);
        }

        [Test]
        public void ZweiteFaehigkeitIstAbLevel5Aktiv()
        {
            Assert.IsFalse(NewInstance(level: 4).HasSecondAbilityUnlocked);
            Assert.IsTrue(NewInstance(level: 5).HasSecondAbilityUnlocked);
        }

        [Test]
        public void DritteFaehigkeitIstAbLevel10Aktiv()
        {
            Assert.IsFalse(NewInstance(level: 9).HasThirdAbilityUnlocked);
            Assert.IsTrue(NewInstance(level: 10).HasThirdAbilityUnlocked);
        }

        [Test]
        public void IsMaxLevelAbLevel15True()
        {
            Assert.IsFalse(NewInstance(level: 14).IsMaxLevel);
            Assert.IsTrue(NewInstance(level: 15).IsMaxLevel);
        }

        [Test]
        public void ApplyLevelUpKannLevelNichtSenken()
        {
            var c = NewInstance(level: 5);
            Assert.Throws<InvalidOperationException>(() => c.ApplyLevelUp(4));
        }

        [Test]
        public void ApplyLevelUpKannMaxLevelNichtUeberschreiten()
        {
            var c = NewInstance(level: 14);
            Assert.Throws<ArgumentOutOfRangeException>(() => c.ApplyLevelUp(16));
        }

        private static CardInstance NewInstance(int level) =>
            new CardInstance("inst-1", "card_test", level, 0, DateTime.UtcNow);
    }
}
