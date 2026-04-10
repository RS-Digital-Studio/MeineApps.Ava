using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Core.Models.ATI;
using BingXBot.Engine.ATI;
using BingXBot.Engine.Filters;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using System.Collections.Concurrent;

namespace BingXBot.Services;

/// <summary>
/// Abstrakte Basisklasse für Paper- und Live-Trading-Services.
/// Enthält die gesamte gemeinsame Logik: Scan-Loop (30s), PriceTicker-Loop (5s),
/// SL/TP-Prüfung, Trailing-Stop, Korrelations-Check, Risk-Management,
/// Margin-Monitoring, ATI Auto-Save, Desktop-Notifications.
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
    /// <summary>Geteilter RiskManager vom MultiModeOrchestrator. Wenn gesetzt, wird dieser statt des eigenen verwendet.</summary>
    public RiskManager? RiskManagerOverride { get; set; }
    protected CancellationTokenSource? _cts;
    protected volatile bool _isRunning;
    protected volatile bool _isPaused;
    protected bool _disposed;
    protected DateTime _lastDailyResetDate = DateTime.UtcNow.Date;

    // Funding-Rates pro Symbol (wird von Subklassen aktualisiert, z.B. aus BingX API)
    protected readonly ConcurrentDictionary<string, decimal> _fundingRates = new();
    // Margin-Monitoring: Bereits gewarnte Positionen (nicht bei jedem Tick erneut warnen)
    private readonly ConcurrentDictionary<string, DateTime> _marginWarningsIssued = new();

    // SL/TP-Tracking: Speichert das Original-Signal pro offener Position (Symbol_Side → SignalResult)
    // ConcurrentDictionary weil PriceTickerLoop und ScanAndTradeAsync parallel darauf zugreifen
    protected readonly ConcurrentDictionary<string, SignalResult> _positionSignals = new();
    // SK-System: Letzter Status für Scan-Summary (wird auf Symbol-Klonen evaluiert, nicht auf dem Template)
    private string _lastSkStatus = "";
    // Trailing-Stop: Höchst-/Tiefstpreis seit Eröffnung pro Position (Symbol_Side → Preis)
    protected readonly ConcurrentDictionary<string, decimal> _extremePriceSinceEntry = new();
    // Trailing-Stop pro Position: ATI kann pro Trade einen optimierten Trailing-Prozentsatz setzen
    protected readonly ConcurrentDictionary<string, decimal> _positionTrailingPercent = new();
    // Wiederverwendbares Dictionary für Ticker-Preise (ConcurrentDictionary für Thread-Safety
    // da PriceTickerLoop und RunLoopAsync parallel laufen)
    private readonly ConcurrentDictionary<string, decimal> _tickerPriceMap = new();

    // Multi-Stage Exit: Vollständiger Positions-Zustand (ersetzt teilweise _positionSignals für Exit-Logik)
    protected readonly ConcurrentDictionary<string, PositionExitState> _exitStates = new();
    // Verlust-Tracking (für Leverage-Reduktion bei Verlusten, kein Cooldown-Pause mehr)
    protected volatile int _consecutiveLosses;
    // Täglicher Trade-Counter (wird bei Tageswechsel zurückgesetzt)
    protected int _tradesToday;
    // Equity-Curve-Trading: Equity-Historie für EMA-Berechnung
    // Lock nötig: Add() aus ProcessCompletedTrade, Lesen aus GetEquityCurveScaleFactor (verschiedene Loops)
    private readonly List<decimal> _equityHistory = new();
    private readonly object _equityLock = new();
    // Semaphore für paralleles Klines-Laden (max 5 gleichzeitige Requests)
    private readonly SemaphoreSlim _klineSemaphore = new(10); // 10 parallele Klines-Requests (war 5)

    // ATI: Adaptive Trading Intelligence (optional, kann aktiviert/deaktiviert werden)
    protected AdaptiveTradingIntelligence? _ati;
    // Letztes erkanntes Regime (für Wechsel-Erkennung im Activity Feed)
    private MarketRegime? _lastLoggedRegime;
    // Bot-Einstellungen (für ATI Auto-Save Intervall, Notifications etc.)
    protected readonly BotSettings _botSettings;

    // Fear & Greed Index Cache (wird alle 15 Min aktualisiert, API hat 60 req/min Limit)
    private float _cachedFearGreedIndex;
    /// <summary>Letzter Fear & Greed Wert [0, 1] normalisiert. Fuer Dashboard-Widget.</summary>
    public float CachedFearGreedIndex => _cachedFearGreedIndex;
    private DateTime _lastFearGreedFetch = DateTime.MinValue;
    private static readonly HttpClient _fearGreedClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    // Open Interest Tracking pro Symbol: Vorheriger Wert für Change-Berechnung
    private readonly ConcurrentDictionary<string, decimal> _previousOpenInterest = new();

    /// <summary>Adaptive Trading Intelligence Instanz (null = deaktiviert).</summary>
    public AdaptiveTradingIntelligence? ATI
    {
        get => _ati;
        set => _ati = value;
    }

    /// <summary>Ob der Service gerade läuft.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Ob der Service pausiert ist.</summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Wenn true, werden BotState-Events in StopBase() unterdrückt.
    /// Wird vom MultiModeOrchestrator gesetzt, damit StopAllAsync() nicht 3x BotState.Stopped feuert.
    /// </summary>
    public bool SuppressBotStateEvents { get; set; }

    /// <summary>Log-Präfix ("" für Paper, "LIVE: " für Live).</summary>
    protected abstract string LogPrefix { get; }

    /// <summary>Modus-Name für Log-Texte ("Paper-Trading" vs "Live-Trading").</summary>
    protected abstract string ModeName { get; }

    /// <summary>
    /// Eindeutiger Identifier für ATI-OpenTrade-Keys im Multi-Mode-Betrieb.
    /// Verhindert Key-Kollision wenn mehrere Instanzen dieselbe ATI-Instanz teilen.
    /// </summary>
    private string AtiSourceId => _scannerSettings.ScanTimeFrame.ToString();

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
        _positionSignals[key] = signal;
        // Entry-Preis als Startwert für _extremePriceSinceEntry (Trailing-Stop-Dictionary)
        var entry = signal.EntryPrice ?? 0m;
        if (entry > 0)
            _extremePriceSinceEntry[key] = entry;
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
                // Entry-Preis als konservativer Startwert für Trailing-Stop (nicht 0!)
                // Bei 0 würde Momentum-Decay bei Short sofort triggern (price - 0 = riesig)
                ExtremePriceSinceEntry = entry,
                BreakevenSet = slAlreadyAtBe,
                IsRecovered = true, // Echte Haltezeit unbekannt — Time-Exit mit Karenz
                MaxHoldHours = 0    // Deaktiviert bis Karenz-Periode abgelaufen
            };
        }
    }

    /// <summary>
    /// Entfernt das gespeicherte Signal für eine Position (z.B. bei manuellem Close über Dashboard).
    /// Verhindert, dass PriceTickerLoop eine bereits geschlossene Position erneut zu schließen versucht.
    /// </summary>
    public void RemovePositionSignal(string symbol, Side side) =>
        RemoveSignalByKey($"{symbol}_{side}");

    /// <summary>Entfernt Signal, Extreme-Price, Trailing-% und ruft OnSignalRemoved auf. Zentral für alle Subklassen.</summary>
    protected void RemoveSignalByKey(string key)
    {
        _positionSignals.TryRemove(key, out _);
        _extremePriceSinceEntry.TryRemove(key, out _);
        _positionTrailingPercent.TryRemove(key, out _);
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
        _extremePriceSinceEntry[key] = currentPrice;
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
        _riskManager = RiskManagerOverride ?? riskManager;
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
    }

    /// <summary>Gemeinsames Stop-Cleanup: CTS canceln, Signale leeren, State zurücksetzen.</summary>
    protected void StopBase(BotState endState, string logMessage)
    {
        _isRunning = false;
        _isPaused = false;

        _positionSignals.Clear();
        _extremePriceSinceEntry.Clear();
        _positionTrailingPercent.Clear();
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

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
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
                    await ScanAndTradeAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                    $"Fehler in der {ModeName}-Loop: {ex.Message}"));

                // Subklasse kann zusätzliche Wartezeit definieren (z.B. 60s bei API-Fehler)
                try { await OnScanErrorAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            // 30 Sekunden warten bis zum nächsten Scan
            // Scan-Intervall dynamisch basierend auf Timeframe (H4=15min, H1=5min, etc.)
            try { await Task.Delay(_scannerSettings.ScanIntervalSeconds * 1000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PriceTicker-Loop: Alle 5 Sekunden SL/TP prüfen + Trailing-Stop
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

                // ATI Auto-Save: Periodisch ATI-Lernzustand persistieren
                // TryClaimAutoSave ist atomar: Im Multi-Mode gewinnt nur ein Service pro Intervall.
                // Zeitstempel wird erst nach erfolgreichem Save bestätigt.
                if (_ati is { IsEnabled: true } && _ati.TryClaimAutoSave(_botSettings.AtiAutoSaveIntervalMinutes))
                {
                    try
                    {
                        await OnAtiAutoSaveAsync().ConfigureAwait(false);
                        _ati.ConfirmAutoSave();
                    }
                    catch
                    {
                        // DB-Fehler: Claim freigeben damit nächster Versuch möglich ist
                        _ati.ReleaseAutoSaveClaim();
                    }
                }

                // Regime-Warnung: Bei Chaotic-Regime WARNEN, nicht automatisch schließen.
                // SL schützt die Positionen. Automatisches Schließen bei Chaotic führt zu
                // unnötigen Verlusten weil Krypto-Volatilität oft als "Chaotic" erkannt wird.
                if (_ati is { IsEnabled: true } && positions.Count > 0)
                {
                    var regime = _ati.RegimeDetector.CurrentRegime;
                    if (regime == MarketRegime.Chaotic)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Regime",
                            $"{LogPrefix}Chaotisches Regime erkannt - {positions.Count} Position(en) werden durch SL geschützt"));
                    }
                }

                foreach (var pos in positions)
                {
                    if (!_tickerPriceMap.TryGetValue(pos.Symbol, out var price)) continue;

                    // Preis auf Exchange setzen (nur für Paper relevant)
                    SetCurrentPriceIfNeeded(pos.Symbol, price);

                    // Margin-Monitoring: Liquidationspreis-Abstand prüfen
                    if (_riskManager != null && pos.Leverage > 0)
                    {
                        var liqPrice = _riskManager.CalculateLiquidationPrice(pos.EntryPrice, pos.Leverage, pos.Side);
                        if (liqPrice > 0 && pos.EntryPrice > 0)
                        {
                            var distancePercent = Math.Abs(price - liqPrice) / price * 100m;
                            // Warnung wenn Abstand < 2x MinLiquidationDistance (doppelter Schwellwert als Frühwarnung)
                            var warningThreshold = _riskSettings.MinLiquidationDistancePercent * 2m;
                            var posKey = $"{pos.Symbol}_{pos.Side}";
                            if (distancePercent < warningThreshold)
                            {
                                // Nur alle 5 Minuten erneut warnen pro Position
                                var shouldWarn = !_marginWarningsIssued.TryGetValue(posKey, out var lastWarning)
                                    || (DateTime.UtcNow - lastWarning).TotalMinutes >= 5;
                                if (shouldWarn)
                                {
                                    _marginWarningsIssued[posKey] = DateTime.UtcNow;
                                    _eventBus.PublishMarginWarning(pos.Symbol, price, liqPrice, distancePercent);
                                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Risk",
                                        $"{LogPrefix}{pos.Symbol}: Liquidation nur noch {distancePercent:F1}% entfernt! (Liq={liqPrice:F4}, Preis={price:F4})",
                                        pos.Symbol));
                                }
                            }
                            else
                            {
                                // Warnung zurücksetzen wenn Abstand wieder sicher
                                _marginWarningsIssued.TryRemove(posKey, out _);
                            }
                        }
                    }

                    var key = $"{pos.Symbol}_{pos.Side}";

                    // TradFi bei geschlossenem Markt: Nur Margin-Monitoring, kein SL/TP/Trailing
                    // (Preise sind stale, SL/TP-Trigger auf letztem Kurs wäre falsch)
                    if (!TradingHoursFilter.IsMarketOpen(pos.Symbol, DateTime.UtcNow))
                        continue;

                    // ═══ Auto-Breakeven: SL auf Entry wenn Gewinn% >= Leverage% ═══
                    // Funktioniert auch OHNE Signal (z.B. nach App-Neustart bevor Recovery gelaufen ist)
                    // Bleibt auch bei SK-Trades aktiv — Kapitalschutz hat Vorrang
                    if (pos.Leverage > 0 && pos.EntryPrice > 0)
                    {
                        var beAlreadySet = _exitStates.TryGetValue(key, out var beState) && beState.BreakevenSet;
                        if (!beAlreadySet)
                        {
                            var pnlPercent = pos.Side == Side.Buy
                                ? (price - pos.EntryPrice) / pos.EntryPrice * 100m
                                : (pos.EntryPrice - price) / pos.EntryPrice * 100m;

                            if (pnlPercent >= pos.Leverage)
                            {
                                // Breakeven = Entry + Round-Trip-Fees (0.05% * 2 = 0.1%) + Sicherheitspuffer
                                // 0.15% deckt Fees + minimale Slippage, damit BE-Hit kein Verlust ist
                                var beSl = pos.Side == Side.Buy
                                    ? pos.EntryPrice * 1.0015m
                                    : pos.EntryPrice * 0.9985m;

                                // Signal aktualisieren falls vorhanden
                                if (_positionSignals.TryGetValue(key, out var sig))
                                    _positionSignals[key] = sig with { StopLoss = beSl };

                                // ExitState erstellen/aktualisieren
                                if (beState != null)
                                    beState.BreakevenSet = true;
                                else
                                    _exitStates[key] = new PositionExitState
                                    {
                                        Symbol = pos.Symbol, Side = pos.Side,
                                        EntryPrice = pos.EntryPrice, BreakevenSet = true
                                    };

                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: Auto-Breakeven bei {pnlPercent:F1}% Gewinn (Lev={pos.Leverage}x) → SL auf {beSl:F8}",
                                    pos.Symbol));

                                await OnBreakevenSetAsync(pos.Symbol, pos.Side, beSl).ConfigureAwait(false);
                            }
                        }
                    }

                    if (!_positionSignals.TryGetValue(key, out var signal)) continue;

                    var hit = false;
                    var isStopLoss = false;
                    string reason = "";

                    // ═══ SK-System: Gestufter Breakeven nach Stefan Kassing Regeln ═══
                    // Stufe 1: Gewinn >= 2× SL-Distanz → SL auf Breakeven (Entry + Fees)
                    //   (SK-Originalregel: "Freeride" — Risiko aus dem Markt nehmen)
                    // Stufe 2: Preis über TP1 hinaus (~180% Extension) → SL auf TP1-Level
                    if (signal.DisableSmartBreakeven && signal.StopLoss.HasValue && signal.TakeProfit.HasValue
                        && _exitStates.TryGetValue(key, out var skState) && skState.EntryPrice > 0)
                    {
                        var slDistance = Math.Abs(skState.EntryPrice - signal.StopLoss.Value);
                        var currentProfit = pos.Side == Side.Buy
                            ? price - skState.EntryPrice
                            : skState.EntryPrice - price;

                        // Stufe 2: Preis über TP1 → SL auf TP1-Level (Gewinn absichern)
                        var entryToTp1 = Math.Abs(signal.TakeProfit.Value - skState.EntryPrice);
                        var progressToTp1 = entryToTp1 > 0 ? currentProfit / entryToTp1 : 0m;

                        if (progressToTp1 >= 1.2m && !skState.SkSlAtTp1)
                        {
                            var tp1Sl = signal.TakeProfit.Value;
                            _positionSignals[key] = signal with { StopLoss = tp1Sl };
                            skState.SkSlAtTp1 = true;
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                $"{LogPrefix}{pos.Symbol}: SK SL→TP1 ({tp1Sl:F8}) — Preis {progressToTp1:P0} zum TP1",
                                pos.Symbol));
                            await OnBreakevenSetAsync(pos.Symbol, pos.Side, tp1Sl).ConfigureAwait(false);
                        }
                        // Stufe 1: Gewinn >= 2× SL-Distanz → Breakeven (Stefan Kassing Originalregel)
                        else if (slDistance > 0 && currentProfit >= slDistance * 2m && !skState.BreakevenSet)
                        {
                            var beSl = pos.Side == Side.Buy
                                ? skState.EntryPrice * 1.0015m  // Entry + Fees (0.15% Puffer)
                                : skState.EntryPrice * 0.9985m;
                            _positionSignals[key] = signal with { StopLoss = beSl };
                            skState.BreakevenSet = true;
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                $"{LogPrefix}{pos.Symbol}: SK Breakeven ({beSl:F8}) — Gewinn {currentProfit:F8} >= 2× SL-Distanz {slDistance:F8}",
                                pos.Symbol));
                            await OnBreakevenSetAsync(pos.Symbol, pos.Side, beSl).ConfigureAwait(false);
                        }
                    }

                    // ═══ Multi-Stage Exit: TP1 Partial Close ═══
                    if (_riskSettings.EnableMultiStageExit && _exitStates.TryGetValue(key, out var exitState))
                    {
                        // Phase Initial: Prüfe TP1 (Partial Close)
                        if (exitState.Phase == ExitPhase.Initial && signal.TakeProfit.HasValue && !exitState.PartialClosed)
                        {
                            var tp1Hit = pos.Side == Side.Buy
                                ? price >= signal.TakeProfit.Value
                                : price <= signal.TakeProfit.Value;

                            if (tp1Hit)
                            {
                                // TP1 erreicht: Position teilweise schließen
                                // SK-System: 50% bei TP1 (100% Extension = altes Hoch, Gewinne sichern)
                                // SK Holy Trinity: 50% bei TP1 (Signal-Override), sonst globaler Default
                                var tp1Ratio = signal.Tp1CloseRatioOverride ?? _riskSettings.Tp1CloseRatio;
                                var closeQty = pos.Quantity * tp1Ratio;
                                await OnPartialCloseAsync(pos, price, closeQty).ConfigureAwait(false);
                                exitState.PartialClosed = true;

                                if (!signal.DisableSmartBreakeven)
                                {
                                    // Standard: Smart Breakeven bei TP1 — SL = Entry + ATR-Puffer
                                    var beSl = pos.Side == Side.Buy
                                        ? exitState.EntryPrice * 1.0015m
                                        : exitState.EntryPrice * 0.9985m;
                                    if (_riskSettings.SmartBreakevenAtrMultiplier > 0 && exitState.CurrentAtr > 0)
                                    {
                                        var atrBe = pos.Side == Side.Buy
                                            ? exitState.EntryPrice + exitState.CurrentAtr * _riskSettings.SmartBreakevenAtrMultiplier
                                            : exitState.EntryPrice - exitState.CurrentAtr * _riskSettings.SmartBreakevenAtrMultiplier;
                                        beSl = pos.Side == Side.Buy ? Math.Max(beSl, atrBe) : Math.Min(beSl, atrBe);
                                    }
                                    _positionSignals[key] = signal with { StopLoss = beSl, TakeProfit = exitState.Tp2 };
                                    exitState.Signal = _positionSignals[key];
                                }
                                else
                                {
                                    // SK-System bei TP1: SL mindestens auf Breakeven (gestufter Mechanismus hat
                                    // das evtl. schon bei 50% gesetzt). TP auf TP2 verschieben.
                                    var skSl = exitState.BreakevenSet ? _positionSignals[key].StopLoss : signal.StopLoss;
                                    _positionSignals[key] = signal with { StopLoss = skSl, TakeProfit = exitState.Tp2 };
                                    exitState.Signal = _positionSignals[key];
                                }
                                exitState.Phase = ExitPhase.Tp1Hit;
                                exitState.MaxHoldHours = _riskSettings.MaxHoldHoursAfterTp1;

                                var currentSl = _positionSignals[key].StopLoss;
                                var beInfo = signal.DisableSmartBreakeven ? "SL unverändert (SK-Regel)" : $"SL→BE ({currentSl:F8})";
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: TP1 erreicht → {_riskSettings.Tp1CloseRatio:P0} geschlossen, {beInfo}",
                                    pos.Symbol));
                                continue; // Nächste Position, kein weiterer Check nötig
                            }
                        }

                        // Phase Tp1Hit: Prüfe TP2 (Partial Close, Pyramid 30/30/40)
                        if (exitState.Phase == ExitPhase.Tp1Hit && exitState.PartialClosed && !exitState.Tp2Closed
                            && signal.TakeProfit.HasValue && _riskSettings.Tp2CloseRatio > 0 && _riskSettings.Tp2CloseRatio < 1m)
                        {
                            var tp2Hit = pos.Side == Side.Buy
                                ? price >= signal.TakeProfit.Value
                                : price <= signal.TakeProfit.Value;

                            if (tp2Hit)
                            {
                                // SK-System: TP2 = 200% = Sequenz abgearbeitet → ALLES schließen
                                // Andere Strategien: Tp2CloseRatio (30%), Rest Chandelier-Trailing
                                if (signal.DisableSmartBreakeven)
                                {
                                    // SK-Modus: Kompletter Close bei 200% (Sequenz abgearbeitet)
                                    reason = $"SK TP2 bei {signal.TakeProfit.Value:F8} (200% Extension — Sequenz abgearbeitet)";
                                    hit = true;
                                    isStopLoss = false;
                                }
                                else
                                {
                                    // Standard: Tp2CloseRatio der Gesamt-Position schließen (Pyramid 30/30/40)
                                    var remainingQty = pos.Quantity;
                                    var tp2CloseQty = Math.Round(exitState.OriginalQuantity * _riskSettings.Tp2CloseRatio, 6);
                                    tp2CloseQty = Math.Min(tp2CloseQty, remainingQty);

                                    if (tp2CloseQty > 0 && tp2CloseQty < remainingQty)
                                    {
                                        await OnPartialCloseAsync(pos, price, tp2CloseQty).ConfigureAwait(false);
                                        exitState.Tp2Closed = true;
                                        exitState.Phase = ExitPhase.Trailing;

                                        // Rest läuft nur noch mit Chandelier-Trailing (kein TP mehr)
                                        var updatedSignal = signal with { TakeProfit = null };
                                        _positionSignals[key] = updatedSignal;
                                        exitState.Signal = updatedSignal;

                                        await OnEnterTrailingPhaseAsync(pos.Symbol, pos.Side, updatedSignal.StopLoss).ConfigureAwait(false);

                                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                            $"{LogPrefix}{pos.Symbol}: TP2 erreicht → {_riskSettings.Tp2CloseRatio:P0} geschlossen, Rest Chandelier-Trailing",
                                            pos.Symbol));
                                        continue;
                                    }
                                    else
                                    {
                                        reason = $"TP2 bei {signal.TakeProfit.Value:F8} (Rest zu klein für Partial)";
                                        hit = true;
                                        isStopLoss = false;
                                    }
                                }
                            }
                        }

                        // Recovered Positionen: MaxHoldHours nach 4h Karenz aktivieren
                        // (echte Haltezeit unbekannt, BingX liefert kein OpenTime)
                        if (exitState.IsRecovered && exitState.MaxHoldHours == 0)
                        {
                            var recoveryAge = (DateTime.UtcNow - exitState.EntryTime).TotalHours;
                            if (recoveryAge >= 4)
                            {
                                exitState.MaxHoldHours = _riskSettings.MaxHoldHours;
                                exitState.IsRecovered = false; // Karenz abgelaufen
                            }
                        }

                        // Time-based Exit: Position zu lange offen ohne TP1
                        if (exitState.MaxHoldHours > 0)
                        {
                            var holdHours = (DateTime.UtcNow - exitState.EntryTime).TotalHours;
                            if (holdHours >= exitState.MaxHoldHours)
                            {
                                // Nur schließen wenn nicht im Gewinn (Gewinner laufen lassen)
                                var isInProfit = pos.Side == Side.Buy
                                    ? price > exitState.EntryPrice
                                    : price < exitState.EntryPrice;

                                if (!isInProfit || exitState.Phase == ExitPhase.Initial)
                                {
                                    reason = $"Time-Exit nach {holdHours:F0}h (Max: {exitState.MaxHoldHours}h)";
                                    hit = true;
                                    isStopLoss = false;
                                }
                            }
                        }

                        // Extreme-Price tracken (für Chandelier-Trailing)
                        if (pos.Side == Side.Buy && price > exitState.ExtremePriceSinceEntry)
                            exitState.ExtremePriceSinceEntry = price;
                        else if (pos.Side == Side.Sell && price < exitState.ExtremePriceSinceEntry)
                            exitState.ExtremePriceSinceEntry = price;
                    }

                    // ═══ Standard SL/TP-Check (auch für Multi-Stage Phase) ═══
                    if (!hit)
                    {
                        if (pos.Side == Side.Buy)
                        {
                            if (signal.StopLoss.HasValue && price <= signal.StopLoss.Value)
                            { hit = true; isStopLoss = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:F8}"; }
                            else if (signal.TakeProfit.HasValue && price >= signal.TakeProfit.Value)
                            { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:F8}"; }
                        }
                        else
                        {
                            if (signal.StopLoss.HasValue && price >= signal.StopLoss.Value)
                            { hit = true; isStopLoss = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:F8}"; }
                            else if (signal.TakeProfit.HasValue && price <= signal.TakeProfit.Value)
                            { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:F8}"; }
                        }
                    }

                    // ═══ Chandelier-Trailing (ATR-basiert statt Prozent-basiert) ═══
                    // SK-System: Kein Trailing — SL bleibt strukturell unter Punkt A
                    if (!hit && _riskSettings.EnableTrailingStop && signal.StopLoss.HasValue
                        && !signal.DisableSmartBreakeven)
                    {
                        // Chandelier-Trailing: SL = ExtremPrice - N*ATR (dynamisch, passt sich Volatilität an)
                        decimal trailDistance;
                        if (_exitStates.TryGetValue(key, out var es) && es.CurrentAtr > 0)
                        {
                            // ATR-basierter Trail (bevorzugt für CryptoTrendPro)
                            trailDistance = es.CurrentAtr * es.TrailingAtrMultiplier;
                        }
                        else
                        {
                            // Fallback: Prozent-basiert (für Legacy-Strategien)
                            var trailPercent = _positionTrailingPercent.TryGetValue(key, out var customTrail)
                                ? customTrail / 100m
                                : _riskSettings.TrailingStopPercent / 100m;
                            trailDistance = price * trailPercent;
                        }

                        // Extreme-Price: ExitState als primäre Quelle, _extremePriceSinceEntry als Sync
                        var extreme = _exitStates.TryGetValue(key, out var trEs)
                            ? trEs.ExtremePriceSinceEntry
                            : _extremePriceSinceEntry.GetValueOrDefault(key, pos.EntryPrice);

                        if (pos.Side == Side.Buy)
                        {
                            if (price > extreme) extreme = price;
                            _extremePriceSinceEntry[key] = extreme;
                            if (trEs != null) trEs.ExtremePriceSinceEntry = extreme;

                            var newSl = extreme - trailDistance;
                            if (newSl > signal.StopLoss.Value && newSl < price)
                            {
                                var oldSl = signal.StopLoss.Value;
                                var updated = _positionSignals.AddOrUpdate(key,
                                    signal with { StopLoss = newSl },
                                    (_, current) => current.StopLoss.HasValue && newSl > current.StopLoss.Value && newSl < price
                                        ? current with { StopLoss = newSl }
                                        : current);
                                if (updated.StopLoss.HasValue && updated.StopLoss.Value == newSl)
                                    await OnTrailingStopMovedAsync(pos.Symbol, pos.Side, oldSl, newSl).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            if (price < extreme) extreme = price;
                            _extremePriceSinceEntry[key] = extreme;
                            if (trEs != null) trEs.ExtremePriceSinceEntry = extreme;

                            var newSl = extreme + trailDistance;
                            if (newSl < signal.StopLoss.Value && newSl > price)
                            {
                                var oldSl = signal.StopLoss.Value;
                                var updated = _positionSignals.AddOrUpdate(key,
                                    signal with { StopLoss = newSl },
                                    (_, current) => current.StopLoss.HasValue && newSl < current.StopLoss.Value && newSl > price
                                        ? current with { StopLoss = newSl }
                                        : current);
                                if (updated.StopLoss.HasValue && updated.StopLoss.Value == newSl)
                                    await OnTrailingStopMovedAsync(pos.Symbol, pos.Side, oldSl, newSl).ConfigureAwait(false);
                            }
                        }
                    }

                    // ═══ Momentum-Decay: MACD-Histogramm schrumpft → Position warnen/schließen ═══
                    if (!hit && _riskSettings.EnableMomentumDecay
                        && _exitStates.TryGetValue(key, out var mdState) && mdState.Phase != ExitPhase.Initial)
                    {
                        // Prüfe ob MACD-Histogramm 3+ Balken schrumpft (Momentum stirbt)
                        // Nutzt gecachte Klines falls im gleichen Scan-Zyklus verfügbar
                        if (mdState.CurrentAtr > 0 && mdState.PartialClosed)
                        {
                            // Einfache Heuristik: Wenn Preis sich vom Extreme-Preis entfernt (Momentum-Verlust)
                            var extremeDistance = pos.Side == Side.Buy
                                ? mdState.ExtremePriceSinceEntry - price
                                : price - mdState.ExtremePriceSinceEntry;
                            // Shorts haben stärkere Pullbacks nach schnellem Abstieg → höherer Threshold
                            var atrThreshold = mdState.CurrentAtr * (pos.Side == Side.Buy ? 1.5m : 2.5m);

                            if (extremeDistance > atrThreshold && !mdState.Tp2Closed)
                            {
                                // Momentum-Decay erkannt → Position schließen statt auf SL warten
                                reason = $"Momentum-Decay: Preis {extremeDistance / mdState.CurrentAtr:F1}x ATR vom Höchstpunkt entfernt";
                                hit = true;
                                isStopLoss = false;

                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: {reason}", pos.Symbol));
                            }
                        }
                    }

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
        // MUSS vor dem ersten Scan geladen sein, sonst kommen Meme-Coins durch
        if (!Core.Helpers.MarketCapCache.IsLoaded)
        {
            try
            {
                await Core.Helpers.MarketCapCache.RefreshIfNeededAsync().ConfigureAwait(false);
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
            try { await Core.Helpers.MarketCapCache.RefreshIfNeededAsync().ConfigureAwait(false); }
            catch { /* Stündlicher Refresh optional */ }
        }

        // 1. Alle Ticker holen und filtern
        var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
        if (tickers.Count == 0)
        {
            PublishScanSummary("Keine Ticker verfügbar", nextScanText);
            return;
        }

        var candidates = ScanHelper.FilterCandidates(tickers, _scannerSettings);
        if (candidates.Count == 0)
        {
            PublishScanSummary("Keine Kandidaten (Volumen-/Momentum-Filter)", nextScanText);
            return;
        }

        if (_eventBus.HasLogSubscribers)
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Scanner",
                $"{candidates.Count} Kandidaten gefunden"));

        // 2. Globale MarketFilter prüfen (VOR dem teuren Klines-Loading)
        // Funding-Settlement blockiert nur wenn ausschließlich Krypto-Kandidaten gescannt werden.
        // Bei gemischtem Scan (TradFi aktiv) wird Funding per-Kandidat im Loop geprüft.
        var hasTradFi = _scannerSettings.EnableTradFi && candidates.Any(t => SymbolClassifier.IsTradFi(t.Symbol));
        var sessionFilter = MarketFilter.CheckSession(DateTime.UtcNow, _botSettings.LastTradingModePreset, hasTradFi);
        if (!sessionFilter.IsAllowed)
        {
            PublishScanSummary($"Session-Filter: {sessionFilter.SessionInfo}", nextScanText);
            IndicatorHelper.ClearCache();
            return;
        }

        // 2a. Max Trades/Tag prüfen (0 = unbegrenzt)
        if (_riskSettings.MaxTradesPerDay > 0 && _tradesToday >= _riskSettings.MaxTradesPerDay)
        {
            PublishScanSummary($"Max Trades/Tag erreicht ({_tradesToday}/{_riskSettings.MaxTradesPerDay})", nextScanText);
            IndicatorHelper.ClearCache();
            return;
        }

        // Kein Cooldown-Pause mehr: SL schützt Positionen, Leverage-Reduktion bei Verlusten reicht.

        // 3. Account + Positionen holen
        var account = await GetAccountAsync().ConfigureAwait(false);
        var positions = await GetPositionsForScanAsync().ConfigureAwait(false);

        // 4. Klines für alle Kandidaten PARALLEL vorladen (statt sequenziell pro Kandidat)
        //    Begrenzt auf 5 parallele Requests um Rate-Limiter nicht zu überlasten
        var now = DateTime.UtcNow;
        var klineResults = new ConcurrentDictionary<string, List<Candle>>();
        var htfResults = new ConcurrentDictionary<string, List<Candle>?>();
        var m15Results = new ConcurrentDictionary<string, List<Candle>?>(); // M15 für Entry-Timing

        var klineTasks = candidates.Select(async ticker =>
        {
            await _klineSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Klines-Ladezeitraum TF-abhängig: H4 braucht ~200 Kerzen für State Machine + Indikatoren
                // (100h / 4h = nur 25 Kerzen → zu wenig für SK-System das min. 30 braucht)
                var tfDuration = TimeFrameHelper.ToDuration(_scannerSettings.ScanTimeFrame);
                var klineHours = Math.Max(100, (int)(tfDuration.TotalHours * 200));
                var candles = await _publicClient.GetKlinesAsync(
                    ticker.Symbol, _scannerSettings.ScanTimeFrame,
                    now.AddHours(-klineHours), now, ct).ConfigureAwait(false);

                // SK Holy Trinity: H1 als Filter-TF (nicht D1!), M15 als Trigger-TF (immer laden)
                // Andere Strategien: HtfTimeFrame aus ScannerSettings (z.B. D1)
                var isSKSystem = _strategyManager.CurrentTemplate is Engine.Strategies.SequenzKonzeptStrategy;
                var htfTimeFrame = isSKSystem ? Core.Enums.TimeFrame.H1 : _scannerSettings.HtfTimeFrame;

                List<Candle>? htfCandles = null;
                try
                {
                    htfCandles = await _publicClient.GetKlinesAsync(
                        ticker.Symbol, htfTimeFrame,
                        now.AddDays(-14), now, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* HTF optional */ }

                // M15-Candles: Bei SK IMMER laden (Trigger-TF), bei anderen optional
                List<Candle>? m15Candles = null;
                var loadM15 = isSKSystem || (_scannerSettings.UseM15EntryTiming &&
                    _scannerSettings.ScanTimeFrame is TimeFrame.H4 or TimeFrame.H1 or TimeFrame.H2);
                if (loadM15)
                {
                    try
                    {
                        m15Candles = await _publicClient.GetKlinesAsync(
                            ticker.Symbol, Core.Enums.TimeFrame.M15,
                            now.AddHours(-24), now, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { /* M15 optional */ }
                }

                klineResults[ticker.Symbol] = candles;
                htfResults[ticker.Symbol] = htfCandles;
                m15Results[ticker.Symbol] = m15Candles;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (_eventBus.HasLogSubscribers)
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Scanner",
                        $"{ticker.Symbol}: Klines-Laden fehlgeschlagen: {ex.Message}", ticker.Symbol));
            }
            finally { _klineSemaphore.Release(); }
        });

        await Task.WhenAll(klineTasks).ConfigureAwait(false);

        // 4a. Cross-Market-Features für ATI: BTC-Kontext und Markt-Stimmung
        await UpdateCrossMarketFeaturesAsync(tickers, candidates, klineResults, ct).ConfigureAwait(false);

        // 5. Kandidaten sequenziell evaluieren (Order-Platzierung muss sequenziell sein)
        var orderPlaced = false;

        foreach (var ticker in candidates)
        {
            if (ct.IsCancellationRequested) break;

            // Trading-Hours-Check für TradFi (Krypto = 24/7, immer offen)
            if (!TradingHoursFilter.IsMarketOpen(ticker.Symbol, DateTime.UtcNow))
                continue;

            // Markt-Kategorie bestimmen (für per-Markt Leverage, Feature-Masking, etc.)
            var category = SymbolClassifier.Classify(ticker.Symbol);

            // Funding-Settlement nur für Krypto-Perpetuals (TradFi hat kein Funding)
            if (category == MarketCategory.Crypto && hasTradFi && MarketFilter.IsFundingSettlement(DateTime.UtcNow))
                continue;

            // Vorgeladene Klines verwenden
            if (!klineResults.TryGetValue(ticker.Symbol, out var candles) || candles.Count < 50)
                continue;
            htfResults.TryGetValue(ticker.Symbol, out var htfCandles);
            m15Results.TryGetValue(ticker.Symbol, out var m15Candles);

            try
            {
                // ATI-Pipeline (wenn aktiviert UND Strategie ist ATI-kompatibel):
                // SK-System hat eigene Multi-TF-Logik → nutzt den Standard-Pfad direkt
                var isAtiCompatible = _strategyManager.CurrentTemplate is not Engine.Strategies.SequenzKonzeptStrategy;
                if (_ati is { IsEnabled: true } && isAtiCompatible)
                {
                    SetCurrentPriceIfNeeded(ticker.Symbol, ticker.LastPrice);

                    var context = new MarketContext(ticker.Symbol, candles, ticker, positions, account, htfCandles, category, m15Candles);
                    var atiResult = _ati.EvaluateCandidate(context);
                    if (atiResult == null) continue;

                    var signal = atiResult.Signal;

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "ATI",
                        $"{LogPrefix}{ticker.Symbol}: {signal.Signal} ({signal.Reason})", ticker.Symbol));

                    // Close-Signale verarbeiten, dann sofort re-evaluieren für Entry in Gegenrichtung
                    // (Supertrend-Flip ist nur 1 Candle lang → nächster Scan in 15min verpasst das Entry)
                    if (signal.Signal is Signal.CloseLong or Signal.CloseShort)
                    {
                        var closeSide = signal.Signal == Signal.CloseLong ? Side.Buy : Side.Sell;
                        if (positions.Any(p => p.Symbol == ticker.Symbol && p.Side == closeSide))
                        {
                            await ClosePositionAndPublishAsync(ticker.Symbol, closeSide).ConfigureAwait(false);
                            positions = await GetPositionsForScanAsync().ConfigureAwait(false);
                            account = await GetAccountAsync().ConfigureAwait(false);
                        }
                        // Re-Evaluation: Nach Close kann ein Entry-Signal in Gegenrichtung vorliegen
                        var reContext = new MarketContext(ticker.Symbol, candles, ticker, positions, account, htfCandles, category, m15Candles);
                        var reResult = _ati.EvaluateCandidate(reContext);
                        if (reResult != null && reResult.Signal.Signal is Signal.Long or Signal.Short)
                        {
                            signal = reResult.Signal;
                            atiResult = reResult;
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "ATI",
                                $"{LogPrefix}{ticker.Symbol}: Re-Eval nach Close → {signal.Signal} ({signal.Reason})", ticker.Symbol));
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // Korrelations-Check (auch mit ATI noch relevant)
                    if (await ScanHelper.CheckCorrelationAsync(
                        ticker.Symbol, positions, _riskSettings, _publicClient, candles, _eventBus, LogPrefix, ct))
                        continue;

                    // Adaptiver Leverage: ATR-Perzentil + Score, mit marktspezifischem Maximum
                    var catSettings = _riskSettings.GetCategorySettings(category);
                    var atrPctLev = IndicatorHelper.CalculateAtrPercentile(candles);
                    var adaptLev = CryptoTrendProStrategy.GetAdaptiveLeverage(atrPctLev, signal.ConfluenceScore, (int)catSettings.MaxLeverage);
                    if (_riskSettings.EnableCooldownEscalation && _consecutiveLosses >= 3)
                        adaptLev = Math.Max(1, adaptLev - 1);

                    // Risk-Check mit tatsächlichem Leverage (für korrekte Margin-Berechnung)
                    // Funding-Rate nur für Krypto relevant (TradFi hat keine Perpetual Funding)
                    var fundingRate = category == MarketCategory.Crypto
                        ? _fundingRates.GetValueOrDefault(ticker.Symbol, 0m) : 0m;
                    var riskCheck = _riskManager!.ValidateTrade(signal, context, fundingRate, adaptLev);
                    if (!riskCheck.IsAllowed)
                    {
                        if (_eventBus.HasLogSubscribers)
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                                $"{LogPrefix}{ticker.Symbol}: Trade abgelehnt - {riskCheck.RejectionReason}", ticker.Symbol));
                        continue;
                    }

                    // Equity-Curve-Trading als zusätzlicher Schutz
                    var equityScale = GetEquityCurveScaleFactor();
                    var positionSize = riskCheck.AdjustedPositionSize * equityScale;
                    if (equityScale < 1.0m)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                            $"{LogPrefix}{ticker.Symbol}: Equity-Curve-Scaling ({equityScale:P0})"));
                    }

                    // M15-Entry-Timing: Bei H4/H1-Signal prüfen ob M15 den Einstieg bestätigt
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    if (!CheckM15EntryTiming(m15Candles, side, ticker.Symbol))
                        continue;

                    // Doppelte Order verhindern: Signal-Check ist atomarer als BingX-positions-Liste
                    var slTpKey = $"{ticker.Symbol}_{side}";
                    if (_positionSignals.ContainsKey(slTpKey))
                    {
                        if (_eventBus.HasLogSubscribers)
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Risk",
                                $"{LogPrefix}{ticker.Symbol}: Übersprungen — Signal bereits aktiv für {side}", ticker.Symbol));
                        continue;
                    }

                    // Order platzieren (Signal für native SL/TP mitgeben)
                    var placed = await PlaceOrderOnExchangeAsync(ticker, side, positionSize, signal, adaptLev).ConfigureAwait(false);
                    if (!placed) continue;

                    // Signal speichern + Multi-Stage Exit State erstellen
                    _positionSignals[slTpKey] = signal;
                    _extremePriceSinceEntry[slTpKey] = ticker.LastPrice;
                    _positionTrailingPercent[slTpKey] = atiResult.TrailingStopPercent;

                    var atrVal = IndicatorHelper.CalculateAtr(candles);
                    var atrLast = atrVal.Count > 0 && atrVal[^1].HasValue ? atrVal[^1]!.Value : 0m;

                    _exitStates[slTpKey] = new PositionExitState
                    {
                        Signal = signal, Symbol = ticker.Symbol, Side = side,
                        EntryPrice = ticker.LastPrice, OriginalQuantity = positionSize, // Tatsächlich platzierte Menge (nach Equity-Scaling)
                        Tp2 = signal.TakeProfit2, ExtremePriceSinceEntry = ticker.LastPrice,
                        TrailingAtrMultiplier = atiResult.TrailingStopPercent > 0 ? atiResult.TrailingStopPercent : 2.5m,
                        CurrentAtr = atrLast, ConfluenceScore = signal.ConfluenceScore,
                        MaxHoldHours = _riskSettings.MaxHoldHours
                    };
                    Interlocked.Increment(ref _tradesToday);
                    OnSignalCreated(slTpKey);

                    // ATI-Kontext für späteres Lernen speichern
                    var slMult = signal.StopLoss.HasValue && atrLast > 0
                        ? Math.Abs(ticker.LastPrice - signal.StopLoss.Value) / atrLast : 2m;
                    var tpMult = signal.TakeProfit.HasValue && atrLast > 0
                        ? Math.Abs(signal.TakeProfit.Value - ticker.LastPrice) / atrLast : 4m;
                    _ati.RegisterOpenTrade(ticker.Symbol, side, atiResult.Features,
                        atiResult.Regime, atiResult.EnsembleVote, slMult, tpMult, AtiSourceId);

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                        $"{LogPrefix}{ticker.Symbol}: {side} {positionSize:F4} @ {ticker.LastPrice:F8} | Lev={adaptLev}x | SL={signal.StopLoss:F8} | TP1={signal.TakeProfit:F8} | TP2={signal.TakeProfit2:F8}",
                        ticker.Symbol));

                    await OnOrderPlacedAsync(ticker, side, riskCheck.AdjustedPositionSize).ConfigureAwait(false);
                    orderPlaced = true;
                }
                else
                {
                    // Standard-Pipeline ohne ATI: Vorgeladene Klines an ScanHelper übergeben
                    SetCurrentPriceIfNeeded(ticker.Symbol, ticker.LastPrice);

                    var strategy = _strategyManager.GetOrCreateForSymbol(ticker.Symbol);
                    var context = new MarketContext(ticker.Symbol, candles, ticker, positions, account, htfCandles, category, m15Candles);
                    var signal = strategy.Evaluate(context);

                    // SK-System: Status vom Symbol-Klon speichern (Template wird nie evaluiert)
                    if (strategy is Engine.Strategies.SequenzKonzeptStrategy skInst)
                        _lastSkStatus = skInst.LastStatus;

                    if (signal.Signal == Signal.None)
                    {
                        // SK-System: Jedes Symbol loggen (warum kein Trade)
                        if (_eventBus.HasLogSubscribers && strategy is Engine.Strategies.SequenzKonzeptStrategy)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "SK",
                                $"{LogPrefix}{ticker.Symbol}: {signal.Reason}", ticker.Symbol));
                        }
                        continue;
                    }

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Scanner",
                        $"{LogPrefix}{ticker.Symbol}: {signal.Signal} Signal (Confidence: {signal.Confidence:P0}) - {signal.Reason}",
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
                        // Re-Evaluation: Strategie erneut evaluieren (Position ist jetzt geschlossen)
                        var reContext = new MarketContext(ticker.Symbol, candles, ticker, positions, account, htfCandles, category, m15Candles);
                        signal = strategy.Evaluate(reContext);
                        if (signal.Signal is not (Signal.Long or Signal.Short))
                            continue;
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Scanner",
                            $"{LogPrefix}{ticker.Symbol}: Re-Eval nach Close → {signal.Signal} ({signal.Reason})", ticker.Symbol));
                    }

                    // Korrelations-Check
                    if (await ScanHelper.CheckCorrelationAsync(
                        ticker.Symbol, positions, _riskSettings, _publicClient, candles, _eventBus, LogPrefix, ct))
                        continue;

                    // Adaptiver Leverage: ATR-Perzentil + Score, mit marktspezifischem Maximum
                    var catSettingsStd = _riskSettings.GetCategorySettings(category);
                    var atrPctStd = IndicatorHelper.CalculateAtrPercentile(candles);
                    var adaptLevStd = CryptoTrendProStrategy.GetAdaptiveLeverage(atrPctStd, signal.ConfluenceScore, (int)catSettingsStd.MaxLeverage);
                    if (_riskSettings.EnableCooldownEscalation && _consecutiveLosses >= 3)
                        adaptLevStd = Math.Max(1, adaptLevStd - 1);

                    // Funding-Rate nur für Krypto relevant
                    var fundingRateStd = category == MarketCategory.Crypto
                        ? _fundingRates.GetValueOrDefault(ticker.Symbol, 0m) : 0m;
                    var riskCheck = _riskManager!.ValidateTrade(signal, context, fundingRateStd, adaptLevStd);
                    if (!riskCheck.IsAllowed)
                    {
                        if (_eventBus.HasLogSubscribers)
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                                $"{LogPrefix}{ticker.Symbol}: Trade abgelehnt - {riskCheck.RejectionReason}", ticker.Symbol));
                        continue;
                    }

                    // Score-basiertes + Equity-Curve Position-Sizing
                    var scoreScaleStd = CryptoTrendProStrategy.GetPositionScaleFactor(signal.ConfluenceScore);
                    var equityScaleStd = GetEquityCurveScaleFactor();
                    var positionSizeStd = riskCheck.AdjustedPositionSize * scoreScaleStd * equityScaleStd;
                    if (scoreScaleStd != 1.0m || equityScaleStd != 1.0m)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                            $"{LogPrefix}{ticker.Symbol}: Position skaliert (Score={scoreScaleStd:P0}, Equity={equityScaleStd:P0})"));
                    }

                    // M15-Entry-Timing: Bei H4/H1-Signal prüfen ob M15 den Einstieg bestätigt
                    // SK-System hat eigenen umfassenden 15m-Filter (State Machine, ChoCH, ATR, Over-Extension)
                    // → CheckM15EntryTiming (RSI + Candle-Richtung) würde SK-Signale kontraproduktiv blockieren
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    var isSKSignal = _strategyManager.CurrentTemplate is Engine.Strategies.SequenzKonzeptStrategy;
                    if (!isSKSignal && !CheckM15EntryTiming(m15Candles, side, ticker.Symbol))
                        continue;

                    // Doppelte Order verhindern: Signal-Check ist atomarer als BingX-positions-Liste
                    var slTpKey = $"{ticker.Symbol}_{side}";
                    if (_positionSignals.ContainsKey(slTpKey))
                    {
                        if (_eventBus.HasLogSubscribers)
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Risk",
                                $"{LogPrefix}{ticker.Symbol}: Übersprungen — Signal bereits aktiv für {side}", ticker.Symbol));
                        continue;
                    }

                    // Order platzieren
                    var placed = await PlaceOrderOnExchangeAsync(ticker, side, positionSizeStd, signal, adaptLevStd).ConfigureAwait(false);
                    if (!placed) continue;

                    // SL/TP-Signal speichern + Multi-Stage Exit State
                    _positionSignals[slTpKey] = signal;
                    _extremePriceSinceEntry[slTpKey] = ticker.LastPrice;

                    var atrForExit = IndicatorHelper.CalculateAtr(candles);
                    var atrLastVal = atrForExit.Count > 0 && atrForExit[^1].HasValue ? atrForExit[^1]!.Value : 0m;
                    _exitStates[slTpKey] = new PositionExitState
                    {
                        Signal = signal, Symbol = ticker.Symbol, Side = side,
                        EntryPrice = ticker.LastPrice, OriginalQuantity = positionSizeStd, // Tatsächlich platzierte Menge (nach Score+Equity-Scaling)
                        Tp2 = signal.TakeProfit2, ExtremePriceSinceEntry = ticker.LastPrice,
                        CurrentAtr = atrLastVal, ConfluenceScore = signal.ConfluenceScore,
                        MaxHoldHours = _riskSettings.MaxHoldHours
                    };
                    Interlocked.Increment(ref _tradesToday);
                    OnSignalCreated(slTpKey);

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                        $"{LogPrefix}{ticker.Symbol}: {side} {positionSizeStd:F4} @ {ticker.LastPrice:F8} | Lev={adaptLevStd}x | SL={signal.StopLoss:F8} | TP1={signal.TakeProfit:F8} | TP2={signal.TakeProfit2:F8}",
                        ticker.Symbol));

                    await OnOrderPlacedAsync(ticker, side, riskCheck.AdjustedPositionSize).ConfigureAwait(false);
                    orderPlaced = true;
                }

                // Account + Positionen nur aktualisieren wenn eine Order platziert wurde
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
                    $"{LogPrefix}{ticker.Symbol}: Fehler - {ex.Message}", ticker.Symbol));
            }
        }

        // ATI: Ablehnungs-Zusammenfassung loggen (Info statt Debug, damit im Activity Feed sichtbar)
        if (_ati is { IsEnabled: true } && _eventBus.HasLogSubscribers)
        {
            var summary = _ati.GetScanSummaryAndReset();
            if (summary != null)
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "ATI",
                    $"{LogPrefix}{summary}"));
        }

        // ATI: Regime-Wechsel erkennen und detailliert loggen
        if (_ati is { IsEnabled: true })
        {
            var currentRegime = _ati.RegimeDetector.CurrentRegime;
            if (_lastLoggedRegime.HasValue && _lastLoggedRegime.Value != currentRegime)
            {
                var regimeNames = new Dictionary<MarketRegime, string>
                {
                    [MarketRegime.TrendingBull] = "Aufwärtstrend",
                    [MarketRegime.TrendingBear] = "Abwärtstrend",
                    [MarketRegime.Range] = "Seitwärtsmarkt",
                    [MarketRegime.Chaotic] = "Chaotisch"
                };
                var fromName = regimeNames.GetValueOrDefault(_lastLoggedRegime.Value, _lastLoggedRegime.Value.ToString());
                var toName = regimeNames.GetValueOrDefault(currentRegime, currentRegime.ToString());

                var level = currentRegime == MarketRegime.Chaotic ? LogLevel.Warning : LogLevel.Info;
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, level, "Regime",
                    $"{LogPrefix}Regime-Wechsel: {fromName} → {toName}"));
            }
            _lastLoggedRegime = currentRegime;
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

        // Scan-Zusammenfassung: Kompakte Info-Zeile mit Regime, Kandidaten, Ergebnis
        var elapsed = (DateTime.UtcNow - scanStart).TotalSeconds;
        var nextScanFinal = DateTime.UtcNow.AddSeconds(_scannerSettings.ScanIntervalSeconds).ToLocalTime();
        var strategyInfo = _strategyManager.CurrentTemplate?.Name ?? "n/a";
        // SK-System: Keine ATI-Regime-Info (hat eigene Sequenz-Logik)
        if (_ati is { IsEnabled: true } && _strategyManager.CurrentTemplate is not Engine.Strategies.SequenzKonzeptStrategy)
        {
            var regime = _ati.RegimeDetector.CurrentRegime;
            var regimeNames = new Dictionary<MarketRegime, string>
            {
                [MarketRegime.TrendingBull] = "Bull",
                [MarketRegime.TrendingBear] = "Bear",
                [MarketRegime.Range] = "Range",
                [MarketRegime.Chaotic] = "Chaotisch"
            };
            strategyInfo = $"Regime: {regimeNames.GetValueOrDefault(regime, regime.ToString())}";
        }
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

    /// <summary>
    /// M15-Entry-Timing: Prüft ob der Einstieg auf M15-Ebene bestätigt wird.
    /// H4 gibt die Richtung vor, M15 bestätigt den Einstiegszeitpunkt.
    /// Kriterien: RSI nicht überkauft/überverkauft + letzte M15-Candle in Trendrichtung.
    /// Gibt true zurück wenn kein M15-Check aktiv ist oder wenn M15 den Entry bestätigt.
    /// </summary>
    private bool CheckM15EntryTiming(List<Candle>? m15Candles, Side side, string symbol)
    {
        // Wenn M15-Timing deaktiviert oder keine Daten → Entry erlaubt (kein Filter)
        if (!_scannerSettings.UseM15EntryTiming || m15Candles == null || m15Candles.Count < 20)
            return true;

        // M15 RSI: Kein Entry bei extremen Werten (Reversal-Risiko)
        var rsi = IndicatorHelper.CalculateRsi(m15Candles, 14);
        if (rsi.Count > 0 && rsi[^1].HasValue)
        {
            var rsiVal = rsi[^1]!.Value;
            if (side == Side.Buy && rsiVal > 75)
            {
                if (_eventBus.HasLogSubscribers)
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "M15-Timing",
                        $"{LogPrefix}{symbol}: Long-Entry verzögert (M15 RSI {rsiVal:F0} > 75, überkauft)"));
                return false;
            }
            if (side == Side.Sell && rsiVal < 25)
            {
                if (_eventBus.HasLogSubscribers)
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "M15-Timing",
                        $"{LogPrefix}{symbol}: Short-Entry verzögert (M15 RSI {rsiVal:F0} < 25, überverkauft)"));
                return false;
            }
        }

        // M15 Candle-Richtung: Letzte geschlossene M15-Candle sollte in Trendrichtung sein
        // (mindestens nicht stark gegen den Trend)
        if (m15Candles.Count >= 2)
        {
            var lastClosed = m15Candles[^2]; // Vorletzte = letzte geschlossene
            var candleBody = lastClosed.Close - lastClosed.Open;
            var candleRange = lastClosed.High - lastClosed.Low;

            // Starke Gegen-Candle blockiert Entry (>60% des Range gegen die Richtung)
            if (candleRange > 0)
            {
                var bodyRatio = candleBody / candleRange;
                if (side == Side.Buy && bodyRatio < -0.6m)
                {
                    if (_eventBus.HasLogSubscribers)
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "M15-Timing",
                            $"{LogPrefix}{symbol}: Long-Entry verzögert (starke M15 Bär-Candle {bodyRatio:P0})"));
                    return false;
                }
                if (side == Side.Sell && bodyRatio > 0.6m)
                {
                    if (_eventBus.HasLogSubscribers)
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "M15-Timing",
                            $"{LogPrefix}{symbol}: Short-Entry verzögert (starke M15 Bull-Candle {bodyRatio:P0})"));
                    return false;
                }
            }
        }

        return true; // M15 bestätigt den Entry
    }

    /// <summary>Publiziert eine kompakte Scan-Zusammenfassung (Info-Level) bei Early-Returns.</summary>
    private void PublishScanSummary(string reason, string nextScanText)
    {
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Scanner",
            $"{LogPrefix}{reason} | {nextScanText}"));
    }

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

    /// <summary>Hook: Wird vor jeder PriceTicker-Iteration aufgerufen (z.B. verwaiste Signale bereinigen). Positionen werden übergeben.</summary>
    protected virtual Task OnBeforePriceTickerIteration(IReadOnlyList<Position> positions) => Task.CompletedTask;

    /// <summary>Hook: Zusätzliche Aktionen nach erfolgreicher Order-Platzierung.</summary>
    protected virtual Task OnOrderPlacedAsync(Ticker ticker, Side side, decimal quantity) => Task.CompletedTask;

    /// <summary>Hook: Trailing-Stop wurde nachgezogen. Live synchronisiert den SL auf BingX.</summary>
    protected virtual Task OnTrailingStopMovedAsync(string symbol, Side side, decimal oldSl, decimal newSl) => Task.CompletedTask;

    /// <summary>Hook: Position wechselt in Trailing-Phase (nach TP2). Live cancelt native TP-Orders und setzt nur SL.</summary>
    protected virtual Task OnEnterTrailingPhaseAsync(string symbol, Side side, decimal? currentSl) => Task.CompletedTask;

    /// <summary>Hook: ATI Auto-Save (Subklassen implementieren DB-Zugriff).</summary>
    protected virtual Task OnAtiAutoSaveAsync() => Task.CompletedTask;

    /// <summary>
    /// Verarbeitet einen abgeschlossenen Trade: ATI-Lernen + Risiko-Update + Desktop-Notification.
    /// Subklassen sollten diese Methode aufrufen statt _riskManager.UpdateDailyStats direkt.
    /// </summary>
    protected void ProcessCompletedTrade(CompletedTrade trade)
    {
        _riskManager?.UpdateDailyStats(trade);

        // ATI-Lernen: NUR bei finalem Close, NICHT bei Partial Close (TP1/TP2).
        // Partial Close entfernt sonst den OpenTrade-Kontext → finale Close-Phase lernt nicht.
        // Partial Closes haben "Partial Close" im Reason-Feld.
        var isPartialClose = trade.Reason != null &&
            trade.Reason.Contains("Partial", StringComparison.OrdinalIgnoreCase);
        if (!isPartialClose)
        {
            if (_ati != null)
            {
                _ati.ProcessTradeOutcome(trade, AtiSourceId);
                // Diagnostik: ATI-Counter nach Trade-Verarbeitung loggen
                if (_eventBus.HasLogSubscribers)
                {
                    var stats = _ati.ConfidenceGate.GetStatistics();
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "ATI",
                        $"{LogPrefix}Trade gelernt: {trade.Symbol} {(trade.Pnl > 0 ? "Win" : "Loss")} → {stats.TotalTrades}/{_ati.MinTradesBeforeLearning} Trades",
                        trade.Symbol));
                }
            }
        }

        // Consecutive-Losses tracken (für Leverage-Reduktion, kein Cooldown-Pause)
        if (trade.Pnl < 0)
        {
            var losses = Interlocked.Increment(ref _consecutiveLosses);
            if (losses >= 3)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Risk",
                    $"{LogPrefix}{losses} Verluste in Folge → Leverage wird reduziert"));
            }
        }
        else
        {
            Interlocked.Exchange(ref _consecutiveLosses, 0);
        }

        // Equity-Curve-Trading: Equity-Historie aktualisieren
        if (_riskSettings.EnableEquityCurveTrading && _riskManager != null)
        {
            lock (_equityLock)
                _equityHistory.Add(_riskManager.TotalPnl);
        }

        // Desktop-Notification senden
        if (_botSettings.EnableDesktopNotifications)
        {
            var direction = trade.Pnl >= 0 ? "Gewinn" : "Verlust";
            _eventBus.PublishNotification(
                $"{LogPrefix}{trade.Symbol} geschlossen",
                $"{direction}: {trade.Pnl:F2} USDT ({trade.Side}, {trade.EntryPrice:F4} → {trade.ExitPrice:F4})");
        }
    }

    /// <summary>Prüft ob die Equity-Curve unter ihrer EMA liegt (Position-Scaling reduzieren).</summary>
    protected decimal GetEquityCurveScaleFactor()
    {
        lock (_equityLock)
        {
            if (!_riskSettings.EnableEquityCurveTrading || _equityHistory.Count < _riskSettings.EquityCurvePeriod)
                return 1.0m;

            // EMA der Equity-Kurve berechnen
            var period = _riskSettings.EquityCurvePeriod;
            var multiplier = 2m / (period + 1);
            var ema = _equityHistory[^period];
            for (int i = _equityHistory.Count - period + 1; i < _equityHistory.Count; i++)
                ema = (_equityHistory[i] - ema) * multiplier + ema;

            var currentEquity = _equityHistory[^1];
            // Wenn Equity unter EMA → halbe Position
            return currentEquity < ema ? 0.5m : 1.0m;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Cross-Market Features (BTC-Kontext für ATI)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Berechnet BTC-Kontext und Markt-Stimmung und setzt sie in der FeatureEngine.
    /// Wird pro Scan-Zyklus aufgerufen (kostet keine zusätzlichen API-Calls).
    /// </summary>
    private async Task UpdateCrossMarketFeaturesAsync(
        IReadOnlyList<Ticker> allTickers,
        List<Ticker> candidates,
        ConcurrentDictionary<string, List<Candle>> klineResults,
        CancellationToken ct)
    {
        // BTC-Ticker finden (BTC-USDT)
        var btcTicker = allTickers.FirstOrDefault(t =>
            t.Symbol.Equals("BTC-USDT", StringComparison.OrdinalIgnoreCase));

        var btcReturn24h = 0f;
        var btcTrend = 0f;

        if (btcTicker != null)
        {
            // BTC 24h-Return normalisiert
            btcReturn24h = (float)(btcTicker.PriceChangePercent24h / 100m);

            // BTC-Trend aus HTF-Klines (wenn verfügbar)
            btcTrend = IndicatorHelper.GetHigherTimeframeTrend(null);
        }

        // Markt-Stimmung: Durchschnittlicher 24h-Return der Top-20 Coins nach Volumen
        var top20 = allTickers
            .Where(t => t.Volume24h > 0)
            .OrderByDescending(t => t.Volume24h)
            .Take(20);

        var sentimentSum = 0f;
        var sentimentCount = 0;
        foreach (var t in top20)
        {
            sentimentSum += (float)t.PriceChangePercent24h;
            sentimentCount++;
        }
        var marketSentiment = sentimentCount > 0 ? sentimentSum / sentimentCount / 100f : 0f;

        // Fear & Greed Index: Alle 15 Min aktualisieren (alternative.me API, gratis)
        if ((DateTime.UtcNow - _lastFearGreedFetch).TotalMinutes >= 15)
        {
            try
            {
                var json = await _fearGreedClient.GetStringAsync("https://api.alternative.me/fng/?limit=1", ct).ConfigureAwait(false);
                // Einfaches Parsing: {"data":[{"value":"42",...}]}
                var startIdx = json.IndexOf("\"value\":\"", StringComparison.Ordinal);
                if (startIdx >= 0)
                {
                    startIdx += 9;
                    var endIdx = json.IndexOf('"', startIdx);
                    if (endIdx > startIdx && int.TryParse(json[startIdx..endIdx], out var fng))
                    {
                        _cachedFearGreedIndex = fng / 100f; // Normalisiert auf [0, 1]
                        _lastFearGreedFetch = DateTime.UtcNow;

                        var fngLabel = fng switch { < 25 => "Extreme Fear", < 45 => "Fear", < 55 => "Neutral", < 75 => "Greed", _ => "Extreme Greed" };
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Market",
                            $"{LogPrefix}Fear & Greed Index: {fng}/100 ({fngLabel})"));
                    }
                }
            }
            catch
            {
                // API nicht erreichbar → letzten Wert behalten
            }
        }

        FeatureEngine.SetCrossMarketData(btcReturn24h, btcTrend, marketSentiment, _cachedFearGreedIndex);

        // BTC-Korrelation pro Kandidat berechnen (aus bereits geladenen Klines)
        if (klineResults.TryGetValue("BTC-USDT", out var btcCandles) && btcCandles.Count > 20)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Symbol.Equals("BTC-USDT", StringComparison.OrdinalIgnoreCase)) continue;
                if (!klineResults.TryGetValue(candidate.Symbol, out var altCandles) || altCandles.Count < 20)
                    continue;

                // Einfache Pearson-Korrelation auf Log-Returns der letzten 20 Perioden
                var correlation = CalculateSimpleCorrelation(btcCandles, altCandles, 20);
                FeatureEngine.SetBtcCorrelation(candidate.Symbol, correlation);
            }
        }

        // Open Interest Change pro Kandidat berechnen
        if (_publicClient is BingXBot.Exchange.BingXPublicClient publicClient)
        {
            foreach (var candidate in candidates)
            {
                try
                {
                    var oi = await publicClient.GetOpenInterestAsync(candidate.Symbol, ct).ConfigureAwait(false);
                    if (oi > 0)
                    {
                        var oiChange = 0f;
                        if (_previousOpenInterest.TryGetValue(candidate.Symbol, out var prevOi) && prevOi > 0)
                        {
                            oiChange = (float)((oi - prevOi) / prevOi); // Normalisierter Change
                            oiChange = Math.Clamp(oiChange, -1f, 1f);
                        }
                        _previousOpenInterest[candidate.Symbol] = oi;
                        FeatureEngine.SetOpenInterestChange(candidate.Symbol, oiChange);

                        if (Math.Abs(oiChange) > 0.03f) // Nur bei signifikanter Änderung loggen (>3%)
                        {
                            var direction = oiChange > 0 ? "steigend" : "fallend";
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Market",
                                $"{LogPrefix}{candidate.Symbol}: OI {direction} ({oiChange:+0.0%;-0.0%})", candidate.Symbol));
                        }
                    }
                }
                catch { /* OI nicht verfügbar → 0 */ }
            }
        }
    }

    /// <summary>Berechnet Pearson-Korrelation auf Log-Returns der letzten N Candles.</summary>
    private static float CalculateSimpleCorrelation(List<Candle> series1, List<Candle> series2, int periods)
    {
        var n = Math.Min(Math.Min(series1.Count, series2.Count), periods + 1);
        if (n < 5) return 0f;

        var returns1 = new float[n - 1];
        var returns2 = new float[n - 1];

        for (int i = 1; i < n; i++)
        {
            var idx1 = series1.Count - n + i;
            var idx2 = series2.Count - n + i;
            if (idx1 < 1 || idx2 < 1) continue;

            returns1[i - 1] = series1[idx1].Close > 0 && series1[idx1 - 1].Close > 0
                ? (float)Math.Log((double)(series1[idx1].Close / series1[idx1 - 1].Close))
                : 0f;
            returns2[i - 1] = series2[idx2].Close > 0 && series2[idx2 - 1].Close > 0
                ? (float)Math.Log((double)(series2[idx2].Close / series2[idx2 - 1].Close))
                : 0f;
        }

        // Pearson-Korrelation
        var mean1 = returns1.Average();
        var mean2 = returns2.Average();
        var cov = 0f;
        var var1 = 0f;
        var var2 = 0f;
        for (int i = 0; i < returns1.Length; i++)
        {
            var d1 = returns1[i] - mean1;
            var d2 = returns2[i] - mean2;
            cov += d1 * d2;
            var1 += d1 * d1;
            var2 += d2 * d2;
        }
        var denom = MathF.Sqrt(var1 * var2);
        return denom > 0 ? cov / denom : 0f;
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

    /// <summary>Hook: Wird aufgerufen wenn Auto-Breakeven gesetzt wird. LiveTradingService aktualisiert den nativen SL auf BingX.</summary>
    protected virtual Task OnBreakevenSetAsync(string symbol, Side side, decimal breakevenPrice) => Task.CompletedTask;

    /// <summary>Hook: Zusätzliche Dispose-Logik für Subklassen (z.B. WebSocket-Cleanup).</summary>
    protected virtual void DisposeAdditional() { }
}
