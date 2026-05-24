#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Collection;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class CollectionEvaluatorTests
    {
        private static CollectionSet WhiteHeart() => new()
        {
            Id = "white_heart",
            RequiredMaterialIds = new() { "helle_sphaere", "sterne_splitter", "engelsfeder", "heiliges_wasser" },
            RewardCardId = "engelsritter"
        };

        [Test]
        public void VollstaendigesSetIstComplete()
        {
            var owned = new HashSet<string> { "helle_sphaere", "sterne_splitter", "engelsfeder", "heiliges_wasser", "andere_karte" };
            var progress = CollectionEvaluator.Evaluate(WhiteHeart(), owned);
            Assert.IsTrue(progress.IsComplete);
            Assert.AreEqual(4, progress.OwnedCount);
            Assert.AreEqual(0, progress.MissingMaterialIds.Count);
        }

        [Test]
        public void UnvollstaendigesSetListetMissing()
        {
            var owned = new HashSet<string> { "helle_sphaere", "engelsfeder" };
            var progress = CollectionEvaluator.Evaluate(WhiteHeart(), owned);
            Assert.IsFalse(progress.IsComplete);
            Assert.AreEqual(2, progress.OwnedCount);
            CollectionAssert.AreEquivalent(new[] { "sterne_splitter", "heiliges_wasser" }, progress.MissingMaterialIds);
        }

        [Test]
        public void LeereSammlungZeigtAlleAlsMissing()
        {
            var progress = CollectionEvaluator.Evaluate(WhiteHeart(), new HashSet<string>());
            Assert.AreEqual(0, progress.OwnedCount);
            Assert.AreEqual(4, progress.MissingMaterialIds.Count);
        }

        [Test]
        public void TotalCountStimmtMitRequiredIds()
        {
            var progress = CollectionEvaluator.Evaluate(WhiteHeart(), new HashSet<string>());
            Assert.AreEqual(4, progress.TotalCount);
        }
    }
}
