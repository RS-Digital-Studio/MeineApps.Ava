using System;
using System.Collections.Generic;
using System.Linq;

namespace HandwerkerImperium.Domain.Guild
{
    /// <summary>
    /// Service-Formel-Extrakt aus <c>GuildBossService</c> (Avalonia-Original): die reinen,
    /// balancing-relevanten Boss-Formeln (Schadens-Multiplikator nach Quelle, HP-Skalierung nach
    /// Mitgliederzahl, Wochen-Rotation, Belohnung nach Rang, Rang-Berechnung, Rest-HP). 1:1 zur Vorlage.
    ///
    /// Bewusst NICHT extrahiert (Firebase-/Netzwerk-/Lock-gekoppelt, bleiben im Game-Service):
    /// GetActiveBoss/DealDamage/CheckBossStatus/SpawnBossIfNeeded/GetLeaderboard + Damage-Aggregation
    /// (Firebase-Reads/Writes, Race-Condition-Handling, Duplikat-Schutz).
    /// </summary>
    public static class GuildBossFormulas
    {
        /// <summary>MVP (Rang 1) Goldschrauben-Belohnung.</summary>
        public const int MvpRewardGs = 30;
        /// <summary>Top-3 Goldschrauben-Belohnung.</summary>
        public const int Top3RewardGs = 20;
        /// <summary>Teilnahme-Goldschrauben-Belohnung.</summary>
        public const int ParticipantRewardGs = 10;

        /// <summary>
        /// Effektiver Schaden nach Anwendung des quellenabhaengigen Boss-Multiplikators
        /// (z.B. Eisentitan: Crafting 2x). Unbekannte Quelle = 1x. <c>(long)(damage * multiplier)</c>.
        /// </summary>
        public static long CalculateEffectiveDamage(long damage, GuildBossDefinition definition, string source)
        {
            if (definition == null) return damage;
            decimal multiplier = (source ?? string.Empty).ToLowerInvariant() switch
            {
                "crafting" => definition.CraftingDamageMultiplier,
                "order" or "orders" => definition.OrderDamageMultiplier,
                "minigame" or "minigames" => definition.MiniGameDamageMultiplier,
                "donation" or "donations" => definition.MoneyDonationDamageMultiplier,
                _ => 1m
            };
            return (long)(damage * multiplier);
        }

        /// <summary>Boss-Typ-Rotation: Index in <see cref="GuildBossDefinition.GetAll"/> = weekNumber % bossCount.</summary>
        public static int GetBossIndexForWeek(int weekNumber, int bossCount) => weekNumber % bossCount;

        /// <summary>
        /// Boss-HP skaliert mit der Gildengroesse (Solo-Schutz): stufenlos
        /// <c>baseBossHp × max(0.5, memberCount / 5.0)</c> (1 Mitglied=0.5x, 5=1.0x, 10=1.5x, 20=2.5x).
        /// </summary>
        public static long CalculateScaledBossHp(long baseBossHp, int memberCount)
        {
            int mc = Math.Max(1, memberCount);
            return (long)(baseBossHp * Math.Max(0.5, mc / 5.0));
        }

        /// <summary>Goldschrauben-Belohnung nach Rang: 1=MVP 30, 2-3=20, sonst Teilnahme 10.</summary>
        public static int CalculateBossReward(int rank) => rank switch
        {
            1 => MvpRewardGs,
            <= 3 => Top3RewardGs,
            _ => ParticipantRewardGs
        };

        /// <summary>
        /// Rang eines Spielers = Anzahl Eintraege mit STRIKT mehr Schaden + 1 (1-basiert).
        /// <paramref name="allDamages"/> darf den eigenen Wert enthalten.
        /// </summary>
        public static int CalculateRank(long ownDamage, IEnumerable<long> allDamages) =>
            allDamages.Count(d => d > ownDamage) + 1;

        /// <summary>Rest-HP des Bosses = max(0, bossHp - totalDamage).</summary>
        public static long CalculateCurrentHp(long bossHp, long totalDamage) => Math.Max(0, bossHp - totalDamage);

        /// <summary>Boss besiegt, sobald Gesamtschaden die HP erreicht.</summary>
        public static bool IsDefeated(long bossHp, long totalDamage) => totalDamage >= bossHp;
    }
}
