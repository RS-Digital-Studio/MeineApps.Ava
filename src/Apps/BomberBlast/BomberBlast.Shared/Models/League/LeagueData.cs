namespace BomberBlast.Models.League;

/// <summary>
/// Persistenz-Daten des Liga-Systems. JSON via IPreferencesService.
/// </summary>
public class LeagueData
{
    public LeagueTier CurrentTier { get; set; } = LeagueTier.Bronze;
    public int Points { get; set; }
    public int SeasonNumber { get; set; } = 1;
    public string SeasonStartUtc { get; set; } = "";
    public bool SeasonRewardClaimed { get; set; }

    /// <summary>
    /// Zuletzt online ermitteltes Perzentil des Spielers (0 = Top, 1 = Schlusslicht) der LAUFENDEN
    /// Saison. Wird bei jedem echten Online-Leaderboard-Refresh aktualisiert und beim Saisonende für
    /// Auf-/Abstieg genutzt — statt einer Offline-Schätzung gegen den NPC-Backfill der NEUEN Saison.
    /// -1 = noch nie online ermittelt → kein Auf-/Abstieg (kein unverdienter Abstieg).
    /// </summary>
    public float LastOnlinePercentile { get; set; } = -1f;

    /// <summary>NPC-Einträge der aktuellen Saison (20 Stück)</summary>
    public List<LeagueNpcEntry> Npcs { get; set; } = [];
}

/// <summary>Simulierter NPC-Gegner in der Liga</summary>
public class LeagueNpcEntry
{
    public string Name { get; set; } = "";
    public int BaseScore { get; set; }

    /// <summary>Täglicher Score-Zuwachs (seeded Random pro Saison)</summary>
    public int DailyGrowth { get; set; }
}

/// <summary>Stats über alle Saisons hinweg</summary>
public class LeagueStats
{
    public int TotalSeasons { get; set; }
    public int HighestTier { get; set; }
    public int TotalPointsEarned { get; set; }
    public int TotalPromotions { get; set; }
    public int BestSeasonPoints { get; set; }
}
