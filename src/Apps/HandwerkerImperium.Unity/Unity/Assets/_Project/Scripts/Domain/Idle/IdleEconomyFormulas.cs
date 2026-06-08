using System;
using HandwerkerImperium.Domain.Offline;

namespace HandwerkerImperium.Domain.Idle
{
    /// <summary>
    /// Reine, Unity-freie Wirtschafts-Mathematik des Greybox-Loops (P0): geometrische Upgrade-Kosten,
    /// Upgrade-Effekte (Tempo/Radius/Kapazitaet), Produktions-/Worker-Durchsatz und Offline-Verdienst.
    /// Offline reused die Staffelung (0.80/0.35/0.15/0.05) aus dem Domain-Port
    /// (<see cref="OfflineProgressFormulas.CalculateStaggeredEarnings"/>) — P0-Spec §3.
    /// </summary>
    public static class IdleEconomyFormulas
    {
        /// <summary>Geometrische Upgrade-Kosten: round(base × growth^currentLevel), Minimum 1.</summary>
        public static decimal UpgradeCost(int currentLevel, decimal baseCost, double growth)
        {
            if (currentLevel < 0) currentLevel = 0;
            decimal raw = baseCost * (decimal)Math.Pow(growth, currentLevel);
            return Math.Max(1m, Math.Round(raw, 0));
        }

        /// <summary>Effektives Produktionsintervall (Sekunden/Ware) nach Tempo-Upgrade: base / (1 + level×step).</summary>
        public static double EffectiveProduceInterval(double baseInterval, int speedLevel, double step)
        {
            if (speedLevel < 0) speedLevel = 0;
            double divisor = 1.0 + speedLevel * step;
            if (divisor <= 0) divisor = 1.0;
            return baseInterval / divisor;
        }

        /// <summary>Effektiver Sammelradius nach Radius-Upgrade: base × (1 + level×step).</summary>
        public static double EffectiveCollectRadius(double baseRadius, int level, double step)
        {
            if (level < 0) level = 0;
            return baseRadius * (1.0 + level * step);
        }

        /// <summary>Effektive Trag-Kapazitaet nach Kapazitaets-Upgrade: round(base × (1 + level×step)), Minimum base.</summary>
        public static int EffectiveCarryCapacity(int baseCapacity, int level, double step)
        {
            if (level < 0) level = 0;
            int scaled = (int)Math.Round(baseCapacity * (1.0 + level * step));
            return Math.Max(baseCapacity, scaled);
        }

        /// <summary>Produktionsrate einer Station in Waren/Sekunde (1 / effektives Intervall).</summary>
        public static double ProductionRatePerSecond(double effectiveProduceInterval)
        {
            if (effectiveProduceInterval <= 0) return 0;
            return 1.0 / effectiveProduceInterval;
        }

        /// <summary>
        /// Worker-Durchsatz einer automatisierten Station in Waren/Sekunde — limitiert durch die
        /// Produktionsrate UND die Trag-Geschwindigkeit des Workers (min beider).
        /// </summary>
        public static double WorkerThroughputPerSecond(double effectiveProduceInterval, double workerCarrySpeed)
        {
            double prod = ProductionRatePerSecond(effectiveProduceInterval);
            return Math.Min(prod, Math.Max(0, workerCarrySpeed));
        }

        /// <summary>Automatisiertes Einkommen/Sekunde einer Station: Verkaufswert × Worker-Durchsatz.</summary>
        public static decimal AutomatedIncomePerSecond(decimal sellValue, double throughputPerSecond)
        {
            if (throughputPerSecond <= 0) return 0m;
            return sellValue * (decimal)throughputPerSecond;
        }

        /// <summary>
        /// Offline-Verdienst: gestaffelte Earnings (Domain-Port) bei einer Einkommens-Rate/Sekunde
        /// ueber die auf <paramref name="capSeconds"/> gedeckelte Abwesenheitsdauer.
        /// </summary>
        public static decimal OfflineEarnings(decimal automatedIncomePerSecond, double elapsedSeconds, double capSeconds)
        {
            if (automatedIncomePerSecond <= 0 || elapsedSeconds <= 0) return 0m;
            double capped = Math.Min(elapsedSeconds, Math.Max(0, capSeconds));
            if (capped <= 0) return 0m;
            return OfflineProgressFormulas.CalculateStaggeredEarnings(automatedIncomePerSecond, (decimal)capped);
        }
    }
}
