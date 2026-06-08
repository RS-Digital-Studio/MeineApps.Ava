using NUnit.Framework;
using HandwerkerImperium.Domain.Franchise;

namespace HandwerkerImperium.Domain.Tests.Franchise
{
    /// <summary>
    /// Verifiziert die World-Tier-Skalierung der 4 Städte: Stadt-Index-Klemmung, steigende Stern-Schwellen
    /// und Income-Ziele je Stadt, Endstadt-Erkennung.
    /// </summary>
    [TestFixture]
    public class WorldTierFormulasTests
    {
        [Test]
        public void CityCount_AndClamp()
        {
            Assert.That(WorldTierFormulas.CityCount, Is.EqualTo(4));
            Assert.That(WorldTierFormulas.ClampCityIndex(-1), Is.EqualTo(0));
            Assert.That(WorldTierFormulas.ClampCityIndex(9), Is.EqualTo(3));
            Assert.That(WorldTierFormulas.ClampCityIndex(2), Is.EqualTo(2));
        }

        [Test]
        public void StarThresholdScale_RisesPerCity()
        {
            Assert.That(WorldTierFormulas.StarThresholdScale(0, 1.5), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(WorldTierFormulas.StarThresholdScale(1, 1.5), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(WorldTierFormulas.StarThresholdScale(2, 1.5), Is.EqualTo(2.25).Within(1e-9));
            Assert.That(WorldTierFormulas.StarThresholdScale(3, 1.5), Is.EqualTo(3.375).Within(1e-9));
        }

        [Test]
        public void CityIncomeTargetScale_Geometric()
        {
            Assert.That(WorldTierFormulas.CityIncomeTargetScale(0, 10m), Is.EqualTo(1m));
            Assert.That(WorldTierFormulas.CityIncomeTargetScale(1, 10m), Is.EqualTo(10m));
            Assert.That(WorldTierFormulas.CityIncomeTargetScale(3, 10m), Is.EqualTo(1000m));
        }

        [Test]
        public void IsFinalCity_OnlyMetropole()
        {
            Assert.That(WorldTierFormulas.IsFinalCity(3), Is.True);
            Assert.That(WorldTierFormulas.IsFinalCity(2), Is.False);
        }
    }
}
