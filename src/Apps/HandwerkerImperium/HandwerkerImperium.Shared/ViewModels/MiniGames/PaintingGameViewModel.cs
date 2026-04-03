using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels.MiniGames;

/// <summary>
/// ViewModel für das Maler-MiniGame.
/// Spieler muss alle Zielzellen bemalen ohne daneben zu streichen.
/// Hat ein Combo-System das die Belohnung erhöht.
/// </summary>
public sealed partial class PaintingGameViewModel : BaseMiniGameViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS (spiel-spezifisch)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Event für Combo-Animation in der View.</summary>
    public event EventHandler? ComboIncreased;

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-SPEZIFISCHE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<PaintCell> _cells = [];

    [ObservableProperty]
    private int _gridSize = 5;

    [ObservableProperty]
    private int _targetCellCount;

    [ObservableProperty]
    private int _paintedTargetCount;

    [ObservableProperty]
    private int _mistakeCount;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime = 30;

    [ObservableProperty]
    private string _selectedColor = "#4169E1";

    [ObservableProperty]
    private double _paintProgress;

    // Combo-System
    [ObservableProperty]
    private int _comboCount;

    [ObservableProperty]
    private string _comboDisplay = "";

    [ObservableProperty]
    private bool _isComboActive;

    private int _bestCombo;

    /// <summary>Combo-Multiplikator: Staffel +0.25x pro 5 fehlerfreie Treffer (0-4=1.0x, 5-9=1.25x, 10-14=1.5x)</summary>
    public decimal ComboMultiplier => 1.0m + (_bestCombo / 5) * 0.25m;

    /// <summary>Breite des Paint-Grids in Pixeln für WrapPanel.</summary>
    public double PaintGridWidth => GridSize * 54;

    partial void OnGridSizeChanged(int value) => OnPropertyChanged(nameof(PaintGridWidth));

    // ═══════════════════════════════════════════════════════════════════════
    // ABSTRACT/VIRTUAL IMPLEMENTIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    protected override MiniGameType GameMiniGameType => MiniGameType.PaintingGame;

    /// <summary>PaintingGame: Combo-Multiplikator auf Belohnungen anwenden.</summary>
    protected override void CalculateAndSetRewards()
    {
        var comboMult = ComboMultiplier;
        var order = _gameStateService.GetActiveOrder();
        if (order != null && IsLastTask)
        {
            // Combo-Multiplikator auf Order setzen für Auszahlung in CompleteActiveOrder()
            order.ComboMultiplier = comboMult;
            RewardAmount = order.FinalReward * _gameStateService.GetOrderRewardMultiplier(order) * comboMult;
            XpAmount = (int)(order.FinalXp * comboMult);
        }
        else if (order == null)
        {
            var quickJob = _gameStateService.State.ActiveQuickJob;
            RewardAmount = (quickJob?.Reward ?? 0) * comboMult;
            XpAmount = (int)((quickJob?.XpReward ?? 0) * comboMult);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public PaintingGameViewModel(
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
        (GridSize, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (4, 24),
            OrderDifficulty.Medium => (5, 28),
            OrderDifficulty.Hard => (5, 22),
            OrderDifficulty.Expert => (6, 30),
            _ => (5, 28)
        };

        // Tool-Bonus: Pinsel gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Paintbrush);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        PaintedTargetCount = 0;
        MistakeCount = 0;
        PaintProgress = 0;
        ComboCount = 0;
        _bestCombo = 0;
        IsComboActive = false;
        ComboDisplay = "";

        SelectedColor = GetRandomPaintColor();
        GenerateCanvas();
    }

    private void GenerateCanvas()
    {
        Cells.Clear();
        var targetPattern = GenerateTargetPattern();

        for (int row = 0; row < GridSize; row++)
        {
            for (int col = 0; col < GridSize; col++)
            {
                Cells.Add(new PaintCell
                {
                    Row = row,
                    Column = col,
                    Index = row * GridSize + col,
                    IsTarget = targetPattern[row, col],
                    TargetColor = SelectedColor
                });
            }
        }

        TargetCellCount = Cells.Count(c => c.IsTarget);
    }

    private bool[,] GenerateTargetPattern()
    {
        var pattern = new bool[GridSize, GridSize];
        int shapeType = Random.Shared.Next(3);

        switch (shapeType)
        {
            case 0: GenerateRectangle(pattern); break;
            case 1: GenerateLShape(pattern); break;
            case 2: GenerateTShape(pattern); break;
        }

        return pattern;
    }

    private void GenerateRectangle(bool[,] pattern)
    {
        int startRow = Random.Shared.Next(0, GridSize / 2);
        int startCol = Random.Shared.Next(0, GridSize / 2);
        int height = Random.Shared.Next(2, GridSize - startRow);
        int width = Random.Shared.Next(2, GridSize - startCol);

        for (int r = startRow; r < startRow + height; r++)
            for (int c = startCol; c < startCol + width; c++)
                pattern[r, c] = true;
    }

    private void GenerateLShape(bool[,] pattern)
    {
        int startCol = Random.Shared.Next(1, GridSize - 2);
        for (int r = 0; r < GridSize - 1; r++)
            pattern[r, startCol] = true;
        for (int c = startCol; c < GridSize; c++)
            pattern[GridSize - 2, c] = true;
    }

    private void GenerateTShape(bool[,] pattern)
    {
        int midCol = GridSize / 2;
        for (int r = 0; r < GridSize; r++)
            pattern[r, midCol] = true;
        for (int c = 1; c < GridSize - 1; c++)
            pattern[1, c] = true;
    }

    private static string GetRandomPaintColor()
    {
        var colors = new[] { "#4169E1", "#32CD32", "#FF6347", "#FFD700", "#9370DB", "#20B2AA" };
        return colors[Random.Shared.Next(colors.Length)];
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
    private async Task PaintCellAsync(PaintCell? cell)
    {
        if (cell == null || !IsPlaying || IsResultShown || cell.IsPainted) return;

        cell.IsPainted = true;
        cell.PaintedAt = DateTime.UtcNow;
        cell.PaintColor = SelectedColor;

        if (cell.IsTarget)
        {
            PaintedTargetCount++;
            ComboCount++;
            if (ComboCount > _bestCombo) _bestCombo = ComboCount;

            if (ComboCount >= 3)
            {
                IsComboActive = true;
                ComboDisplay = string.Format(_localizationService.GetString("ComboX"), ComboCount);
                ComboIncreased?.Invoke(this, EventArgs.Empty);
                await _audioService.PlaySoundAsync(GameSound.ComboHit);
            }
            else
            {
                await _audioService.PlaySoundAsync(GameSound.ButtonTap);
            }
        }
        else
        {
            MistakeCount++;
            cell.HasError = true;
            ComboCount = 0;
            IsComboActive = false;
            ComboDisplay = "";
            await _audioService.PlaySoundAsync(GameSound.Miss);
        }

        PaintProgress = TargetCellCount > 0 ? (double)PaintedTargetCount / TargetCellCount : 0;

        if (PaintedTargetCount >= TargetCellCount)
            await EndGameAsync();
    }

    private async Task EndGameAsync()
    {
        if (!StopGame()) return;

        double completionRatio = TargetCellCount > 0 ? (double)PaintedTargetCount / TargetCellCount : 0;
        int totalAttempts = PaintedTargetCount + MistakeCount;
        double accuracy = totalAttempts > 0 ? (double)PaintedTargetCount / totalAttempts : 0;

        MiniGameRating rating;
        if (completionRatio >= 1.0 && MistakeCount == 0) rating = MiniGameRating.Perfect;
        else if (completionRatio >= 0.9 && accuracy >= 0.8) rating = MiniGameRating.Good;
        else if (completionRatio >= 0.7 && accuracy >= 0.6) rating = MiniGameRating.Ok;
        else rating = MiniGameRating.Miss;

        await ShowResultAsync(rating);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Repräsentiert eine Zelle im Maler-Canvas.</summary>
public partial class PaintCell : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public int Row { get; set; }
    public int Column { get; set; }
    public int Index { get; set; }
    public bool IsTarget { get; set; }
    public string TargetColor { get; set; } = "#FFFFFF";

    [ObservableProperty]
    private bool _isPainted;

    public DateTime PaintedAt { get; set; }

    [ObservableProperty]
    private string _paintColor = "Transparent";

    [ObservableProperty]
    private bool _hasError;

    partial void OnIsPaintedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayColor));
        OnPropertyChanged(nameof(IsPaintedCorrectly));
    }

    partial void OnPaintColorChanged(string value) => OnPropertyChanged(nameof(DisplayColor));

    public string DisplayColor => IsPainted ? PaintColor : IsTarget ? "#30FFFFFF" : "#4A5568";
    public string BorderColor => IsTarget ? "#60FFFFFF" : "#2D3748";
    public bool IsPaintedCorrectly => IsTarget && IsPainted;
}
