using BomberBlast.Models.League;

namespace BomberBlast.Services;

/// <summary>
/// Liga-System: Echte Online-Rangliste via Firebase + NPC-Backfill.
/// Local-First: Lokaler State für schnelle Reads, Firebase-Sync im Hintergrund.
/// Deterministische 14-Tage-Saisons (alle Clients stimmen überein).
/// </summary>
public interface ILeagueService : IDisposable
{
    LeagueTier CurrentTier { get; }
    int CurrentPoints { get; }
    int SeasonNumber { get; }
    bool IsSeasonRewardClaimed { get; }

    /// <summary>Ob Firebase erreichbar ist.</summary>
    bool IsOnline { get; }

    /// <summary>Ob gerade Daten geladen werden.</summary>
    bool IsLoading { get; }

    /// <summary>Spieler-Anzeigename für die Rangliste.</summary>
    string PlayerName { get; }

    /// <summary>Punkte hinzufügen (Story-Level, Boss, Daily, Dungeon etc.). Pushed async zu Firebase.</summary>
    void AddPoints(int amount);

    /// <summary>Rangliste: Echte Spieler aus Firebase-Cache + NPC-Backfill auf 20 Einträge.</summary>
    IReadOnlyList<LeagueLeaderboardEntry> GetLeaderboard();

    /// <summary>Spieler-Rang in der aktuellen Rangliste (1-basiert).</summary>
    int GetPlayerRank();

    /// <summary>Verbleibende Zeit bis Saisonende (deterministisch berechnet).</summary>
    TimeSpan GetSeasonTimeRemaining();

    /// <summary>Prüft ob Saison abgelaufen ist und verarbeitet Saisonende.</summary>
    bool CheckAndProcessSeasonEnd();

    /// <summary>Saison-Belohnung abholen.</summary>
    bool ClaimSeasonReward();

    /// <summary>Liga-Statistiken über alle Saisons.</summary>
    LeagueStats GetStats();

    /// <summary>Spielername setzen (wird in Firebase gespeichert).</summary>
    void SetPlayerName(string name);

    /// <summary>Rangliste von Firebase aktualisieren (async, für UI-Refresh).</summary>
    Task RefreshLeaderboardAsync();

    /// <summary>Initialen Firebase-Sync durchführen (Auth + Daten laden).</summary>
    Task InitializeOnlineAsync();

    /// <summary>
    /// Meldet einen Leaderboard-Eintrag wegen anstössigem Namen / Cheating (UGC-Moderation).
    /// Schreibt nach Firebase: <c>reports/{reportedUid}/{reporterUid}</c> mit Timestamp + Reason.
    /// Security-Rules erlauben max. 1 Report pro Reporter/Reported-Kombi pro 24h.
    /// </summary>
    /// <param name="reportedUid">Firebase-UID des gemeldeten Spielers.</param>
    /// <param name="reason">Grund: "offensive_name", "cheating", "other".</param>
    /// <returns>true bei Erfolg, false wenn offline oder Rate-Limit.</returns>
    Task<bool> ReportPlayerAsync(string reportedUid, string reason);

    event EventHandler? PointsChanged;
    event EventHandler? SeasonEnded;
    event EventHandler? LeaderboardUpdated;

}

/// <summary>Einzelner Eintrag in der Liga-Rangliste.</summary>
public class LeagueLeaderboardEntry
{
    /// <summary>Firebase-UID des Spielers (leer bei NPCs). Für Report-Funktion.</summary>
    public string Uid { get; set; } = "";

    public string Name { get; set; } = "";
    public int Points { get; set; }
    public int Rank { get; set; }
    public bool IsPlayer { get; set; }

    /// <summary>Ob dieser Eintrag ein echter Spieler ist (nicht NPC).</summary>
    public bool IsRealPlayer { get; set; }
}
