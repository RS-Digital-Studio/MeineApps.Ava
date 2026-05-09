using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using BomberBlast.Models.League;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für das Liga-System mit Firebase Online-Rangliste.
/// Zeigt echte Spieler + NPC-Backfill, Saison-Countdown, Belohnungen.
/// </summary>
public sealed partial class LeagueViewModel : ViewModelBase, INavigable, IGameJuiceEmitter
{
    private readonly ILeagueService _leagueService;
    private readonly ILocalizationService _localization;
    private readonly IBattlePassService _battlePassService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<NavigationRequest>? NavigationRequested;
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
    private string _seasonDisplayText = "";

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

    [ObservableProperty]
    private string _claimRewardText = "";

    [ObservableProperty]
    private string _rewardClaimedText = "";

    [ObservableProperty]
    private string _playerHeaderText = "";

    [ObservableProperty]
    private string _pointsHeaderText = "";

    [ObservableProperty]
    private string _loadingLeaderboardText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY RACE (v2.0.42, Plan Task 3.1)
    // ═══════════════════════════════════════════════════════════════════════
    // Tab-Index: 0 = Saison-Liga, 1 = Daily Race.
    // SwitchTab triggert Lazy-Load des Daily-Race-Leaderboards aus Firebase.

    /// <summary>Aktiver Tab im Liga-Header (0=Saison, 1=Daily Race).</summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isSeasonTabActive = true;

    [ObservableProperty]
    private bool _isDailyRaceTabActive;

    [ObservableProperty]
    private string _seasonTabText = "";

    [ObservableProperty]
    private string _dailyRaceTabText = "";

    [ObservableProperty]
    private string _dailyRaceTitleText = "";

    [ObservableProperty]
    private string _dailyRaceDescText = "";

    [ObservableProperty]
    private string _dailyRaceTodayBestText = "";

    [ObservableProperty]
    private string _dailyRaceStartButtonText = "";

    [ObservableProperty]
    private string _dailyRaceCountdownText = "";

    [ObservableProperty]
    private bool _isDailyRaceLeaderboardLoading;

    /// <summary>True = Cross-Tier (alle Spieler weltweit), false = Eigener Tier.</summary>
    [ObservableProperty]
    private bool _isDailyRaceGlobalView;

    [ObservableProperty]
    private string _dailyRaceTierTabText = "";

    [ObservableProperty]
    private string _dailyRaceGlobalTabText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // COLLECTIONS
    // ═══════════════════════════════════════════════════════════════════════

    public ObservableCollection<LeagueDisplayEntry> LeaderboardEntries { get; } = [];

    /// <summary>Daily-Race-Rangliste (heute, alle Tier des Spielers).</summary>
    public ObservableCollection<LeagueDisplayEntry> DailyRaceLeaderboardEntries { get; } = [];

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
        Title = _localization.GetString("LeagueTitle") ?? "League";
        PlayerName = _leagueService.PlayerName;
        ClaimRewardText = _localization.GetString("LeagueClaimReward") ?? "Claim";
        RewardClaimedText = _localization.GetString("LeagueRewardClaimed") ?? "Claimed";
        PlayerHeaderText = _localization.GetString("LeaguePlayer") ?? "Player";
        PointsHeaderText = _localization.GetString("LeaguePointsHeader") ?? "Points";
        LoadingLeaderboardText = _localization.GetString("LeagueLoading") ?? "Loading...";

        // Tab-Texte
        SeasonTabText = _localization.GetString("LeagueTabSeason") ?? "Season";
        DailyRaceTabText = _localization.GetString("LeagueTabDailyRace") ?? "Daily Race";
        DailyRaceTierTabText = _localization.GetString("DailyRaceTierTab") ?? "My Tier";
        DailyRaceGlobalTabText = _localization.GetString("DailyRaceGlobalTab") ?? "Global";

        // Daily-Race-Texte
        DailyRaceTitleText = _localization.GetString("DailyRaceTitle") ?? "Daily Bomb Race";
        DailyRaceDescText = _localization.GetString("DailyRaceDesc") ?? "Same level for everyone. Top score wins.";
        UpdateDailyRaceLocalizedFigures();

        LoadLeaderboard();
        UpdateTierInfo();
        UpdateCountdown();
        UpdateRewardState();
        UpdateStats();
    }

    /// <summary>Aktualisiert die Daily-Race-Texte mit den aktuellen Service-Werten (Today-Best, Start-Button, Countdown).</summary>
    private void UpdateDailyRaceLocalizedFigures()
    {
        var bestFmt = _localization.GetString("DailyRaceTodayBest") ?? "Today's best: {0}";
        DailyRaceTodayBestText = string.Format(bestFmt, _leagueService.TodayDailyRaceBestScore.ToString("N0"));
        DailyRaceStartButtonText = _localization.GetString("DailyRaceStartButton") ?? "Start Race";

        // Countdown bis Mitternacht UTC (naechster Daily-Race-Reset)
        var now = DateTime.UtcNow;
        var nextMidnight = now.Date.AddDays(1);
        var until = nextMidnight - now;
        var fmt = _localization.GetString("DailyRaceResetIn") ?? "Resets in {0}h {1}m";
        DailyRaceCountdownText = string.Format(fmt, until.Hours, until.Minutes);
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
        var seasonLabel = _localization.GetString("LeagueSeason") ?? "Season";
        SeasonDisplayText = $"{seasonLabel} {SeasonNumber}";

        var pointsLabel = _localization.GetString("LeaguePoints") ?? "Points";
        PointsText = $"{CurrentPoints} {pointsLabel}";

        PlayerRank = _leagueService.GetPlayerRank();
        var rankLabel = _localization.GetString("LeagueRank") ?? "Rank";
        RankText = $"{rankLabel} #{PlayerRank}";

        // Nächste Liga anzeigen
        if (tier < LeagueTier.Diamond)
        {
            var nextTier = tier + 1;
            var nextName = _localization.GetString(nextTier.GetNameKey()) ?? nextTier.ToString();
            var nextLabel = _localization.GetString("LeagueNextTier") ?? "Next League";
            NextTierText = $"{nextLabel}: {nextName}";
        }
        else
        {
            NextTierText = _localization.GetString("LeagueMaxTier") ?? "Highest League!";
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
            SeasonCountdown = _localization.GetString("LeagueSeasonEnded") ?? "Season ended!";
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
                Uid = entry.Uid,
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

    /// <summary>
    /// Meldet einen Leaderboard-Eintrag wegen anstössigem Namen / Cheating.
    /// Zeigt einen Bestätigungs-Dialog (aktuell: vereinfachter FloatingText-Flow, kein Multi-Choice).
    /// Bei Erfolg wird Firebase-Node <c>reports/{uid}/{reporterUid}</c> geschrieben.
    /// </summary>
    [RelayCommand]
    private async Task ReportPlayerAsync(LeagueDisplayEntry entry)
    {
        if (entry == null || !entry.CanReport) return;

        // Report-Reason: Aktuell "offensive_name" als Default (UI-Expansion mit Multi-Choice-Dialog
        // als Follow-up geplant — z.B. Action-Sheet mit 3 Optionen).
        var success = await _leagueService.ReportPlayerAsync(entry.Uid, "offensive_name");

        var msgKey = success ? "ReportSubmitted" : "ReportFailed";
        var msg = _localization.GetString(msgKey) ?? (success ? "Report submitted" : "Report failed");
        FloatingTextRequested?.Invoke(msg, success ? "success" : "error");
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
        var seasonsLabel = _localization.GetString("LeagueStatSeasons") ?? "Seasons";
        var promotionsLabel = _localization.GetString("LeagueStatPromotions") ?? "Promotions";
        var bestLabel = _localization.GetString("LeagueStatBest") ?? "Best Score";

        StatsText = $"{seasonsLabel}: {stats.TotalSeasons}  |  {promotionsLabel}: {stats.TotalPromotions}  |  {bestLabel}: {stats.BestSeasonPoints}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void GoBack() => NavigationRequested?.Invoke(new GoBack());

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

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY RACE COMMANDS (v2.0.42, Plan Task 3.1)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Wechselt auf den Saison-Liga-Tab.</summary>
    [RelayCommand]
    private void SelectSeasonTab()
    {
        SelectedTabIndex = 0;
        IsSeasonTabActive = true;
        IsDailyRaceTabActive = false;
    }

    /// <summary>
    /// Wechselt auf den Daily-Race-Tab und triggert Lazy-Load des Daily-Race-Leaderboards.
    /// Erfordert eine bestehende Auth-Session (laeuft im Hintergrund weiter wenn offline).
    /// </summary>
    [RelayCommand]
    private async Task SelectDailyRaceTab()
    {
        SelectedTabIndex = 1;
        IsSeasonTabActive = false;
        IsDailyRaceTabActive = true;
        UpdateDailyRaceLocalizedFigures();
        await LoadDailyRaceLeaderboardAsync();
    }

    /// <summary>Startet das Daily Race-Spiel (NavigationRequest mit Mode "dailyrace").</summary>
    [RelayCommand]
    private void StartDailyRace()
    {
        NavigationRequested?.Invoke(new GoGame(Mode: "dailyrace", Level: 1, Floor: 0));
    }

    /// <summary>Lazy-Load des Daily-Race-Leaderboards aus Firebase. Setzt _isDailyRaceLeaderboardLoading wahrend des Calls.</summary>
    private async Task LoadDailyRaceLeaderboardAsync()
    {
        IsDailyRaceLeaderboardLoading = true;
        try
        {
            // Cross-Tier (Global) oder eigener Tier abhaengig vom Toggle
            var entries = IsDailyRaceGlobalView
                ? await _leagueService.GetDailyRaceGlobalLeaderboardAsync()
                : await _leagueService.GetDailyRaceLeaderboardAsync();
            DailyRaceLeaderboardEntries.Clear();
            foreach (var entry in entries)
            {
                DailyRaceLeaderboardEntries.Add(new LeagueDisplayEntry
                {
                    Uid = entry.Uid,
                    Rank = entry.Rank,
                    Name = entry.Name,
                    Points = entry.Points,
                    IsPlayer = entry.IsPlayer,
                    IsRealPlayer = entry.IsRealPlayer,
                    RankText = $"#{entry.Rank}",
                    PointsText = $"{entry.Points:N0}",
                    NameColor = entry.IsPlayer ? "#FFD700" : entry.IsRealPlayer ? "#00CED1" : "#FFFFFF",
                    BackgroundOpacity = entry.IsPlayer ? 0.2 : entry.IsRealPlayer ? 0.1 : 0.05
                });
            }
        }
        finally
        {
            IsDailyRaceLeaderboardLoading = false;
        }
    }

    /// <summary>Wechselt zwischen My-Tier-Ansicht und Global-Cross-Tier-Ansicht im Daily-Race-Leaderboard.</summary>
    [RelayCommand]
    private async Task ToggleDailyRaceScope()
    {
        IsDailyRaceGlobalView = !IsDailyRaceGlobalView;
        await LoadDailyRaceLeaderboardAsync();
    }

    /// <summary>Manuelles Refresh des Daily-Race-Leaderboards (Refresh-Button im Daily-Race-Tab).</summary>
    [RelayCommand]
    private async Task RefreshDailyRaceLeaderboard()
    {
        UpdateDailyRaceLocalizedFigures();
        await LoadDailyRaceLeaderboardAsync();
    }
}

/// <summary>Display-Item für einen Leaderboard-Eintrag.</summary>
public class LeagueDisplayEntry
{
    /// <summary>Firebase-UID (leer bei NPCs). Für Report-Funktion.</summary>
    public string Uid { get; set; } = "";

    public int Rank { get; set; }
    public string Name { get; set; } = "";
    public int Points { get; set; }
    public bool IsPlayer { get; set; }
    public bool IsRealPlayer { get; set; }
    public string RankText { get; set; } = "";
    public string PointsText { get; set; } = "";
    public string NameColor { get; set; } = "#FFFFFF";
    public double BackgroundOpacity { get; set; } = 0.05;

    /// <summary>Ob der Report-Button angezeigt werden soll (echte Spieler ausser man selbst).</summary>
    public bool CanReport => IsRealPlayer && !IsPlayer && !string.IsNullOrEmpty(Uid);
}
