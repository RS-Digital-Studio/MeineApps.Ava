using NUnit.Framework;
using HandwerkerImperium.Domain.Runtime;

namespace HandwerkerImperium.Domain.Tests.Runtime
{
    /// <summary>
    /// Integrations-Beweis der Meta-Progression: Meisterschafts-XP-Gewinn (permanent), Prestige als Akt-Finale
    /// (Reset akt-intern, Persist permanent, kumulativer Multiplikator, PP/Marken), 3-Prestige-Limit und
    /// Endgame-Meistergrad-Kauf — alle Formel-Sätze zusammen über EINEM Zustand.
    /// </summary>
    [TestFixture]
    public class MetaProgressionTests
    {
        private static readonly decimal[] Stages = { 3m, 4m, 5m };

        [Test]
        public void GainMasteryXp_RaisesLevel_Permanently()
        {
            var s = new MetaState();
            Assert.That(MetaProgression.GainMasteryXp(s, 215.0, 100.0, 1.15), Is.EqualTo(2)); // TotalXp(2)=215
            Assert.That(s.MasteryLevel, Is.EqualTo(2));
            Assert.That(MetaProgression.GainMasteryXp(s, 0.0, 100.0, 1.15), Is.EqualTo(0));
        }

        [Test]
        public void Prestige_RequiresFiveStars_ResetsActInternal_PersistsPermanent()
        {
            var s = new MetaState();
            MetaProgression.GainMasteryXp(s, 215.0, 100.0, 1.15); // Level 2 (permanent)
            s.CurrentStar = 4;
            s.OrdersServed = 50;
            s.RestorationPhases = 10;

            Assert.That(MetaProgression.TryPrestige(s, 400_000m, Stages, 5, 3), Is.False, "unter 5 Sternen");

            s.CurrentStar = 5;
            Assert.That(MetaProgression.TryPrestige(s, 400_000m, Stages, 5, 3), Is.True);

            // Permanent persistiert:
            Assert.That(s.PrestigeCount, Is.EqualTo(1));
            Assert.That(s.CityIndex, Is.EqualTo(1));
            Assert.That(s.PrestigeMultiplier, Is.EqualTo(3m));
            Assert.That(s.PrestigeCurrency, Is.EqualTo(2), "PP = floor(sqrt(400k/100k)) = 2");
            Assert.That(s.AvailableMarks, Is.EqualTo(5));
            Assert.That(s.MasteryLevel, Is.EqualTo(2), "Meisterschaft ueberlebt Prestige");
            // Akt-intern zurueckgesetzt:
            Assert.That(s.CurrentStar, Is.EqualTo(1));
            Assert.That(s.OrdersServed, Is.EqualTo(0));
            Assert.That(s.RestorationPhases, Is.EqualTo(0));
        }

        [Test]
        public void Prestige_CumulativeMultiplier_AndThreePrestigeLimit()
        {
            var s = new MetaState();
            s.CurrentStar = 5; MetaProgression.TryPrestige(s, 1_000_000m, Stages, 5, 3); // P1 -> x3
            s.CurrentStar = 5; MetaProgression.TryPrestige(s, 1_000_000m, Stages, 5, 3); // P2 -> x12
            s.CurrentStar = 5; MetaProgression.TryPrestige(s, 1_000_000m, Stages, 5, 3); // P3 -> x60
            Assert.That(s.PrestigeCount, Is.EqualTo(3));
            Assert.That(s.PrestigeMultiplier, Is.EqualTo(60m));
            Assert.That(s.CityIndex, Is.EqualTo(3), "Metropole");

            s.CurrentStar = 5;
            Assert.That(MetaProgression.TryPrestige(s, 1_000_000m, Stages, 5, 3), Is.False, "max 3 Prestige");
        }

        [Test]
        public void Meistergrad_Purchase_ConsumesRenommee()
        {
            var s = new MetaState { Renommee = 150m };
            Assert.That(MetaProgression.TryBuyMeistergrad(s, 100m, 1.5), Is.True); // Kosten 100
            Assert.That(s.MeistergradGrade, Is.EqualTo(1));
            Assert.That(s.Renommee, Is.EqualTo(50m));
            Assert.That(MetaProgression.TryBuyMeistergrad(s, 100m, 1.5), Is.False, "Kosten 150 > 50 Renommee");
        }
    }
}
