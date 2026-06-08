#nullable enable
using System;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Meisterschafts-Track (PROGRESSION_BALANCING §4/§7) — das <b>permanente, nie zurückgesetzte</b> Langzeit-Rückgrat.
    /// Kontoweite XP aus aller Aktivität; Kurve <c>XP(N) = base × 1.15^N</c> (~100+ Level über Monate), je Level ein
    /// kleiner permanenter globaler Income-Bonus (per Income-Soft-Cap gedämpft). Reine, Unity-freie Mathematik.
    /// </summary>
    public static class MasteryFormulas
    {
        /// <summary>Standard-Wachstumsfaktor je Level (PROGRESSION §7).</summary>
        public const double DefaultGrowth = 1.15;

        /// <summary>XP-Kosten des Schritts von <paramref name="level"/> auf level+1: <c>base × growth^level</c>.</summary>
        public static double XpForLevelStep(int level, double baseXp, double growth)
        {
            if (level < 0) level = 0;
            return baseXp * Math.Pow(growth, level);
        }

        /// <summary>Kumulierte Gesamt-XP, um <paramref name="level"/> zu erreichen (geometrische Summe).</summary>
        public static double TotalXpForLevel(int level, double baseXp, double growth)
        {
            if (level <= 0) return 0.0;
            if (Math.Abs(growth - 1.0) < 1e-12) return baseXp * level;
            return baseXp * (Math.Pow(growth, level) - 1.0) / (growth - 1.0);
        }

        /// <summary>Erreichtes Level für eine Gesamt-XP-Menge (Inverse der geometrischen Summe).</summary>
        public static int LevelForTotalXp(double totalXp, double baseXp, double growth)
        {
            if (totalXp <= 0.0 || baseXp <= 0.0) return 0;
            if (growth <= 1.0) return (int)Math.Floor(totalXp / baseXp);
            double inside = 1.0 + totalXp * (growth - 1.0) / baseXp;
            double level = Math.Log(inside, growth);
            return (int)Math.Floor(level + 1e-9);
        }

        /// <summary>
        /// Roher permanenter globaler Income-Bonus für ein Meisterschafts-Level (<c>level × bonusPerLevel</c>).
        /// Der Aggregat-Bonus wird vom Einkommens-Pfad durch den Log2-Soft-Cap gedämpft (nie wertlos, nie explosiv).
        /// </summary>
        public static decimal GlobalIncomeBonus(int level, decimal bonusPerLevel) =>
            level <= 0 ? 0m : level * bonusPerLevel;
    }
}
