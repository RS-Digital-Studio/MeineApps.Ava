using NUnit.Framework;
using HandwerkerImperium.Domain.Common;

namespace HandwerkerImperium.Domain.Tests.Common
{
    /// <summary>
    /// Verifiziert den stabilen FNV-1a-Hash: Determinismus (gleicher Input → gleicher Hash), Unterscheidung
    /// verschiedener Inputs, Bucket im Bereich. Wichtig, weil string.GetHashCode unter IL2CPP randomisiert ist.
    /// </summary>
    [TestFixture]
    public class StableHashTests
    {
        [Test]
        public void Fnv1a_IsDeterministic_AndDistinguishes()
        {
            Assert.That(StableHash.Fnv1a("abc"), Is.EqualTo(StableHash.Fnv1a("abc")));
            Assert.That(StableHash.Fnv1a("abc"), Is.Not.EqualTo(StableHash.Fnv1a("abd")));
            Assert.That(StableHash.Fnv1a(""), Is.EqualTo(StableHash.Fnv1a(null)), "leer/null gleicher Basiswert");
        }

        [Test]
        public void Bucket_InRange_AndDeterministic()
        {
            for (int i = 0; i < 50; i++)
            {
                int b = StableHash.Bucket("player_" + i, 10);
                Assert.That(b, Is.InRange(0, 9));
            }
            Assert.That(StableHash.Bucket("xyz", 10), Is.EqualTo(StableHash.Bucket("xyz", 10)));
            Assert.That(StableHash.Bucket("xyz", 1), Is.EqualTo(0));
        }
    }
}
