#nullable enable
using ArcaneKingdom.Domain.Quest;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class QuestProgressTests
    {
        [Test]
        public void AdvanceErhoehtAberRespektiertTarget()
        {
            var p = new QuestProgress("q1");
            p.Advance(2, 5);
            Assert.AreEqual(2, p.CurrentCount);
            Assert.IsFalse(p.Completed);
            p.Advance(10, 5);
            Assert.AreEqual(5, p.CurrentCount);
            Assert.IsTrue(p.Completed);
        }

        [Test]
        public void ClaimNurNachAbschluss()
        {
            var p = new QuestProgress("q1");
            Assert.IsFalse(p.TryClaim(), "Vor Abschluss nicht claimbar.");
            p.Advance(5, 5);
            Assert.IsTrue(p.TryClaim());
            Assert.IsFalse(p.TryClaim(), "Doppel-Claim verhindert.");
        }

        [Test]
        public void AdvanceNachClaimIsNoOp()
        {
            var p = new QuestProgress("q1");
            p.Advance(5, 5);
            p.TryClaim();
            p.Advance(3, 5);
            Assert.AreEqual(5, p.CurrentCount, "Nach Claim soll Advance nicht mehr greifen.");
        }
    }
}
