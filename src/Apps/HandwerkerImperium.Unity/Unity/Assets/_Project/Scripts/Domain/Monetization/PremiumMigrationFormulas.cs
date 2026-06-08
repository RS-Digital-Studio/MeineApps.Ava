#nullable enable

namespace HandwerkerImperium.Domain.Monetization
{
    /// <summary>
    /// Migration bestehender Avalonia-Käufer (P3 §5, GDD §9.2): wer im produktiven HandwerkerImperium
    /// <c>IsPremium</c> war, bekommt den „Imperium-Pass" auch hier + einen einmaligen Migrations-Gem-Bonus.
    /// Reine, Unity-freie Logik (idempotent über ein „bereits migriert"-Flag).
    /// </summary>
    public static class PremiumMigrationFormulas
    {
        /// <summary>Einmaliger Migrations-Gem-Bonus (P3 §5: 100 GS).</summary>
        public const int MigrationBonusGems = 100;

        /// <summary>True, wenn der Spieler den Imperium-Pass aus der Avalonia-Premium-Migration erhält.</summary>
        public static bool GrantsPass(bool avaloniaWasPremium) => avaloniaWasPremium;

        /// <summary>
        /// Einmaliger Gem-Bonus: nur wenn Avalonia-Premium UND noch nicht migriert. Danach 0 (idempotent).
        /// </summary>
        public static int MigrationGemBonus(bool avaloniaWasPremium, bool alreadyMigrated) =>
            (avaloniaWasPremium && !alreadyMigrated) ? MigrationBonusGems : 0;
    }
}
