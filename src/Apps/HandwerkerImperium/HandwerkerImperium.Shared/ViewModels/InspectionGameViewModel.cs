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
/// ViewModel fuer das Inspektions-MiniGame (Baustelleninspektion / Fehlersuche).
/// Spieler muss Fehler auf einer Baustelle finden, indem er fehlerhafte Felder antippt.
/// </summary>
public partial class InspectionGameViewModel : ObservableObject, IDisposable
{
    private static readonly Random _random = new();

    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly ILocalizationService _localizationService;
    private DispatcherTimer? _timer;
    private bool _disposed;
    private bool _isEnding;

    // Korrekte Baustellen-Elemente
    private static readonly string[] GoodIcons = { "\U0001F9F1", "\U0001FAB5", "\U0001F529", "\U0001FA9C", "\U0001F3D7\uFE0F", "\U0001F527", "\u2699\uFE0F", "\U0001FA63" };
    // Fehlerhafte Elemente (mit visuellem Hinweis)
    private static readonly string[] DefectIcons = { "\u26A0\uFE0F", "\U0001F6A7", "\U0001F4A5", "\U0001F525", "\u274C", "\u26D4", "\U0001F573\uFE0F", "\U0001F494" };

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    private OrderDifficulty _difficulty = OrderDifficulty.Medium;

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

    // Countdown vor Spielstart
    [ObservableProperty]
    private bool _isCountdownActive;

    [ObservableProperty]
    private string _countdownText = "";

    // Sterne-Anzeige (staggered: 0→1 mit Verzoegerung)
    [ObservableProperty]
    private double _star1Opacity;

    [ObservableProperty]
    private double _star2Opacity;

    [ObservableProperty]
    private double _star3Opacity;

    // Grid-Dimensionen
    private int _gridColumns = 4;
    private int _gridRows = 4;

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Schwierigkeitsgrad als Sterne-Anzeige.
    /// </summary>
    public string DifficultyStars => Difficulty switch
    {
        OrderDifficulty.Easy => "\u2605\u2606\u2606",
        OrderDifficulty.Medium => "\u2605\u2605\u2606",
        OrderDifficulty.Hard => "\u2605\u2605\u2605",
        _ => "\u2605\u2606\u2606"
    };

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

    partial void OnDifficultyChanged(OrderDifficulty value) => OnPropertyChanged(nameof(DifficultyStars));
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

        // Zustand zuruecksetzen (sonst bleibt Ergebnis-Screen stehen)
        IsPlaying = false;
        IsResultShown = false;

        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            Difficulty = activeOrder.Difficulty;
        }

        InitializeGame();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeGame()
    {
        // Grid-Groesse und Zeit je nach Schwierigkeit
        (_gridColumns, _gridRows, MaxTime, var defectCount) = Difficulty switch
        {
            OrderDifficulty.Easy => (4, 4, 45, 3),
            OrderDifficulty.Medium => (5, 4, 35, 5),
            OrderDifficulty.Hard => (5, 5, 25, 7),
            OrderDifficulty.Expert => (6, 5, 20, 9),
            _ => (5, 4, 35, 5)
        };

        OnPropertyChanged(nameof(GridWidth));

        TimeRemaining = MaxTime;
        FoundDefects = 0;
        TotalDefects = defectCount;
        FalseAlarms = 0;
        IsPlaying = false;
        IsResultShown = false;

        GenerateGrid(defectCount);
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
            int randIndex = _random.Next(allIndices.Count);
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
                    ? DefectIcons[_random.Next(DefectIcons.Length)]
                    : GoodIcons[_random.Next(GoodIcons.Length)]
            });
        }
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
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || _isEnding) return;

        TimeRemaining--;

        if (TimeRemaining <= 0)
        {
            await EndGameAsync();
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
            // Fehler korrekt gefunden
            cell.IsDefectFound = true;
            FoundDefects++;
            await _audioService.PlaySoundAsync(GameSound.Good);
        }
        else
        {
            // Falscher Alarm - kein Fehler vorhanden
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
        if (_isEnding) return;
        _isEnding = true;

        IsPlaying = false;
        _timer?.Stop();

        // Alle nicht-gefundenen Fehler aufdecken
        foreach (var cell in Cells.Where(c => c.HasDefect && !c.IsInspected))
        {
            cell.IsInspected = true;
        }

        // Rating berechnen
        Result = CalculateRating();

        // Ergebnis im GameState erfassen
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
        if (order != null)
        {
            int taskCount = Math.Max(1, order.Tasks.Count);
            decimal baseReward = order.BaseReward / taskCount;
            RewardAmount = baseReward * Result.GetRewardPercentage();

            int baseXp = order.BaseXp / taskCount;
            XpAmount = (int)(baseXp * Result.GetXpPercentage());
        }

        // Ergebnis-Anzeige
        ResultText = _localizationService.GetString(Result.GetLocalizationKey());
        ResultEmoji = Result switch
        {
            MiniGameRating.Perfect => "\u2B50\u2B50\u2B50",
            MiniGameRating.Good => "\u2B50\u2B50",
            MiniGameRating.Ok => "\u2B50",
            _ => "\U0001F4A8"
        };

        IsResultShown = true;

        // Sterne staggered einblenden
        Star1Opacity = 0; Star2Opacity = 0; Star3Opacity = 0;
        int starCount = Result switch
        {
            MiniGameRating.Perfect => 3,
            MiniGameRating.Good => 2,
            MiniGameRating.Ok => 1,
            _ => 0
        };
        if (starCount >= 1) { await Task.Delay(200); Star1Opacity = 1.0; }
        if (starCount >= 2) { await Task.Delay(200); Star2Opacity = 1.0; }
        if (starCount >= 3) { await Task.Delay(200); Star3Opacity = 1.0; }

        AdWatched = false;
        CanWatchAd = _rewardedAdService.IsAvailable;
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
        // Pruefen ob weitere Tasks im Auftrag vorhanden sind
        var order = _gameStateService.GetActiveOrder();
        if (order == null)
        {
            NavigationRequested?.Invoke("../..");
            return;
        }

        if (order.IsCompleted)
        {
            // Auftrag abgeschlossen - Belohnungen vergeben und zurueck
            _gameStateService.CompleteActiveOrder();
            NavigationRequested?.Invoke("../..");
        }
        else
        {
            // Weitere Tasks - zum naechsten Mini-Game
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
    private void Cancel()
    {
        _timer?.Stop();
        IsPlaying = false;

        _gameStateService.CancelActiveOrder();
        NavigationRequested?.Invoke("../..");
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

    // Computed Properties fuer die View aktualisieren
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
