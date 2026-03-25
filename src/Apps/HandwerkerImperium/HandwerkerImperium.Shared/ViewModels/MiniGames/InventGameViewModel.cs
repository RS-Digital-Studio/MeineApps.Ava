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
/// ViewModel für das Erfinder-Puzzle-Minispiel.
/// Der Spieler merkt sich die Montage-Reihenfolge der Bauteile und tippt sie danach korrekt an.
/// </summary>
public sealed partial class InventGameViewModel : BaseMiniGameViewModel
{
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
    // SPIEL-SPEZIFISCHE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

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

    /// <summary>
    /// Breite des Grids in Pixeln für WrapPanel-Constraint.
    /// Jedes Teil: 68px + 6px Margin = 74px.
    /// </summary>
    public double GridWidth => _gridColumns * 74;

    private int _gridColumns = 3;

    // ═══════════════════════════════════════════════════════════════════════
    // BASIS-KLASSE IMPLEMENTIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    protected override MiniGameType GameMiniGameType => MiniGameType.InventGame;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public InventGameViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        IRewardedAdService rewardedAdService,
        ILocalizationService localizationService)
        : base(gameStateService, audioService, rewardedAdService, localizationService)
    {
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-INITIALISIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    protected override void InitializeGame()
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
        IsMemorizing = false;

        OnPropertyChanged(nameof(GridWidth));

        GenerateParts();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MEMORISIERUNGSPHASE (vor Timer-Start)
    // ═══════════════════════════════════════════════════════════════════════

    protected override async Task OnPreGameStartAsync()
    {
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

        // Nummern verstecken
        foreach (var part in Parts)
        {
            part.IsRevealed = false;
        }
        IsMemorizing = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TIMER-TICK
    // ═══════════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-LOGIK
    // ═══════════════════════════════════════════════════════════════════════

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
            ResetErrorAsync(part).SafeFireAndForget();
        }
    }

    private static async Task ResetErrorAsync(InventPart part)
    {
        await Task.Delay(500);
        part.HasError = false;
    }

    private async Task EndGameAsync()
    {
        if (!StopGame()) return;

        // Rating berechnen basierend auf Leistung
        bool allCompleted = CompletedParts >= TotalParts;
        double timeRatio = MaxTime > 0 ? (double)TimeRemaining / MaxTime : 0;

        MiniGameRating rating;
        if (allCompleted && MistakeCount == 0 && timeRatio > 0.4)
            rating = MiniGameRating.Perfect;
        else if (allCompleted && MistakeCount <= 2 && timeRatio > 0.2)
            rating = MiniGameRating.Good;
        else if (allCompleted)
            rating = MiniGameRating.Ok;
        else
            rating = MiniGameRating.Miss;

        await ShowResultAsync(rating);
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
