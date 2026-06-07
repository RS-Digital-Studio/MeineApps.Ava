using System;
using HandwerkerImperium.Domain;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Long-Term-Engagement post-Lv1000 (Eternal Mastery): permanenter Einkommens-Bonus,
    /// der mit jedem abgeschlossenen Prestige wächst. Kein Reset bei Ascension.
    ///
    /// Reine Formel-Extraktion aus dem Avalonia-Original (Services/EternalMasteryService.cs).
    /// Die State-/DI-Anbindung (CompletedPrestiges-Lookup, IsActive, DisplayText) lebt in der
    /// Unity-Game-/Präsentationsschicht; die deterministische Berechnung gehört in die Domain.
    /// Konstanten kommen aus <see cref="GameBalanceConstants"/>.
    /// </summary>
    public static class EternalMasteryFormulas
    {
        /// <summary>
        /// Berechnet den akkumulierten Einkommens-Bonus (z.B. 0.25 = +25%) für eine Anzahl
        /// abgeschlossener Prestiges. Ab <see cref="GameBalanceConstants.EternalMasterySoftCapThreshold"/>
        /// greift ein logarithmischer Soft-Cap auf den Überschuss.
        /// </summary>
        public static decimal CalculateBonus(int completedPrestiges)
        {
            if (completedPrestiges <= 0) return 0m;

            // Soft-Cap ab Schwelle: Bis dahin voller Bonus, der Überschuss wird logarithmisch gedämpft.
            int effectivePrestiges = completedPrestiges;
            if (completedPrestiges > GameBalanceConstants.EternalMasterySoftCapThreshold)
            {
                int excess = completedPrestiges - GameBalanceConstants.EternalMasterySoftCapThreshold;
                effectivePrestiges = GameBalanceConstants.EternalMasterySoftCapThreshold
                    + (int)(Math.Log10(excess + 1) * 10);
            }

            // Linear: 0.5% pro Prestige
            decimal linear = effectivePrestiges * GameBalanceConstants.EternalMasteryBonusPerPrestige;

            // 5er-Stufen-Bonus: alle 5 Prestiges +2.5% zusätzlich
            int tiers5 = effectivePrestiges / 5;
            decimal tier5Bonus = tiers5 * GameBalanceConstants.EternalMasteryBonusPer5Prestiges;

            // 10er-Mega-Stufen-Bonus: alle 10 Prestiges +5% zusätzlich
            int tiers10 = effectivePrestiges / 10;
            decimal tier10Bonus = tiers10 * GameBalanceConstants.EternalMasteryBonusPer10Prestiges;

            return linear + tier5Bonus + tier10Bonus;
        }

        /// <summary>Anzahl Prestiges bis zur nächsten 5er-Stufe.</summary>
        public static int PrestigesUntilNextTier(int completedPrestiges)
        {
            if (completedPrestiges == 0) return 5;
            int next5 = ((completedPrestiges / 5) + 1) * 5;
            return next5 - completedPrestiges;
        }

        /// <summary>Anzahl Prestiges bis zur nächsten 10er-Mega-Stufe.</summary>
        public static int PrestigesUntilNextMegaTier(int completedPrestiges)
        {
            if (completedPrestiges == 0) return 10;
            int next10 = ((completedPrestiges / 10) + 1) * 10;
            return next10 - completedPrestiges;
        }
    }
}
