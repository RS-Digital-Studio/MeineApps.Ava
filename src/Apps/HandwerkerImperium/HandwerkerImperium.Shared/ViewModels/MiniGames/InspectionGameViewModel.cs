using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels.MiniGames;

/// <summary>
/// ViewModel fuer das Inspektions-MiniGame (Baustelleninspektion / Fehlersuche).
/// Spieler muss Fehler auf einer Baustelle finden, indem er fehlerhafte Felder antippt.
/// </summary>
public sealed partial class InspectionGameViewModel : BaseMiniGameViewModel
{
    // Korrekte Baustellen-Elemente (gut)
    private static readonly string[] GoodIcons = { "brick", "wood", "bolt", "ladder", "crane", "wrench", "gear", "beam" };
    // Fehlerhafte Elemente (Maengel)
    private static readonly string[] DefectIcons = { "warning", "barrier", "crack", "fire", "cross", "stop", "hole", "leak" };

    // ═══════════════════════════════════════════════════════════════════════
    // BASIS-KLASSE KONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════

    protected override MiniGameType GameMiniGameType => MiniGameType.Inspection;

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-SPEZIFISCHE OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<InspectionCell> _cells = [];

    [ObservableProperty]
    private int _foundDefects;

    [ObservableProperty]
    private int _totalDefects;

    [ObservableProperty]
    private int _falseAlarms;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime = 35;

    // Grid-Dimensionen
    private int _gridColumns = 4;
    private int _gridRows = 4;

    /// <summary>Spaltenanzahl (fuer SkiaSharp-Renderer).</summary>
    public int GridColumns => _gridColumns;

    /// <summary>Zeilenanzahl (fuer SkiaSharp-Renderer).</summary>
    public int GridRows => _gridRows;

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Breite des Inspektions-Grids in Pixeln fuer WrapPanel.
    /// Jede Zelle ist 60px + 4px Margin = 64px.
    /// </summary>
    public double GridWidth => _gridColumns * 64;

    /// <summary>
    /// Fortschrittsanzeige als Prozent (0.0 bis 1.0).
    /// </summary>
    public double InspectionProgress => TotalDefects > 0
        ? (double)FoundDefects / TotalDefects
        : 0;

    partial void OnFoundDefectsChanged(int value) => OnPropertyChanged(nameof(InspectionProgress));
    partial void OnTotalDefectsChanged(int value) => OnPropertyChanged(nameof(InspectionProgress));

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public InspectionGameViewModel(
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
        // Grid-Groesse und Zeit je nach Schwierigkeit
        (_gridColumns, _gridRows, MaxTime, var defectCount) = Difficulty switch
        {
            OrderDifficulty.Easy => (4, 4, 45, 3),
            OrderDifficulty.Medium => (5, 4, 35, 5),
            OrderDifficulty.Hard => (5, 5, 28, 7),
            OrderDifficulty.Expert => (6, 5, 28, 9),
            _ => (5, 4, 35, 5)
        };

        OnPropertyChanged(nameof(GridWidth));

        // Tool-Bonus: Lupe gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Magnifier);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        FoundDefects = 0;
        TotalDefects = defectCount;
        FalseAlarms = 0;
        IsPlaying = false;
        IsResultShown = false;

        GenerateGrid(defectCount);
    }

    protected override async void OnGameTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (!IsPlaying || _isEnding) return;

            TimeRemaining--;

            if (TimeRemaining <= 0)
            {
                await EndGameAsync();
            }
        }
        catch
        {
            // Timer-Fehler still behandelt
        }
    }

    private void GenerateGrid(int defectCount)
    {
        Cells.Clear();

        int totalCells = _gridColumns * _gridRows;
        var allIndices = Enumerable.Range(0, totalCells).ToList();

        // Zufaellige Positionen fuer Fehler auswaehlen
        var defectPositions = new HashSet<int>();
        while (defectPositions.Count < defectCount && allIndices.Count > 0)
        {
            int randIndex = Random.Shared.Next(allIndices.Count);
            defectPositions.Add(allIndices[randIndex]);
            allIndices.RemoveAt(randIndex);
        }

        for (int i = 0; i < totalCells; i++)
        {
            bool hasDefect = defectPositions.Contains(i);
            Cells.Add(new InspectionCell
            {
                Index = i,
                Row = i / _gridColumns,
                Column = i % _gridColumns,
                HasDefect = hasDefect,
                Icon = hasDefect
                    ? DefectIcons[Random.Shared.Next(DefectIcons.Length)]
                    : GoodIcons[Random.Shared.Next(GoodIcons.Length)]
            });
        }
    }

    /// <summary>
    /// Feld untersuchen - Spieler tippt auf ein Baustellen-Feld.
    /// </summary>
    [RelayCommand]
    private async Task InspectCellAsync(InspectionCell? cell)
    {
        if (cell == null || !IsPlaying || _isEnding || cell.IsInspected) return;

        cell.IsInspected = true;

        if (cell.HasDefect)
        {
            cell.IsDefectFound = true;
            FoundDefects++;
            await _audioService.PlaySoundAsync(GameSound.Good);
        }
        else
        {
            cell.IsFalseAlarm = true;
            FalseAlarms++;
            await _audioService.PlaySoundAsync(GameSound.Miss);
        }

        // Pruefen ob alle Fehler gefunden wurden
        if (FoundDefects >= TotalDefects)
        {
            await EndGameAsync();
        }
    }

    private async Task EndGameAsync()
    {
        if (!StopGame()) return;

        // Alle nicht-gefundenen Fehler aufdecken
        foreach (var cell in Cells.Where(c => c.HasDefect && !c.IsInspected))
        {
            cell.IsInspected = true;
        }

        var rating = CalculateRating();
        await ShowResultAsync(rating);
    }

    /// <summary>
    /// Rating-Berechnung basierend auf gefundenen Fehlern, Fehl-Taps und verbleibender Zeit.
    /// - Perfect: Alle Fehler + 0 Fehl-Taps + >40% Zeit uebrig
    /// - Good: Alle Fehler + maximal 2 Fehl-Taps
    /// - Ok: Mindestens 50% der Fehler gefunden
    /// - Miss: Weniger als 50% gefunden oder Zeit abgelaufen ohne Ergebnis
    /// </summary>
    private MiniGameRating CalculateRating()
    {
        double timeRatio = MaxTime > 0 ? (double)TimeRemaining / MaxTime : 0;
        double defectRatio = TotalDefects > 0 ? (double)FoundDefects / TotalDefects : 0;

        if (defectRatio >= 1.0 && FalseAlarms == 0 && timeRatio > 0.4)
        {
            return MiniGameRating.Perfect;
        }

        if (defectRatio >= 1.0 && FalseAlarms <= 2)
        {
            return MiniGameRating.Good;
        }

        if (defectRatio >= 0.5)
        {
            return MiniGameRating.Ok;
        }

        return MiniGameRating.Miss;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Repraesentiert ein einzelnes Feld auf der Baustelle.
/// </summary>
public partial class InspectionCell : ObservableObject
{
    public int Index { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }

    [ObservableProperty]
    private string _icon = "";

    [ObservableProperty]
    private bool _hasDefect;

    [ObservableProperty]
    private bool _isInspected;

    [ObservableProperty]
    private bool _isDefectFound;

    [ObservableProperty]
    private bool _isFalseAlarm;

    partial void OnIsInspectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BorderColor));
        OnPropertyChanged(nameof(ContentOpacity));
    }

    partial void OnIsDefectFoundChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BorderColor));
    }

    partial void OnIsFalseAlarmChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BorderColor));
    }

    /// <summary>
    /// Hintergrundfarbe: Gruen bei gefundenem Fehler, Rot bei Fehlalarm, Standard sonst.
    /// </summary>
    public string BackgroundColor => IsDefectFound ? "#4CAF50" : (IsFalseAlarm ? "#F44336" : "#2A2A2A");

    /// <summary>
    /// Rahmenfarbe: Gruen bei Fehler gefunden, Rot bei Fehlalarm, Standard-Grau sonst.
    /// </summary>
    public string BorderColor => IsInspected ? (HasDefect ? "#4CAF50" : "#F44336") : "#555555";

    /// <summary>
    /// Deckkraft des Inhalts: Reduziert bei falsch inspiziertem Feld.
    /// </summary>
    public double ContentOpacity => IsInspected ? (IsDefectFound ? 1.0 : 0.5) : 1.0;
}
