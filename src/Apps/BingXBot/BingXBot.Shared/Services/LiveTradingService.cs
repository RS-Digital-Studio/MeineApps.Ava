using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Indicators;
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
    private readonly BingXWebSocketClient? _wsClient;

    private RiskManager? _riskManager;
    private CancellationTokenSource? _cts;
    private volatile bool _isRunning;
    private volatile bool _isPaused;
    private bool _disposed;
    private string? _listenKey;
    private PeriodicTimer? _listenKeyRenewTimer;
    private DateTime _lastDailyResetDate = DateTime.UtcNow.Date;

    /// <summary>BingX Perpetual Futures Taker Fee: 0.05% (Standard-Level).</summary>
    private const decimal TakerFeeRate = 0.0005m;

    // SL/TP-Tracking: Speichert das Original-Signal pro offener Position (Symbol_Side -> SignalResult)
    // ConcurrentDictionary weil PriceTickerLoop und ScanAndTradeAsync parallel darauf zugreifen
    private readonly ConcurrentDictionary<string, SignalResult> _positionSignals = new();
    // Zeitpunkt der Signal-Erstellung (für Grace Period bei Bereinigung verwaister Signale)
    private readonly ConcurrentDictionary<string, DateTime> _signalCreatedAt = new();

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
        BotEventBus eventBus,
        BingXWebSocketClient? wsClient = null)
    {
        _restClient = restClient;
        _publicClient = publicClient;
        _strategyManager = strategyManager;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _eventBus = eventBus;
        _wsClient = wsClient;
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

        // User-Data-Stream starten (optional, Fallback auf REST-Polling)
        if (_wsClient != null)
            _ = StartUserDataStreamAsync(_cts.Token);
    }

    /// <summary>
    /// Startet den User-Data-Stream für Echtzeit-Account/Position-Updates.
    /// ListenKey wird alle 30 Minuten erneuert.
    /// </summary>
    private async Task StartUserDataStreamAsync(CancellationToken ct)
    {
        try
        {
            _listenKey = await _restClient.CreateListenKeyAsync().ConfigureAwait(false);
            await _wsClient!.ConnectUserDataStreamAsync(_listenKey, ct).ConfigureAwait(false);

            // Handler für User-Data Events
            _wsClient.UserDataReceived += OnUserDataReceived;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
                "User-Data-Stream verbunden (Echtzeit-Updates aktiv)"));

            // ListenKey alle 30 Minuten erneuern
            _listenKeyRenewTimer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            while (await _listenKeyRenewTimer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await _restClient.RenewListenKeyAsync(_listenKey).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                        $"ListenKey-Erneuerung fehlgeschlagen: {ex.Message}"));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
                $"User-Data-Stream konnte nicht gestartet werden: {ex.Message} (Fallback: REST-Polling)"));
        }
    }

    /// <summary>Verarbeitet User-Data-Stream Events (ACCOUNT_UPDATE, ORDER_TRADE_UPDATE).</summary>
    private void OnUserDataReceived(object? sender, string message)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(message);
            var root = doc.RootElement;

            var eventType = root.TryGetProperty("e", out var eProp) ? eProp.GetString() : null;

            switch (eventType)
            {
                case "ACCOUNT_UPDATE":
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "WebSocket",
                        "Account-Update empfangen (Balance/Position geändert)"));
                    break;

                case "ORDER_TRADE_UPDATE":
                    if (root.TryGetProperty("o", out var orderData))
                    {
                        var symbol = orderData.TryGetProperty("s", out var sProp) ? sProp.GetString() : "?";
                        var status = orderData.TryGetProperty("X", out var xProp) ? xProp.GetString() : "?";
                        var side = orderData.TryGetProperty("S", out var sideProp) ? sideProp.GetString() : "?";
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "WebSocket",
                            $"Order-Update: {symbol} {side} → {status}", symbol));
                    }
                    break;
            }
        }
        catch
        {
            // Parse-Fehler ignorieren - User-Data ist optional
        }
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

        // User-Data-Stream sauber trennen
        await CleanupUserDataStreamAsync();

        // Im Live-Modus: Positionen NICHT automatisch schließen beim normalen Stop
        // Der User soll entscheiden ob er sie manuell schließen will
        _positionSignals.Clear();
        _signalCreatedAt.Clear();
        _cts?.Dispose();
        _cts = null;

        _eventBus.PublishBotState(BotState.Stopped);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Engine",
            "Live-Trading gestoppt. Offene Positionen bleiben bestehen."));
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

        // User-Data-Stream sauber trennen
        await CleanupUserDataStreamAsync();

        _positionSignals.Clear();
        _signalCreatedAt.Clear();
        _cts?.Dispose();
        _cts = null;

        _eventBus.PublishBotState(BotState.EmergencyStop);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Engine",
            "NOTFALL-STOP abgeschlossen. Prüfe dein BingX-Konto!"));
    }

    /// <summary>
    /// Räumt den User-Data-Stream sauber auf: Event-Handler abmelden, Timer stoppen,
    /// ListenKey löschen, WebSocket trennen. Wird bei Stop und EmergencyStop aufgerufen.
    /// </summary>
    private async Task CleanupUserDataStreamAsync()
    {
        // Timer stoppen
        _listenKeyRenewTimer?.Dispose();
        _listenKeyRenewTimer = null;

        // Event-Handler abmelden
        if (_wsClient != null)
            _wsClient.UserDataReceived -= OnUserDataReceived;

        // WebSocket-User-Data-Stream trennen
        if (_wsClient != null && _wsClient.IsUserDataConnected)
        {
            try { await _wsClient.DisconnectUserDataStreamAsync().ConfigureAwait(false); }
            catch { /* Best-effort beim Cleanup */ }
        }

        // ListenKey auf dem Server löschen (Best-effort)
        if (_listenKey != null)
        {
            try { await _restClient.DeleteListenKeyAsync(_listenKey).ConfigureAwait(false); }
            catch { /* Best-effort */ }
            _listenKey = null;
        }
    }

    /// <summary>Hauptschleife: Alle 30s scannen und handeln.</summary>
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
                        "LIVE: Tages-Drawdown zurückgesetzt (neuer Tag)"));
                }

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

                // Verwaiste Signale bereinigen: Wenn Position nicht mehr existiert UND Signal älter als 30s
                // Grace Period verhindert dass ein Signal entfernt wird bevor die Order auf BingX settlet
                if (_positionSignals.Count > 0)
                {
                    var positionKeys = new HashSet<string>(positions.Select(p => $"{p.Symbol}_{p.Side}"));
                    var now = DateTime.UtcNow;
                    foreach (var key in _positionSignals.Keys)
                    {
                        if (!positionKeys.Contains(key))
                        {
                            // Nur entfernen wenn Signal älter als 30 Sekunden (API-Latenz-Grace-Period)
                            if (_signalCreatedAt.TryGetValue(key, out var createdAt) && (now - createdAt).TotalSeconds > 30)
                            {
                                _positionSignals.TryRemove(key, out _);
                                _signalCreatedAt.TryRemove(key, out _);
                            }
                        }
                    }
                }

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
                            _signalCreatedAt.TryRemove(key, out _);

                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                                $"LIVE: {pos.Symbol}: {reason} ({pos.Side})", pos.Symbol));

                            // CompletedTrade erstellen für TradeHistory
                            // Fees: BingX Taker Fee 0.05% pro Seite (Entry-Preis + Exit-Preis separat)
                            var entryFee = pos.Quantity * pos.EntryPrice * TakerFeeRate;
                            var exitFee = pos.Quantity * price * TakerFeeRate;
                            var totalFee = entryFee + exitFee;
                            var rawPnl = pos.Side == Side.Buy
                                ? (price - pos.EntryPrice) * pos.Quantity
                                : (pos.EntryPrice - price) * pos.Quantity;
                            var pnl = rawPnl - totalFee;
                            var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, price,
                                pos.Quantity, pnl, totalFee, pos.OpenTime, DateTime.UtcNow, reason, TradingMode.Live);
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
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Error, "PriceTicker",
                    $"LIVE PriceTicker Fehler: {ex.Message}"));
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

        // 1. Ticker holen und filtern
        var tickers = await _publicClient.GetAllTickersAsync(ct).ConfigureAwait(false);
        if (tickers.Count == 0) return;

        var candidates = ScanHelper.FilterCandidates(tickers, _scannerSettings);
        if (candidates.Count == 0) return;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Debug, "Scanner",
            $"{candidates.Count} Kandidaten"));

        // 2. Account + Positionen von BingX holen
        var account = await _restClient.GetAccountInfoAsync().ConfigureAwait(false);
        var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);

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
                            var closeKey = $"{ticker.Symbol}_{closeSide}";
                            _positionSignals.TryRemove(closeKey, out _);
                            _signalCreatedAt.TryRemove(closeKey, out _);

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

                // Korrelations-Check + Risk-Check (gemeinsame Logik via ScanHelper)
                if (await ScanHelper.CheckCorrelationAsync(
                    ticker.Symbol, positions, _riskSettings, _publicClient, result.Candles, _eventBus, "LIVE: ", ct))
                    continue;

                var riskCheck = ScanHelper.ValidateRisk(signal, context, _riskManager, _eventBus, "LIVE: ");
                if (!riskCheck.IsAllowed) continue;

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
                    var signalKey = $"{ticker.Symbol}_{side}";
                    _positionSignals[signalKey] = signal;
                    _signalCreatedAt[signalKey] = DateTime.UtcNow;

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Trade, "Trade",
                        $"LIVE ORDER: {ticker.Symbol} {side} {riskCheck.AdjustedPositionSize:F6} @ {ticker.LastPrice:N2}",
                        ticker.Symbol));

                    // RiskManager aktualisieren (Eröffnungs-Fee als initialer Verlust)
                    var entryNotional = riskCheck.AdjustedPositionSize * ticker.LastPrice;
                    var entryFee = entryNotional * TakerFeeRate;
                    _riskManager.UpdateDailyStats(new CompletedTrade(
                        ticker.Symbol, side, ticker.LastPrice, ticker.LastPrice,
                        riskCheck.AdjustedPositionSize, -entryFee, entryFee,
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
        var key = $"{symbol}_{side}";
        _positionSignals.TryRemove(key, out _);
        _signalCreatedAt.TryRemove(key, out _);
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

        if (_wsClient != null)
            _wsClient.UserDataReceived -= OnUserDataReceived;

        _listenKeyRenewTimer?.Dispose();
        _listenKeyRenewTimer = null;

        // ListenKey löschen (Best-effort, nicht awaiten in Dispose)
        if (_listenKey != null)
        {
            _ = _restClient.DeleteListenKeyAsync(_listenKey);
            _listenKey = null;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
