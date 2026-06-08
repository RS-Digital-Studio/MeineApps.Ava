using System.Collections.Generic;
using NUnit.Framework;
using HandwerkerImperium.Domain.Restoration;

namespace HandwerkerImperium.Domain.Tests.Restoration
{
    /// <summary>
    /// Verifiziert den Stadt-Wiederaufbau: geometrische Phasenkosten, Phasen-Abschluss durch Investition
    /// (inkl. Rest-Budget), Restbedarf, Abschluss-Erkennung und die Aggregate fürs Stern-Rating.
    /// </summary>
    [TestFixture]
    public class RestorationFormulasTests
    {
        [Test]
        public void PhaseCost_IsGeometric()
        {
            Assert.That(RestorationFormulas.PhaseCost(0, 100m, 2.0), Is.EqualTo(100m));
            Assert.That(RestorationFormulas.PhaseCost(1, 100m, 2.0), Is.EqualTo(200m));
            Assert.That(RestorationFormulas.PhaseCost(2, 100m, 2.0), Is.EqualTo(400m));
        }

        [Test]
        public void Invest_CompletesPhases_WithLeftover()
        {
            var lm = new LandmarkState("marktplatz", 5);
            // Kosten 100/200/400/800/1600. Invest 350 -> Phase0(100)+Phase1(200)=300, Rest 50.
            int done = RestorationFormulas.Invest(lm, 350m, 100m, 2.0);
            Assert.That(done, Is.EqualTo(2));
            Assert.That(lm.PhasesComplete, Is.EqualTo(2));
            Assert.That(lm.InvestedResources, Is.EqualTo(50m));
            // Restbedarf fuer Phase2 (400): 400-50 = 350
            Assert.That(RestorationFormulas.RemainingForNextPhase(lm, 100m, 2.0), Is.EqualTo(350m));
        }

        [Test]
        public void Invest_DoesNotOvercomplete()
        {
            var lm = new LandmarkState("klein", 2);
            int done = RestorationFormulas.Invest(lm, 1_000_000m, 100m, 2.0); // weit mehr als noetig
            Assert.That(done, Is.EqualTo(2));
            Assert.That(lm.PhasesComplete, Is.EqualTo(2));
            Assert.That(RestorationFormulas.IsComplete(lm), Is.True);
            // weitere Investition in fertiges Wahrzeichen -> 0
            Assert.That(RestorationFormulas.Invest(lm, 500m, 100m, 2.0), Is.EqualTo(0));
        }

        [Test]
        public void Aggregates_FeedStarRating()
        {
            var a = new LandmarkState("a", 5); RestorationFormulas.Invest(a, 300m, 100m, 2.0); // 2 Phasen
            var b = new LandmarkState("b", 3); RestorationFormulas.Invest(b, 1_000_000m, 100m, 2.0); // 3 Phasen (komplett)
            var list = new List<LandmarkState> { a, b };
            Assert.That(RestorationFormulas.TotalPhasesComplete(list), Is.EqualTo(5));
            Assert.That(RestorationFormulas.CompletedLandmarks(list), Is.EqualTo(1));
            Assert.That(RestorationFormulas.TotalPhasesComplete(null), Is.EqualTo(0));
        }
    }
}
