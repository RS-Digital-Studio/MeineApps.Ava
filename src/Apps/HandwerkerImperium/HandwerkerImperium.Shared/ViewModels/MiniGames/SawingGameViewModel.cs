using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels.MiniGames;

/// <summary>
/// ViewModel für das Säge-MiniGame.
/// Spieler muss den Marker in der Zielzone stoppen.
/// Deckt 4 Sub-Typen ab: Sawing, Planing, TileLaying, Measuring.
/// </summary>
public sealed partial class SawingGameViewModel : BaseMiniGameViewModel
{
    // Spiel-Konfiguration
    private const double TICK_INTERVAL_MS = 16; // ~60 FPS
    private const double MARKER_SPEED = 0.017;  // Units pro Tick (0.0-1.0)
    private const double BAR_WIDTH = 300.0;     // Timing-Bar Breite in Pixel

    // Sub-Typ-spezifische Gameplay-Modifikationen
    private double _planingZoneShrink = 1.0;     // Planing: Zonen-Verkleinerung (0.7 = 30% kleiner)
    private double _planingSpeedFactor = 0.75;   // Planing: Langsamerer Marker
    private double _tileLayingAcceleration;       // TileLaying: Beschleunigung pro Tick
    private int _tileLayingTickCount;             // TileLaying: Tick-Zähler für Beschleunigung
    private double _measuringZoneDrift;           // Measuring: Zonen-Drift-Geschwindigkeit
    private int _measuringDriftDirection = 1;     // Measuring: Drift-Richtung

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS (spiel-spezifisch)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Wird beim Spielstart nach Countdown gefeuert.</summary>
    public event EventHandler? GameStarted;

    /// <summary>Wird bei Zonen-Treffer gefeuert (Zone-Name: "Perfect", "Good", "Ok", "Miss").</summary>
    public event EventHandler<string>? ZoneHit;

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-SPEZIFISCHE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private MiniGameType _gameType = MiniGameType.Sawing;

    [ObservableProperty]
    private string _gameTitle = "";

    [ObservableProperty]
    private string _gameIcon = "Saw";

    [ObservableProperty]
    private string _actionButtonText = "";

    [ObservableProperty]
    private string _instructionText = "";

    [ObservableProperty]
    private double _markerPosition; // 0.0 to 1.0

    [ObservableProperty]
    private double _perfectZoneStart;

    [ObservableProperty]
    private double _perfectZoneWidth;

    [ObservableProperty]
    private double _goodZoneStart;

    [ObservableProperty]
    private double _goodZoneWidth;

    [ObservableProperty]
    private double _okZoneStart;

    [ObservableProperty]
    private double _okZoneWidth;

    [ObservableProperty]
    private double _speedMultiplier = 1.0;

    // Marker-Richtung (1 = rechts, -1 = links)
    private int _direction = 1;

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES FÜR VIEW BINDING
    // ═══════════════════════════════════════════════════════════════════════

    public double OkZonePixelWidth => OkZoneWidth * BAR_WIDTH;
    public Avalonia.Thickness OkZoneMargin => new(OkZoneStart * BAR_WIDTH, 0, 0, 0);

    public double GoodZonePixelWidth => GoodZoneWidth * BAR_WIDTH;
    public Avalonia.Thickness GoodZoneMargin => new(GoodZoneStart * BAR_WIDTH, 0, 0, 0);

    public double PerfectZonePixelWidth => PerfectZoneWidth * BAR_WIDTH;
    public Avalonia.Thickness PerfectZoneMargin => new(PerfectZoneStart * BAR_WIDTH, 0, 0, 0);

    public Avalonia.Thickness MarkerMargin => new(MarkerPosition * BAR_WIDTH - 3, 0, 0, 0);

    partial void OnMarkerPositionChanged(double value) => OnPropertyChanged(nameof(MarkerMargin));

    partial void OnOkZoneStartChanged(double value) => OnPropertyChanged(nameof(OkZoneMargin));
    partial void OnOkZoneWidthChanged(double value) => OnPropertyChanged(nameof(OkZonePixelWidth));

    partial void OnGoodZoneStartChanged(double value) => OnPropertyChanged(nameof(GoodZoneMargin));
    partial void OnGoodZoneWidthChanged(double value) => OnPropertyChanged(nameof(GoodZonePixelWidth));

    partial void OnPerfectZoneStartChanged(double value) => OnPropertyChanged(nameof(PerfectZoneMargin));
    partial void OnPerfectZoneWidthChanged(double value) => OnPropertyChanged(nameof(PerfectZonePixelWidth));

    // ═══════════════════════════════════════════════════════════════════════
    // ABSTRACT/VIRTUAL IMPLEMENTIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    protected override MiniGameType GameMiniGameType => MiniGameType.Sawing;
    protected override TimeSpan TimerInterval => TimeSpan.FromMilliseconds(TICK_INTERVAL_MS);
    protected override bool PlaySoundBeforeCountdown => false;

    // SawingGame deckt 4 Sub-Typen ab, GameType wird dynamisch gesetzt
    protected override MiniGameType GetCurrentMiniGameType() => GameType;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public SawingGameViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        IRewardedAdService rewardedAdService,
        ILocalizationService localizationService)
        : base(gameStateService, audioService, rewardedAdService, localizationService)
    {
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALISIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    public override void SetOrderId(string orderId)
    {
        // GameType vom aktuellen Task setzen (vor base.SetOrderId für Tutorial/AutoComplete)
        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder?.CurrentTask != null)
        {
            GameType = activeOrder.CurrentTask.GameType;
            UpdateGameTypeVisuals();
        }

        base.SetOrderId(orderId);
    }

    private void UpdateGameTypeVisuals()
    {
        string L(string key) => _localizationService.GetString(key);

        (GameTitle, GameIcon, ActionButtonText, InstructionText) = GameType switch
        {
            MiniGameType.Sawing => (L("SawingTitle"), "Saw", L("SawNow"), L("StopInGreenZone")),
            MiniGameType.Planing => (L("PlaningTitle"), "Axe", L("PlaneNow"), L("StopForSmoothSurface")),
            MiniGameType.TileLaying => (L("TileLayingTitle"), "ViewDashboard", L("LayNow"), L("StopAtPerfectMoment")),
            MiniGameType.Measuring => (L("MeasuringTitle"), "Ruler", L("MeasureNow"), L("StopAtRightLength")),
            _ => (L("SawingTitle"), "Saw", L("SawNow"), L("StopInGreenZone"))
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIELLOGIK
    // ═══════════════════════════════════════════════════════════════════════

    protected override void InitializeGame()
    {
        // Sub-Typ-Parameter zurücksetzen
        _planingZoneShrink = 1.0;
        _planingSpeedFactor = 1.0;
        _tileLayingAcceleration = 0;
        _tileLayingTickCount = 0;
        _measuringZoneDrift = 0;
        _measuringDriftDirection = 1;

        // Zonen-Größen basierend auf Schwierigkeit
        double perfectSize = Difficulty.GetPerfectZoneSize();
        // Tool-Bonus: Säge vergrößert die Zielzone
        var sawTool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Saw);
        if (sawTool != null) perfectSize += perfectSize * sawTool.ZoneBonus;

        // Sub-Typ-Modifikationen auf Gameplay-Parameter
        double speedMultiplier = Difficulty.GetSpeedMultiplier();
        switch (GameType)
        {
            case MiniGameType.Planing:
                // Planing: Langsamerer Marker, aber 30% kleinere Zonen (Präzisions-Arbeit)
                _planingZoneShrink = 0.70;
                _planingSpeedFactor = 0.75;
                perfectSize *= _planingZoneShrink;
                speedMultiplier *= _planingSpeedFactor;
                break;

            case MiniGameType.TileLaying:
                // TileLaying: Marker wird mit der Zeit schneller (Zeitdruck steigt)
                _tileLayingAcceleration = 0.00008 * speedMultiplier;
                break;

            case MiniGameType.Measuring:
                // Measuring: Zielzone driftet langsam hin und her (bewegliches Ziel)
                _measuringZoneDrift = 0.0008 * speedMultiplier;
                break;
        }

        double goodSize = perfectSize * 2;
        double okSize = perfectSize * 3;

        // Zufällige Zielposition (zwischen 0.3 und 0.7)
        var random = Random.Shared;
        double targetCenter = 0.3 + (random.NextDouble() * 0.4);

        // Zonen-Positionen (zentriert auf Ziel)
        PerfectZoneWidth = perfectSize;
        PerfectZoneStart = targetCenter - (perfectSize / 2);
        GoodZoneWidth = goodSize;
        GoodZoneStart = targetCenter - (goodSize / 2);
        OkZoneWidth = okSize;
        OkZoneStart = targetCenter - (okSize / 2);

        // Geschwindigkeit je nach Schwierigkeit
        SpeedMultiplier = speedMultiplier;

        // Marker zurücksetzen
        MarkerPosition = 0;
        _direction = 1;
    }

    protected override Task OnPreGameStartAsync()
    {
        MarkerPosition = 0;
        _direction = 1;
        GameStarted?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    protected override void OnGameTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying) return;

        // TileLaying: Marker beschleunigt mit der Zeit (Zeitdruck steigt)
        double currentSpeed = SpeedMultiplier;
        if (GameType == MiniGameType.TileLaying && _tileLayingAcceleration > 0)
        {
            _tileLayingTickCount++;
            // Beschleunigung bis max 1.6x der Startgeschwindigkeit
            currentSpeed += Math.Min(_tileLayingAcceleration * _tileLayingTickCount, SpeedMultiplier * 0.6);
        }

        // Marker bewegen
        MarkerPosition += MARKER_SPEED * currentSpeed * _direction;

        // An Kanten abprallen
        if (MarkerPosition >= 1.0)
        {
            MarkerPosition = 1.0;
            _direction = -1;
        }
        else if (MarkerPosition <= 0.0)
        {
            MarkerPosition = 0.0;
            _direction = 1;
        }

        // Measuring: Zielzone driftet langsam hin und her
        if (GameType == MiniGameType.Measuring && _measuringZoneDrift > 0)
        {
            double drift = _measuringZoneDrift * _measuringDriftDirection;

            // Neue Positionen berechnen
            double newPerfectStart = PerfectZoneStart + drift;
            double newOkStart = OkZoneStart + drift;

            // An Grenzen umkehren (Zonen bleiben innerhalb 0.05 - 0.95)
            if (newOkStart + OkZoneWidth > 0.95 || newOkStart < 0.05)
            {
                _measuringDriftDirection *= -1;
            }
            else
            {
                PerfectZoneStart = newPerfectStart;
                GoodZoneStart += drift;
                OkZoneStart = newOkStart;
            }
        }
    }

    [RelayCommand]
    private async Task StopMarkerAsync()
    {
        if (!StopGame()) return;

        // Rating berechnen
        var rating = CalculateRating(MarkerPosition);

        // Zonen-Treffer Event feuern
        ZoneHit?.Invoke(this, rating.GetLocalizationKey());

        await ShowResultAsync(rating);
    }

    private static MiniGameRating CalculateRating(double position, double perfectStart, double perfectWidth,
        double goodStart, double goodWidth, double okStart, double okWidth)
    {
        if (position >= perfectStart && position <= perfectStart + perfectWidth)
            return MiniGameRating.Perfect;
        if (position >= goodStart && position <= goodStart + goodWidth)
            return MiniGameRating.Good;
        if (position >= okStart && position <= okStart + okWidth)
            return MiniGameRating.Ok;
        return MiniGameRating.Miss;
    }

    private MiniGameRating CalculateRating(double position) =>
        CalculateRating(position, PerfectZoneStart, PerfectZoneWidth,
            GoodZoneStart, GoodZoneWidth, OkZoneStart, OkZoneWidth);
}
