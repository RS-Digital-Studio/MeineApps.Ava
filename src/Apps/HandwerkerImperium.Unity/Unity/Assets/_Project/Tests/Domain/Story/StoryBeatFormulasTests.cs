using System.Collections.Generic;
using NUnit.Framework;
using HandwerkerImperium.Domain.Story;

namespace HandwerkerImperium.Domain.Tests.Story
{
    /// <summary>
    /// Verifiziert die Story-Beat-Auswahl: Auslöser-gefiltert, gespielt-gefiltert, nach Order sortiert.
    /// </summary>
    [TestFixture]
    public class StoryBeatFormulasTests
    {
        private static List<StoryBeatDefinition> Catalog() => new List<StoryBeatDefinition>
        {
            new StoryBeatDefinition("gs_1", StoryTrigger.GameStart, 1),
            new StoryBeatDefinition("gs_0", StoryTrigger.GameStart, 0),
            new StoryBeatDefinition("worker", StoryTrigger.FirstWorkerHired, 0),
        };

        [Test]
        public void BeatsForTrigger_FiltersAndSortsByOrder()
        {
            var beats = StoryBeatFormulas.BeatsForTrigger(Catalog(), StoryTrigger.GameStart, null);
            Assert.That(beats, Is.EqualTo(new[] { "gs_0", "gs_1" }).AsCollection, "nach Order sortiert");

            var worker = StoryBeatFormulas.BeatsForTrigger(Catalog(), StoryTrigger.FirstWorkerHired, null);
            Assert.That(worker, Is.EqualTo(new[] { "worker" }).AsCollection);
        }

        [Test]
        public void BeatsForTrigger_ExcludesAlreadyPlayed()
        {
            var played = new List<string> { "gs_0" };
            var beats = StoryBeatFormulas.BeatsForTrigger(Catalog(), StoryTrigger.GameStart, played);
            Assert.That(beats, Is.EqualTo(new[] { "gs_1" }).AsCollection);
        }

        [Test]
        public void BeatsForTrigger_NullCatalog_Empty()
        {
            Assert.That(StoryBeatFormulas.BeatsForTrigger(null, StoryTrigger.GameStart, null), Is.Empty);
        }
    }
}
