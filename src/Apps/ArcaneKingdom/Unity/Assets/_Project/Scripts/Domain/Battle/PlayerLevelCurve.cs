#nullable enable
using System;

namespace ArcaneKingdom.Domain.Battle
{
    /// <summary>
    /// Spieler-EXP-Kurve (DESIGN.md Kapitel 4.2).
    /// Vorlaeufige Formel: <c>EXP(n) = round(1000 * 1.08^n + 50 * n^2)</c>.
    /// </summary>
    public static class PlayerLevelCurve
    {
        public const int SoftCap = 150;

        /// <summary>
        /// EXP, die zum Wechsel von Level n auf n+1 benoetigt werden.
        /// </summary>
        public static long ExpRequiredFromLevel(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), "Level muss >= 1 sein.");
            if (level >= SoftCap) return long.MaxValue;
            var value = 1000d * Math.Pow(1.08d, level) + 50d * level * level;
            return (long)Math.Round(value);
        }

        /// <summary>
        /// Kumulierte EXP, die noetig sind um Level <paramref name="targetLevel"/> zu erreichen
        /// (ausgehend von Level 1, 0 EXP). Wird fuer Profil-Anzeige verwendet.
        /// </summary>
        public static long ExpCumulativeForLevel(int targetLevel)
        {
            if (targetLevel <= 1) return 0;
            var sum = 0L;
            for (var lv = 1; lv < targetLevel && lv < SoftCap; lv++)
                sum += ExpRequiredFromLevel(lv);
            return sum;
        }

        /// <summary>
        /// Berechnet, auf welchem Level der Spieler bei gegebener kumulierter EXP ist.
        /// </summary>
        public static int LevelForExp(long totalExp)
        {
            if (totalExp < 0) throw new ArgumentOutOfRangeException(nameof(totalExp));
            var level = 1;
            var remaining = totalExp;
            while (level < SoftCap)
            {
                var needed = ExpRequiredFromLevel(level);
                if (remaining < needed) return level;
                remaining -= needed;
                level++;
            }
            return SoftCap;
        }
    }
}
