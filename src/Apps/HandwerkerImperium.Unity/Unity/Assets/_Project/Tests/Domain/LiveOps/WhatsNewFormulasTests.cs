using NUnit.Framework;
using HandwerkerImperium.Domain.LiveOps;

namespace HandwerkerImperium.Domain.Tests.LiveOps
{
    /// <summary>Verifiziert die What's-New-Anzeigebedingung (nur bei neuerer Version).</summary>
    [TestFixture]
    public class WhatsNewFormulasTests
    {
        [Test]
        public void ShouldShow_OnlyWhenNewerVersion()
        {
            Assert.That(WhatsNewFormulas.ShouldShow(5, 4), Is.True);
            Assert.That(WhatsNewFormulas.ShouldShow(4, 4), Is.False);
            Assert.That(WhatsNewFormulas.ShouldShow(3, 4), Is.False);
        }
    }
}
