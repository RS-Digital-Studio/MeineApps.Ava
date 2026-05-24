#nullable enable
using System;
using ArcaneKingdom.Domain.Guild;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class GuildTests
    {
        [Test]
        public void GuildTagMussExakt5ZeichenSein()
        {
            Assert.Throws<ArgumentException>(() => new Guild("id", "TestName", "ABCD", "leader", DateTime.UtcNow));
            Assert.Throws<ArgumentException>(() => new Guild("id", "TestName", "ABCDEF", "leader", DateTime.UtcNow));
            Assert.DoesNotThrow(() => new Guild("id", "TestName", "ABCDE", "leader", DateTime.UtcNow));
        }

        [Test]
        public void GuildNameMussZwischen3Und20ZeichenSein()
        {
            Assert.Throws<ArgumentException>(() => new Guild("id", "AB", "ABCDE", "leader", DateTime.UtcNow));
            Assert.Throws<ArgumentException>(() => new Guild("id", new string('X', 21), "ABCDE", "leader", DateTime.UtcNow));
            Assert.DoesNotThrow(() => new Guild("id", "ABC", "ABCDE", "leader", DateTime.UtcNow));
        }

        [Test]
        public void MaxMembersSkaliertMitLevel()
        {
            var g = new Guild("id", "TestName", "ABCDE", "leader", DateTime.UtcNow);
            Assert.AreEqual(30, g.MaxMembers);  // LV 1
            g.Level = 5;
            Assert.AreEqual(40, g.MaxMembers);  // LV 5
            g.Level = 10;
            Assert.AreEqual(50, g.MaxMembers);  // LV 10
        }

        [Test]
        public void ContributionRequiredFuerLevelSteigtMonoton()
        {
            long? prev = null;
            for (var lv = 1; lv <= 10; lv++)
            {
                var req = Guild.ContributionRequiredForLevel(lv);
                if (prev.HasValue) Assert.GreaterOrEqual(req, prev.Value, $"LV {lv} sollte >= LV {lv - 1} sein.");
                prev = req;
            }
        }

        [Test]
        public void LevelForContributionFindetRichtigesLevel()
        {
            var g = new Guild("id", "TestName", "ABCDE", "leader", DateTime.UtcNow);
            Assert.AreEqual(1, g.LevelForContribution(99_999));
            Assert.AreEqual(2, g.LevelForContribution(100_000));
            Assert.AreEqual(2, g.LevelForContribution(499_999));
            Assert.AreEqual(3, g.LevelForContribution(500_000));
            Assert.AreEqual(10, g.LevelForContribution(60_000_000));
        }
    }
}
