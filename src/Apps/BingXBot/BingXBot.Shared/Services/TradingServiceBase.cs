using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.ATI;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;
using System.Collections.Concurrent;

namespace BingXBot.Services;

/// <summary>
/// Abstrakte Basisklasse für Paper- und Live-Trading-Services.
/// Enthält die gesamte gemeinsame Logik: Scan-Loop (30s), PriceTicker-Loop (5s),
/// SL/TP-Prüfung, Trailing-Stop, Korrelations-Check, Risk-Management.
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

    // SL/TP-Tracking: Speichert das Original-Signal pro offener Position (Symbol_Side → SignalResult)
    // ConcurrentDictionary weil PriceTickerLoop und ScanAndTradeAsync parallel darauf zugreifen
    protected readonly ConcurrentDictionary<string, SignalResult> _positionSignals = new();
    // Trailing-Stop: Höchst-/Tiefstpreis seit Eröffnung pro Position (Symbol_Side → Preis)
    protected readonly ConcurrentDictionary<string, decimal> _extremePriceSinceEntry = new();
    // Trailing-Stop pro Position: ATI kann pro Trade einen optimierten Trailing-Prozentsatz setzen
    protected readonly ConcurrentDictionary<string, decimal> _positionTrailingPercent = new();

    // ATI: Adaptive Trading Intelligence (optional, kann aktiviert/deaktiviert werden)
    protected AdaptiveTradingIntelligence? _ati;

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
        BotEventBus eventBus)
    {
        _publicClient = publicClient;
        _strategyManager = strategyManager;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _eventBus = eventBus;
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
        OnSignalsClearedAll();

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
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Risk",
                        $"{LogPrefix}Tages-Drawdown zurückgesetzt (neuer Tag)"));
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
            try { await Task.Delay(30_000, ct).ConfigureAwait(false); }
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

                // Aktuelle Ticker holen (ein API-Call)
                var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
                if (tickers.Count == 0) continue;

                var tickerMap = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);

                foreach (var pos in positions)
                {
                    if (!tickerMap.TryGetValue(pos.Symbol, out var price)) continue;

                    // Preis auf Exchange setzen (nur für Paper relevant)
                    SetCurrentPriceIfNeeded(pos.Symbol, price);

                    var key = $"{pos.Symbol}_{pos.Side}";
                    if (!_positionSignals.TryGetValue(key, out var signal)) continue;

                    var hit = false;
                    var isStopLoss = false;
                    string reason = "";

                    // SL/TP-Check
                    if (pos.Side == Side.Buy)
                    {
                        if (signal.StopLoss.HasValue && price <= signal.StopLoss.Value)
                        { hit = true; isStopLoss = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:G8}"; }
                        else if (signal.TakeProfit.HasValue && price >= signal.TakeProfit.Value)
                        { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:G8}"; }
                    }
                    else // Short
                    {
                        if (signal.StopLoss.HasValue && price >= signal.StopLoss.Value)
                        { hit = true; isStopLoss = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:G8}"; }
                        else if (signal.TakeProfit.HasValue && price <= signal.TakeProfit.Value)
                        { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:G8}"; }
                    }

                    // Trailing-Stop: SL nachziehen wenn Position im Profit
                    // Pro-Position Trailing-% (von ATI optimiert) oder globaler Fallback aus RiskSettings
                    if (!hit && _riskSettings.EnableTrailingStop && signal.StopLoss.HasValue)
                    {
                        var trailPercent = _positionTrailingPercent.TryGetValue(key, out var customTrail)
                            ? customTrail / 100m
                            : _riskSettings.TrailingStopPercent / 100m;
                        if (pos.Side == Side.Buy)
                        {
                            var prev = _extremePriceSinceEntry.GetValueOrDefault(key, pos.EntryPrice);
                            if (price > prev) _extremePriceSinceEntry[key] = price;
                            var highest = _extremePriceSinceEntry.GetValueOrDefault(key, price);
                            var newSl = highest * (1m - trailPercent);
                            if (newSl > signal.StopLoss.Value && newSl < price)
                            {
                                _positionSignals[key] = signal with { StopLoss = newSl };
                                OnTrailingStopMoved(pos.Symbol, signal.StopLoss.Value, newSl);
                            }
                        }
                        else
                        {
                            var prev = _extremePriceSinceEntry.GetValueOrDefault(key, pos.EntryPrice);
                            if (price < prev) _extremePriceSinceEntry[key] = price;
                            var lowest = _extremePriceSinceEntry.GetValueOrDefault(key, price);
                            var newSl = lowest * (1m + trailPercent);
                            if (newSl < signal.StopLoss.Value && newSl > price)
                            {
                                _positionSignals[key] = signal with { StopLoss = newSl };
                                OnTrailingStopMoved(pos.Symbol, signal.StopLoss.Value, newSl);
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

        // 1. Alle Ticker holen und filtern
        var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
        if (tickers.Count == 0) return;

        var candidates = ScanHelper.FilterCandidates(tickers, _scannerSettings);
        if (candidates.Count == 0) return;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Scanner",
            $"{candidates.Count} Kandidaten gefunden"));

        // 2. Account + Positionen holen
        var account = await GetAccountAsync().ConfigureAwait(false);
        var positions = await GetPositionsForScanAsync().ConfigureAwait(false);

        foreach (var ticker in candidates)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // ATI-Pipeline (wenn aktiviert): Alle Strategien als Ensemble evaluieren
                if (_ati is { IsEnabled: true })
                {
                    // Candles laden (gleiche Logik wie ScanHelper.EvaluateCandidateAsync)
                    var candles = await _publicClient.GetKlinesAsync(
                        ticker.Symbol, _scannerSettings.ScanTimeFrame,
                        DateTime.UtcNow.AddHours(-100), DateTime.UtcNow, ct).ConfigureAwait(false);
                    if (candles.Count < 50) continue;

                    List<Candle>? htfCandles = null;
                    try
                    {
                        htfCandles = await _publicClient.GetKlinesAsync(
                            ticker.Symbol, Core.Enums.TimeFrame.H4,
                            DateTime.UtcNow.AddDays(-14), DateTime.UtcNow, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { /* HTF optional */ }

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
                    var riskCheck = ScanHelper.ValidateRisk(signal, context, _riskManager!, _eventBus, LogPrefix);
                    if (!riskCheck.IsAllowed) continue;

                    // Order platzieren (Signal für native SL/TP mitgeben)
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    var placed = await PlaceOrderOnExchangeAsync(ticker, side, riskCheck.AdjustedPositionSize, signal).ConfigureAwait(false);
                    if (!placed) continue;

                    // Signal speichern
                    var slTpKey = $"{ticker.Symbol}_{side}";
                    _positionSignals[slTpKey] = signal;
                    _extremePriceSinceEntry[slTpKey] = ticker.LastPrice;
                    // ATI-optimierter Trailing-Stop pro Position (statt globalem RiskSettings-Wert)
                    _positionTrailingPercent[slTpKey] = atiResult.TrailingStopPercent;
                    OnSignalCreated(slTpKey);

                    // ATI-Kontext für späteres Lernen speichern
                    var atrVal = IndicatorHelper.CalculateAtr(candles);
                    var atrLast = atrVal.Count > 0 && atrVal[^1].HasValue ? atrVal[^1]!.Value : 0m;
                    var slMult = signal.StopLoss.HasValue && atrLast > 0
                        ? Math.Abs(ticker.LastPrice - signal.StopLoss.Value) / atrLast : 2m;
                    var tpMult = signal.TakeProfit.HasValue && atrLast > 0
                        ? Math.Abs(signal.TakeProfit.Value - ticker.LastPrice) / atrLast : 4m;
                    _ati.RegisterOpenTrade(ticker.Symbol, side, atiResult.Features,
                        atiResult.Regime, atiResult.EnsembleVote, slMult, tpMult);

                    // G-Format statt N2/F6: Krypto-Preise haben variable Dezimalstellen
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                        $"{LogPrefix}{ticker.Symbol}: {side} {riskCheck.AdjustedPositionSize:G6} @ {ticker.LastPrice:G8} (SL={signal.StopLoss:G6} TP={signal.TakeProfit:G6})",
                        ticker.Symbol));

                    await OnOrderPlacedAsync(ticker, side, riskCheck.AdjustedPositionSize).ConfigureAwait(false);
                    account = await GetAccountAsync().ConfigureAwait(false);
                    positions = await GetPositionsForScanAsync().ConfigureAwait(false);
                }
                else
                {
                    // Bestehender Code (Standard-Pipeline ohne ATI):
                    // Kandidat evaluieren (Klines + HTF + Strategie)
                    var result = await ScanHelper.EvaluateCandidateAsync(
                        ticker, _publicClient, _strategyManager, _scannerSettings, positions, account, ct, _eventBus);
                    if (result == null) continue;

                    var signal = result.Signal;
                    var context = result.Context;

                    // Preis setzen (nur für Paper relevant)
                    SetCurrentPriceIfNeeded(ticker.Symbol, ticker.LastPrice);

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
                        }
                        continue;
                    }

                    // Korrelations-Check + Risk-Check (gemeinsame Logik via ScanHelper)
                    if (await ScanHelper.CheckCorrelationAsync(
                        ticker.Symbol, positions, _riskSettings, _publicClient, result.Candles, _eventBus, LogPrefix, ct))
                        continue;

                    var riskCheck = ScanHelper.ValidateRisk(signal, context, _riskManager, _eventBus, LogPrefix);
                    if (!riskCheck.IsAllowed) continue;

                    // Order platzieren (exchange-spezifisch, Signal für native SL/TP mitgeben)
                    var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                    var placed = await PlaceOrderOnExchangeAsync(ticker, side, riskCheck.AdjustedPositionSize, result.Signal).ConfigureAwait(false);

                    if (!placed) continue;

                    // SL/TP-Signal speichern (wird im PriceTickerLoop geprüft)
                    var slTpKey = $"{ticker.Symbol}_{side}";
                    _positionSignals[slTpKey] = signal;
                    _extremePriceSinceEntry[slTpKey] = ticker.LastPrice;
                    OnSignalCreated(slTpKey);

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                        $"{LogPrefix}{ticker.Symbol}: {side} {riskCheck.AdjustedPositionSize:G6} @ {ticker.LastPrice:G8}",
                        ticker.Symbol));

                    // Zusätzliche Aktionen nach Order (z.B. Entry-Fee loggen)
                    await OnOrderPlacedAsync(ticker, side, riskCheck.AdjustedPositionSize).ConfigureAwait(false);

                    // Account + Positionen aktualisieren (für nächste Iteration)
                    account = await GetAccountAsync().ConfigureAwait(false);
                    positions = await GetPositionsForScanAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                    $"{LogPrefix}{ticker.Symbol}: Fehler - {ex.Message}", ticker.Symbol));
            }
        }

        // ATI: Debug-Zusammenfassung loggen (warum wurden Kandidaten abgelehnt?)
        if (_ati is { IsEnabled: true })
        {
            var summary = _ati.GetScanSummaryAndReset();
            if (summary != null)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "ATI", summary));
            }
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

    /// <summary>
    /// Verarbeitet einen abgeschlossenen Trade: ATI-Lernen + Risiko-Update.
    /// Subklassen sollten diese Methode aufrufen statt _riskManager.UpdateDailyStats direkt.
    /// </summary>
    protected void ProcessCompletedTrade(CompletedTrade trade)
    {
        _riskManager?.UpdateDailyStats(trade);
        _ati?.ProcessTradeOutcome(trade);
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
