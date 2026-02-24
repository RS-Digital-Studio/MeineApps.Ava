using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel für das wöchentliche MiniGame-Turnier.
/// Zeigt Bestenliste, verbleibende Zeit und Belohnungen.
/// </summary>
public partial class TournamentViewModel : ObservableObject
{
    private readonly IGameStateService _gameStateService;
    private readonly ITournamentService _tournamentService;
    private readonly ILocalizationService _localizationService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event für Alert-Dialoge. Parameter: Titel, Nachricht, Button-Text.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private ObservableCollection<TournamentLeaderboardEntry> _leaderboard = [];

    [ObservableProperty]
    private string _gameTypeName = "";

    [ObservableProperty]
    private string _timeRemaining = "";

    [ObservableProperty]
    private bool _isTournamentActive;

    [ObservableProperty]
    private bool _canEnter;

    [ObservableProperty]
    private string _entryCostDisplay = "";

    [ObservableProperty]
    private int _bestScore;

    [ObservableProperty]
    private string _rewardTierDisplay = "";

    [ObservableProperty]
    private bool _hasRewardsToClaim;

    /// <summary>
    /// Ob das Leaderboard gerade geladen wird.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingLeaderboard;

    /// <summary>
    /// Hinweis wenn nicht bei Play Games angemeldet.
    /// </summary>
    [ObservableProperty]
    private string _playGamesHint = "";

    /// <summary>
    /// Ob der Play Games Hinweis angezeigt werden soll.
    /// </summary>
    [ObservableProperty]
    private bool _showPlayGamesHint;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public TournamentViewModel(
        IGameStateService gameStateService,
        ITournamentService tournamentService,
        ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _tournamentService = tournamentService;
        _localizationService = localizationService;

        UpdateLocalizedTexts();
        RefreshTournament();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void EnterTournament()
    {
        if (!_tournamentService.CanEnter)
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("TournamentFull") ?? "Turnier",
                _localizationService.GetString("TournamentNoEntries") ?? "Keine Teilnahmen mehr verfügbar.",
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Navigation zum MiniGame wird vom MainViewModel gesteuert
        NavigationRequested?.Invoke("tournament_enter");
    }

    [RelayCommand]
    private void ClaimRewards()
    {
        var result = _tournamentService.ClaimRewards();
        if (result.HasValue)
        {
            var (tier, screws, money) = result.Value;
            string tierName = tier switch
            {
                TournamentRewardTier.Gold => "Gold",
                TournamentRewardTier.Silver => "Silber",
                TournamentRewardTier.Bronze => "Bronze",
                _ => ""
            };

            AlertRequested?.Invoke(
                _localizationService.GetString("TournamentReward") ?? "Turnier-Belohnung",
                $"{tierName}: {screws} GS",
                _localizationService.GetString("OK") ?? "OK");

            RefreshTournament();
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert alle Turnier-Daten aus dem State.
    /// </summary>
    public void RefreshTournament()
    {
        var state = _gameStateService.State;
        var tournament = state.CurrentTournament;

        IsTournamentActive = tournament != null && !tournament.IsExpired;

        // Play Games Hinweis
        ShowPlayGamesHint = !_tournamentService.IsPlayGamesSignedIn;
        PlayGamesHint = ShowPlayGamesHint
            ? _localizationService.GetString("TournamentSignInHint") ?? "Melde dich bei Play Games an für echte Gegner!"
            : "";

        if (tournament != null && IsTournamentActive)
        {
            // Bestenliste sortiert nach Score
            var sorted = tournament.Leaderboard
                .OrderByDescending(e => e.Score)
                .Select((e, i) => { e.Rank = i + 1; return e; })
                .ToList();
            Leaderboard = new ObservableCollection<TournamentLeaderboardEntry>(sorted);

            // Spieltyp-Name
            GameTypeName = GetMiniGameName(tournament.GameType);

            // Verbleibende Zeit
            var remaining = tournament.TimeRemaining;
            TimeRemaining = remaining.TotalHours >= 24
                ? $"{(int)remaining.TotalDays}d {remaining.Hours}h"
                : $"{(int)remaining.TotalHours}h {remaining.Minutes}m";

            // Teilnahme-Status
            CanEnter = _tournamentService.CanEnter;
            int cost = _tournamentService.EntryCost;
            EntryCostDisplay = cost == 0
                ? _localizationService.GetString("Free") ?? "Gratis"
                : $"{cost} GS";

            // Bester Score
            BestScore = tournament.BestScores.Count > 0 ? tournament.BestScores[0] : 0;

            // Belohnungsstufe
            var rewardTier = tournament.GetRewardTier();
            RewardTierDisplay = rewardTier switch
            {
                TournamentRewardTier.Gold => "Gold",
                TournamentRewardTier.Silver => "Silber",
                TournamentRewardTier.Bronze => "Bronze",
                _ => "-"
            };

            HasRewardsToClaim = tournament.IsExpired && !tournament.RewardsClaimed
                                && rewardTier != TournamentRewardTier.None;
        }
        else
        {
            Leaderboard.Clear();
            GameTypeName = "-";
            TimeRemaining = "-";
            CanEnter = false;
            EntryCostDisplay = "";
            BestScore = 0;
            RewardTierDisplay = "-";
            HasRewardsToClaim = false;
        }

        // Echtes Leaderboard async laden
        _ = LoadLeaderboardAsync();
    }

    /// <summary>
    /// Lädt das Leaderboard async (Play Games oder Fallback).
    /// </summary>
    private async Task LoadLeaderboardAsync()
    {
        if (IsLoadingLeaderboard) return;

        IsLoadingLeaderboard = true;
        try
        {
            await _tournamentService.LoadLeaderboardAsync();

            // Nach dem Laden die Anzeige aktualisieren (ohne erneut LoadLeaderboard aufzurufen)
            var tournament = _gameStateService.State.CurrentTournament;
            if (tournament != null && IsTournamentActive)
            {
                var sorted = tournament.Leaderboard
                    .OrderByDescending(e => e.Score)
                    .Select((e, i) => { e.Rank = i + 1; return e; })
                    .ToList();
                Leaderboard = new ObservableCollection<TournamentLeaderboardEntry>(sorted);
            }
        }
        catch
        {
            // Stilles Fallback - simulierte Daten bleiben bestehen
        }
        finally
        {
            IsLoadingLeaderboard = false;
        }
    }

    /// <summary>
    /// Lokalisierte Texte aktualisieren.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("Tournament") ?? "Turnier";
        RefreshTournament();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private string GetMiniGameName(MiniGameType type) => type switch
    {
        MiniGameType.Sawing => _localizationService.GetString("MiniGameSawing") ?? "Sägen",
        MiniGameType.PipePuzzle => _localizationService.GetString("MiniGamePipePuzzle") ?? "Rohr-Puzzle",
        MiniGameType.WiringGame => _localizationService.GetString("MiniGameWiring") ?? "Verkabelung",
        MiniGameType.PaintingGame => _localizationService.GetString("MiniGamePainting") ?? "Streichen",
        MiniGameType.RoofTiling => _localizationService.GetString("MiniGameRoofTiling") ?? "Dachdecken",
        MiniGameType.Blueprint => _localizationService.GetString("MiniGameBlueprint") ?? "Bauplan",
        MiniGameType.DesignPuzzle => _localizationService.GetString("MiniGameDesignPuzzle") ?? "Grundriss",
        MiniGameType.Inspection => _localizationService.GetString("MiniGameInspection") ?? "Inspektion",
        _ => type.ToString()
    };
}
