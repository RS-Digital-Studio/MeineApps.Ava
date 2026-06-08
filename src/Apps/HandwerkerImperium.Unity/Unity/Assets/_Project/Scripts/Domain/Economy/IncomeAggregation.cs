#nullable enable

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Die zentrale Einkommens-Integration (GDD §7, PROGRESSION §7): bündelt alle permanenten Multiplikatoren
    /// (Prestige-Stadt-Multiplikator × Meisterschaft × Imperium-Marken-Perkboard × Master-Tools × Premium ×
    /// Meistergrad) zu EINEM Aggregat und legt darauf den Log2-Soft-Cap, damit die Zahlen über Monate lesbar
    /// bleiben. Bindeglied zwischen dem P0-Idle-Loop (liefert Basis-Einkommen/s) und der Meta-Progression.
    /// Reine, Unity-freie Mathematik.
    /// </summary>
    public static class IncomeAggregation
    {
        /// <summary>
        /// Aggregierter permanenter Multiplikator: Prestige-Multiplikator × Π(1 + Bonus) über die additiven
        /// Bonus-Quellen. Negative Boni werden als 0 behandelt, ein ≤0-Prestige-Multiplikator als 1.
        /// </summary>
        public static decimal AggregateMultiplier(
            decimal prestigeMultiplier, decimal masteryBonus, decimal perkboardBonus,
            decimal masterToolBonus, decimal premiumBonus, decimal meistergradBonus)
        {
            decimal m = prestigeMultiplier <= 0m ? 1m : prestigeMultiplier;
            m *= 1m + Nz(masteryBonus);
            m *= 1m + Nz(perkboardBonus);
            m *= 1m + Nz(masterToolBonus);
            m *= 1m + Nz(premiumBonus);
            m *= 1m + Nz(meistergradBonus);
            return m;
        }

        /// <summary>
        /// Effektives Einkommen/s: Basis-Idle-Einkommen × Aggregat-Multiplikator, gedämpft durch den
        /// Log2-Soft-Cap (Schwelle steigt je Stadt; aus dem BalancingConfig).
        /// </summary>
        public static decimal EffectiveIncomePerSecond(decimal baseIdleIncomePerSecond, decimal aggregateMultiplier, decimal softCapThreshold) =>
            IncomeSoftCap.ApplySoftCap(baseIdleIncomePerSecond, aggregateMultiplier, softCapThreshold);

        private static decimal Nz(decimal x) => x < 0m ? 0m : x;
    }
}
