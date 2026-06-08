using NUnit.Framework;
using HandwerkerImperium.Domain.Save;

namespace HandwerkerImperium.Domain.Tests.Save
{
    /// <summary>
    /// Verifiziert die Cloud-Save-Konfliktauflösung: Cloud neuer → Alert (kein Overwrite), lokal neuer → Upload,
    /// gleich → InSync.
    /// </summary>
    [TestFixture]
    public class CloudSaveFormulasTests
    {
        [Test]
        public void Resolve_ByRevision()
        {
            Assert.That(CloudSaveFormulas.Resolve(5, 3), Is.EqualTo(CloudSyncResolution.UseLocal));
            Assert.That(CloudSaveFormulas.Resolve(3, 5), Is.EqualTo(CloudSyncResolution.ConflictAlert));
            Assert.That(CloudSaveFormulas.Resolve(5, 5), Is.EqualTo(CloudSyncResolution.InSync));
        }

        [Test]
        public void ShouldUpload_WhenLocalAtLeastAsNew()
        {
            Assert.That(CloudSaveFormulas.ShouldUpload(5, 3), Is.True);
            Assert.That(CloudSaveFormulas.ShouldUpload(5, 5), Is.True);
            Assert.That(CloudSaveFormulas.ShouldUpload(3, 5), Is.False);
        }
    }
}
