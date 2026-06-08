using NUnit.Framework;
using HandwerkerImperium.Domain.Progression;

namespace HandwerkerImperium.Domain.Tests.Progression
{
    /// <summary>
    /// Verifiziert den Endgame-Meistergrad-Loop: geometrische Renommee-Kosten (1.5^R), langsame Akkumulation,
    /// Kauf-Bedingung und per-Grad-Bonus.
    /// </summary>
    [TestFixture]
    public class MeistergradFormulasTests
    {
        [Test]
        public void RenommeeCost_Geometric_1point5()
        {
            Assert.That(MeistergradFormulas.RenommeeCost(0, 100m, 1.5), Is.EqualTo(100m));
            Assert.That(MeistergradFormulas.RenommeeCost(1, 100m, 1.5), Is.EqualTo(150m));
            Assert.That(MeistergradFormulas.RenommeeCost(2, 100m, 1.5), Is.EqualTo(225m));
        }

        [Test]
        public void CanPurchase_RequiresEnoughRenommee()
        {
            Assert.That(MeistergradFormulas.CanPurchase(150m, 1, 100m, 1.5), Is.True);  // Kosten 150
            Assert.That(MeistergradFormulas.CanPurchase(149m, 1, 100m, 1.5), Is.False);
        }

        [Test]
        public void AccrueRenommee_ScalesWithIncomeAndTime()
        {
            Assert.That(MeistergradFormulas.AccrueRenommee(10m, 5.0, 0.01m), Is.EqualTo(0.5m));
            Assert.That(MeistergradFormulas.AccrueRenommee(0m, 5.0, 0.01m), Is.EqualTo(0m));
        }

        [Test]
        public void GlobalBonus_LinearPerGrade()
        {
            Assert.That(MeistergradFormulas.GlobalBonus(5, 0.01m), Is.EqualTo(0.05m));
            Assert.That(MeistergradFormulas.GlobalBonus(0, 0.01m), Is.EqualTo(0m));
        }
    }
}
