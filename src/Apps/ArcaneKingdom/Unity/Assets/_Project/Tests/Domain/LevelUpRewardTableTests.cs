#nullable enable
using ArcaneKingdom.Domain.Progression;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class LevelUpRewardTableTests
    {
        [Test]
        public void LV5IstDefiniertUndHatBelohnung()
        {
            Assert.IsTrue(LevelUpRewardTable.TryGet(5, out var reward));
            Assert.AreEqual(1, reward.CommonPacks);
            Assert.Greater(reward.Diamonds, 0);
        }

        [Test]
        public void LV15FreiSchaltetArena()
        {
            Assert.IsTrue(LevelUpRewardTable.TryGet(15, out var reward));
            Assert.AreEqual("feature.arena", reward.UnlockedFeatureKey);
        }

        [Test]
        public void LV20FreiSchaltetRuneSlot2()
        {
            Assert.IsTrue(LevelUpRewardTable.TryGet(20, out var reward));
            Assert.AreEqual(2, reward.RuneSlotUnlocked);
        }

        [Test]
        public void LV100GibtAvatarRahmen()
        {
            Assert.IsTrue(LevelUpRewardTable.TryGet(100, out var reward));
            Assert.IsNotNull(reward.AvatarFrameKey);
        }

        [Test]
        public void NichtDefinierteLevelGebenFalseZurueck()
        {
            Assert.IsFalse(LevelUpRewardTable.TryGet(7, out _));
            Assert.IsFalse(LevelUpRewardTable.TryGet(99, out _));
        }
    }
}
