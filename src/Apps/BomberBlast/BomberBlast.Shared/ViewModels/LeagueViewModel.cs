using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models.League;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für das Liga-System mit Firebase Online-Rangliste.
/// Zeigt echte Spieler + NPC-Backfill, Saison-Countdown, Belohnungen.
/// </summary>
public partial class LeagueViewModel : ObservableObject
{
    private readonly ILeagueService _leagueService;
    private readonly ILocalizationService _localization;
    private readonly IBattlePassService _battlePassService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _tierName = "";

    [ObservableProperty]
    private string _tierIconName = "ShieldOutline";

    [ObservableProperty]
    private Color _tierColor = Colors.White;

    [ObservableProperty]
    private int _currentPoints;

    [ObservableProperty]
    private int _playerRank;

    [ObservableProperty]
    private string _seasonCountdown = "";

    [ObservableProperty]
    private int _seasonNumber;

    [ObservableProperty]
    private string _pointsText = "";

    [ObservableProperty]
    private string _rankText = "";

    [ObservableProperty]
    private string _nextTierText = "";

    [ObservableProperty]
    private string _rewardText = "";

    [ObservableProperty]
    private bool _canClaimReward;

    [ObservableProperty]
    private bool _isRewardClaimed;

    [ObservableProperty]
    private string _statsText = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private string _playerName = "";

    // ═══════════════════════════════════════════════════════════════════════
    // COLLECTIONS
    // ═══════════════════════════════════════════════════════════════════════

    public ObservableCollection<LeagueDisplayEntry> LeaderboardEntries { get; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public LeagueViewModel(ILeagueService leagueService, ILocalizationService localization,
        IBattlePassService battlePassService)
    {
        _leagueService = leagueService;
        _localization = localization;
        _battlePassService = battlePassService;

        // Leaderboard-Updates aus Firebase empfangen
        _leagueService.LeaderboardUpdated += (_, _) => LoadLeaderboard();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        // Saison-Ende prüfen
        _leagueService.CheckAndProcessSeasonEnd();

        UpdateLocalizedTexts();
        LoadLeaderboard();
        UpdateTierInfo();
        UpdateCountdown();
        UpdateRewardState();
        UpdateStats();
        UpdateOnlineStatus();

        // Firebase-Daten im Hintergrund laden
        _ = RefreshFromFirebaseAsync();
    }

    public void UpdateLocalizedTexts()
    {
        Title = _localization.GetString("LeagueTitle") ?? "Liga";
        PlayerName = _leagueService.PlayerName;
        LoadLeaderboard();
        UpdateTierInfo();
        UpdateCountdown();
        UpdateRewardState();
        UpdateStats();
    }

    /// <summary>Firebase-Daten laden und UI aktualisieren.</summary>
    private async Task RefreshFromFirebaseAsync()
    {
        IsLoading = true;
        try
        {
            await _leagueService.InitializeOnlineAsync();
            UpdateOnlineStatus();
            LoadLeaderboard();
            UpdateTierInfo();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateOnlineStatus()
    {
        IsOnline = _leagueService.IsOnline;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TIER-INFO
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateTierInfo()
    {
        var tier = _leagueService.CurrentTier;
        TierName = _localization.GetString(tier.GetNameKey()) ?? tier.ToString();
        TierIconName = tier.GetIconName();
        TierColor = Color.Parse(tier.GetColor());
        CurrentPoints = _leagueService.CurrentPoints;
        SeasonNumber = _leagueService.SeasonNumber;

        var pointsLabel = _localization.GetString("LeaguePoints") ?? "Punkte";
        PointsText = $"{CurrentPoints} {pointsLabel}";

        PlayerRank = _leagueService.GetPlayerRank();
        var rankLabel = _localization.GetString("LeagueRank") ?? "Rang";
        RankText = $"{rankLabel} #{PlayerRank}";

        // Nächste Liga anzeigen
        if (tier < LeagueTier.Diamond)
        {
            var nextTier = tier + 1;
            var nextName = _localization.GetString(nextTier.GetNameKey()) ?? nextTier.ToString();
            var nextLabel = _localization.GetString("LeagueNextTier") ?? "Nächste Liga";
            NextTierText = $"{nextLabel}: {nextName}";
        }
        else
        {
            NextTierText = _localization.GetString("LeagueMaxTier") ?? "Höchste Liga!";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COUNTDOWN
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateCountdown()
    {
        var remaining = _leagueService.GetSeasonTimeRemaining();
        if (remaining <= TimeSpan.Zero)
        {
            SeasonCountdown = _localization.GetString("LeagueSeasonEnded") ?? "Saison beendet!";
            return;
        }

        string days = remaining.Days > 0 ? $"{remaining.Days}d " : "";
        SeasonCountdown = $"{days}{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RANGLISTE
    // ═══════════════════════════════════════════════════════════════════════

    private void LoadLeaderboard()
    {
        LeaderboardEntries.Clear();
        var entries = _leagueService.GetLeaderboard();

        foreach (var entry in entries)
        {
            LeaderboardEntries.Add(new LeagueDisplayEntry
            {
                Rank = entry.Rank,
                Name = entry.Name,
                Points = entry.Points,
                IsPlayer = entry.IsPlayer,
                IsRealPlayer = entry.IsRealPlayer,
                RankText = $"#{entry.Rank}",
                PointsText = $"{entry.Points}",
                NameColor = entry.IsPlayer ? "#FFD700" : entry.IsRealPlayer ? "#00CED1" : "#FFFFFF",
                BackgroundOpacity = entry.IsPlayer ? 0.2 : entry.IsRealPlayer ? 0.1 : 0.05
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BELOHNUNGEN
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateRewardState()
    {
        var (coins, gems) = _leagueService.CurrentTier.GetSeasonReward();
        var coinsLabel = _localization.GetString("LeagueRewardCoins") ?? "Coins";
        var gemsLabel = _localization.GetString("LeagueRewardGems") ?? "Gems";
        RewardText = $"{coins:N0} {coinsLabel} + {gems} {gemsLabel}";

        IsRewardClaimed = _leagueService.IsSeasonRewardClaimed;
        CanClaimReward = !IsRewardClaimed && _leagueService.GetSeasonTimeRemaining() <= TimeSpan.Zero;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STATS
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateStats()
    {
        var stats = _leagueService.GetStats();
        var seasonsLabel = _localization.GetString("LeagueStatSeasons") ?? "Saisons";
        var promotionsLabel = _localization.GetString("LeagueStatPromotions") ?? "Aufstiege";
        var bestLabel = _localization.GetString("LeagueStatBest") ?? "Bestpunkte";

        StatsText = $"{seasonsLabel}: {stats.TotalSeasons}  |  {promotionsLabel}: {stats.TotalPromotions}  |  {bestLabel}: {stats.BestSeasonPoints}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke("..");

    [RelayCommand]
    private void ClaimReward()
    {
        if (_leagueService.ClaimSeasonReward())
        {
            // Battle Pass XP für Liga-Saison-Belohnung
            _battlePassService.AddXp(BattlePassXpSources.LeagueSeasonReward, "league_season_reward");

            var (coins, gems) = _leagueService.CurrentTier.GetSeasonReward();
            FloatingTextRequested?.Invoke($"+{coins:N0} Coins + {gems} Gems", "gold");
            CelebrationRequested?.Invoke();
            UpdateRewardState();
        }
    }

    [RelayCommand]
    private async Task RefreshLeaderboard()
    {
        IsLoading = true;
        try
        {
            await _leagueService.RefreshLeaderboardAsync();
            UpdateOnlineStatus();
            LoadLeaderboard();
            UpdateTierInfo();
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>Display-Item für einen Leaderboard-Eintrag.</summary>
public class LeagueDisplayEntry
{
    public int Rank { get; set; }
    public string Name { get; set; } = "";
    public int Points { get; set; }
    public bool IsPlayer { get; set; }
    public bool IsRealPlayer { get; set; }
    public string RankText { get; set; } = "";
    public string PointsText { get; set; } = "";
    public string NameColor { get; set; } = "#FFFFFF";
    public double BackgroundOpacity { get; set; } = 0.05;
}
