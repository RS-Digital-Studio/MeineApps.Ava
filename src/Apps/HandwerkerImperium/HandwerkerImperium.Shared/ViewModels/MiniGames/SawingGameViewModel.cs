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
        // Zonen-Größen basierend auf Schwierigkeit
        double perfectSize = Difficulty.GetPerfectZoneSize();
        // Tool-Bonus: Säge vergrößert die Zielzone
        var sawTool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Saw);
        if (sawTool != null) perfectSize += perfectSize * sawTool.ZoneBonus;
        double goodSize = perfectSize * 2;
        double okSize = perfectSize * 3;

        // Zufällige Zielposition (zwischen 0.2 und 0.8)
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
        SpeedMultiplier = Difficulty.GetSpeedMultiplier();

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

        // Marker bewegen
        MarkerPosition += MARKER_SPEED * SpeedMultiplier * _direction;

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
