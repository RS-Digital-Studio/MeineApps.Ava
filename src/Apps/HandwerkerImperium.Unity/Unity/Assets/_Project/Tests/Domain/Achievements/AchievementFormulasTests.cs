using System.Collections.Generic;
using NUnit.Framework;
using HandwerkerImperium.Domain.Achievements;

namespace HandwerkerImperium.Domain.Tests.Achievements
{
    /// <summary>
    /// Verifiziert die Achievement-Logik: Abschluss/Fortschritt, das Erkennen neu abgeschlossener
    /// Achievements (Metrik-gefiltert, Claimed-gefiltert) und die Gem-Belohnungssumme.
    /// </summary>
    [TestFixture]
    public class AchievementFormulasTests
    {
        private static List<AchievementDefinition> Catalog() => new List<AchievementDefinition>
        {
            new AchievementDefinition("orders_10", AchievementMetric.OrdersServed, 10, 5),
            new AchievementDefinition("orders_100", AchievementMetric.OrdersServed, 100, 20),
            new AchievementDefinition("prestige_1", AchievementMetric.PrestigeCount, 1, 50),
        };

        [Test]
        public void IsComplete_And_Progress()
        {
            Assert.That(AchievementFormulas.IsComplete(10, 10), Is.True);
            Assert.That(AchievementFormulas.IsComplete(9, 10), Is.False);
            Assert.That(AchievementFormulas.Progress01(5, 10), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(AchievementFormulas.Progress01(20, 10), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void NewlyCompleted_FiltersByMetric_AndClaimed()
        {
            var cat = Catalog();
            var claimed = new List<string> { "orders_10" };
            var newly = AchievementFormulas.NewlyCompleted(cat, AchievementMetric.OrdersServed, 150, claimed);
            Assert.That(newly, Has.Count.EqualTo(1));
            Assert.That(newly[0], Is.EqualTo("orders_100"));

            // andere Metrik bleibt unberuehrt
            var none = AchievementFormulas.NewlyCompleted(cat, AchievementMetric.OrdersServed, 5, claimed);
            Assert.That(none, Is.Empty);
        }

        [Test]
        public void TotalGemReward_SumsOverIds()
        {
            var cat = Catalog();
            var ids = new List<string> { "orders_100", "prestige_1" }; // 20 + 50
            Assert.That(AchievementFormulas.TotalGemReward(cat, ids), Is.EqualTo(70));
        }
    }
}
