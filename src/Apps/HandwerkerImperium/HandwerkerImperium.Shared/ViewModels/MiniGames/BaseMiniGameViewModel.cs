using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;
using HandwerkerImperium.Helpers;

namespace HandwerkerImperium.ViewModels.MiniGames;

/// <summary>
/// Basis-Klasse für alle MiniGame-ViewModels.
/// Eliminiert ~2.500 Zeilen Duplikation über 10 Spiele hinweg.
/// Enthält: Auftrags-Init, Countdown, Timer, Ergebnis-Anzeige,
/// Tutorial, Auto-Complete, Werbe-Verdopplung, Navigation, Disposal.
/// </summary>
public abstract partial class BaseMiniGameViewModel : ViewModelBase, IDisposable
{
    // ═══════════════════════════════════════════════════════════════════════
    // SERVICES & FELDER
    // ═══════════════════════════════════════════════════════════════════════

    protected readonly IGameStateService _gameStateService;
    protected readonly IAudioService _audioService;
    protected readonly IRewardedAdService _rewardedAdService;
    protected readonly ILocalizationService _localizationService;
    protected DispatcherTimer? _timer;
    protected bool _disposed;
    protected bool _isEnding;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>Wird nach Spielende mit Rating (0-3 Sterne) gefeuert.</summary>
    public event EventHandler<int>? GameCompleted;

    // ═══════════════════════════════════════════════════════════════════════
    // GEMEINSAME OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    private OrderDifficulty _difficulty = OrderDifficulty.Medium;

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

    [ObservableProperty]
    private bool _isCountdownActive;

    [ObservableProperty]
    private string _countdownText = "";

    [ObservableProperty]
    private double _star1Opacity;

    [ObservableProperty]
    private double _star2Opacity;

    [ObservableProperty]
    private double _star3Opacity;

    [ObservableProperty]
    private bool _showTutorial;

    [ObservableProperty]
    private string _tutorialTitle = "";

    [ObservableProperty]
    private string _tutorialText = "";

    [ObservableProperty]
    private bool _canAutoComplete;

    [ObservableProperty]
    private string _autoCompleteHint = "";

    /// <summary>Bisheriger Durchschnitt bei Multi-Task-Orders (z.B. "Bisheriger Durchschnitt: ★★☆").</summary>
    [ObservableProperty]
    private string _intermediateAverage = "";

    /// <summary>Ob der Info-Button sichtbar ist (Tutorial kann nochmal gelesen werden).</summary>
    [ObservableProperty]
    private bool _canShowTutorialInfo;

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    public string RewardAmountDisplay => $"+{MoneyFormatter.FormatCompact(RewardAmount)}";

    /// <summary>Schwierigkeitsgrad als Sterne-Anzeige.</summary>
    public string DifficultyStars => Difficulty switch
    {
        OrderDifficulty.Easy => "★☆☆",
        OrderDifficulty.Medium => "★★☆",
        OrderDifficulty.Hard => "★★★",
        OrderDifficulty.Expert => "★★★★",
        _ => "★☆☆"
    };

    partial void OnDifficultyChanged(OrderDifficulty value) => OnPropertyChanged(nameof(DifficultyStars));
    partial void OnRewardAmountChanged(decimal value) => OnPropertyChanged(nameof(RewardAmountDisplay));

    // ═══════════════════════════════════════════════════════════════════════
    // ABSTRACT / VIRTUAL (von abgeleiteten Klassen implementiert)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Der MiniGame-Typ dieses Spiels.</summary>
    protected abstract MiniGameType GameMiniGameType { get; }

    /// <summary>Timer-Intervall (Standard: 1s, Forge/Sawing: 16ms für ~60 FPS).</summary>
    protected virtual TimeSpan TimerInterval => TimeSpan.FromSeconds(1);

    /// <summary>Ob vor dem Countdown ein Sound gespielt wird (Standard: ja).</summary>
    protected virtual bool PlaySoundBeforeCountdown => true;

    /// <summary>Spiel-spezifische Initialisierung (Zones, Grid, Wires etc.).</summary>
    protected abstract void InitializeGame();

    /// <summary>Timer-Tick-Handler (spiel-spezifisch).</summary>
    protected abstract void OnGameTimerTick(object? sender, EventArgs e);

    /// <summary>
    /// Hook zwischen Countdown und Timer-Start.
    /// Für Memorisierungsphase (Blueprint/Invent) oder GameStarted-Events (Sawing/Forge).
    /// </summary>
    protected virtual Task OnPreGameStartAsync() => Task.CompletedTask;

    /// <summary>
    /// Aktueller MiniGameType. SawingGame überschreibt dies (4 Sub-Typen).
    /// </summary>
    protected virtual MiniGameType GetCurrentMiniGameType() => GameMiniGameType;

    /// <summary>
    /// Belohnungen berechnen und auf RewardAmount/XpAmount setzen.
    /// PaintingGame überschreibt für Combo-Multiplikator.
    /// </summary>
    protected virtual void CalculateAndSetRewards()
    {
        var order = _gameStateService.GetActiveOrder();
        if (order != null && IsLastTask)
        {
            RewardAmount = order.FinalReward * _gameStateService.GetOrderRewardMultiplier(order);
            XpAmount = order.FinalXp;
        }
        else if (order == null)
        {
            // QuickJob: Belohnung aus aktivem QuickJob lesen
            var quickJob = _gameStateService.State.ActiveQuickJob;
            RewardAmount = quickJob?.Reward ?? 0;
            XpAmount = quickJob?.XpReward ?? 0;
        }
        // else: Zwischen-Runde, keine Belohnung anzeigen
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    protected BaseMiniGameViewModel(
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
    // INITIALISIERUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Initialisiert das Spiel mit einer Auftrags-ID.</summary>
    public virtual void SetOrderId(string orderId)
    {
        OrderId = orderId;
        IsPlaying = false;
        IsResultShown = false;
        IntermediateAverage = "";

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
            // QuickJob: Difficulty vom QuickJob-Model übernehmen, immer letzte (einzige) Aufgabe
            var quickJob = _gameStateService.State.ActiveQuickJob;
            if (quickJob != null)
                Difficulty = quickJob.Difficulty;

            TaskProgressDisplay = "";
            IsLastTask = true;
            ContinueButtonText = _localizationService.GetString("Continue");
        }

        InitializeGame();

        var gameType = GetCurrentMiniGameType();
        UpdateAutoCompleteStatus(gameType);
        CheckAndShowTutorial(gameType);

        if (!ShowTutorial && !CanAutoComplete)
            StartGameAsync().SafeFireAndForget();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIELSTART MIT COUNTDOWN
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    protected async Task StartGameAsync()
    {
        if (IsPlaying || IsCountdownActive) return;

        IsResultShown = false;
        _isEnding = false;

        if (PlaySoundBeforeCountdown)
            await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Countdown 3-2-1-Los! (verkürzt nach 50+ gespielten MiniGames)
        IsCountdownActive = true;
        int delay = _gameStateService.State.TotalMiniGamesPlayed >= 50 ? 350 : 700;
        foreach (var text in new[] { "3", "2", "1", _localizationService.GetString("CountdownGo") })
        {
            CountdownText = text;
            await Task.Delay(delay);
        }
        IsCountdownActive = false;

        // Hook für Vor-Start-Logik (Memorisierung, GameStarted-Event etc.)
        await OnPreGameStartAsync();

        // Timer starten
        IsPlaying = true;
        StartTimer();
    }

    /// <summary>Startet den Game-Timer mit dem konfigurierten Intervall.</summary>
    protected void StartTimer()
    {
        if (_timer != null) { _timer.Stop(); _timer.Tick -= OnGameTimerTick; }
        _timer = new DispatcherTimer { Interval = TimerInterval };
        _timer.Tick += OnGameTimerTick;
        _timer.Start();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL BEENDEN + ERGEBNIS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stoppt Spiel und Timer. Gibt false zurück wenn bereits beendet (Doppel-Aufruf-Schutz).
    /// </summary>
    protected bool StopGame()
    {
        if (_isEnding) return false;
        _isEnding = true;
        IsPlaying = false;
        _timer?.Stop();
        return true;
    }

    /// <summary>
    /// Zeigt das Spielergebnis an: Sound, Belohnungen, Sterne-Animation.
    /// Wird von abgeleiteten Klassen nach der Rating-Berechnung aufgerufen.
    /// </summary>
    protected async Task ShowResultAsync(MiniGameRating rating)
    {
        Result = rating;

        // Ergebnis aufzeichnen
        _gameStateService.RecordMiniGameResult(rating);

        // Perfect-Rating für Auto-Complete zählen
        if (rating == MiniGameRating.Perfect)
            _gameStateService.RecordPerfectRating(GetCurrentMiniGameType());

        // Sound abspielen
        var sound = rating switch
        {
            MiniGameRating.Perfect => GameSound.Perfect,
            MiniGameRating.Good => GameSound.Good,
            MiniGameRating.Ok => GameSound.ButtonTap,
            _ => GameSound.Miss
        };
        await _audioService.PlaySoundAsync(sound);

        // Belohnungen berechnen
        CalculateAndSetRewards();

        // Ergebnis-Anzeige
        ResultText = _localizationService.GetString(rating.GetLocalizationKey());
        ResultEmoji = rating switch
        {
            MiniGameRating.Perfect => "★★★",
            MiniGameRating.Good => "★★",
            MiniGameRating.Ok => "★",
            _ => "💨"
        };

        IsResultShown = true;

        // Sterne-Bewertung
        int starCount = rating switch
        {
            MiniGameRating.Perfect => 3,
            MiniGameRating.Good => 2,
            MiniGameRating.Ok => 1,
            _ => 0
        };

        if (IsLastTask)
        {
            // Aggregierte Sterne berechnen (alle Runden zusammen)
            var order = _gameStateService.GetActiveOrder();
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
            // Zwischen-Runde: Sterne sofort setzen
            Star1Opacity = starCount >= 1 ? 1.0 : 0.3;
            Star2Opacity = starCount >= 2 ? 1.0 : 0.3;
            Star3Opacity = starCount >= 3 ? 1.0 : 0.3;

            // Bisheriger Durchschnitt über alle abgeschlossenen Runden anzeigen
            var order = _gameStateService.GetActiveOrder();
            if (order != null && order.TaskResults.Count > 0)
            {
                double avgStars = order.TaskResults.Average(r => r switch
                {
                    MiniGameRating.Perfect => 3.0,
                    MiniGameRating.Good => 2.0,
                    MiniGameRating.Ok => 1.0,
                    _ => 0.0
                });
                int roundedAvg = (int)Math.Round(avgStars);
                string stars = roundedAvg >= 3 ? "★★★" : roundedAvg == 2 ? "★★☆" : roundedAvg == 1 ? "★☆☆" : "☆☆☆";
                var avgLabel = _localizationService.GetString("AverageRating") ?? "";
                IntermediateAverage = $"{avgLabel} {stars}";
            }
        }

        AdWatched = false;
        CanWatchAd = IsLastTask && _rewardedAdService.IsAvailable;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GEMEINSAME COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    protected async Task WatchAdAsync()
    {
        if (!CanWatchAd || AdWatched) return;

        var success = await _rewardedAdService.ShowAdAsync("score_double");
        if (success)
        {
            // Belohnungen verdoppeln (Anzeige + Flag für Auszahlung)
            RewardAmount *= 2;
            XpAmount *= 2;
            var order = _gameStateService.GetActiveOrder();
            if (order != null)
                order.IsScoreDoubled = true;
            else
            {
                // QuickJob: Verdopplung auf dem QuickJob-Model markieren
                var quickJob = _gameStateService.State.ActiveQuickJob;
                if (quickJob != null) quickJob.IsScoreDoubled = true;
            }
            AdWatched = true;
            CanWatchAd = false;

            await _audioService.PlaySoundAsync(GameSound.MoneyEarned);
        }
    }

    [RelayCommand]
    protected void Continue()
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
                NavigationRequested?.Invoke($"../{nextTask.GameType.GetRoute()}?orderId={order.Id}");
            else
                NavigationRequested?.Invoke("../..");
        }
    }

    [RelayCommand]
    protected void DismissTutorial()
    {
        ShowTutorial = false;
        CanShowTutorialInfo = true;
        var state = _gameStateService.State;
        var gameType = GetCurrentMiniGameType();
        if (!state.SeenMiniGameTutorials.Contains(gameType))
        {
            state.SeenMiniGameTutorials.Add(gameType);
            _gameStateService.MarkDirty();
        }
        // Nur Spiel starten wenn nicht bereits läuft (Info-Button während Spiel)
        if (!IsPlaying && !IsResultShown)
            StartGameAsync().SafeFireAndForget();
    }

    [RelayCommand]
    protected void Cancel()
    {
        _timer?.Stop();
        IsPlaying = false;

        _gameStateService.CancelActiveOrder();
        NavigationRequested?.Invoke("../..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AUTO-COMPLETE
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    protected async Task AutoCompleteGameAsync()
    {
        if (!CanAutoComplete) return;

        var order = _gameStateService.GetActiveOrder();
        if (order == null)
        {
            NavigationRequested?.Invoke("../..");
            return;
        }

        // Mastery-Belohnung: Alle verbleibenden Tasks mit Perfect abschließen
        // Der Spieler hat sich Auto-Complete durch 30/15 Perfect-Ratings verdient
        while (!order.IsCompleted)
            _gameStateService.RecordMiniGameResult(MiniGameRating.Perfect);

        await _audioService.PlaySoundAsync(GameSound.Perfect);

        RewardAmount = order.FinalReward * _gameStateService.GetOrderRewardMultiplier(order);
        XpAmount = order.FinalXp;
        Result = MiniGameRating.Perfect;
        ResultText = _localizationService.GetString(Result.GetLocalizationKey());
        ResultEmoji = "★★★";
        IsLastTask = true;
        IsResultShown = true;

        // Perfect = 3 Sterne (verdiente Mastery-Belohnung)
        Star1Opacity = 1.0;
        Star2Opacity = 1.0;
        Star3Opacity = 1.0;

        GameCompleted?.Invoke(this, 3);

        AdWatched = false;
        CanWatchAd = _rewardedAdService.IsAvailable;
        CanAutoComplete = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    protected void CheckAndShowTutorial(MiniGameType gameType)
    {
        // Tutorial-Texte immer laden (für Info-Button)
        TutorialTitle = _localizationService.GetString($"Tutorial{gameType}Title") ?? "";
        TutorialText = _localizationService.GetString($"Tutorial{gameType}Text") ?? "";

        var state = _gameStateService.State;
        if (!state.SeenMiniGameTutorials.Contains(gameType))
        {
            ShowTutorial = true;
            CanShowTutorialInfo = false;
        }
        else
        {
            // Tutorial bereits gesehen → Info-Button zeigen
            CanShowTutorialInfo = true;
        }
    }

    /// <summary>Tutorial nochmal anzeigen (Info-Button, kein Tracking-Reset).</summary>
    [RelayCommand]
    protected void ShowTutorialInfo()
    {
        ShowTutorial = true;
        CanShowTutorialInfo = false;
    }

    protected void UpdateAutoCompleteStatus(MiniGameType gameType)
    {
        var state = _gameStateService.State;
        // Auto-Complete nur bei echten Aufträgen, nicht bei QuickJobs (die haben kein ActiveOrder)
        bool hasActiveOrder = _gameStateService.GetActiveOrder() != null;
        bool canAuto = hasActiveOrder && _gameStateService.CanAutoComplete(gameType, state.IsPremium);
        CanAutoComplete = canAuto;
        if (canAuto)
        {
            int count = state.PerfectRatingCounts.TryGetValue((int)gameType, out int c) ? c : 0;
            var hint = _localizationService.GetString("AutoCompleteHint") ?? "";
            AutoCompleteHint = string.Format(hint, count);
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
            _timer.Tick -= OnGameTimerTick;

        OnDispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Hook für zusätzliches Cleanup in abgeleiteten Klassen.</summary>
    protected virtual void OnDispose() { }
}
