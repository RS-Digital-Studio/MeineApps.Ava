#nullable enable
namespace ArcaneKingdom.Domain.Arena
{
    /// <summary>
    /// Arena-Liga-Stufen (Spielplan v5 Kap. 11.3).
    /// Bronze ist Einsteiger-Liga, Meister nur Top-100.
    /// </summary>
    public enum ArenaLeague
    {
        Trainings = 0,  // freigeschaltet bei LV10
        Bronze    = 1,
        Silber    = 2,
        Gold      = 3,
        Platin    = 4,
        Diamant   = 5,
        Meister   = 6  // Top 100
    }

    /// <summary>
    /// Punkte-Schwellen + Saison-Belohnungen pro Liga.
    /// </summary>
    public static class ArenaLeagueTable
    {
        /// <summary>Minimum-Rangpunkte um in dieser Liga zu spielen.</summary>
        public static int MinPointsForLeague(ArenaLeague league) => league switch
        {
            ArenaLeague.Trainings => 0,
            ArenaLeague.Bronze    => 1_000,
            ArenaLeague.Silber    => 2_500,
            ArenaLeague.Gold      => 5_000,
            ArenaLeague.Platin    => 10_000,
            ArenaLeague.Diamant   => 20_000,
            ArenaLeague.Meister   => 40_000,
            _ => 0
        };

        /// <summary>Liga zu Punkten zuordnen.</summary>
        public static ArenaLeague LeagueForPoints(int points)
        {
            if (points >= MinPointsForLeague(ArenaLeague.Meister)) return ArenaLeague.Meister;
            if (points >= MinPointsForLeague(ArenaLeague.Diamant)) return ArenaLeague.Diamant;
            if (points >= MinPointsForLeague(ArenaLeague.Platin))  return ArenaLeague.Platin;
            if (points >= MinPointsForLeague(ArenaLeague.Gold))    return ArenaLeague.Gold;
            if (points >= MinPointsForLeague(ArenaLeague.Silber))  return ArenaLeague.Silber;
            if (points >= MinPointsForLeague(ArenaLeague.Bronze))  return ArenaLeague.Bronze;
            return ArenaLeague.Trainings;
        }

        /// <summary>Punkte fuer Sieg (Plan-Werte: +25 Punkte pro Sieg, -15 pro Niederlage).</summary>
        public const int PointsPerWin = 25;
        public const int PointsPerLoss = -15;

        /// <summary>Maximaler Punkte-Verlust pro Niederlage (kein Liga-Abstieg unter Liga-Minimum).</summary>
        public static int ApplyMatchResult(int currentPoints, bool isWin)
        {
            var delta = isWin ? PointsPerWin : PointsPerLoss;
            var newPoints = currentPoints + delta;
            // Verhindert Abrutschen unter Liga-Schwelle bei Niederlage (Plan-konform: Soft-Floor)
            return System.Math.Max(0, newPoints);
        }
    }
}
