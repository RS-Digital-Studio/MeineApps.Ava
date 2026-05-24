#nullable enable
using System;
using ArcaneKingdom.Domain.World;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class MaterialDropResolverTests
    {
        private static NodeMaterialDropTable MakeTable(string nodeId, string matId, float c1, float c2, float c3, float c4)
        {
            return new NodeMaterialDropTable
            {
                NodeId = nodeId,
                Drops = new[] { new MaterialDropEntry { MaterialId = matId, ChanceOneStar = c1, ChanceTwoStar = c2, ChanceThreeStar = c3, ChanceFourStar = c4 } }
            };
        }

        [Test]
        public void ChanceNullErgibtKeinDrop()
        {
            var table = MakeTable("n", "x", 0f, 0f, 0f, 0f);
            var drops = MaterialDropResolver.RollDrops(table, 4, new Random(0));
            Assert.AreEqual(0, drops.Count);
        }

        [Test]
        public void ChanceEinsGarantiertDrop()
        {
            var table = MakeTable("n", "x", 1f, 1f, 1f, 1f);
            var drops = MaterialDropResolver.RollDrops(table, 1, new Random(0));
            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual("x", drops[0]);
        }

        [Test]
        public void DeterministischMitGleichemSeed()
        {
            var table = MakeTable("n", "x", 0.5f, 0.5f, 0.5f, 0.5f);
            var a = MaterialDropResolver.RollDrops(table, 2, new Random(42));
            var b = MaterialDropResolver.RollDrops(table, 2, new Random(42));
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void UngueltigerStarsParameterWirft()
        {
            var table = MakeTable("n", "x", 0.5f, 0.5f, 0.5f, 0.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => MaterialDropResolver.RollDrops(table, 0, new Random()));
            Assert.Throws<ArgumentOutOfRangeException>(() => MaterialDropResolver.RollDrops(table, 5, new Random()));
        }
    }
}
