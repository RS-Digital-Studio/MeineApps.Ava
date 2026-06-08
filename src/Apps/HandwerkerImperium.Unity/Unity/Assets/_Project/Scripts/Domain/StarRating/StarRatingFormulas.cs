#nullable enable
using System.Collections.Generic;

namespace HandwerkerImperium.Domain.StarRating
{
    /// <summary>
    /// Stadt-Stern-Rating 1–5★ (P1 §3/§4, GDD §7, PROGRESSION_BALANCING §7): ein gewichtetes Aggregat aus
    /// freigeschalteten Werkstätten + abgeschlossenen Sanierungs-Bauphasen + Auftragsvolumen, mit
    /// <b>Hysterese</b> (persistierter Stern + Puffer), damit das Rating nicht an einer Schwelle flackert.
    /// Schwellen/Gewichte/Puffer kommen aus dem BalancingConfig (Game-Layer) — hier nur die reine,
    /// Unity-freie, NUnit-testbare Mathematik. Schwellen steigen je Stadt (Akte werden länger).
    /// </summary>
    public static class StarRatingFormulas
    {
        /// <summary>
        /// Aggregat-Score der Stadt aus den drei Quellen, jeweils gewichtet.
        /// </summary>
        public static double Score(
            int unlockedWorkshops, int restorationPhasesComplete, long ordersServed,
            double workshopWeight, double restorationWeight, double volumeWeight)
        {
            double w = unlockedWorkshops < 0 ? 0 : unlockedWorkshops;
            double r = restorationPhasesComplete < 0 ? 0 : restorationPhasesComplete;
            double o = ordersServed < 0 ? 0 : ordersServed;
            return workshopWeight * w + restorationWeight * r + volumeWeight * o;
        }

        /// <summary>
        /// Roher Stern (1–5) rein aus Score + Schwellen, ohne Hysterese.
        /// <paramref name="thresholds"/> = aufsteigende Score-Schwellen für 2★…5★ (Länge 4 ⇒ max. 5★).
        /// </summary>
        public static int RawStar(double score, IReadOnlyList<double>? thresholds)
        {
            int star = 1;
            if (thresholds == null) return star;
            for (int i = 0; i < thresholds.Count; i++)
                if (score >= thresholds[i]) star = i + 2;
            return star;
        }

        /// <summary>
        /// Sternbewertung mit Hysterese: steigt sofort, wenn der Score eine höhere Schwelle erreicht;
        /// fällt erst, wenn er die Eintrittsschwelle des aktuellen Sterns um mehr als
        /// <paramref name="buffer"/> Punkte unterschreitet (sonst wird der Stern gehalten).
        /// </summary>
        /// <param name="currentStar">Zuletzt persistierter Stern (TownSlice), wird auf 1…max geklemmt.</param>
        public static int EvaluateStars(double score, int currentStar, IReadOnlyList<double>? thresholds, double buffer)
        {
            int maxStar = (thresholds?.Count ?? 0) + 1;
            if (currentStar < 1) currentStar = 1;
            if (currentStar > maxStar) currentStar = maxStar;

            int rawStar = RawStar(score, thresholds);

            if (rawStar > currentStar)
                return rawStar; // Aufstieg: sofort

            if (rawStar < currentStar)
            {
                // Abstieg nur, wenn der Score die Eintrittsschwelle des aktuellen Sterns um > buffer unterschreitet.
                double entryThreshold = thresholds![currentStar - 2]; // currentStar >= 2 garantiert
                if (score < entryThreshold - buffer)
                    return rawStar;
                return currentStar; // im Hysterese-Puffer -> halten
            }

            return currentStar;
        }

        /// <summary>True, wenn die Stadt das Prestige-freigebende 5★ erreicht hat.</summary>
        public static bool IsPrestigeReady(int currentStar) => currentStar >= 5;
    }
}
