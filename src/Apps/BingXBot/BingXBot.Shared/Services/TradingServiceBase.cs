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
    // Cooldown: Zeitpunkt des letzten Verlust-Trades
    protected DateTime? _lastLossTime;
    // Täglicher Trade-Counter (wird bei Tageswechsel zurückgesetzt)
    protected int _tradesToday;

    // ATI: Adaptive Trading Intelligence (optional, kann aktiviert/deaktiviert werden)
    protected AdaptiveTradingIntelligence? _ati;
    // Bot-Einstellungen (für ATI Auto-Save Intervall, Notifications etc.)
    protected readonly BotSettings _botSettings;

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
        _riskManager = riskManager;
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
        _tradesToday = 0;
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
                    _tradesToday = 0;
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
                if (_ati is { IsEnabled: true } && _botSettings.AtiAutoSaveIntervalMinutes > 0)
                {
                    var minutesSinceLastSave = (DateTime.UtcNow - _ati.LastAutoSaveTime).TotalMinutes;
                    if (minutesSinceLastSave >= _botSettings.AtiAutoSaveIntervalMinutes)
                    {
                        _ati.LastAutoSaveTime = DateTime.UtcNow;
                        await OnAtiAutoSaveAsync().ConfigureAwait(false);
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
                                        $"{LogPrefix}{pos.Symbol}: Liquidation nur noch {distancePercent:F1}% entfernt! (Liq={liqPrice:G6}, Preis={price:G6})",
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

                                // SL auf Break-Even verschieben
                                var beSl = exitState.EntryPrice;
                                _positionSignals[key] = signal with { StopLoss = beSl, TakeProfit = exitState.Tp2 };
                                exitState.Signal = _positionSignals[key];
                                exitState.Phase = ExitPhase.Tp1Hit;
                                exitState.MaxHoldHours = _riskSettings.MaxHoldHoursAfterTp1;

                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Exit",
                                    $"{LogPrefix}{pos.Symbol}: TP1 erreicht → {_riskSettings.Tp1CloseRatio:P0} geschlossen, SL→BE ({beSl:G8})",
                                    pos.Symbol));
                                continue; // Nächste Position, kein weiterer Check nötig
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
                            { hit = true; isStopLoss = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:G8}"; }
                            else if (signal.TakeProfit.HasValue && price >= signal.TakeProfit.Value)
                            { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:G8}"; }
                        }
                        else
                        {
                            if (signal.StopLoss.HasValue && price >= signal.StopLoss.Value)
                            { hit = true; isStopLoss = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:G8}"; }
                            else if (signal.TakeProfit.HasValue && price <= signal.TakeProfit.Value)
                            { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:G8}"; }
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

        // 1. Alle Ticker holen und filtern
        var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
        if (tickers.Count == 0) return;

        var candidates = ScanHelper.FilterCandidates(tickers, _scannerSettings);
        if (candidates.Count == 0) return;

        if (_eventBus.HasLogSubscribers)
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Scanner",
                $"{candidates.Count} Kandidaten gefunden"));

        // 2. Globale MarketFilter prüfen (VOR dem teuren Klines-Loading)
        var sessionFilter = MarketFilter.CheckSession(DateTime.UtcNow);
        if (!sessionFilter.IsAllowed)
        {
            if (_eventBus.HasLogSubscribers)
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Filter",
                    $"{LogPrefix}Scan übersprungen: {sessionFilter.SessionInfo}"));
            IndicatorHelper.ClearCache();
            return;
        }

        if (MarketFilter.IsCooldownActive(_lastLossTime, _riskSettings.CooldownHours))
        {
            if (_eventBus.HasLogSubscribers)
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Filter",
                    $"{LogPrefix}Cooldown aktiv (letzter Verlust: {_lastLossTime:HH:mm})"));
            IndicatorHelper.ClearCache();
            return;
        }

        if (_riskSettings.MaxTradesPerDay > 0 && MarketFilter.IsMaxDailyTradesReached(_tradesToday, _riskSettings.MaxTradesPerDay))
        {
            if (_eventBus.HasLogSubscribers)
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Filter",
                    $"{LogPrefix}Max Trades/Tag erreicht ({_tradesToday}/{_riskSettings.MaxTradesPerDay})"));
            IndicatorHelper.ClearCache();
            return;
        }

        // 3. Account + Positionen holen
        var account = await GetAccountAsync().ConfigureAwait(false);
        var positions = await GetPositionsForScanAsync().ConfigureAwait(false);

        // 4. Klines für alle Kandidaten PARALLEL vorladen (statt sequenziell pro Kandidat)
        //    Begrenzt auf 5 parallele Requests um Rate-Limiter nicht zu überlasten
        var now = DateTime.UtcNow;
        var klineResults = new Dictionary<string, List<Candle>>();
        var htfResults = new Dictionary<string, List<Candle>?>();

        var semaphore = new SemaphoreSlim(5);
        var klineTasks = candidates.Select(async ticker =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
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

                lock (klineResults)
                {
                    klineResults[ticker.Symbol] = candles;
                    htfResults[ticker.Symbol] = htfCandles;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (_eventBus.HasLogSubscribers)
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Scanner",
                        $"{ticker.Symbol}: Klines-Laden fehlgeschlagen: {ex.Message}", ticker.Symbol));
            }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(klineTasks).ConfigureAwait(false);

        // 4. Kandidaten sequenziell evaluieren (Order-Platzierung muss sequenziell sein)
        var orderPlaced = false;

        foreach (var ticker in candidates)
        {
            if (ct.IsCancellationRequested) break;

            // Vorgeladene Klines verwenden
            if (!klineResults.TryGetValue(ticker.Symbol, out var candles) || candles.Count < 50)
                continue;
            htfResults.TryGetValue(ticker.Symbol, out var htfCandles);

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

                    // Risk-Check
                    var riskCheck = ScanHelper.ValidateRisk(signal, context, _riskManager!, _eventBus, LogPrefix, _currentFundingRate);
                    if (!riskCheck.IsAllowed) continue;

                    // Order platzieren (Signal für native SL/TP mitgeben)
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    var placed = await PlaceOrderOnExchangeAsync(ticker, side, riskCheck.AdjustedPositionSize, signal).ConfigureAwait(false);
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
                        CurrentAtr = atrLast, ConflueceScore = signal.ConflueceScore,
                        MaxHoldHours = _riskSettings.MaxHoldHours
                    };
                    _tradesToday++;
                    OnSignalCreated(slTpKey);

                    // ATI-Kontext für späteres Lernen speichern
                    var slMult = signal.StopLoss.HasValue && atrLast > 0
                        ? Math.Abs(ticker.LastPrice - signal.StopLoss.Value) / atrLast : 2m;
                    var tpMult = signal.TakeProfit.HasValue && atrLast > 0
                        ? Math.Abs(signal.TakeProfit.Value - ticker.LastPrice) / atrLast : 4m;
                    _ati.RegisterOpenTrade(ticker.Symbol, side, atiResult.Features,
                        atiResult.Regime, atiResult.EnsembleVote, slMult, tpMult);

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                        $"{LogPrefix}{ticker.Symbol}: {side} {riskCheck.AdjustedPositionSize:G6} @ {ticker.LastPrice:G8} (SL={signal.StopLoss:G6} TP={signal.TakeProfit:G6})",
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

                    // Korrelations-Check + Risk-Check
                    if (await ScanHelper.CheckCorrelationAsync(
                        ticker.Symbol, positions, _riskSettings, _publicClient, candles, _eventBus, LogPrefix, ct))
                        continue;

                    var riskCheck = ScanHelper.ValidateRisk(signal, context, _riskManager, _eventBus, LogPrefix);
                    if (!riskCheck.IsAllowed) continue;

                    // Order platzieren
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    var placed = await PlaceOrderOnExchangeAsync(ticker, side, riskCheck.AdjustedPositionSize, signal).ConfigureAwait(false);

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
                        CurrentAtr = atrLastVal, ConflueceScore = signal.ConflueceScore,
                        MaxHoldHours = _riskSettings.MaxHoldHours
                    };
                    _tradesToday++;
                    OnSignalCreated(slTpKey);

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                        $"{LogPrefix}{ticker.Symbol}: {side} {riskCheck.AdjustedPositionSize:G6} @ {ticker.LastPrice:G8}",
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

        // Indikator-Cache nach Scan-Durchlauf leeren (Daten sind beim nächsten Scan veraltet)
        IndicatorHelper.ClearCache();
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

    /// <summary>Order auf der Exchange platzieren. Gibt true zurück bei Erfolg. Signal optional für native SL/TP.</summary>
    protected abstract Task<bool> PlaceOrderOnExchangeAsync(Ticker ticker, Side side, decimal quantity, SignalResult? signal = null);

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
        _ati?.ProcessTradeOutcome(trade);

        // Desktop-Notification senden
        if (_botSettings.EnableDesktopNotifications)
        {
            var direction = trade.Pnl >= 0 ? "Gewinn" : "Verlust";
            _eventBus.PublishNotification(
                $"{LogPrefix}{trade.Symbol} geschlossen",
                $"{direction}: {trade.Pnl:F2} USDT ({trade.Side}, {trade.EntryPrice:G6} → {trade.ExitPrice:G6})");
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
        DisposeAdditional();
    }

    /// <summary>Hook: Zusätzliche Dispose-Logik für Subklassen (z.B. WebSocket-Cleanup).</summary>
    protected virtual void DisposeAdditional() { }
}
