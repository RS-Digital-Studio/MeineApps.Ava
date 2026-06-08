#nullable enable
using System;
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Prestige als zeremonielles Akt-Finale (P1 §4 / GDD §7 / PROGRESSION_BALANCING §2/§7): <b>max. 3</b>
    /// Prestige-Übergänge (4 Städte). Bei 5★ → Umzug in die nächste Stadt mit permanentem, festem
    /// Multiplikator (kumulativ ~×3 / ×12 / ×60) + Prestige-Währung <c>PP = floor(sqrt(Money / 100k))</c>
    /// für das Imperium-Marken-Perkboard. Reine, Unity-freie Mathematik; Reset-/Persist-Logik liegt im Game-Layer.
    /// </summary>
    public static class PrestigeFormulas
    {
        /// <summary>Geld-Schwelle, ab der 1 Prestige-Punkt anfällt (PROGRESSION-Vorgabe: 100k).</summary>
        public const decimal MoneyPerPrestigePointBase = 100_000m;

        /// <summary>Default-Obergrenze der Prestige-Übergänge (GDD §16: 4 Städte = 3 Prestige).</summary>
        public const int MaxPrestige = 3;

        /// <summary>
        /// Prestige-Währung aus dem aktuellen Run: <c>floor(sqrt(Money / 100_000))</c>.
        /// Unter 100k Geld fällt 0 an.
        /// </summary>
        public static int PrestigePoints(decimal currentRunMoney)
        {
            if (currentRunMoney < MoneyPerPrestigePointBase) return 0;
            double ratio = (double)(currentRunMoney / MoneyPerPrestigePointBase);
            return (int)Math.Floor(Math.Sqrt(ratio));
        }

        /// <summary>
        /// Kumulativer permanenter Einkommens-Multiplikator nach <paramref name="prestigeCount"/> Prestiges:
        /// Produkt der je-Stufe-Multiplikatoren (z. B. [3, 4, 5] → ×3 / ×12 / ×60). 0 Prestiges → ×1.
        /// </summary>
        public static decimal CityMultiplier(int prestigeCount, IReadOnlyList<decimal>? perStageMultipliers)
        {
            if (prestigeCount <= 0 || perStageMultipliers == null || perStageMultipliers.Count == 0) return 1m;
            int n = prestigeCount < perStageMultipliers.Count ? prestigeCount : perStageMultipliers.Count;
            decimal product = 1m;
            for (int i = 0; i < n; i++)
                product *= perStageMultipliers[i];
            return product;
        }

        /// <summary>True, wenn ein Prestige erlaubt ist: 5★ erreicht und das Prestige-Limit nicht ausgeschöpft.</summary>
        public static bool CanPrestige(int currentStar, int prestigeCount, int maxPrestige) =>
            currentStar >= 5 && prestigeCount < maxPrestige;

        /// <summary>Stadt-Index nach dem nächsten Prestige (0=Hansstadt … 3=Metropole), auf Max geklemmt.</summary>
        public static int NextCityIndex(int prestigeCount, int maxPrestige)
        {
            int next = prestigeCount + 1;
            return next < maxPrestige ? next : maxPrestige;
        }

        /// <summary>True, wenn die Endstadt (Metropole) erreicht ist — danach kein Prestige mehr (Endgame §5).</summary>
        public static bool IsFinalCity(int prestigeCount, int maxPrestige) => prestigeCount >= maxPrestige;
    }
}
