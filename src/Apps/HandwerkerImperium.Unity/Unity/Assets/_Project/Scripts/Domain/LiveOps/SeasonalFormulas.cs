#nullable enable
using System;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>Die vier Saisons (P2 §4 / GDD §10).</summary>
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    /// <summary>
    /// Saison-Erkennung (P2 §4, Original-Logik geborgen): jede Saison ist in den <b>ersten 14 Tagen</b>
    /// ihres Leitmonats aktiv (Frühling=März, Sommer=Juni, Herbst=September, Winter=Dezember). Liefert
    /// das Stadt-Deko-Override + die Event-Währung. UTC-datumsbasiert, reine Mathematik.
    /// </summary>
    public static class SeasonalFormulas
    {
        /// <summary>
        /// True, wenn am <paramref name="dateUtc"/> eine Saison aktiv ist; setzt dann <paramref name="season"/>.
        /// </summary>
        public static bool TryGetActiveSeason(DateTime dateUtc, out Season season)
        {
            int month = dateUtc.Month;
            int day = dateUtc.Day;
            season = Season.Spring;

            if (day < 1 || day > 14) return false;
            switch (month)
            {
                case 3: season = Season.Spring; return true;
                case 6: season = Season.Summer; return true;
                case 9: season = Season.Autumn; return true;
                case 12: season = Season.Winter; return true;
                default: return false;
            }
        }

        /// <summary>True, wenn an diesem UTC-Datum überhaupt eine Saison läuft.</summary>
        public static bool IsAnySeasonActive(DateTime dateUtc) => TryGetActiveSeason(dateUtc, out _);
    }
}
