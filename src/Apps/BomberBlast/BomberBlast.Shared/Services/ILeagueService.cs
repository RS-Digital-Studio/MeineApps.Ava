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

    /// <summary>
    /// DSGVO Art. 17: Eigenen Liga-Eintrag aus Firebase löschen.
    /// Cascading-Delete für Account-Löschung. Best-Effort: Bei Offline-Status oder Auth-Fehler
    /// wird der Aufruf still abgebrochen (lokale Daten werden trotzdem gelöscht).
    /// </summary>
    Task DeleteOwnEntryAsync();

    event EventHandler? PointsChanged;
    event EventHandler? SeasonEnded;
    event EventHandler? LeaderboardUpdated;

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY BOMB RACE (v2.0.41, Plan Task 3.1)
    // ═══════════════════════════════════════════════════════════════════════
    // Alle Liga-Mitglieder bekommen denselben Tages-Seed. Top-Score pro Tag ranked.
    // Liga-Punkte: Daily-Race-Top-10 = +50 Punkte, Top-3 = +100 Punkte.
    // Firebase-Schema: league/s{saison}/daily_race/{utcDate-yyyy-MM-dd}/{tier}/{uid}

    /// <summary>UTC-Datum-basierter Seed fuer das tageaktuelle Race-Level (deterministisch fuer alle Spieler).</summary>
    int GetDailyRaceSeed(DateTime utcDate);

    /// <summary>UTC-Datum-Key fuer das tageaktuelle Race (Format "yyyy-MM-dd").</summary>
    string GetDailyRaceDateKey(DateTime utcDate);

    /// <summary>
    /// Daily-Race-Score melden (zentral fuer alle Spieler in derselben Liga + Tag).
    /// Nimmt nur Submission an wenn Score > bisheriger eigener Tagesbest.
    /// Liga-Punkte werden separat ueber AddPoints() vergeben (Top-10/Top-3 nach Refresh).
    /// </summary>
    /// <param name="score">Erzielter Score auf dem heutigen Daily-Race-Level.</param>
    Task<bool> SubmitDailyRaceScoreAsync(int score);

    /// <summary>
    /// Liefert die Daily-Race-Rangliste fuer das angegebene Datum (default: heute).
    /// Top 20 echte Spieler aus Firebase fuer den eigenen Tier.
    /// </summary>
    Task<IReadOnlyList<LeagueLeaderboardEntry>> GetDailyRaceLeaderboardAsync(DateTime? utcDate = null);

    /// <summary>
    /// Liefert die globale Cross-Tier-Daily-Race-Rangliste (alle 5 Tiers parallel gefetcht + gemerged).
    /// Top 50 echte Spieler weltweit. Performance-Hinweis: 5 parallele Firebase-Reads — etwas teurer
    /// als der Single-Tier-Fetch, aber bei kleinem Saison-Set akzeptabel.
    /// </summary>
    Task<IReadOnlyList<LeagueLeaderboardEntry>> GetDailyRaceGlobalLeaderboardAsync(DateTime? utcDate = null);

    /// <summary>Eigener Best-Score fuer das heutige Daily Race (0 wenn noch nicht gespielt).</summary>
    int TodayDailyRaceBestScore { get; }

    /// <summary>True wenn der Spieler heute schon mind. 1 Daily-Race-Run aufgezeichnet hat.</summary>
    bool HasPlayedDailyRaceToday { get; }
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
