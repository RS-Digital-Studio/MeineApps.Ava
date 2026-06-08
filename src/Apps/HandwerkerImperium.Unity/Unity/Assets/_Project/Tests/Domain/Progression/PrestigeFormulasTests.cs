using System.Collections.Generic;
using NUnit.Framework;
using HandwerkerImperium.Domain.Progression;

namespace HandwerkerImperium.Domain.Tests.Progression
{
    /// <summary>
    /// Verifiziert Prestige als Akt-Finale: PP = floor(sqrt(Money/100k)), kumulativer Stadt-Multiplikator
    /// (×3/×12/×60), 5★-Gate, Stadt-Index-Fortschritt und das 3-Prestige-Limit.
    /// </summary>
    [TestFixture]
    public class PrestigeFormulasTests
    {
        private static readonly decimal[] Stages = { 3m, 4m, 5m };

        [Test]
        public void PrestigePoints_FloorSqrtMoneyOver100k()
        {
            Assert.That(PrestigeFormulas.PrestigePoints(99_999m), Is.EqualTo(0));
            Assert.That(PrestigeFormulas.PrestigePoints(100_000m), Is.EqualTo(1));   // sqrt(1)
            Assert.That(PrestigeFormulas.PrestigePoints(400_000m), Is.EqualTo(2));   // sqrt(4)
            Assert.That(PrestigeFormulas.PrestigePoints(900_000m), Is.EqualTo(3));   // sqrt(9)
            Assert.That(PrestigeFormulas.PrestigePoints(1_000_000m), Is.EqualTo(3)); // floor(sqrt(10))
            Assert.That(PrestigeFormulas.PrestigePoints(2_500_000m), Is.EqualTo(5)); // sqrt(25)
        }

        [Test]
        public void CityMultiplier_CumulativeProduct()
        {
            Assert.That(PrestigeFormulas.CityMultiplier(0, Stages), Is.EqualTo(1m));
            Assert.That(PrestigeFormulas.CityMultiplier(1, Stages), Is.EqualTo(3m));
            Assert.That(PrestigeFormulas.CityMultiplier(2, Stages), Is.EqualTo(12m));
            Assert.That(PrestigeFormulas.CityMultiplier(3, Stages), Is.EqualTo(60m));
            Assert.That(PrestigeFormulas.CityMultiplier(9, Stages), Is.EqualTo(60m), "ueber Stufen-Anzahl geklemmt");
            Assert.That(PrestigeFormulas.CityMultiplier(2, null), Is.EqualTo(1m));
        }

        [Test]
        public void CanPrestige_RequiresFiveStars_AndUnderLimit()
        {
            Assert.That(PrestigeFormulas.CanPrestige(5, 0, 3), Is.True);
            Assert.That(PrestigeFormulas.CanPrestige(4, 0, 3), Is.False, "unter 5 Sternen");
            Assert.That(PrestigeFormulas.CanPrestige(5, 3, 3), Is.False, "Limit ausgeschoepft");
        }

        [Test]
        public void NextCityIndex_And_FinalCity()
        {
            Assert.That(PrestigeFormulas.NextCityIndex(0, 3), Is.EqualTo(1));
            Assert.That(PrestigeFormulas.NextCityIndex(2, 3), Is.EqualTo(3));
            Assert.That(PrestigeFormulas.NextCityIndex(3, 3), Is.EqualTo(3));
            Assert.That(PrestigeFormulas.IsFinalCity(3, 3), Is.True);
            Assert.That(PrestigeFormulas.IsFinalCity(2, 3), Is.False);
        }
    }
}
