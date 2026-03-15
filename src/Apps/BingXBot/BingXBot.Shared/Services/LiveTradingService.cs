using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Risk;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace BingXBot.Services;

/// <summary>
/// Live-Trading-Service: Echte Orders auf BingX platzieren.
/// Nutzt BingXRestClient für Orders und IPublicMarketDataClient für Marktdaten.
/// WARNUNG: Echtes Geld! Nur mit ausreichendem Paper-Testing verwenden.
/// Analog zum PaperTradingService, aber mit echtem BingXRestClient statt SimulatedExchange.
/// </summary>
public class LiveTradingService : IDisposable
{
    private readonly BingXRestClient _restClient;
    private readonly IPublicMarketDataClient _publicClient;
    private readonly StrategyManager _strategyManager;
    private readonly RiskSettings _riskSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly BotEventBus _eventBus;

    private RiskManager? _riskManager;
    private CancellationTokenSource? _cts;
    private volatile bool _isRunning;
    private volatile bool _isPaused;
    private bool _disposed;

    // SL/TP-Tracking: Speichert das Original-Signal pro offener Position (Symbol_Side -> SignalResult)
    // ConcurrentDictionary weil PriceTickerLoop und ScanAndTradeAsync parallel darauf zugreifen
    private readonly ConcurrentDictionary<string, SignalResult> _positionSignals = new();

    /// <summary>Ob der Service gerade läuft.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Ob der Service pausiert ist.</summary>
    public bool IsPaused => _isPaused;

    public LiveTradingService(
        BingXRestClient restClient,
        IPublicMarketDataClient publicClient,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus)
    {
        _restClient = restClient;
        _publicClient = publicClient;
        _strategyManager = strategyManager;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _eventBus = eventBus;
    }

    /// <summary>Startet das Live-Trading.</summary>
    public void Start()
    {
        if (_isRunning) return;

        _riskManager = new RiskManager(_riskSettings, NullLogger<RiskManager>.Instance);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _isRunning = true;
        _isPaused = false;

        _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
            "LIVE-TRADING GESTARTET - Echtes Geld! Überwache den Bot sorgfältig."));

        _ = RunLoopAsync(_cts.Token);
        _ = PriceTickerLoopAsync(_cts.Token);
    }

    /// <summary>Pausiert das Live-Trading (Loop läuft weiter, überspringt aber Scans).</summary>
    public void Pause()
    {
        if (!_isRunning || _isPaused) return;
        _isPaused = true;

        _eventBus.PublishBotState(BotState.Paused);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Live-Trading pausiert - offene Positionen bleiben bestehen"));
    }

    /// <summary>Setzt das Live-Trading nach Pause fort.</summary>
    public void Resume()
    {
        if (!_isRunning || !_isPaused) return;
        _isPaused = false;

        _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Live-Trading fortgesetzt"));
    }

    /// <summary>
    /// Stoppt das Live-Trading. Offene Positionen bleiben bestehen (User entscheidet).
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;
        _cts?.Cancel();
        _isRunning = false;
        _isPaused = false;

        // Im Live-Modus: Positionen NICHT automatisch schließen beim normalen Stop
        // Der User soll entscheiden ob er sie manuell schließen will
        _positionSignals.Clear();
        _cts?.Dispose();
        _cts = null;

        _eventBus.PublishBotState(BotState.Stopped);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Live-Trading gestoppt. Offene Positionen bleiben bestehen."));

        await Task.CompletedTask;
    }

    /// <summary>
    /// Notfall-Stop: ALLE echten Positionen auf BingX sofort schließen!
    /// </summary>
    public async Task EmergencyStopAsync()
    {
        _cts?.Cancel();
        _isRunning = false;
        _isPaused = false;

        // Emergency: ALLE Positionen sofort schließen!
        try
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                "NOTFALL-STOP: Schließe alle Positionen auf BingX..."));

            var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
            foreach (var pos in positions)
            {
                try
                {
                    await _restClient.ClosePositionAsync(pos.Symbol, pos.Side).ConfigureAwait(false);
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Trade",
                        $"NOTFALL: {pos.Symbol} {pos.Side} geschlossen", pos.Symbol));
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Trade",
                        $"FEHLER: {pos.Symbol} konnte nicht geschlossen werden: {ex.Message}", pos.Symbol));
                }
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                $"FEHLER beim Laden der Positionen: {ex.Message}"));
        }

        _positionSignals.Clear();
        _cts?.Dispose();
        _cts = null;

        _eventBus.PublishBotState(BotState.EmergencyStop);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
            "NOTFALL-STOP abgeschlossen. Prüfe dein BingX-Konto!"));
    }

    /// <summary>Hauptschleife: Alle 30s scannen und handeln.</summary>
    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Bei Pause: Loop läuft weiter, überspringt aber den Scan
                if (!_isPaused)
                    await ScanAndTradeAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                    $"Fehler in der Live-Trading-Loop: {ex.Message}"));

                // Bei API-Fehler: 60s warten statt 30s (Rate-Limit-Schutz)
                try { await Task.Delay(60_000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            // 30 Sekunden warten bis zum nächsten Scan
            try { await Task.Delay(30_000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// SL/TP-Check alle 5 Sekunden für offene Positionen.
    /// Holt echte Positionen von BingX und prüft gegen gespeicherte SL/TP-Levels.
    /// </summary>
    private async Task PriceTickerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(5_000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            if (_isPaused) continue;

            try
            {
                // Offene Positionen von BingX holen
                var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
                if (positions.Count == 0) continue;

                // Aktuelle Preise holen (Public API, kein Key nötig)
                var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
                var tickerMap = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);

                foreach (var pos in positions)
                {
                    if (!tickerMap.TryGetValue(pos.Symbol, out var price)) continue;

                    var key = $"{pos.Symbol}_{pos.Side}";
                    if (!_positionSignals.TryGetValue(key, out var signal)) continue;

                    var hit = false;
                    string reason = "";

                    if (pos.Side == Side.Buy)
                    {
                        if (signal.StopLoss.HasValue && price <= signal.StopLoss.Value)
                        { hit = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:N2}"; }
                        else if (signal.TakeProfit.HasValue && price >= signal.TakeProfit.Value)
                        { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:N2}"; }
                    }
                    else // Short
                    {
                        if (signal.StopLoss.HasValue && price >= signal.StopLoss.Value)
                        { hit = true; reason = $"Stop-Loss bei {signal.StopLoss.Value:N2}"; }
                        else if (signal.TakeProfit.HasValue && price <= signal.TakeProfit.Value)
                        { hit = true; reason = $"Take-Profit bei {signal.TakeProfit.Value:N2}"; }
                    }

                    if (hit)
                    {
                        try
                        {
                            await _restClient.ClosePositionAsync(pos.Symbol, pos.Side).ConfigureAwait(false);
                            _positionSignals.TryRemove(key, out _);

                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                                $"LIVE: {pos.Symbol}: {reason} ({pos.Side})", pos.Symbol));

                            // CompletedTrade erstellen für TradeHistory
                            var pnl = pos.Side == Side.Buy
                                ? (price - pos.EntryPrice) * pos.Quantity
                                : (pos.EntryPrice - price) * pos.Quantity;
                            var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, price,
                                pos.Quantity, pnl, 0m, pos.OpenTime, DateTime.UtcNow, reason, TradingMode.Live);
                            _eventBus.PublishTrade(trade);
                            _riskManager?.UpdateDailyStats(trade);
                        }
                        catch (Exception ex)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Trade",
                                $"LIVE: {pos.Symbol}: {reason} FEHLGESCHLAGEN - {ex.Message}", pos.Symbol));
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Live PriceTicker Fehler: {ex.Message}");
            }
        }
    }

    /// <summary>Ein Scan-Zyklus mit echten Orders.</summary>
    private async Task ScanAndTradeAsync(CancellationToken ct)
    {
        if (_riskManager == null) return;
        if (_strategyManager.CurrentTemplate == null)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                "Keine Strategie ausgewählt"));
            return;
        }

        // 1. Ticker holen (Public API, kein Key nötig)
        var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
        if (tickers.Count == 0) return;

        // 2. Nach Scanner-Kriterien filtern
        var candidates = tickers
            .Where(t => t.Volume24h >= _scannerSettings.MinVolume24h)
            .Where(t => Math.Abs(t.PriceChangePercent24h) >= _scannerSettings.MinPriceChange)
            .Where(t => _scannerSettings.Blacklist.Count == 0 || !_scannerSettings.Blacklist.Contains(t.Symbol))
            .Where(t => _scannerSettings.Whitelist.Count == 0 || _scannerSettings.Whitelist.Contains(t.Symbol))
            .OrderByDescending(t => t.Volume24h)
            .Take(_scannerSettings.MaxResults)
            .ToList();

        if (candidates.Count == 0) return;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Scanner",
            $"{candidates.Count} Kandidaten"));

        // 3. Account + Positionen von BingX holen
        var account = await _restClient.GetAccountInfoAsync().ConfigureAwait(false);
        var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);

        foreach (var ticker in candidates)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Klines laden (letzte 100 Stunden-Candles über Public API)
                var candles = await _publicClient.GetKlinesAsync(
                    ticker.Symbol, _scannerSettings.ScanTimeFrame,
                    DateTime.UtcNow.AddHours(-100), DateTime.UtcNow, ct).ConfigureAwait(false);

                if (candles.Count < 50) continue;

                // Strategie evaluieren
                var strategy = _strategyManager.GetOrCreateForSymbol(ticker.Symbol);
                var context = new MarketContext(ticker.Symbol, candles, ticker, positions, account);
                var signal = strategy.Evaluate(context);

                if (signal.Signal == Signal.None) continue;

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Scanner",
                    $"LIVE: {ticker.Symbol}: {signal.Signal} (Confidence: {signal.Confidence:P0}) - {signal.Reason}",
                    ticker.Symbol));

                // Close-Signale verarbeiten
                if (signal.Signal is Signal.CloseLong or Signal.CloseShort)
                {
                    var closeSide = signal.Signal == Signal.CloseLong ? Side.Buy : Side.Sell;
                    if (positions.Any(p => p.Symbol == ticker.Symbol && p.Side == closeSide))
                    {
                        try
                        {
                            await _restClient.ClosePositionAsync(ticker.Symbol, closeSide).ConfigureAwait(false);
                            _positionSignals.TryRemove($"{ticker.Symbol}_{closeSide}", out _);

                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                                $"LIVE: {ticker.Symbol} {closeSide} geschlossen", ticker.Symbol));
                        }
                        catch (Exception ex)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Trade",
                                $"LIVE: {ticker.Symbol} schließen fehlgeschlagen: {ex.Message}", ticker.Symbol));
                        }
                    }
                    continue;
                }

                // Risk-Check
                var riskCheck = _riskManager.ValidateTrade(signal, context);
                if (!riskCheck.IsAllowed)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Risk",
                        $"LIVE: {ticker.Symbol}: Abgelehnt - {riskCheck.RejectionReason}", ticker.Symbol));
                    continue;
                }

                // ECHTE ORDER PLATZIEREN
                var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;

                try
                {
                    // Leverage setzen
                    await _restClient.SetLeverageAsync(ticker.Symbol, (int)_riskSettings.MaxLeverage, side)
                        .ConfigureAwait(false);

                    // Order platzieren
                    var order = await _restClient.PlaceOrderAsync(new OrderRequest(
                        ticker.Symbol, side, OrderType.Market, riskCheck.AdjustedPositionSize))
                        .ConfigureAwait(false);

                    if (order.Status == OrderStatus.Rejected)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Trade",
                            $"LIVE ORDER ABGELEHNT: {ticker.Symbol} {side}", ticker.Symbol));
                        continue;
                    }

                    // SL/TP Signal speichern (wird im PriceTickerLoop geprüft)
                    _positionSignals[$"{ticker.Symbol}_{side}"] = signal;

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                        $"LIVE ORDER: {ticker.Symbol} {side} {riskCheck.AdjustedPositionSize:F6} @ {ticker.LastPrice:N2}",
                        ticker.Symbol));

                    // RiskManager aktualisieren (als Eröffnungs-Trade mit PnL=0)
                    _riskManager.UpdateDailyStats(new CompletedTrade(
                        ticker.Symbol, side, ticker.LastPrice, ticker.LastPrice,
                        riskCheck.AdjustedPositionSize, 0m, 0m,
                        DateTime.UtcNow, DateTime.UtcNow, "Eröffnet", TradingMode.Live));

                    // Account + Positionen aktualisieren (für nächste Iteration)
                    account = await _restClient.GetAccountInfoAsync().ConfigureAwait(false);
                    positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Trade",
                        $"LIVE ORDER FEHLGESCHLAGEN: {ticker.Symbol} {side} - {ex.Message}", ticker.Symbol));
                }
            }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "Engine",
                    $"LIVE: {ticker.Symbol}: Fehler - {ex.Message}", ticker.Symbol));
            }
        }
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
