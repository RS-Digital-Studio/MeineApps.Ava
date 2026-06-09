using System;
using NUnit.Framework;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Achievements;
using HandwerkerImperium.Domain.Story;
using HandwerkerImperium.Domain.LiveOps;
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

        [Test]
        public void EvaluateDailyTasks_RollsThree_ClaimsCompleted_GrantsGems_Idempotent()
        {
            var m = GameModel.CreateNew(new IdleBalancing());
            var pool = DailyTaskCatalog.Pool();
            long now = 1_000_000_000_000L;

            // erste Auswertung zieht 3 Aufgaben (deterministisch je Tag)
            GameProgress.EvaluateDailyTasks(m, pool, now);
            Assert.That(m.DailyTasks.Count, Is.EqualTo(3), "3 Tagesaufgaben gezogen");

            // bekannte Aufgabe einsetzen + erfuellen
            m.DailyTasks.Clear();
            m.DailyTasks.Add(new DailyTaskRuntime { Id = "dt_serve_10", Metric = DailyTaskMetric.ServeCustomers, Target = 10, GemReward = 15, Baseline = 0 });

            Assert.That(GameProgress.EvaluateDailyTasks(m, pool, now), Is.Empty, "noch nicht erfuellt");
            Assert.That(m.Gems, Is.EqualTo(0m));

            m.Orders.TotalServed = 10;
            Assert.That(GameProgress.EvaluateDailyTasks(m, pool, now), Contains.Item("dt_serve_10"));
            Assert.That(m.Gems, Is.EqualTo(15m), "Gem-Belohnung gutgeschrieben");

            Assert.That(GameProgress.EvaluateDailyTasks(m, pool, now), Is.Empty, "schon abgeholt -> nichts");
            Assert.That(m.Gems, Is.EqualTo(15m), "kein doppeltes Gutschreiben");
        }

        [Test]
        public void EvaluateDailyTasks_ResetsOnNewUtcDay()
        {
            var m = GameModel.CreateNew(new IdleBalancing());
            var pool = DailyTaskCatalog.Pool();

            long day1 = new DateTime(2026, 6, 9, 23, 0, 0, DateTimeKind.Utc).Ticks;
            GameProgress.EvaluateDailyTasks(m, pool, day1);
            Assert.That(m.DailyTasks.Count, Is.EqualTo(3));
            long roll1 = m.DailyTaskRollDayUtc;

            long day2 = new DateTime(2026, 6, 10, 1, 0, 0, DateTimeKind.Utc).Ticks;
            GameProgress.EvaluateDailyTasks(m, pool, day2);
            Assert.That(m.DailyTaskRollDayUtc, Is.EqualTo(day2), "neuer UTC-Tag -> Re-Roll");
            Assert.That(m.DailyTaskRollDayUtc, Is.Not.EqualTo(roll1));
        }
    }
}
