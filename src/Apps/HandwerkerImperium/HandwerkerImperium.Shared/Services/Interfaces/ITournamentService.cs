using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service für wöchentliche MiniGame-Turniere mit echten Play Games Leaderboards.
/// Fallback auf simulierte Gegner wenn nicht angemeldet.
/// </summary>
public interface ITournamentService
{
    /// <summary>Prüft ob ein neues Turnier gestartet werden muss (jede Woche Montag).</summary>
    void CheckAndStartNewTournament();

    /// <summary>Setzt tägliche Teilnahmen zurück wenn neuer Tag.</summary>
    void ResetDailyEntries();

    /// <summary>Ob der Spieler am Turnier teilnehmen kann (gratis oder mit Goldschrauben).</summary>
    bool CanEnter { get; }

    /// <summary>Kosten für die nächste Teilnahme (0 = gratis, 5 = Goldschrauben).</summary>
    int EntryCost { get; }

    /// <summary>Trägt einen MiniGame-Score ins Turnier ein.</summary>
    void RecordScore(int score);

    /// <summary>
    /// Beansprucht Turnier-Belohnungen. Gibt Tier, Goldschrauben und Geld zurück.
    /// Null wenn keine Belohnung verfügbar.
    /// </summary>
    (TournamentRewardTier tier, int screws, decimal money)? ClaimRewards();

    /// <summary>
    /// Lädt das Leaderboard für das aktuelle Turnier (Play Games oder Fallback).
    /// </summary>
    Task LoadLeaderboardAsync();

    /// <summary>
    /// Ob Play Games angemeldet ist (für Fallback-Hinweis im UI).
    /// </summary>
    bool IsPlayGamesSignedIn { get; }
}
