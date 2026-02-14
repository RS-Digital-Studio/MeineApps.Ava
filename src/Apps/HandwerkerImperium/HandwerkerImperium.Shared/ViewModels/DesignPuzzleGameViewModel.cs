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

    // Raum-Definitionen: Id, Emoji, HintIcon, NameKey
    private static readonly (string Id, string Emoji, string HintIcon, string NameKey)[] RoomDefs =
    {
        ("kitchen", "\U0001F373", "\U0001F525", "RoomKitchen"),
        ("bathroom", "\U0001F6BF", "\U0001F4A7", "RoomBathroom"),
        ("bedroom", "\U0001F6CF\uFE0F", "\U0001F319", "RoomBedroom"),
        ("living", "\U0001F6CB\uFE0F", "\U0001F4FA", "RoomLiving"),
        ("office", "\U0001F4BB", "\U0001F4DD", "RoomOffice"),
        ("garage", "\U0001F697", "\U0001F527", "RoomGarage"),
        ("laundry", "\U0001F455", "\U0001F9FA", "RoomLaundry"),
        ("dining", "\U0001F37D\uFE0F", "\U0001FA91", "RoomDining"),
    };

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
        OrderDifficulty.Hard or OrderDifficulty.Expert => 4 * 84,
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
        }

        InitializeGame();
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
            OrderDifficulty.Expert => (8, 30),
            _ => (6, 45)
        };

        // Tool-Bonus: Pinsel gibt Extra-Sekunden (kein spezifisches DesignPuzzle-Tool, verwende Paintbrush)
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Paintbrush);
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

        TotalSlots = roomCount;

        // Slots erstellen (in zufaelliger Reihenfolge fuer den Grundriss)
        var shuffledForSlots = selectedRooms.OrderBy(_ => random.Next()).ToArray();
        foreach (var room in shuffledForSlots)
        {
            Slots.Add(new RoomSlot
            {
                CorrectRoomId = room.Id,
                HintIcon = room.HintIcon
            });
        }

        // Raum-Karten erstellen (ebenfalls gemischt)
        var shuffledForCards = selectedRooms.OrderBy(_ => random.Next()).ToArray();
        foreach (var room in shuffledForCards)
        {
            AvailableRooms.Add(new RoomCard
            {
                RoomId = room.Id,
                Emoji = room.Emoji,
                NameKey = room.NameKey,
                DisplayName = _localizationService.GetString(room.NameKey)
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
            slot.DisplayEmoji = room.Emoji;

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
        if (order != null)
        {
            int taskCount = Math.Max(1, order.Tasks.Count);
            decimal baseReward = order.BaseReward / taskCount;
            RewardAmount = baseReward * Result.GetRewardPercentage();

            int baseXp = order.BaseXp / taskCount;
            XpAmount = (int)(baseXp * Result.GetXpPercentage());
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

    [ObservableProperty]
    private string _hintIcon = "";

    [ObservableProperty]
    private bool _isFilled;

    [ObservableProperty]
    private bool _isCorrect;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _displayEmoji = "";

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

    [ObservableProperty]
    private string _emoji = "";

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
