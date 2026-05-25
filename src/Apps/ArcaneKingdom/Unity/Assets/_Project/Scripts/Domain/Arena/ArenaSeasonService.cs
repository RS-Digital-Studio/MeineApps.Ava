#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Player;

namespace ArcaneKingdom.Domain.Arena
{
    /// <summary>
    /// Arena-Saison-Definition (3 Monate gemaess Plan).
    /// </summary>
    public sealed class ArenaSeason
    {
        public int SeasonNumber { get; init; }
        public DateTime StartUtc { get; init; }
        public DateTime EndUtc { get; init; }
        public string ThemeKey { get; init; } = string.Empty;
        public TimeSpan Duration => EndUtc - StartUtc;
        public bool IsActive => DateTime.UtcNow >= StartUtc && DateTime.UtcNow < EndUtc;
        public TimeSpan TimeRemaining => EndUtc > DateTime.UtcNow ? EndUtc - DateTime.UtcNow : TimeSpan.Zero;
    }

    /// <summary>
    /// Spieler-Status pro Saison.
    /// </summary>
    public sealed class ArenaPlayerStanding
    {
        public string PlayerId { get; init; } = string.Empty;
        public int CurrentPoints { get; set; }
        public int HighestPoints { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public ArenaLeague League => ArenaLeagueTable.LeagueForPoints(CurrentPoints);
        public ArenaLeague HighestLeague => ArenaLeagueTable.LeagueForPoints(HighestPoints);
        public int TotalMatches => Wins + Losses;
        public float WinRate => TotalMatches > 0 ? (float)Wins / TotalMatches : 0f;
    }

    /// <summary>
    /// Saison-Ende-Belohnung pro Liga (Spielplan v5 Kap. 11.3).
    /// </summary>
    public sealed class ArenaSeasonReward
    {
        public ArenaLeague League { get; init; }
        public long Gold { get; init; }
        public int Diamonds { get; init; }
        public List<string> CardIds { get; init; } = new();
        public List<string> RuneIds { get; init; } = new();
        public string? Title { get; init; }
        public string? CosmeticKey { get; init; }
    }

    public sealed class ArenaSeasonService
    {
        /// <summary>
        /// Verarbeitet ein Match-Ergebnis und aktualisiert das Spieler-Standing.
        /// </summary>
        public ArenaPlayerStanding ApplyMatchResult(ArenaPlayerStanding standing, bool isWin)
        {
            if (standing is null) throw new ArgumentNullException(nameof(standing));

            var newPoints = ArenaLeagueTable.ApplyMatchResult(standing.CurrentPoints, isWin);
            standing.CurrentPoints = newPoints;
            standing.HighestPoints = Math.Max(standing.HighestPoints, newPoints);
            if (isWin) standing.Wins++;
            else standing.Losses++;
            return standing;
        }

        /// <summary>
        /// Berechnet Saison-Ende-Belohnung basierend auf der hoechsten erreichten Liga.
        /// </summary>
        public ArenaSeasonReward ComputeSeasonReward(ArenaLeague highestLeague)
        {
            return highestLeague switch
            {
                ArenaLeague.Bronze    => new ArenaSeasonReward { League = ArenaLeague.Bronze,    Gold = 5_000,  Diamonds = 10 },
                ArenaLeague.Silber    => new ArenaSeasonReward { League = ArenaLeague.Silber,    Gold = 15_000, Diamonds = 25, RuneIds = { "rune_common_atk" } },
                ArenaLeague.Gold      => new ArenaSeasonReward { League = ArenaLeague.Gold,      Gold = 40_000, Diamonds = 50, RuneIds = { "rune_rare_atk" }, CardIds = { "rare_seasonal_random" } },
                ArenaLeague.Platin    => new ArenaSeasonReward { League = ArenaLeague.Platin,    Gold = 100_000, Diamonds = 100, RuneIds = { "rune_epic_atk" }, CardIds = { "epic_seasonal_random" } },
                ArenaLeague.Diamant   => new ArenaSeasonReward { League = ArenaLeague.Diamant,   Gold = 250_000, Diamonds = 250, RuneIds = { "rune_epic_combo" }, CardIds = { "legendary_seasonal_random" } },
                ArenaLeague.Meister   => new ArenaSeasonReward { League = ArenaLeague.Meister,   Gold = 750_000, Diamonds = 750, RuneIds = { "rune_legendary_unique" }, CardIds = { "exclusive_meister_card" }, Title = "arena.title.meister", CosmeticKey = "cosmetic_arena_meister" },
                _ => new ArenaSeasonReward { League = highestLeague, Gold = 2_000, Diamonds = 5 }
            };
        }

        /// <summary>
        /// Erzeugt eine Standard-Saison (3 Monate ab Start-Datum).
        /// </summary>
        public ArenaSeason CreateSeason(int seasonNumber, DateTime startUtc, string themeKey) =>
            new()
            {
                SeasonNumber = seasonNumber,
                StartUtc = startUtc,
                EndUtc = startUtc.AddMonths(3),
                ThemeKey = themeKey
            };
    }
}
