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

    /// <summary>Anzahl Köder-Teile (unterscheidet Invent von Blueprint).</summary>
    private int _decoyCount;

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
        // Schwierigkeit bestimmt Teileanzahl, Grid-Spalten, Spielzeit und Köder-Anzahl
        // Köder-Teile (Decoys) differenzieren Invent von Blueprint:
        // Nach der Memorisierung werden Köder ins Grid gemischt — Spieler muss echte Teile erkennen
        // WICHTIG: realParts + decoys ≤ 12 (PartIcons.Length), damit jedes Teil ein einzigartiges Icon hat
        (TotalParts, _gridColumns, MaxTime, _decoyCount) = Difficulty switch
        {
            OrderDifficulty.Easy => (6, 2, 28, 1),     // 7 total — 6 Icons belegt, 6 frei für Köder
            OrderDifficulty.Medium => (7, 3, 36, 2),    // 9 total — reichlich einzigartige Icons
            OrderDifficulty.Hard => (8, 3, 40, 3),      // 11 total — 1 Icon übrig
            OrderDifficulty.Expert => (8, 4, 36, 4),    // 12 total — alle 12 Icons genutzt, max Grid
            _ => (7, 3, 36, 2)
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
        // Memorisierungsphase: Alle echten Teile mit Nummern aufdecken (Köder noch nicht sichtbar)
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

        // Köder-Teile ins Grid einschleusen (NACH der Memorisierung)
        InjectDecoys();

        IsMemorizing = false;
    }

    /// <summary>
    /// Fügt Köder-Teile (Decoys) ins Grid ein und mischt alle Positionen neu.
    /// Köder zeigen "?" wie echte versteckte Teile — Spieler muss sich erinnern welche echt waren.
    /// </summary>
    private void InjectDecoys()
    {
        if (_decoyCount <= 0) return;

        // Welche Icons sind bereits im Spiel? → Köder verwenden unbenutzte Icons
        var usedIcons = new HashSet<string>();
        foreach (var part in Parts)
            usedIcons.Add(part.Icon);

        var availableDecoyIcons = new List<string>();
        for (int i = 0; i < PartIcons.Length; i++)
        {
            if (!usedIcons.Contains(PartIcons[i]))
                availableDecoyIcons.Add(PartIcons[i]);
        }

        // Köder generieren
        for (int i = 0; i < _decoyCount; i++)
        {
            string icon = availableDecoyIcons.Count > 0
                ? availableDecoyIcons[Random.Shared.Next(availableDecoyIcons.Count)]
                : PartIcons[Random.Shared.Next(PartIcons.Length)];

            // Benutzten Köder-Icon nicht wiederholen
            availableDecoyIcons.Remove(icon);

            string label = _localizationService.GetString("InventDecoyPart") ?? "???";

            Parts.Add(new InventPart
            {
                StepNumber = -1, // Markierung: kein gültiger Schritt
                Icon = icon,
                Label = label,
                IsRevealed = false,
                IsCompleted = false,
                HasError = false,
                IsDecoy = true
            });
        }

        // Alle Positionen neu mischen (echte + Köder)
        var shuffled = Parts.OrderBy(_ => Random.Shared.Next()).ToList();
        Parts.Clear();
        foreach (var part in shuffled)
            Parts.Add(part);

        // Grid-Breite aktualisieren (evtl. eine Spalte mehr nötig)
        int totalCount = TotalParts + _decoyCount;
        if (totalCount > _gridColumns * (_gridColumns + 2))
            _gridColumns = Math.Min(_gridColumns + 1, 5);
        OnPropertyChanged(nameof(GridWidth));
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

        // Köder angetippt → Fehler + Teil wird als "entlarvt" markiert
        if (part.IsDecoy)
        {
            MistakeCount++;
            part.HasError = true;
            part.IsCompleted = true; // Entlarvter Köder wird ausgegraut

            await _audioService.PlaySoundAsync(GameSound.Miss);

            // Fehler-Anzeige nach kurzer Zeit zurücksetzen (IsCompleted bleibt)
            ResetErrorAsync(part).SafeFireAndForget();
            return;
        }

        if (part.StepNumber == NextExpectedPart)
        {
            // Korrekt! Teil als erledigt markieren
            part.IsCompleted = true;
            part.HasError = false;
            CompletedParts++;
            NextExpectedPart++;

            await _audioService.PlaySoundAsync(GameSound.Good);

            // Alle echten Teile erledigt?
            if (CompletedParts >= TotalParts)
            {
                await EndGameAsync();
            }
        }
        else
        {
            // Falsche Reihenfolge! Kurzes rotes Blinken
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
    private int _stepNumber; // Korrekte Montage-Reihenfolge (1-basiert, -1 = Köder)

    [ObservableProperty]
    private string _icon = ""; // Vektor-Icon-Identifier (z.B. "gear", "piston")

    [ObservableProperty]
    private bool _isRevealed; // Nummer sichtbar (Memorisierungsphase)

    [ObservableProperty]
    private bool _isCompleted; // Wurde korrekt angetippt (oder Köder entlarvt)

    [ObservableProperty]
    private bool _hasError; // Wurde falsch angetippt (kurzes Blinken)

    [ObservableProperty]
    private string _label = ""; // Beschreibungstext (z.B. "Zahnrad")

    /// <summary>Köder-Teil das nicht zur Montage-Reihenfolge gehört.</summary>
    [ObservableProperty]
    private bool _isDecoy;

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
    /// Angezeigte Nummer: Sichtbar während Memorisierung und nach Abschluss.
    /// Köder zeigen "X" wenn entlarvt, echte Teile ihre Nummer, sonst "?".
    /// </summary>
    public string DisplayNumber
    {
        get
        {
            if (IsDecoy) return IsCompleted ? "X" : "?";
            return IsRevealed || IsCompleted ? StepNumber.ToString() : "?";
        }
    }

    /// <summary>
    /// Hintergrundfarbe basierend auf Zustand.
    /// Entlarvte Köder: Orange, korrekte Teile: Grün, Fehler: Rot, Standard: Dunkelviolett.
    /// </summary>
    public string BackgroundColor
    {
        get
        {
            if (HasError) return "#F44336";
            if (IsCompleted && IsDecoy) return "#FF9800"; // Entlarvter Köder: Orange
            if (IsCompleted) return "#4CAF50";
            return "#2A1A40";
        }
    }
}
