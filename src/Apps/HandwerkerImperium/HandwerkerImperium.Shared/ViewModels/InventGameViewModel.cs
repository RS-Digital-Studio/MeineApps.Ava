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
/// ViewModel für das Erfinder-Puzzle-Minispiel.
/// Der Spieler merkt sich die Montage-Reihenfolge der Bauteile und tippt sie danach korrekt an.
/// </summary>
public partial class InventGameViewModel : ObservableObject, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly ILocalizationService _localizationService;
    private DispatcherTimer? _gameTimer;
    private bool _disposed;
    private bool _isEnding;

    // Bauteil-Icons (Vektor-Identifikatoren für SkiaSharp-Rendering)
    private static readonly string[] PartIcons =
    {
        "gear",      // Zahnrad
        "piston",    // Kolben
        "wire",      // Kabel
        "board",     // Platine
        "screw",     // Schraube
        "housing",   // Gehäuse
        "spring",    // Feder
        "lens",      // Linse
        "motor",     // Motor
        "battery",   // Batterie
        "switch",    // Schalter
        "antenna"    // Antenne
    };

    // Lokalisierte Bauteil-Labels (Keys)
    private static readonly string[] PartLabelKeys =
    {
        "InventPartGear", "InventPartPiston", "InventPartWire", "InventPartBoard",
        "InventPartScrew", "InventPartHousing", "InventPartSpring", "InventPartLens",
        "InventPartMotor", "InventPartBattery", "InventPartSwitch", "InventPartAntenna"
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
    private ObservableCollection<InventPart> _parts = [];

    [ObservableProperty]
    private bool _isMemorizing;

    [ObservableProperty]
    private int _nextExpectedPart = 1;

    [ObservableProperty]
    private int _mistakeCount;

    [ObservableProperty]
    private int _completedParts;

    [ObservableProperty]
    private int _totalParts;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime;

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
    /// Schwierigkeit als Sterne-Anzeige.
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
    /// Breite des Grids in Pixeln für WrapPanel-Constraint.
    /// Jedes Teil: 68px + 6px Margin = 74px.
    /// </summary>
    public double GridWidth => _gridColumns * 74;

    private int _gridColumns = 3;

    partial void OnDifficultyChanged(OrderDifficulty value) => OnPropertyChanged(nameof(DifficultyStars));

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public InventGameViewModel(
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
        IsMemorizing = false;
        _isEnding = false;

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

        CheckAndShowTutorial(MiniGameType.InventGame);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeGame()
    {
        // Schwierigkeit bestimmt Teileanzahl, Grid-Spalten und Spielzeit
        (TotalParts, _gridColumns, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (6, 2, 25),
            OrderDifficulty.Medium => (9, 3, 35),
            OrderDifficulty.Hard => (12, 3, 40),
            OrderDifficulty.Expert => (16, 4, 45),
            _ => (9, 3, 35)
        };

        // Tool-Bonus: Kompass gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Compass);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        CompletedParts = 0;
        MistakeCount = 0;
        NextExpectedPart = 1;
        IsPlaying = false;
        IsResultShown = false;
        IsMemorizing = false;

        OnPropertyChanged(nameof(GridWidth));

        GenerateParts();
    }

    private void GenerateParts()
    {
        Parts.Clear();

        // Zufällige Auswahl der Bauteile
        var indices = Enumerable.Range(0, PartIcons.Length).ToList();
        // Mischen
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < TotalParts; i++)
        {
            int iconIndex = indices[i % indices.Count];
            string label = _localizationService.GetString(PartLabelKeys[iconIndex]) ?? PartLabelKeys[iconIndex];

            Parts.Add(new InventPart
            {
                StepNumber = i + 1,
                Icon = PartIcons[iconIndex],
                Label = label,
                IsRevealed = false,
                IsCompleted = false,
                HasError = false
            });
        }

        // Positionen im Grid mischen (Nummern bleiben, aber physische Position variiert)
        var shuffled = Parts.OrderBy(_ => Random.Shared.Next()).ToList();
        Parts.Clear();
        foreach (var part in shuffled)
        {
            Parts.Add(part);
        }
    }

    [RelayCommand]
    private async Task StartGameAsync()
    {
        if (IsPlaying || IsCountdownActive || IsMemorizing) return;

        IsResultShown = false;
        _isEnding = false;

        // Countdown 3-2-1-Los!
        IsCountdownActive = true;
        foreach (var text in new[] { "3", "2", "1", _localizationService.GetString("CountdownGo") })
        {
            CountdownText = text;
            await Task.Delay(700);
        }
        IsCountdownActive = false;

        // Memorisierungsphase: Alle Nummern aufdecken
        IsMemorizing = true;
        foreach (var part in Parts)
        {
            part.IsRevealed = true;
        }

        // Memorisierungszeit je nach Schwierigkeit
        int memorizeMs = Difficulty switch
        {
            OrderDifficulty.Easy => 3000,
            OrderDifficulty.Medium => 2500,
            OrderDifficulty.Hard => 2000,
            OrderDifficulty.Expert => 1500,
            _ => 2500
        };

        await Task.Delay(memorizeMs);

        // Nummern verstecken, Spiel starten
        foreach (var part in Parts)
        {
            part.IsRevealed = false;
        }
        IsMemorizing = false;

        // Spiel starten mit Timer
        IsPlaying = true;
        if (_gameTimer != null) { _gameTimer.Stop(); _gameTimer.Tick -= OnGameTimerTick; }
        _gameTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _gameTimer.Tick += OnGameTimerTick;
        _gameTimer.Start();
    }

    private async void OnGameTimerTick(object? sender, EventArgs e)
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
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"Fehler in OnGameTimerTick: {ex}");
#endif
        }
    }

    [RelayCommand]
    private async Task SelectPartAsync(InventPart? part)
    {
        if (part == null || !IsPlaying || IsResultShown || part.IsCompleted) return;

        if (part.StepNumber == NextExpectedPart)
        {
            // Korrekt! Teil als erledigt markieren
            part.IsCompleted = true;
            part.HasError = false;
            CompletedParts++;
            NextExpectedPart++;

            await _audioService.PlaySoundAsync(GameSound.Good);

            // Alle Teile erledigt?
            if (CompletedParts >= TotalParts)
            {
                await EndGameAsync();
            }
        }
        else
        {
            // Falsch! Kurzes rotes Blinken
            MistakeCount++;
            part.HasError = true;

            await _audioService.PlaySoundAsync(GameSound.Miss);

            // Fehler nach kurzer Zeit zurücksetzen
            _ = ResetErrorAsync(part);
        }
    }

    private static async Task ResetErrorAsync(InventPart part)
    {
        await Task.Delay(500);
        part.HasError = false;
    }

    private async Task EndGameAsync()
    {
        if (_isEnding) return;
        _isEnding = true;

        IsPlaying = false;
        _gameTimer?.Stop();

        // Rating berechnen basierend auf Leistung
        bool allCompleted = CompletedParts >= TotalParts;
        double timeRatio = MaxTime > 0 ? (double)TimeRemaining / MaxTime : 0;

        if (allCompleted && MistakeCount == 0 && timeRatio > 0.4)
        {
            Result = MiniGameRating.Perfect;
        }
        else if (allCompleted && MistakeCount <= 2 && timeRatio > 0.2)
        {
            Result = MiniGameRating.Good;
        }
        else if (allCompleted)
        {
            Result = MiniGameRating.Ok;
        }
        else
        {
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

        // Ergebnis-Anzeige setzen
        ResultText = _localizationService.GetString(Result.GetLocalizationKey());
        ResultEmoji = Result switch
        {
            MiniGameRating.Perfect => "\u2B50\u2B50\u2B50",
            MiniGameRating.Good => "\u2B50\u2B50",
            MiniGameRating.Ok => "\u2B50",
            _ => "\U0001F4A8"
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

            // Visuelles Event für Result-Polish in der View
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
        // Prüfen ob weitere Aufgaben in der Order sind
        var order = _gameStateService.GetActiveOrder();
        if (order == null)
        {
            NavigationRequested?.Invoke("../..");
            return;
        }

        if (order.IsCompleted)
        {
            // Auftrag fertig - Belohnungen vergeben und zurück
            _gameStateService.CompleteActiveOrder();
            NavigationRequested?.Invoke("../..");
        }
        else
        {
            // Mehr Aufgaben - zum nächsten Mini-Game
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
        if (!state.SeenMiniGameTutorials.Contains(MiniGameType.InventGame))
        {
            state.SeenMiniGameTutorials.Add(MiniGameType.InventGame);
            _gameStateService.MarkDirty();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _gameTimer?.Stop();
        IsPlaying = false;
        IsMemorizing = false;

        _gameStateService.CancelActiveOrder();
        NavigationRequested?.Invoke("../..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

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

        _gameTimer?.Stop();
        if (_gameTimer != null)
        {
            _gameTimer.Tick -= OnGameTimerTick;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Repräsentiert ein einzelnes Bauteil im Erfinder-Puzzle.
/// </summary>
public partial class InventPart : ObservableObject
{
    [ObservableProperty]
    private int _stepNumber; // Korrekte Montage-Reihenfolge (1-basiert)

    [ObservableProperty]
    private string _icon = ""; // Vektor-Icon-Identifier (z.B. "gear", "piston")

    [ObservableProperty]
    private bool _isRevealed; // Nummer sichtbar (Memorisierungsphase)

    [ObservableProperty]
    private bool _isCompleted; // Wurde korrekt angetippt

    [ObservableProperty]
    private bool _hasError; // Wurde falsch angetippt (kurzes Blinken)

    [ObservableProperty]
    private string _label = ""; // Beschreibungstext (z.B. "Zahnrad")

    // Berechnete Anzeige-Properties aktualisieren bei Zustandsänderung
    partial void OnIsRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayNumber));
        OnPropertyChanged(nameof(BackgroundColor));
    }

    partial void OnIsCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayNumber));
        OnPropertyChanged(nameof(BackgroundColor));
    }

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
    }

    /// <summary>
    /// Angezeigte Nummer: Sichtbar während Memorisierung und nach Abschluss, sonst "?".
    /// </summary>
    public string DisplayNumber => IsRevealed || IsCompleted ? StepNumber.ToString() : "?";

    /// <summary>
    /// Hintergrundfarbe basierend auf Zustand.
    /// </summary>
    public string BackgroundColor => IsCompleted ? "#4CAF50" : (HasError ? "#F44336" : "#2A1A40");
}
