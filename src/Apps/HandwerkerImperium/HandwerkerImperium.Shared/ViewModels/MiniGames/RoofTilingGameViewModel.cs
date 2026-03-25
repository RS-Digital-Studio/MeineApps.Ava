using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using HandwerkerImperium.Helpers;

namespace HandwerkerImperium.ViewModels.MiniGames;

/// <summary>
/// ViewModel für das Dachziegel-Muster-Puzzle.
/// Der Spieler muss fehlende Dachziegel in korrekten Farben platzieren.
/// </summary>
public sealed partial class RoofTilingGameViewModel : BaseMiniGameViewModel
{
    // Farb-Palette für Dachziegel (kontrastreich, gut unterscheidbar)
    private static readonly string[] TileColors =
    {
        "#C62828", // Klassisch Rot
        "#D4763A", // Terrakotta
        "#5D4037", // Dunkelbraun
        "#F9A825", // Sandgelb
        "#37474F", // Schiefer-Grau
        "#6D4C41"  // Mittelbraun
    };

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-SPEZIFISCHE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<RoofTile> _tiles = [];

    [ObservableProperty]
    private ObservableCollection<string> _availableColors = [];

    [ObservableProperty]
    private string _selectedColor = "";

    [ObservableProperty]
    private int _mistakeCount;

    [ObservableProperty]
    private int _placedCount;

    [ObservableProperty]
    private int _totalToPlace;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime = 45;

    [ObservableProperty]
    private int _gridColumns = 5;

    [ObservableProperty]
    private int _gridRows = 4;

    // Hinweis: Farbpalette pulsieren wenn keine Farbe gewählt
    [ObservableProperty]
    private bool _selectColorHint;

    /// <summary>Breite des Tile-Grids in Pixeln für WrapPanel.</summary>
    public double TileGridWidth => GridColumns * 54;

    partial void OnGridColumnsChanged(int value) => OnPropertyChanged(nameof(TileGridWidth));

    // ═══════════════════════════════════════════════════════════════════════
    // ABSTRACT/VIRTUAL IMPLEMENTIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    protected override MiniGameType GameMiniGameType => MiniGameType.RoofTiling;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public RoofTilingGameViewModel(
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
        int colorCount;
        double hintPercentage;

        (GridColumns, GridRows, MaxTime, colorCount, hintPercentage) = Difficulty switch
        {
            OrderDifficulty.Easy => (3, 3, 45, 3, 0.55),
            OrderDifficulty.Medium => (4, 4, 50, 4, 0.40),
            OrderDifficulty.Hard => (5, 4, 50, 5, 0.30),
            OrderDifficulty.Expert => (5, 5, 45, 5, 0.25),
            _ => (4, 4, 50, 4, 0.40)
        };

        // Tool-Bonus: Hammer gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Hammer);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        PlacedCount = 0;
        MistakeCount = 0;
        SelectedColor = "";

        // Verfügbare Farben setzen
        AvailableColors.Clear();
        for (int i = 0; i < colorCount; i++)
            AvailableColors.Add(TileColors[i]);

        GenerateGrid(colorCount, hintPercentage);
    }

    private void GenerateGrid(int colorCount, double hintPercentage)
    {
        Tiles.Clear();
        int totalTiles = GridColumns * GridRows;

        var pattern = GenerateRoofPattern(colorCount);

        // Hinweis-Ziegel bestimmen
        int hintCount = (int)(totalTiles * hintPercentage);
        var hintIndices = new HashSet<int>();

        // Jede Reihe mindestens 1 Hint
        for (int row = 0; row < GridRows; row++)
        {
            int startIdx = row * GridColumns;
            int colIdx = Random.Shared.Next(GridColumns);
            hintIndices.Add(startIdx + colIdx);
        }

        while (hintIndices.Count < hintCount)
            hintIndices.Add(Random.Shared.Next(totalTiles));

        // Ziegel erstellen
        for (int i = 0; i < totalTiles; i++)
        {
            int row = i / GridColumns;
            int col = i % GridColumns;
            string correctColor = pattern[row, col];
            bool isHint = hintIndices.Contains(i);

            Tiles.Add(new RoofTile
            {
                Row = row,
                Column = col,
                Index = i,
                CorrectColor = correctColor,
                IsHint = isHint,
                IsPlaced = isHint,
                CurrentColor = isHint ? correctColor : ""
            });
        }

        TotalToPlace = totalTiles - hintCount;
    }

    private string[,] GenerateRoofPattern(int colorCount)
    {
        var pattern = new string[GridRows, GridColumns];
        var colors = TileColors.Take(colorCount).ToArray();
        int patternType = Random.Shared.Next(3);

        switch (patternType)
        {
            case 0: // Diagonales Streifenmuster
                for (int row = 0; row < GridRows; row++)
                    for (int col = 0; col < GridColumns; col++)
                        pattern[row, col] = colors[(row + col) % colorCount];
                break;

            case 1: // Schachbrett mit Versatz
                for (int row = 0; row < GridRows; row++)
                {
                    int offset = row % 2;
                    for (int col = 0; col < GridColumns; col++)
                        pattern[row, col] = colors[(col + offset) % colorCount];
                }
                break;

            case 2: // Blockmuster (2er-Blöcke)
                for (int row = 0; row < GridRows; row++)
                    for (int col = 0; col < GridColumns; col++)
                        pattern[row, col] = colors[(row / 2 + col / 2) % colorCount];
                break;
        }

        return pattern;
    }

    protected override async void OnGameTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (!IsPlaying || _isEnding) return;
            TimeRemaining--;
            if (TimeRemaining <= 0)
                await EndGameAsync();
        }
        catch
        {
            // Timer-Fehler still behandelt
        }
    }

    [RelayCommand]
    private void SelectColor(string? color)
    {
        if (color == null || !IsPlaying) return;
        SelectedColor = color;
    }

    [RelayCommand]
    private async Task PlaceTileAsync(RoofTile? tile)
    {
        if (tile == null || !IsPlaying || IsResultShown) return;
        if (tile.IsPlaced || tile.IsHint) return;

        // Keine Farbe gewählt
        if (string.IsNullOrEmpty(SelectedColor))
        {
            SelectColorHint = true;
            ResetSelectColorHintAsync().SafeFireAndForget();
            return;
        }

        tile.CurrentColor = SelectedColor;

        if (SelectedColor == tile.CorrectColor)
        {
            tile.IsPlaced = true;
            tile.HasError = false;
            PlacedCount++;
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

            if (PlacedCount >= TotalToPlace)
                await EndGameAsync();
        }
        else
        {
            tile.HasError = true;
            MistakeCount++;
            await _audioService.PlaySoundAsync(GameSound.Miss);
            await Task.Delay(400);
            tile.HasError = false;
            tile.CurrentColor = "";
        }
    }

    private async Task EndGameAsync()
    {
        if (!StopGame()) return;

        bool allPlaced = PlacedCount >= TotalToPlace;
        double timeRatio = MaxTime > 0 ? (double)TimeRemaining / MaxTime : 0;

        MiniGameRating rating;
        if (allPlaced && MistakeCount == 0 && timeRatio > 0.50)
            rating = MiniGameRating.Perfect;
        else if (allPlaced && MistakeCount <= 2 && timeRatio > 0.25)
            rating = MiniGameRating.Good;
        else if (allPlaced && MistakeCount <= 8)
            rating = MiniGameRating.Ok;
        else if (!allPlaced && TotalToPlace > 0)
        {
            double placedRatio = (double)PlacedCount / TotalToPlace;
            if (placedRatio >= 0.90 && MistakeCount <= 2) rating = MiniGameRating.Good;
            else if (placedRatio >= 0.70 && MistakeCount <= 4) rating = MiniGameRating.Ok;
            else rating = MiniGameRating.Miss;
        }
        else
            rating = MiniGameRating.Miss;

        await ShowResultAsync(rating);
    }

    private async Task ResetSelectColorHintAsync()
    {
        await Task.Delay(1000);
        SelectColorHint = false;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Repräsentiert einen einzelnen Dachziegel im Gitter.</summary>
public partial class RoofTile : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public int Row { get; set; }
    public int Column { get; set; }
    public int Index { get; set; }

    [ObservableProperty]
    private string _correctColor = "";

    [ObservableProperty]
    private string _currentColor = "";

    [ObservableProperty]
    private bool _isPlaced;

    [ObservableProperty]
    private bool _isHint;

    [ObservableProperty]
    private bool _hasError;

    partial void OnIsPlacedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayColor));
        OnPropertyChanged(nameof(BorderColor));
    }

    partial void OnCurrentColorChanged(string value) => OnPropertyChanged(nameof(DisplayColor));
    partial void OnHasErrorChanged(bool value) => OnPropertyChanged(nameof(BorderColor));
    partial void OnIsHintChanged(bool value) => OnPropertyChanged(nameof(BorderColor));

    public string DisplayColor => IsPlaced
        ? CorrectColor
        : !string.IsNullOrEmpty(CurrentColor)
            ? CurrentColor
            : "#3A3A3A";

    public string BorderColor => IsHint
        ? "#FFD700"
        : HasError
            ? "#F44336"
            : IsPlaced
                ? "#4CAF50"
                : "#555555";
}
