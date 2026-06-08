#nullable enable
using System;

namespace HandwerkerImperium.Domain.Social
{
    /// <summary>
    /// Cross-Promotion (P3 §2): House-Ads für die eigenen Apps mit deterministischer Tagesrotation — jeder
    /// UTC-Tag zeigt einen anderen Eintrag, ohne Server-Roundtrip. Reine, Unity-freie Mathematik.
    /// </summary>
    public static class CrossPromoFormulas
    {
        private const long TicksPerDay = 864_000_000_000L; // 24 h * 3600 s * 10^7 Ticks

        /// <summary>Anzahl ganzer UTC-Tage seit Epoch (für die Rotation).</summary>
        public static long EpochDay(long nowUtcTicks)
        {
            // DateTime-Ticks zählen ab 0001-01-01; ganze Tage davon sind als Rotationsindex ausreichend stabil.
            return nowUtcTicks / TicksPerDay;
        }

        /// <summary>Index des heute anzuzeigenden Promo-Eintrags in [0, promoCount).</summary>
        public static int RotationIndex(long nowUtcTicks, int promoCount)
        {
            if (promoCount <= 1) return 0;
            long idx = EpochDay(nowUtcTicks) % promoCount;
            if (idx < 0) idx += promoCount;
            return (int)idx;
        }

        /// <summary>True, wenn seit dem letzten Anzeigetag ein neuer UTC-Tag begonnen hat (erneut zeigen).</summary>
        public static bool ShouldRotate(long lastShownUtcTicks, long nowUtcTicks)
        {
            if (lastShownUtcTicks <= 0) return true;
            DateTime last = new DateTime(lastShownUtcTicks, DateTimeKind.Utc).Date;
            DateTime now = new DateTime(nowUtcTicks, DateTimeKind.Utc).Date;
            return now > last;
        }
    }
}
