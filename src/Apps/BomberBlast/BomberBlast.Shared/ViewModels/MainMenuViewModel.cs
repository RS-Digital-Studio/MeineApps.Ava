using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für das Hauptmenü.
/// Navigation zu Spielmodi, Meta-Features und Utility-Seiten.
/// </summary>
public partial class MainMenuViewModel : ObservableObject, IDisposable
{
    private readonly IProgressService _progressService;
    private readonly IPurchaseService _purchaseService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly ILocalizationService _localizationService;
    private readonly IDailyRewardService _dailyRewardService;
    private readonly IReviewService _reviewService;
    private readonly IDailyChallengeService _dailyChallengeService;
    private readonly IWeeklyChallengeService _weeklyChallengeService;
    private readonly IDailyMissionService _dailyMissionService;
    private readonly IBattlePassService _battlePassService;
    private readonly ILeagueService _leagueService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Event to request navigation. Parameter is the route string.
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>Floating-Text anzeigen (z.B. Daily Bonus)</summary>
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>Celebration-Effekt (Confetti)</summary>
    public event Action? CelebrationRequested;

    /// <summary>In-App Review anfordern (Android: ReviewManagerFactory)</summary>
    public event Action? ReviewRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _showContinueButton;

    [ObservableProperty]
    private string _versionText = "v1.0.0 - RS-Digital";

    [ObservableProperty]
    private string _coinsText = "0";

    [ObservableProperty]
    private int _coinBalance;

    [ObservableProperty]
    private string _totalEarnedText = "";

    [ObservableProperty]
    private string _gemsText = "0";

    /// <summary>Ob die heutige Daily Challenge noch nicht gespielt wurde</summary>
    [ObservableProperty]
    private bool _isDailyChallengeNew;

    /// <summary>Ob es offene Missionen gibt (tägliche oder wöchentliche)</summary>
    [ObservableProperty]
    private bool _hasNewMissions;

    // Daily Reward Popup
    [ObservableProperty]
    private bool _isRewardPopupVisible;

    [ObservableProperty]
    private string _rewardPopupTitle = "";

    [ObservableProperty]
    private string _rewardClaimText = "";

    public ObservableCollection<DailyRewardDisplayItem> RewardDays { get; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether the player has progress to continue (alias for ShowContinueButton).
    /// </summary>
    public bool HasProgress => ShowContinueButton;

    public MainMenuViewModel(IProgressService progressService, IPurchaseService purchaseService, ICoinService coinService,
        IGemService gemService, ILocalizationService localizationService, IDailyRewardService dailyRewardService,
        IReviewService reviewService, IDailyChallengeService dailyChallengeService,
        IWeeklyChallengeService weeklyChallengeService, IDailyMissionService dailyMissionService,
        IBattlePassService battlePassService, ILeagueService leagueService)
    {
        _progressService = progressService;
        _purchaseService = purchaseService;
        _coinService = coinService;
        _gemService = gemService;
        _localizationService = localizationService;
        _dailyRewardService = dailyRewardService;
        _reviewService = reviewService;
        _dailyChallengeService = dailyChallengeService;
        _weeklyChallengeService = weeklyChallengeService;
        _dailyMissionService = dailyMissionService;
        _battlePassService = battlePassService;
        _leagueService = leagueService;

        // Live-Update bei Coin-/Gem-Änderungen (z.B. aus Shop, Rewarded Ads)
        _coinService.BalanceChanged += OnBalanceChanged;
        _gemService.BalanceChanged += OnBalanceChanged;

        // Version aus dem eigenen Assembly lesen (GetEntryAssembly() gibt null auf Android)
        var assembly = typeof(MainMenuViewModel).Assembly;
        var infoVersion = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (infoVersion != null)
        {
            // InformationalVersion kann "+commitHash" enthalten → nur den Teil vor '+' nehmen
            var plusIndex = infoVersion.IndexOf('+');
            if (plusIndex > 0) infoVersion = infoVersion[..plusIndex];
            VersionText = $"v{infoVersion} - RS-Digital";
        }
        else
        {
            var version = assembly.GetName().Version;
            VersionText = version != null
                ? $"v{version.Major}.{version.Minor}.{version.Build} - RS-Digital"
                : "v2.0.7 - RS-Digital";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when the view appears. Refreshes continue button visibility.
    /// Prüft und vergibt täglichen Bonus.
    /// </summary>
    public void OnAppearing()
    {
        ShowContinueButton = _progressService.HighestCompletedLevel > 0;

        // 7-Tage Daily Reward: Popup anzeigen statt auto-claim
        if (_dailyRewardService.IsRewardAvailable)
        {
            ShowRewardPopup();
        }

        // In-App Review prüfen
        if (_reviewService.ShouldPromptReview())
        {
            _reviewService.MarkReviewPrompted();
            // ReviewRequested Event wird in MainViewModel behandelt (Android: ReviewManagerFactory)
            ReviewRequested?.Invoke();
        }

        CoinBalance = _coinService.Balance;
        CoinsText = _coinService.Balance.ToString("N0");
        GemsText = _gemService.Balance.ToString("N0");
        TotalEarnedText = string.Format(
            _localizationService.GetString("TotalEarned") ?? "Total: {0}",
            _coinService.TotalEarned.ToString("N0"));
        IsDailyChallengeNew = !_dailyChallengeService.IsCompletedToday;
        HasNewMissions = !_weeklyChallengeService.IsAllComplete || !_dailyMissionService.IsAllComplete;
        OnPropertyChanged(nameof(HasProgress));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DAILY REWARD POPUP
    // ═══════════════════════════════════════════════════════════════════════

    private void ShowRewardPopup()
    {
        RewardPopupTitle = _localizationService.GetString("DailyRewardTitle") ?? "Daily Bonus";
        RewardClaimText = _localizationService.GetString("DailyRewardCollect") ?? "Collect";

        RewardDays.Clear();
        var rewards = _dailyRewardService.GetRewards();
        foreach (var r in rewards)
        {
            RewardDays.Add(new DailyRewardDisplayItem
            {
                Day = r.Day,
                DayText = $"Day {r.Day}",
                CoinsText = $"+{r.Coins:N0}",
                HasExtraLife = r.ExtraLives > 0,
                IsClaimed = r.IsClaimed,
                IsCurrentDay = r.IsCurrentDay,
                IsFuture = !r.IsClaimed && !r.IsCurrentDay
            });
        }

        IsRewardPopupVisible = true;
    }

    [RelayCommand]
    private void ClaimDailyReward()
    {
        var reward = _dailyRewardService.ClaimReward();
        if (reward != null)
        {
            _coinService.AddCoins(reward.Coins);

            // Battle Pass XP + Liga-Punkte für täglichen Login
            _battlePassService.AddXp(BattlePassXpSources.DailyLogin, "daily_login");
            _leagueService.AddPoints(5);

            var dayText = string.Format(
                _localizationService.GetString("DailyRewardDay") ?? "Day {0}",
                reward.Day);
            var bonusText = $"{dayText}: +{reward.Coins:N0} Coins!";

            if (reward.ExtraLives > 0)
            {
                bonusText += $" +{reward.ExtraLives} " +
                    (_localizationService.GetString("DailyRewardExtraLife") ?? "Extra Life");
            }

            FloatingTextRequested?.Invoke(bonusText, "gold");
            CelebrationRequested?.Invoke();
        }

        IsRewardPopupVisible = false;

        // Coin-Anzeige aktualisieren
        CoinBalance = _coinService.Balance;
        CoinsText = _coinService.Balance.ToString("N0");
    }

    [RelayCommand]
    private void DismissRewardPopup()
    {
        // Popup schließen OHNE zu claimen (naechster Besuch zeigt es erneut)
        IsRewardPopupVisible = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StoryMode()
    {
        NavigationRequested?.Invoke("LevelSelect");
    }

    [RelayCommand]
    private void Continue()
    {
        int nextLevel = Math.Min(
            _progressService.HighestCompletedLevel + 1,
            _progressService.TotalLevels);
        NavigationRequested?.Invoke($"Game?mode=story&level={nextLevel}");
    }

    [RelayCommand]
    private void QuickPlay()
    {
        NavigationRequested?.Invoke("QuickPlay");
    }

    [RelayCommand]
    private void SurvivalMode()
    {
        NavigationRequested?.Invoke("Game?mode=survival");
    }

    [RelayCommand]
    private void HighScores()
    {
        NavigationRequested?.Invoke("HighScores");
    }

    [RelayCommand]
    private void Help()
    {
        NavigationRequested?.Invoke("Help");
    }

    [RelayCommand]
    private void Settings()
    {
        NavigationRequested?.Invoke("Settings");
    }

    [RelayCommand]
    private void Shop()
    {
        NavigationRequested?.Invoke("Shop");
    }

    [RelayCommand]
    private void Achievements()
    {
        NavigationRequested?.Invoke("Achievements");
    }

    [RelayCommand]
    private void DailyChallenge()
    {
        NavigationRequested?.Invoke("DailyChallenge");
    }

    [RelayCommand]
    private void LuckyWheel()
    {
        NavigationRequested?.Invoke("LuckySpin");
    }

    [RelayCommand]
    private void WeeklyChallenge()
    {
        NavigationRequested?.Invoke("WeeklyChallenge");
    }

    [RelayCommand]
    private void Statistics()
    {
        NavigationRequested?.Invoke("Statistics");
    }

    [RelayCommand]
    private void Deck()
    {
        NavigationRequested?.Invoke("Deck");
    }

    [RelayCommand]
    private void Dungeon() => NavigationRequested?.Invoke("Dungeon");

    [RelayCommand]
    private void BattlePass() => NavigationRequested?.Invoke("BattlePass");

    [RelayCommand]
    private void Collection() => NavigationRequested?.Invoke("Collection");

    [RelayCommand]
    private void League() => NavigationRequested?.Invoke("League");

    // ═══════════════════════════════════════════════════════════════════════
    // BALANCE CHANGED
    // ═══════════════════════════════════════════════════════════════════════

    private void OnBalanceChanged(object? sender, EventArgs e)
    {
        CoinBalance = _coinService.Balance;
        CoinsText = _coinService.Balance.ToString("N0");
        GemsText = _gemService.Balance.ToString("N0");
        TotalEarnedText = string.Format(
            _localizationService.GetString("TotalEarned") ?? "Total: {0}",
            _coinService.TotalEarned.ToString("N0"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        _coinService.BalanceChanged -= OnBalanceChanged;
        _gemService.BalanceChanged -= OnBalanceChanged;
    }
}
