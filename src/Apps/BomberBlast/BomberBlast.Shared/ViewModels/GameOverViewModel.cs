using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer den Game Over / Level Complete Screen.
/// Zeigt Score, Coins, Verdopplungs- und Continue-Option.
/// Bei Level-Complete: Score-Aufschlüsselung und Sterne.
/// </summary>
public partial class GameOverViewModel : ObservableObject
{
    private readonly IPurchaseService _purchaseService;
    private readonly ILocalizationService _localizationService;
    private readonly ICoinService _coinService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IProgressService _progressService;
    private bool _coinsClaimed;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _score;

    [ObservableProperty]
    private string _scoreText = "";

    [ObservableProperty]
    private int _level;

    [ObservableProperty]
    private string _levelText = "";

    [ObservableProperty]
    private bool _isHighScore;

    [ObservableProperty]
    private string _mode = "story";

    [ObservableProperty]
    private bool _isArcadeMode;

    [ObservableProperty]
    private bool _isLevelComplete;

    // Coins
    [ObservableProperty]
    private int _coinsEarned;

    [ObservableProperty]
    private string _coinsEarnedText = "";

    [ObservableProperty]
    private bool _canDoubleCoins;

    [ObservableProperty]
    private bool _hasDoubled;

    [ObservableProperty]
    private string _doubleCoinsButtonText = "";

    // Continue
    [ObservableProperty]
    private bool _canContinue;

    [ObservableProperty]
    private bool _hasContinued;

    [ObservableProperty]
    private string _continueButtonText = "";

    // Level-Skip (nach 3x Game Over)
    [ObservableProperty]
    private bool _canSkipLevel;

    [ObservableProperty]
    private string _skipLevelText = "";

    [ObservableProperty]
    private string _skipLevelInfoText = "";

    // Score-Aufschlüsselung (Level-Complete Summary)
    [ObservableProperty]
    private int _enemyKillPoints;

    [ObservableProperty]
    private string _enemyKillPointsText = "";

    [ObservableProperty]
    private int _timeBonus;

    [ObservableProperty]
    private string _timeBonusText = "";

    [ObservableProperty]
    private int _efficiencyBonus;

    [ObservableProperty]
    private string _efficiencyBonusText = "";

    [ObservableProperty]
    private float _scoreMultiplier;

    [ObservableProperty]
    private string _scoreMultiplierText = "";

    [ObservableProperty]
    private int _starsEarned;

    [ObservableProperty]
    private bool _star1Earned;

    [ObservableProperty]
    private bool _star2Earned;

    [ObservableProperty]
    private bool _star3Earned;

    [ObservableProperty]
    private bool _isPremium;

    [ObservableProperty]
    private bool _hasSummary;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GameOverViewModel(
        IPurchaseService purchaseService,
        ILocalizationService localizationService,
        ICoinService coinService,
        IRewardedAdService rewardedAdService,
        IProgressService progressService)
    {
        _purchaseService = purchaseService;
        _localizationService = localizationService;
        _coinService = coinService;
        _rewardedAdService = rewardedAdService;
        _progressService = progressService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parameter vom Navigation-Query setzen
    /// </summary>
    public void SetParameters(int score, int level, bool isHighScore, string mode,
        int coins, bool isLevelComplete, bool canContinue, int fails = 0,
        int enemyKillPoints = 0, int timeBonus = 0, int efficiencyBonus = 0, float scoreMultiplier = 1f)
    {
        Score = score;
        Level = level;
        IsHighScore = isHighScore;
        Mode = mode;
        IsArcadeMode = mode == "arcade";
        IsLevelComplete = isLevelComplete;
        IsPremium = _purchaseService.IsPremium;

        ScoreText = score.ToString("N0");
        LevelText = IsArcadeMode
            ? string.Format(_localizationService.GetString("WaveOverlay"), level)
            : string.Format(_localizationService.GetString("LevelFormat"), level);

        // Coins
        CoinsEarned = coins;
        CoinsEarnedText = $"+{coins:N0}";
        HasDoubled = false;
        HasContinued = false;
        _coinsClaimed = false;
        CanDoubleCoins = coins > 0 && _rewardedAdService.IsAvailable;
        CanContinue = canContinue && _rewardedAdService.IsAvailable;

        // Level-Skip: Ab 3 Fehlversuchen im Story-Mode
        CanSkipLevel = !isLevelComplete && !IsArcadeMode && fails >= 3 && _rewardedAdService.IsAvailable;
        SkipLevelText = _localizationService.GetString("SkipLevel");
        SkipLevelInfoText = _localizationService.GetString("SkipLevelInfo");

        // Lokalisierte Button-Texte
        DoubleCoinsButtonText = _localizationService.GetString("DoubleCoins");
        ContinueButtonText = _localizationService.GetString("ContinueGame");

        // Score-Aufschlüsselung (nur bei Level-Complete)
        HasSummary = isLevelComplete && !IsArcadeMode;
        if (HasSummary)
        {
            EnemyKillPoints = enemyKillPoints;
            EnemyKillPointsText = $"+{enemyKillPoints:N0}";
            TimeBonus = timeBonus;
            TimeBonusText = $"+{timeBonus:N0}";
            EfficiencyBonus = efficiencyBonus;
            EfficiencyBonusText = efficiencyBonus > 0 ? $"+{efficiencyBonus:N0}" : "-";
            ScoreMultiplier = scoreMultiplier;
            ScoreMultiplierText = scoreMultiplier > 1f ? $"x{scoreMultiplier:F2}" : "-";
            StarsEarned = _progressService.GetLevelStars(level);
            Star1Earned = StarsEarned >= 1;
            Star2Earned = StarsEarned >= 2;
            Star3Earned = StarsEarned >= 3;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task DoubleCoins()
    {
        if (HasDoubled || CoinsEarned <= 0) return;

        bool rewarded = await _rewardedAdService.ShowAdAsync("continue");
        if (rewarded)
        {
            CoinsEarned *= 2;
            CoinsEarnedText = $"+{CoinsEarned:N0}";
            FloatingTextRequested?.Invoke("x2!", "gold");
            HasDoubled = true;
            CanDoubleCoins = false;
            DoubleCoinsButtonText = _localizationService.GetString("CoinsDoubled");
        }
    }

    [RelayCommand]
    private async Task ContinueGame()
    {
        if (HasContinued || IsArcadeMode) return;

        bool rewarded = await _rewardedAdService.ShowAdAsync("continue");
        if (rewarded)
        {
            // Coins fuer bisherigen Fortschritt gutschreiben
            if (CoinsEarned > 0)
            {
                _coinService.AddCoins(CoinsEarned);
                CoinsEarned = 0; // Doppelte Gutschrift verhindern
            }

            HasContinued = true;
            CanContinue = false;

            // Zurueck zum Spiel mit Continue-Modus
            NavigationRequested?.Invoke($"Game?mode={Mode}&level={Level}&continue=true");
        }
    }

    [RelayCommand]
    private async Task SkipLevelAsync()
    {
        if (!CanSkipLevel) return;

        var success = await _rewardedAdService.ShowAdAsync("level_skip");
        if (success)
        {
            CanSkipLevel = false;
            // Level als bestanden markieren (minimaler Score fuer 1 Stern)
            _progressService.CompleteLevel(Level);
            _progressService.SetLevelBestScore(Level, 100);

            ClaimCoins();
            NavigationRequested?.Invoke("LevelSelect");
        }
    }

    [RelayCommand]
    private void TryAgain()
    {
        ClaimCoins();

        if (IsArcadeMode)
        {
            NavigationRequested?.Invoke("//MainMenu/Game?mode=arcade");
        }
        else
        {
            NavigationRequested?.Invoke($"//MainMenu/Game?mode=story&level={Level}");
        }
    }

    [RelayCommand]
    private void MainMenu()
    {
        ClaimCoins();
        NavigationRequested?.Invoke("//MainMenu");
    }

    /// <summary>
    /// Verdiente Coins dem CoinService gutschreiben (nur einmal pro Session)
    /// </summary>
    private void ClaimCoins()
    {
        if (_coinsClaimed || HasContinued)
            return;

        if (CoinsEarned > 0)
        {
            _coinService.AddCoins(CoinsEarned);
            _coinsClaimed = true;
        }
    }
}
