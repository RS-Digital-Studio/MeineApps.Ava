using System;
using HandwerkerImperium.Domain.Offline;
using HandwerkerImperium.Domain.State;
using HandwerkerImperium.Domain.Economy;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt OfflineProgressFormulas (aus OfflineProgressService)
    /// gegen die Original-Mathematik: gestaffelte Earnings, Boost-Stacking, Worker-Offline-Sim.
    /// </summary>
    [TestFixture]
    public class OfflineProgressFormulasTests
    {
        [Test]
        public void StaggeredEarnings_MatchOriginal()
        {
            // 1h@10/s = 3600*0.8*10 = 28800
            Assert.That(OfflineProgressFormulas.CalculateStaggeredEarnings(10m, 3600m), Is.EqualTo(28800m));
            // 8h@1/s = 7200*0.8 + 7200*0.35 + 14400*0.15 = 10440
            Assert.That(OfflineProgressFormulas.CalculateStaggeredEarnings(1m, 28800m), Is.EqualTo(10440m));
            // 10h@1/s = 10440 + 7200*0.05 = 10800
            Assert.That(OfflineProgressFormulas.CalculateStaggeredEarnings(1m, 36000m), Is.EqualTo(10800m));
        }

        [Test]
        public void OfflineDuration_And_MaxDuration()
        {
            Assert.That(OfflineProgressFormulas.GetMaxOfflineDuration(new GameState { IsPremium = false }), Is.EqualTo(TimeSpan.FromHours(4)));
            Assert.That(OfflineProgressFormulas.GetMaxOfflineDuration(new GameState { IsPremium = true }), Is.EqualTo(TimeSpan.FromHours(16)));

            var now = DateTime.UtcNow;
            Assert.That(OfflineProgressFormulas.GetOfflineDuration(now.AddHours(1), now), Is.EqualTo(TimeSpan.Zero)); // Manipulation
            Assert.That(OfflineProgressFormulas.GetOfflineDuration(now.AddHours(-2), now).TotalHours, Is.EqualTo(2.0).Within(0.01));
        }

        [Test]
        public void Boosts_ProRata_MultiplicativeStacking()
        {
            var rs = new GameState();
            rs.Prestige.PurchasedShopItems.Add("pp_rush_boost");
            Assert.That(OfflineProgressFormulas.GetPrestigeRushBonus(rs), Is.EqualTo(0.50m));

            Assert.That(OfflineProgressFormulas.ApplyBoostsProRata(new GameState(), 100m, TimeSpan.FromHours(1)), Is.EqualTo(100m));

            var spd = new GameState(); spd.SpeedBoostEndTime = spd.LastPlayedAt.AddHours(2);
            Assert.That(OfflineProgressFormulas.ApplyBoostsProRata(spd, 100m, TimeSpan.FromHours(1)), Is.EqualTo(200m).Within(0.0001m)); // 2x

            var both = new GameState();
            both.SpeedBoostEndTime = both.LastPlayedAt.AddHours(2);
            both.RushBoostEndTime = both.LastPlayedAt.AddHours(2);
            Assert.That(OfflineProgressFormulas.ApplyBoostsProRata(both, 100m, TimeSpan.FromHours(1)), Is.EqualTo(400m).Within(0.0001m)); // 2*2
        }

        [Test]
        public void WorkerSim_TrainingAndXp()
        {
            var w = Worker.CreateForTier(WorkerTier.C);
            w.ActiveTrainingType = TrainingType.Endurance; w.EnduranceBonus = 0m;
            OfflineProgressFormulas.SimulateTrainingProgress(w, 2m, 1m);
            Assert.That(w.EnduranceBonus, Is.EqualTo(0.10m)); // +0.05/h * 2h
            OfflineProgressFormulas.SimulateTrainingProgress(w, 20m, 1m);
            Assert.That(w.EnduranceBonus, Is.EqualTo(0.5m)); // Cap
            Assert.That(OfflineProgressFormulas.IsTrainingComplete(w), Is.True);

            var w3 = Worker.CreateForTier(WorkerTier.C); w3.ExperienceXp = 0; w3.ExperienceLevel = 1;
            decimal acc = 0m;
            OfflineProgressFormulas.SimulateXpGain(w3, 3.5m, ref acc);
            Assert.That(w3.ExperienceXp, Is.EqualTo(3));
            Assert.That(acc, Is.EqualTo(0.5m).Within(0.0001m));
        }
    }
}
