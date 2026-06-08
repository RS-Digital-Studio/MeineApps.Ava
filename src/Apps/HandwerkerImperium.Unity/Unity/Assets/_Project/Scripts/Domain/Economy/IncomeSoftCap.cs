#nullable enable
using System;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Log2-Soft-Cap auf den aggregierten Einkommens-Multiplikator (GDD §7, PROGRESSION_BALANCING §7).
    /// Schlanke Neufassung der validierten Original-Mathematik (aus IncomeCalculatorService geborgen,
    /// git-Tag <c>hwi-unity-domain-port-pre-cleanslate</c>): oberhalb einer tier-/stadt-abhängigen Schwelle
    /// wird der Überschuss logarithmisch gedämpft (<c>T + log2(1 + (M − T))</c>), damit die Zahlen über
    /// Prestige/Endgame lesbar bleiben und nicht eskalieren. Entkoppelt + Unity-frei; die Schwelle kommt
    /// aus dem BalancingConfig (steigt je Stadt). <b>Dämpfung, nie Verstärkung.</b>
    /// </summary>
    public static class IncomeSoftCap
    {
        /// <summary>
        /// Gedämpfter Multiplikator: ≤ Schwelle bleibt linear, darüber <c>T + log2(1 + (M − T))</c>,
        /// geklemmt so, dass das Ergebnis nie über dem Eingangs-Multiplikator liegt (reine Dämpfung).
        /// </summary>
        public static double SoftCapMultiplier(double multiplier, double threshold)
        {
            if (multiplier <= threshold) return multiplier;
            double excess = multiplier - threshold;
            double softened = threshold + Math.Log(1.0 + excess, 2.0);
            return softened < multiplier ? softened : multiplier;
        }

        /// <summary>
        /// Wendet den Soft-Cap auf ein Basis-Einkommen × Multiplikator an (decimal-Geld-Pfad).
        /// </summary>
        public static decimal ApplySoftCap(decimal baseIncome, decimal multiplier, decimal threshold)
        {
            if (baseIncome <= 0m || multiplier <= 0m) return 0m;
            if (multiplier <= threshold) return baseIncome * multiplier;
            decimal excess = multiplier - threshold;
            decimal softened = threshold + (decimal)Math.Log(1.0 + (double)excess, 2.0);
            if (softened > multiplier) softened = multiplier; // nie verstärken
            return baseIncome * softened;
        }
    }
}
