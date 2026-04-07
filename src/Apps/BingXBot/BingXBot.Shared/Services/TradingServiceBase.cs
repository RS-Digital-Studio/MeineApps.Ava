using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
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

    // Aktuelle Funding-Rate (wird von Subklassen aktualisiert, z.B. aus BingX API)
    protected decimal _currentFundingRate;
    // Margin-Monitoring: Bereits gewarnte Positionen (nicht bei jedem Tick erneut warnen)
    private readonly ConcurrentDictionary<string, DateTime> _marginWarningsIssued = new();

    // SL/TP-Tracking: Speichert das Original-Signal pro offener Position (Symbol_Side → SignalResult)
    // ConcurrentDictionary weil PriceTickerLoop und ScanAndTradeAsync parallel darauf zugreifen
    protected readonly ConcurrentDictionary<string, SignalResult> _positionSignals = new();
    // Trailing-Stop: Höchst-/Tiefstpreis seit Eröffnung pro Position (Symbol_Side → Preis)
    protected readonly ConcurrentDictionary<string, decimal> _extremePriceSinceEntry = new();
    // Trailing-Stop pro Position: ATI kann pro Trade einen optimierten Trailing-Prozentsatz setzen
    protected readonly ConcurrentDictionary<string, decimal> _positionTrailingPercent = new();
    // Wiederverwendbares Dictionary für Ticker-Preise (ConcurrentDictionary für Thread-Safety
    // da PriceTickerLoop und RunLoopAsync parallel laufen)
    private readonly ConcurrentDictionary<string, decimal> _tickerPriceMap = new();

    // Multi-Stage Exit: Vollständiger Positions-Zustand (ersetzt teilweise _positionSignals für Exit-Logik)
    protected readonly ConcurrentDictionary<string, PositionExitState> _exitStates = new();
    // Cooldown-Eskalation: Verlust-Tracking
    protected DateTime? _lastLossTime;
    protected int _consecutiveLosses;
    // Täglicher Trade-Counter (wird bei Tageswechsel zurückgesetzt)
    protected int _tradesToday;
    // Equity-Curve-Trading: Equity-Historie für EMA-Berechnung
    // Lock nötig: Add() aus ProcessCompletedTrade, Lesen aus GetEquityCurveScaleFactor (verschiedene Loops)
    private readonly List<decimal> _equityHistory = new();
    private readonly object _equityLock = new();
    // Semaphore für paralleles Klines-Laden (max 5 gleichzeitige Requests)
    private readonly SemaphoreSlim _klineSemaphore = new(5);

    // ATI: Adaptive Trading Intelligence (optional, kann aktiviert/deaktiviert werden)
    protected AdaptiveTradingIntelligence? _ati;
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

        _eventBus.PublishBotState(BotState.Paused);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
            $"{ModeName} pausiert"));
    }

    /// <summary>Setzt den Trading-Service nach Pause fort.</summary>
    public void Resume()
    {
        if (!_isRunning || !_isPaused) return;
        _isPaused = false;

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

    /// <summary>Stellt ein Signal für eine offene Position wieder her (z.B. nach App-Neustart aus BingX-Orders).</summary>
    public void RestorePositionSignal(string symbol, Side side, SignalResult signal)
    {
        var key = $"{symbol}_{side}";
        _positionSignals[key] = signal;
        if (!_exitStates.ContainsKey(key))
        {
            var entry = signal.EntryPrice ?? 0m;
            // BreakevenSet nur true wenn der SL bereits auf/über Entry liegt (= BE war schon gesetzt)
            var slAlreadyAtBe = signal.StopLoss.HasValue && entry > 0 && (
                (side == Side.Buy && signal.StopLoss.Value >= entry) ||
                (side == Side.Sell && signal.StopLoss.Value <= entry));

            _exitStates[key] = new PositionExitState
            {
                Signal = signal, Symbol = symbol, Side = side,
                EntryPrice = entry,
                BreakevenSet = slAlreadyAtBe
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
                // Tageswechsel: Daily-Drawdown zurücksetzen
                var today = DateTime.UtcNow.Date;
                if (today != _lastDailyResetDate)
                {
                    _riskManager?.ResetDailyStats();
                    _lastDailyResetDate = today;
                    Interlocked.Exchange(ref _tradesToday, 0);
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                        $"{LogPrefix}Tages-Drawdown + Trade-Counter zurückgesetzt (neuer Tag)"));
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
                // TryClaimAutoSave ist atomar: Im Multi-Mode gewinnt nur ein Service pro Intervall
                if (_ati is { IsEnabled: true } && _ati.TryClaimAutoSave(_botSettings.AtiAutoSaveIntervalMinutes))
                {
                    await OnAtiAutoSaveAsync().ConfigureAwait(false);
                }

                // Regime-Warnung: Bei Chaotic-Regime WARNEN, nicht automatisch schließen.
                // SL schützt die Positionen. Automatisches Schließen bei Chaotic führt zu
                // unnötigen Verlusten weil Krypto-Volatilität oft als "Chaotic" erkannt wird.
                if (_ati is { IsEnabled: true } && positions.Count > 0)
                {
                    var regime = _ati.RegimeDetector.CurrentRegime;
                    if (regime == Core.Models.ATI.MarketRegime.Chaotic)
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

                    // ═══ Auto-Breakeven: SL auf Entry wenn Gewinn% >= Leverage% ═══
                    // Funktioniert auch OHNE Signal (z.B. nach App-Neustart bevor Recovery gelaufen ist)
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
                                var beSl = pos.Side == Side.Buy
                                    ? pos.EntryPrice * 1.001m
                                    : pos.EntryPrice * 0.999m;

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
                                // TP1 erreicht: 50% Position schließen
                                var closeQty = exitState.OriginalQuantity * _riskSettings.Tp1CloseRatio;
                                await OnPartialCloseAsync(pos, price, closeQty).ConfigureAwait(false);
                                exitState.PartialClosed = true;

                                // Smart Breakeven: SL = Entry + ATR-Puffer statt exakter Entry
                                var beSl = exitState.EntryPrice;
                                if (_riskSettings.SmartBreakevenAtrMultiplier > 0 && exitState.CurrentAtr > 0)
                                {
                                    beSl = pos.Side == Side.Buy
                                        ? exitState.EntryPrice + exitState.CurrentAtr * _riskSettings.SmartBreakevenAtrMultiplier
                                        : exitState.EntryPrice - exitState.CurrentAtr * _riskSettings.SmartBreakevenAtrMultiplier;
                                }
                                _positionSignals[key] = signal with { StopLoss = beSl, TakeProfit = exitState.Tp2 };
                                exitState.Signal = _positionSignals[key];
                                exitState.Phase = ExitPhase.Tp1Hit;
                                exitState.MaxHoldHours = _riskSettings.MaxHoldHoursAfterTp1;

                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: TP1 erreicht → {_riskSettings.Tp1CloseRatio:P0} geschlossen, SL→BE ({beSl:F8})",
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
                                // TP2 erreicht: Tp2CloseRatio der verbleibenden Position schließen
                                var remainingQty = pos.Quantity;
                                var tp2CloseQty = Math.Round(remainingQty * (_riskSettings.Tp2CloseRatio / (1m - _riskSettings.Tp1CloseRatio)), 6);
                                tp2CloseQty = Math.Min(tp2CloseQty, remainingQty);

                                if (tp2CloseQty > 0 && tp2CloseQty < remainingQty)
                                {
                                    await OnPartialCloseAsync(pos, price, tp2CloseQty).ConfigureAwait(false);
                                    exitState.Tp2Closed = true;
                                    exitState.Phase = ExitPhase.Trailing;

                                    // Rest läuft nur noch mit Chandelier-Trailing (kein TP mehr)
                                    _positionSignals[key] = signal with { TakeProfit = null };
                                    exitState.Signal = _positionSignals[key];

                                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                        $"{LogPrefix}{pos.Symbol}: TP2 erreicht → {_riskSettings.Tp2CloseRatio:P0} geschlossen, Rest Chandelier-Trailing",
                                        pos.Symbol));
                                    continue;
                                }
                                else
                                {
                                    // Zu wenig übrig → komplett schließen
                                    reason = $"TP2 bei {signal.TakeProfit.Value:F8} (Rest zu klein für Partial)";
                                    hit = true;
                                    isStopLoss = false;
                                }
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
                    if (!hit && _riskSettings.EnableTrailingStop && signal.StopLoss.HasValue)
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

                        if (pos.Side == Side.Buy)
                        {
                            var prev = _extremePriceSinceEntry.GetValueOrDefault(key, pos.EntryPrice);
                            if (price > prev) _extremePriceSinceEntry[key] = price;
                            var highest = _extremePriceSinceEntry.GetValueOrDefault(key, price);
                            var newSl = highest - trailDistance;
                            if (newSl > signal.StopLoss.Value && newSl < price)
                            {
                                var oldSl = signal.StopLoss.Value;
                                var updated = _positionSignals.AddOrUpdate(key,
                                    signal with { StopLoss = newSl },
                                    (_, current) => current.StopLoss.HasValue && newSl > current.StopLoss.Value && newSl < price
                                        ? current with { StopLoss = newSl }
                                        : current);
                                if (updated.StopLoss.HasValue && updated.StopLoss.Value == newSl)
                                    OnTrailingStopMoved(pos.Symbol, oldSl, newSl);
                            }
                        }
                        else
                        {
                            var prev = _extremePriceSinceEntry.GetValueOrDefault(key, pos.EntryPrice);
                            if (price < prev) _extremePriceSinceEntry[key] = price;
                            var lowest = _extremePriceSinceEntry.GetValueOrDefault(key, price);
                            var newSl = lowest + trailDistance;
                            if (newSl < signal.StopLoss.Value && newSl > price)
                            {
                                var oldSl = signal.StopLoss.Value;
                                var updated = _positionSignals.AddOrUpdate(key,
                                    signal with { StopLoss = newSl },
                                    (_, current) => current.StopLoss.HasValue && newSl < current.StopLoss.Value && newSl > price
                                        ? current with { StopLoss = newSl }
                                        : current);
                                if (updated.StopLoss.HasValue && updated.StopLoss.Value == newSl)
                                    OnTrailingStopMoved(pos.Symbol, oldSl, newSl);
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
                            var atrThreshold = mdState.CurrentAtr * 1.5m;

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
                        // Cooldown: Verlust-Trade merken
                        if (isStopLoss) _lastLossTime = DateTime.UtcNow;
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
        var sessionFilter = MarketFilter.CheckSession(DateTime.UtcNow, _botSettings.LastTradingModePreset);
        if (!sessionFilter.IsAllowed)
        {
            PublishScanSummary($"Session-Filter: {sessionFilter.SessionInfo}", nextScanText);
            IndicatorHelper.ClearCache();
            return;
        }

        // Cooldown deaktiviert - der SL schützt die Positionen, kein Grund für Handelspausen

        // MaxTradesPerDay nur als Statistik, nicht als Filter (MaxOpenPositions begrenzt gleichzeitige Trades)
        var tradesToday = Volatile.Read(ref _tradesToday);

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
                var candles = await _publicClient.GetKlinesAsync(
                    ticker.Symbol, _scannerSettings.ScanTimeFrame,
                    now.AddHours(-100), now, ct).ConfigureAwait(false);

                List<Candle>? htfCandles = null;
                try
                {
                    htfCandles = await _publicClient.GetKlinesAsync(
                        ticker.Symbol, Core.Enums.TimeFrame.H4,
                        now.AddDays(-14), now, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* HTF optional */ }

                // M15-Candles für Entry-Timing (bei H4/H1 Strategien)
                List<Candle>? m15Candles = null;
                if (_scannerSettings.UseM15EntryTiming &&
                    _scannerSettings.ScanTimeFrame is TimeFrame.H4 or TimeFrame.H1 or TimeFrame.H2)
                {
                    try
                    {
                        m15Candles = await _publicClient.GetKlinesAsync(
                            ticker.Symbol, Core.Enums.TimeFrame.M15,
                            now.AddHours(-12), now, ct).ConfigureAwait(false);
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

            // Vorgeladene Klines verwenden
            if (!klineResults.TryGetValue(ticker.Symbol, out var candles) || candles.Count < 50)
                continue;
            htfResults.TryGetValue(ticker.Symbol, out var htfCandles);
            m15Results.TryGetValue(ticker.Symbol, out var m15Candles);

            try
            {
                // ATI-Pipeline (wenn aktiviert): Alle Strategien als Ensemble evaluieren
                if (_ati is { IsEnabled: true })
                {
                    SetCurrentPriceIfNeeded(ticker.Symbol, ticker.LastPrice);

                    var context = new MarketContext(ticker.Symbol, candles, ticker, positions, account, htfCandles);
                    var atiResult = _ati.EvaluateCandidate(context);
                    if (atiResult == null) continue;

                    var signal = atiResult.Signal;

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "ATI",
                        $"{LogPrefix}{ticker.Symbol}: {signal.Signal} ({signal.Reason})", ticker.Symbol));

                    // Close-Signale verarbeiten (Position schließen, kein neuer Trade)
                    if (signal.Signal is Signal.CloseLong or Signal.CloseShort)
                    {
                        var closeSide = signal.Signal == Signal.CloseLong ? Side.Buy : Side.Sell;
                        if (positions.Any(p => p.Symbol == ticker.Symbol && p.Side == closeSide))
                        {
                            await ClosePositionAndPublishAsync(ticker.Symbol, closeSide).ConfigureAwait(false);
                            positions = await GetPositionsForScanAsync().ConfigureAwait(false);
                        }
                        continue;
                    }

                    // Korrelations-Check (auch mit ATI noch relevant)
                    if (await ScanHelper.CheckCorrelationAsync(
                        ticker.Symbol, positions, _riskSettings, _publicClient, candles, _eventBus, LogPrefix, ct))
                        continue;

                    // Adaptiver Leverage: Eingestellter MaxLeverage als Basis, leicht reduziert bei Volatilität/schwachem Signal
                    var atrForLev = IndicatorHelper.CalculateAtr(candles);
                    var atrPctLev = atrForLev.Count > 0 && atrForLev[^1].HasValue && ticker.LastPrice > 0
                        ? (int)(atrForLev[^1]!.Value / ticker.LastPrice * 100 * 100) : 50;
                    var isBtcSymbol = ticker.Symbol.StartsWith("BTC", StringComparison.OrdinalIgnoreCase);
                    var adaptLev = CryptoTrendProStrategy.GetAdaptiveLeverage(atrPctLev, signal.ConfluenceScore, isBtcSymbol, (int)_riskSettings.MaxLeverage);
                    if (_riskSettings.EnableCooldownEscalation && _consecutiveLosses >= 3)
                        adaptLev = Math.Max(1, adaptLev - 1); // Bei Verlusten nur -1 statt halbieren

                    // Risk-Check mit tatsächlichem Leverage (für korrekte Margin-Berechnung)
                    var riskCheck = _riskManager!.ValidateTrade(signal, context, _currentFundingRate, adaptLev);
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

                    // Order platzieren (Signal für native SL/TP mitgeben)
                    var placed = await PlaceOrderOnExchangeAsync(ticker, side, positionSize, signal, adaptLev).ConfigureAwait(false);
                    if (!placed) continue;

                    // Signal speichern + Multi-Stage Exit State erstellen
                    var slTpKey = $"{ticker.Symbol}_{side}";
                    _positionSignals[slTpKey] = signal;
                    _extremePriceSinceEntry[slTpKey] = ticker.LastPrice;
                    _positionTrailingPercent[slTpKey] = atiResult.TrailingStopPercent;

                    var atrVal = IndicatorHelper.CalculateAtr(candles);
                    var atrLast = atrVal.Count > 0 && atrVal[^1].HasValue ? atrVal[^1]!.Value : 0m;

                    _exitStates[slTpKey] = new PositionExitState
                    {
                        Signal = signal, Symbol = ticker.Symbol, Side = side,
                        EntryPrice = ticker.LastPrice, OriginalQuantity = riskCheck.AdjustedPositionSize,
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
                    var context = new MarketContext(ticker.Symbol, candles, ticker, positions, account, htfCandles);
                    var signal = strategy.Evaluate(context);

                    if (signal.Signal == Signal.None) continue;

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Scanner",
                        $"{LogPrefix}{ticker.Symbol}: {signal.Signal} Signal (Confidence: {signal.Confidence:P0}) - {signal.Reason}",
                        ticker.Symbol));

                    // Close-Signale verarbeiten
                    if (signal.Signal is Signal.CloseLong or Signal.CloseShort)
                    {
                        var closeSide = signal.Signal == Signal.CloseLong ? Side.Buy : Side.Sell;
                        if (positions.Any(p => p.Symbol == ticker.Symbol && p.Side == closeSide))
                        {
                            await ClosePositionAndPublishAsync(ticker.Symbol, closeSide).ConfigureAwait(false);
                            // H-7 Fix: Positions-Liste aktualisieren (wie im ATI-Pfad)
                            positions = await GetPositionsForScanAsync().ConfigureAwait(false);
                        }
                        continue;
                    }

                    // Korrelations-Check
                    if (await ScanHelper.CheckCorrelationAsync(
                        ticker.Symbol, positions, _riskSettings, _publicClient, candles, _eventBus, LogPrefix, ct))
                        continue;

                    // Adaptiver Leverage: Eingestellter MaxLeverage als Basis, leicht reduziert bei Volatilität/schwachem Signal
                    var atrForLevStd = IndicatorHelper.CalculateAtr(candles);
                    var atrPctStd = atrForLevStd.Count > 0 && atrForLevStd[^1].HasValue && ticker.LastPrice > 0
                        ? (int)(atrForLevStd[^1]!.Value / ticker.LastPrice * 100 * 100) : 50;
                    var isBtcStd = ticker.Symbol.StartsWith("BTC", StringComparison.OrdinalIgnoreCase);
                    var adaptLevStd = CryptoTrendProStrategy.GetAdaptiveLeverage(atrPctStd, signal.ConfluenceScore, isBtcStd, (int)_riskSettings.MaxLeverage);
                    if (_riskSettings.EnableCooldownEscalation && _consecutiveLosses >= 3)
                        adaptLevStd = Math.Max(1, adaptLevStd - 1);

                    // Risk-Check mit tatsächlichem Leverage
                    var riskCheck = _riskManager!.ValidateTrade(signal, context, null, adaptLevStd);
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
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    if (!CheckM15EntryTiming(m15Candles, side, ticker.Symbol))
                        continue;

                    // Order platzieren
                    var placed = await PlaceOrderOnExchangeAsync(ticker, side, positionSizeStd, signal, adaptLevStd).ConfigureAwait(false);
                    if (!placed) continue;

                    // SL/TP-Signal speichern + Multi-Stage Exit State
                    var slTpKey = $"{ticker.Symbol}_{side}";
                    _positionSignals[slTpKey] = signal;
                    _extremePriceSinceEntry[slTpKey] = ticker.LastPrice;

                    var atrForExit = IndicatorHelper.CalculateAtr(candles);
                    var atrLastVal = atrForExit.Count > 0 && atrForExit[^1].HasValue ? atrForExit[^1]!.Value : 0m;
                    _exitStates[slTpKey] = new PositionExitState
                    {
                        Signal = signal, Symbol = ticker.Symbol, Side = side,
                        EntryPrice = ticker.LastPrice, OriginalQuantity = riskCheck.AdjustedPositionSize,
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

        // ATI: Debug-Zusammenfassung loggen
        if (_ati is { IsEnabled: true } && _eventBus.HasLogSubscribers)
        {
            var summary = _ati.GetScanSummaryAndReset();
            if (summary != null)
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "ATI", summary));
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
        var regimeText = _ati is { IsEnabled: true } ? _ati.RegimeDetector.CurrentRegime.ToString() : "n/a";
        var posCount = positions.Count;
        var scanSummary = $"{candidates.Count} Kandidaten geprüft | Regime: {regimeText} | " +
            $"Positionen: {posCount}/{_riskSettings.MaxOpenPositions} | " +
            $"{elapsed:F1}s | Nächster: {nextScanFinal:HH:mm:ss}";
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

    /// <summary>Hook: Trailing-Stop wurde nachgezogen (für Logging in Live).</summary>
    protected virtual void OnTrailingStopMoved(string symbol, decimal oldSl, decimal newSl) { }

    /// <summary>Hook: ATI Auto-Save (Subklassen implementieren DB-Zugriff).</summary>
    protected virtual Task OnAtiAutoSaveAsync() => Task.CompletedTask;

    /// <summary>
    /// Verarbeitet einen abgeschlossenen Trade: ATI-Lernen + Risiko-Update + Desktop-Notification.
    /// Subklassen sollten diese Methode aufrufen statt _riskManager.UpdateDailyStats direkt.
    /// </summary>
    protected void ProcessCompletedTrade(CompletedTrade trade)
    {
        _riskManager?.UpdateDailyStats(trade);
        _ati?.ProcessTradeOutcome(trade, AtiSourceId);

        // Cooldown-Eskalation: Consecutive-Losses tracken
        if (trade.Pnl < 0)
        {
            _consecutiveLosses++;
            _lastLossTime = DateTime.UtcNow;

            if (_riskSettings.EnableCooldownEscalation && _consecutiveLosses >= 3)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Risk",
                    $"{LogPrefix}{_consecutiveLosses} Verluste in Folge → Cooldown eskaliert auf {GetEscalatedCooldownHours()}h"));
            }
        }
        else
        {
            _consecutiveLosses = 0;
            _lastLossTime = null; // Cooldown aufheben bei Gewinn-Trade
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

    /// <summary>Berechnet den eskalierten Cooldown basierend auf aufeinanderfolgenden Verlusten.</summary>
    protected int GetEscalatedCooldownHours()
    {
        if (!_riskSettings.EnableCooldownEscalation || _consecutiveLosses <= 1)
            return _riskSettings.CooldownHours;

        // Eskalation: Base * 2^(losses-1), gecapped auf MaxCooldownHours
        var escalated = _riskSettings.CooldownHours * (int)Math.Pow(2, Math.Min(_consecutiveLosses - 1, 3));
        return Math.Min(escalated, _riskSettings.MaxCooldownHours);
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
