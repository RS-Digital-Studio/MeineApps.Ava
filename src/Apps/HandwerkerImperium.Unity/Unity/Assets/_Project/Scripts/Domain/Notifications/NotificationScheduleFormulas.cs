#nullable enable
using System;

namespace HandwerkerImperium.Domain.Notifications
{
    /// <summary>Lokal planbare Push-Trigger (P3 §2/§4, sinngemäß aus dem Original; Hans-Persona-Präfix im Game-Layer).</summary>
    public enum NotificationTrigger
    {
        OfflineCapFull = 0,
        DailyReady = 1,
        RushAvailable = 2,
        RestorationDone = 3,
        RushOrderExpiring = 4,
        SupplyEmpty = 5,
        MasteryLevelUp = 6,
        SeasonStart = 7
    }

    /// <summary>
    /// Notification-Scheduling (P3 §2/§4): berechnet die UTC-Zeitpunkte der lokal planbaren Push-Trigger.
    /// Reine, Unity-freie Zeit-Mathematik (UTC-Ticks); die eigentliche Planung + der Hans-Text liegen im
    /// Android-Game-Layer.
    /// </summary>
    public static class NotificationScheduleFormulas
    {
        private const long TicksPerSecond = 10_000_000L;
        private const long MaxDtTicks = 3155378975999999999L; // DateTime.MaxValue.Ticks

        /// <summary>Zeitpunkt, an dem der Offline-Verdienst-Deckel voll ist (lastSeen + Cap-Stunden), gesättigt.</summary>
        public static long OfflineCapFullAt(long lastSeenUtcTicks, double capHours)
        {
            if (capHours < 0) capHours = 0;
            return AddClamped(lastSeenUtcTicks, capHours * 3600.0 * TicksPerSecond);
        }

        /// <summary>Nächste UTC-Mitternacht ab <paramref name="nowUtcTicks"/> (Daily-bereit-Erinnerung), eingabe-robust.</summary>
        public static long NextUtcMidnight(long nowUtcTicks)
        {
            if (nowUtcTicks < 0) nowUtcTicks = 0;
            if (nowUtcTicks > MaxDtTicks) nowUtcTicks = MaxDtTicks;
            DateTime now = new DateTime(nowUtcTicks, DateTimeKind.Utc);
            if (now.Date >= DateTime.MaxValue.Date) return MaxDtTicks;
            return now.Date.AddDays(1).Ticks;
        }

        /// <summary>Zeitpunkt, an dem ein Rush-Event wieder verfügbar ist (Cooldown-Ende), nie in der Vergangenheit.</summary>
        public static long AvailableAt(long cooldownUntilUtcTicks, long nowUtcTicks) =>
            cooldownUntilUtcTicks > nowUtcTicks ? cooldownUntilUtcTicks : nowUtcTicks;

        /// <summary>Zeitpunkt in <paramref name="seconds"/> Sekunden ab jetzt (generischer relativer Trigger), gesättigt.</summary>
        public static long InSeconds(long nowUtcTicks, double seconds)
        {
            if (seconds < 0) seconds = 0;
            return AddClamped(nowUtcTicks, seconds * TicksPerSecond);
        }

        /// <summary>Addiert einen (nicht-negativen) Tick-Offset gesättigt auf [0, DateTime.MaxValue.Ticks].</summary>
        private static long AddClamped(long baseTicks, double offsetTicks)
        {
            if (double.IsNaN(offsetTicks) || offsetTicks < 0) offsetTicks = 0;
            if (baseTicks < 0) baseTicks = 0;
            if (offsetTicks > (double)MaxDtTicks) return MaxDtTicks;
            long o = (long)offsetTicks;
            if (baseTicks > MaxDtTicks - o) return MaxDtTicks;
            return baseTicks + o;
        }
    }
}
