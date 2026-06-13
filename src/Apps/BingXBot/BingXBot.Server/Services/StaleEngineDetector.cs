using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Trading;

namespace BingXBot.Server.Services;

/// <summary>
/// Watchdog-HostedService: Erkennt wenn der Bot laut State "Running" ist, aber der Scan-Loop
/// in Wahrheit stillschweigend stehengeblieben ist. Symptom (real beobachtet 09.-15.05.2026):
/// Service laeuft seit Tagen, isRunning=true, Heartbeat-DB-Writes laufen weiter, aber
/// <c>ScanAndTradeAsync</c> wird nicht mehr aufgerufen — Counter <c>strategy_evaluations</c>
/// eingefroren, keine Trade-Activity, kein Reconcile.
///
/// Aktivitaets-Tracking (priorisiert):
/// <list type="number">
/// <item><c>BotEventBus.ScanCycleCompleted</c> — Watchdog-Event, gefeuert pro RunLoopAsync-
///   Iteration (Success oder Failure). Zuverlaessigster Indikator: feuert auch bei Scans
///   die keine Kandidaten finden oder bei API-Errors. Stagniert NUR wenn der Loop selbst tot ist.</item>
/// <item><c>ScannerResult</c>/<c>TradeOpened</c> als Backup (alte Events).</item>
/// </list>
///
/// Eskalations-Pfad:
/// <list type="bullet">
/// <item>Erste Schwelle (Default 6 h): Warning-Log + FCM-Push (Anti-Spam 12 h zwischen Pushes).</item>
/// <item>Auto-Restart-Schwelle (Default 2 Push-Aktionen in Folge ohne Erholung): Engine wird via
///   <see cref="IBotControlService.StopAsync"/> + <see cref="IBotControlService.StartAsync"/>
///   automatisch neu gestartet. Opt-out via <see cref="BotSettings.EnableAutoRestartOnStale"/>.</item>
/// </list>
/// </summary>
public sealed class StaleEngineDetector : IHostedService, IDisposable
{
    private readonly IBotEventStream _stream;
    private readonly BotEventBus _bus;
    private readonly FcmDeviceStore _store;
    private readonly IBotControlService _botControl;
    private readonly BotSettings _botSettings;
    private readonly ILogger<StaleEngineDetector> _logger;
    // Optional: Cross-Sectional-Engine erkennen. Sie hat KEINEN Scan-Loop (monatlicher
    // Wall-Clock-Rebalance, 30-min-Ticks) — ohne diesen Guard meldete der Detector nach 6 h
    // "LastScanCycle=NIE" und der Auto-Restart ersetzte die laufende Xsec-Engine durch den
    // Scalper-Default (live passiert 10.06.2026, 15:32).
    private readonly BingXBot.Trading.CrossSectional.CrossSectionalManager? _xsecManager;

    private readonly TimeSpan _staleAfter = TimeSpan.FromHours(6);
    // Xsec-Tick laeuft alle 30 min — 90 min ohne Tick (3 verpasste Intervalle) = Loop tot.
    private readonly TimeSpan _xsecStaleAfter = TimeSpan.FromMinutes(90);
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _minTimeBetweenPushes = TimeSpan.FromHours(12);

    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private DateTime? _lastScanCycleUtc;
    private string? _lastScanCycleError;
    private BotState _currentState = BotState.Stopped;
    private DateTime _lastPushUtc = DateTime.MinValue;
    private int _consecutiveAlertsWithoutRecovery;

    private Timer? _timer;
    private readonly object _gate = new();

    public StaleEngineDetector(
        IBotEventStream stream,
        BotEventBus bus,
        FcmDeviceStore store,
        IBotControlService botControl,
        BotSettings botSettings,
        ILogger<StaleEngineDetector> logger,
        BingXBot.Trading.CrossSectional.CrossSectionalManager? xsecManager = null)
    {
        _stream = stream;
        _bus = bus;
        _store = store;
        _botControl = botControl;
        _botSettings = botSettings;
        _logger = logger;
        _xsecManager = xsecManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stream.BotStateChanged += OnBotStateChanged;
        _stream.ScannerResult += OnActivity;
        _stream.TradeOpened += OnActivity;
        // Primaerer Activity-Indikator: ScanCycleCompleted feuert pro RunLoopAsync-Iteration,
        // auch bei Scans ohne Kandidaten oder bei Exceptions. Stagniert NUR wenn der Scan-Loop
        // selbst stillschweigend tot ist — genau der Fall den dieser Detector erkennen muss.
        _bus.ScanCycleCompleted += OnScanCycleCompleted;

        _timer = new Timer(_ => CheckStale(), null, _checkInterval, _checkInterval);
        _logger.LogInformation("StaleEngineDetector gestartet (Schwelle {Hours} h, Check alle {Min} min, AutoRestart={AutoRestart})",
            _staleAfter.TotalHours, _checkInterval.TotalMinutes, _botSettings.EnableAutoRestartOnStale);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        _stream.BotStateChanged -= OnBotStateChanged;
        _stream.ScannerResult -= OnActivity;
        _stream.TradeOpened -= OnActivity;
        _bus.ScanCycleCompleted -= OnScanCycleCompleted;
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private void OnBotStateChanged(BotStateChangedDto dto)
    {
        lock (_gate)
        {
            _currentState = dto.State;
            if (dto.State == BotState.Running)
            {
                // Reset Stale-Timer bei Start/Resume — erst ab jetzt wieder counting.
                _lastActivityUtc = DateTime.UtcNow;
                _consecutiveAlertsWithoutRecovery = 0;
                _lastScanCycleUtc = null;
                _lastScanCycleError = null;
            }
        }
    }

    private void OnActivity(ScannerResultDto _) { lock (_gate) _lastActivityUtc = DateTime.UtcNow; }
    private void OnActivity(TradeDto _) { lock (_gate) _lastActivityUtc = DateTime.UtcNow; }

    private void OnScanCycleCompleted(object? sender, ScanCycleEventArgs args)
    {
        lock (_gate)
        {
            _lastScanCycleUtc = args.UtcTimestamp;
            _lastScanCycleError = args.Success ? null : args.ErrorMessage;
            // Auch ein erfolgloser Scan-Cycle ist "Activity" im Sinne von "Loop laeuft noch".
            // Wenn Exceptions wiederholt feuern, sieht der User das im LastScanError; aber die
            // Engine ist eindeutig nicht im "Silent Death"-Zustand.
            _lastActivityUtc = args.UtcTimestamp;
            _consecutiveAlertsWithoutRecovery = 0;
        }
    }

    private void CheckStale()
    {
        BotState state;
        TimeSpan idle;
        bool canPush;
        DateTime? lastCycle;
        string? lastError;
        int alertsBefore;

        lock (_gate)
        {
            state = _currentState;
            idle = DateTime.UtcNow - _lastActivityUtc;
            canPush = (DateTime.UtcNow - _lastPushUtc) >= _minTimeBetweenPushes;
            lastCycle = _lastScanCycleUtc;
            lastError = _lastScanCycleError;
            alertsBefore = _consecutiveAlertsWithoutRecovery;
        }

        if (state != BotState.Running) return;
        // Cross-Sectional hat per Design keine Scanner-Aktivitaet (Rebalance ~monatlich), aber
        // einen eigenen Liveness-Proxy: LastTickUtc (gesetzt pro Tick-Versuch, 30-min-Intervall).
        // Frueher wurde der Xsec-Modus hier KOMPLETT uebersprungen — ein toter Xsec-Tick-Loop
        // war damit fuer jeden Watchdog unsichtbar (live diagnostiziert 12.06.2026).
        if (_xsecManager?.IsRunning == true)
        {
            var lastTick = _xsecManager.LastTickUtc;
            // Vor dem ersten Tick zaehlt der Engine-Start (idle aus _lastActivityUtc) als Referenz.
            var tickIdle = lastTick.HasValue ? DateTime.UtcNow - lastTick.Value : idle;
            if (tickIdle < _xsecStaleAfter) return;
            if (!canPush) return;

            _logger.LogWarning(
                "[FCM-Stub] Stale-Xsec-Alert (#{Count}): Cross-Sectional-Tick-Loop seit {Hours:F1} h ohne Tick " +
                "(Intervall 30 min, Schwelle {Threshold:F1} h). LastTick={LastTick}. " +
                "Moegliche Ursachen: Tick-Loop-Hang (HTTP ohne Timeout), Task-Deadlock. {DeviceCount} Ziel-Geraete.",
                alertsBefore + 1, tickIdle.TotalHours, _xsecStaleAfter.TotalHours,
                lastTick?.ToString("O") ?? "NIE", _store.AllDevices.Count);

            EscalateStaleAlert();
            return;
        }
        if (idle < _staleAfter) return;
        if (!canPush) return;

        // Eskalations-Log: zeigt alles was die Diagnose braucht
        var cycleInfo = lastCycle.HasValue
            ? $"LastScanCycle={lastCycle.Value:O}"
            : "LastScanCycle=NIE";
        var errorInfo = string.IsNullOrEmpty(lastError) ? "" : $", LastScanError=\"{lastError}\"";

        _logger.LogWarning(
            "[FCM-Stub] Stale-Engine-Alert (#{Count}): Bot ist {Hours:F1} h ohne Scanner/Trade-Aktivitaet im Running-State. " +
            "Diagnose: {CycleInfo}{ErrorInfo}. " +
            "Moegliche Ursachen: Scanner-TF leer, alle Symbole blacklisted, BingX-API-Quota, News-Blackout festgeklemmt, Scan-Loop-Hang. " +
            "{DeviceCount} Ziel-Geraete.",
            alertsBefore + 1, idle.TotalHours, cycleInfo, errorInfo, _store.AllDevices.Count);

        EscalateStaleAlert();
    }

    /// <summary>
    /// Gemeinsame Eskalation fuer Scalper- und Xsec-Stale-Alert: Push-Timestamp + Alert-Counter
    /// hochzaehlen und ab Schwelle den Auto-Restart anstossen. Muss NACH dem Alert-Log aufgerufen
    /// werden (der Counter+1 ist dort bereits im Log-Text vorweggenommen).
    /// </summary>
    private void EscalateStaleAlert()
    {
        int alertsNow;
        lock (_gate)
        {
            _lastPushUtc = DateTime.UtcNow;
            _consecutiveAlertsWithoutRecovery++;
            alertsNow = _consecutiveAlertsWithoutRecovery;
        }

        // Auto-Recovery: nach N aufeinanderfolgenden Alerts ohne Activity wird die Engine hart
        // neu gestartet (Stop + Start). Setting kann das deaktivieren (z.B. fuer Debugging).
        var threshold = Math.Max(1, _botSettings.AutoRestartAfterStaleAlertCount);
        if (_botSettings.EnableAutoRestartOnStale && alertsNow >= threshold)
        {
            _ = TryAutoRestartAsync(alertsNow);
        }
    }

    private async Task TryAutoRestartAsync(int alertCount)
    {
        try
        {
            _logger.LogError(
                "Auto-Restart: {Alerts}× Stale-Alert in Folge ohne Recovery — Engine wird neu gestartet (Stop → Start). " +
                "Opt-out via BotSettings.EnableAutoRestartOnStale=false.",
                alertCount);

            var lastMode = _botSettings.LastMode;
            var activeTfs = new List<TimeFrame>();
            // Active-TFs werden nicht direkt aus den Settings hier gelesen (LocalBotControlService
            // hat den scannerSettings-Singleton). Wir uebergeben null → er nutzt die aktuellen
            // ScannerSettings.ActiveTimeframes.
            // BotStartRequest mit InitialBalance=null + leerem ActiveTimeframes-List signalisiert
            // "nimm die persistierten Werte". Siehe LocalBotControlService.StartAsync.

            await _botControl.StopAsync(CancellationToken.None).ConfigureAwait(false);

            // Kurze Pause, damit StopBase + Dispose abgeschlossen sind
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            // Engine-aware: ohne explizites Engine-Feld wuerde der Default (Scalper) eine
            // laufende Cross-Sectional-Engine ersetzen.
            var request = new BotStartRequest(lastMode, InitialBalance: null, ActiveTimeframes: activeTfs,
                Engine: _botSettings.LastEngineMode);
            var status = await _botControl.StartAsync(request, CancellationToken.None).ConfigureAwait(false);

            if (status.State == BotState.Running)
            {
                _logger.LogWarning("Auto-Restart erfolgreich. State=Running, Mode={Mode}.", status.Mode);
                lock (_gate)
                {
                    _consecutiveAlertsWithoutRecovery = 0;
                    _lastActivityUtc = DateTime.UtcNow;
                    _lastPushUtc = DateTime.MinValue; // Damit naechster Alert nicht 12 h blockiert ist
                }
            }
            else
            {
                _logger.LogError("Auto-Restart fehlgeschlagen. State={State}, LastError={Error}.",
                    status.State, status.LastError ?? "(kein Fehler)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-Restart wurde von Exception unterbrochen — manueller Eingriff noetig.");
        }
    }
}
