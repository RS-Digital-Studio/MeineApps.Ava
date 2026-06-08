using NUnit.Framework;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Tests.Economy
{
    /// <summary>
    /// Verifiziert den Log2-Soft-Cap (geborgene Original-Mathematik, schlank neu): linear unter der Schwelle,
    /// logarithmische Dämpfung darüber, nie Verstärkung.
    /// </summary>
    [TestFixture]
    public class IncomeSoftCapTests
    {
        [Test]
        public void SoftCapMultiplier_LinearBelowThreshold()
        {
            Assert.That(IncomeSoftCap.SoftCapMultiplier(3.0, 4.0), Is.EqualTo(3.0).Within(1e-9));
            Assert.That(IncomeSoftCap.SoftCapMultiplier(4.0, 4.0), Is.EqualTo(4.0).Within(1e-9));
        }

        [Test]
        public void SoftCapMultiplier_DampensExcess()
        {
            // excess=1 -> log2(2)=1 -> softened=5 == M (keine Reduktion)
            Assert.That(IncomeSoftCap.SoftCapMultiplier(5.0, 4.0), Is.EqualTo(5.0).Within(1e-9));
            // excess=3 -> log2(4)=2 -> softened=6 (< 7)
            Assert.That(IncomeSoftCap.SoftCapMultiplier(7.0, 4.0), Is.EqualTo(6.0).Within(1e-9));
            // excess=8 -> log2(9)=3.169925 -> 7.169925
            Assert.That(IncomeSoftCap.SoftCapMultiplier(12.0, 4.0), Is.EqualTo(7.169925).Within(1e-5));
        }

        [Test]
        public void SoftCapMultiplier_NeverAmplifies_ForSmallExcess()
        {
            // excess=0.5 -> log2(1.5)=0.585 > 0.5 -> geklemmt auf M (4.5)
            Assert.That(IncomeSoftCap.SoftCapMultiplier(4.5, 4.0), Is.EqualTo(4.5).Within(1e-9));
        }

        [Test]
        public void ApplySoftCap_DecimalPath()
        {
            Assert.That(IncomeSoftCap.ApplySoftCap(100m, 3m, 4m), Is.EqualTo(300m)); // unter Schwelle: linear
            Assert.That(IncomeSoftCap.ApplySoftCap(100m, 7m, 4m), Is.EqualTo(600m)); // gedämpft auf x6
            Assert.That(IncomeSoftCap.ApplySoftCap(0m, 7m, 4m), Is.EqualTo(0m));
            Assert.That(IncomeSoftCap.ApplySoftCap(100m, 0m, 4m), Is.EqualTo(0m));
        }
    }
}
