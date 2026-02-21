using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel für das Dachziegel-Muster-Puzzle.
/// Der Spieler muss fehlende Dachziegel in korrekten Farben platzieren.
/// </summary>
public partial class RoofTilingGameViewModel : ObservableObject, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly ILocalizationService _localizationService;
    private DispatcherTimer? _timer;
    private bool _disposed;
    private bool _isEnding;

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
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>Wird nach Spielende mit Rating (0-3 Sterne) gefeuert.</summary>
    public event EventHandler<int>? GameCompleted;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    private OrderDifficulty _difficulty = OrderDifficulty.Medium;

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

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isResultShown;

    [ObservableProperty]
    private MiniGameRating _result;

    [ObservableProperty]
    private string _resultText = "";

    [ObservableProperty]
    private string _resultEmoji = "";

    [ObservableProperty]
    private decimal _rewardAmount;

    [ObservableProperty]
    private int _xpAmount;

    [ObservableProperty]
    private bool _canWatchAd;

    [ObservableProperty]
    private bool _adWatched;

    [ObservableProperty]
    private string _taskProgressDisplay = "";

    [ObservableProperty]
    private bool _isLastTask;

    [ObservableProperty]
    private string _continueButtonText = "";

    // Countdown vor Spielstart
    [ObservableProperty]
    private bool _isCountdownActive;

    [ObservableProperty]
    private string _countdownText = "";

    // Sterne-Anzeige (staggered: 0→1 mit Verzögerung)
    [ObservableProperty]
    private double _star1Opacity;

    [ObservableProperty]
    private double _star2Opacity;

    [ObservableProperty]
    private double _star3Opacity;

    // Hinweis: Farbpalette pulsen wenn keine Farbe gewählt
    [ObservableProperty]
    private bool _selectColorHint;

    // Tutorial (beim ersten Spielstart anzeigen)
    [ObservableProperty]
    private bool _showTutorial;

    [ObservableProperty]
    private string _tutorialTitle = "";

    [ObservableProperty]
    private string _tutorialText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Schwierigkeitsgrad als Sterne-Anzeige.
    /// </summary>
    public string DifficultyStars => Difficulty switch
    {
        OrderDifficulty.Easy => "★☆☆",
        OrderDifficulty.Medium => "★★☆",
        OrderDifficulty.Hard => "★★★",
        OrderDifficulty.Expert => "★★★★",
        _ => "★☆☆"
    };

    /// <summary>
    /// Breite des Tile-Grids in Pixeln für WrapPanel.
    /// Jeder Ziegel ist 50px + 4px Margin = 54px.
    /// </summary>
    public double TileGridWidth => GridColumns * 54;

    partial void OnDifficultyChanged(OrderDifficulty value) => OnPropertyChanged(nameof(DifficultyStars));
    partial void OnGridColumnsChanged(int value) => OnPropertyChanged(nameof(TileGridWidth));

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public RoofTilingGameViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        IRewardedAdService rewardedAdService,
        ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _rewardedAdService = rewardedAdService;
        _localizationService = localizationService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialisiert das Spiel mit einer Auftrags-ID.
    /// </summary>
    public void SetOrderId(string orderId)
    {
        OrderId = orderId;

        // Zustand zurücksetzen (sonst bleibt Ergebnis-Screen stehen)
        IsPlaying = false;
        IsResultShown = false;

        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            Difficulty = activeOrder.Difficulty;

            int totalTasks = activeOrder.Tasks.Count;
            int currentTaskNum = activeOrder.CurrentTaskIndex + 1;
            TaskProgressDisplay = totalTasks > 1
                ? string.Format(_localizationService.GetString("TaskProgress"), currentTaskNum, totalTasks)
                : "";
            IsLastTask = currentTaskNum >= totalTasks;
            ContinueButtonText = IsLastTask
                ? _localizationService.GetString("Continue")
                : _localizationService.GetString("NextTask");
        }
        else
        {
            TaskProgressDisplay = "";
            IsLastTask = true;
            ContinueButtonText = _localizationService.GetString("Continue");
        }

        InitializeGame();

        CheckAndShowTutorial(MiniGameType.RoofTiling);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeGame()
    {
        // Grid-Größe, Zeit und Farbanzahl je nach Schwierigkeit
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
        IsPlaying = false;
        IsResultShown = false;
        SelectedColor = "";

        // Verfügbare Farben setzen
        AvailableColors.Clear();
        for (int i = 0; i < colorCount; i++)
        {
            AvailableColors.Add(TileColors[i]);
        }

        GenerateGrid(colorCount, hintPercentage);
    }

    /// <summary>
    /// Generiert das Dach-Gitter mit einem Muster aus farbigen Ziegeln.
    /// Ein Teil der Ziegel wird als Hinweis vorplatziert.
    /// </summary>
    private void GenerateGrid(int colorCount, double hintPercentage)
    {
        Tiles.Clear();
        int totalTiles = GridColumns * GridRows;

        // Muster generieren: Reihenweises Muster mit Versatz (wie echte Dachziegel)
        var pattern = GenerateRoofPattern(colorCount);

        // Bestimme welche Ziegel als Hinweis vorplatziert werden
        int hintCount = (int)(totalTiles * hintPercentage);
        var hintIndices = new HashSet<int>();

        // Erst jede Reihe mindestens 1 Hint garantieren (Referenz in jedem Bereich)
        for (int row = 0; row < GridRows; row++)
        {
            int startIdx = row * GridColumns;
            int colIdx = Random.Shared.Next(GridColumns);
            hintIndices.Add(startIdx + colIdx);
        }

        // Restliche Hints zufällig verteilen
        while (hintIndices.Count < hintCount)
        {
            hintIndices.Add(Random.Shared.Next(totalTiles));
        }

        // Ziegel erstellen
        for (int i = 0; i < totalTiles; i++)
        {
            int row = i / GridColumns;
            int col = i % GridColumns;
            string correctColor = pattern[row, col];
            bool isHint = hintIndices.Contains(i);

            var tile = new RoofTile
            {
                Row = row,
                Column = col,
                Index = i,
                CorrectColor = correctColor,
                IsHint = isHint,
                IsPlaced = isHint,
                CurrentColor = isHint ? correctColor : ""
            };

            Tiles.Add(tile);
        }

        TotalToPlace = totalTiles - hintCount;
    }

    /// <summary>
    /// Generiert ein realistisches Dachziegel-Muster.
    /// Jede Reihe hat ein versetztes Farbmuster (wie Ziegelverbund).
    /// </summary>
    private string[,] GenerateRoofPattern(int colorCount)
    {
        var pattern = new string[GridRows, GridColumns];
        var colors = TileColors.Take(colorCount).ToArray();

        // Verschiedene Muster-Typen zufällig wählen
        int patternType = Random.Shared.Next(3);

        switch (patternType)
        {
            case 0: // Diagonales Streifenmuster
                for (int row = 0; row < GridRows; row++)
                {
                    for (int col = 0; col < GridColumns; col++)
                    {
                        int index = (row + col) % colorCount;
                        pattern[row, col] = colors[index];
                    }
                }
                break;

            case 1: // Schachbrett-ähnliches Muster mit Versatz
                for (int row = 0; row < GridRows; row++)
                {
                    int offset = row % 2;
                    for (int col = 0; col < GridColumns; col++)
                    {
                        int index = (col + offset) % colorCount;
                        pattern[row, col] = colors[index];
                    }
                }
                break;

            case 2: // Blockmuster (2er-Blöcke)
                for (int row = 0; row < GridRows; row++)
                {
                    int rowBlock = row / 2;
                    for (int col = 0; col < GridColumns; col++)
                    {
                        int colBlock = col / 2;
                        int index = (rowBlock + colBlock) % colorCount;
                        pattern[row, col] = colors[index];
                    }
                }
                break;
        }

        return pattern;
    }

    [RelayCommand]
    private async Task StartGameAsync()
    {
        if (IsPlaying || IsCountdownActive) return;

        IsResultShown = false;
        _isEnding = false;
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Countdown 3-2-1-Los!
        IsCountdownActive = true;
        foreach (var text in new[] { "3", "2", "1", _localizationService.GetString("CountdownGo") })
        {
            CountdownText = text;
            await Task.Delay(700);
        }
        IsCountdownActive = false;

        // Spiel starten
        IsPlaying = true;
        if (_timer != null) { _timer.Stop(); _timer.Tick -= OnTimerTick; }
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler in OnTimerTick: {ex}");
        }
    }

    /// <summary>
    /// Wählt eine Farbe aus der Farbauswahl-Leiste.
    /// </summary>
    [RelayCommand]
    private void SelectColor(string? color)
    {
        if (color == null || !IsPlaying) return;
        SelectedColor = color;
    }

    /// <summary>
    /// Platziert einen Ziegel auf dem Gitter.
    /// Prüft ob die gewählte Farbe korrekt ist.
    /// </summary>
    [RelayCommand]
    private async Task PlaceTileAsync(RoofTile? tile)
    {
        if (tile == null || !IsPlaying || IsResultShown) return;

        // Bereits platzierte/Hinweis-Ziegel ignorieren
        if (tile.IsPlaced || tile.IsHint) return;

        // Keine Farbe gewählt → Farbpalette pulsieren lassen
        if (string.IsNullOrEmpty(SelectedColor))
        {
            SelectColorHint = true;
            _ = ResetSelectColorHintAsync();
            return;
        }

        // Farbe setzen
        tile.CurrentColor = SelectedColor;

        if (SelectedColor == tile.CorrectColor)
        {
            // Korrekt platziert
            tile.IsPlaced = true;
            tile.HasError = false;
            PlacedCount++;

            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

            // Alle Ziegel platziert?
            if (PlacedCount >= TotalToPlace)
            {
                await EndGameAsync();
            }
        }
        else
        {
            // Falscher Ziegel
            tile.HasError = true;
            MistakeCount++;

            await _audioService.PlaySoundAsync(GameSound.Miss);

            // Fehler nach kurzer Zeit zurücksetzen
            await Task.Delay(400);
            tile.HasError = false;
            tile.CurrentColor = "";
        }
    }

    /// <summary>
    /// Beendet das Spiel und berechnet das Ergebnis.
    /// </summary>
    private async Task EndGameAsync()
    {
        if (_isEnding) return;
        _isEnding = true;

        IsPlaying = false;
        _timer?.Stop();

        // Rating berechnen
        bool allPlaced = PlacedCount >= TotalToPlace;
        double timeRatio = MaxTime > 0 ? (double)TimeRemaining / MaxTime : 0;

        if (allPlaced && MistakeCount == 0 && timeRatio > 0.50)
        {
            // Perfect: 0 Fehler + >50% Zeit übrig
            Result = MiniGameRating.Perfect;
        }
        else if (allPlaced && MistakeCount <= 2 && timeRatio > 0.25)
        {
            // Good: <=2 Fehler + >25% Zeit übrig
            Result = MiniGameRating.Good;
        }
        else if (allPlaced && MistakeCount <= 8)
        {
            // Ok: <=8 Fehler + alle platziert
            Result = MiniGameRating.Ok;
        }
        else if (!allPlaced && TotalToPlace > 0)
        {
            // Teilbewertung bei Zeitablauf: basierend auf Platzierungs-Quote
            double placedRatio = (double)PlacedCount / TotalToPlace;
            if (placedRatio >= 0.90 && MistakeCount <= 2)
                Result = MiniGameRating.Good;
            else if (placedRatio >= 0.70 && MistakeCount <= 4)
                Result = MiniGameRating.Ok;
            else
                Result = MiniGameRating.Miss;
        }
        else
        {
            // >8 Fehler
            Result = MiniGameRating.Miss;
        }

        // Ergebnis aufzeichnen
        _gameStateService.RecordMiniGameResult(Result);

        // Sound abspielen
        var sound = Result switch
        {
            MiniGameRating.Perfect => GameSound.Perfect,
            MiniGameRating.Good => GameSound.Good,
            MiniGameRating.Ok => GameSound.ButtonTap,
            _ => GameSound.Miss
        };
        await _audioService.PlaySoundAsync(sound);

        // Belohnungen berechnen
        var order = _gameStateService.GetActiveOrder();
        if (order != null && IsLastTask)
        {
            // Gesamt-Belohnung
            RewardAmount = order.FinalReward;
            XpAmount = order.FinalXp;
        }
        else if (order != null)
        {
            // Teilbelohnung für diese Aufgabe anzeigen
            int taskCount = Math.Max(1, order.Tasks.Count);
            decimal basePerTask = order.BaseReward / taskCount;
            RewardAmount = basePerTask * Result.GetRewardPercentage()
                * order.Difficulty.GetRewardMultiplier() * order.OrderType.GetRewardMultiplier();
            int baseXpPerTask = order.BaseXp / taskCount;
            XpAmount = (int)(baseXpPerTask * Result.GetXpPercentage()
                * order.Difficulty.GetXpMultiplier() * order.OrderType.GetXpMultiplier());
        }
        else
        {
            // QuickJob: Belohnung aus aktivem QuickJob lesen
            var quickJob = _gameStateService.State.ActiveQuickJob;
            RewardAmount = quickJob?.Reward ?? 0;
            XpAmount = quickJob?.XpReward ?? 0;
        }

        // Ergebnis-Anzeige
        ResultText = _localizationService.GetString(Result.GetLocalizationKey());
        ResultEmoji = Result switch
        {
            MiniGameRating.Perfect => "⭐⭐⭐",
            MiniGameRating.Good => "⭐⭐",
            MiniGameRating.Ok => "⭐",
            _ => "💨"
        };

        IsResultShown = true;

        // Sterne-Bewertung berechnen
        int starCount = Result switch
        {
            MiniGameRating.Perfect => 3,
            MiniGameRating.Good => 2,
            MiniGameRating.Ok => 1,
            _ => 0
        };

        if (IsLastTask)
        {
            // Aggregierte Sterne berechnen (alle Runden zusammen)
            if (order != null && order.TaskResults.Count > 1)
            {
                int totalStarSum = order.TaskResults.Sum(r => r switch
                {
                    MiniGameRating.Perfect => 3,
                    MiniGameRating.Good => 2,
                    MiniGameRating.Ok => 1,
                    _ => 0
                });
                int totalPossible = order.TaskResults.Count * 3;
                starCount = totalPossible > 0
                    ? (int)Math.Round((double)totalStarSum / totalPossible * 3.0)
                    : 0;
                starCount = Math.Clamp(starCount, 0, 3);
            }

            // Sterne staggered einblenden
            Star1Opacity = 0; Star2Opacity = 0; Star3Opacity = 0;
            if (starCount >= 1) { await Task.Delay(200); Star1Opacity = 1.0; }
            if (starCount >= 2) { await Task.Delay(200); Star2Opacity = 1.0; }
            if (starCount >= 3) { await Task.Delay(200); Star3Opacity = 1.0; }

            // Visuelles Event fuer Result-Polish in der View
            GameCompleted?.Invoke(this, starCount);
        }
        else
        {
            // Zwischen-Runde: Sterne sofort setzen, keine Animation
            Star1Opacity = starCount >= 1 ? 1.0 : 0.3;
            Star2Opacity = starCount >= 2 ? 1.0 : 0.3;
            Star3Opacity = starCount >= 3 ? 1.0 : 0.3;
        }

        AdWatched = false;
        CanWatchAd = IsLastTask && _rewardedAdService.IsAvailable;
    }

    [RelayCommand]
    private async Task WatchAdAsync()
    {
        if (!CanWatchAd || AdWatched) return;

        var success = await _rewardedAdService.ShowAdAsync("score_double");
        if (success)
        {
            // Belohnungen verdoppeln
            RewardAmount *= 2;
            XpAmount *= 2;
            AdWatched = true;
            CanWatchAd = false;

            await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
        }
    }

    [RelayCommand]
    private void Continue()
    {
        // Prüfe ob weitere Aufgaben im Auftrag vorhanden
        var order = _gameStateService.GetActiveOrder();
        if (order == null)
        {
            NavigationRequested?.Invoke("../..");
            return;
        }

        if (order.IsCompleted)
        {
            // Auftrag abgeschlossen - Belohnungen vergeben und zurück
            _gameStateService.CompleteActiveOrder();
            NavigationRequested?.Invoke("../..");
        }
        else
        {
            // Weitere Aufgaben - zum nächsten Mini-Game
            var nextTask = order.CurrentTask;
            if (nextTask != null)
            {
                NavigationRequested?.Invoke($"../{nextTask.GameType.GetRoute()}?orderId={order.Id}");
            }
            else
            {
                NavigationRequested?.Invoke("../..");
            }
        }
    }

    [RelayCommand]
    private void DismissTutorial()
    {
        ShowTutorial = false;
        // Als gesehen markieren und speichern
        var state = _gameStateService.State;
        if (!state.SeenMiniGameTutorials.Contains(MiniGameType.RoofTiling))
        {
            state.SeenMiniGameTutorials.Add(MiniGameType.RoofTiling);
            _gameStateService.MarkDirty();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _timer?.Stop();
        IsPlaying = false;

        _gameStateService.CancelActiveOrder();
        NavigationRequested?.Invoke("../..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Setzt den SelectColorHint nach 1 Sekunde zurück.
    /// </summary>
    private async Task ResetSelectColorHintAsync()
    {
        await Task.Delay(1000);
        SelectColorHint = false;
    }

    private void CheckAndShowTutorial(MiniGameType gameType)
    {
        var state = _gameStateService.State;
        if (!state.SeenMiniGameTutorials.Contains(gameType))
        {
            TutorialTitle = _localizationService.GetString($"Tutorial{gameType}Title") ?? "";
            TutorialText = _localizationService.GetString($"Tutorial{gameType}Text") ?? "";
            ShowTutorial = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISPOSAL
    // ═══════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;

        _timer?.Stop();
        if (_timer != null)
        {
            _timer.Tick -= OnTimerTick;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Repräsentiert einen einzelnen Dachziegel im Gitter.
/// </summary>
public partial class RoofTile : ObservableObject
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

    // Visuelle Properties bei Zustandsänderung aktualisieren
    partial void OnIsPlacedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayColor));
        OnPropertyChanged(nameof(BorderColor));
    }

    partial void OnCurrentColorChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayColor));
    }

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(BorderColor));
    }

    partial void OnIsHintChanged(bool value)
    {
        OnPropertyChanged(nameof(BorderColor));
    }

    /// <summary>
    /// Angezeigte Farbe: Platziert=korrekte Farbe, Temporär=aktuelle Farbe, Leer=Dunkelgrau.
    /// </summary>
    public string DisplayColor => IsPlaced
        ? CorrectColor
        : !string.IsNullOrEmpty(CurrentColor)
            ? CurrentColor
            : "#3A3A3A";

    /// <summary>
    /// Rahmenfarbe: Hinweis=Gold, Fehler=Rot, Standard=Grau.
    /// </summary>
    public string BorderColor => IsHint
        ? "#FFD700"
        : HasError
            ? "#F44336"
            : IsPlaced
                ? "#4CAF50"
                : "#555555";
}
