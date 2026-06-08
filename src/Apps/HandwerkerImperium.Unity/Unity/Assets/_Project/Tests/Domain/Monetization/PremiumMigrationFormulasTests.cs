using NUnit.Framework;
using HandwerkerImperium.Domain.Monetization;

namespace HandwerkerImperium.Domain.Tests.Monetization
{
    /// <summary>Verifiziert die Avalonia-Premium-Migration: Pass-Gewährung + einmaliger (idempotenter) Gem-Bonus.</summary>
    [TestFixture]
    public class PremiumMigrationFormulasTests
    {
        [Test]
        public void GrantsPass_OnlyForAvaloniaPremium()
        {
            Assert.That(PremiumMigrationFormulas.GrantsPass(true), Is.True);
            Assert.That(PremiumMigrationFormulas.GrantsPass(false), Is.False);
        }

        [Test]
        public void MigrationGemBonus_OnceOnly()
        {
            Assert.That(PremiumMigrationFormulas.MigrationGemBonus(true, false), Is.EqualTo(100));
            Assert.That(PremiumMigrationFormulas.MigrationGemBonus(true, true), Is.EqualTo(0), "schon migriert");
            Assert.That(PremiumMigrationFormulas.MigrationGemBonus(false, false), Is.EqualTo(0), "kein Premium");
        }
    }
}
