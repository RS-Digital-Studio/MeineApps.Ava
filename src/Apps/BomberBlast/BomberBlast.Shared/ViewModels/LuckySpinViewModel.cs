using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für das Glücksrad (Lucky Spin).
/// 9 Segmente (Coins + Gems), 1x gratis pro Tag, Extra-Spins per Rewarded Ad.
/// </summary>
public partial class LuckySpinViewModel : ObservableObject, INavigable, IGameJuiceEmitter
{
    private readonly ILuckySpinService _spinService;
    private readonly ICoinService _coinService;
    private readonly IGemService _gemService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly ILocalizationService _localizationService;
    private readonly IBattlePassService _battlePassService;
    private readonly IAchievementService _achievementService;
    private readonly IWeeklyChallengeService _weeklyService;
    private readonly IDailyMissionService _dailyMissionService;

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Ob das Rad gerade dreht</summary>
    [ObservableProperty]
    private bool _isSpinning;

    /// <summary>Ob ein kostenloser Spin verfügbar ist</summary>
    [ObservableProperty]
    private bool _isFreeSpinAvailable;

    /// <summary>Aktueller Drehwinkel in Grad</summary>
    [ObservableProperty]
    private float _currentAngle;

    /// <summary>Ergebnis-Text nach dem Spin</summary>
    [ObservableProperty]
    private string _resultText = "";

    /// <summary>Ob ein Ergebnis angezeigt wird</summary>
    [ObservableProperty]
    private bool _showResult;

    /// <summary>Coin-Guthaben-Text</summary>
    [ObservableProperty]
    private string _coinsText = "0";

    /// <summary>Bisherige Spins</summary>
    [ObservableProperty]
    private string _totalSpinsText = "";

    /// <summary>Button-Text für den Spin-Button</summary>
    [ObservableProperty]
    private string _spinButtonText = "";

    /// <summary>Ob der Spin-Button aktiv ist</summary>
    [ObservableProperty]
    private bool _canSpin = true;

    /// <summary>Ob ein Extra-Spin per Werbung verfügbar ist (nach Gratis-Spin verbraucht)</summary>
    [ObservableProperty]
    private bool _canWatchAdForSpin;

    /// <summary>Ob ein Extra-Spin per Gems kaufbar ist (3 Gems)</summary>
    [ObservableProperty]
    private bool _canBuySpinWithGems;

    /// <summary>Gem-Preis für einen Extra-Spin</summary>
    private const int GEM_SPIN_COST = 3;

    // Spin-Animations-State
    private float _spinSpeed;
    private float _targetAngle;
    private int _spinResultIndex = -1;
    private bool _isDecelerating;
    private int _pendingCoins;
    private int _pendingGems;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public LuckySpinViewModel(
        ILuckySpinService spinService,
        ICoinService coinService,
        IGemService gemService,
        IRewardedAdService rewardedAdService,
        ILocalizationService localizationService,
        IBattlePassService battlePassService,
        IAchievementService achievementService,
        IWeeklyChallengeService weeklyService,
        IDailyMissionService dailyMissionService)
    {
        _spinService = spinService;
        _coinService = coinService;
        _gemService = gemService;
        _rewardedAdService = rewardedAdService;
        _localizationService = localizationService;
        _battlePassService = battlePassService;
        _achievementService = achievementService;
        _weeklyService = weeklyService;
        _dailyMissionService = dailyMissionService;

        _coinService.BalanceChanged += (_, _) => UpdateCoinsText();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        IsFreeSpinAvailable = _spinService.IsFreeSpinAvailable;
        CanWatchAdForSpin = !IsFreeSpinAvailable && _rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd;
        CanBuySpinWithGems = !IsFreeSpinAvailable && _gemService.CanAfford(GEM_SPIN_COST);
        ShowResult = false;
        ResultText = "";
        UpdateCoinsText();
        UpdateSpinButton();
        TotalSpinsText = string.Format(
            _localizationService.GetString("LuckySpinTotal") ?? "Total: {0} Spins",
            _spinService.TotalSpins);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SpinWheel()
    {
        if (IsSpinning || !CanSpin) return;

        if (IsFreeSpinAvailable)
        {
            // Gratis-Spin
            _spinService.ClaimFreeSpin();
            StartSpin();
        }
        else
        {
            // Rewarded Ad für Extra-Spin
            CanSpin = false;
            var result = await _rewardedAdService.ShowAdAsync("lucky_spin");
            if (result)
            {
                RewardedAdCooldownTracker.RecordAdShown();
                StartSpin();
            }
            else
            {
                CanSpin = true;
            }
        }
    }

    /// <summary>
    /// Extra-Spin per Rewarded Ad (nach verbrauchtem Gratis-Spin verfügbar).
    /// Nutzt ein eigenes Placement für besseres Ad-Tracking.
    /// </summary>
    [RelayCommand]
    private async Task WatchAdForExtraSpin()
    {
        if (IsSpinning || !CanWatchAdForSpin) return;

        CanWatchAdForSpin = false;
        var success = await _rewardedAdService.ShowAdAsync("extra_daily_spin");
        if (success)
        {
            RewardedAdCooldownTracker.RecordAdShown();
            StartSpin();
        }
        else
        {
            // Ad fehlgeschlagen → wieder aktivieren falls Service verfügbar
            CanWatchAdForSpin = _rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd;
            FloatingTextRequested?.Invoke(
                _localizationService.GetString("AdUnavailable") ?? "Ad not available",
                "warning");
        }
    }

    /// <summary>
    /// Extra-Spin für 3 Gems kaufen (Gem-Sink, jederzeit verfügbar wenn genug Gems).
    /// </summary>
    [RelayCommand]
    private void BuySpinWithGems()
    {
        if (IsSpinning || !CanBuySpinWithGems) return;

        if (!_gemService.TrySpendGems(GEM_SPIN_COST))
        {
            FloatingTextRequested?.Invoke(
                _localizationService.GetString("InsufficientGems") ?? "Not enough Gems",
                "warning");
            return;
        }

        FloatingTextRequested?.Invoke($"-{GEM_SPIN_COST} Gems", "cyan");
        CanBuySpinWithGems = false;
        StartSpin();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(new GoBack());
    }

    [RelayCommand]
    private void CollectReward()
    {
        var rewards = _spinService.GetRewards();
        var isJackpot = _spinResultIndex >= 0 && _spinResultIndex < rewards.Count && rewards[_spinResultIndex].IsJackpot;

        if (_pendingCoins > 0)
        {
            _coinService.AddCoins(_pendingCoins);
            FloatingTextRequested?.Invoke($"+{_pendingCoins:N0} Coins!", "gold");
            _pendingCoins = 0;
        }

        if (_pendingGems > 0)
        {
            _gemService.AddGems(_pendingGems);
            FloatingTextRequested?.Invoke($"+{_pendingGems} Gems!", "cyan");
            _pendingGems = 0;
        }

        // Battle Pass XP für Lucky Spin
        _battlePassService.AddXp(BattlePassXpSources.LuckySpin, "lucky_spin");

        // Mission-Tracking: Glücksrad gedreht
        _weeklyService.TrackProgress(WeeklyMissionType.SpinLuckyWheel);
        _dailyMissionService.TrackProgress(WeeklyMissionType.SpinLuckyWheel);

        if (isJackpot)
        {
            CelebrationRequested?.Invoke();
            _achievementService.OnLuckyJackpot();
        }

        ShowResult = false;
        IsFreeSpinAvailable = _spinService.IsFreeSpinAvailable;

        // Nach dem Einsammeln: Extra-Spin per Ad oder Gems anbieten
        CanWatchAdForSpin = !IsFreeSpinAvailable && _rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd;
        CanBuySpinWithGems = !IsFreeSpinAvailable && _gemService.CanAfford(GEM_SPIN_COST);

        UpdateSpinButton();
        UpdateCoinsText();
        TotalSpinsText = string.Format(
            _localizationService.GetString("LuckySpinTotal") ?? "Total: {0} Spins",
            _spinService.TotalSpins);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIN ANIMATION
    // ═══════════════════════════════════════════════════════════════════════

    private void StartSpin()
    {
        // Ergebnis vorab berechnen
        _spinResultIndex = _spinService.Spin();

        // Zielwinkel berechnen: mehrere volle Drehungen + Segment-Position
        // Segment 0 ist oben (12 Uhr), Segmente gehen im Uhrzeigersinn
        var rewards = _spinService.GetRewards();
        var segmentAngle = 360f / rewards.Count; // 40° pro Segment (9 Segmente)
        // Zeiger ist oben → Segment-Mitte berechnen
        var segmentCenter = _spinResultIndex * segmentAngle + segmentAngle / 2f;
        // Rad dreht sich → der Zeiger zeigt auf (360 - segmentCenter) wenn das Rad rotiert
        var stopAngle = 360f - segmentCenter;

        // Mindestens 5 volle Drehungen + Offset zum Ziel
        _targetAngle = CurrentAngle + 360f * 7 + stopAngle;
        // Sicherstellen dass _targetAngle > CurrentAngle
        while (_targetAngle - CurrentAngle < 360f * 5)
            _targetAngle += 360f;

        _spinSpeed = 720f; // Start-Geschwindigkeit (Grad/Sekunde)
        _isDecelerating = false;
        IsSpinning = true;
        CanSpin = false;
        ShowResult = false;
    }

    /// <summary>
    /// Wird vom View per DispatcherTimer aufgerufen (~60fps).
    /// Gibt true zurück solange die Animation läuft.
    /// </summary>
    public bool UpdateAnimation(float deltaTime)
    {
        if (!IsSpinning) return false;

        var remaining = _targetAngle - CurrentAngle;

        if (remaining <= 360f * 2 && !_isDecelerating)
            _isDecelerating = true;

        if (_isDecelerating)
        {
            // Ease-Out: Geschwindigkeit proportional zum verbleibenden Winkel
            var factor = Math.Max(remaining / (360f * 2), 0.02f);
            _spinSpeed = 720f * factor;
            _spinSpeed = Math.Max(_spinSpeed, 15f); // Minimum damit es nicht einfriert
        }

        CurrentAngle += _spinSpeed * deltaTime;

        if (CurrentAngle >= _targetAngle)
        {
            CurrentAngle = _targetAngle % 360f;
            IsSpinning = false;
            OnSpinComplete();
        }

        return IsSpinning;
    }

    private void OnSpinComplete()
    {
        var rewards = _spinService.GetRewards();
        if (_spinResultIndex >= 0 && _spinResultIndex < rewards.Count)
        {
            var reward = rewards[_spinResultIndex];
            _pendingCoins = reward.Coins;
            _pendingGems = reward.Gems;

            var nameKey = _localizationService.GetString(reward.NameKey)
                ?? (reward.Gems > 0 ? $"{reward.Gems} Gems" : $"{reward.Coins} Coins");
            ResultText = reward.IsJackpot
                ? $"JACKPOT! {nameKey}"
                : nameKey;
        }

        ShowResult = true;
        IsFreeSpinAvailable = _spinService.IsFreeSpinAvailable;
        UpdateSpinButton();
    }

    /// <summary>Belohnungs-Segmente für das Rad-Rendering</summary>
    public IReadOnlyList<SpinReward> Rewards => _spinService.GetRewards();

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateCoinsText()
    {
        CoinsText = _coinService.Balance.ToString("N0");
    }

    private void UpdateSpinButton()
    {
        if (ShowResult)
        {
            SpinButtonText = _localizationService.GetString("LuckySpinCollect") ?? "Collect!";
            CanSpin = false;
        }
        else if (IsFreeSpinAvailable)
        {
            SpinButtonText = _localizationService.GetString("LuckySpinFree") ?? "Free Spin!";
            CanSpin = true;
        }
        else
        {
            // Ad-Spin nur möglich wenn kein Cooldown aktiv
            bool adReady = _rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd;
            SpinButtonText = _localizationService.GetString("LuckySpinAd") ?? "Watch Ad";
            CanSpin = adReady;
        }
    }
}
