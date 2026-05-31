#nullable enable
using ArcaneKingdom.Domain.Battle;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    /// <summary>
    /// Regressions-Tests fuer den Mulberry32-PRNG (DeterministicRng).
    /// Die Anker sind BIT-IDENTISCH zu den Ankern in der TypeScript-Portierung
    /// (Server/CloudFunctions/src/battle/engine/__tests__/deterministicRng.test.ts) — der
    /// Cross-Test gegen die TS-Engine wurde bestaetigt (C# == TS). Diese Tests sichern den
    /// gemeinsamen RNG gegen kuenftige Regression, sodass Client- und Server-Replay
    /// deterministisch dieselbe Sequenz erzeugen.
    /// </summary>
    public sealed class DeterministicRngTests
    {
        [Test]
        public void NextUInt_LiefertFixierteRegressionsAnker_IdentischZurTsPortierung()
        {
            AssertSeq(0,          new uint[] { 1144304738u, 1416247u, 958946056u, 627933444u, 2007157716u });
            AssertSeq(1,          new uint[] { 2693262067u, 11749833u, 2265367787u, 4213581821u, 4159151403u });
            AssertSeq(12345,      new uint[] { 4207900869u, 1317490944u, 2079646450u, 3513001552u, 2187978186u });
            AssertSeq(-1,         new uint[] { 3850105811u, 813802916u, 3073704848u, 4054706436u, 3630262831u });
            AssertSeq(2147483647, new uint[] { 1842962257u, 546041740u, 1654754255u, 1702490205u, 513796057u });
        }

        private static void AssertSeq(int seed, uint[] expected)
        {
            var rng = new DeterministicRng(seed);
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], rng.NextUInt(), $"Seed {seed}, Index {i}");
        }

        [Test]
        public void Next_BleibtImBereichUndDeterministisch()
        {
            var rng = new DeterministicRng(42);
            var got = new int[8];
            for (var i = 0; i < 8; i++) got[i] = rng.Next(6);
            CollectionAssert.AreEqual(new[] { 0, 4, 0, 5, 0, 3, 4, 5 }, got);

            // maxExclusive <= 0 liefert 0
            var rng2 = new DeterministicRng(7);
            Assert.AreEqual(0, rng2.Next(0));
            Assert.AreEqual(0, rng2.Next(-5));
        }

        [Test]
        public void GleicherSeed_ErzeugtIdentischeSequenz()
        {
            var a = new DeterministicRng(13371337);
            var b = new DeterministicRng(13371337);
            for (var i = 0; i < 200; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void NextDouble_LiegtInNullBisEins()
        {
            var rng = new DeterministicRng(2024);
            for (var i = 0; i < 1000; i++)
            {
                var d = rng.NextDouble();
                Assert.GreaterOrEqual(d, 0.0);
                Assert.Less(d, 1.0);
            }
        }
    }
}
