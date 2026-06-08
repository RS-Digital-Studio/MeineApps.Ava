using NUnit.Framework;
using HandwerkerImperium.Domain.Progression;

namespace HandwerkerImperium.Domain.Tests.Progression
{
    /// <summary>
    /// Verifiziert den Meisterschafts-Track: geometrische XP-Kurve (1.15^N), Gesamt-XP-Summe,
    /// die Inverse (Level aus Gesamt-XP) und den per-Level-Bonus.
    /// </summary>
    [TestFixture]
    public class MasteryFormulasTests
    {
        private const double Base = 100.0;
        private const double Growth = 1.15;

        [Test]
        public void XpForLevelStep_Geometric()
        {
            Assert.That(MasteryFormulas.XpForLevelStep(0, Base, Growth), Is.EqualTo(100.0).Within(1e-6));
            Assert.That(MasteryFormulas.XpForLevelStep(1, Base, Growth), Is.EqualTo(115.0).Within(1e-6));
            Assert.That(MasteryFormulas.XpForLevelStep(2, Base, Growth), Is.EqualTo(132.25).Within(1e-6));
        }

        [Test]
        public void TotalXpForLevel_GeometricSum()
        {
            Assert.That(MasteryFormulas.TotalXpForLevel(0, Base, Growth), Is.EqualTo(0.0).Within(1e-6));
            Assert.That(MasteryFormulas.TotalXpForLevel(1, Base, Growth), Is.EqualTo(100.0).Within(1e-6));
            Assert.That(MasteryFormulas.TotalXpForLevel(2, Base, Growth), Is.EqualTo(215.0).Within(1e-6)); // 100+115
            Assert.That(MasteryFormulas.TotalXpForLevel(3, Base, Growth), Is.EqualTo(347.25).Within(1e-6)); // +132.25
        }

        [Test]
        public void LevelForTotalXp_InvertsTotal()
        {
            Assert.That(MasteryFormulas.LevelForTotalXp(0, Base, Growth), Is.EqualTo(0));
            Assert.That(MasteryFormulas.LevelForTotalXp(99, Base, Growth), Is.EqualTo(0));
            Assert.That(MasteryFormulas.LevelForTotalXp(100, Base, Growth), Is.EqualTo(1));
            Assert.That(MasteryFormulas.LevelForTotalXp(214, Base, Growth), Is.EqualTo(1));
            Assert.That(MasteryFormulas.LevelForTotalXp(215, Base, Growth), Is.EqualTo(2));
            Assert.That(MasteryFormulas.LevelForTotalXp(347.25, Base, Growth), Is.EqualTo(3));
        }

        [Test]
        public void GlobalIncomeBonus_LinearPerLevel()
        {
            Assert.That(MasteryFormulas.GlobalIncomeBonus(10, 0.01m), Is.EqualTo(0.10m));
            Assert.That(MasteryFormulas.GlobalIncomeBonus(0, 0.01m), Is.EqualTo(0m));
            Assert.That(MasteryFormulas.GlobalIncomeBonus(-5, 0.01m), Is.EqualTo(0m));
        }
    }
}
