using System;
using HandwerkerImperium.Domain.Economy;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt WorkerTickFormulas (aus WorkerService) gegen die
    /// Original-Per-Tick-Mathematik: Ruhe-Erholung, Ausdauer-/Stimmungs-/Effizienz-Training (Caps,
    /// XP, Level-Up) und Arbeiten (Mood-Decay + Fatigue). Erwartungen aus den abgeleiteten
    /// Worker-Properties berechnet (robust gegen Personality-Zufall in CreateForTier).
    /// </summary>
    [TestFixture]
    public class WorkerTickFormulasTests
    {
        [Test]
        public void EnduranceAndMoraleTraining_GainAndCap()
        {
            var we = Worker.CreateForTier(WorkerTier.C);
            we.EnduranceBonus = 0m; we.IsTraining = true; we.ActiveTrainingType = TrainingType.Endurance;
            WorkerTickFormulas.ApplyEnduranceTraining(we, 2m, 1m);
            Assert.That(we.EnduranceBonus, Is.EqualTo(0.10m));
            WorkerTickFormulas.ApplyEnduranceTraining(we, 20m, 1m);
            Assert.That(we.EnduranceBonus, Is.EqualTo(0.5m));
            Assert.That(we.IsTraining, Is.False);

            var wm = Worker.CreateForTier(WorkerTier.C);
            wm.MoraleBonus = 0m; wm.IsTraining = true; wm.ActiveTrainingType = TrainingType.Morale;
            WorkerTickFormulas.ApplyMoraleTraining(wm, 3m, 1m);
            Assert.That(wm.MoraleBonus, Is.EqualTo(0.15m));
            WorkerTickFormulas.ApplyMoraleTraining(wm, 20m, 1m);
            Assert.That(wm.MoraleBonus, Is.EqualTo(0.5m));
            Assert.That(wm.IsTraining, Is.False);
        }

        [Test]
        public void Resting_FatigueRecovery()
        {
            var wr = Worker.CreateForTier(WorkerTier.C);
            wr.Fatigue = 50m; wr.EquippedItem = null; wr.IsResting = true;
            WorkerTickFormulas.ApplyResting(wr, 1m, null); // (100/4)*1 = 25
            Assert.That(wr.Fatigue, Is.EqualTo(25m));
        }

        [Test]
        public void Working_MoodDecayAndFatigue()
        {
            var ww = Worker.CreateForTier(WorkerTier.C);
            ww.Mood = 80m; ww.Fatigue = 0m; ww.EquippedItem = null; ww.MoraleBonus = 0m; ww.EnduranceBonus = 0m;
            decimal md = ww.MoodDecayPerHour;
            decimal fp = ww.FatiguePerHour;
            WorkerTickFormulas.ApplyWorking(ww, 1m, 0m, 0m, null, 0m);
            Assert.That(ww.Mood, Is.EqualTo(80m - md));
            Assert.That(ww.Fatigue, Is.EqualTo(fp));
        }

        [Test]
        public void EfficiencyTraining_XpAndLevelUp()
        {
            var wx = Worker.CreateForTier(WorkerTier.C);
            wx.ExperienceXp = 0; wx.ExperienceLevel = 1; wx.TrainingXpAccumulator = 0m;
            decimal xpMult = wx.Personality.GetXpMultiplier();
            WorkerTickFormulas.ApplyEfficiencyTraining(wx, 1m, 1m);
            Assert.That(wx.ExperienceXp, Is.EqualTo((int)(50m * xpMult)));

            var wl = Worker.CreateForTier(WorkerTier.C);
            wl.ExperienceXp = wl.XpForNextLevel - 1; wl.ExperienceLevel = 1; wl.TrainingXpAccumulator = 0m;
            decimal effBefore = wl.Efficiency;
            WorkerTickFormulas.ApplyEfficiencyTraining(wl, 1m, 1m);
            Assert.That(wl.ExperienceLevel, Is.EqualTo(2));
            Assert.That(wl.Efficiency, Is.GreaterThanOrEqualTo(effBefore));
        }
    }
}
