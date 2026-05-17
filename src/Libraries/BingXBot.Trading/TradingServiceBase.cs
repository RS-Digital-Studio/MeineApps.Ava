using BingXBot.Contracts.Dto;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Services;
using BingXBot.Engine;
using BingXBot.Engine.Filters;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using BingXBot.Trading.Local;
using System.Collections.Concurrent;

namespace BingXBot.Trading;

/// <summary>
/// Abstrakte Basisklasse für Paper- und Live-Trading-Services.
/// Enthält die gesamte gemeinsame Logik: Scan-Loop (30s), PriceTicker-Loop (5s),
/// SL/TP-Prüfung, SK-Buch-BE-Regel, Korrelations-Check, Risk-Management,
/// Margin-Monitoring, Desktop-Notifications.
/// Subklassen implementieren nur die exchange-spezifischen Operationen.
/// </summary>
public abstract class TradingServiceBase : IDisposable
{
    protected readonly IPublicMarketDataClient _publicClient;
    protected readonly StrategyManager _strategyManager;
    protected readonly RiskSettings _riskSettings;
    protected readonly ScannerSettings _scannerSettings;
    protected readonly BotEventBus _eventBus;

    protected RiskManager? _riskManager;
    /// <summary>RiskManager-Instanz (für Rolling-Metriken im UI).</summary>
    public RiskManager? RiskManager => _riskManager;
    protected CancellationTokenSource? _cts;
    protected volatile bool _isRunning;
    protected volatile bool _isPaused;
    protected bool _disposed;
    protected DateTime _lastDailyResetDate = DateTime.UtcNow.Date;

    // Funding-Rates pro Symbol (wird von Subklassen aktualisiert, z.B. aus BingX API)
    protected readonly ConcurrentDictionary<string, decimal> _fundingRates = new();
    // v1.5.4 Phase 7 — Letzter Fetch-Zeitpunkt pro Symbol fuer 30-s-TTL-Cache. Schuetzt vor
    // BingX-Rate-Limit-Spam, wenn `PreloadScanDataAsync` ueber viele Kandidaten iteriert oder
    // `OnBeforePriceTickerIteration` (alle 5 s) Funding pruefen will.
    protected readonly ConcurrentDictionary<string, DateTime> _fundingRatesFetchedAt = new();
    /// <summary>v1.5.4 Phase 7 — TTL fuer den Funding-Cache. 30 s reichen, Funding-Wert aendert sich alle 8 h.</summary>
    protected static readonly TimeSpan FundingRateCacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>v1.5.4 Phase 7 — Hilfs-Methode fuer Subklassen: True wenn der Cache fuer das Symbol noch frisch ist.</summary>
    protected bool IsFundingRateCacheFresh(string symbol)
    {
        if (!_fundingRatesFetchedAt.TryGetValue(symbol, out var ts)) return false;
        return DateTime.UtcNow - ts < FundingRateCacheTtl;
    }

    /// <summary>
    /// v1.6.6 Phase 17 — Static-Bridge fuer Adaptive-TF-Disable. Wird vom Server-Bootstrap
    /// auf <c>AdaptiveTfDisableService.IsTfDisabled</c> verdrahtet. Default: alle TFs enabled.
    /// Static, weil der DI-Graph zwischen BingXBot.Trading + BingXBot.Server-Services keinen
    /// Konstruktor-Injection-Pfad hat (Trading kennt Server-Side nicht).
    /// </summary>
    public static Func<TimeFrame, bool>? AdaptiveTfDisableProbe { get; set; }

    /// <summary>v1.6.6 Phase 17 — Pruefung im Scan-Loop: TF auto-disabled?</summary>
    protected bool IsTfAutoDisabled(TimeFrame tf)
    {
        var probe = AdaptiveTfDisableProbe;
        return probe != null && probe(tf);
    }
    // Margin-Monitoring: Bereits gewarnte Positionen (nicht bei jedem Tick erneut warnen)
    private readonly ConcurrentDictionary<string, DateTime> _marginWarningsIssued = new();

    // SL/TP-Tracking: Speichert das Original-Signal pro offener Position (Symbol_Side → SignalResult)
    // ConcurrentDictionary weil PriceTickerLoop und ScanAndTradeAsync parallel darauf zugreifen
    // protected internal: protected fuer Subklassen (Live/Paper), internal fuer BingXBot.Tests
    // (InternalsVisibleTo in BingXBot.Trading.csproj). Erlaubt Integration-Tests des Reconcile-Flows.
    protected internal readonly ConcurrentDictionary<string, SignalResult> _positionSignals = new();
    // SK-System: Letzter Status für Scan-Summary (wird auf Symbol-Klonen evaluiert, nicht auf dem Template)
    private string _lastSkStatus = "";
    // Multi-TF Standalone: Letzter nicht-blockierter SK-Status pro TF (für Ampel-UI)
    private readonly Dictionary<TimeFrame, string> _lastSkStatusByTf = new();
    // Wiederverwendbares Dictionary für Ticker-Preise (ConcurrentDictionary für Thread-Safety
    // da PriceTickerLoop und RunLoopAsync parallel laufen)
    private readonly ConcurrentDictionary<string, decimal> _tickerPriceMap = new();

    // Positions-Zustand (SL/TP-Tracking, BE-Status)
    protected readonly ConcurrentDictionary<string, PositionExitState> _exitStates = new();
    // Verlust-Tracking
    protected volatile int _consecutiveLosses;
    // Täglicher Trade-Counter (wird bei Tageswechsel zurückgesetzt)
    protected volatile int _tradesToday;

    // Semaphore fuer paralleles Klines-Laden.
    //
    // BingX-Rate-Limit: ~100 Requests pro 10s PRO IP (nicht pro Connection). Bei Reconnect
    // nach WebSocket-Drop laufen parallel: Ticker-Poll, Kline-Load, RecoverMissingTpOrdersAsync,
    // ReconcilePendingLimitOrdersAsync → mehrere Flows teilen sich das IP-Budget.
    //
    // 10 parallel ist der sichere Wert: Selbst mit allen Recovery-Flows gleichzeitig bleibt
    // der Burst-Request-Count unter 100/10s. Hoehere Semaphore-Werte riskieren IP-Ban (5min).
    // Scanner-Rotation + MaxScanSymbols limitieren die Gesamt-Calls pro Scan-Zyklus — nicht
    // die Semaphore.
    private readonly SemaphoreSlim _klineSemaphore = new(10);

    // Bot-Einstellungen (für Notifications etc.)
    protected readonly BotSettings _botSettings;

    /// <summary>
    /// Optionaler DB-Service: persistiert abgeschlossene Trades und Log-Entries.
    /// Live-Mode setzt das im Konstruktor; Paper-Mode laesst es null (oder setzt es per Property).
    /// Vorher lebte das Field nur in <c>LiveTradingService</c> — dadurch konnte
    /// <c>ProcessCompletedTrade</c> nicht persistieren, und der Live-Pfad hat NULL Trades in die
    /// DB geschrieben (Snapshot-Report Befund 1).
    /// </summary>
    protected internal BotDatabaseService? _dbService { get; set; }

    /// <summary>
    /// Snapshot-Report-Fix Befund 2 / A1.3 — Set bereits geloggter Triggered-Sequenz-Keys.
    /// Verhindert dass dieselbe Setup-Triggered-Decision in jedem Scan-Tick erneut persistiert
    /// wird (im Snapshot vom 2026-05-17 stand ZEC-USDT_M15 60× drin, obwohl die Order genau einmal
    /// platziert wurde). Set bleibt fuer die Bot-Laufzeit — nach Stop/Start ist es leer (neue
    /// Sequenz, neue Logs).
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _loggedTriggeredSequences = new();

    /// <summary>
    /// Helper fuer A1.3: baut den Dedup-Key aus Symbol + TF + SequenceState + Point0/PointA/PointB.
    /// Wenn Sequenz-Punkte fehlen (NULL), wird kein Key gebaut — dann wird nicht dedupliziert
    /// (Fail-Open: lieber doppelt loggen als gar nicht).
    /// </summary>
    private static string? BuildDecisionDedupKey(BingXBot.Core.Diagnostics.EvaluationDecision d)
    {
        if (string.IsNullOrEmpty(d.Symbol)) return null;
        if (d.Point0 is null && d.PointA is null && d.PointB is null) return null;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{d.Symbol}|{d.Tf}|{d.SequenceState}|{d.Point0}|{d.PointA}|{d.PointB}");
    }

    /// <summary>
    /// Optionaler Hook: wird nach erfolgreicher Trade-Persistenz aufgerufen.
    /// Server nutzt das, um den Equity-Snapshot in derselben Transaktionsgrenze zu schreiben.
    /// </summary>
    public Func<CompletedTrade, Task>? PostTradePersistHook { get; set; }

    // Optional: Multi-TF Standalone — Scanner-Cache für /api/v1/scanner/results
    protected ScannerResultsCache? _scannerCache;

    /// <summary>Setzt den Scanner-Cache nachträglich (Desktop/Server registrieren ihn per DI).</summary>
    public void SetScannerResultsCache(ScannerResultsCache? cache) => _scannerCache = cache;

    /// <summary>Multi-TF Standalone: Navigator-TF einer offenen Position aus ExitState (Fallback H4).</summary>
    protected TimeFrame GetNavigatorTimeframeForKey(string key)
        => _exitStates.TryGetValue(key, out var s) ? s.NavigatorTimeframe : TimeFrame.H4;

    /// <summary>Ob der Service gerade läuft.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Ob der Service pausiert ist.</summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Wenn true, werden BotState-Events in StopBase() unterdrückt.
    /// Wird vom Orchestrator gesetzt, damit nicht mehrfach BotState.Stopped gefeuert wird.
    /// </summary>
    public bool SuppressBotStateEvents { get; set; }

    /// <summary>Log-Präfix ("" für Paper, "LIVE: " für Live).</summary>
    protected abstract string LogPrefix { get; }

    /// <summary>Modus-Name für Log-Texte ("Paper-Trading" vs "Live-Trading").</summary>
    protected abstract string ModeName { get; }

    protected TradingServiceBase(
        IPublicMarketDataClient publicClient,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus,
        BotSettings botSettings)
    {
        _publicClient = publicClient;
        _strategyManager = strategyManager;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _eventBus = eventBus;
        _botSettings = botSettings;
    }

    // ═══════════════════════════════════════════════════════════════
    // Gemeinsame öffentliche Methoden
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Pausiert den Trading-Service (Loop läuft weiter, überspringt aber Scans).</summary>
    public void Pause()
    {
        if (!_isRunning || _isPaused) return;
        _isPaused = true;

        // Im Multi-Mode unterdrückt der Orchestrator individuelle BotState-Events (wie bei Stop)
        if (!SuppressBotStateEvents)
            _eventBus.PublishBotState(BotState.Paused);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
            $"{ModeName} pausiert"));
    }

    /// <summary>Setzt den Trading-Service nach Pause fort.</summary>
    public void Resume()
    {
        if (!_isRunning || !_isPaused) return;
        _isPaused = false;

        if (!SuppressBotStateEvents)
            _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
            $"{ModeName} fortgesetzt"));
    }

    /// <summary>Gibt das gespeicherte Signal (SL/TP) für eine offene Position zurück.</summary>
    public SignalResult? GetPositionSignal(string symbol, Side side)
    {
        _positionSignals.TryGetValue($"{symbol}_{side}", out var signal);
        return signal;
    }

    /// <summary>Gibt die Entry-Zeit einer Position zurück (aus ExitState). Null wenn nicht bekannt.</summary>
    public DateTime? GetEntryTime(string symbol, Side side)
    {
        _exitStates.TryGetValue($"{symbol}_{side}", out var state);
        return state?.EntryTime;
    }

    /// <summary>Stellt ein Signal für eine offene Position wieder her (z.B. nach App-Neustart aus BingX-Orders).</summary>
    public void RestorePositionSignal(string symbol, Side side, SignalResult signal)
    {
        var key = $"{symbol}_{side}";

        // Wenn ExitState bereits aus DB geladen wurde, SK-kritische Felder vom Original-Signal übernehmen.
        // BingX liefert nur SL/TP aus Conditional-Orders (STOP_MARKET/TAKE_PROFIT_MARKET) — SK-System nutzt
        // aber LIMIT Reduce-Only für TPs, die nicht als TP-Typ erkannt werden. Ohne Fallback auf das
        // Original-Signal gehen TakeProfit, TakeProfit2, DisableSmartBreakeven (SK-BE Workflow 4.2 "A-Bruch")
        // und IsAdditionalEntry verloren.
        if (_exitStates.TryGetValue(key, out var existingState) && existingState.Signal != null)
        {
            signal = signal with
            {
                TakeProfit = signal.TakeProfit ?? existingState.Signal.TakeProfit,
                TakeProfit2 = signal.TakeProfit2 ?? existingState.Signal.TakeProfit2,
                DisableSmartBreakeven = existingState.Signal.DisableSmartBreakeven,
                IsAdditionalEntry = existingState.Signal.IsAdditionalEntry
            };
        }

        _positionSignals[key] = signal;
        var entry = signal.EntryPrice ?? 0m;
        if (!_exitStates.ContainsKey(key))
        {
            // BreakevenSet nur true wenn der SL bereits auf/über Entry liegt (= BE war schon gesetzt)
            var slAlreadyAtBe = signal.StopLoss.HasValue && entry > 0 && (
                (side == Side.Buy && signal.StopLoss.Value >= entry) ||
                (side == Side.Sell && signal.StopLoss.Value <= entry));

            _exitStates[key] = new PositionExitState
            {
                Signal = signal, Symbol = symbol, Side = side,
                EntryPrice = entry,
                BreakevenSet = slAlreadyAtBe,
                IsRecovered = true
            };
        }
        else
        {
            // ExitState existiert: Signal-Referenz aktualisieren damit neue SL/TP-Werte greifen
            existingState!.Signal = signal;
        }

        // NF15 Fix — Recovery-Signale muessen auch im _signalCreatedAt/_positionOpenTimes-Tracking
        // landen, sonst funktioniert der Reconcile-Grace-Window-Check (Orphan/MissingStop) nicht
        // und kann in den ersten 30-90 s nach Restart faelschlich triggern.
        OnSignalCreated(key);
    }

    /// <summary>
    /// Entfernt das gespeicherte Signal für eine Position (z.B. bei manuellem Close über Dashboard).
    /// Verhindert, dass PriceTickerLoop eine bereits geschlossene Position erneut zu schließen versucht.
    /// </summary>
    public void RemovePositionSignal(string symbol, Side side) =>
        RemoveSignalByKey($"{symbol}_{side}");

    /// <summary>Entfernt Signal und ExitState und ruft OnSignalRemoved auf.</summary>
    protected void RemoveSignalByKey(string key)
    {
        _positionSignals.TryRemove(key, out _);
        _exitStates.TryRemove(key, out _);
        OnSignalRemoved(key);
    }

    /// <summary>
    /// Registriert ein SL/TP-Signal für eine bestehende Position (z.B. nach App-Neustart).
    /// Erstellt einen neuen Eintrag in _positionSignals wenn keiner existiert.
    /// </summary>
    public void RegisterPositionSignal(string symbol, Side side, SignalResult signal, decimal currentPrice)
    {
        var key = $"{symbol}_{side}";
        _positionSignals[key] = signal;
        OnSignalCreated(key);
    }

    /// <summary>Aktualisiert SL/TP für eine offene Position (z.B. wenn der User im Dashboard editiert).</summary>
    public void UpdatePositionSignal(string symbol, Side side, decimal? newSl, decimal? newTp)
    {
        var key = $"{symbol}_{side}";
        if (_positionSignals.TryGetValue(key, out var existing))
        {
            _positionSignals[key] = existing with { StopLoss = newSl, TakeProfit = newTp };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Gemeinsame Start-Infrastruktur
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Initialisiert RiskManager, CancellationToken und startet die Loops.</summary>
    protected void StartBase(RiskManager riskManager)
    {
        _riskManager = riskManager;
        // Phase 18 / H2 — RiskManager-Health-Hook auf den BotEventBus verdrahten.
        // Edge-Transitions (degraded/recovered) erreichen darüber LocalBotEventStream → SignalR → UI.
        _riskManager.NewsServiceHealthChanged = (isDeg, count, reason) =>
            _eventBus.PublishNewsServiceHealthChanged(isDeg, count, reason);
        // K-5 Fix: Cancel VOR Dispose — sonst laufen alte Loops weiter (ObjectDisposedException
        // wird nicht von catch(OperationCanceledException) gefangen)
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _isRunning = true;
        _isPaused = false;
        _lastDailyResetDate = DateTime.UtcNow.Date;

        // Scan-Loops starten (MarketCap-Cache wird im ersten Scan geladen)
        _ = RunLoopAsync(_cts.Token);
        _ = PriceTickerLoopAsync(_cts.Token);
        _ = HeartbeatLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Phase 18 / B2 — Heartbeat-Loop persistiert alle 30 s den letzten "Bot-aktiv"-Zeitstempel
    /// in der DB. Wird vom <c>BotAutoResumeService</c> beim Resume gelesen, um Trade-Replay
    /// (BingX-User-Trades since=lastHeartbeat) anzustossen oder Missing-TP zu erkennen.
    /// Bewusst eigener Loop: PriceTickerLoopAsync ist 5-s-Tick (zu haeufige DB-Writes), und
    /// RunLoopAsync ist 60-s-Tick (zu lange Luecke wenn der Pi crasht).
    /// </summary>
    public Func<DateTime, Task>? HeartbeatPersist { get; set; }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(30);
        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                if (HeartbeatPersist != null)
                    await HeartbeatPersist(DateTime.UtcNow).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Heartbeat",
                    $"{LogPrefix}Heartbeat-Persist fehlgeschlagen: {ex.Message}"));
            }
            try { await Task.Delay(interval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Gemeinsames Stop-Cleanup: CTS canceln, Signale leeren, State zurücksetzen.</summary>
    // SK-VERIFY: [6.1] ExitState-Persistenz: Subklassen können ExitStates VOR dem Clear speichern
    /// <summary>Gibt alle aktuellen ExitStates als Dictionary zurück (für DB-Persistenz).</summary>
    public Dictionary<string, PositionExitState> GetExitStatesSnapshot()
        => new(_exitStates);

    /// <summary>Gibt Runtime-State zurück (TradesToday, ConsecutiveLosses).</summary>
    public (int TradesToday, int ConsecutiveLosses) GetRuntimeStateSnapshot()
        => (_tradesToday, _consecutiveLosses);

    /// <summary>Stellt ExitStates aus DB-Persistenz wieder her.</summary>
    public void RestoreExitStates(Dictionary<string, PositionExitState> states)
    {
        foreach (var kvp in states)
            _exitStates.TryAdd(kvp.Key, kvp.Value);
    }

    /// <summary>Stellt Runtime-State aus DB-Persistenz wieder her.</summary>
    public void RestoreRuntimeState(int tradesToday, int consecutiveLosses)
    {
        Interlocked.Exchange(ref _tradesToday, tradesToday);
        Interlocked.Exchange(ref _consecutiveLosses, consecutiveLosses);
    }

    protected void StopBase(BotState endState, string logMessage)
    {
        _isRunning = false;
        _isPaused = false;

        _positionSignals.Clear();
        _exitStates.Clear();
        _marginWarningsIssued.Clear();
        Interlocked.Exchange(ref _tradesToday, 0);
        OnSignalsClearedAll();

        // N-4 Fix: Cancel vor Dispose damit laufende Tasks sauber beendet werden
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        // Im Multi-Mode unterdrückt der Orchestrator individuelle BotState-Events
        if (!SuppressBotStateEvents)
            _eventBus.PublishBotState(endState);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine", logMessage));
    }

    // ═══════════════════════════════════════════════════════════════
    // Hauptschleife: Alle 30 Sekunden scannen und handeln
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Watchdog-Diagnostik: Per-Iteration-Hard-Cap. Wenn eine ScanAndTradeAsync-Iteration laenger
    /// als dieser Wert braucht, wird sie via LinkedTokenSource hart gecancelled. Verhindert
    /// "silent death"-Hangs in HTTP-Calls oder Task.WhenAll-Subkomponenten. 4 min ist deutlich
    /// ueber dem typischen Scan-Zyklus (~10-30 s mit 100 Kandidaten), aber unter dem 6h-Stale-
    /// Detection-Threshold — bei echten BingX-Outages greift OnScanErrorAsync danach normal.
    /// </summary>
    protected static readonly TimeSpan ScanIterationTimeout = TimeSpan.FromMinutes(4);

    /// <summary>Watchdog-Diagnostik: UTC-Zeitpunkt des letzten erfolgreich abgeschlossenen Scan-Zyklus.</summary>
    public DateTime LastSuccessfulScanUtc { get; private set; } = DateTime.MinValue;

    /// <summary>Watchdog-Diagnostik: UTC-Zeitpunkt der letzten Exception in der Scan-Loop.</summary>
    public DateTime LastScanErrorUtc { get; private set; } = DateTime.MinValue;

    /// <summary>Watchdog-Diagnostik: Nachricht der letzten Scan-Loop-Exception (null wenn nie gefailt).</summary>
    public string? LastScanError { get; private set; }

    /// <summary>Watchdog-Diagnostik: True wenn aktuell eine Scan-Iteration laeuft.</summary>
    public bool ScanIterationInProgress { get; private set; }

    /// <summary>Watchdog-Diagnostik: UTC-Zeitpunkt zu dem die aktuelle Scan-Iteration gestartet wurde.</summary>
    public DateTime CurrentScanStartedUtc { get; private set; } = DateTime.MinValue;

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var iterationStart = DateTime.UtcNow;
            CurrentScanStartedUtc = iterationStart;
            ScanIterationInProgress = true;
            // Per-Iteration-Hard-Cap: verhindert "silent death" bei haengenden Sub-Awaits
            // (z.B. HTTP-Call der weder respondiert noch ct respektiert). Nach 4 min wird die
            // gesamte ScanAndTradeAsync hart gecancelled → catch → naechster Iteration-Versuch.
            using var iterationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            iterationCts.CancelAfter(ScanIterationTimeout);
            var iterationCt = iterationCts.Token;

            try
            {
                // Tageswechsel: Daily-Drawdown + Consecutive-Losses zurücksetzen
                var today = DateTime.UtcNow.Date;
                if (today != _lastDailyResetDate)
                {
                    _riskManager?.ResetDailyStats();
                    _lastDailyResetDate = today;
                    Interlocked.Exchange(ref _tradesToday, 0);
                    Interlocked.Exchange(ref _consecutiveLosses, 0);
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                        $"{LogPrefix}Tages-Drawdown + Trade-Counter + Verlustserie zurückgesetzt (neuer Tag)"));
                }

                // Bei Pause: Loop läuft weiter, überspringt aber den Scan
                if (!_isPaused)
                    await ScanAndTradeAsync(iterationCt).ConfigureAwait(false);

                var elapsed = (DateTime.UtcNow - iterationStart).TotalSeconds;
                LastSuccessfulScanUtc = DateTime.UtcNow;
                _eventBus.PublishScanCycle(success: true, durationSeconds: elapsed, errorMessage: null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Echter Shutdown — Loop beenden.
                ScanIterationInProgress = false;
                break;
            }
            catch (OperationCanceledException)
            {
                // Per-Iteration-Timeout ausgeloest (iterationCts hat ct nicht). 4 min hat nicht gereicht.
                var elapsed = (DateTime.UtcNow - iterationStart).TotalSeconds;
                LastScanError = $"Scan-Iteration Timeout nach {elapsed:F0}s — Hard-Cancel ausgeloest";
                LastScanErrorUtc = DateTime.UtcNow;
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                    $"{LogPrefix}{LastScanError}. Naechster Versuch im normalen Tick."));
                _eventBus.PublishScanCycle(success: false, durationSeconds: elapsed, errorMessage: LastScanError);
                try { await OnScanErrorAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { ScanIterationInProgress = false; break; }
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.UtcNow - iterationStart).TotalSeconds;
                LastScanError = ex.Message;
                LastScanErrorUtc = DateTime.UtcNow;
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                    $"Fehler in der {ModeName}-Loop: {ex.Message}"));
                _eventBus.PublishScanCycle(success: false, durationSeconds: elapsed, errorMessage: ex.Message);

                // Subklasse kann zusätzliche Wartezeit definieren (z.B. 60s bei API-Fehler)
                try { await OnScanErrorAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { ScanIterationInProgress = false; break; }
            }
            finally
            {
                ScanIterationInProgress = false;
            }

            // 30 Sekunden warten bis zum nächsten Scan
            // Scan-Intervall dynamisch basierend auf Timeframe (H4=15min, H1=5min, etc.)
            try { await Task.Delay(_scannerSettings.ScanIntervalSeconds * 1000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PriceTicker-Loop: Alle 5 Sekunden SL/TP prüfen + BE-Regel (Workflow 4.2 "A-Bruch").
    // ═══════════════════════════════════════════════════════════════

    private async Task PriceTickerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(5_000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            if (_isPaused) continue;

            try
            {
                // Offene Positionen holen
                var positions = await GetPositionsForTickerAsync().ConfigureAwait(false);

                // Subklasse kann hier z.B. verwaiste Signale bereinigen (Positionen werden übergeben)
                await OnBeforePriceTickerIteration(positions).ConfigureAwait(false);
                if (positions.Count == 0) continue;

                // Aktuelle Ticker holen (ein API-Call für alle Symbole)
                var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
                if (tickers.Count == 0) continue;

                // Ticker-Preise aktualisieren (ConcurrentDictionary, Thread-safe)
                foreach (var t in tickers)
                    _tickerPriceMap[t.Symbol] = t.LastPrice;

                // v1.3.0 K1: BTC-USDT-Ticker als separates Event fuer Dashboard-Ticker.
                // Einmal pro PriceTickerLoop-Iteration (alle 5 s) — ueber Hub-Throttle ist das
                // ausreichend glatt ohne Client zu spammen.
                var btc = tickers.FirstOrDefault(t => t.Symbol == "BTC-USDT");
                if (btc != null) _eventBus.PublishBtcPrice(btc);

                foreach (var pos in positions)
                {
                    if (!_tickerPriceMap.TryGetValue(pos.Symbol, out var price)) continue;

                    // Preis auf Exchange setzen (nur für Paper relevant)
                    SetCurrentPriceIfNeeded(pos.Symbol, price);

                    // BUCH-ONLY: Kein Liquidationspreis-Abstands-Monitoring. Risiko-Gate erfolgt
                    // ausschliesslich ueber Risk-Per-Trade + Positionsgroesse beim Entry.

                    var key = $"{pos.Symbol}_{pos.Side}";

                    // v1.3.0 K1 Remote-Events: Pro Position + pro Ticker-Loop-Iteration (alle 5 s)
                    // publishen. Der BotHubEventForwarder drosselt Ticker auf 1/s/Symbol — hier kein
                    // eigenes Throttling noetig. SL/TP/BE kommen aus dem aktuellen Signal/ExitState.
                    _eventBus.PublishTicker(new Ticker(
                        Symbol: pos.Symbol,
                        LastPrice: price,
                        BidPrice: 0m,
                        AskPrice: 0m,
                        Volume24h: 0m,
                        PriceChangePercent24h: 0m,
                        Timestamp: DateTime.UtcNow));
                    _positionSignals.TryGetValue(key, out var sigForSnapshot);
                    _exitStates.TryGetValue(key, out var exitStateForSnapshot);
                    _eventBus.PublishPositionUpdated(new PositionSnapshotArgs(
                        Position: pos,
                        StopLoss: sigForSnapshot?.StopLoss,
                        TakeProfit: sigForSnapshot?.TakeProfit ?? sigForSnapshot?.TakeProfit2,
                        LiquidationPrice: null,
                        IsSmartBreakevenArmed: exitStateForSnapshot?.BreakevenSet ?? false,
                        StrategyName: _botSettings.LastStrategyName));

                    // TradFi bei geschlossenem Markt: Nur Margin-Monitoring, kein SL/TP-Trigger
                    // (Preise sind stale, SL/TP-Trigger auf letztem Kurs wäre falsch)
                    if (!TradingHoursFilter.IsMarketOpen(pos.Symbol, DateTime.UtcNow))
                        continue;

                    if (!_positionSignals.TryGetValue(key, out var signal)) continue;

                    var hit = false;
                    var isStopLoss = false;
                    string reason = "";

                    // ═══ SK-BE-Regel (Cheat 53, Workflow 4.2, S.18) + User-Ausnahme 2x-SL (24.04.2026) ═══
                    // Zwei Trigger OR-verknuepft (siehe BreakevenCalculator):
                    //   1) A-Bruch (Buch Masterclass): Preis erreicht NavPointA → SL = Entry ± 0,5 %.
                    //      Buch-Zitat: "Sobald der Preis [...] das Level A signifikant durchbricht,
                    //      ziehst du den Stop-Loss auf Break Even."
                    //   2) 2x SL-Distanz (User-Ausnahme, nicht Buch): Preis hat doppelten SL-Abstand
                    //      in Profit-Richtung erreicht → SL = Entry ± 0,2 %. Greift wenn A-Bruch
                    //      (noch) nicht oder gar nicht kommt (z.B. NavPointA=0 bei Legacy-Signalen).
                    // Einmal pro Position (BreakevenSet-Flag), Buch 4.3: KEIN weiteres Nachziehen.
                    if (signal.DisableSmartBreakeven && signal.StopLoss.HasValue
                        && _exitStates.TryGetValue(key, out var skState) && skState.EntryPrice > 0
                        && !skState.BreakevenSet)
                    {
                        var decision = BreakevenCalculator.Evaluate(
                            pos.Side, price, skState.EntryPrice,
                            signal.StopLoss.Value, skState.NavPointA);

                        if (decision.HasValue)
                        {
                            // Snapshot-Report-Fix Befund 3 / A0.6 — SL-Sanity-Pruefung bevor wir BE pushen.
                            // BreakevenSet ist hier (noch) false — der Validator akzeptiert nur Werte bis
                            // Entry × (1 + MaxBreakevenBufferPercent). Verhindert Long-SL kilometerweit ueber Entry.
                            var sanity = StopLossSanityGuard.Validate(
                                pos.Side, skState.EntryPrice, decision.Value.NewStopLoss,
                                breakevenSet: true, partialClosed: skState.PartialClosed, runnerActive: skState.RunnerActive);
                            if (!sanity.IsAcceptable)
                            {
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: BE-Push abgelehnt — {sanity.RejectReason}. NewSL={decision.Value.NewStopLoss:F8}, Entry={skState.EntryPrice:F8}",
                                    pos.Symbol));
                            }
                            else
                            {
                                _positionSignals[key] = signal with { StopLoss = decision.Value.NewStopLoss };
                                skState.BreakevenSet = true;
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: SK Breakeven ({decision.Value.NewStopLoss:F8}) — {decision.Value.TriggerName}",
                                    pos.Symbol));
                                await OnStopLossAdjustedAsync(pos.Symbol, pos.Side, decision.Value.NewStopLoss).ConfigureAwait(false);
                                await PersistExitStatesAsync().ConfigureAwait(false);
                            }
                        }
                    }

                    // ═══ Multi-Stage Exit: TP1 (50%) bei 161.8%, TP2 (Rest) bei 200%+Buffer ═══
                    // Buch S.16 Zielbereich 161.8-200% — Partial Close 50/50 entspricht diesem Range
                    if (_exitStates.TryGetValue(key, out var exitState))
                    {
                        // v1.4.0 Phase 0.2 (Finding 0.2) — Skip Bot-TP1-Hit-Check wenn TP1 als
                        // Reduce-Only-LIMIT auf BingX liegt. Sonst Doppel-Close-Race: BingX fuellt
                        // den Limit, gleichzeitig sendet der Bot ClosePartialAsync mit pos.Quantity*0.5
                        // → bei Limit-Partial-Fill ist pos.Quantity bereits reduziert → falsche Mengen.
                        // Paper-Mode: Tp1LimitOrderId bleibt null → Bot-Pfad bleibt aktiv.
                        // Phase Initial: TP1 Partial Close (50% bei 161.8% Extension)
                        if (exitState.Phase == ExitPhase.Initial && signal.TakeProfit.HasValue && !exitState.PartialClosed
                            && _riskSettings.Tp1CloseRatio > 0 && _riskSettings.Tp1CloseRatio < 1m
                            && string.IsNullOrEmpty(exitState.Tp1LimitOrderId))
                        {
                            var tp1Hit = pos.Side == Side.Buy
                                ? price >= signal.TakeProfit.Value
                                : price <= signal.TakeProfit.Value;

                            if (tp1Hit)
                            {
                                if (!exitState.Tp2.HasValue)
                                {
                                    reason = $"Take-Profit bei {signal.TakeProfit.Value:F8} (TP2 nicht definiert, Full-Close)";
                                    hit = true;
                                }
                                else
                                {
                                    // SK-Buch S.16: Partial Close 50% bei 161.8%, Rest zu 200%+Buffer.
                                    // Buch Workflow 4.3: SL wird NICHT nachgezogen nach TP1.
                                    var closeQty = pos.Quantity * _riskSettings.Tp1CloseRatio;
                                    await OnPartialCloseAsync(pos, price, closeQty).ConfigureAwait(false);
                                    exitState.PartialClosed = true;

                                    _positionSignals[key] = signal with { TakeProfit = exitState.Tp2 };
                                    exitState.Signal = _positionSignals[key];
                                    exitState.Phase = ExitPhase.Tp1Hit;

                                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                        $"{LogPrefix}{pos.Symbol}: TP1 (161.8%) erreicht → {_riskSettings.Tp1CloseRatio:P0} geschlossen, Rest läuft bis 200%+Buffer",
                                        pos.Symbol));
                                    await PersistExitStatesAsync().ConfigureAwait(false);
                                    continue;
                                }
                            }
                        }

                        // BUCH-ONLY: Kein Time-Exit. Das Buch managed Exits ausschliesslich ueber SL/TP/BE.
                    }

                    // ═══ Buch Workflow 6.1+6.2: Verlust-Ausgleichs-TP (Task 2.5) ═══
                    // "Wenn x Trades in SL und Möglichkeit besteht mit einem Trade die Verluste auszugleichen → TP!"
                    // Task 2.5: Nur nach TP1-Hit aktivieren — Trend muss bestätigt sein.
                    // Ohne diesen Gate würde ein frisch geöffneter Trade bei zufällig erreichter
                    // Tagesverlust-Schwelle sofort geschlossen, bevor die eigentliche Bewegung läuft.
                    if (!hit && _riskManager != null && pos.EntryPrice > 0
                        && _exitStates.TryGetValue(key, out var xsForRecovery)
                        && xsForRecovery.Phase == ExitPhase.Tp1Hit)
                    {
                        var dailyLoss = _riskManager.DailyPnl < 0 ? Math.Abs(_riskManager.DailyPnl) : 0m;
                        if (dailyLoss > 0)
                        {
                            // Unrealized PnL (grob, ohne Fees — reicht für Entscheidung)
                            var unrealizedPnl = pos.Side == Side.Buy
                                ? (price - pos.EntryPrice) * pos.Quantity
                                : (pos.EntryPrice - price) * pos.Quantity;

                            if (unrealizedPnl >= dailyLoss)
                            {
                                reason = $"Verlust-Ausgleich aktiv (post-TP1): Gewinn {unrealizedPnl:F2}$ ≥ Tagesverluste {dailyLoss:F2}$";
                                hit = true;
                                isStopLoss = false;
                            }
                        }
                    }

                    // ═══ Task 4.7 — Runner-TP mit Trailing-ATR ═══
                    // Wenn RunnerActive: trail SL mit (bestPrice - ATR × Multiplier),
                    // Exit bei Trail-Hit oder RunnerHardCap (423.6%) erreicht.
                    if (!hit && _exitStates.TryGetValue(key, out var runnerState) && runnerState.RunnerActive)
                    {
                        // Anchor aktualisieren (bestPrice seit Runner-Aktivierung)
                        if (pos.Side == Side.Buy)
                        {
                            if (price > runnerState.RunnerTrailAnchor) runnerState.RunnerTrailAnchor = price;
                        }
                        else
                        {
                            if (runnerState.RunnerTrailAnchor <= 0 || price < runnerState.RunnerTrailAnchor)
                                runnerState.RunnerTrailAnchor = price;
                        }

                        // Trailing-Distance: ATR-basiert (Fallback 1% vom Preis wenn kein ATR)
                        var trailMul = _riskSettings.RunnerTrailingAtrMultiplier;
                        var trailDistance = runnerState.RunnerAtrBase > 0
                            ? runnerState.RunnerAtrBase * trailMul
                            : price * 0.01m * trailMul;
                        var trailSl = pos.Side == Side.Buy
                            ? runnerState.RunnerTrailAnchor - trailDistance
                            : runnerState.RunnerTrailAnchor + trailDistance;

                        // v1.2.7 Fix — Trail-SL an die Exchange pushen, damit App-Crash den nachgezogenen
                        // SL nicht verliert. Ohne diesen Push würde der Runner-Gewinn nur im Memory leben;
                        // bei Crash wäre der BingX-SL noch der initiale (ungünstige) SL.
                        // Throttle: nur pushen wenn (erster Push nach Runner-Aktivierung) ODER
                        // (Preis-Delta ≥ 0.15% UND letzter Push ≥ 10s her) — schont API-Rate-Limit.
                        var needsInitialPush = runnerState.RunnerLastPushedSl == 0m;
                        var pushThreshold = price * 0.0015m; // 0.15% = Fee-Floor-Konsistenz
                        var slDelta = Math.Abs(trailSl - runnerState.RunnerLastPushedSl);
                        var timeSinceLastPush = DateTime.UtcNow - runnerState.RunnerLastPushUtc;
                        if (needsInitialPush
                            || (slDelta >= pushThreshold && timeSinceLastPush >= TimeSpan.FromSeconds(10)))
                        {
                            // Snapshot-Report-Fix Befund 3 / A0.6 — Runner-Trail-SL durch den Sanity-Guard.
                            // Runner darf SL in Gewinn-Richtung trailen, aber NIE in die falsche Richtung
                            // (z.B. Long-SL ueber Entry waehrend Runner gerade negativ scrolled).
                            var trailSanity = StopLossSanityGuard.Validate(
                                pos.Side, runnerState.EntryPrice, trailSl,
                                breakevenSet: runnerState.BreakevenSet, partialClosed: runnerState.PartialClosed,
                                runnerActive: true);
                            if (!trailSanity.IsAcceptable)
                            {
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: Runner-Trail-Push abgelehnt — {trailSanity.RejectReason}. TrailSL={trailSl:F8}",
                                    pos.Symbol));
                                continue;
                            }
                            try
                            {
                                await OnStopLossAdjustedAsync(pos.Symbol, pos.Side, trailSl).ConfigureAwait(false);
                                runnerState.RunnerLastPushedSl = trailSl;
                                runnerState.RunnerLastPushUtc = DateTime.UtcNow;
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: Runner-Trail-SL auf {trailSl:F8} gepusht (Anchor {runnerState.RunnerTrailAnchor:F8})",
                                    pos.Symbol));
                            }
                            catch (Exception pushEx)
                            {
                                // Push-Fehler nicht eskalieren — nächster Tick versucht es erneut.
                                // trailSl bleibt im Memory, Trail-Exit-Check feuert trotzdem korrekt.
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: Runner-SL-Push fehlgeschlagen ({pushEx.Message}) — bleibt bei {runnerState.RunnerLastPushedSl:F8}",
                                    pos.Symbol));
                            }
                        }

                        var trailHit = pos.Side == Side.Buy ? price <= trailSl : price >= trailSl;
                        var capHit = runnerState.RunnerHardCap > 0 &&
                            (pos.Side == Side.Buy
                                ? price >= runnerState.RunnerHardCap
                                : price <= runnerState.RunnerHardCap);

                        if (trailHit || capHit)
                        {
                            reason = capHit
                                ? $"Runner-Exit: Hard-Cap (423.6%) @ {price:F8}"
                                : $"Runner-Exit: Trailing-SL bei {trailSl:F8} (Anchor {runnerState.RunnerTrailAnchor:F8}, ATR×{trailMul})";
                            hit = true;
                            isStopLoss = false;
                        }
                    }

                    // ═══ Standard SL/TP-Check (auch für Multi-Stage Phase) ═══
                    // v1.4.0 Phase 0.2/0.3 (Findings 0.2/0.3): TP-Branch wird geskippt wenn das
                    // aktive TP als Reduce-Only-LIMIT auf BingX liegt (Phase=Initial → Tp1LimitOrderId,
                    // Phase=Tp1Hit → Tp2LimitOrderId). BingX fuellt den Limit + Order-Filled-Event vom
                    // User-Data-Stream triggert Phase-Transition. Bot-TP-Hit-Check wuerde Mark-Price
                    // ausloesen BEVOR der LIMIT auf Last-Price fuellt → Doppel-Close. SL-Branch bleibt
                    // aktiv (BingX nativer SL ist Backup, Bot-Detection ist Safety-Net).
                    var tpManagedByExchange = _exitStates.TryGetValue(key, out var esTp02)
                        && esTp02.IsTpManagedByExchange;

                    if (!hit)
                    {
                        if (pos.Side == Side.Buy)
                        {
                            if (signal.StopLoss.HasValue && price <= signal.StopLoss.Value)
                            { hit = true; isStopLoss = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:F8}"; }
                            else if (!tpManagedByExchange && signal.TakeProfit.HasValue && price >= signal.TakeProfit.Value)
                            { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:F8}"; }
                        }
                        else
                        {
                            if (signal.StopLoss.HasValue && price >= signal.StopLoss.Value)
                            { hit = true; isStopLoss = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:F8}"; }
                            else if (!tpManagedByExchange && signal.TakeProfit.HasValue && price <= signal.TakeProfit.Value)
                            { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:F8}"; }
                        }

                        // Task 4.7 — Wenn TP2 getroffen wurde UND EnableRunner: Partial-Close statt Full-Close
                        if (hit && !isStopLoss
                            && _exitStates.TryGetValue(key, out var tp2State)
                            && tp2State.Phase == ExitPhase.Tp1Hit
                            && !tp2State.RunnerActive
                            && _riskSettings.EnableRunner
                            && _riskSettings.RunnerPercent > 0m && _riskSettings.RunnerPercent < 1m
                            && tp2State.OriginalQuantity > 0)
                        {
                            // TP2 ist erreicht: Schließe (1-RunnerPercent) der OriginalQuantity, Rest läuft als Runner
                            var runnerKeep = tp2State.OriginalQuantity * _riskSettings.RunnerPercent;
                            var tp2CloseQty = Math.Max(0m, pos.Quantity - runnerKeep);
                            if (tp2CloseQty > 0m && runnerKeep > 0m)
                            {
                                await OnPartialCloseAsync(pos, price, tp2CloseQty).ConfigureAwait(false);
                                tp2State.RunnerActive = true;
                                tp2State.RunnerTrailAnchor = price;
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: TP2 (200%) erreicht → Runner aktiv ({_riskSettings.RunnerPercent:P0} weiterlaufen, Trail ATR×{_riskSettings.RunnerTrailingAtrMultiplier})",
                                    pos.Symbol));
                                await PersistExitStatesAsync().ConfigureAwait(false);
                                // Standard-TP-Hit überspringen, Runner übernimmt
                                hit = false;
                                reason = "";
                            }
                        }
                    }

                    // Buch Workflow 4.3: SL wird NICHT nachgezogen (außer auf BE).
                    // Kein Nachziehen des SL. Trade läuft auf BE oder ins Ziel.

                    if (hit)
                    {
                        await OnSlTpHitAsync(pos, price, key, reason, isStopLoss).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "PriceTicker",
                    $"{LogPrefix}PriceTicker Fehler: {ex.Message}"));
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Scan-Zyklus: Ticker laden, filtern, Strategie evaluieren, handeln
    // ═══════════════════════════════════════════════════════════════

    private async Task ScanAndTradeAsync(CancellationToken ct)
    {
        if (_riskManager == null) return;
        if (_strategyManager.CurrentTemplate == null)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                "Keine Strategie ausgewählt"));
            return;
        }

        var scanStart = DateTime.UtcNow;
        var nextScanTime = scanStart.AddSeconds(_scannerSettings.ScanIntervalSeconds).ToLocalTime();
        var nextScanText = $"Nächster Scan: {nextScanTime:HH:mm:ss}";

        // 0. Market-Cap-Cache aktualisieren (CoinGecko, max 1x pro Stunde)
        // MUSS vor dem ersten Scan geladen sein, sonst kommen Meme-Coins durch.
        // 04.05.2026: HTTP-Logik ist jetzt im Engine-Layer (CoinGeckoMarketCapProvider) —
        // hier nur der Static-Bridge MarketCapRefreshHelper.RefreshIfNeededAsync.
        if (!Core.Helpers.MarketCapCache.IsLoaded)
        {
            try
            {
                await Engine.Helpers.MarketCapRefreshHelper.RefreshIfNeededAsync(ct).ConfigureAwait(false);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Market",
                    Core.Helpers.MarketCapCache.IsLoaded
                        ? $"MarketCap-Cache geladen: {Core.Helpers.MarketCapCache.CachedCount} Coins von CoinGecko"
                        : "MarketCap-Cache: CoinGecko gab leere Antwort — Volume-Fallback aktiv"));
            }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Market",
                    $"CoinGecko nicht erreichbar: {ex.Message} — Volume-Fallback aktiv (Meme-Coins möglich!)"));
            }
        }
        else
        {
            // Cache ist geladen — nur stündlich refreshen (still, kein Log)
            try { await Engine.Helpers.MarketCapRefreshHelper.RefreshIfNeededAsync(ct).ConfigureAwait(false); }
            catch { /* Stündlicher Refresh optional */ }
        }

        // 1. Alle Ticker holen und filtern
        var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
        if (tickers.Count == 0)
        {
            PublishScanSummary("Keine Ticker verfügbar", nextScanText);
            return;
        }

        // Multi-TF Standalone: Aktive TFs bestimmen + Kandidaten PRO TF filtern (per-TF-Volumen/Change).
        // Krypto nutzt MinVolume24hByTf, TradFi separat MinVolume24hTradFiByTf (niedriger).
        var activeTfsEarly = (_scannerSettings.ActiveTimeframes?.Count > 0
            ? _scannerSettings.ActiveTimeframes
            : new List<TimeFrame> { TimeFrame.D1, TimeFrame.H4, TimeFrame.H1, TimeFrame.M15 })
            .Distinct()
            .OrderByDescending(tf => TimeFrameHelper.ToDuration(tf))
            .ToList();

        var candidatesByTf = new Dictionary<TimeFrame, List<Ticker>>();
        foreach (var tf in activeTfsEarly)
            candidatesByTf[tf] = ScanHelper.FilterCandidatesForTimeframe(tickers, _scannerSettings, tf);

        // Superset: Union aller TF-Kandidaten (Kerzen nur einmal pro Symbol fetchen)
        var candidates = candidatesByTf.Values
            .SelectMany(x => x)
            .GroupBy(t => t.Symbol)
            .Select(g => g.First())
            .ToList();

        if (candidates.Count == 0)
        {
            PublishScanSummary("Keine Kandidaten in einer aktiven TF (Volumen-/Momentum-Filter)", nextScanText);
            return;
        }

        if (_eventBus.HasLogSubscribers)
        {
            var tradFiCount = candidates.Count(t => SymbolClassifier.IsTradFi(t.Symbol));
            var cryptoCount = candidates.Count - tradFiCount;
            var perTfCounts = string.Join(" | ",
                candidatesByTf.Select(kv => $"{kv.Key}={kv.Value.Count}"));
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Scanner",
                $"{LogPrefix}{candidates.Count} Symbole total ({cryptoCount}Cr + {tradFiCount}TradFi) | per-TF: {perTfCounts} | Hedge={_scannerSettings.IsHedgeModeActive}"));
        }

        // v1.3.0 K1: Scanner-Kandidaten als Remote-Event publishen — pro Navigator-TF eine
        // Sweep-Nachricht mit Top-N-Tickern. Score/SuggestedSide sind hier noch 0/null, weil
        // die SK-Evaluation erst weiter unten (pro Symbol+TF) laeuft. Client zeigt damit die
        // Vor-Filter-Liste im Dashboard an — reicht fuer "sieht-was-gerade-passiert"-UX.
        foreach (var kvp in candidatesByTf)
        {
            if (kvp.Value.Count == 0) continue;
            var sweepCandidates = kvp.Value
                .Select(t => new ScannerCandidate(
                    Symbol: t.Symbol,
                    Price: t.LastPrice,
                    Volume24h: t.Volume24h,
                    PriceChangePercent: t.PriceChangePercent24h,
                    Score: 0,
                    SuggestedSide: null,
                    Reason: null))
                .ToList();
            _eventBus.PublishScannerSweep(new ScannerSweepArgs(kvp.Key, sweepCandidates));
        }

        // 2. Globale MarketFilter prüfen (VOR dem teuren Klines-Loading)
        // Funding-Settlement blockiert ALLE BingX-Perpetuals (Krypto + TradFi haben Funding).
        var sessionFilter = MarketFilter.CheckSession(DateTime.UtcNow);
        if (!sessionFilter.IsAllowed)
        {
            PublishScanSummary($"Session-Filter: {sessionFilter.SessionInfo}", nextScanText);
            IndicatorHelper.ClearCache();
            return;
        }

        // BUCH-ONLY: Keine Max-Trades/Tag-Obergrenze. Das Buch kennt kein Trade-Limit pro Tag.

        // TradFi-Kandidaten mit geschlossenem Markt VOR dem teuren Klines-Loading entfernen.
        // Spart ~5 API-Calls pro geschlossenem TradFi-Symbol (H4+H1+M30+D1+W1).
        var nowPreFilter = DateTime.UtcNow;
        var openCandidates = candidates.Where(t => TradingHoursFilter.IsMarketOpen(t.Symbol, nowPreFilter)).ToList();
        if (openCandidates.Count < candidates.Count && _eventBus.HasLogSubscribers)
        {
            var skipped = candidates.Count - openCandidates.Count;
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Scanner",
                $"{LogPrefix}{skipped} TradFi-Kandidaten übersprungen (Markt geschlossen)"));
        }
        candidates = openCandidates;

        // 3. Account + Positionen holen
        var account = await GetAccountAsync().ConfigureAwait(false);
        var positions = await GetPositionsForScanAsync().ConfigureAwait(false);

        // NF8 Fix — RiskManager._openRiskEstimate pro Scan-Tick aus den offenen Positionen
        // aktualisieren. Vor diesem Fix war _openRiskEstimate immer 0, sodass MaxDailyRiskPercent
        // offene Positionen ignoriert hat (nur realisierte Verluste + geplanter neuer Trade-Risk).
        // Wir summieren |Entry - SL| * Qty pro Position; ohne bekannten SL faellt das Symbol weg
        // (besser Unterschaetzung als Ueberschaetzung — Recovery-Pfade setzen SL beim ersten Tick).
        if (_riskManager != null && positions.Count > 0)
        {
            decimal openRiskUsd = 0m;
            for (var i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                var key = $"{p.Symbol}_{p.Side}";
                if (!_positionSignals.TryGetValue(key, out var sig)) continue;
                if (!sig.StopLoss.HasValue || sig.StopLoss.Value <= 0) continue;
                var slDist = Math.Abs(p.EntryPrice - sig.StopLoss.Value);
                openRiskUsd += slDist * p.Quantity;
            }
            _riskManager.SetOpenRiskEstimate(openRiskUsd);
        }
        else
        {
            _riskManager?.SetOpenRiskEstimate(0m);
        }

        // 4. Multi-TF Standalone: Klines pro Navigator-TF × Symbol laden.
        //    W1 + D1 einmal pro Symbol (shared über alle Navigator-TF-Evaluierungen).
        //    Pro Navigator-TF wird zusätzlich die Filter-TF (H4/H1/M15/M5) geladen.
        var now = DateTime.UtcNow;
        var activeTfs = activeTfsEarly;

        // Lookup-Set pro TF für schnelles "ist Symbol in dieser TF-Kandidatenliste?"
        var candidateSetByTf = candidatesByTf.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(t => t.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase));

        // Shared D1/W1 pro Symbol
        var dailyResults = new ConcurrentDictionary<string, List<Candle>?>();
        var weeklyResults = new ConcurrentDictionary<string, List<Candle>?>();
        // Nav + Filter pro (Symbol, TF)
        var navResults = new ConcurrentDictionary<(string Symbol, TimeFrame Nav), List<Candle>>();
        var filterResults = new ConcurrentDictionary<(string Symbol, TimeFrame Nav), List<Candle>?>();

        var fetchTasks = candidates.SelectMany(ticker =>
        {
            var tasks = new List<Task>();
            // W1/D1 shared
            tasks.Add(FetchCandlesAsync(ticker.Symbol, TimeFrame.W1, 730, ct)
                .ContinueWith(t => weeklyResults[ticker.Symbol] = t.IsCompletedSuccessfully ? t.Result : null,
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));
            tasks.Add(FetchCandlesAsync(ticker.Symbol, TimeFrame.D1, 365, ct)
                .ContinueWith(t => dailyResults[ticker.Symbol] = t.IsCompletedSuccessfully ? t.Result : null,
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));

            foreach (var tf in activeTfs)
            {
                // Nav-Kerzen TF-abhängige Tiefe (siehe Plan)
                var daysBack = GetNavigatorLookbackDays(tf);
                tasks.Add(FetchCandlesAsync(ticker.Symbol, tf, daysBack, ct)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully && t.Result != null)
                            navResults[(ticker.Symbol, tf)] = t.Result;
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));

                var filterTf = Engine.Strategies.SequenzKonzeptStrategy.GetFilterTimeframe(tf);
                if (filterTf.HasValue && filterTf.Value != TimeFrame.D1 && filterTf.Value != TimeFrame.W1)
                {
                    var fDays = GetFilterLookbackDays(filterTf.Value);
                    tasks.Add(FetchCandlesAsync(ticker.Symbol, filterTf.Value, fDays, ct)
                        .ContinueWith(t => filterResults[(ticker.Symbol, tf)] =
                            t.IsCompletedSuccessfully ? t.Result : null,
                            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));
                }
            }
            return tasks;
        });

        await Task.WhenAll(fetchTasks).ConfigureAwait(false);

        // 4b. BTC-Health einmalig pro Scan berechnen (v1.2.5) — wird als PositionScale-Faktor
        // fuer Crypto-Trades genutzt + harter Block bei AllowLong/AllowShort=false.
        // Vor v1.2.5 nur fuer Dokumentation da, aber nie im Sizing angewendet.
        BtcHealthResult? btcHealth = null;
        var btcSymbol = tickers.FirstOrDefault(t => t.Symbol.StartsWith("BTC", StringComparison.OrdinalIgnoreCase))?.Symbol;
        if (btcSymbol != null)
        {
            dailyResults.TryGetValue(btcSymbol, out var btcD1);
            var btcH4Available = navResults.TryGetValue((btcSymbol, TimeFrame.H4), out var btcH4)
                                  ? btcH4 : null;
            var btcFundingForHealth = _fundingRates.GetValueOrDefault(btcSymbol, 0m);
            if (btcD1 is { Count: > 55 } && btcH4Available is { Count: > 20 })
            {
                btcHealth = MarketFilter.CalculateBtcHealth(btcD1, btcH4Available, btcFundingForHealth);
                if (_eventBus.HasLogSubscribers)
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Market",
                        $"{LogPrefix}BTC-Health: Score={btcHealth.Score}, Scale={btcHealth.PositionScale:F2}, L/S={btcHealth.AllowLong}/{btcHealth.AllowShort} | {btcHealth.Reasons}"));
            }
        }

        // 4c. Scan-Prefetch-Hook (v1.2.5): Subklassen koennen hier zusaetzliche Daten laden,
        // die pro Scan einmalig sind (z.B. Funding-Rates fuer Kandidaten ohne Cache). Damit
        // sehen neue Signale im SkConfluenceScorer/MarketFilter korrekte Funding-Werte statt 0.
        try { await PreloadScanDataAsync(candidates, ct).ConfigureAwait(false); }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Scanner",
                $"{LogPrefix}PreloadScanData fehlgeschlagen: {ex.Message}"));
        }

        // 5. Evaluierung: pro Symbol × aktive TF — sequenziell (Order-Platzierung muss sequenziell sein)
        var orderPlaced = false;

        // Phase 18 / B1 — News-Blackout 1× pro Scan-Tick auflösen (statt N× sync-over-async im Hot-Path).
        // Bei Service-Fehlern liefert ResolveActiveNewsBlackoutAsync null → graceful pass, B4-Counter zaehlt mit.
        string? resolvedNewsBlackout = null;
        if (_riskManager != null)
        {
            try { resolvedNewsBlackout = await _riskManager.ResolveActiveNewsBlackoutAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { /* ResolveActiveNewsBlackoutAsync schluckt + loggt schon, hier nur Belt-and-Suspenders */ }
        }

        // Multi-TF Standalone: Scanner-Result pro TF sammeln für /api/v1/scanner/results
        var scanSymbolsByTf = activeTfs.ToDictionary(
            tf => tf,
            _ => new List<ScannerSymbolDto>());

        foreach (var ticker in candidates)
        {
            if (ct.IsCancellationRequested) break;

            if (!TradingHoursFilter.IsMarketOpen(ticker.Symbol, DateTime.UtcNow))
                continue;

            var category = SymbolClassifier.Classify(ticker.Symbol);
            weeklyResults.TryGetValue(ticker.Symbol, out var weeklyCandles);
            dailyResults.TryGetValue(ticker.Symbol, out var dailyCandles);

            foreach (var navTf in activeTfs)
            {
                if (ct.IsCancellationRequested) break;

                // v1.6.6 Phase 17 — Adaptive TF-Disable: TF wegen schlechter WinRate auto-disabled?
                if (IsTfAutoDisabled(navTf)) continue;

                // Per-TF-Kandidaten-Filter: Symbol muss in dieser TF die Volume/PriceChange-Schwellen passieren.
                if (!candidateSetByTf.TryGetValue(navTf, out var tfSet) || !tfSet.Contains(ticker.Symbol))
                    continue;

                // Phase 18 / A7 — Time-of-Day Session-Filter (Crypto). Default = All → no-op.
                // TradFi-Symbole haben ihre eigene Marktoeffnung-Pruefung (siehe TradingHoursFilter.IsMarketOpen).
                if (!Engine.Filters.TradingHoursFilter.IsSessionAllowed(DateTime.UtcNow, _botSettings.EnabledSessions))
                {
                    if (_botSettings.EnableDecisionTrail)
                    {
                        var sessionDecision = new BingXBot.Core.Diagnostics.EvaluationDecision(
                            ticker.Symbol, navTf, DateTime.UtcNow, "SessionFilter",
                            null, null, null, false,
                            BingXBot.Core.Diagnostics.RejectionReasons.OutsideAllowedSession,
                            0, Array.Empty<string>(), Array.Empty<string>());
                        _eventBus.PublishEvaluationDecision(sessionDecision);
                    }
                    continue;
                }

                if (!navResults.TryGetValue((ticker.Symbol, navTf), out var navCandles) || navCandles.Count < 50)
                    continue;

                // D1 ist zugleich Fahrplan — wenn navTf=D1 nehmen wir die gleichen Kerzen als Fahrplan-Daily.
                // (DetermineFahrplanBias nutzt separate D1-Candles, aber hier ist es Nav.)
                var dailyForContext = navTf == TimeFrame.D1 ? navCandles : dailyCandles;
                filterResults.TryGetValue((ticker.Symbol, navTf), out var filterCandles);

                try
                {
                    SetCurrentPriceIfNeeded(ticker.Symbol, ticker.LastPrice);

                    var strategy = _strategyManager.GetOrCreateForSymbol(ticker.Symbol, navTf);

                    var fundingForStrategy = _fundingRates.GetValueOrDefault(ticker.Symbol, 0m);

                    var context = new MarketContext(
                        ticker.Symbol, navCandles, ticker, positions, account,
                        FilterTimeframeCandles: filterCandles,
                        Category: category,
                        DailyCandles: dailyForContext,
                        WeeklyCandles: weeklyCandles,
                        NavigatorTimeframe: navTf,
                        ScannerSettings: _scannerSettings,
                        RiskSettings: _riskSettings,
                        FundingRatePercent: fundingForStrategy,
                        ResolvedNewsBlackoutEvent: resolvedNewsBlackout);
                    // Phase 18 / H6 — Tracing + Counter pro Strategy-Evaluation.
                    Telemetry.BotTelemetry.StrategyEvaluations.Add(1,
                        new System.Collections.Generic.KeyValuePair<string, object?>("symbol", ticker.Symbol),
                        new System.Collections.Generic.KeyValuePair<string, object?>("tf", navTf.ToString()));
                    SignalResult signal;
                    using (var act = Telemetry.BotTelemetry.StartActivity("Strategy.Evaluate"))
                    {
                        act?.SetTag("symbol", ticker.Symbol);
                        act?.SetTag("tf", navTf.ToString());
                        signal = strategy.Evaluate(context);
                        act?.SetTag("signal", signal.Signal.ToString());
                    }

                    if (strategy is Engine.Strategies.SequenzKonzeptStrategy skInst
                        && !string.IsNullOrEmpty(skInst.LastStatus) && !skInst.LastStatus.Contains("—"))
                    {
                        _lastSkStatus = $"{ticker.Symbol} [{navTf}]: {skInst.LastStatus}";
                        _lastSkStatusByTf[navTf] = $"{ticker.Symbol}: {skInst.LastStatus}";
                    }

                    // v1.5.2 Phase 4 — Decision-Trail-Eintrag publishen.
                    // Nur wenn EnableDecisionTrail=true UND mindestens ein Subscriber existiert
                    // (typisch DecisionTrailBuffer). Hot-Path-Schutz: bei null kein Allocation-Overhead.
                    //
                    // Snapshot-Report-Fix Befund 2 / A1.1: state_not_activated-Eintraege standardmaessig
                    // unterdruecken (Default 81 % Rauschen). Robert kann via DecisionTrailIncludeNotActivated
                    // wieder einschalten wenn er die State-Machine selbst tunt.
                    //
                    // Snapshot-Report-Fix Befund 2 / A1.3: Idempotenz-Check fuer Trigger-Decisions —
                    // dieselbe Sequenz nur einmal pro Bot-Laufzeit loggen (verhindert ZEC-60×-Spam).
                    if (_botSettings.EnableDecisionTrail
                        && strategy is Engine.Strategies.SequenzKonzeptStrategy decSk
                        && decSk.LastEvaluationDecision != null)
                    {
                        var decision = decSk.LastEvaluationDecision;
                        var shouldPublish = true;

                        if (!_botSettings.DecisionTrailIncludeNotActivated
                            && string.Equals(decision.RejectionReason,
                                BingXBot.Core.Diagnostics.RejectionReasons.StateNotActivated, StringComparison.Ordinal))
                        {
                            shouldPublish = false;
                        }
                        else if (decision.Triggered && _botSettings.DecisionTrailDeduplicateTriggers)
                        {
                            var seqKey = BuildDecisionDedupKey(decision);
                            if (seqKey != null && !_loggedTriggeredSequences.TryAdd(seqKey, 1))
                                shouldPublish = false;
                        }

                        if (shouldPublish)
                            _eventBus.PublishEvaluationDecision(decision);
                    }

                    // Scanner-Ergebnis pro TF sammeln (auch für Signal.None — zeigt Status im UI)
                    var symbolDto = new ScannerSymbolDto(
                        Symbol: ticker.Symbol,
                        Price: ticker.LastPrice,
                        Volume24h: ticker.Volume24h,
                        PriceChangePercent: ticker.PriceChangePercent24h,
                        Score: signal.ConfluenceScore,
                        SuggestedSide: signal.Signal is Signal.Long ? "Long"
                                     : signal.Signal is Signal.Short ? "Short"
                                     : null,
                        Reason: signal.Reason);
                    if (scanSymbolsByTf.TryGetValue(navTf, out var listForTf))
                        listForTf.Add(symbolDto);

                    if (signal.Signal == Signal.None)
                    {
                        if (_eventBus.HasLogSubscribers && strategy is Engine.Strategies.SequenzKonzeptStrategy)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "SK",
                                $"{LogPrefix}{ticker.Symbol} [{navTf}]: {signal.Reason}", ticker.Symbol));
                        }
                        continue;
                    }

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Scanner",
                        $"{LogPrefix}{ticker.Symbol} [{navTf}]: {signal.Signal} Signal (Confidence: {signal.Confidence:P0}) - {signal.Reason}",
                        ticker.Symbol));

                    // Close-Signale verarbeiten, dann re-evaluieren für Entry in Gegenrichtung
                    if (signal.Signal is Signal.CloseLong or Signal.CloseShort)
                    {
                        var closeSide = signal.Signal == Signal.CloseLong ? Side.Buy : Side.Sell;
                        if (positions.Any(p => p.Symbol == ticker.Symbol && p.Side == closeSide))
                        {
                            await ClosePositionAndPublishAsync(ticker.Symbol, closeSide).ConfigureAwait(false);
                            positions = await GetPositionsForScanAsync().ConfigureAwait(false);
                            account = await GetAccountAsync().ConfigureAwait(false);
                        }
                        var reContext = context with { OpenPositions = positions, Account = account };
                        signal = strategy.Evaluate(reContext);
                        if (signal.Signal is not (Signal.Long or Signal.Short))
                            continue;
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Scanner",
                            $"{LogPrefix}{ticker.Symbol} [{navTf}]: Re-Eval nach Close → {signal.Signal} ({signal.Reason})", ticker.Symbol));
                    }

                    var candles = navCandles; // Alias für den Rest der Order-Logik

                    // BUCH-ONLY: Kein Korrelations-Check zwischen offenen Positionen (nicht im Buch).

                    // Leverage: Marktspezifisches Maximum
                    var catSettingsStd = _riskSettings.GetCategorySettings(category);
                    var adaptLevStd = (int)catSettingsStd.MaxLeverage;

                    var fundingRateStd = _fundingRates.GetValueOrDefault(ticker.Symbol, 0m);
                    var riskCheck = _riskManager!.ValidateTrade(signal, context, fundingRateStd, adaptLevStd);
                    if (!riskCheck.IsAllowed)
                    {
                        if (_eventBus.HasLogSubscribers)
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                                $"{LogPrefix}{ticker.Symbol} [{navTf}]: Trade abgelehnt - {riskCheck.RejectionReason}", ticker.Symbol));
                        continue;
                    }

                    var positionSizeStd = riskCheck.AdjustedPositionSize;

                    // BTC-Health-Filter (v1.2.5) — nur fuer Crypto relevant (TradFi ist von BTC entkoppelt).
                    // AllowLong/AllowShort aus BtcHealthResult sind harte Blocker bei extremem BTC-Score.
                    // PositionScale (0.65..1.0) skaliert die Positionsgroesse linear nach BTC-Zustand.
                    var signalIsLong = signal.Signal == Signal.Long;
                    if (btcHealth != null && category == MarketCategory.Crypto)
                    {
                        var btcAllows = signalIsLong ? btcHealth.AllowLong : btcHealth.AllowShort;
                        if (!btcAllows)
                        {
                            if (_eventBus.HasLogSubscribers)
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                                    $"{LogPrefix}{ticker.Symbol} [{navTf}]: BTC-Health blockiert {(signalIsLong ? "Long" : "Short")} (Score={btcHealth.Score}, {btcHealth.Reasons})", ticker.Symbol));
                            continue;
                        }
                        if (btcHealth.PositionScale > 0m && btcHealth.PositionScale < 1m)
                        {
                            positionSizeStd *= btcHealth.PositionScale;
                            if (_eventBus.HasLogSubscribers)
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Risk",
                                    $"{LogPrefix}{ticker.Symbol} [{navTf}]: BTC-PositionScale={btcHealth.PositionScale:F2} (Score={btcHealth.Score})", ticker.Symbol));
                        }
                    }

                    // SK-Score Position-Sizing (v1.2.5) — Confluence-basierte Skalierung:
                    //   < 5  → 75%  (Setup marginal ueber Min-Confluence)
                    //   5-9  → 100% (Basis-SK-Setup, Buch-konform)
                    //   ≥10  → 125% (Mehrfach-Confluence, hohe Gewinn-Wahrscheinlichkeit)
                    // Diese Schwellen stehen in CLAUDE.md dokumentiert, Code hat es bisher nicht umgesetzt.
                    if (signal.ConfluenceScore > 0)
                    {
                        var skScale = signal.ConfluenceScore switch
                        {
                            >= 10 => 1.25m,
                            >= 5  => 1.00m,
                            _     => 0.75m,
                        };
                        if (skScale != 1m)
                        {
                            positionSizeStd *= skScale;
                            if (_eventBus.HasLogSubscribers)
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Risk",
                                    $"{LogPrefix}{ticker.Symbol} [{navTf}]: SK-Score-Scale={skScale:F2} (Score={signal.ConfluenceScore})", ticker.Symbol));
                        }
                    }

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                        $"{LogPrefix}{ticker.Symbol} [{navTf}]: Sizing: Qty={positionSizeStd:G6} | Lev={adaptLevStd}x | estMargin={positionSizeStd * ticker.LastPrice / adaptLevStd:F2} USDT"));

                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;

                    // Dedup pro (Symbol, NavTF, Side) — aber eine Position auf BingX je (Symbol, Side).
                    // Wenn bereits ein anderes TF-Signal offen ist, skippen wir (BingX würde sonst die Position nur vergrößern).
                    var slTpKey = $"{ticker.Symbol}_{side}";
                    if (_positionSignals.ContainsKey(slTpKey))
                    {
                        // v1.7.0 Phase 16 — Cross-TF-Pyramiding (User-Ausnahme).
                        // Bedingungen fuer Add-On:
                        //   1. RiskSettings.EnableCrossTfPyramiding=true (opt-in)
                        //   2. Aktuelle navTf ist STRENG HOEHER als die der bestehenden Position (W1>D1>H4>H1>M15)
                        //   3. PyramidAddOnCount < PyramidMaxAddOns
                        //   4. Side identisch (Pyramiding nur auf bestehende Trade-Richtung)
                        // Wenn alle erfuellt: zusaetzliche Order mit positionSizeStd * PyramidScalePercent.
                        var addOnPlaced = false;
                        if (_riskSettings.EnableCrossTfPyramiding
                            && _exitStates.TryGetValue(slTpKey, out var existingExit)
                            && existingExit.PyramidAddOnCount < _riskSettings.PyramidMaxAddOns
                            && (int)navTf > (int)existingExit.NavigatorTimeframe)
                        {
                            var addOnQty = positionSizeStd * _riskSettings.PyramidScalePercent;
                            if (addOnQty > 0m)
                            {
                                var addOnSig = signal with { /* keine SL-Aenderung — bestehende Position behaelt SL */ };
                                var addOnOk = await PlaceOrderOnExchangeAsync(ticker, side, addOnQty, addOnSig, adaptLevStd).ConfigureAwait(false);
                                if (addOnOk)
                                {
                                    existingExit.PyramidEntries.Add(new BingXBot.Core.Models.PyramidEntry(
                                        Tf: navTf,
                                        EntryTimeUtc: DateTime.UtcNow,
                                        EntryPrice: ticker.LastPrice,
                                        Quantity: addOnQty,
                                        TakeProfit1: addOnSig.TakeProfit,
                                        TakeProfit2: addOnSig.TakeProfit2));
                                    existingExit.PyramidAddOnCount++;
                                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                                        $"{LogPrefix}{ticker.Symbol} [{navTf}]: Pyramid-Add-On #{existingExit.PyramidAddOnCount} {side} {addOnQty:F4} @ {ticker.LastPrice:F8} (Scale={_riskSettings.PyramidScalePercent:F2})",
                                        ticker.Symbol));
                                    addOnPlaced = true;
                                }
                            }
                        }
                        if (addOnPlaced) continue;

                        if (_eventBus.HasLogSubscribers)
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Risk",
                                $"{LogPrefix}{ticker.Symbol} [{navTf}]: Übersprungen — Signal bereits aktiv für {side}", ticker.Symbol));
                        continue;
                    }

                    // Order platzieren
                    var placed = await PlaceOrderOnExchangeAsync(ticker, side, positionSizeStd, signal, adaptLevStd).ConfigureAwait(false);
                    if (!placed) continue;

                    _positionSignals[slTpKey] = signal;

                    _exitStates[slTpKey] = new PositionExitState
                    {
                        Signal = signal, Symbol = ticker.Symbol, Side = side,
                        EntryPrice = ticker.LastPrice, OriginalQuantity = positionSizeStd,
                        Tp2 = signal.TakeProfit2,
                        SequenceId = signal.SequenceId,
                        NavigatorTimeframe = navTf,
                        NavPointA = signal.NavPointA ?? 0m,       // A-Bruch-BE-Trigger (Buch-Masterclass)
                        RunnerAtrBase = signal.EntryAtr ?? 0m,    // Task 4.7: Trailing-ATR-Basis
                        RunnerHardCap = signal.RunnerHardCap ?? 0m, // Task 4.7: 423.6% Hard-Cap
                    };
                    Interlocked.Increment(ref _tradesToday);
                    OnSignalCreated(slTpKey);

                    // v1.3.0 K1: TradeOpened-Event fuer Remote-Clients — konstruierte Position,
                    // weil die echte Exchange-Position erst im naechsten PriceTickerLoop auftaucht.
                    // UnrealizedPnl=0, MarginType=Isolated (unser Default), Werte werden im naechsten
                    // PositionUpdated-Event (5 s spaeter) korrigiert.
                    _eventBus.PublishTradeOpened(new Position(
                        Symbol: ticker.Symbol,
                        Side: side,
                        EntryPrice: ticker.LastPrice,
                        MarkPrice: ticker.LastPrice,
                        Quantity: positionSizeStd,
                        UnrealizedPnl: 0m,
                        Leverage: adaptLevStd,
                        MarginType: MarginType.Isolated,
                        OpenTime: DateTime.UtcNow), navTf);

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                        $"{LogPrefix}{ticker.Symbol} [{navTf}]: {side} {positionSizeStd:F4} @ {ticker.LastPrice:F8} | Lev={adaptLevStd}x | SL={signal.StopLoss:F8} | TP1={signal.TakeProfit:F8} | TP2={signal.TakeProfit2:F8}",
                        ticker.Symbol));

                    await OnOrderPlacedAsync(ticker, side, riskCheck.AdjustedPositionSize).ConfigureAwait(false);
                    orderPlaced = true;

                    if (orderPlaced)
                    {
                        account = await GetAccountAsync().ConfigureAwait(false);
                        positions = await GetPositionsForScanAsync().ConfigureAwait(false);
                        orderPlaced = false;
                    }
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                        $"{LogPrefix}{ticker.Symbol} [{navTf}]: Fehler - {ex.Message}", ticker.Symbol));
                }
            }
        }

        // SK-Ampel pro TF publizieren (UI zeigt Status-Tabelle pro Navigator-TF)
        if (_lastSkStatusByTf.Count > 0)
        {
            var ampelSnapshot = new Dictionary<TimeFrame, string>(_lastSkStatusByTf);
            // Fehlende aktive TFs mit "—" auffüllen, damit die UI alle 4 Zeilen zeigt
            foreach (var tf in activeTfs)
                if (!ampelSnapshot.ContainsKey(tf))
                    ampelSnapshot[tf] = "—";
            _eventBus.PublishSkAmpel(ampelSnapshot);
        }

        // Scanner-Cache pro TF aktualisieren (für /api/v1/scanner/results)
        if (_scannerCache != null)
        {
            foreach (var (tf, symbolList) in scanSymbolsByTf)
            {
                var ordered = symbolList
                    .OrderByDescending(s => s.Score)
                    .ThenByDescending(s => s.Volume24h)
                    .Take(50)
                    .ToList();
                _scannerCache.Update(new ScannerResultDto(
                    NavigatorTimeframe: tf,
                    TimestampUtc: DateTime.UtcNow,
                    Symbols: ordered));
            }
        }

        // Strategy-Health-Check: Warnung bei Performance-Degradation
        if (_riskManager != null)
        {
            var healthWarning = _riskManager.CheckStrategyHealth();
            if (healthWarning != null)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Health",
                    $"{LogPrefix}Strategy-Health-Warnung: {healthWarning}"));

                if (_botSettings.EnableDesktopNotifications)
                    _eventBus.PublishNotification("Strategy Health", healthWarning);
            }
        }

        // Scan-Zusammenfassung: Kompakte Info-Zeile mit Kandidaten, Ergebnis
        var elapsed = (DateTime.UtcNow - scanStart).TotalSeconds;
        var nextScanFinal = DateTime.UtcNow.AddSeconds(_scannerSettings.ScanIntervalSeconds).ToLocalTime();
        var strategyInfo = _strategyManager.CurrentTemplate?.Name ?? "n/a";
        var posCount = positions.Count;

        // SK-System: Detaillierten Status des zuletzt evaluierten Symbols anzeigen
        var skStatus = "";
        if (!string.IsNullOrEmpty(_lastSkStatus))
        {
            skStatus = $" | SK: {_lastSkStatus}";
        }

        var scanSummary = $"{candidates.Count} Kandidaten | {strategyInfo} | " +
            $"Positionen: {posCount}/{_riskSettings.MaxOpenPositions} | " +
            $"{elapsed:F1}s | Nächster: {nextScanFinal:HH:mm:ss}{skStatus}";
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Scanner", $"{LogPrefix}{scanSummary}"));

        // Indikator-Cache nach Scan-Durchlauf leeren (Daten sind beim nächsten Scan veraltet)
        IndicatorHelper.ClearCache();
    }

    /// <summary>Publiziert eine kompakte Scan-Zusammenfassung (Info-Level) bei Early-Returns.</summary>
    private void PublishScanSummary(string reason, string nextScanText)
    {
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Scanner",
            $"{LogPrefix}{reason} | {nextScanText}"));
    }

    /// <summary>
    /// Multi-TF Standalone: Lädt Kerzen für ein Symbol/TF innerhalb des Scan-Loops (Rate-Limit-gethrottelt).
    /// Gibt bei Exception eine leere Liste zurück (ruft ConfigureAwait(false) intern).
    /// </summary>
    private async Task<List<Candle>> FetchCandlesAsync(string symbol, TimeFrame tf, int daysBack, CancellationToken ct)
    {
        await _klineSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            return await _publicClient.GetKlinesAsync(
                symbol, tf, now.AddDays(-daysBack), now, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return new List<Candle>();
        }
        finally { _klineSemaphore.Release(); }
    }

    /// <summary>Lookback-Tage für Navigator-TF (Plan Abschnitt 2).</summary>
    private static int GetNavigatorLookbackDays(TimeFrame tf) => tf switch
    {
        TimeFrame.D1 => 365,                           // ~365 Kerzen
        TimeFrame.H4 => 60,                            // ~360 Kerzen (60 Tage × 6)
        TimeFrame.H1 => 20,                            // ~480 Kerzen (20 Tage × 24)
        TimeFrame.M5 => 2,                             // ~576 Kerzen (2 Tage × 288)
        TimeFrame.M15 => 5,                            // ~480 Kerzen
        TimeFrame.M30 => 10,
        TimeFrame.W1 => 730,
        _ => 30,
    };

    /// <summary>Lookback-Tage für Filter-TF (~200 Kerzen Ziel).</summary>
    private static int GetFilterLookbackDays(TimeFrame tf) => tf switch
    {
        TimeFrame.H4 => 35,
        TimeFrame.H1 => 10,
        TimeFrame.M15 => 3,
        TimeFrame.M5 => 1,
        TimeFrame.M1 => 1,
        _ => 14,
    };

    // ═══════════════════════════════════════════════════════════════
    // Abstrakte Methoden (müssen von Subklassen implementiert werden)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Account-Info von der Exchange abrufen.</summary>
    protected abstract Task<AccountInfo> GetAccountAsync();

    /// <summary>Offene Positionen für den Scan-Zyklus abrufen.</summary>
    protected abstract Task<IReadOnlyList<Position>> GetPositionsForScanAsync();

    /// <summary>Offene Positionen für den PriceTicker-Loop abrufen.</summary>
    protected abstract Task<IReadOnlyList<Position>> GetPositionsForTickerAsync();

    /// <summary>Preis auf der Exchange setzen (nur für SimulatedExchange relevant, Live ignoriert).</summary>
    protected abstract void SetCurrentPriceIfNeeded(string symbol, decimal price);

    /// <summary>Order auf der Exchange platzieren. Gibt true zurück bei Erfolg. Signal optional für native SL/TP. adaptiveLeverage überschreibt MaxLeverage wenn > 0.</summary>
    protected abstract Task<bool> PlaceOrderOnExchangeAsync(Ticker ticker, Side side, decimal quantity, SignalResult? signal = null, int adaptiveLeverage = 0);

    /// <summary>Position schließen und CompletedTrade publizieren.</summary>
    protected abstract Task ClosePositionAndPublishAsync(string symbol, Side side);

    /// <summary>Wird aufgerufen wenn SL/TP getroffen wurde. isStopLoss=true für SL, false für TP.</summary>
    protected abstract Task OnSlTpHitAsync(Position pos, decimal price, string key, string reason, bool isStopLoss);

    /// <summary>Partial Close: Schließt einen Teil der Position (Multi-Stage TP1). Menge in Basiswährung.</summary>
    protected abstract Task OnPartialCloseAsync(Position pos, decimal price, decimal quantityToClose);

    // ═══════════════════════════════════════════════════════════════
    // Virtuelle Hooks (optional überschreibbar)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Hook: Wird aufgerufen wenn ein Signal entfernt wird (z.B. für _signalCreatedAt in Live).</summary>
    protected virtual void OnSignalRemoved(string key) { }

    /// <summary>Hook: Wird aufgerufen wenn alle Signale geleert werden (Stop/Emergency).</summary>
    protected virtual void OnSignalsClearedAll() { }

    /// <summary>Hook: Wird aufgerufen wenn ein neues Signal erstellt wird (z.B. für _signalCreatedAt in Live).</summary>
    protected virtual void OnSignalCreated(string key) { }

    /// <summary>Hook: Zusätzliche Wartezeit bei Scan-Fehlern (Standard: keine, Live: 60s).</summary>
    protected virtual Task OnScanErrorAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Hook: Wird einmal pro Scan-Zyklus aufgerufen, NACH Klines-Load und VOR der Symbol-Evaluation.
    /// Subklassen koennen hier Daten laden die pro Scan einmalig sind (Funding-Rates etc.).
    /// Standard: no-op. Live: Funding-Rate-Prefetch fuer Kandidaten ohne Cache.
    /// </summary>
    protected virtual Task PreloadScanDataAsync(IReadOnlyList<Ticker> candidates, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Hook: Wird vor jeder PriceTicker-Iteration aufgerufen (z.B. verwaiste Signale bereinigen). Positionen werden übergeben.</summary>
    protected virtual Task OnBeforePriceTickerIteration(IReadOnlyList<Position> positions) => Task.CompletedTask;

    /// <summary>Hook: Zusätzliche Aktionen nach erfolgreicher Order-Platzierung.</summary>
    protected virtual Task OnOrderPlacedAsync(Ticker ticker, Side side, decimal quantity) => Task.CompletedTask;

    /// <summary>
    /// Verarbeitet einen abgeschlossenen Trade: Risiko-Update + Symbol-Cooldown + Desktop-Notification.
    /// Subklassen sollten diese Methode aufrufen statt _riskManager.UpdateDailyStats direkt.
    /// </summary>
    protected void ProcessCompletedTrade(CompletedTrade trade)
    {
        _riskManager?.UpdateDailyStats(trade);

        // Buch Workflow 6.8: BE-Ausstoppung → sofortiger Re-Entry (kein Cooldown)
        // Erkennen: Trade-PnL nahe 0 (±0.2% von Entry×Quantity) = BE-Exit
        var isBreakEvenExit = trade.EntryPrice > 0
                             && Math.Abs(trade.Pnl) < Math.Abs(trade.EntryPrice * trade.Quantity) * 0.002m;

        if (trade.Pnl < 0 && !isBreakEvenExit)
        {
            Interlocked.Increment(ref _consecutiveLosses);
        }
        else if (isBreakEvenExit)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                $"{LogPrefix}{trade.Symbol}: BE-Exit (Buch 6.8: sofort Re-Entry erlaubt)", trade.Symbol));
            Interlocked.Exchange(ref _consecutiveLosses, 0);
        }
        else
        {
            Interlocked.Exchange(ref _consecutiveLosses, 0);
        }

        // Sync RiskManager.CurrentConsecutiveLosses mit Base-Counter (v1.2.5).
        // RiskManager.UpdateDailyStats erhoeht bei Pnl<0 unabhaengig von BE-Detection.
        // Base-Counter kennt die BE-Exit-Regel — ohne Sync waere GetPositionScalingFactor
        // (nutzt RiskManager.CurrentConsecutiveLosses) zu aggressiv nach BE-Exits.
        _riskManager?.SetConsecutiveLosses(_consecutiveLosses);

        // Desktop-Notification senden
        if (_botSettings.EnableDesktopNotifications)
        {
            var direction = trade.Pnl >= 0 ? "Gewinn" : "Verlust";
            _eventBus.PublishNotification(
                $"{LogPrefix}{trade.Symbol} geschlossen",
                $"{direction}: {trade.Pnl:F2} USDT ({trade.Side}, {trade.EntryPrice:F4} → {trade.ExitPrice:F4})");
        }

        // Snapshot-Report-Fix Befund 1: Trade-Persistenz im Live-Pfad reaktivieren.
        // Vorher rief KEIN Pfad SaveTradeAsync waehrend des Live-Trade-Closes auf — Pi-DB war blind,
        // alle abgeschlossenen Trades existierten nur im SignalR-RAM und gingen beim Reload verloren.
        // Fire-and-Forget mit try/catch + Log: Fill-Pfad hat strenge Timing-Constraints und darf nicht
        // durch DB-IO blockiert werden, aber DB-Fehler werden sichtbar geloggt.
        if (_dbService != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _dbService.SaveTradeAsync(trade).ConfigureAwait(false);
                    if (PostTradePersistHook != null)
                    {
                        try { await PostTradePersistHook(trade).ConfigureAwait(false); }
                        catch (Exception hookEx)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "DB",
                                $"PostTradePersistHook fehlgeschlagen: {hookEx.Message}", trade.Symbol));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "DB",
                        $"SaveTradeAsync fehlgeschlagen ({trade.Symbol} {trade.Side} PnL {trade.Pnl:F2}): {ex.Message}",
                        trade.Symbol));
                }
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Dispose
    // ═══════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _klineSemaphore.Dispose();
        DisposeAdditional();
    }

    /// <summary>Stoppt den Service. Kann von Subklassen überschrieben werden.</summary>
    public virtual Task StopAsync()
    {
        StopBase(BotState.Stopped, $"{ModeName} gestoppt");
        return Task.CompletedTask;
    }

    /// <summary>Notfall-Stop: Alle Positionen schließen. Kann von Subklassen überschrieben werden.</summary>
    public virtual Task EmergencyStopAsync()
    {
        StopBase(BotState.EmergencyStop, $"{ModeName} Notfall-Stop");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Hook: SL wurde auf Break-Even gesetzt (Workflow 4.2 "A-Bruch").
    /// LiveTradingService überschreibt dies und aktualisiert den nativen SL auf BingX.
    /// </summary>
    protected virtual Task OnStopLossAdjustedAsync(string symbol, Side side, decimal newStopLoss) => Task.CompletedTask;

    /// <summary>
    /// Hook: Synchrone Persistierung der ExitStates nach kritischen Mutationen (TP-OrderId set/null,
    /// Phase-Transition Initial→Tp1Hit, RunnerActive=true, BreakevenSet=true).
    /// LiveTradingService überschreibt mit DB-Write — schützt vor Hot-Crash zwischen Mutation
    /// und nächstem Stop-Zyklus (vor diesem Fix wurden ExitStates nur in StopAsync/EmergencyStopAsync
    /// persistiert → TP-OrderId-Zuordnung ging bei Hot-Crash verloren). PaperTradingService no-op.
    /// </summary>
    protected virtual Task PersistExitStatesAsync() => Task.CompletedTask;


    /// <summary>Hook: Zusätzliche Dispose-Logik für Subklassen (z.B. WebSocket-Cleanup).</summary>
    protected virtual void DisposeAdditional() { }
}