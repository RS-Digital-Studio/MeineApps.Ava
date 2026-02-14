using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel for the main menu page.
/// Provides navigation commands for Story, Arcade, Quick Play, and other menu options.
/// </summary>
public partial class MainMenuViewModel : ObservableObject, IDisposable
{
    private readonly IProgressService _progressService;
    private readonly IPurchaseService _purchaseService;
    private readonly ICoinService _coinService;
    private readonly ILocalizationService _localizationService;
    private readonly IDailyRewardService _dailyRewardService;
    private readonly IReviewService _reviewService;

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

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether the player has progress to continue (alias for ShowContinueButton).
    /// </summary>
    public bool HasProgress => ShowContinueButton;

    public MainMenuViewModel(IProgressService progressService, IPurchaseService purchaseService, ICoinService coinService,
        ILocalizationService localizationService, IDailyRewardService dailyRewardService, IReviewService reviewService)
    {
        _progressService = progressService;
        _purchaseService = purchaseService;
        _coinService = coinService;
        _localizationService = localizationService;
        _dailyRewardService = dailyRewardService;
        _reviewService = reviewService;

        // Live-Update bei Coin-Änderungen (z.B. aus Shop, Rewarded Ads)
        _coinService.BalanceChanged += OnBalanceChanged;

        // Set version from assembly
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        VersionText = version != null
            ? $"v{version.Major}.{version.Minor}.{version.Build} - RS-Digital"
            : "v1.0.0 - RS-Digital";
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

        // 7-Tage Daily Reward prüfen und automatisch vergeben
        if (_dailyRewardService.IsRewardAvailable)
        {
            var reward = _dailyRewardService.ClaimReward();
            if (reward != null)
            {
                _coinService.AddCoins(reward.Coins);

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
        OnPropertyChanged(nameof(HasProgress));
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
    private void ArcadeMode()
    {
        NavigationRequested?.Invoke("Game?mode=arcade");
    }

    [RelayCommand]
    private void QuickPlay()
    {
        NavigationRequested?.Invoke("Game?mode=quick");
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

    // ═══════════════════════════════════════════════════════════════════════
    // BALANCE CHANGED
    // ═══════════════════════════════════════════════════════════════════════

    private void OnBalanceChanged(object? sender, EventArgs e)
    {
        CoinBalance = _coinService.Balance;
        CoinsText = _coinService.Balance.ToString("N0");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        _coinService.BalanceChanged -= OnBalanceChanged;
    }
}
