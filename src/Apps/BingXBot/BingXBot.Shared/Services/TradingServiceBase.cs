using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Filters;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using System.Collections.Concurrent;

namespace BingXBot.Services;

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
    // Wiederverwendbares Dictionary für Ticker-Preise (ConcurrentDictionary für Thread-Safety
    // da PriceTickerLoop und RunLoopAsync parallel laufen)
    private readonly ConcurrentDictionary<string, decimal> _tickerPriceMap = new();

    // Positions-Zustand (SL/TP-Tracking, BE-Status)
    protected readonly ConcurrentDictionary<string, PositionExitState> _exitStates = new();
    // Verlust-Tracking
    protected volatile int _consecutiveLosses;
    // Täglicher Trade-Counter (wird bei Tageswechsel zurückgesetzt)
    protected volatile int _tradesToday;
    // Semaphore für paralleles Klines-Laden (max 10 gleichzeitige Requests)
    private readonly SemaphoreSlim _klineSemaphore = new(10);

    // Bot-Einstellungen (für Notifications etc.)
    protected readonly BotSettings _botSettings;

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
        // Original-Signal gehen TakeProfit, TakeProfit2, DisableSmartBreakeven (SK-BE Workflow 4.1/4.2)
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
                IsRecovered = true, // Echte Haltezeit unbekannt — Time-Exit mit Karenz
                MaxHoldHours = 0    // Deaktiviert bis Karenz-Periode abgelaufen
            };
        }
        else
        {
            // ExitState existiert: Signal-Referenz aktualisieren damit neue SL/TP-Werte greifen
            existingState!.Signal = signal;
        }
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
    // PriceTicker-Loop: Alle 5 Sekunden SL/TP prüfen + BE-Regel (Workflow 4.1/4.2)
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

                    // TradFi bei geschlossenem Markt: Nur Margin-Monitoring, kein SL/TP-Trigger
                    // (Preise sind stale, SL/TP-Trigger auf letztem Kurs wäre falsch)
                    if (!TradingHoursFilter.IsMarketOpen(pos.Symbol, DateTime.UtcNow))
                        continue;

                    if (!_positionSignals.TryGetValue(key, out var signal)) continue;

                    var hit = false;
                    var isStopLoss = false;
                    string reason = "";

                    // ═══ SK-Buch-BE-Regel (Cheat 53, Workflow 4.1-4.3, S.18) ═══
                    // Workflow 4.1: SL halbieren wenn Trade ≥ 1× SL-Distanz im Gewinn (Risiko reduzieren)
                    // Workflow 4.2: BE setzen bei entgegengesetztem KL vor doppeltem Profit (Proxy: 2× SL-Distanz)
                    // Workflow 4.3: SL wird NICHT nachgezogen (außer auf BE) — Trade läuft BE oder ins Ziel
                    // S.18: BE = Entry + Spread (0.15%-Buffer als Krypto-Spread-Proxy: ~0.08% Fee + Slippage)
                    if (signal.DisableSmartBreakeven && signal.StopLoss.HasValue
                        && _exitStates.TryGetValue(key, out var skState) && skState.EntryPrice > 0)
                    {
                        var slDistance = Math.Abs(skState.EntryPrice - signal.StopLoss.Value);
                        var currentProfit = pos.Side == Side.Buy
                            ? price - skState.EntryPrice
                            : skState.EntryPrice - price;

                        // Workflow 4.1: SL halbieren bei 1× SL-Distanz im Gewinn (vor BE bei 2×)
                        if (slDistance > 0 && currentProfit >= slDistance * 1m
                                 && currentProfit < slDistance * 2m
                                 && !skState.BreakevenSet && !skState.SlHalved)
                        {
                            var halvedSl = pos.Side == Side.Buy
                                ? skState.EntryPrice - slDistance * 0.5m
                                : skState.EntryPrice + slDistance * 0.5m;
                            _positionSignals[key] = signal with { StopLoss = halvedSl };
                            skState.SlHalved = true;
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                $"{LogPrefix}{pos.Symbol}: SK SL halbiert ({halvedSl:F8}) — Workflow 4.1 (Gewinn >= 1× SL)",
                                pos.Symbol));
                            // Nativer SL auf BingX synchronisieren (sonst geht der bessere SL bei App-Crash verloren)
                            await OnStopLossAdjustedAsync(pos.Symbol, pos.Side, halvedSl).ConfigureAwait(false);
                        }
                        // Workflow 4.2: BE einmal bei 2× SL-Distanz (doppelter Profit) — danach nicht mehr nachziehen (4.3)
                        else if (slDistance > 0 && currentProfit >= slDistance * 2m && !skState.BreakevenSet)
                        {
                            // BE = Entry + Spread (S.18). 0.15% = grober Proxy (Fees + Slippage bei BingX Krypto).
                            var beSl = pos.Side == Side.Buy
                                ? skState.EntryPrice * 1.0015m
                                : skState.EntryPrice * 0.9985m;
                            _positionSignals[key] = signal with { StopLoss = beSl };
                            skState.BreakevenSet = true;
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                $"{LogPrefix}{pos.Symbol}: SK Breakeven ({beSl:F8}) — Workflow 4.2 (Entry+Spread bei 2× SL)",
                                pos.Symbol));
                            await OnStopLossAdjustedAsync(pos.Symbol, pos.Side, beSl).ConfigureAwait(false);
                        }
                        // Workflow 4.3: KEIN weiteres Nachziehen nach BE — Trade läuft ins Ziel oder wird BE-ausgestoppt
                    }

                    // ═══ Multi-Stage Exit: TP1 (50%) bei 161.8%, TP2 (Rest) bei 200%+Buffer ═══
                    // Buch S.16 Zielbereich 161.8-200% — Partial Close 50/50 entspricht diesem Range
                    if (_exitStates.TryGetValue(key, out var exitState))
                    {
                        // Phase Initial: TP1 Partial Close (50% bei 161.8% Extension)
                        if (exitState.Phase == ExitPhase.Initial && signal.TakeProfit.HasValue && !exitState.PartialClosed
                            && _riskSettings.Tp1CloseRatio > 0 && _riskSettings.Tp1CloseRatio < 1m)
                        {
                            var tp1Hit = pos.Side == Side.Buy
                                ? price >= signal.TakeProfit.Value
                                : price <= signal.TakeProfit.Value;

                            if (tp1Hit)
                            {
                                // Wenn kein TP2 existiert → komplett schließen (nicht Rest ohne TP laufen lassen)
                                if (!exitState.Tp2.HasValue)
                                {
                                    reason = $"Take-Profit bei {signal.TakeProfit.Value:F8} (kein TP2 definiert, Full-Close)";
                                    hit = true;
                                }
                                else
                                {
                                    var closeQty = pos.Quantity * _riskSettings.Tp1CloseRatio;
                                    await OnPartialCloseAsync(pos, price, closeQty).ConfigureAwait(false);
                                    exitState.PartialClosed = true;
                                    // SL bleibt (Buch 4.3: kein Nachziehen außer BE). TP auf TP2 verschieben.
                                    _positionSignals[key] = signal with { TakeProfit = exitState.Tp2 };
                                    exitState.Signal = _positionSignals[key];
                                    exitState.Phase = ExitPhase.Tp1Hit;

                                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                        $"{LogPrefix}{pos.Symbol}: TP1 (161.8%) erreicht → {_riskSettings.Tp1CloseRatio:P0} geschlossen, Rest läuft bis 200%+Buffer",
                                        pos.Symbol));
                                    continue;
                                }
                            }
                        }

                        // Time-based Exit: Position zu lange offen (wenn aktiviert)
                        if (exitState.MaxHoldHours > 0)
                        {
                            var holdHours = (DateTime.UtcNow - exitState.EntryTime).TotalHours;
                            if (holdHours >= exitState.MaxHoldHours)
                            {
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
                    }

                    // ═══ Buch Workflow 6.1+6.2: Verlust-Ausgleichs-TP ═══
                    // "Wenn x Trades in SL und Möglichkeit besteht mit einem Trade die Verluste auszugleichen → TP!"
                    // "Gewinne sollten am gleichen Tag realisiert werden"
                    if (!hit && _riskManager != null && pos.EntryPrice > 0)
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
                                reason = $"Verlust-Ausgleichs-TP (Workflow 6.1): Gewinn {unrealizedPnl:F2}$ ≥ Tagesverluste {dailyLoss:F2}$";
                                hit = true;
                                isStopLoss = false;
                            }
                        }
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
        {
            var tradFiCount = candidates.Count(t => SymbolClassifier.IsTradFi(t.Symbol));
            var cryptoCount = candidates.Count - tradFiCount;
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Scanner",
                $"{LogPrefix}{candidates.Count} Kandidaten ({cryptoCount} Krypto + {tradFiCount} TradFi)"));
        }

        // 2. Globale MarketFilter prüfen (VOR dem teuren Klines-Loading)
        // Funding-Settlement blockiert ALLE BingX-Perpetuals (Krypto + TradFi haben Funding).
        var sessionFilter = MarketFilter.CheckSession(DateTime.UtcNow, _botSettings.LastTradingModePreset);
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

        // 2b. TradFi-Kandidaten mit geschlossenem Markt VOR dem teuren Klines-Loading entfernen.
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

        // 4. Klines für alle Kandidaten PARALLEL vorladen (statt sequenziell pro Kandidat)
        //    Begrenzt auf 5 parallele Requests um Rate-Limiter nicht zu überlasten
        var now = DateTime.UtcNow;
        var klineResults = new ConcurrentDictionary<string, List<Candle>>();
        var htfResults = new ConcurrentDictionary<string, List<Candle>?>();
        var m15Results = new ConcurrentDictionary<string, List<Candle>?>(); // M15 für Entry-Timing
        var dailyResults = new ConcurrentDictionary<string, List<Candle>?>(); // D1 für SK-Fahrplan
        var weeklyResults = new ConcurrentDictionary<string, List<Candle>?>(); // Weekly für SK-Fahrplan

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

                // SK-System (Buch S.15): H1 als Filter-Chart (nicht D1), M30 als Entry-Chart.
                // Da SK-System die einzige Strategie ist, immer H1/M30 laden.
                var htfTimeFrame = Core.Enums.TimeFrame.H1;

                List<Candle>? htfCandles = null;
                try
                {
                    htfCandles = await _publicClient.GetKlinesAsync(
                        ticker.Symbol, htfTimeFrame,
                        now.AddDays(-14), now, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* HTF optional */ }

                // Entry-Chart: M30 (SK-Buch S.15 Primär-Entry-Chart)
                // SK-Sequenzen dauern ~100+ Kerzen → 48h = nur ~96 M30-Kerzen (grenzwertig).
                // 120h (5 Tage) = ~240 Kerzen → genug für 2 vollständige M30-Sequenzen + GKL-Historie.
                List<Candle>? entryTfCandles = null;
                try
                {
                    entryTfCandles = await _publicClient.GetKlinesAsync(
                        ticker.Symbol, Core.Enums.TimeFrame.M30,
                        now.AddHours(-120), now, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* Entry-TF optional */ }

                // D1-Candles: Übergeordneter Fahrplan (BLASH, Daily-GKLs)
                // SK-Sequenzen dauern ~100+ Kerzen → 90 Tage reicht nicht für eine vollständige D1-Sequenz
                // + abgearbeitete GKLs. 365 Tage = ~365 Kerzen = 3-4 volle Sequenzzyklen + GKL-Historie.
                List<Candle>? dailyCandles = null;
                try
                {
                    dailyCandles = await _publicClient.GetKlinesAsync(
                        ticker.Symbol, Core.Enums.TimeFrame.D1,
                        now.AddDays(-365), now, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* Daily optional */ }

                // Weekly-Candles: Top-Level Fahrplan (Weekly-GKLs, ~2 Jahre Historie)
                List<Candle>? weeklyCandles = null;
                try
                {
                    weeklyCandles = await _publicClient.GetKlinesAsync(
                        ticker.Symbol, Core.Enums.TimeFrame.W1,
                        now.AddDays(-730), now, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch { /* Weekly optional */ }

                klineResults[ticker.Symbol] = candles;
                htfResults[ticker.Symbol] = htfCandles;
                m15Results[ticker.Symbol] = entryTfCandles;  // Name bleibt aus Kompatibilitätsgründen, enthält M30-Candles
                dailyResults[ticker.Symbol] = dailyCandles;
                weeklyResults[ticker.Symbol] = weeklyCandles;
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

            // Funding-Settlement wird global in CheckSession geprüft (Krypto + TradFi haben Funding auf BingX)

            // Vorgeladene Klines verwenden
            if (!klineResults.TryGetValue(ticker.Symbol, out var candles) || candles.Count < 50)
                continue;
            htfResults.TryGetValue(ticker.Symbol, out var htfCandles);
            m15Results.TryGetValue(ticker.Symbol, out var m15Candles);
            dailyResults.TryGetValue(ticker.Symbol, out var dailyCandles);
            weeklyResults.TryGetValue(ticker.Symbol, out var weeklyCandles);

            try
            {
                // Buch S.15: EIN linearer Flow — übergeordnet (W1/D1/H4/H1) → untergeordnet (M30).
                SetCurrentPriceIfNeeded(ticker.Symbol, ticker.LastPrice);

                var strategy = _strategyManager.GetOrCreateForSymbol(ticker.Symbol);
                var context = new MarketContext(ticker.Symbol, candles, ticker, positions, account, htfCandles, category, m15Candles, dailyCandles, weeklyCandles);
                var signal = strategy.Evaluate(context);

                // SK-System: Status vom Symbol-Klon speichern (Template wird nie evaluiert)
                // Nur den letzten nicht-blockierten Status zeigen (informativer als "Blocked" vom 50. Symbol)
                if (strategy is Engine.Strategies.SequenzKonzeptStrategy skInst
                    && !skInst.LastStatus.StartsWith("[4H:—"))
                    _lastSkStatus = $"{ticker.Symbol}: {skInst.LastStatus}";

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
                    var reContext = new MarketContext(ticker.Symbol, candles, ticker, positions, account, htfCandles, category, m15Candles, dailyCandles, weeklyCandles);
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

                // Leverage: Marktspezifisches Maximum aus CategorySettings (SK-Buch: einheitlich)
                var catSettingsStd = _riskSettings.GetCategorySettings(category);
                var adaptLevStd = (int)catSettingsStd.MaxLeverage;

                // Funding-Rate für alle BingX-Perpetuals (Krypto + TradFi haben Funding)
                var fundingRateStd = _fundingRates.GetValueOrDefault(ticker.Symbol, 0m);
                var riskCheck = _riskManager!.ValidateTrade(signal, context, fundingRateStd, adaptLevStd);
                if (!riskCheck.IsAllowed)
                {
                    if (_eventBus.HasLogSubscribers)
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                            $"{LogPrefix}{ticker.Symbol}: Trade abgelehnt - {riskCheck.RejectionReason}", ticker.Symbol));
                    continue;
                }

                // Position = exakt MaxPositionSizePercent der Wallet-Balance. Keine Skalierung.
                var positionSizeStd = riskCheck.AdjustedPositionSize;

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                    $"{LogPrefix}{ticker.Symbol}: Sizing: Qty={positionSizeStd:G6} | Lev={adaptLevStd}x | estMargin={positionSizeStd * ticker.LastPrice / adaptLevStd:F2} USDT"));

                // SK-System hat eigenen umfassenden M30-Filter (State Machine, ChoCH, ATR, Over-Extension, Fib-Levels)
                // → Kein weiterer M15-Timing-Filter nötig (wäre kontraproduktiv und ist nicht im Buch)
                var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;

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

                // SL/TP-Signal speichern + Exit State
                _positionSignals[slTpKey] = signal;

                _exitStates[slTpKey] = new PositionExitState
                {
                    Signal = signal, Symbol = ticker.Symbol, Side = side,
                    EntryPrice = ticker.LastPrice, OriginalQuantity = positionSizeStd,
                    Tp2 = signal.TakeProfit2,
                    MaxHoldHours = _riskSettings.MaxHoldHours
                };
                Interlocked.Increment(ref _tradesToday);
                OnSignalCreated(slTpKey);

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                    $"{LogPrefix}{ticker.Symbol}: {side} {positionSizeStd:F4} @ {ticker.LastPrice:F8} | Lev={adaptLevStd}x | SL={signal.StopLoss:F8} | TP1={signal.TakeProfit:F8} | TP2={signal.TakeProfit2:F8}",
                    ticker.Symbol));

                await OnOrderPlacedAsync(ticker, side, riskCheck.AdjustedPositionSize).ConfigureAwait(false);
                orderPlaced = true;

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

        // Desktop-Notification senden
        if (_botSettings.EnableDesktopNotifications)
        {
            var direction = trade.Pnl >= 0 ? "Gewinn" : "Verlust";
            _eventBus.PublishNotification(
                $"{LogPrefix}{trade.Symbol} geschlossen",
                $"{direction}: {trade.Pnl:F2} USDT ({trade.Side}, {trade.EntryPrice:F4} → {trade.ExitPrice:F4})");
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
    /// Hook: SL wurde angepasst (SL halbiert nach Workflow 4.1 oder BE gesetzt nach Workflow 4.2).
    /// LiveTradingService überschreibt dies und aktualisiert den nativen SL auf BingX.
    /// </summary>
    protected virtual Task OnStopLossAdjustedAsync(string symbol, Side side, decimal newStopLoss) => Task.CompletedTask;

    /// <summary>Hook: Zusätzliche Dispose-Logik für Subklassen (z.B. WebSocket-Cleanup).</summary>
    protected virtual void DisposeAdditional() { }
}