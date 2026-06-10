using NUnit.Framework;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Config;
using HandwerkerImperium.Domain.Restoration;
using HandwerkerImperium.Domain.Runtime;

namespace HandwerkerImperium.Domain.Tests.Restoration
{
    /// <summary>
    /// Verifiziert den Wahrzeichen-Lebenszyklus (Stadt-Wiederaufbau, GDD §6.4): Katalog-Spawn bei
    /// Spielstart, Save-Migration (EnsureLandmarks) und frische Wahrzeichen nach dem Prestige.
    /// </summary>
    [TestFixture]
    public class LandmarkCatalogTests
    {
        [Test]
        public void CreateNew_SpawnsRuinedCatalogLandmarks()
        {
            var m = GameModel.CreateNew(new IdleBalancing());
            Assert.That(m.Landmarks.Count, Is.EqualTo(3), "Akt-1-Katalog: brunnen/glockenturm/stadttor");
            Assert.That(m.Landmarks.Find(l => l.Id == "brunnen").TotalPhases, Is.EqualTo(3));
            Assert.That(m.Landmarks.Find(l => l.Id == "glockenturm").TotalPhases, Is.EqualTo(4));
            Assert.That(m.Landmarks.Find(l => l.Id == "stadttor").TotalPhases, Is.EqualTo(5));
            foreach (var lm in m.Landmarks)
                Assert.That(lm.PhasesComplete, Is.EqualTo(0), "startet ruiniert");
        }

        [Test]
        public void EnsureLandmarks_AddsMissing_KeepsProgress()
        {
            var list = new System.Collections.Generic.List<LandmarkState>
            {
                new LandmarkState("brunnen", 3) { PhasesComplete = 2 }
            };
            LandmarkCatalog.EnsureLandmarks(list);
            Assert.That(list.Count, Is.EqualTo(3), "fehlende Katalog-Wahrzeichen ergaenzt");
            Assert.That(list.Find(l => l.Id == "brunnen").PhasesComplete, Is.EqualTo(2), "Fortschritt unangetastet");
        }

        [Test]
        public void Prestige_ResetsLandmarks_ToFreshRuins()
        {
            var ib = new IdleBalancing();
            var bal = new GameBalancing();
            var m = GameModel.CreateNew(ib);
            m.Landmarks.Find(l => l.Id == "brunnen").PhasesComplete = 3; // saniert
            m.Meta.CurrentStar = 5;
            m.Idle.Money = 1_000_000m;

            Assert.That(GameSimulation.TryPrestige(m, ib, bal), Is.True);
            Assert.That(m.Landmarks.Count, Is.EqualTo(3), "neue Stadt hat wieder alle Katalog-Wahrzeichen");
            foreach (var lm in m.Landmarks)
                Assert.That(lm.PhasesComplete, Is.EqualTo(0), "Wahrzeichen starten wieder ruiniert");
        }
    }
}
