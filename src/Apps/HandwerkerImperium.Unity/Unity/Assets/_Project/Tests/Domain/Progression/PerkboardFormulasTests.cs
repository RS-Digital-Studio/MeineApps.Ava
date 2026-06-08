using NUnit.Framework;
using HandwerkerImperium.Domain.Progression;

namespace HandwerkerImperium.Domain.Tests.Progression
{
    /// <summary>
    /// Verifiziert das Imperium-Marken-Perkboard: Marken aus Prestiges, geometrische Stufenkosten,
    /// linearer Bonus je Stufe (mit MaxLevel-Klemmung) und die Kauf-Bedingung.
    /// </summary>
    [TestFixture]
    public class PerkboardFormulasTests
    {
        [Test]
        public void MarksFromPrestige_Linear()
        {
            Assert.That(PerkboardFormulas.MarksFromPrestige(3, 5), Is.EqualTo(15));
            Assert.That(PerkboardFormulas.MarksFromPrestige(0, 5), Is.EqualTo(0));
        }

        [Test]
        public void MarkCost_Geometric_CeilMinOne()
        {
            Assert.That(PerkboardFormulas.MarkCost(0, 10, 1.5), Is.EqualTo(10));
            Assert.That(PerkboardFormulas.MarkCost(1, 10, 1.5), Is.EqualTo(15));
            Assert.That(PerkboardFormulas.MarkCost(2, 10, 1.5), Is.EqualTo(23)); // ceil(22.5)
        }

        [Test]
        public void BonusAtLevel_Linear_ClampedToMax()
        {
            Assert.That(PerkboardFormulas.BonusAtLevel(3, 5, 0.1m), Is.EqualTo(0.3m));
            Assert.That(PerkboardFormulas.BonusAtLevel(7, 5, 0.1m), Is.EqualTo(0.5m), "auf MaxLevel geklemmt");
            Assert.That(PerkboardFormulas.BonusAtLevel(-1, 5, 0.1m), Is.EqualTo(0m));
        }

        [Test]
        public void CanBuy_RequiresMarks_AndUnderMax()
        {
            Assert.That(PerkboardFormulas.CanBuy(15, 1, 5, 10, 1.5), Is.True);   // Kosten 15
            Assert.That(PerkboardFormulas.CanBuy(14, 1, 5, 10, 1.5), Is.False);  // zu wenig
            Assert.That(PerkboardFormulas.CanBuy(999, 5, 5, 10, 1.5), Is.False); // MaxLevel
        }
    }
}
