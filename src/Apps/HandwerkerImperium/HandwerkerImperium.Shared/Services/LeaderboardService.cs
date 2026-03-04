using System.Globalization;
using System.Text.Json;
using HandwerkerImperium.Models.Firebase;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet globale Leaderboards via Firebase Realtime Database.
/// Unterstuetzt 5 Boards: 3 permanente + 2 woechentliche.
/// </summary>
public sealed class LeaderboardService : ILeaderboardService
{
    // Board-IDs (Firebase-Pfade unter leaderboards/)
    public const string BoardPlayerLevel = "player_level";
    public const string BoardTotalEarnings = "total_earnings";
    public const string BoardPrestigePoints = "prestige_points";
    public const string BoardWeeklyEarnings = "weekly_earnings";
    public const string BoardWeeklyMinigame = "weekly_minigame";

    private static readonly string[] AllBoards =
    [
        BoardPlayerLevel,
        BoardTotalEarnings,
        BoardPrestigePoints,
        BoardWeeklyEarnings,
        BoardWeeklyMinigame
    ];

    private static readonly string[] WeeklyBoards =
    [
        BoardWeeklyEarnings,
        BoardWeeklyMinigame
    ];

    private readonly IFirebaseService _firebase;
    private readonly IGameStateService _gameState;
    private readonly IGuildService _guildService;
    private readonly SemaphoreSlim _submitLock = new(1, 1);

    public LeaderboardService(
        IFirebaseService firebase,
        IGameStateService gameState,
        IGuildService guildService)
    {
        _firebase = firebase;
        _gameState = gameState;
        _guildService = guildService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SUBMIT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert alle Leaderboard-Scores des Spielers (fire-and-forget sicher).
    /// </summary>
    public async Task SubmitScoresAsync()
    {
        if (!await _submitLock.WaitAsync(0).ConfigureAwait(false))
            return; // Bereits am Submiten, ueberspringen

        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid))
                return;

            var state = _gameState.State;
            var playerName = GetPlayerName();
            var now = DateTime.UtcNow.ToString("O");
            var weekId = GetCurrentWeekId();

            // Permanente Boards
            await SubmitEntryAsync(BoardPlayerLevel, uid, playerName, state.PlayerLevel, now).ConfigureAwait(false);
            await SubmitEntryAsync(BoardTotalEarnings, uid, playerName, (long)state.TotalMoneyEarned, now).ConfigureAwait(false);
            await SubmitEntryAsync(BoardPrestigePoints, uid, playerName, state.Prestige.TotalPrestigeCount, now).ConfigureAwait(false);

            // Woechentliche Boards (mit weekId)
            await SubmitWeeklyEntryAsync(BoardWeeklyEarnings, uid, playerName, (long)state.TotalMoneyEarned, now, weekId).ConfigureAwait(false);
            await SubmitWeeklyEntryAsync(BoardWeeklyMinigame, uid, playerName, state.TotalMiniGamesPlayed, now, weekId).ConfigureAwait(false);

            // Spieler-Profil aktualisieren
            await UpdatePlayerProfileAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in SubmitScoresAsync: {ex.Message}");
        }
        finally
        {
            _submitLock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // QUERY
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Laedt die Top-50 eines Leaderboards, sortiert nach Score absteigend.
    /// </summary>
    public async Task<List<LeaderboardDisplayEntry>> GetTopEntriesAsync(string boardId)
    {
        var result = new List<LeaderboardDisplayEntry>();

        try
        {
            var uid = _firebase.Uid;

            // Firebase REST API: orderBy + limitToLast fuer Top-Eintraege
            var queryParams = "orderBy=\"score\"&limitToLast=50";
            var json = await _firebase.QueryAsync($"leaderboards/{boardId}/entries", queryParams).ConfigureAwait(false);

            if (string.IsNullOrEmpty(json))
                return result;

            var entries = JsonSerializer.Deserialize<Dictionary<string, FirebaseLeaderboardEntry>>(json);
            if (entries == null || entries.Count == 0)
                return result;

            // Woechentliche Boards: Nur Eintraege der aktuellen Woche
            var currentWeekId = GetCurrentWeekId();
            var isWeekly = WeeklyBoards.Contains(boardId);

            // Nach Score absteigend sortieren
            var sorted = entries
                .Where(e => !isWeekly || e.Value.WeekId == currentWeekId)
                .OrderByDescending(e => e.Value.Score)
                .ToList();

            int rank = 1;
            foreach (var (entryUid, entry) in sorted)
            {
                result.Add(new LeaderboardDisplayEntry
                {
                    Rank = rank++,
                    Name = entry.Name,
                    Score = entry.Score,
                    IsCurrentPlayer = entryUid == uid
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in GetTopEntriesAsync: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Laedt den eigenen Eintrag auf einem Board.
    /// </summary>
    public async Task<LeaderboardDisplayEntry?> GetOwnEntryAsync(string boardId)
    {
        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid))
                return null;

            var entry = await _firebase.GetAsync<FirebaseLeaderboardEntry>($"leaderboards/{boardId}/entries/{uid}").ConfigureAwait(false);
            if (entry == null)
                return null;

            // Woechentliche Boards: Pruefen ob aktuelle Woche
            var isWeekly = WeeklyBoards.Contains(boardId);
            if (isWeekly && entry.WeekId != GetCurrentWeekId())
                return null;

            // Rang muss ueber GetTopEntriesAsync ermittelt werden
            // (Firebase REST API hat keine direkte Rang-Abfrage)
            var topEntries = await GetTopEntriesAsync(boardId).ConfigureAwait(false);
            var ownInTop = topEntries.FirstOrDefault(e => e.IsCurrentPlayer);

            return new LeaderboardDisplayEntry
            {
                Rank = ownInTop?.Rank ?? -1, // -1 = nicht in Top-50
                Name = entry.Name,
                Score = entry.Score,
                IsCurrentPlayer = true
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in GetOwnEntryAsync: {ex.Message}");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROFIL
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert das oeffentliche Spieler-Profil in Firebase.
    /// </summary>
    public async Task UpdatePlayerProfileAsync()
    {
        try
        {
            var uid = _firebase.Uid;
            if (string.IsNullOrEmpty(uid))
                return;

            var state = _gameState.State;
            var profile = new FirebasePlayerProfile
            {
                Name = GetPlayerName(),
                Level = state.PlayerLevel,
                PrestigeTier = state.Prestige.CurrentTier.ToString(),
                GuildName = state.GuildMembership?.GuildName,
                LastSeen = DateTime.UtcNow.ToString("O")
            };

            await _firebase.SetAsync($"player_profiles/{uid}", profile).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in UpdatePlayerProfileAsync: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Schreibt einen Eintrag auf ein permanentes Board.
    /// </summary>
    private async Task SubmitEntryAsync(string boardId, string uid, string name, long score, string updatedAt)
    {
        var entry = new FirebaseLeaderboardEntry
        {
            Name = name,
            Score = score,
            UpdatedAt = updatedAt
        };

        await _firebase.SetAsync($"leaderboards/{boardId}/entries/{uid}", entry).ConfigureAwait(false);
    }

    /// <summary>
    /// Schreibt einen Eintrag auf ein woechentliches Board (mit weekId).
    /// </summary>
    private async Task SubmitWeeklyEntryAsync(string boardId, string uid, string name, long score, string updatedAt, string weekId)
    {
        var entry = new FirebaseLeaderboardEntry
        {
            Name = name,
            Score = score,
            UpdatedAt = updatedAt,
            WeekId = weekId
        };

        await _firebase.SetAsync($"leaderboards/{boardId}/entries/{uid}", entry).ConfigureAwait(false);
    }

    /// <summary>
    /// Ermittelt den Spieler-Namen (aus Gilden-System oder Fallback).
    /// </summary>
    private string GetPlayerName()
    {
        // Gilden-Name hat Prioritaet (wurde vom Spieler eingegeben)
        if (!string.IsNullOrEmpty(_guildService.PlayerName))
            return _guildService.PlayerName;

        // Fallback: "Spieler" + Level
        return $"Spieler {_gameState.State.PlayerLevel}";
    }

    /// <summary>
    /// Berechnet die ISO-Wochennummer fuer woechentliche Boards.
    /// Format: "2026-W08"
    /// </summary>
    private static string GetCurrentWeekId()
    {
        var now = DateTime.UtcNow;
        var cal = CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{now.Year}-W{week:D2}";
    }
}
