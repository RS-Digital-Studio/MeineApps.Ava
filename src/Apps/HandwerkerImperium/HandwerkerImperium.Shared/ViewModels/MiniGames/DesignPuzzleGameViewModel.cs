using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.Icons;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;
using HandwerkerImperium.Helpers;

namespace HandwerkerImperium.ViewModels.MiniGames;

/// <summary>
/// ViewModel für das Grundriss-Rätsel Mini-Game.
/// Der Spieler muss Räume einem Grundriss korrekt zuordnen.
/// </summary>
public sealed partial class DesignPuzzleGameViewModel : BaseMiniGameViewModel
{
    // Raum-Definitionen: Id, IconKind, IconColor, NameKey
    private static readonly (string Id, GameIconKind IconKind, string IconColor, string NameKey)[] RoomDefs =
    {
        ("kitchen", GameIconKind.Stove, "#FF6F00", "RoomKitchen"),
        ("bathroom", GameIconKind.ShowerHead, "#0288D1", "RoomBathroom"),
        ("bedroom", GameIconKind.Bed, "#7B1FA2", "RoomBedroom"),
        ("living", GameIconKind.Sofa, "#2E7D32", "RoomLiving"),
        ("office", GameIconKind.Laptop, "#455A64", "RoomOffice"),
        ("garage", GameIconKind.Garage, "#795548", "RoomGarage"),
        ("laundry", GameIconKind.WashingMachine, "#00838F", "RoomLaundry"),
        ("dining", GameIconKind.SilverwareForkKnife, "#C62828", "RoomDining"),
        ("hallway", GameIconKind.DoorOpen, "#5D4037", "RoomHallway"),
        ("basement", GameIconKind.Stairs, "#37474F", "RoomBasement"),
    };

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-SPEZIFISCHE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Breite des Grundriss-Grids in Pixeln für WrapPanel.
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

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Difficulty))
            OnPropertyChanged(nameof(GridWidth));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BASIS-KLASSE OVERRIDES
    // ═══════════════════════════════════════════════════════════════════════

    protected override MiniGameType GameMiniGameType => MiniGameType.DesignPuzzle;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public DesignPuzzleGameViewModel(
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
        // Schwierigkeitsabhängige Parameter
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

    private void GeneratePuzzle(int roomCount)
    {
        Slots.Clear();
        AvailableRooms.Clear();

        // Zufällige Raum-Auswahl
        var random = Random.Shared;
        var selectedRooms = RoomDefs
            .OrderBy(_ => random.Next())
            .Take(roomCount)
            .ToArray();

        TotalSlots = selectedRooms.Length;

        // Slots erstellen (in zufälliger Reihenfolge für den Grundriss)
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

    /// <summary>
    /// Raum-Karte auswählen.
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
    /// Ausgewählten Raum in einen Slot platzieren.
    /// </summary>
    [RelayCommand]
    private async Task PlaceRoomAsync(RoomSlot? slot)
    {
        if (slot == null || !IsPlaying || IsResultShown || slot.IsFilled) return;
        if (SelectedRoom == null) return;

        var room = SelectedRoom;

        // Prüfen ob der Raum korrekt platziert wurde
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

            // Prüfen ob alle Slots gefüllt sind
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

            // Fehler-Anzeige nach kurzer Verzögerung zurücksetzen
            await Task.Delay(400);
            slot.HasError = false;
        }
    }

    private async Task EndGameAsync()
    {
        if (!StopGame()) return;

        // Auswahl aufheben
        if (SelectedRoom != null)
        {
            SelectedRoom.IsSelected = false;
            SelectedRoom = null;
        }

        // Rating berechnen basierend auf Fortschritt, Fehlern und verbleibender Zeit
        bool allPlaced = PlacedCount >= TotalSlots;
        double timeRatio = MaxTime > 0 ? (double)TimeRemaining / MaxTime : 0;

        MiniGameRating rating;
        if (allPlaced && MistakeCount == 0 && timeRatio > 0.5)
        {
            rating = MiniGameRating.Perfect;
        }
        else if (allPlaced && MistakeCount <= 2 && timeRatio > 0.25)
        {
            rating = MiniGameRating.Good;
        }
        else if (allPlaced)
        {
            rating = MiniGameRating.Ok;
        }
        else
        {
            // Zeit abgelaufen - teilweise Bewertung
            double completionRatio = TotalSlots > 0 ? (double)PlacedCount / TotalSlots : 0;
            rating = completionRatio >= 0.75 ? MiniGameRating.Ok : MiniGameRating.Miss;
        }

        await ShowResultAsync(rating);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// HILFSTYPEN
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Platzhalter-Slot im Grundriss für einen Raum.
/// </summary>
public partial class RoomSlot : ObservableObject
{
    [ObservableProperty]
    private string _correctRoomId = "";

    [ObservableProperty]
    private string _currentRoomId = "";

    /// <summary>Farbe als Hinweis welcher Raum hierhin gehört.</summary>
    [ObservableProperty]
    private string _hintColor = "#555555";

    /// <summary>Icon-Typ als Hinweis (wird im Renderer als Farb-Akzent verwendet).</summary>
    [ObservableProperty]
    private GameIconKind _hintIconKind = GameIconKind.HomeRoof;

    [ObservableProperty]
    private bool _isFilled;

    [ObservableProperty]
    private bool _isCorrect;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>Lokalisierter Raumname für gefüllte Slots.</summary>
    [ObservableProperty]
    private string _displayLabel = "";

    /// <summary>Farbe des platzierten Raums.</summary>
    [ObservableProperty]
    private string _filledColor = "";

    // Berechnete Farben bei Zustandsänderung aktualisieren
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
/// Raum-Karte zur Auswahl für den Spieler.
/// </summary>
public partial class RoomCard : ObservableObject
{
    [ObservableProperty]
    private string _roomId = "";

    /// <summary>Material Icon für den Raum.</summary>
    [ObservableProperty]
    private GameIconKind _iconKind = GameIconKind.HomeRoof;

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

    // Berechnete Properties bei Zustandsänderung aktualisieren
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

    /// <summary>Rand-Farbe wenn ausgewählt.</summary>
    public string SelectionBorderColor => IsSelected ? "#FFD700" : "Transparent";
}
