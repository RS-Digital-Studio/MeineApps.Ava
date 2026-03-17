using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Simulation;
using BingXBot.Engine;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBot.Services;

/// <summary>
/// Paper-Trading-Service: Echtzeit-Simulation mit REST-Polling.
/// Alle 30 Sekunden: Ticker holen, Scanner filtern, Klines laden, Strategie evaluieren,
/// bei Signal: RiskManager pruefen, Order auf SimulatedExchange platzieren.
/// Events werden ueber den BotEventBus publiziert.
/// </summary>
public class PaperTradingService : IDisposable
{
    private readonly IPublicMarketDataClient _publicClient;
    private readonly StrategyManager _strategyManager;
    private readonly RiskSettings _riskSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly BotEventBus _eventBus;

    private SimulatedExchange? _exchange;
    private RiskManager? _riskManager;
    private CancellationTokenSource? _cts;
    private volatile bool _isRunning;
    private volatile bool _isPaused;
    private bool _disposed;
    private DateTime _lastDailyResetDate = DateTime.UtcNow.Date;

    // SL/TP-Tracking: Speichert das Original-Signal pro offener Position (Symbol_Side → SignalResult)
    // ConcurrentDictionary weil PriceTickerLoop und ScanAndTradeAsync parallel darauf zugreifen
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SignalResult> _positionSignals = new();

    /// <summary>Zugriff auf die simulierte Exchange fuer Account-Abfragen.</summary>
    public SimulatedExchange? Exchange => _exchange;

    /// <summary>Ob der Service gerade laeuft.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Ob der Service pausiert ist.</summary>
    public bool IsPaused => _isPaused;

    public PaperTradingService(
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

    /// <summary>Startet das Paper-Trading mit dem angegebenen Startkapital.</summary>
    public void Start(decimal initialBalance = 10_000m)
    {
        if (_isRunning) return;

        _exchange = new SimulatedExchange(new BacktestSettings { InitialBalance = initialBalance });
        _riskManager = new RiskManager(_riskSettings, NullLogger<RiskManager>.Instance);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _isRunning = true;
        _isPaused = false;

        _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            $"Paper-Trading gestartet (Startkapital: {initialBalance:N0} USDT)"));

        _ = RunLoopAsync(_cts.Token);
        _ = PriceTickerLoopAsync(_cts.Token);
    }

    /// <summary>Pausiert das Paper-Trading (Loop laeuft weiter, ueberspringt aber Scans).</summary>
    public void Pause()
    {
        if (!_isRunning || _isPaused) return;
        _isPaused = true;

        _eventBus.PublishBotState(BotState.Paused);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Paper-Trading pausiert"));
    }

    /// <summary>Setzt das Paper-Trading nach Pause fort.</summary>
    public void Resume()
    {
        if (!_isRunning || !_isPaused) return;
        _isPaused = false;

        _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Paper-Trading fortgesetzt"));
    }

    /// <summary>Stoppt das Paper-Trading und schliesst alle offenen Positionen.</summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;
        _cts?.Cancel();
        _isRunning = false;
        _isPaused = false;

        // Alle Positionen schliessen
        if (_exchange != null)
        {
            // Anzahl bereits publizierter Trades merken
            var previousTradeCount = _exchange.GetCompletedTrades().Count;
            await _exchange.CloseAllPositionsAsync();
            // Nur die neu durch CloseAll entstandenen Trades publizieren (verhindert Doppel-Publizierung)
            var allTrades = _exchange.GetCompletedTrades();
            for (int i = previousTradeCount; i < allTrades.Count; i++)
            {
                _eventBus.PublishTrade(allTrades[i]);
                _riskManager?.UpdateDailyStats(allTrades[i]);
            }
        }

        _cts?.Dispose();
        _cts = null;

        _positionSignals.Clear();
        _eventBus.PublishBotState(BotState.Stopped);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Paper-Trading gestoppt"));
    }

    /// <summary>Notfall-Stop: Sofort alle Positionen schliessen (async statt blockierend).</summary>
    public async Task EmergencyStopAsync()
    {
        _cts?.Cancel();
        _isRunning = false;
        _isPaused = false;

        if (_exchange != null)
        {
            // Anzahl bereits publizierter Trades merken (wie in StopAsync)
            var previousTradeCount = _exchange.GetCompletedTrades().Count;
            await _exchange.CloseAllPositionsAsync();
            // Nur die neu durch CloseAll entstandenen Trades publizieren
            var allTrades = _exchange.GetCompletedTrades();
            for (int i = previousTradeCount; i < allTrades.Count; i++)
            {
                _eventBus.PublishTrade(allTrades[i]);
                _riskManager?.UpdateDailyStats(allTrades[i]);
            }
        }
        _positionSignals.Clear();

        _cts?.Dispose();
        _cts = null;

        _eventBus.PublishBotState(BotState.EmergencyStop);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
            "NOTFALL-STOP: Alle Positionen geschlossen"));
    }

    /// <summary>Hauptschleife: Alle 30 Sekunden scannen und handeln.</summary>
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
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Risk",
                        "Tages-Drawdown zurückgesetzt (neuer Tag)"));
                }

                // Bei Pause: Loop laeuft weiter, ueberspringt aber den Scan
                if (!_isPaused)
                    await ScanAndTradeAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                    $"Fehler in der Trading-Loop: {ex.Message}"));
            }

            // 30 Sekunden warten bis zum naechsten Scan
            try { await Task.Delay(30_000, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Schneller Preis-Ticker: Aktualisiert alle 5 Sekunden die Preise offener Positionen.
    /// Damit PnL und MarkPrice in der UI flüssig aktualisiert werden statt nur alle 30s beim Scan.
    /// </summary>
    private async Task PriceTickerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(5_000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            if (_isPaused || _exchange == null) continue;

            try
            {
                // Prüfe ob offene Positionen vorhanden
                var positions = await _exchange.GetPositionsAsync().ConfigureAwait(false);
                if (positions.Count == 0) continue;

                // Aktuelle Ticker für offene Symbole holen (ein einzelner API-Call)
                var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
                if (tickers.Count == 0) continue;

                var tickerMap = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);

                // Preise auf der SimulatedExchange aktualisieren + SL/TP prüfen
                foreach (var pos in positions)
                {
                    if (!tickerMap.TryGetValue(pos.Symbol, out var price)) continue;
                    _exchange.SetCurrentPrice(pos.Symbol, price);

                    // SL/TP-Prüfung für diese Position
                    var key = $"{pos.Symbol}_{pos.Side}";
                    if (!_positionSignals.TryGetValue(key, out var signal)) continue;

                    var hit = false;
                    string reason = "";

                    if (pos.Side == Side.Buy)
                    {
                        if (signal.StopLoss.HasValue && price <= signal.StopLoss.Value)
                        {
                            _exchange.SetCurrentPrice(pos.Symbol, signal.StopLoss.Value);
                            hit = true;
                            reason = $"Stop-Loss bei {signal.StopLoss.Value:N2}";
                        }
                        else if (signal.TakeProfit.HasValue && price >= signal.TakeProfit.Value)
                        {
                            _exchange.SetCurrentPrice(pos.Symbol, signal.TakeProfit.Value);
                            hit = true;
                            reason = $"Take-Profit bei {signal.TakeProfit.Value:N2}";
                        }
                    }
                    else // Short
                    {
                        if (signal.StopLoss.HasValue && price >= signal.StopLoss.Value)
                        {
                            _exchange.SetCurrentPrice(pos.Symbol, signal.StopLoss.Value);
                            hit = true;
                            reason = $"Stop-Loss bei {signal.StopLoss.Value:N2}";
                        }
                        else if (signal.TakeProfit.HasValue && price <= signal.TakeProfit.Value)
                        {
                            _exchange.SetCurrentPrice(pos.Symbol, signal.TakeProfit.Value);
                            hit = true;
                            reason = $"Take-Profit bei {signal.TakeProfit.Value:N2}";
                        }
                    }

                    if (hit)
                    {
                        var prevCount = _exchange.GetCompletedTrades().Count;
                        await _exchange.ClosePositionAsync(pos.Symbol, pos.Side).ConfigureAwait(false);
                        _positionSignals.TryRemove(key, out _);

                        // Preis zurücksetzen
                        _exchange.SetCurrentPrice(pos.Symbol, price);

                        // Trade publizieren + RiskManager aktualisieren
                        var allTrades = _exchange.GetCompletedTrades();
                        for (int i = prevCount; i < allTrades.Count; i++)
                        {
                            _eventBus.PublishTrade(allTrades[i]);
                            _riskManager?.UpdateDailyStats(allTrades[i]);
                        }

                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                            $"{pos.Symbol}: {reason} ({pos.Side})", pos.Symbol));
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "PriceTicker",
                    $"PriceTicker Fehler: {ex.Message}"));
            }
        }
    }

    /// <summary>Ein Scan-Zyklus: Ticker laden, filtern, Strategie evaluieren, handeln.</summary>
    private async Task ScanAndTradeAsync(CancellationToken ct)
    {
        if (_exchange == null || _riskManager == null) return;
        if (_strategyManager.CurrentTemplate == null)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                "Keine Strategie ausgewaehlt"));
            return;
        }

        // 1. Alle Ticker holen und filtern
        var tickers = await _publicClient.GetAllTickersAsync(ct);
        if (tickers.Count == 0) return;

        var candidates = ScanHelper.FilterCandidates(tickers, _scannerSettings);
        if (candidates.Count == 0) return;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Scanner",
            $"{candidates.Count} Kandidaten gefunden"));

        // 2. Fuer jeden Kandidaten: Evaluieren und handeln
        var account = await _exchange.GetAccountInfoAsync();
        var positions = await _exchange.GetPositionsAsync();

        foreach (var ticker in candidates)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Kandidat evaluieren (Klines + HTF + Strategie)
                var result = await ScanHelper.EvaluateCandidateAsync(
                    ticker, _publicClient, _strategyManager, _scannerSettings, positions, account, ct);
                if (result == null) continue;

                var signal = result.Signal;
                var context = result.Context;

                // Aktuellen Preis auf der SimulatedExchange setzen
                _exchange.SetCurrentPrice(ticker.Symbol, ticker.LastPrice);

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Scanner",
                    $"{ticker.Symbol}: {signal.Signal} Signal (Confidence: {signal.Confidence:P0}) - {signal.Reason}",
                    ticker.Symbol));

                // Close-Signale verarbeiten
                if (signal.Signal is Signal.CloseLong or Signal.CloseShort)
                {
                    var closeSide = signal.Signal == Signal.CloseLong ? Side.Buy : Side.Sell;
                    if (positions.Any(p => p.Symbol == ticker.Symbol && p.Side == closeSide))
                    {
                        var prevCount = _exchange.GetCompletedTrades().Count;
                        await _exchange.ClosePositionAsync(ticker.Symbol, closeSide);
                        _positionSignals.TryRemove($"{ticker.Symbol}_{closeSide}", out _);

                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                            $"{ticker.Symbol}: Position geschlossen ({closeSide})", ticker.Symbol));

                        var allTrades = _exchange.GetCompletedTrades();
                        for (int j = prevCount; j < allTrades.Count; j++)
                        {
                            _eventBus.PublishTrade(allTrades[j]);
                            _riskManager.UpdateDailyStats(allTrades[j]);
                        }
                    }
                    continue;
                }

                // Korrelations-Check + Risk-Check (gemeinsame Logik via ScanHelper)
                if (await ScanHelper.CheckCorrelationAsync(
                    ticker.Symbol, positions, _riskSettings, _publicClient, result.Candles, _eventBus, "", ct))
                    continue;

                var riskCheck = ScanHelper.ValidateRisk(signal, context, _riskManager, _eventBus, "");
                if (!riskCheck.IsAllowed) continue;

                // Leverage setzen (aus RiskSettings) und Order platzieren
                var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                await _exchange.SetLeverageAsync(ticker.Symbol, (int)_riskSettings.MaxLeverage, side);
                var order = await _exchange.PlaceOrderAsync(new OrderRequest(
                    ticker.Symbol, side, OrderType.Market, riskCheck.AdjustedPositionSize));

                // Pruefen ob die Order abgelehnt wurde (z.B. nicht genug Margin)
                if (order.Status == OrderStatus.Rejected)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Trade",
                        $"{ticker.Symbol}: Order abgelehnt (nicht genug Margin)", ticker.Symbol));
                    continue;
                }

                // SL/TP-Signal für diese Position speichern (wird im PriceTickerLoop geprüft)
                var slTpKey = $"{ticker.Symbol}_{side}";
                _positionSignals[slTpKey] = signal;

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                    $"{ticker.Symbol}: {side} {riskCheck.AdjustedPositionSize:F6} @ {ticker.LastPrice:N2}",
                    ticker.Symbol));

                // Account + Positionen aktualisieren
                account = await _exchange.GetAccountInfoAsync();
                positions = await _exchange.GetPositionsAsync();
            }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                    $"{ticker.Symbol}: Fehler - {ex.Message}", ticker.Symbol));
            }
        }

        // Indikator-Cache nach Scan-Durchlauf leeren (Daten sind beim nächsten Scan veraltet)
        IndicatorHelper.ClearCache();
    }

    /// <summary>
    /// Gibt das gespeicherte Signal (SL/TP) fuer eine offene Position zurueck.
    /// </summary>
    public SignalResult? GetPositionSignal(string symbol, Side side)
    {
        _positionSignals.TryGetValue($"{symbol}_{side}", out var signal);
        return signal;
    }

    /// <summary>
    /// Entfernt das gespeicherte Signal fuer eine Position (z.B. bei manuellem Close ueber Dashboard).
    /// Verhindert, dass PriceTickerLoop eine bereits geschlossene Position erneut zu schliessen versucht.
    /// </summary>
    public void RemovePositionSignal(string symbol, Side side)
    {
        _positionSignals.TryRemove($"{symbol}_{side}", out _);
    }

    /// <summary>
    /// Aktualisiert SL/TP fuer eine offene Position (z.B. wenn der User im Dashboard editiert).
    /// </summary>
    public void UpdatePositionSignal(string symbol, Side side, decimal? newSl, decimal? newTp)
    {
        var key = $"{symbol}_{side}";
        if (_positionSignals.TryGetValue(key, out var existing))
        {
            _positionSignals[key] = existing with { StopLoss = newSl, TakeProfit = newTp };
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
