using NUnit.Framework;
using HandwerkerImperium.Domain.Analytics;

namespace HandwerkerImperium.Domain.Tests.Analytics
{
    /// <summary>
    /// Verifiziert die Analytics-Event-Taxonomie: vollständiger eindeutiger Katalog, snake_case-Format, IsKnown.
    /// </summary>
    [TestFixture]
    public class AnalyticsEventsTests
    {
        [Test]
        public void All_AreUnique_AndSnakeCase()
        {
            var all = AnalyticsEvents.All;
            Assert.That(all.Count, Is.EqualTo(13));
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var name in all)
            {
                Assert.That(seen.Add(name), Is.True, "kein Duplikat: " + name);
                Assert.That(name, Is.EqualTo(name.ToLowerInvariant()), "lowercase: " + name);
                Assert.That(name, Does.Not.Contain(" "), "kein Leerzeichen: " + name);
                foreach (char c in name)
                    Assert.That((c >= 'a' && c <= 'z') || c == '_', Is.True, "snake_case: " + name);
            }
        }

        [Test]
        public void IsKnown_TrueForCatalog_FalseOtherwise()
        {
            Assert.That(AnalyticsEvents.IsKnown(AnalyticsEvents.PrestigeDone), Is.True);
            Assert.That(AnalyticsEvents.IsKnown("not_an_event"), Is.False);
        }
    }
}
