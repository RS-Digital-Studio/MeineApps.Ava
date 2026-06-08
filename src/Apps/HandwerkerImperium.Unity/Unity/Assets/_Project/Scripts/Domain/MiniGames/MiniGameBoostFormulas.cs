#nullable enable
using System;

namespace HandwerkerImperium.Domain.MiniGames
{
    /// <summary>Bewertung einer Tap-Timing-Aktion (GDD §6.7).</summary>
    public enum TapRating
    {
        Miss = 0,
        Ok = 1,
        Good = 2,
        Perfect = 3
    }

    /// <summary>
    /// Optionale „Perfekt-Aktionen" (GDD §6.7): 2–3 sekundenkurze, freiwillige Tap-Timing-Mikrospiele
    /// (z. B. Säge-Schnitt / Hau-den-Nagel) → temporärer Tempo-Buff an einer Station. Statt 10 Pflicht-
    /// Mini-Games. Reine, Unity-freie Mathematik: Timing-Fehler → Rating → Buff-Stärke + -Dauer.
    /// </summary>
    public static class MiniGameBoostFormulas
    {
        /// <summary>
        /// Bewertet die Treffergüte aus dem absoluten Timing-Fehler (Sekunden) gegen die Fenster
        /// (perfect ⊂ good ⊂ ok). Außerhalb des Ok-Fensters → Miss.
        /// </summary>
        public static TapRating Rate(double errorSeconds, double perfectWindow, double goodWindow, double okWindow)
        {
            double e = Math.Abs(errorSeconds);
            if (e <= perfectWindow) return TapRating.Perfect;
            if (e <= goodWindow) return TapRating.Good;
            if (e <= okWindow) return TapRating.Ok;
            return TapRating.Miss;
        }

        /// <summary>Buff-Multiplikator je Rating (Miss = 1 = kein Buff; Perfect = voller Bonus).</summary>
        public static decimal BoostMultiplier(TapRating rating)
        {
            switch (rating)
            {
                case TapRating.Perfect: return 2.0m;
                case TapRating.Good: return 1.5m;
                case TapRating.Ok: return 1.2m;
                default: return 1.0m;
            }
        }

        /// <summary>Buff-Dauer (Sekunden) je Rating, skaliert über die Basis-Dauer (Miss = 0).</summary>
        public static double BoostDurationSeconds(TapRating rating, double baseDuration)
        {
            switch (rating)
            {
                case TapRating.Perfect: return baseDuration;
                case TapRating.Good: return baseDuration * 0.66;
                case TapRating.Ok: return baseDuration * 0.33;
                default: return 0.0;
            }
        }
    }
}
