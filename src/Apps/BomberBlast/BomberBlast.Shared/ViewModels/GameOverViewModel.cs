using Avalonia.Threading;
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
    private readonly IWeeklyChallengeService _weeklyService;
    private readonly IDailyMissionService _dailyMissionService;
    private bool _coinsClaimed;
    private DispatcherTimer? _coinAnimTimer;
    private int _animatedCoins;
    private int _targetCoins;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;

    /// <summary>Bestätigungsdialog anfordern (Titel, Nachricht, Akzeptieren, Abbrechen)</summary>
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;

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
    private bool _isSurvivalMode;

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

    // Near-Miss (knapp am nächsten Stern vorbei)
    [ObservableProperty]
    private string _nearMissText = "";

    [ObservableProperty]
    private bool _hasNearMiss;

    // Motivationstext (kontextbezogen statt nur "GAME OVER")
    [ObservableProperty]
    private string _motivationText = "";

    [ObservableProperty]
    private bool _hasMotivation;

    // Paid Continue (199 Coins als Alternative zur Ad)
    [ObservableProperty]
    private bool _canPaidContinue;

    [ObservableProperty]
    private string _paidContinueText = "";

    private const int PAID_CONTINUE_COST = 199;

    // Premium: Kostenloser Level-Skip (1x pro Session)
    private static bool _premiumSkipUsed;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GameOverViewModel(
        IPurchaseService purchaseService,
        ILocalizationService localizationService,
        ICoinService coinService,
        IRewardedAdService rewardedAdService,
        IProgressService progressService,
        IWeeklyChallengeService weeklyService,
        IDailyMissionService dailyMissionService)
    {
        _purchaseService = purchaseService;
        _localizationService = localizationService;
        _coinService = coinService;
        _rewardedAdService = rewardedAdService;
        _progressService = progressService;
        _weeklyService = weeklyService;
        _dailyMissionService = dailyMissionService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parameter vom Navigation-Query setzen
    /// </summary>
    public void SetParameters(int score, int level, bool isHighScore, string mode,
        int coins, bool isLevelComplete, bool canContinue, int fails = 0,
        int enemyKillPoints = 0, int timeBonus = 0, int efficiencyBonus = 0, float scoreMultiplier = 1f,
        int survivalKills = 0, float survivalTime = 0)
    {
        // Timer defensiv stoppen bevor neuer State gesetzt wird
        _coinAnimTimer?.Stop();

        Score = score;
        Level = level;
        IsHighScore = isHighScore;
        Mode = mode;
        IsSurvivalMode = mode == "survival";
        IsLevelComplete = isLevelComplete;
        IsPremium = _purchaseService.IsPremium;

        ScoreText = score.ToString("N0");

        // Survival: Kills + Zeit statt Level-Nummer
        if (IsSurvivalMode)
        {
            int mins = (int)(survivalTime / 60);
            int secs = (int)(survivalTime % 60);
            SurvivalKills = survivalKills;
            SurvivalTimeText = $"{mins}:{secs:D2}";
            HasSurvivalStats = true;
            LevelText = string.Format(
                _localizationService.GetString("SurvivalKills") ?? "Kills: {0}",
                survivalKills);
        }
        else
        {
            HasSurvivalStats = false;
            LevelText = string.Format(_localizationService.GetString("LevelFormat"), level);
        }

        // Coins (mit hochzählender Animation)
        CoinsEarned = coins;
        _targetCoins = coins;
        _animatedCoins = 0;
        CoinsEarnedText = "+0";
        HasDoubled = false;
        StartCoinAnimation();
        HasContinued = false;
        _coinsClaimed = false;
        CanDoubleCoins = coins > 0 && _rewardedAdService.IsAvailable;
        CanContinue = canContinue && (_rewardedAdService.IsAvailable || _purchaseService.IsPremium);

        // Level-Skip: Ab 2 Fehlversuchen (Free: Rewarded Ad, Premium: 1x kostenlos pro Session)
        bool hasFreeSkip = _purchaseService.IsPremium && !_premiumSkipUsed;
        CanSkipLevel = !isLevelComplete && !IsSurvivalMode &&
            (fails >= 2 && _rewardedAdService.IsAvailable || hasFreeSkip);
        SkipLevelText = hasFreeSkip
            ? _localizationService.GetString("SkipLevelFree") ?? "Level überspringen"
            : _localizationService.GetString("SkipLevel");
        SkipLevelInfoText = _localizationService.GetString("SkipLevelInfo");

        // Paid Continue: Alternative zur Ad wenn Coins vorhanden
        CanPaidContinue = canContinue && !HasContinued && _coinService.CanAfford(PAID_CONTINUE_COST);
        PaidContinueText = string.Format(
            _localizationService.GetString("PaidContinue") ?? "Continue ({0} Coins)",
            PAID_CONTINUE_COST);

        // Lokalisierte Button-Texte
        DoubleCoinsButtonText = _localizationService.GetString("DoubleCoins");
        ContinueButtonText = _purchaseService.IsPremium
            ? _localizationService.GetString("ContinueFree") ?? "Weiterspielen"
            : _localizationService.GetString("ContinueGame");

        // Score-Aufschlüsselung (nur bei Level-Complete)
        HasSummary = isLevelComplete;
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

            // Near-Miss: Knapp am nächsten Stern vorbei (innerhalb 30% des Schwellwerts)
            HasNearMiss = false;
            if (StarsEarned < 3)
            {
                int baseScoreForLevel = _progressService.GetBaseScoreForLevel(level);
                int nextThreshold = baseScoreForLevel * (StarsEarned + 1);
                int pointsNeeded = nextThreshold - score;
                if (pointsNeeded > 0 && pointsNeeded < baseScoreForLevel * 0.3f)
                {
                    NearMissText = string.Format(
                        _localizationService.GetString("NearMissStars") ?? "Only {0} more points for the next star!",
                        pointsNeeded.ToString("N0"));
                    HasNearMiss = true;
                }
            }
        }

        // Motivationstext (kontextbezogen)
        HasMotivation = !isLevelComplete;
        if (!isLevelComplete)
        {
            if (score > 0 && fails >= 2)
                MotivationText = _localizationService.GetString("MotivationKeepGoing") ?? "Don't give up!";
            else
                MotivationText = _localizationService.GetString("MotivationTryAgain") ?? "Give it another shot!";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task DoubleCoins()
    {
        if (HasDoubled || CoinsEarned <= 0) return;

        // Sofort deaktivieren um Doppelklick während async-Gap zu verhindern
        CanDoubleCoins = false;

        bool rewarded = await _rewardedAdService.ShowAdAsync("continue");
        if (rewarded)
        {
            CoinsEarned *= 2;
            CoinsEarnedText = $"+{CoinsEarned:N0}";
            FloatingTextRequested?.Invoke("x2!", "gold");
            HasDoubled = true;
            DoubleCoinsButtonText = _localizationService.GetString("CoinsDoubled");
        }
        else
        {
            // Ad fehlgeschlagen → Button wieder aktivieren
            CanDoubleCoins = _rewardedAdService.IsAvailable;
        }
    }

    [RelayCommand]
    private async Task ContinueGame()
    {
        if (HasContinued || IsSurvivalMode) return;

        // Premium: Kostenloser Continue (kein Ad)
        if (_purchaseService.IsPremium)
        {
            PerformContinue();
            return;
        }

        // Free: Rewarded Ad
        bool rewarded = await _rewardedAdService.ShowAdAsync("revival");
        if (rewarded)
        {
            PerformContinue();
        }
    }

    [RelayCommand]
    private async Task PaidContinueAsync()
    {
        if (HasContinued || IsSurvivalMode) return;

        // Bestätigungsdialog vor Coin-Ausgabe
        if (ConfirmationRequested != null)
        {
            var msg = string.Format(
                _localizationService.GetString("ConfirmPaidContinue") ?? "{0} Coins ausgeben um weiterzuspielen?",
                PAID_CONTINUE_COST);
            var confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("ContinueGame"),
                msg,
                _localizationService.GetString("Continue"),
                _localizationService.GetString("Cancel"));
            if (!confirmed) return;
        }

        if (!_coinService.TrySpendCoins(PAID_CONTINUE_COST)) return;

        PerformContinue();
    }

    /// <summary>
    /// Gemeinsame Continue-Logik (Ad oder Coins)
    /// </summary>
    private void PerformContinue()
    {
        // Coins fuer bisherigen Fortschritt gutschreiben
        if (CoinsEarned > 0)
        {
            _coinService.AddCoins(CoinsEarned);
            CoinsEarned = 0; // Doppelte Gutschrift verhindern
        }

        HasContinued = true;
        CanContinue = false;
        CanPaidContinue = false;

        // Zurueck zum Spiel mit Continue-Modus
        NavigationRequested?.Invoke($"Game?mode={Mode}&level={Level}&continue=true");
    }

    [RelayCommand]
    private async Task SkipLevelAsync()
    {
        if (!CanSkipLevel) return;

        // Premium: Kostenloser Skip (1x pro Session)
        if (_purchaseService.IsPremium && !_premiumSkipUsed)
        {
            _premiumSkipUsed = true;
            PerformSkip();
            return;
        }

        // Free: Rewarded Ad
        var success = await _rewardedAdService.ShowAdAsync("level_skip");
        if (success)
        {
            PerformSkip();
        }
    }

    private void PerformSkip()
    {
        CanSkipLevel = false;
        // Level als bestanden markieren (minimaler Score fuer 1 Stern)
        _progressService.CompleteLevel(Level);
        _progressService.SetLevelBestScore(Level, 100);

        ClaimCoins();
        NavigationRequested?.Invoke("LevelSelect");
    }

    [RelayCommand]
    private void TryAgain()
    {
        ClaimCoins();

        if (IsSurvivalMode)
        {
            NavigationRequested?.Invoke("//MainMenu/Game?mode=survival");
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

    // Survival-Statistiken
    [ObservableProperty]
    private int _survivalKills;

    [ObservableProperty]
    private string _survivalTimeText = "";

    [ObservableProperty]
    private bool _hasSurvivalStats;

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

            // Wöchentliche Challenge: Coins-Verdienst tracken
            _weeklyService.TrackProgress(Models.WeeklyMissionType.EarnCoins, CoinsEarned);
            _dailyMissionService.TrackProgress(Models.WeeklyMissionType.EarnCoins, CoinsEarned);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COIN-COUNTER ANIMATION
    // ═══════════════════════════════════════════════════════════════════════

    private int _coinAnimFrame;
    private const int COIN_ANIM_FRAMES = 35; // ~0.56s bei 60fps

    private void StartCoinAnimation()
    {
        _coinAnimTimer?.Stop();
        if (_targetCoins <= 0)
        {
            CoinsEarnedText = "+0";
            return;
        }

        _coinAnimFrame = 0;
        _coinAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _coinAnimTimer.Tick += (_, _) =>
        {
            _coinAnimFrame++;
            // Ease-Out: Anfangs schnell, Ende langsam (1 - (1-t)^2)
            float t = Math.Min(1f, (float)_coinAnimFrame / COIN_ANIM_FRAMES);
            float eased = 1f - (1f - t) * (1f - t);
            _animatedCoins = (int)(eased * _targetCoins);
            CoinsEarnedText = $"+{_animatedCoins:N0}";

            if (_coinAnimFrame >= COIN_ANIM_FRAMES)
            {
                _animatedCoins = _targetCoins;
                CoinsEarnedText = $"+{_targetCoins:N0}";
                _coinAnimTimer?.Stop();
                _coinAnimTimer = null;
            }
        };
        _coinAnimTimer.Start();
    }
}
