#nullable enable
using System;

namespace HandwerkerImperium.Domain.Offline
{
    /// <summary>
    /// Pure, entkoppelte Offline-Staffel-Formel — die einzige aus dem alten Domain-Port
    /// übernommene Mathematik (3D-Idle-Neuausrichtung, P0-Spec §3 / GDD §8). Keine
    /// Abhängigkeit auf Alt-Typen mehr; der Idle-Kern (<c>IdleEconomyFormulas</c>) ruft
    /// ausschließlich <see cref="CalculateStaggeredEarnings"/>.
    /// </summary>
    public static class OfflineProgressFormulas
    {
        /// <summary>
        /// Gestaffelte Offline-Earnings: 0–2 h 80 %, 2–4 h 35 %, 4–8 h 15 %, 8 h+ 5 %.
        /// </summary>
        public static decimal CalculateStaggeredEarnings(decimal netPerSecond, decimal totalSeconds)
        {
            decimal first2h = Math.Min(totalSeconds, 7200m);
            decimal next2h = Math.Min(Math.Max(totalSeconds - 7200m, 0m), 7200m);
            decimal next4h = Math.Min(Math.Max(totalSeconds - 14400m, 0m), 14400m);
            decimal remaining = Math.Max(totalSeconds - 28800m, 0m);
            return netPerSecond * (first2h * 0.80m + next2h * 0.35m + next4h * 0.15m + remaining * 0.05m);
        }
    }
}
