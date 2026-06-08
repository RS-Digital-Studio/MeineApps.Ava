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

        /// <summary>Zeitpunkt, an dem der Offline-Verdienst-Deckel voll ist (lastSeen + Cap-Stunden).</summary>
        public static long OfflineCapFullAt(long lastSeenUtcTicks, double capHours)
        {
            if (capHours < 0) capHours = 0;
            return lastSeenUtcTicks + (long)(capHours * 3600.0 * TicksPerSecond);
        }

        /// <summary>Nächste UTC-Mitternacht ab <paramref name="nowUtcTicks"/> (Daily-bereit-Erinnerung).</summary>
        public static long NextUtcMidnight(long nowUtcTicks)
        {
            DateTime now = new DateTime(nowUtcTicks, DateTimeKind.Utc);
            DateTime midnight = now.Date.AddDays(1);
            return midnight.Ticks;
        }

        /// <summary>Zeitpunkt, an dem ein Rush-Event wieder verfügbar ist (Cooldown-Ende), nie in der Vergangenheit.</summary>
        public static long AvailableAt(long cooldownUntilUtcTicks, long nowUtcTicks) =>
            cooldownUntilUtcTicks > nowUtcTicks ? cooldownUntilUtcTicks : nowUtcTicks;

        /// <summary>Zeitpunkt in <paramref name="seconds"/> Sekunden ab jetzt (generischer relativer Trigger).</summary>
        public static long InSeconds(long nowUtcTicks, double seconds)
        {
            if (seconds < 0) seconds = 0;
            return nowUtcTicks + (long)(seconds * TicksPerSecond);
        }
    }
}
