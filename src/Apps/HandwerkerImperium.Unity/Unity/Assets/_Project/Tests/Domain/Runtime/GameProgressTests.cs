using NUnit.Framework;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Achievements;
using HandwerkerImperium.Domain.Story;
using HandwerkerImperium.Domain.Config;
using HandwerkerImperium.Domain.Runtime;

namespace HandwerkerImperium.Domain.Tests.Runtime
{
    /// <summary>
    /// Verifiziert die live-Fortschritts-Auswertung: Master-Tool-Auto-Sammlung, Achievement-Gutschrift (Gems)
    /// und die Meisterschafts-XP-Akkumulation aus dem laufenden Verdienst im Tick.
    /// </summary>
    [TestFixture]
    public class GameProgressTests
    {
        [Test]
        public void CollectEligibleMasterTools_CollectsEligible_Idempotent()
        {
            var ib = new IdleBalancing();
            var m = GameModel.CreateNew(ib);
            var cat = MasterToolFormulas.DefaultCatalog();
            m.Idle.StationSpeedLevel = 75; // golden_hammer (MaxStationLevel 75) wird erfuellt

            var newly = GameProgress.CollectEligibleMasterTools(m, cat);
            Assert.That(newly, Contains.Item("mt_golden_hammer"));
            Assert.That(m.CollectedMasterTools, Contains.Item("mt_golden_hammer"));

            var again = GameProgress.CollectEligibleMasterTools(m, cat);
            Assert.That(again, Does.Not.Contain("mt_golden_hammer"), "nicht doppelt sammeln");
        }

        [Test]
        public void GrantNewAchievements_GrantsGems_Idempotent()
        {
            var m = GameModel.CreateNew(new IdleBalancing());
            var cat = AchievementCatalog.Default();
            m.Orders.TotalServed = 10;

            int gems = GameProgress.GrantNewAchievements(m, cat);
            Assert.That(gems, Is.EqualTo(10), "orders_10 -> 10 Gems");
            Assert.That(m.Gems, Is.EqualTo(10m));
            Assert.That(m.ClaimedAchievements, Contains.Item("orders_10"));

            Assert.That(GameProgress.GrantNewAchievements(m, cat), Is.EqualTo(0), "schon eingeloest -> 0");
        }

        [Test]
        public void Tick_AccruesMasteryXp_FromWorkerIncome()
        {
            var ib = new IdleBalancing();
            var bal = new GameBalancing();
            var m = GameModel.CreateNew(ib);
            m.Idle.Stations[0].HasWorker = true;

            double before = m.Meta.MasteryXp;
            for (int i = 0; i < 10; i++)
                GameSimulation.Tick(m, ib, bal, 5.0, 1000 + i);

            Assert.That(m.Meta.MasteryXp, Is.GreaterThan(before), "Verdienst fliesst als Meisterschafts-XP");
        }

        [Test]
        public void EvaluateStory_FiresBeatsOnMilestones_Idempotent()
        {
            var ib = new IdleBalancing();
            var m = GameModel.CreateNew(ib);
            var cat = StoryCatalog.Default();

            Assert.That(GameProgress.EvaluateStory(m, cat), Contains.Item("hans_intro"), "GameStart -> Intro");
            Assert.That(GameProgress.EvaluateStory(m, cat), Is.Empty, "ohne neuen Meilenstein -> nichts");

            m.Idle.Stations[0].HasWorker = true;
            Assert.That(GameProgress.EvaluateStory(m, cat), Contains.Item("hans_first_worker"));

            m.Meta.PrestigeCount = 1;
            Assert.That(GameProgress.EvaluateStory(m, cat), Contains.Item("hans_first_prestige"));
        }

        [Test]
        public void Tick_AccruesRenommee_OnlyInFinalCity()
        {
            var ib = new IdleBalancing();
            var bal = new GameBalancing();
            var m = GameModel.CreateNew(ib);
            m.Idle.Stations[0].HasWorker = true;

            for (int i = 0; i < 5; i++) GameSimulation.Tick(m, ib, bal, 5.0, 1000 + i);
            Assert.That(m.Meta.Renommee, Is.EqualTo(0m), "vor der Endstadt kein Renommee");

            m.Meta.PrestigeCount = 3; // Metropole (Endstadt)
            for (int i = 0; i < 5; i++) GameSimulation.Tick(m, ib, bal, 5.0, 2000 + i);
            Assert.That(m.Meta.Renommee, Is.GreaterThan(0m), "in der Endstadt akkumuliert Renommee");
        }
    }
}
