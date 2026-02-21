using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using Material.Icons;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer das Grundriss-Raetsel Mini-Game.
/// Der Spieler muss Raeume einem Grundriss korrekt zuordnen.
/// </summary>
public partial class DesignPuzzleGameViewModel : ObservableObject, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly ILocalizationService _localizationService;
    private DispatcherTimer? _timer;
    private bool _disposed;
    private bool _isEnding;

    // Raum-Definitionen: Id, IconKind, IconColor, NameKey
    private static readonly (string Id, MaterialIconKind IconKind, string IconColor, string NameKey)[] RoomDefs =
    {
        ("kitchen", MaterialIconKind.Stove, "#FF6F00", "RoomKitchen"),
        ("bathroom", MaterialIconKind.ShowerHead, "#0288D1", "RoomBathroom"),
        ("bedroom", MaterialIconKind.Bed, "#7B1FA2", "RoomBedroom"),
        ("living", MaterialIconKind.Sofa, "#2E7D32", "RoomLiving"),
        ("office", MaterialIconKind.Laptop, "#455A64", "RoomOffice"),
        ("garage", MaterialIconKind.Garage, "#795548", "RoomGarage"),
        ("laundry", MaterialIconKind.WashingMachine, "#00838F", "RoomLaundry"),
        ("dining", MaterialIconKind.SilverwareForkKnife, "#C62828", "RoomDining"),
        ("hallway", MaterialIconKind.DoorOpen, "#5D4037", "RoomHallway"),
        ("basement", MaterialIconKind.Stairs, "#37474F", "RoomBasement"),
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
    private ObservableCollection<RoomSlot> _slots = [];

    [ObservableProperty]
    private ObservableCollection<RoomCard> _availableRooms = [];

    [ObservableProperty]
    private RoomCard? _selectedRoom;

    [ObservableProperty]
    private int _mistakeCount;

    [ObservableProperty]
    private int _placedCount;

    [ObservableProperty]
    private int _totalSlots;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime = 60;

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

    // Sterne-Anzeige (staggered: 0->1 mit Verzoegerung)
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
        OrderDifficulty.Easy => "\u2605\u2606\u2606",
        OrderDifficulty.Medium => "\u2605\u2605\u2606",
        OrderDifficulty.Hard => "\u2605\u2605\u2605",
        OrderDifficulty.Expert => "\u2605\u2605\u2605\u2605",
        _ => "\u2605\u2606\u2606"
    };

    /// <summary>
    /// Breite des Grundriss-Grids in Pixeln fuer WrapPanel.
    /// Jeder Slot ist 84px breit (80 + 4 Margin).
    /// Easy: 2 Spalten = 168, Medium: 3 Spalten = 252, Hard: 4 Spalten = 336.
    /// </summary>
    public double GridWidth => Difficulty switch
    {
        OrderDifficulty.Easy => 2 * 84,
        OrderDifficulty.Hard => 4 * 84,
        OrderDifficulty.Expert => 5 * 84,
        _ => 3 * 84
    };

    partial void OnDifficultyChanged(OrderDifficulty value)
    {
        OnPropertyChanged(nameof(DifficultyStars));
        OnPropertyChanged(nameof(GridWidth));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public DesignPuzzleGameViewModel(
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
    /// Spiel mit einer Auftrags-ID initialisieren.
    /// </summary>
    public void SetOrderId(string orderId)
    {
        OrderId = orderId;

        // Zustand zuruecksetzen (sonst bleibt Ergebnis-Screen stehen)
        IsPlaying = false;
        IsResultShown = false;

        // Schwierigkeit aus aktivem Auftrag laden
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

        CheckAndShowTutorial(MiniGameType.DesignPuzzle);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GAME LOGIC
    // ═══════════════════════════════════════════════════════════════════════

    private void InitializeGame()
    {
        // Schwierigkeitsabhaengige Parameter
        int roomCount;
        (roomCount, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (4, 60),
            OrderDifficulty.Medium => (6, 45),
            OrderDifficulty.Hard => (8, 35),
            OrderDifficulty.Expert => (10, 35),
            _ => (6, 45)
        };

        // Tool-Bonus: Zirkel gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Compass);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        PlacedCount = 0;
        MistakeCount = 0;
        SelectedRoom = null;
        _isEnding = false;

        GeneratePuzzle(roomCount);
    }

    private void GeneratePuzzle(int roomCount)
    {
        Slots.Clear();
        AvailableRooms.Clear();

        // Zufaellige Raum-Auswahl
        var random = Random.Shared;
        var selectedRooms = RoomDefs
            .OrderBy(_ => random.Next())
            .Take(roomCount)
            .ToArray();

        TotalSlots = selectedRooms.Length;

        // Slots erstellen (in zufaelliger Reihenfolge fuer den Grundriss)
        var shuffledForSlots = selectedRooms.OrderBy(_ => random.Next()).ToArray();
        foreach (var room in shuffledForSlots)
        {
            Slots.Add(new RoomSlot
            {
                CorrectRoomId = room.Id,
                HintColor = room.IconColor,
                HintIconKind = room.IconKind
            });
        }

        // Raum-Karten erstellen (ebenfalls gemischt)
        var shuffledForCards = selectedRooms.OrderBy(_ => random.Next()).ToArray();
        foreach (var room in shuffledForCards)
        {
            string displayName = _localizationService.GetString(room.NameKey) ?? room.NameKey;
            AvailableRooms.Add(new RoomCard
            {
                RoomId = room.Id,
                IconKind = room.IconKind,
                IconColor = room.IconColor,
                NameKey = room.NameKey,
                DisplayName = displayName
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
    /// Raum-Karte auswaehlen.
    /// </summary>
    [RelayCommand]
    private async Task SelectRoomAsync(RoomCard? room)
    {
        if (room == null || !IsPlaying || IsResultShown || room.IsUsed) return;

        // Vorherige Auswahl aufheben
        if (SelectedRoom != null)
        {
            SelectedRoom.IsSelected = false;
        }

        // Neue Auswahl setzen
        room.IsSelected = true;
        SelectedRoom = room;

        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
    }

    /// <summary>
    /// Ausgewaehlten Raum in einen Slot platzieren.
    /// </summary>
    [RelayCommand]
    private async Task PlaceRoomAsync(RoomSlot? slot)
    {
        if (slot == null || !IsPlaying || IsResultShown || slot.IsFilled) return;
        if (SelectedRoom == null) return;

        var room = SelectedRoom;

        // Pruefen ob der Raum korrekt platziert wurde
        if (slot.CorrectRoomId == room.RoomId)
        {
            // Korrekt platziert
            slot.IsFilled = true;
            slot.IsCorrect = true;
            slot.HasError = false;
            slot.CurrentRoomId = room.RoomId;
            slot.DisplayLabel = room.DisplayName;
            slot.FilledColor = room.IconColor;

            room.IsUsed = true;
            room.IsSelected = false;
            SelectedRoom = null;
            PlacedCount++;

            await _audioService.PlaySoundAsync(GameSound.Good);

            // Pruefen ob alle Slots gefuellt sind
            if (PlacedCount >= TotalSlots)
            {
                await EndGameAsync();
            }
        }
        else
        {
            // Falsch platziert - Fehler anzeigen
            MistakeCount++;
            slot.HasError = true;

            await _audioService.PlaySoundAsync(GameSound.Miss);

            // Fehler-Anzeige nach kurzer Verzoegerung zuruecksetzen
            await Task.Delay(400);
            slot.HasError = false;
        }
    }

    private async Task EndGameAsync()
    {
        if (_isEnding) return;
        _isEnding = true;

        IsPlaying = false;
        _timer?.Stop();

        // Auswahl aufheben
        if (SelectedRoom != null)
        {
            SelectedRoom.IsSelected = false;
            SelectedRoom = null;
        }

        // Rating berechnen basierend auf Fortschritt, Fehlern und verbleibender Zeit
        bool allPlaced = PlacedCount >= TotalSlots;
        double timeRatio = MaxTime > 0 ? (double)TimeRemaining / MaxTime : 0;

        if (allPlaced && MistakeCount == 0 && timeRatio > 0.5)
        {
            Result = MiniGameRating.Perfect;
        }
        else if (allPlaced && MistakeCount <= 2 && timeRatio > 0.25)
        {
            Result = MiniGameRating.Good;
        }
        else if (allPlaced)
        {
            Result = MiniGameRating.Ok;
        }
        else
        {
            // Zeit abgelaufen - teilweise Bewertung
            double completionRatio = TotalSlots > 0 ? (double)PlacedCount / TotalSlots : 0;
            Result = completionRatio >= 0.75 ? MiniGameRating.Ok : MiniGameRating.Miss;
        }

        // Ergebnis im GameState vermerken
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
    private void DismissTutorial()
    {
        ShowTutorial = false;
        // Als gesehen markieren und speichern
        var state = _gameStateService.State;
        if (!state.SeenMiniGameTutorials.Contains(MiniGameType.DesignPuzzle))
        {
            state.SeenMiniGameTutorials.Add(MiniGameType.DesignPuzzle);
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
// HILFSTYPEN
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Platzhalter-Slot im Grundriss fuer einen Raum.
/// </summary>
public partial class RoomSlot : ObservableObject
{
    [ObservableProperty]
    private string _correctRoomId = "";

    [ObservableProperty]
    private string _currentRoomId = "";

    /// <summary>Farbe als Hinweis welcher Raum hierhin gehoert.</summary>
    [ObservableProperty]
    private string _hintColor = "#555555";

    /// <summary>Icon-Typ als Hinweis (wird im Renderer als Farb-Akzent verwendet).</summary>
    [ObservableProperty]
    private MaterialIconKind _hintIconKind = MaterialIconKind.Home;

    [ObservableProperty]
    private bool _isFilled;

    [ObservableProperty]
    private bool _isCorrect;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>Lokalisierter Raumname fuer gefuellte Slots.</summary>
    [ObservableProperty]
    private string _displayLabel = "";

    /// <summary>Farbe des platzierten Raums.</summary>
    [ObservableProperty]
    private string _filledColor = "";

    // Berechnete Farben bei Zustandsaenderung aktualisieren
    partial void OnIsFilledChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BorderColor));
    }

    partial void OnIsCorrectChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BorderColor));
    }

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BorderColor));
    }

    /// <summary>Hintergrundfarbe basierend auf Zustand.</summary>
    public string BackgroundColor => IsCorrect ? "#4CAF50" : (HasError ? "#F44336" : "#2A2A2A");

    /// <summary>Randfarbe basierend auf Zustand.</summary>
    public string BorderColor => IsFilled ? (IsCorrect ? "#4CAF50" : "#FF9800") : "#555555";
}

/// <summary>
/// Raum-Karte zur Auswahl fuer den Spieler.
/// </summary>
public partial class RoomCard : ObservableObject
{
    [ObservableProperty]
    private string _roomId = "";

    /// <summary>Material Icon fuer den Raum.</summary>
    [ObservableProperty]
    private MaterialIconKind _iconKind = MaterialIconKind.Home;

    /// <summary>Farbe des Icons/Raums.</summary>
    [ObservableProperty]
    private string _iconColor = "#FFFFFF";

    [ObservableProperty]
    private string _nameKey = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private bool _isUsed;

    [ObservableProperty]
    private bool _isSelected;

    // Berechnete Properties bei Zustandsaenderung aktualisieren
    partial void OnIsUsedChanged(bool value)
    {
        OnPropertyChanged(nameof(CardOpacity));
    }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectionBorderColor));
    }

    /// <summary>Transparenz wenn bereits platziert.</summary>
    public double CardOpacity => IsUsed ? 0.3 : 1.0;

    /// <summary>Rand-Farbe wenn ausgewaehlt.</summary>
    public string SelectionBorderColor => IsSelected ? "#FFD700" : "Transparent";
}
