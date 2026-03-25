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
/// ViewModel für das Bauplan-Reihenfolge-Minispiel.
/// Der Spieler merkt sich die Reihenfolge der Bauschritte und tippt sie danach korrekt an.
/// </summary>
public sealed partial class BlueprintGameViewModel : BaseMiniGameViewModel
{
    // Bauschritt-Icons (Vektor-Identifikatoren für SkiaSharp-Rendering)
    private static readonly string[] StepIcons =
    {
        "foundation",   // Fundament
        "walls",        // Mauern
        "framework",    // Rahmenwerk
        "electrics",    // Elektrik
        "plumbing",     // Sanitär
        "windows",      // Fenster
        "doors",        // Türen
        "painting",     // Malerei
        "roof",         // Dach
        "fittings",     // Beschläge
        "measuring",    // Messen
        "scaffolding"   // Gerüst
    };

    // Lokalisierte Bauschritt-Labels (Keys)
    private static readonly string[] StepLabelKeys =
        { "BlueprintFoundation", "BlueprintWalls", "BlueprintFramework", "BlueprintElectrics", "BlueprintPlumbing", "BlueprintWindows", "BlueprintDoors", "BlueprintPainting", "BlueprintRoof", "BlueprintFittings", "BlueprintMeasuring", "BlueprintScaffolding" };

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL-SPEZIFISCHE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<BlueprintStep> _steps = [];

    [ObservableProperty]
    private bool _isMemorizing;

    [ObservableProperty]
    private int _nextExpectedStep = 1;

    [ObservableProperty]
    private int _mistakeCount;

    [ObservableProperty]
    private int _completedSteps;

    [ObservableProperty]
    private int _totalSteps;

    [ObservableProperty]
    private int _timeRemaining;

    [ObservableProperty]
    private int _maxTime;

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Breite des Grids in Pixeln für WrapPanel-Constraint.
    /// Jeder Schritt: 68px + 6px Margin = 74px.
    /// </summary>
    public double GridWidth => _gridColumns * 74;

    private int _gridColumns = 3;

    // ═══════════════════════════════════════════════════════════════════════
    // BASIS-KLASSE OVERRIDES
    // ═══════════════════════════════════════════════════════════════════════

    protected override MiniGameType GameMiniGameType => MiniGameType.Blueprint;

    protected override bool PlaySoundBeforeCountdown => false;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public BlueprintGameViewModel(
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
        // Schwierigkeit bestimmt Schrittanzahl, Grid-Spalten, Memorisierungs-Zeit und Spielzeit
        (TotalSteps, _gridColumns, MaxTime) = Difficulty switch
        {
            OrderDifficulty.Easy => (6, 3, 45),
            OrderDifficulty.Medium => (9, 3, 35),
            OrderDifficulty.Hard => (12, 4, 25),
            OrderDifficulty.Expert => (16, 4, 20),
            _ => (9, 3, 35)
        };

        // Tool-Bonus: Wasserwaage gibt Extra-Sekunden
        var tool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.SpiritLevel);
        TimeRemaining = MaxTime + (tool?.TimeBonus ?? 0);
        CompletedSteps = 0;
        MistakeCount = 0;
        NextExpectedStep = 1;
        IsMemorizing = false;

        OnPropertyChanged(nameof(GridWidth));

        GenerateSteps();
    }

    protected override async Task OnPreGameStartAsync()
    {
        // Memorisierungsphase: Alle Nummern aufdecken
        IsMemorizing = true;
        foreach (var step in Steps)
        {
            step.IsRevealed = true;
        }

        // Memorisierungszeit je nach Schwierigkeit
        int memorizeMs = Difficulty switch
        {
            OrderDifficulty.Easy => 4000,
            OrderDifficulty.Medium => 3000,
            OrderDifficulty.Hard => 2500,
            OrderDifficulty.Expert => 2000,
            _ => 3000
        };

        await Task.Delay(memorizeMs);

        // Nummern verstecken
        foreach (var step in Steps)
        {
            step.IsRevealed = false;
        }
        IsMemorizing = false;
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

    private void GenerateSteps()
    {
        Steps.Clear();

        // Zufällige Auswahl und Anordnung der Schritte
        var indices = Enumerable.Range(0, StepIcons.Length).ToList();
        // Mischen
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < TotalSteps; i++)
        {
            int iconIndex = indices[i % indices.Count];
            string label = _localizationService.GetString(StepLabelKeys[iconIndex]) ?? StepLabelKeys[iconIndex];

            Steps.Add(new BlueprintStep
            {
                StepNumber = i + 1,
                Icon = StepIcons[iconIndex],
                Label = label,
                IsRevealed = false,
                IsCompleted = false,
                HasError = false
            });
        }

        // Positionen im Grid mischen (Nummern bleiben, aber physische Position variiert)
        var shuffled = Steps.OrderBy(_ => Random.Shared.Next()).ToList();
        Steps.Clear();
        foreach (var step in shuffled)
        {
            Steps.Add(step);
        }
    }

    [RelayCommand]
    private async Task SelectStepAsync(BlueprintStep? step)
    {
        if (step == null || !IsPlaying || IsResultShown || step.IsCompleted) return;

        if (step.StepNumber == NextExpectedStep)
        {
            // Korrekt! Schritt als erledigt markieren
            step.IsCompleted = true;
            step.HasError = false;
            CompletedSteps++;
            NextExpectedStep++;

            await _audioService.PlaySoundAsync(GameSound.Good);

            // Alle Schritte erledigt?
            if (CompletedSteps >= TotalSteps)
            {
                await EndGameAsync();
            }
        }
        else
        {
            // Falsch! Kurzes rotes Blinken
            MistakeCount++;
            step.HasError = true;

            await _audioService.PlaySoundAsync(GameSound.Miss);

            // Fehler nach kurzer Zeit zurücksetzen
            ResetErrorAsync(step).SafeFireAndForget();
        }
    }

    private static async Task ResetErrorAsync(BlueprintStep step)
    {
        await Task.Delay(500);
        step.HasError = false;
    }

    private async Task EndGameAsync()
    {
        if (!StopGame()) return;

        // Rating berechnen basierend auf Leistung
        bool allCompleted = CompletedSteps >= TotalSteps;
        double timeRatio = MaxTime > 0 ? (double)TimeRemaining / MaxTime : 0;

        MiniGameRating rating;
        if (allCompleted && MistakeCount == 0 && timeRatio > 0.4)
        {
            rating = MiniGameRating.Perfect;
        }
        else if (allCompleted && MistakeCount <= 2 && timeRatio > 0.2)
        {
            rating = MiniGameRating.Good;
        }
        else if (allCompleted)
        {
            rating = MiniGameRating.Ok;
        }
        else
        {
            rating = MiniGameRating.Miss;
        }

        await ShowResultAsync(rating);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CANCEL OVERRIDE (setzt zusätzlich IsMemorizing zurück)
    // ═══════════════════════════════════════════════════════════════════════

    public override void SetOrderId(string orderId)
    {
        IsMemorizing = false;
        base.SetOrderId(orderId);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// HILFSTYPEN
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Repräsentiert einen einzelnen Bauschritt im Bauplan-Spiel.
/// </summary>
public partial class BlueprintStep : ObservableObject
{
    [ObservableProperty]
    private int _stepNumber; // Korrekte Reihenfolgenummer (1-basiert)

    [ObservableProperty]
    private string _icon = ""; // Vektor-Icon-Identifier

    [ObservableProperty]
    private bool _isRevealed; // Nummer sichtbar (Memorisierungsphase)

    [ObservableProperty]
    private bool _isCompleted; // Wurde korrekt angetippt

    [ObservableProperty]
    private bool _hasError; // Wurde falsch angetippt (kurzes Blinken)

    [ObservableProperty]
    private string _label = ""; // Beschreibungstext (z.B. "Fundament")

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
    public string BackgroundColor => IsCompleted ? "#4CAF50" : (HasError ? "#F44336" : "#2A2A2A");
}
