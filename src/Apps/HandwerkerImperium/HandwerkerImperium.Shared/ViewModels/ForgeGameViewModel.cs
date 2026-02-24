using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer das Schmiede-Minigame.
/// Spieler muss bei richtiger Temperatur auf den Amboss haemmern.
/// Temperatur steigt automatisch, kuehlt nach Hammer-Schlag ab.
/// </summary>
public partial class ForgeGameViewModel : ObservableObject, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly ILocalizationService _localizationService;
    private DispatcherTimer? _timer;
    private bool _disposed;
    private bool _isEnding;

    // Spiel-Konfiguration
    private const double TICK_INTERVAL_MS = 16; // ~60 FPS
    private const double HEAT_RATE = 0.008;     // Aufheiz-Geschwindigkeit pro Tick
    private const double COOL_RATE = 0.25;      // Abkuehl-Menge nach Hammer-Schlag
    private const double COOL_DECAY = 0.003;    // Natuerliche Abkuehlung pro Tick (langsam)

    // Sinus-basierte Temperatur-Oszillation
    private double _heatTime;
    private double _heatDirection = 1.0;

    // ===================================================================
    // EVENTS
    // ===================================================================

    public event Action<string>? NavigationRequested;

    /// <summary>Wird beim Spielstart nach Countdown gefeuert.</summary>
    public event EventHandler? GameStarted;

    /// <summary>Wird nach Spielende mit Rating (0-3 Sterne) gefeuert.</summary>
    public event EventHandler<int>? GameCompleted;

    /// <summary>Wird bei Zonen-Treffer gefeuert (Zone-Name: "Perfect", "Good", "Ok", "Miss").</summary>
    public event EventHandler<string>? ZoneHit;

    // ===================================================================
    // OBSERVABLE PROPERTIES
    // ===================================================================

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    private OrderDifficulty _difficulty = OrderDifficulty.Medium;

    [ObservableProperty]
    private MiniGameType _gameType = MiniGameType.ForgeGame;

    [ObservableProperty]
    private string _gameTitle = "";

    [ObservableProperty]
    private string _gameIcon = "Anvil";

    [ObservableProperty]
    private string _actionButtonText = "";

    [ObservableProperty]
    private string _instructionText = "";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isResultShown;

    // Temperatur des Werkstuecks (0.0 = kalt, 1.0 = weissglühend)
    [ObservableProperty]
    private double _temperature;

    // Zielzone (Perfect-Temperatur)
    [ObservableProperty]
    private double _targetTemperatureStart;

    [ObservableProperty]
    private double _targetTemperatureWidth;

    // Gut-Zone (etwas breiter als Perfect)
    [ObservableProperty]
    private double _goodTemperatureStart;

    [ObservableProperty]
    private double _goodTemperatureWidth;

    // Ok-Zone (noch breiter)
    [ObservableProperty]
    private double _okTemperatureStart;

    [ObservableProperty]
    private double _okTemperatureWidth;

    // Benoetigte und abgeschlossene Schlaege
    [ObservableProperty]
    private int _hitsRequired;

    [ObservableProperty]
    private int _hitsCompleted;

    // Treffer-Statistik pro Zone
    [ObservableProperty]
    private int _perfectHits;

    [ObservableProperty]
    private int _goodHits;

    [ObservableProperty]
    private int _okHits;

    [ObservableProperty]
    private int _missHits;

    // Ob gerade aufgeheizt wird (fuer visuelle Effekte)
    [ObservableProperty]
    private bool _isHeating = true;

    // Ob gerade gehaemmert wird (kurze Animation)
    [ObservableProperty]
    private bool _isHammering;

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
    private double _speedMultiplier = 1.0;

    [ObservableProperty]
    private bool _canWatchAd;

    /// <summary>Fortschritts-Anzeige z.B. "Aufgabe 2/3" (leer bei QuickJobs/Einzelaufgaben).</summary>
    [ObservableProperty]
    private string _taskProgressDisplay = "";

    /// <summary>Ob dies die letzte Aufgabe des Auftrags ist (bestimmt ob Belohnungen angezeigt werden).</summary>
    [ObservableProperty]
    private bool _isLastTask;

    /// <summary>Text fuer den Continue-Button ("Naechste Aufgabe" oder "Weiter").</summary>
    [ObservableProperty]
    private string _continueButtonText = "";

    [ObservableProperty]
    private bool _adWatched;

    // Countdown vor Spielstart
    [ObservableProperty]
    private bool _isCountdownActive;

    [ObservableProperty]
    private string _countdownText = "";

    // Sterne-Anzeige (staggered: 0 -> 1 mit Verzoegerung)
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

    // ===================================================================
    // COMPUTED PROPERTIES
    // ===================================================================

    public string DifficultyStars => Difficulty switch
    {
        OrderDifficulty.Easy => "★☆☆",
        OrderDifficulty.Medium => "★★☆",
        OrderDifficulty.Hard => "★★★",
        OrderDifficulty.Expert => "★★★★",
        _ => "★☆☆"
    };

    /// <summary>Fortschritts-Anzeige fuer Schlaege: "3/5"</summary>
    public string HitsProgressDisplay => $"{HitsCompleted}/{HitsRequired}";

    // ===================================================================
    // PROPERTY CHANGE HANDLERS
    // ===================================================================

    partial void OnDifficultyChanged(OrderDifficulty value) => OnPropertyChanged(nameof(DifficultyStars));

    partial void OnHitsCompletedChanged(int value) => OnPropertyChanged(nameof(HitsProgressDisplay));

    partial void OnHitsRequiredChanged(int value) => OnPropertyChanged(nameof(HitsProgressDisplay));

    // ===================================================================
    // CONSTRUCTOR
    // ===================================================================

    public ForgeGameViewModel(
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

    // ===================================================================
    // INITIALIZATION
    // ===================================================================

    /// <summary>
    /// Initialisiert das Spiel mit einer Auftrags-ID.
    /// </summary>
    public void SetOrderId(string orderId)
    {
        OrderId = orderId;

        // Zustand zuruecksetzen
        IsPlaying = false;
        IsResultShown = false;

        // Schwierigkeit und Spieltyp aus aktivem Auftrag
        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            Difficulty = activeOrder.Difficulty;

            // Fortschritts-Anzeige: "Aufgabe X/Y"
            int totalTasks = activeOrder.Tasks.Count;
            int currentTaskNum = activeOrder.CurrentTaskIndex + 1;
            if (totalTasks > 1)
            {
                var taskLabel = _localizationService.GetString("TaskProgress");
                TaskProgressDisplay = string.Format(taskLabel, currentTaskNum, totalTasks);
            }
            else
            {
                TaskProgressDisplay = "";
            }

            // Letzte Aufgabe?
            IsLastTask = currentTaskNum >= totalTasks;
            ContinueButtonText = IsLastTask
                ? _localizationService.GetString("Continue")
                : _localizationService.GetString("NextTask");

            // Aktuellen Task-Typ pruefen
            var currentTask = activeOrder.CurrentTask;
            if (currentTask != null)
            {
                GameType = currentTask.GameType;
            }
        }
        else
        {
            // QuickJob: Immer letzte (einzige) Aufgabe
            TaskProgressDisplay = "";
            IsLastTask = true;
            ContinueButtonText = _localizationService.GetString("Continue");
        }

        UpdateGameTypeVisuals();
        InitializeZones();
        CheckAndShowTutorial(GameType);
    }

    private void UpdateGameTypeVisuals()
    {
        string L(string key) => _localizationService.GetString(key);

        GameTitle = L("ForgeGameTitle");
        GameIcon = "Anvil";
        ActionButtonText = L("HammerNow");
        InstructionText = L("HammerAtRightTemperature");
    }

    // ===================================================================
    // GAME LOGIC
    // ===================================================================

    private void InitializeZones()
    {
        // Zielzonen basierend auf Schwierigkeit
        double perfectSize = Difficulty.GetPerfectZoneSize();

        // Tool-Bonus: Hammer vergroessert die Zielzone
        var hammerTool = _gameStateService.State.Tools.FirstOrDefault(t => t.Type == Models.ToolType.Hammer);
        if (hammerTool != null) perfectSize += perfectSize * hammerTool.ZoneBonus;

        double goodSize = perfectSize * 2;
        double okSize = perfectSize * 3;

        // Ziel-Temperatur zufaellig (zwischen 0.3 und 0.8)
        var random = Random.Shared;
        double targetCenter = 0.35 + (random.NextDouble() * 0.3);

        // Zonen-Positionen (zentriert auf Ziel)
        TargetTemperatureWidth = perfectSize;
        TargetTemperatureStart = targetCenter - (perfectSize / 2);

        GoodTemperatureWidth = goodSize;
        GoodTemperatureStart = targetCenter - (goodSize / 2);

        OkTemperatureWidth = okSize;
        OkTemperatureStart = targetCenter - (okSize / 2);

        // Geschwindigkeit basierend auf Schwierigkeit
        SpeedMultiplier = Difficulty.GetSpeedMultiplier();

        // Benoetigte Schlaege basierend auf Schwierigkeit
        HitsRequired = Difficulty switch
        {
            OrderDifficulty.Easy => 3,
            OrderDifficulty.Medium => 5,
            OrderDifficulty.Hard => 7,
            OrderDifficulty.Expert => 10,
            _ => 5
        };

        // Zuruecksetzen
        Temperature = 0;
        HitsCompleted = 0;
        PerfectHits = 0;
        GoodHits = 0;
        OkHits = 0;
        MissHits = 0;
        _heatTime = 0;
        _heatDirection = 1.0;
        IsHeating = true;
        IsHammering = false;
    }

    [RelayCommand]
    private async Task StartGameAsync()
    {
        if (IsPlaying || IsCountdownActive) return;

        IsResultShown = false;
        _isEnding = false;
        Temperature = 0;
        HitsCompleted = 0;
        PerfectHits = 0;
        GoodHits = 0;
        OkHits = 0;
        MissHits = 0;
        _heatTime = 0;
        _heatDirection = 1.0;

        // Countdown 3-2-1-Los!
        IsCountdownActive = true;
        foreach (var text in new[] { "3", "2", "1", _localizationService.GetString("CountdownGo") })
        {
            CountdownText = text;
            await Task.Delay(700);
        }
        IsCountdownActive = false;

        // Spiel starten
        GameStarted?.Invoke(this, EventArgs.Empty);
        IsPlaying = true;
        if (_timer != null) { _timer.Stop(); _timer.Tick -= OnTimerTick; }
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(TICK_INTERVAL_MS)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying) return;

        _heatTime += TICK_INTERVAL_MS / 1000.0;

        // Temperatur steigt mit Sinus-Oszillation (natuerliches Auf und Ab der Esse)
        double heatWave = Math.Sin(_heatTime * 2.5 * SpeedMultiplier) * 0.3;
        double baseHeat = HEAT_RATE * SpeedMultiplier;

        // Temperatur aendern: steigt tendenziell, mit Oszillation
        Temperature += (baseHeat + heatWave * 0.005) * _heatDirection;

        // Natuerliche Abkuehlung (immer leicht, staerker bei hoher Temperatur)
        Temperature -= COOL_DECAY * Temperature * SpeedMultiplier;

        // Grenzen einhalten
        if (Temperature >= 1.0)
        {
            Temperature = 1.0;
            _heatDirection = -0.5; // Kehrt um, kuehlt langsam
        }
        else if (Temperature <= 0.0)
        {
            Temperature = 0.0;
            _heatDirection = 1.0; // Heizt wieder auf
        }

        // Richtung langsam normalisieren
        if (_heatDirection < 1.0)
        {
            _heatDirection += 0.002 * SpeedMultiplier;
            if (_heatDirection > 1.0) _heatDirection = 1.0;
        }

        IsHeating = _heatDirection > 0;
    }

    [RelayCommand]
    private async Task HammerStrikeAsync()
    {
        if (!IsPlaying || _isEnding || IsHammering) return;

        // Hammer-Animation starten
        IsHammering = true;

        // Aktuellen Treffer auswerten
        var hitRating = CalculateHitRating(Temperature);
        string zoneName = hitRating.GetLocalizationKey();

        // Treffer zaehlen
        switch (hitRating)
        {
            case MiniGameRating.Perfect: PerfectHits++; break;
            case MiniGameRating.Good: GoodHits++; break;
            case MiniGameRating.Ok: OkHits++; break;
            default: MissHits++; break;
        }

        HitsCompleted++;

        // Zonen-Treffer Event feuern
        ZoneHit?.Invoke(this, zoneName);

        // Sound abspielen
        var sound = hitRating switch
        {
            MiniGameRating.Perfect => GameSound.Perfect,
            MiniGameRating.Good => GameSound.Good,
            MiniGameRating.Ok => GameSound.ButtonTap,
            _ => GameSound.Miss
        };
        await _audioService.PlaySoundAsync(sound);

        // Temperatur sinkt nach Hammerschlag (Werkstueck kuehlt durch Verformung)
        Temperature = Math.Max(0, Temperature - COOL_RATE);
        _heatDirection = 1.0; // Sofort wieder aufheizen

        // Kurze Hammer-Animation-Dauer
        await Task.Delay(150);
        IsHammering = false;

        // Pruefen ob alle Schlaege erledigt
        if (HitsCompleted >= HitsRequired)
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

        // Gesamt-Rating basierend auf Treffer-Verteilung
        Result = CalculateOverallRating();

        // Ergebnis im GameState speichern
        _gameStateService.RecordMiniGameResult(Result);

        // Belohnungen berechnen
        var order = _gameStateService.GetActiveOrder();
        if (order != null && IsLastTask)
        {
            RewardAmount = order.FinalReward;
            XpAmount = order.FinalXp;
        }
        else if (order != null)
        {
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
            // QuickJob
            var quickJob = _gameStateService.State.ActiveQuickJob;
            RewardAmount = quickJob?.Reward ?? 0;
            XpAmount = quickJob?.XpReward ?? 0;
        }

        // Ergebnis-Anzeige
        ResultText = _localizationService.GetString(Result.GetLocalizationKey());
        ResultEmoji = Result switch
        {
            MiniGameRating.Perfect => "★★★",
            MiniGameRating.Good => "★★",
            MiniGameRating.Ok => "★",
            _ => "---"
        };

        IsResultShown = true;

        // Sterne-Bewertung
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
            // Zwischen-Runde: Sterne sofort setzen
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
            RewardAmount *= 2;
            XpAmount *= 2;
            AdWatched = true;
            CanWatchAd = false;

            await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
        }
    }

    /// <summary>
    /// Bewertet einen einzelnen Hammerschlag basierend auf der Temperatur.
    /// </summary>
    private MiniGameRating CalculateHitRating(double temp)
    {
        // Perfect-Zone pruefen
        if (temp >= TargetTemperatureStart && temp <= TargetTemperatureStart + TargetTemperatureWidth)
        {
            return MiniGameRating.Perfect;
        }

        // Good-Zone pruefen
        if (temp >= GoodTemperatureStart && temp <= GoodTemperatureStart + GoodTemperatureWidth)
        {
            return MiniGameRating.Good;
        }

        // Ok-Zone pruefen
        if (temp >= OkTemperatureStart && temp <= OkTemperatureStart + OkTemperatureWidth)
        {
            return MiniGameRating.Ok;
        }

        // Daneben
        return MiniGameRating.Miss;
    }

    /// <summary>
    /// Berechnet das Gesamtergebnis basierend auf allen Schlaegen.
    /// </summary>
    private MiniGameRating CalculateOverallRating()
    {
        if (HitsRequired <= 0) return MiniGameRating.Miss;

        // Punkte: Perfect=3, Good=2, Ok=1, Miss=0
        int totalPoints = PerfectHits * 3 + GoodHits * 2 + OkHits * 1;
        int maxPoints = HitsRequired * 3;
        double ratio = (double)totalPoints / maxPoints;

        if (ratio >= 0.85) return MiniGameRating.Perfect;
        if (ratio >= 0.60) return MiniGameRating.Good;
        if (ratio >= 0.35) return MiniGameRating.Ok;
        return MiniGameRating.Miss;
    }

    [RelayCommand]
    private void Continue()
    {
        var order = _gameStateService.GetActiveOrder();
        if (order == null)
        {
            NavigationRequested?.Invoke("../..");
            return;
        }

        if (order.IsCompleted)
        {
            _gameStateService.CompleteActiveOrder();
            NavigationRequested?.Invoke("../..");
        }
        else
        {
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
        var state = _gameStateService.State;
        if (!state.SeenMiniGameTutorials.Contains(GameType))
        {
            state.SeenMiniGameTutorials.Add(GameType);
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

    // ===================================================================
    // HELPERS
    // ===================================================================

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

    // ===================================================================
    // DISPOSAL
    // ===================================================================

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
