using NUnit.Framework;
using HandwerkerImperium.Game;

namespace HandwerkerImperium.Game.Tests
{
    /// <summary>Verifiziert das Idle-Kurzformat (deutsche Schreibweise, abschneidend statt rundend).</summary>
    [TestFixture]
    public class MoneyFormatTests
    {
        [Test]
        public void Small_FullWithThousandsSeparator()
        {
            Assert.That(MoneyFormat.Short(0m), Is.EqualTo("0"));
            Assert.That(MoneyFormat.Short(950m), Is.EqualTo("950"));
            Assert.That(MoneyFormat.Short(9999.9m), Is.EqualTo("9.999"), "unter 10k voll, abgeschnitten");
        }

        [Test]
        public void Stepped_K_M_B_T()
        {
            Assert.That(MoneyFormat.Short(10_000m), Is.EqualTo("10,0k"));
            Assert.That(MoneyFormat.Short(12_540m), Is.EqualTo("12,5k"));
            Assert.That(MoneyFormat.Short(999_999m), Is.EqualTo("999,9k"), "abschneiden, nie '1000,0k'");
            Assert.That(MoneyFormat.Short(3_250_000m), Is.EqualTo("3,2M"));
            Assert.That(MoneyFormat.Short(1_500_000_000m), Is.EqualTo("1,5B"));
            Assert.That(MoneyFormat.Short(2_000_000_000_000m), Is.EqualTo("2,0T"));
        }

        [Test]
        public void Negative_KeepsSign()
        {
            Assert.That(MoneyFormat.Short(-12_540m), Is.EqualTo("-12,5k"));
        }
    }
}
