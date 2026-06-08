using NUnit.Framework;
using HandwerkerImperium.Domain.Config;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Monetization;

namespace HandwerkerImperium.Domain.Tests.Config
{
    /// <summary>
    /// Pinnt die spec-gegründeten Default-Werte der zentralen Balancing-Konfiguration und prüft die
    /// Konsistenz zu den Formel-Konstanten (verhindert stilles Auseinanderdriften von Config und Formeln).
    /// </summary>
    [TestFixture]
    public class GameBalancingTests
    {
        [Test]
        public void Defaults_MatchSpecValues()
        {
            var b = new GameBalancing();
            Assert.That(b.Prestige.StageMultipliers, Is.EqualTo(new[] { 3m, 4m, 5m }).AsCollection);
            Assert.That(b.Prestige.MaxPrestige, Is.EqualTo(3));
            Assert.That(b.Mastery.Growth, Is.EqualTo(1.15));
            Assert.That(b.Meistergrad.Growth, Is.EqualTo(1.5));
            Assert.That(b.Daily.LadderLength, Is.EqualTo(30));
            Assert.That(b.Referral.TierRewards, Is.EqualTo(new[] { 50, 200, 500 }).AsCollection);
            Assert.That(b.Referral.TierThresholds, Is.EqualTo(new[] { 1, 5, 10 }).AsCollection);
            Assert.That(b.Offline.BaseCapHours + b.Offline.PremiumExtraHours, Is.EqualTo(16), "Premium-Offline-Cap 16 h");
        }

        [Test]
        public void StarThresholds_AreAscending_WithBuffer()
        {
            var s = new GameBalancing().Star;
            Assert.That(s.Thresholds.Count, Is.EqualTo(4), "2★..5★");
            for (int i = 1; i < s.Thresholds.Count; i++)
                Assert.That(s.Thresholds[i], Is.GreaterThan(s.Thresholds[i - 1]), "aufsteigend");
            Assert.That(s.HysteresisBuffer, Is.GreaterThan(0));
        }

        [Test]
        public void Config_IsConsistentWithFormulaConstants()
        {
            var b = new GameBalancing();
            Assert.That(b.Prestige.MaxPrestige, Is.EqualTo(PrestigeFormulas.MaxPrestige), "MaxPrestige == Formel-Konstante");
            Assert.That(b.Monetization.MigrationBonusGems, Is.EqualTo(PremiumMigrationFormulas.MigrationBonusGems));
            Assert.That(b.Meistergrad.Growth, Is.EqualTo(MeistergradFormulas.DefaultGrowth));
            Assert.That(b.Mastery.Growth, Is.EqualTo(MasteryFormulas.DefaultGrowth));
        }

        [Test]
        public void AllSubConfigs_AreInstantiated()
        {
            var b = new GameBalancing();
            Assert.That(b.Star, Is.Not.Null);
            Assert.That(b.Prestige, Is.Not.Null);
            Assert.That(b.Mastery, Is.Not.Null);
            Assert.That(b.Meistergrad, Is.Not.Null);
            Assert.That(b.Perkboard, Is.Not.Null);
            Assert.That(b.SoftCap, Is.Not.Null);
            Assert.That(b.WorldTier, Is.Not.Null);
            Assert.That(b.Orders, Is.Not.Null);
            Assert.That(b.Rush, Is.Not.Null);
            Assert.That(b.Restoration, Is.Not.Null);
            Assert.That(b.Daily, Is.Not.Null);
            Assert.That(b.Offline, Is.Not.Null);
            Assert.That(b.Monetization, Is.Not.Null);
            Assert.That(b.Referral, Is.Not.Null);
        }
    }
}
