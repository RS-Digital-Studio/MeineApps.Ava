using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels.MiniGames;

/// <summary>
/// ViewModel für das Kabel-Verbindungs-MiniGame.
/// Spieler muss farbige Kabel von links nach rechts verbinden.
/// </summary>
public sealed partial class WiringGameViewModel : BaseMiniGameViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-SPEZIFISCHE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<Wire> _leftWires = [];

    [ObservableProperty]
    private ObservableCollection<Wire> _rightWires = [];

    [ObservableProperty]
    private int _wireCount = 4;

    [ObservableProperty]
    private int _connectedCount;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime = 30;

    [ObservableProperty]
    private Wire? _selectedLeftWire;

    // ═══════════════════════════════════════════════════════════════════════
    // ABSTRACT/VIRTUAL IMPLEMENTIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    protected override MiniGameType GameMiniGameType => MiniGameType.WiringGame;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public WiringGameViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        IRewardedAdService rewardedAdService,
        ILocalizationService localizationService)
        : base(gameStateService, audioService, rewardedAdService, localizationService)
    {
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIELLOGIK
    // ═══════════════════════════════════════════════════════════════════════

    protected override void InitializeGame()
    {
        (WireCount, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (3, 15),
            OrderDifficulty.Medium => (4, 16),
            OrderDifficulty.Hard => (5, 17),
            OrderDifficulty.Expert => (7, 18),
            _ => (4, 15)
        };

        // Tool-Bonus: Schraubendreher gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Screwdriver);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        ConnectedCount = 0;
        SelectedLeftWire = null;

        GenerateWires();
    }

    private void GenerateWires()
    {
        LeftWires.Clear();
        RightWires.Clear();

        var colors = GetWireColors();
        var random = Random.Shared;

        for (int i = 0; i < WireCount; i++)
        {
            var color = colors[i];
            LeftWires.Add(new Wire { Index = i, WireColor = color, IsLeft = true });
            RightWires.Add(new Wire { Index = i, WireColor = color, IsLeft = false });
        }

        // Rechte Kabel mischen
        var shuffledRight = RightWires.OrderBy(_ => random.Next()).ToList();
        RightWires.Clear();
        for (int i = 0; i < shuffledRight.Count; i++)
        {
            var wire = shuffledRight[i];
            wire.Index = i;
            RightWires.Add(wire);
        }
    }

    private static List<WireColor> GetWireColors() =>
    [
        WireColor.Red, WireColor.Blue, WireColor.Green, WireColor.Yellow,
        WireColor.Orange, WireColor.Purple, WireColor.Cyan
    ];

    protected override async void OnGameTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (!IsPlaying || _isEnding) return;
            TimeRemaining--;
            if (TimeRemaining <= 0)
                await EndGameAsync(false);
        }
        catch
        {
            // Timer-Fehler still behandelt
        }
    }

    [RelayCommand]
    private async Task SelectLeftWireAsync(Wire? wire)
    {
        if (wire == null || !IsPlaying || IsResultShown || wire.IsConnected) return;

        if (SelectedLeftWire != null)
            SelectedLeftWire.IsSelected = false;

        wire.IsSelected = true;
        SelectedLeftWire = wire;
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
    }

    [RelayCommand]
    private async Task SelectRightWireAsync(Wire? wire)
    {
        if (wire == null || !IsPlaying || IsResultShown || wire.IsConnected) return;
        if (SelectedLeftWire == null) return;

        if (SelectedLeftWire.WireColor == wire.WireColor)
        {
            // Korrekte Verbindung
            SelectedLeftWire.IsConnected = true;
            SelectedLeftWire.IsSelected = false;
            wire.IsConnected = true;
            ConnectedCount++;
            await _audioService.PlaySoundAsync(GameSound.Good);

            if (ConnectedCount >= WireCount)
                await EndGameAsync(true);
        }
        else
        {
            // Falsche Verbindung
            wire.HasError = true;
            await _audioService.PlaySoundAsync(GameSound.Miss);
            await Task.Delay(300);
            wire.HasError = false;
        }

        SelectedLeftWire.IsSelected = false;
        SelectedLeftWire = null;
    }

    private async Task EndGameAsync(bool completed)
    {
        if (!StopGame()) return;

        MiniGameRating rating;
        if (completed)
        {
            double timeRatio = (double)TimeRemaining / MaxTime;
            if (timeRatio > 0.6) rating = MiniGameRating.Perfect;
            else if (timeRatio > 0.3) rating = MiniGameRating.Good;
            else rating = MiniGameRating.Ok;
        }
        else
        {
            double completionRatio = (double)ConnectedCount / WireCount;
            rating = completionRatio >= 0.75 ? MiniGameRating.Ok : MiniGameRating.Miss;
        }

        await ShowResultAsync(rating);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

public enum WireColor
{
    Red, Blue, Green, Yellow, Orange, Purple, Cyan
}

/// <summary>Repräsentiert ein einzelnes Kabel im Wiring-Game.</summary>
public partial class Wire : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public int Index { get; set; }
    public WireColor WireColor { get; set; }
    public bool IsLeft { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _hasError;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BorderWidth));
    }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(ContentOpacity));
    }

    partial void OnHasErrorChanged(bool value) => OnPropertyChanged(nameof(BackgroundColor));

    public string ColorHex => WireColor switch
    {
        WireColor.Red => "#FF4444",
        WireColor.Blue => "#4444FF",
        WireColor.Green => "#44FF44",
        WireColor.Yellow => "#FFFF44",
        WireColor.Orange => "#FF8844",
        WireColor.Purple => "#AA44FF",
        WireColor.Cyan => "#00BCD4",
        _ => "#888888"
    };

    public string DisplayColor => ColorHex;

    public string BackgroundColor => HasError
        ? "#40FF4444"
        : IsConnected
            ? "#3000FF00"
            : IsSelected
                ? "#30FFFFFF"
                : "Transparent";

    public double ContentOpacity => IsConnected ? 0.5 : 1.0;
    public double BorderWidth => IsSelected ? 4 : 3;
}
