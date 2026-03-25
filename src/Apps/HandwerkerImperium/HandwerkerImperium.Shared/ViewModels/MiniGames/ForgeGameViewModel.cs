using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels.MiniGames;

/// <summary>
/// ViewModel fuer das Schmiede-Minigame.
/// Spieler muss bei richtiger Temperatur auf den Amboss haemmern.
/// Temperatur steigt automatisch, kuehlt nach Hammer-Schlag ab.
/// </summary>
public sealed partial class ForgeGameViewModel : BaseMiniGameViewModel
{
    // Spiel-Konfiguration
    private const double TICK_INTERVAL_MS = 16; // ~60 FPS
    private const double HEAT_RATE = 0.008;     // Aufheiz-Geschwindigkeit pro Tick
    private const double COOL_RATE = 0.25;      // Abkuehl-Menge nach Hammer-Schlag
    private const double COOL_DECAY = 0.003;    // Natuerliche Abkuehlung pro Tick (langsam)

    // Sinus-basierte Temperatur-Oszillation
    private double _heatTime;
    private double _heatDirection = 1.0;

    // ===================================================================
    // BASIS-KLASSE KONFIGURATION
    // ===================================================================

    protected override MiniGameType GameMiniGameType => MiniGameType.ForgeGame;
    protected override TimeSpan TimerInterval => TimeSpan.FromMilliseconds(TICK_INTERVAL_MS);
    protected override bool PlaySoundBeforeCountdown => false;
    protected override MiniGameType GetCurrentMiniGameType() => GameType;

    // ===================================================================
    // SPIEL-SPEZIFISCHE EVENTS
    // ===================================================================

    /// <summary>Wird beim Spielstart nach Countdown gefeuert.</summary>
    public event EventHandler? GameStarted;

    /// <summary>Wird bei Zonen-Treffer gefeuert (Zone-Name: "Perfect", "Good", "Ok", "Miss").</summary>
    public event EventHandler<string>? ZoneHit;

    // ===================================================================
    // SPIEL-SPEZIFISCHE OBSERVABLE PROPERTIES
    // ===================================================================

    [ObservableProperty]
    private MiniGameType _gameType = MiniGameType.ForgeGame;

    [ObservableProperty]
    private string _gameTitle = "";

    [ObservableProperty]
    private string _gameIcon = "Anvil";

    [ObservableProperty]
    private string _actionButtonText = "";

    [ObservableProperty]
    private string _instructionText = "";

    // Temperatur des Werkstuecks (0.0 = kalt, 1.0 = weissgluehend)
    [ObservableProperty]
    private double _temperature;

    // Zielzone (Perfect-Temperatur)
    [ObservableProperty]
    private double _targetTemperatureStart;

    [ObservableProperty]
    private double _targetTemperatureWidth;

    // Gut-Zone (etwas breiter als Perfect)
    [ObservableProperty]
    private double _goodTemperatureStart;

    [ObservableProperty]
    private double _goodTemperatureWidth;

    // Ok-Zone (noch breiter)
    [ObservableProperty]
    private double _okTemperatureStart;

    [ObservableProperty]
    private double _okTemperatureWidth;

    // Benoetigte und abgeschlossene Schlaege
    [ObservableProperty]
    private int _hitsRequired;

    [ObservableProperty]
    private int _hitsCompleted;

    // Treffer-Statistik pro Zone
    [ObservableProperty]
    private int _perfectHits;

    [ObservableProperty]
    private int _goodHits;

    [ObservableProperty]
    private int _okHits;

    [ObservableProperty]
    private int _missHits;

    // Ob gerade aufgeheizt wird (fuer visuelle Effekte)
    [ObservableProperty]
    private bool _isHeating = true;

    // Ob gerade gehaemmert wird (kurze Animation)
    [ObservableProperty]
    private bool _isHammering;

    [ObservableProperty]
    private double _speedMultiplier = 1.0;

    // ===================================================================
    // COMPUTED PROPERTIES
    // ===================================================================

    /// <summary>Fortschritts-Anzeige fuer Schlaege: "3/5"</summary>
    public string HitsProgressDisplay => $"{HitsCompleted}/{HitsRequired}";

    partial void OnHitsCompletedChanged(int value) => OnPropertyChanged(nameof(HitsProgressDisplay));
    partial void OnHitsRequiredChanged(int value) => OnPropertyChanged(nameof(HitsProgressDisplay));

    // ===================================================================
    // CONSTRUCTOR
    // ===================================================================

    public ForgeGameViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        IRewardedAdService rewardedAdService,
        ILocalizationService localizationService)
        : base(gameStateService, audioService, rewardedAdService, localizationService)
    {
    }

    // ===================================================================
    // SetOrderId OVERRIDE (GameType dynamisch setzen)
    // ===================================================================

    public override void SetOrderId(string orderId)
    {
        // GameType vor base.SetOrderId setzen, damit GetCurrentMiniGameType() korrekt ist
        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            var currentTask = activeOrder.CurrentTask;
            if (currentTask != null)
                GameType = currentTask.GameType;
        }

        base.SetOrderId(orderId);
    }

    // ===================================================================
    // SPIELLOGIK
    // ===================================================================

    protected override void InitializeGame()
    {
        UpdateGameTypeVisuals();
        InitializeZones();
    }

    protected override Task OnPreGameStartAsync()
    {
        // Werte zuruecksetzen vor Spielstart
        Temperature = 0;
        HitsCompleted = 0;
        PerfectHits = 0;
        GoodHits = 0;
        OkHits = 0;
        MissHits = 0;
        _heatTime = 0;
        _heatDirection = 1.0;

        GameStarted?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    protected override void OnGameTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying) return;

        _heatTime += TICK_INTERVAL_MS / 1000.0;

        // Temperatur steigt mit Sinus-Oszillation (natuerliches Auf und Ab der Esse)
        double heatWave = Math.Sin(_heatTime * 2.5 * SpeedMultiplier) * 0.3;
        double baseHeat = HEAT_RATE * SpeedMultiplier;

        // Temperatur aendern: steigt tendenziell, mit Oszillation
        Temperature += (baseHeat + heatWave * 0.005) * _heatDirection;

        // Natuerliche Abkuehlung (immer leicht, staerker bei hoher Temperatur)
        Temperature -= COOL_DECAY * Temperature * SpeedMultiplier;

        // Grenzen einhalten
        if (Temperature >= 1.0)
        {
            Temperature = 1.0;
            _heatDirection = -0.5;
        }
        else if (Temperature <= 0.0)
        {
            Temperature = 0.0;
            _heatDirection = 1.0;
        }

        // Richtung langsam normalisieren
        if (_heatDirection < 1.0)
        {
            _heatDirection += 0.002 * SpeedMultiplier;
            if (_heatDirection > 1.0) _heatDirection = 1.0;
        }

        IsHeating = _heatDirection > 0;
    }

    private void UpdateGameTypeVisuals()
    {
        string L(string key) => _localizationService.GetString(key);

        GameTitle = L("ForgeGameTitle");
        GameIcon = "Anvil";
        ActionButtonText = L("HammerNow");
        InstructionText = L("HammerAtRightTemperature");
    }

    private void InitializeZones()
    {
        // Zielzonen basierend auf Schwierigkeit
        double perfectSize = Difficulty.GetPerfectZoneSize();

        // Tool-Bonus: Hammer vergroessert die Zielzone
        var hammerTool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Hammer);
        if (hammerTool != null) perfectSize += perfectSize * hammerTool.ZoneBonus;

        double goodSize = perfectSize * 2;
        double okSize = perfectSize * 3;

        // Ziel-Temperatur zufaellig (zwischen 0.3 und 0.8)
        var random = Random.Shared;
        double targetCenter = 0.35 + (random.NextDouble() * 0.3);

        // Zonen-Positionen (zentriert auf Ziel)
        TargetTemperatureWidth = perfectSize;
        TargetTemperatureStart = targetCenter - (perfectSize / 2);

        GoodTemperatureWidth = goodSize;
        GoodTemperatureStart = targetCenter - (goodSize / 2);

        OkTemperatureWidth = okSize;
        OkTemperatureStart = targetCenter - (okSize / 2);

        // Geschwindigkeit basierend auf Schwierigkeit
        SpeedMultiplier = Difficulty.GetSpeedMultiplier();

        // Benoetigte Schlaege basierend auf Schwierigkeit
        HitsRequired = Difficulty switch
        {
            OrderDifficulty.Easy => 3,
            OrderDifficulty.Medium => 5,
            OrderDifficulty.Hard => 7,
            OrderDifficulty.Expert => 10,
            _ => 5
        };

        // Zuruecksetzen
        Temperature = 0;
        HitsCompleted = 0;
        PerfectHits = 0;
        GoodHits = 0;
        OkHits = 0;
        MissHits = 0;
        _heatTime = 0;
        _heatDirection = 1.0;
        IsHeating = true;
        IsHammering = false;
    }

    [RelayCommand]
    private async Task HammerStrikeAsync()
    {
        if (!IsPlaying || _isEnding || IsHammering) return;

        // Hammer-Animation starten
        IsHammering = true;

        // Aktuellen Treffer auswerten
        var hitRating = CalculateHitRating(Temperature);
        string zoneName = hitRating.GetLocalizationKey();

        // Treffer zaehlen
        switch (hitRating)
        {
            case MiniGameRating.Perfect: PerfectHits++; break;
            case MiniGameRating.Good: GoodHits++; break;
            case MiniGameRating.Ok: OkHits++; break;
            default: MissHits++; break;
        }

        HitsCompleted++;

        // Zonen-Treffer Event feuern
        ZoneHit?.Invoke(this, zoneName);

        // Sound abspielen
        var sound = hitRating switch
        {
            MiniGameRating.Perfect => GameSound.Perfect,
            MiniGameRating.Good => GameSound.Good,
            MiniGameRating.Ok => GameSound.ButtonTap,
            _ => GameSound.Miss
        };
        await _audioService.PlaySoundAsync(sound);

        // Temperatur sinkt nach Hammerschlag (Werkstueck kuehlt durch Verformung)
        Temperature = Math.Max(0, Temperature - COOL_RATE);
        _heatDirection = 1.0;

        // Kurze Hammer-Animation-Dauer
        await Task.Delay(150);
        IsHammering = false;

        // Pruefen ob alle Schlaege erledigt
        if (HitsCompleted >= HitsRequired)
        {
            await EndGameAsync();
        }
    }

    private async Task EndGameAsync()
    {
        if (!StopGame()) return;

        var rating = CalculateOverallRating();
        await ShowResultAsync(rating);
    }

    /// <summary>
    /// Bewertet einen einzelnen Hammerschlag basierend auf der Temperatur.
    /// </summary>
    private MiniGameRating CalculateHitRating(double temp)
    {
        if (temp >= TargetTemperatureStart && temp <= TargetTemperatureStart + TargetTemperatureWidth)
            return MiniGameRating.Perfect;

        if (temp >= GoodTemperatureStart && temp <= GoodTemperatureStart + GoodTemperatureWidth)
            return MiniGameRating.Good;

        if (temp >= OkTemperatureStart && temp <= OkTemperatureStart + OkTemperatureWidth)
            return MiniGameRating.Ok;

        return MiniGameRating.Miss;
    }

    /// <summary>
    /// Berechnet das Gesamtergebnis basierend auf allen Schlaegen.
    /// </summary>
    private MiniGameRating CalculateOverallRating()
    {
        if (HitsRequired <= 0) return MiniGameRating.Miss;

        // Punkte: Perfect=3, Good=2, Ok=1, Miss=0
        int totalPoints = PerfectHits * 3 + GoodHits * 2 + OkHits * 1;
        int maxPoints = HitsRequired * 3;
        double ratio = (double)totalPoints / maxPoints;

        if (ratio >= 0.85) return MiniGameRating.Perfect;
        if (ratio >= 0.60) return MiniGameRating.Good;
        if (ratio >= 0.35) return MiniGameRating.Ok;
        return MiniGameRating.Miss;
    }
}
