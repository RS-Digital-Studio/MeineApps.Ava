#nullable enable
using System;

namespace ArcaneKingdom.Domain.Season
{
    /// <summary>
    /// Pure-C#-Logik für Reset-Zeitpunkte (Daily/Weekly/Saison).
    /// Reset-Grenzen sind UTC, damit alle Server-Regionen synchron resetten.
    /// </summary>
    public static class ResetWindow
    {
        public const int DailyResetHourUtc = 0;             // 00:00 UTC
        public const DayOfWeek WeeklyResetDay = DayOfWeek.Monday;
        public const int ArenaSeasonDays = 30;

        public static DateTime NextDailyResetUtc(DateTime nowUtc)
        {
            var today = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, DailyResetHourUtc, 0, 0, DateTimeKind.Utc);
            return today > nowUtc ? today : today.AddDays(1);
        }

        public static DateTime NextWeeklyResetUtc(DateTime nowUtc)
        {
            var nextDaily = NextDailyResetUtc(nowUtc);
            // Voranschreiten bis zum WeeklyResetDay
            for (var i = 0; i < 8; i++)
            {
                if (nextDaily.DayOfWeek == WeeklyResetDay) return nextDaily;
                nextDaily = nextDaily.AddDays(1);
            }
            // Defensive: sollte nie ausgelöst werden
            return nextDaily;
        }

        public static DateTime NextSeasonResetUtc(DateTime seasonStartUtc) =>
            seasonStartUtc.AddDays(ArenaSeasonDays);

        public static bool HasCrossedDailyReset(DateTime lastResetUtc, DateTime nowUtc) =>
            NextDailyResetUtc(lastResetUtc) <= nowUtc;

        public static bool HasCrossedWeeklyReset(DateTime lastResetUtc, DateTime nowUtc) =>
            NextWeeklyResetUtc(lastResetUtc) <= nowUtc;
    }
}
