using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für die Daily-Challenge-Übersicht.
/// Zeigt Streak, Best-Score und ermöglicht Start der heutigen Challenge.
/// </summary>
public partial class DailyChallengeViewModel : ObservableObject, INavigable, IGameJuiceEmitter
{
    private readonly IDailyChallengeService _dailyChallengeService;
    private readonly ICoinService _coinService;
    private readonly ILocalizationService _localizationService;
    private readonly IAchievementService _achievementService;

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    [ObservableProperty]
    private bool _isCompletedToday;

    [ObservableProperty]
    private string _todayBestScoreText = "0";

    [ObservableProperty]
    private string _currentStreakText = "0";

    [ObservableProperty]
    private string _longestStreakText = "0";

    [ObservableProperty]
    private string _totalCompletedText = "0";

    [ObservableProperty]
    private string _streakBonusText = "";

    [ObservableProperty]
    private string _playButtonText = "";

    // Lokalisierte Texte
    [ObservableProperty]
    private string _dailyChallengeTitle = "";

    [ObservableProperty]
    private string _bestScoreLabel = "";

    [ObservableProperty]
    private string _streakLabel = "";

    [ObservableProperty]
    private string _longestStreakLabel = "";

    [ObservableProperty]
    private string _completedLabel = "";

    [ObservableProperty]
    private string _streakBonusLabel = "";

    [ObservableProperty]
    private string _completedTodayText = "";

    public DailyChallengeViewModel(
        IDailyChallengeService dailyChallengeService,
        ICoinService coinService,
        ILocalizationService localizationService,
        IAchievementService achievementService)
    {
        _dailyChallengeService = dailyChallengeService;
        _coinService = coinService;
        _localizationService = localizationService;
        _achievementService = achievementService;
    }

    public void OnAppearing()
    {
        UpdateLocalizedTexts();
        UpdateStats();
    }

    /// <summary>
    /// Wird nach GameOver mit mode=daily aufgerufen um Score zu melden
    /// </summary>
    public void SubmitScore(int score)
    {
        if (score <= 0) return;

        bool isNewBest = _dailyChallengeService.SubmitScore(score);

        // Achievement: Daily Challenge Fortschritt prüfen
        _achievementService.OnDailyChallengeCompleted(
            _dailyChallengeService.TotalCompleted,
            _dailyChallengeService.CurrentStreak);

        // Streak-Bonus vergeben
        int bonus = _dailyChallengeService.GetStreakBonus();
        if (bonus > 0)
        {
            _coinService.AddCoins(bonus);
            FloatingTextRequested?.Invoke($"+{bonus:N0} Streak Bonus!", "gold");
        }

        if (isNewBest)
        {
            CelebrationRequested?.Invoke();
        }
    }

    [RelayCommand]
    private void PlayChallenge()
    {
        var seed = _dailyChallengeService.GetTodaySeed();
        NavigationRequested?.Invoke(new GoGame(Mode: "daily", Level: seed));
    }

    [RelayCommand]
    private void Back()
    {
        NavigationRequested?.Invoke(new GoBack());
    }

    private void UpdateStats()
    {
        IsCompletedToday = _dailyChallengeService.IsCompletedToday;
        TodayBestScoreText = _dailyChallengeService.TodayBestScore.ToString("N0");
        CurrentStreakText = _dailyChallengeService.CurrentStreak.ToString();
        LongestStreakText = _dailyChallengeService.LongestStreak.ToString();
        TotalCompletedText = _dailyChallengeService.TotalCompleted.ToString();

        int bonus = _dailyChallengeService.GetStreakBonus();
        StreakBonusText = bonus > 0 ? $"+{bonus:N0} Coins" : "-";

        PlayButtonText = IsCompletedToday
            ? _localizationService.GetString("DailyChallengeRetry") ?? "Play Again"
            : _localizationService.GetString("DailyChallengePlay") ?? "Start Challenge!";
    }

    private void UpdateLocalizedTexts()
    {
        DailyChallengeTitle = _localizationService.GetString("DailyChallengeTitle") ?? "Daily Challenge";
        BestScoreLabel = _localizationService.GetString("DailyChallengeBestScore") ?? "Best Score";
        StreakLabel = _localizationService.GetString("DailyChallengeStreak") ?? "Streak";
        LongestStreakLabel = _localizationService.GetString("DailyChallengeLongestStreak") ?? "Longest Streak";
        CompletedLabel = _localizationService.GetString("DailyChallengeCompleted") ?? "Completed";
        StreakBonusLabel = _localizationService.GetString("DailyChallengeStreakBonus") ?? "Streak Bonus";
        CompletedTodayText = _localizationService.GetString("DailyChallengeCompletedToday") ?? "Already played today!";
    }
}
