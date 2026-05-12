using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels;
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
public abstract partial class BaseMiniGameViewModel : ViewModelBase, INavigable, IDisposable
{
    // ═══════════════════════════════════════════════════════════════════════
    // SERVICES & FELDER
    // ═══════════════════════════════════════════════════════════════════════

    protected readonly IGameStateService _gameStateService;
    protected readonly IAudioService _audioService;
    protected readonly IRewardedAdService _rewardedAdService;
    protected readonly ILocalizationService _localizationService;
    /// <summary>AAA-Audit P1 Mini-Games-Telemetrie: optional injected via App.Services.</summary>
    protected IAnalyticsService? _analyticsService;
    protected DispatcherTimer? _timer;
    protected bool _disposed;
    protected bool _isEnding;

    /// <summary>
    /// v2.1.0: Aktiver Co-op-Auftrag (Order-ID auf Firebase). Wird vom Co-op-Flow gesetzt,
    /// bevor das MiniGame startet. Wenn != null, wird beim Spielende der Score an
    /// <see cref="IGuildCoopOrderService.SubmitScoreAsync"/> uebermittelt.
    /// </summary>
    public static string? ActiveCoopOrderId { get; set; }

    /// <summary>True wenn der aktuelle Spieler der Auftrags-Ersteller ist (Player1).</summary>
    public static bool ActiveCoopIsPlayer1 { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>Wird nach Spielende mit Rating (0-3 Sterne) gefeuert.</summary>
    public event EventHandler<int>? GameCompleted;

    /// <summary>
    /// Wird gefeuert wenn das Spiel neu initialisiert wird (Task-Wechsel bei Multi-Task-Orders).
    /// Views nutzen dies, um ihren Render-Loop neu zu starten (der bei IsResultShown=true gestoppt wurde).
    /// </summary>
    public event EventHandler? GameRestarted;

    // ═══════════════════════════════════════════════════════════════════════
    // GEMEINSAME OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _orderId = string.Empty;

    [ObservableProperty]
    private OrderDifficulty _difficulty = OrderDifficulty.Medium;

    /// <summary>
    /// Vom Spieler gewaehlte Strategie (v2.0.35) — Safe/Standard/Risk.
    /// Abgeleitete MiniGames nutzen <c>CurrentStrategy.GetToleranceMultiplier()</c>,
    /// <c>.GetSpeedMultiplier()</c>, <c>.GetTimeMultiplier()</c> in ihrer Initialisierung.
    /// Default Standard bis Order.Strategy im SetOrderId uebernommen wird.
    /// </summary>
    [ObservableProperty]
    private OrderStrategy _currentStrategy = OrderStrategy.Standard;

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

    /// <summary>
    /// Spiel-spezifischer Timer-Tick (async). Exceptions werden im Wrapper <see cref="HandleTimerTick"/>
    /// gefangen und loggen — kein ungeschuetztes async void mehr (Prozess-Crash-Schutz).
    /// </summary>
    protected abstract Task OnGameTimerTickAsync(object? sender, EventArgs e);

    /// <summary>
    /// Dispatcher-sicherer Wrapper um <see cref="OnGameTimerTickAsync"/>. Fängt ALLE Exceptions
    /// (Timer-Handler laufen als async void über den DispatcherTimer — eine unbehandelte Exception
    /// würde den Prozess zerreißen). Bei Fehler wird der Timer gestoppt, damit das Spiel nicht endlos feuert.
    /// </summary>
    private async void HandleTimerTick(object? sender, EventArgs e)
    {
        try
        {
            await OnGameTimerTickAsync(sender, e);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HandwerkerImperium] MiniGame Timer-Tick-Exception ({GetType().Name}): {ex}");
            try { _timer?.Stop(); } catch { /* Timer-Stop Fehler ignorieren */ }
        }
    }

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
    /// v2.1.0: Score-Mapping fuer Co-op-Auftraege. Aggregiert ueber alle Tasks der Order
    /// (Durchschnitt aller Ratings). Fallback auf das aktuelle Rating wenn keine Order existiert.
    /// </summary>
    protected int ComputeCoopScore(MiniGameRating finalRating)
    {
        var order = _gameStateService.GetActiveOrder();
        if (order == null || order.TaskResults.Count == 0)
            return RatingToScore(finalRating);

        // Aggregierter Score: Durchschnitt aller Task-Ratings (Multi-Task-Order).
        double sum = 0;
        foreach (var r in order.TaskResults) sum += RatingToScore(r);
        return (int)Math.Round(sum / order.TaskResults.Count);
    }

    private static int RatingToScore(MiniGameRating r) => r switch
    {
        MiniGameRating.Perfect => 100,
        MiniGameRating.Good => 75,
        MiniGameRating.Ok => 50,
        _ => 0
    };

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

    /// <summary>
    /// Optionaler Co-op-Service (v2.1.0). Wenn injiziert + <see cref="ActiveCoopOrderId"/>
    /// gesetzt, wird der MiniGame-Score am Spielende an Firebase uebermittelt.
    /// </summary>
    protected readonly IGuildCoopOrderService? _coopOrderService;

    protected BaseMiniGameViewModel(
        IGameStateService gameStateService,
        IAudioService audioService,
        IRewardedAdService rewardedAdService,
        ILocalizationService localizationService,
        IGuildCoopOrderService? coopOrderService = null)
    {
        _gameStateService = gameStateService;
        _audioService = audioService;
        _rewardedAdService = rewardedAdService;
        _localizationService = localizationService;
        _coopOrderService = coopOrderService;
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

        // v2.0.35 Bugfix: Sterne-Opacity zurueck auf 0 setzen, damit bei Spielbeginn
        // keine Rest-Sterne vom letzten Spiel sichtbar sind. Die View-Animation in
        // OnGameCompleted setzt sie dann am Ende des MiniGames sauber auf starCount.
        Star1Opacity = 0;
        Star2Opacity = 0;
        Star3Opacity = 0;

        var activeOrder = _gameStateService.GetActiveOrder();
        if (activeOrder != null)
        {
            Difficulty = activeOrder.Difficulty;
            CurrentStrategy = activeOrder.Strategy;

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

        // View benachrichtigen (Render-Loop neu starten bei Task-Wechsel).
        // MUSS vor StartGameAsync() gefeuert werden, damit die View ihren Timer hat,
        // bevor das Spiel-VM den Countdown startet.
        GameRestarted?.Invoke(this, EventArgs.Empty);

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
        int delay = _gameStateService.Statistics.TotalMiniGamesPlayed >= 50 ? 350 : 700;
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
        if (_timer != null) { _timer.Stop(); _timer.Tick -= HandleTimerTick; }
        _timer = new DispatcherTimer { Interval = TimerInterval };
        _timer.Tick += HandleTimerTick;
        _timer.Start();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SPIEL BEENDEN + ERGEBNIS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stoppt Spiel und Timer. Gibt false zurück wenn bereits beendet (Doppel-Aufruf-Schutz).
    /// </summary>
    public bool StopGame()
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

        // Risk-Strategie Hard-Fail: Miss = Auftrag komplett verloren + Reputation-Hit (v2.0.35).
        // Zusaetzlich: Alle restlichen Tasks werden uebersprungen (CurrentTaskIndex auf Ende
        // gesetzt) — der Spieler muss nicht weiterspielen, wenn ohnehin kein Reward mehr kommt.
        // IsLastTask wird damit true → GameCompleted-Event feuert → Ende-Flow.
        if (rating == MiniGameRating.Miss && CurrentStrategy.HasHardFail())
        {
            var failedOrder = _gameStateService.GetActiveOrder();
            if (failedOrder != null)
            {
                failedOrder.HasHardFailed = true;
                int penalty = CurrentStrategy.GetReputationPenaltyOnMiss();
                if (penalty < 0)
                {
                    // v2.1.0: Reputation-Insurance — Charge verhindert Reputation-Verlust.
                    var state = _gameStateService.State;
                    if (state.RepShopInsuranceCharges > 0)
                    {
                        state.RepShopInsuranceCharges--;
                        // FloatingText fuer Spieler-Feedback (kein Penalty).
                    }
                    else
                    {
                        state.Reputation.ReputationScore = Math.Max(0, state.Reputation.ReputationScore + penalty);
                    }
                }
                // Restliche Tasks ueberspringen — Continue geht direkt zum Ende.
                IsLastTask = true;
            }
        }

        // Ergebnis aufzeichnen — v2.0.36: Mit MiniGameType fuer Sliding-Window-Stats.
        _gameStateService.RecordMiniGameResult(rating, GetCurrentMiniGameType());

        // Perfect-Rating für Auto-Complete zählen
        if (rating == MiniGameRating.Perfect)
            _gameStateService.RecordPerfectRating(GetCurrentMiniGameType());

        // AAA-Audit P1 Mini-Games-Telemetrie: Welche Mini-Games werden gespielt?
        // Ergebnisse landen via Analytics-Pipeline in Firebase und ermoeglichen den
        // Bottom-50%-Audit (welche Mini-Games werden NICHT gespielt → killen oder polieren).
        // Lazy-Load des Analytics-Service ueber App.Services, damit kein Constructor-Aufwand
        // in jedem MiniGame-VM noetig ist. Event-Name aus zentralem AnalyticsEvents-Katalog.
        try
        {
            _analyticsService ??= App.Services?.GetService(typeof(IAnalyticsService)) as IAnalyticsService;
            var miniGameType = GetCurrentMiniGameType().ToString();
            _analyticsService?.TrackEvent(HandwerkerImperium.Models.AnalyticsEvents.MiniGamePlayed, new Dictionary<string, object?>
            {
                ["mini_game_type"] = miniGameType,
                ["rating"] = rating.ToString(),
                ["was_score_doubled"] = _gameStateService.GetActiveOrder()?.IsScoreDoubled ?? false,
            });

            // Perfect-Rating als separates Event (kuerzerer Funnel, einfacher zu auswerten)
            if (rating == MiniGameRating.Perfect)
            {
                _analyticsService?.TrackEvent(HandwerkerImperium.Models.AnalyticsEvents.MiniGamePerfect, new Dictionary<string, object?>
                {
                    ["mini_game_type"] = miniGameType,
                });
            }
        }
        catch { /* Analytics darf das Spiel nicht crashen */ }

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

            // v2.1.0: Co-op-Auftrag — Score uebermitteln wenn aktiv.
            // Score-Mapping: Perfect=100, Good=75, Ok=50, Miss=0. Aggregiert ueber alle Tasks
            // wenn Multi-Task-Order (Durchschnitt aller Ratings).
            if (_coopOrderService != null && !string.IsNullOrEmpty(ActiveCoopOrderId))
            {
                int coopScore = ComputeCoopScore(rating);
                var coopId = ActiveCoopOrderId;
                var isP1 = ActiveCoopIsPlayer1;
                _ = Task.Run(async () =>
                {
                    try { await _coopOrderService.SubmitScoreAsync(coopId, coopScore, isP1); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Coop] SubmitScore-Fehler: {ex.Message}"); }
                });
                // Co-op-Order-State zuruecksetzen — naechstes MiniGame ist normal.
                ActiveCoopOrderId = null;
            }

            // v2.0.35 Bugfix: Sterne werden NUR von der View via MiniGameEffectHelper.
            // ShowStarsStaggeredAsync animiert (Bounce-Effekt). Vorher gab es ZWEI parallele
            // Animationen (ViewModel-Property-Staggered + View-Control-Animation) die
            // gegeneinander kaempften und die "Sterne doppelt sehen"-Optik produzierten.
            // GameCompleted-Event triggert die einzige Animation.
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
        // Doppel-Tap-Schutz: Continue darf nur aus dem Result-Dialog heraus ausgeloest werden.
        // Bei schnellem Doppel-Tap (z.B. <100ms) waere die Navigation sonst re-entrant:
        // Der erste Tap ruft SetOrderId -> Countdown (Task.Delay) -> bevor der async-Countdown
        // durchlaeuft, kann der noch sichtbare Button (Avalonia-Binding-Frame) erneut geklickt
        // werden. Zweiter SetOrderId-Aufruf wuerde InitializeGame nochmal machen und der alte
        // Countdown liefe auf dem neuen Spielfeld weiter (inkorrekter State).
        if (!IsResultShown) return;

        var order = _gameStateService.GetActiveOrder();
        if (order == null)
        {
            // QuickJob-Flow: "../.." gibt die Kontrolle an NavigationService.HandleBackRoute
            // zurueck, der die QuickJob-Belohnung sauber auszahlt und zurueck zum Sender geht.
            NavigationRequested?.Invoke("../..");
            return;
        }

        if (order.IsCompleted)
        {
            _gameStateService.CompleteActiveOrder();
            // v2.0.35 Bugfix: Direkt zum Dashboard navigieren statt "../.." (Stack-Pop).
            // Der Stack-Pop wuerde den Spieler auf OrderDetail zuruecksetzen, wo der
            // fertige Auftrag nicht mehr existiert — Dead-End-UX. Direktsprung ist
            // der saubere Flow nach Auftragsabschluss.
            NavigationRequested?.Invoke("dashboard");
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
        if (!state.Tutorial.SeenMiniGameTutorials.Contains(gameType))
        {
            state.Tutorial.SeenMiniGameTutorials.Add(gameType);
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

        // QuickJob-Flow: kein Order — "../.." triggert NavigationService.HandleBackRoute
        // fuer sauberes QuickJob-Cleanup. Bei Orders direkt zum Dashboard (vermeidet
        // OrderDetail-Stack-Fallback auf gecancelltem Auftrag).
        var order = _gameStateService.GetActiveOrder();
        _gameStateService.CancelActiveOrder();

        if (order != null)
            NavigationRequested?.Invoke("dashboard");
        else
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
        // v2.0.36: Mit MiniGameType fuer Sliding-Window-Stats.
        var miniGameType = GetCurrentMiniGameType();
        while (!order.IsCompleted)
            _gameStateService.RecordMiniGameResult(MiniGameRating.Perfect, miniGameType);

        await _audioService.PlaySoundAsync(GameSound.Perfect);

        RewardAmount = order.FinalReward * _gameStateService.GetOrderRewardMultiplier(order);
        XpAmount = order.FinalXp;
        Result = MiniGameRating.Perfect;
        ResultText = _localizationService.GetString(Result.GetLocalizationKey());
        ResultEmoji = "★★★";
        IsLastTask = true;
        IsResultShown = true;

        // v2.0.35 Bugfix: Keine manuelle Star-Opacity-Setzung. Die View-Animation
        // (OnGameCompleted → ShowStarsStaggeredAsync) macht das sauber.
        // Perfect = 3 Sterne (verdiente Mastery-Belohnung)
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
        if (!state.Tutorial.SeenMiniGameTutorials.Contains(gameType))
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
        var activeOrder = _gameStateService.GetActiveOrder();
        bool hasActiveOrder = activeOrder != null;
        bool canAuto = hasActiveOrder && _gameStateService.CanAutoComplete(gameType, state.IsPremium);

        // v2.0.36: Bei AutoCompleteSkipLiveOrders wird Auto-Complete fuer Live-/VIP-Auftraege
        // ausgeblendet — Spieler soll den Risk/Reward-Run aktiv steuern.
        if (canAuto && state.Automation.AutoCompleteSkipLiveOrders
            && activeOrder != null && (activeOrder.IsLive || activeOrder.IsPremium))
        {
            canAuto = false;
        }

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
            _timer.Tick -= HandleTimerTick;

        OnDispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Hook für zusätzliches Cleanup in abgeleiteten Klassen.</summary>
    protected virtual void OnDispose() { }
}
