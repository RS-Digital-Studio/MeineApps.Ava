#nullable enable
using ArcaneKingdom.Domain.Guild;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class TerritoryTests
    {
        [Test]
        public void GoldBonusSteigtMitRarity()
        {
            Assert.AreEqual(1_000, new Territory("t1", "k", TerritoryRarity.Common).DailyGoldPerMember);
            Assert.AreEqual(3_000, new Territory("t2", "k", TerritoryRarity.Rare).DailyGoldPerMember);
            Assert.AreEqual(8_000, new Territory("t3", "k", TerritoryRarity.Epic).DailyGoldPerMember);
            Assert.AreEqual(20_000, new Territory("t4", "k", TerritoryRarity.Legendaer).DailyGoldPerMember);
        }

        [Test]
        public void MindestGebotEntsprichtRarity()
        {
            Assert.AreEqual(50_000,    new Territory("t1", "k", TerritoryRarity.Common).MinBidAmount);
            Assert.AreEqual(200_000,   new Territory("t2", "k", TerritoryRarity.Rare).MinBidAmount);
            Assert.AreEqual(500_000,   new Territory("t3", "k", TerritoryRarity.Epic).MinBidAmount);
            Assert.AreEqual(1_500_000, new Territory("t4", "k", TerritoryRarity.Legendaer).MinBidAmount);
        }

        [Test]
        public void DiamondsNurAbEpic()
        {
            Assert.AreEqual(0,   new Territory("t1", "k", TerritoryRarity.Common).DailyDiamondsPerMember);
            Assert.AreEqual(0,   new Territory("t2", "k", TerritoryRarity.Rare).DailyDiamondsPerMember);
            Assert.AreEqual(50,  new Territory("t3", "k", TerritoryRarity.Epic).DailyDiamondsPerMember);
            Assert.AreEqual(100, new Territory("t4", "k", TerritoryRarity.Legendaer).DailyDiamondsPerMember);
        }
    }
}
