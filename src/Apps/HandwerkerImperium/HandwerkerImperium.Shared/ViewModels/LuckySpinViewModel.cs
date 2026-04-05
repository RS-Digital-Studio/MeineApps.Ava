using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel für das Glücksrad-Feature.
/// Täglicher Gratis-Spin + kostenpflichtige Spins (5 Goldschrauben).
/// Spin-Animation per DispatcherTimer mit Easing (schnell → langsam, ~3 Sekunden).
/// </summary>
public sealed partial class LuckySpinViewModel : ViewModelBase, IDisposable
{
    private readonly ILuckySpinService _luckySpinService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IAudioService _audioService;
    private readonly IRewardedAdService? _rewardedAdService;

    // Animations-State
    private DispatcherTimer? _spinTimer;
    private DispatcherTimer? _countdownTimer;
    private double _targetAngle;
    private double _totalRotation;
    private DateTime _spinStartTime;
    private LuckySpinPrizeType _pendingPrize;

    // Animations-Konstanten
    private const double SpinDurationMs = 3000.0;       // Gesamtdauer ~3 Sekunden
    private const int TimerIntervalMs = 16;             // ~60fps
    private const int SegmentCount = 8;                 // 8 Preissegmente à 45°
    private const double SegmentAngle = 360.0 / SegmentCount; // 45°
    private const int MinFullRotations = 3;             // Mindestens 3 volle Umdrehungen

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wird nach dem Spin ausgelöst (für Celebration-Effekte im MainViewModel).
    /// </summary>
    public Action? SpinCompleted;

    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private bool _hasFreeSpin;

    [ObservableProperty]
    private bool _isSpinning;

    [ObservableProperty]
    private double _spinAngle;

    [ObservableProperty]
    private string _lastPrizeDisplay = "";

    [ObservableProperty]
    private LuckySpinPrizeType? _lastPrizeType;

    [ObservableProperty]
    private bool _showPrize;

    [ObservableProperty]
    private bool _canSpin;

    [ObservableProperty]
    private string _spinButtonText = "";

    [ObservableProperty]
    private string _spinCostDisplay = "";

    /// <summary>
    /// Countdown bis zum nächsten Gratis-Spin (z.B. "12:34:56").
    /// Nur sichtbar wenn kein Gratis-Spin verfügbar.
    /// </summary>
    [ObservableProperty]
    private string _freeSpinCountdown = "";

    /// <summary>
    /// Ob der Countdown angezeigt werden soll.
    /// </summary>
    [ObservableProperty]
    private bool _showCountdown;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public LuckySpinViewModel(
        ILuckySpinService luckySpinService,
        IGameStateService gameStateService,
        ILocalizationService localizationService,
        IAudioService audioService,
        IRewardedAdService? rewardedAdService = null)
    {
        _luckySpinService = luckySpinService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _audioService = audioService;
        _rewardedAdService = rewardedAdService;

        UpdateLocalizedTexts();
        Refresh();
        // Timer wird erst bei ShowLuckySpin() gestartet (Batterie-Schonung)
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task Spin()
    {
        if (IsSpinning || !CanSpin) return;

        // Reihenfolge: 1. Gratis-Spin, 2. Ad-Spin, 3. GS-Spin
        bool isFree = _luckySpinService.HasFreeSpin;
        bool isAd = !isFree && _luckySpinService.HasAdSpin && _rewardedAdService?.IsAvailable == true;

        if (!isFree && !isAd && !_gameStateService.CanAffordGoldenScrews(_luckySpinService.SpinCost))
            return;

        if (isAd)
        {
            // BAL-AD-6: Ad-Spin - erst Video, dann drehen
            var success = await _rewardedAdService!.ShowAdAsync("lucky_spin");
            if (!success) return;
            _pendingPrize = _luckySpinService.SpinForAd();
            _luckySpinService.MarkAdSpinUsed();
        }
        else
        {
            _pendingPrize = _luckySpinService.Spin();
        }

        // UI-State vorbereiten
        IsSpinning = true;
        ShowPrize = false;
        LastPrizeDisplay = "";
        LastPrizeType = null;

        // Sound abspielen
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
        _audioService.Vibrate(VibrationType.Medium);

        // Zielwinkel berechnen: Mindestens 3 volle Umdrehungen + Segment-Mitte
        // Das Rad dreht sich im Uhrzeigersinn, der Zeiger steht oben.
        // Segmente starten bei -90° (12 Uhr). Segment i hat seine Mitte bei:
        //   -90° + i*45° + 22.5°
        // Damit Segment i unter dem Zeiger (-90°) steht, muss das Rad um
        //   360° - (i*45° + 22.5°) rotiert werden.
        int segmentIndex = GetSegmentIndex(_pendingPrize);
        double segmentCenter = segmentIndex * SegmentAngle + SegmentAngle / 2.0;
        double stopAngle = 360.0 - segmentCenter;
        // Leichte Zufallsvariation innerhalb des Segments (+/- 15°)
        double variation = (Random.Shared.NextDouble() - 0.5) * (SegmentAngle * 0.6);
        _targetAngle = MinFullRotations * 360.0 + stopAngle + variation;

        // Animations-Parameter initialisieren
        _totalRotation = 0;
        _spinStartTime = DateTime.UtcNow;

        // Timer starten
        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimerIntervalMs) };
        _spinTimer.Tick += OnSpinTick;
        _spinTimer.Start();

        // CanSpin sofort aktualisieren (Kosten wurden abgezogen)
        Refresh();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void DismissPrize()
    {
        ShowPrize = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ANIMATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tick-Handler für die Spin-Animation.
    /// Easing: Geschwindigkeit nimmt über die Dauer exponentiell ab.
    /// </summary>
    private async void OnSpinTick(object? sender, EventArgs e)
    {
        try
        {
            double elapsed = (DateTime.UtcNow - _spinStartTime).TotalMilliseconds;
            double progress = Math.Clamp(elapsed / SpinDurationMs, 0.0, 1.0);

            // Exponentielles Easing (CubicEaseOut): schneller Start, sanftes Auslaufen
            double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3.0);

            // Aktuelle Position interpolieren
            _totalRotation = _targetAngle * easedProgress;
            SpinAngle = _totalRotation % 360.0;

            // Animation beendet?
            if (progress >= 1.0)
            {
                _spinTimer?.Stop();
                _spinTimer!.Tick -= OnSpinTick;
                _spinTimer = null;

                // Endposition exakt setzen
                SpinAngle = (_targetAngle % 360.0 + 360.0) % 360.0;

                // Gewinn anwenden
                _luckySpinService.ApplyPrize(_pendingPrize);

                // Gewinn-Anzeige vorbereiten
                LastPrizeType = _pendingPrize;
                LastPrizeDisplay = BuildPrizeDisplay(_pendingPrize);
                ShowPrize = true;
                IsSpinning = false;

                // Sound + Haptik für Gewinn
                await _audioService.PlaySoundAsync(GameSound.CoinCollect);
                _audioService.Vibrate(VibrationType.Success);

                // Event für Celebration-Effekte
                SpinCompleted?.Invoke();

                // Properties aktualisieren
                Refresh();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] {nameof(OnSpinTick)} Fehler: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert alle UI-Properties aus dem Service-/GameState.
    /// </summary>
    public void Refresh()
    {
        HasFreeSpin = _luckySpinService.HasFreeSpin;
        bool hasAdSpin = _luckySpinService.HasAdSpin && _rewardedAdService?.IsAvailable == true;

        bool hasEnoughScrews = _gameStateService.CanAffordGoldenScrews(_luckySpinService.SpinCost);
        CanSpin = !IsSpinning && (HasFreeSpin || hasAdSpin || hasEnoughScrews);

        if (HasFreeSpin)
        {
            SpinButtonText = _localizationService.GetString("LuckySpinFree") ?? "Gratis drehen!";
            SpinCostDisplay = "";
        }
        else if (hasAdSpin)
        {
            // BAL-AD-6: Ad-Spin verfügbar
            SpinButtonText = _localizationService.GetString("LuckySpinAd") ?? "Video drehen";
            SpinCostDisplay = "";
        }
        else
        {
            var costFormat = _localizationService.GetString("LuckySpinCost") ?? "Drehen ({0})";
            var idx = costFormat.IndexOf(" (", StringComparison.Ordinal);
            SpinButtonText = idx > 0 ? costFormat[..idx] : string.Format(costFormat, _luckySpinService.SpinCost);
            SpinCostDisplay = _luckySpinService.SpinCost.ToString("N0");
        }

        // Countdown aktualisieren
        UpdateCountdown();
    }

    /// <summary>
    /// Startet den Countdown-Timer (1 Sekunde Intervall).
    /// Wird beim Anzeigen des Glücksrads aufgerufen.
    /// </summary>
    public void StartCountdownTimer()
    {
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => UpdateCountdown();
        _countdownTimer.Start();
    }

    /// <summary>
    /// Stoppt den Countdown-Timer.
    /// </summary>
    public void StopCountdownTimer()
    {
        _countdownTimer?.Stop();
        _countdownTimer = null;
    }

    /// <summary>
    /// Berechnet die verbleibende Zeit bis Mitternacht UTC.
    /// </summary>
    private void UpdateCountdown()
    {
        if (_luckySpinService.HasFreeSpin)
        {
            ShowCountdown = false;
            FreeSpinCountdown = "";
            return;
        }

        var now = DateTime.UtcNow;
        var midnight = now.Date.AddDays(1);
        var remaining = midnight - now;

        if (remaining.TotalSeconds <= 0)
        {
            // Mitternacht erreicht → Gratis-Spin wieder verfügbar
            ShowCountdown = false;
            Refresh();
            return;
        }

        ShowCountdown = true;
        FreeSpinCountdown = $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    /// <summary>
    /// Lokalisierte Texte aktualisieren (bei Sprachwechsel).
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("LuckySpin") ?? "Glücksrad";
        Refresh();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bestimmt den Segment-Index (0-7) für einen Preis-Typ.
    /// Die Reihenfolge MUSS mit LuckySpinWheelRenderer.DrawSegmentIcons() übereinstimmen:
    ///   0=MoneySmall(Grau), 1=MoneyMedium(Grün), 2=MoneyLarge(Gold),
    ///   3=XpBoost(Blau), 4=GoldenScrews(Amber), 5=SpeedBoost(Cyan),
    ///   6=ToolUpgrade(Orange), 7=Jackpot(Rot)
    /// </summary>
    private static int GetSegmentIndex(LuckySpinPrizeType prizeType) => prizeType switch
    {
        LuckySpinPrizeType.MoneySmall => 0,
        LuckySpinPrizeType.MoneyMedium => 1,
        LuckySpinPrizeType.MoneyLarge => 2,
        LuckySpinPrizeType.XpBoost => 3,
        LuckySpinPrizeType.GoldenScrews5 => 4,
        LuckySpinPrizeType.SpeedBoost => 5,
        LuckySpinPrizeType.ToolUpgrade => 6,
        LuckySpinPrizeType.Jackpot50 => 7,
        _ => 0
    };

    /// <summary>
    /// Erstellt den Anzeige-Text für einen Gewinn.
    /// </summary>
    private string BuildPrizeDisplay(LuckySpinPrizeType prizeType)
    {
        var incomePerSecond = Math.Max(1m, _gameStateService.State.NetIncomePerSecond);
        var (money, screws, xp, description) = LuckySpinPrize.CalculateReward(prizeType, incomePerSecond);

        return prizeType switch
        {
            LuckySpinPrizeType.MoneySmall or
            LuckySpinPrizeType.MoneyMedium or
            LuckySpinPrizeType.MoneyLarge => FormatMoney(money),

            LuckySpinPrizeType.XpBoost => $"+{xp} XP",

            LuckySpinPrizeType.GoldenScrews5 => $"+{screws} \U0001f529",

            LuckySpinPrizeType.SpeedBoost =>
                _localizationService.GetString("LuckySpinSpeedBoost") ?? "2x Speed 30min",

            LuckySpinPrizeType.ToolUpgrade =>
                _localizationService.GetString("LuckySpinToolUpgrade") ?? "Werkzeug-Upgrade!",

            LuckySpinPrizeType.Jackpot50 => $"+{screws} \U0001f529 JACKPOT!",

            _ => description
        };
    }

    /// <summary>
    /// Formatiert einen Geldbetrag mit +Vorzeichen und passendem Suffix.
    /// </summary>
    private static string FormatMoney(decimal amount)
    {
        return $"+{MoneyFormatter.FormatCompact(amount)}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt alle DispatcherTimer frei (kein Memory Leak bei ViewModel-Austausch).
    /// </summary>
    public void Dispose()
    {
        _spinTimer?.Stop();
        _spinTimer = null;

        _countdownTimer?.Stop();
        _countdownTimer = null;
    }
}
