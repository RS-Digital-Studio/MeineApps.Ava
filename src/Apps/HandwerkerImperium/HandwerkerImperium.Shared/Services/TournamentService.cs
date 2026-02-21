using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet wöchentliche MiniGame-Turniere mit echten Play Games Leaderboards.
/// Jede Woche wird ein zufälliger MiniGame-Typ ausgewählt.
/// Spieler kann 3x täglich gratis teilnehmen, danach für 5 Goldschrauben.
/// Wenn Play Games nicht angemeldet: Fallback auf simulierte Gegner.
/// </summary>
public class TournamentService : ITournamentService
{
    private readonly IGameStateService _gameState;
    private readonly IPlayGamesService? _playGamesService;

    /// <summary>Leaderboard-IDs für Turnier-Scores in Play Games.</summary>
    private const string LeaderboardWeeklyScore = "CgkloeDjOZMKEAIQFA";
    private const string LeaderboardTournamentWins = "CgkloeDjOZMKEAIQFQ";
    private const string LeaderboardHighScore = "CgkloeDjOZMKEAIQFg";

    /// <summary>Verfügbare MiniGame-Typen für Turniere (nur die tatsächlich spielbaren).</summary>
    private static readonly MiniGameType[] TournamentGameTypes =
    [
        MiniGameType.Sawing,
        MiniGameType.PipePuzzle,
        MiniGameType.WiringGame,
        MiniGameType.PaintingGame,
        MiniGameType.RoofTiling,
        MiniGameType.Blueprint,
        MiniGameType.DesignPuzzle,
        MiniGameType.Inspection
    ];

    public event Action? TournamentUpdated;

    public bool IsPlayGamesSignedIn => _playGamesService?.IsSignedIn == true;

    public TournamentService(IGameStateService gameState, IPlayGamesService playGamesService)
    {
        _gameState = gameState;
        _playGamesService = playGamesService;
    }

    public bool CanEnter
    {
        get
        {
            var tournament = _gameState.State.CurrentTournament;
            if (tournament == null || tournament.IsExpired) return false;

            // 3 Gratis-Teilnahmen pro Tag
            if (tournament.FreeEntriesRemaining > 0) return true;

            // Danach für 5 Goldschrauben
            return _gameState.CanAffordGoldenScrews(5);
        }
    }

    public int EntryCost
    {
        get
        {
            var tournament = _gameState.State.CurrentTournament;
            if (tournament == null) return 0;

            return tournament.FreeEntriesRemaining > 0 ? 0 : 5;
        }
    }

    public void CheckAndStartNewTournament()
    {
        var state = _gameState.State;
        var currentMonday = GetCurrentMonday();

        // Kein Turnier vorhanden oder Turnierwoche vorbei → neues Turnier starten
        if (state.CurrentTournament == null || state.CurrentTournament.WeekStart < currentMonday)
        {
            // Zufälligen MiniGame-Typ auswählen
            var gameType = TournamentGameTypes[Random.Shared.Next(TournamentGameTypes.Length)];

            var tournament = new Tournament
            {
                WeekStart = currentMonday,
                GameType = gameType,
                // Initiales Leaderboard: simuliert als Fallback, wird bei LoadLeaderboardAsync überschrieben
                Leaderboard = Tournament.GenerateSimulatedOpponents(state.PlayerLevel),
                IsRealLeaderboard = false
            };

            state.CurrentTournament = tournament;
            _gameState.MarkDirty();
            TournamentUpdated?.Invoke();
        }
    }

    public void ResetDailyEntries()
    {
        var tournament = _gameState.State.CurrentTournament;
        if (tournament == null) return;

        // LastEntryDate-Check ist schon im Tournament-Model eingebaut (FreeEntriesRemaining),
        // aber wir setzen EntriesUsedToday explizit zurück wenn neuer Tag
        if (tournament.LastEntryDate.Date < DateTime.UtcNow.Date)
        {
            tournament.EntriesUsedToday = 0;
            _gameState.MarkDirty();
        }
    }

    public void RecordScore(int score)
    {
        var state = _gameState.State;
        var tournament = state.CurrentTournament;
        if (tournament == null || tournament.IsExpired) return;

        // Kosten abziehen wenn nötig
        int cost = EntryCost;
        if (cost > 0)
        {
            if (!_gameState.TrySpendGoldenScrews(cost))
                return;
        }

        // Score eintragen (aktualisiert Top-3, EntriesUsedToday, LastEntryDate)
        tournament.AddScore(score);

        // Spieler-Eintrag in der Bestenliste aktualisieren
        UpdatePlayerInLeaderboard(tournament);

        state.TotalTournamentsPlayed++;
        _gameState.MarkDirty();
        TournamentUpdated?.Invoke();

        // Scores an Play Games senden (fire-and-forget)
        if (_playGamesService?.IsSignedIn == true && tournament.TotalScore > 0)
        {
            _ = _playGamesService.SubmitScoreAsync(LeaderboardWeeklyScore, tournament.TotalScore);
            _ = _playGamesService.SubmitScoreAsync(LeaderboardHighScore, tournament.BestScores.Count > 0 ? tournament.BestScores[0] : 0);
            _ = _playGamesService.SubmitScoreAsync(LeaderboardTournamentWins, state.TotalTournamentsPlayed);
        }
    }

    public (TournamentRewardTier tier, int screws, decimal money)? ClaimRewards()
    {
        var tournament = _gameState.State.CurrentTournament;
        if (tournament == null || tournament.RewardsClaimed) return null;
        if (tournament.TotalScore == 0) return null; // Nie teilgenommen

        var rewardTier = tournament.GetRewardTier();
        if (rewardTier == TournamentRewardTier.None) return null;

        // Belohnungen berechnen basierend auf Rang
        // Gold (Top 3): 30 Schrauben + 100K Geld
        // Silver (Rang 4-7): 15 Schrauben + 50K Geld
        // Bronze (Rang 8-10): 5 Schrauben + 20K Geld
        var (screws, money) = rewardTier switch
        {
            TournamentRewardTier.Gold => (30, 100_000m),
            TournamentRewardTier.Silver => (15, 50_000m),
            TournamentRewardTier.Bronze => (5, 20_000m),
            _ => (0, 0m)
        };

        if (screws > 0)
            _gameState.AddGoldenScrews(screws);
        if (money > 0)
            _gameState.AddMoney(money);

        tournament.RewardsClaimed = true;
        _gameState.MarkDirty();
        TournamentUpdated?.Invoke();

        return (rewardTier, screws, money);
    }

    /// <summary>
    /// Lädt das echte Play Games Leaderboard oder verwendet simulierte Gegner als Fallback.
    /// </summary>
    public async Task LoadLeaderboardAsync()
    {
        var tournament = _gameState.State.CurrentTournament;
        if (tournament == null) return;

        if (_playGamesService?.IsSignedIn == true)
        {
            try
            {
                var entries = await _playGamesService.LoadLeaderboardScoresAsync(LeaderboardWeeklyScore, 10);

                if (entries.Count > 0)
                {
                    // Play Games Einträge in Turnier-Format konvertieren
                    var leaderboard = entries.Select(e => new TournamentLeaderboardEntry
                    {
                        Name = e.PlayerName,
                        Score = (int)e.Score,
                        IsPlayer = e.PlayerName == _playGamesService.PlayerDisplayName,
                        Rank = e.Rank
                    }).ToList();

                    // Spieler-Eintrag sicherstellen (falls eigener Score noch nicht im Leaderboard)
                    if (tournament.TotalScore > 0 && !leaderboard.Any(e => e.IsPlayer))
                    {
                        leaderboard.Add(new TournamentLeaderboardEntry
                        {
                            Name = _playGamesService.PlayerDisplayName ?? "Du",
                            Score = tournament.TotalScore,
                            IsPlayer = true
                        });
                    }

                    // Nach Score sortieren und Ränge zuweisen
                    var sorted = leaderboard.OrderByDescending(e => e.Score).ToList();
                    for (int i = 0; i < sorted.Count; i++)
                        sorted[i].Rank = i + 1;

                    tournament.Leaderboard = sorted;
                    tournament.IsRealLeaderboard = true;
                    _gameState.MarkDirty();
                    TournamentUpdated?.Invoke();
                    return;
                }
            }
            catch
            {
                // Bei Fehler: Fallback auf simulierte Gegner
            }
        }

        // Fallback: Wenn kein echtes Leaderboard geladen werden konnte und noch kein simuliertes existiert
        if (tournament.Leaderboard.Count == 0)
        {
            tournament.Leaderboard = Tournament.GenerateSimulatedOpponents(_gameState.State.PlayerLevel);
            tournament.IsRealLeaderboard = false;
            UpdatePlayerInLeaderboard(tournament);
            _gameState.MarkDirty();
            TournamentUpdated?.Invoke();
        }
    }

    /// <summary>
    /// Aktualisiert den Spieler-Eintrag in der Bestenliste und sortiert nach Score.
    /// </summary>
    private static void UpdatePlayerInLeaderboard(Tournament tournament)
    {
        // Spieler-Eintrag finden oder erstellen
        var playerEntry = tournament.Leaderboard.FirstOrDefault(e => e.IsPlayer);
        if (playerEntry == null)
        {
            playerEntry = new TournamentLeaderboardEntry
            {
                Name = "Du",
                IsPlayer = true
            };
            tournament.Leaderboard.Add(playerEntry);
        }

        playerEntry.Score = tournament.TotalScore;

        // Bestenliste nach Score sortieren und Ränge zuweisen
        var sorted = tournament.Leaderboard.OrderByDescending(e => e.Score).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }

        tournament.Leaderboard = sorted;
    }

    /// <summary>
    /// Gibt den Montag 00:00 UTC der aktuellen Woche zurück.
    /// </summary>
    private static DateTime GetCurrentMonday()
    {
        var today = DateTime.UtcNow.Date;
        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
        return today.AddDays(-diff);
    }
}
