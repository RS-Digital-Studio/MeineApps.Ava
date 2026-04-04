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
/// Erbt die gesamte Scan-/SL/TP-/Trailing-Stop-Logik von TradingServiceBase.
/// Nutzt BingXRestClient für Orders und IPublicMarketDataClient für Marktdaten.
/// WARNUNG: Echtes Geld! Nur mit ausreichendem Paper-Testing verwenden.
/// </summary>
public class LiveTradingService : TradingServiceBase
{
    private readonly BingXRestClient _restClient;
    private readonly BingXWebSocketClient? _wsClient;
    private readonly BotDatabaseService? _dbService;

    /// <summary>BingX Perpetual Futures Taker Fee: 0.05% (Standard-Level).</summary>
    private const decimal TakerFeeRate = 0.0005m;

    // Zeitpunkt der Signal-Erstellung (für Grace Period bei Bereinigung verwaister Signale)
    private readonly ConcurrentDictionary<string, DateTime> _signalCreatedAt = new();
    // Eröffnungszeitpunkt pro Position (BingX liefert kein OpenTime in GetPositions)
    private readonly ConcurrentDictionary<string, DateTime> _positionOpenTimes = new();
    private string? _listenKey;
    private PeriodicTimer? _listenKeyRenewTimer;
    private DateTime _lastFundingRateUpdate = DateTime.MinValue;

    protected override string LogPrefix => "LIVE: ";
    protected override string ModeName => "Live-Trading";

    public LiveTradingService(
        BingXRestClient restClient,
        IPublicMarketDataClient publicClient,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus,
        BotSettings botSettings,
        BingXWebSocketClient? wsClient = null,
        BotDatabaseService? dbService = null)
        : base(publicClient, strategyManager, riskSettings, scannerSettings, eventBus, botSettings)
    {
        _restClient = restClient;
        _wsClient = wsClient;
        _dbService = dbService;
    }

    /// <summary>Startet das Live-Trading.</summary>
    public void Start()
    {
        if (_isRunning) return;

        _eventBus.PublishBotState(BotState.Running);
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
            "LIVE-TRADING GESTARTET - Echtes Geld! Überwache den Bot sorgfältig."));

        StartBase(new RiskManager(_riskSettings, NullLogger<RiskManager>.Instance));

        // User-Data-Stream starten (optional, Fallback auf REST-Polling)
        if (_wsClient != null)
            _ = StartUserDataStreamAsync(_cts!.Token);
    }

    /// <summary>
    /// Stoppt das Live-Trading. Offene Positionen bleiben bestehen (User entscheidet).
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;
        _cts?.Cancel();

        await CleanupUserDataStreamAsync();

        StopBase(BotState.Stopped, "Live-Trading gestoppt. Offene Positionen bleiben bestehen.");
    }

    /// <summary>
    /// Notfall-Stop: ALLE echten Positionen auf BingX sofort schließen!
    /// </summary>
    public async Task EmergencyStopAsync()
    {
        _cts?.Cancel();

        // Emergency: ALLE Positionen sofort schließen!
        try
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                "NOTFALL-STOP: Schließe alle Positionen auf BingX..."));

            var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
            // Ticker für Exit-Preise holen (ein API-Call)
            var tickers = await _publicClient.GetAllTickersAsync().ConfigureAwait(false);
            var tickerMap = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);

            // Alle Positionen parallel schließen (schnellstmöglich bei Notfall)
            var closeTasks = positions.Select(pos => Task.Run(async () =>
            {
                try
                {
                    await _restClient.ClosePositionAsync(pos.Symbol, pos.Side).ConfigureAwait(false);

                    // CompletedTrade erstellen damit ATI + RiskManager Feedback bekommen
                    var exitPrice = tickerMap.GetValueOrDefault(pos.Symbol, pos.MarkPrice);
                    var entryFee = pos.Quantity * pos.EntryPrice * TakerFeeRate;
                    var exitFee = pos.Quantity * exitPrice * TakerFeeRate;
                    var totalFee = entryFee + exitFee;
                    var rawPnl = pos.Side == Side.Buy
                        ? (exitPrice - pos.EntryPrice) * pos.Quantity
                        : (pos.EntryPrice - exitPrice) * pos.Quantity;
                    var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, exitPrice,
                        pos.Quantity, rawPnl - totalFee, totalFee, pos.OpenTime, DateTime.UtcNow,
                        "Notfall-Stop", TradingMode.Live);
                    _eventBus.PublishTrade(trade);
                    ProcessCompletedTrade(trade);

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                        $"NOTFALL: {pos.Symbol} {pos.Side} geschlossen (PnL: {trade.Pnl:+0.00;-0.00})", pos.Symbol));
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                        $"FEHLER: {pos.Symbol} konnte nicht geschlossen werden: {ex.Message}", pos.Symbol));
                }
            }));
            await Task.WhenAll(closeTasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                $"FEHLER beim Laden der Positionen: {ex.Message}"));
        }

        await CleanupUserDataStreamAsync();

        StopBase(BotState.EmergencyStop, "NOTFALL-STOP abgeschlossen. Prüfe dein BingX-Konto!");
    }

    // ═══════════════════════════════════════════════════════════════
    // Abstrakte Methoden implementieren
    // ═══════════════════════════════════════════════════════════════

    protected override Task<AccountInfo> GetAccountAsync() =>
        _restClient.GetAccountInfoAsync();

    protected override Task<IReadOnlyList<Position>> GetPositionsForScanAsync() =>
        _restClient.GetPositionsAsync();

    protected override Task<IReadOnlyList<Position>> GetPositionsForTickerAsync() =>
        _restClient.GetPositionsAsync();

    protected override void SetCurrentPriceIfNeeded(string symbol, decimal price)
    {
        // Live-Trading: Preis wird nicht gesetzt (echte Exchange hat eigene Preise)
    }

    protected override async Task<bool> PlaceOrderOnExchangeAsync(Ticker ticker, Side side, decimal quantity, SignalResult? signal = null)
    {
        try
        {
            // Leverage setzen
            await _restClient.SetLeverageAsync(ticker.Symbol, (int)_riskSettings.MaxLeverage, side)
                .ConfigureAwait(false);

            // Order platzieren (mit nativen SL/TP wenn Signal vorhanden)
            var order = await _restClient.PlaceOrderAsync(new OrderRequest(
                ticker.Symbol, side, OrderType.Market, quantity,
                StopLoss: signal?.StopLoss,
                TakeProfit: signal?.TakeProfit))
                .ConfigureAwait(false);

            if (order.Status == OrderStatus.Rejected)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE ORDER ABGELEHNT: {ticker.Symbol} {side}", ticker.Symbol));
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                $"LIVE ORDER FEHLGESCHLAGEN: {ticker.Symbol} {side} - {ex.Message}", ticker.Symbol));
            return false;
        }
    }

    protected override async Task ClosePositionAndPublishAsync(string symbol, Side side)
    {
        try
        {
            // Position-Daten VOR dem Close lesen (für CompletedTrade)
            var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
            var pos = positions.FirstOrDefault(p => p.Symbol == symbol && p.Side == side);

            await _restClient.ClosePositionAsync(symbol, side).ConfigureAwait(false);
            RemoveSignalByKey($"{symbol}_{side}");

            // CompletedTrade erstellen damit ATI + RiskManager Feedback bekommen
            if (pos != null)
            {
                var tickers = await _publicClient.GetAllTickersAsync().ConfigureAwait(false);
                var exitPrice = tickers.FirstOrDefault(t => t.Symbol == symbol)?.LastPrice ?? pos.MarkPrice;
                var entryFee = pos.Quantity * pos.EntryPrice * TakerFeeRate;
                var exitFee = pos.Quantity * exitPrice * TakerFeeRate;
                var totalFee = entryFee + exitFee;
                var rawPnl = side == Side.Buy
                    ? (exitPrice - pos.EntryPrice) * pos.Quantity
                    : (pos.EntryPrice - exitPrice) * pos.Quantity;
                var posKey = $"{symbol}_{side}";
                var entryTime = _positionOpenTimes.GetValueOrDefault(posKey, pos.OpenTime);
                var trade = new CompletedTrade(symbol, side, pos.EntryPrice, exitPrice,
                    pos.Quantity, rawPnl - totalFee, totalFee, entryTime, DateTime.UtcNow,
                    "Close-Signal", TradingMode.Live);
                _eventBus.PublishTrade(trade);
                ProcessCompletedTrade(trade);
            }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                $"LIVE: {symbol} {side} geschlossen", symbol));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                $"LIVE: {symbol} schließen fehlgeschlagen: {ex.Message}", symbol));
        }
    }

    protected override async Task OnSlTpHitAsync(Position pos, decimal price, string key, string reason, bool isStopLoss)
    {
        try
        {
            // K-2 Fix: Signal ZUERST entfernen, dann schließen.
            // Verhindert doppelten Trigger wenn BingX die Position bereits nativ geschlossen hat.
            RemoveSignalByKey(key);

            // Prüfen ob die Position noch existiert (BingX könnte sie nativ per SL/TP geschlossen haben)
            var currentPositions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
            var stillExists = currentPositions.Any(p => p.Symbol == pos.Symbol && p.Side == pos.Side);
            if (!stillExists)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"LIVE: {pos.Symbol} {pos.Side} bereits durch native SL/TP geschlossen", pos.Symbol));
                return;
            }

            await _restClient.ClosePositionAsync(pos.Symbol, pos.Side).ConfigureAwait(false);

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
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
            // Echte Eröffnungszeit aus _positionOpenTimes (BingX liefert kein OpenTime)
            var entryTime = _positionOpenTimes.GetValueOrDefault(key, pos.OpenTime);
            var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, price,
                pos.Quantity, pnl, totalFee, entryTime, DateTime.UtcNow, reason, TradingMode.Live);
            _eventBus.PublishTrade(trade);
            ProcessCompletedTrade(trade);
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                $"LIVE: {pos.Symbol}: {reason} FEHLGESCHLAGEN - {ex.Message}", pos.Symbol));
        }
    }

    protected override async Task OnPartialCloseAsync(Position pos, decimal price, decimal quantityToClose)
    {
        try
        {
            // Gegenorder mit reduzierter Menge = Partial Close
            var closeSide = pos.Side == Side.Buy ? Side.Sell : Side.Buy;
            await _restClient.PlaceOrderAsync(new OrderRequest(
                pos.Symbol, closeSide, OrderType.Market, quantityToClose))
                .ConfigureAwait(false);

            // CompletedTrade für den geschlossenen Teil
            var entryFee = quantityToClose * pos.EntryPrice * TakerFeeRate;
            var exitFee = quantityToClose * price * TakerFeeRate;
            var totalFee = entryFee + exitFee;
            var rawPnl = pos.Side == Side.Buy
                ? (price - pos.EntryPrice) * quantityToClose
                : (pos.EntryPrice - price) * quantityToClose;
            var key = $"{pos.Symbol}_{pos.Side}";
            var entryTime = _positionOpenTimes.GetValueOrDefault(key, pos.OpenTime);
            var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, price,
                quantityToClose, rawPnl - totalFee, totalFee, entryTime, DateTime.UtcNow,
                "Partial Close (TP1)", TradingMode.Live);
            _eventBus.PublishTrade(trade);
            ProcessCompletedTrade(trade);

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                $"LIVE: {pos.Symbol} Partial Close {quantityToClose:G6} @ {price:G8}", pos.Symbol));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                $"LIVE: {pos.Symbol} Partial Close FEHLGESCHLAGEN: {ex.Message}", pos.Symbol));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Virtuelle Hooks überschreiben
    // ═══════════════════════════════════════════════════════════════

    protected override void OnSignalRemoved(string key)
    {
        _signalCreatedAt.TryRemove(key, out _);
        _positionOpenTimes.TryRemove(key, out _);
    }

    protected override void OnSignalsClearedAll()
    {
        _signalCreatedAt.Clear();
        _positionOpenTimes.Clear();
    }

    protected override void OnSignalCreated(string key)
    {
        _signalCreatedAt[key] = DateTime.UtcNow;
        _positionOpenTimes[key] = DateTime.UtcNow;
    }

    /// <summary>Bei API-Fehler: 60s warten statt 30s (Rate-Limit-Schutz).</summary>
    protected override Task OnScanErrorAsync(CancellationToken ct) =>
        Task.Delay(60_000, ct);

    /// <summary>Verwaiste Signale bereinigen (Grace Period 30s) + Funding-Rate aktualisieren.</summary>
    protected override async Task OnBeforePriceTickerIteration(IReadOnlyList<Position> positions)
    {
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
                        RemoveSignalByKey(key);
                }
            }
        }

        // Funding-Rate periodisch aktualisieren (alle 5 Min, über PriceTicker-Zähler)
        if ((DateTime.UtcNow - _lastFundingRateUpdate).TotalMinutes >= 5 && positions.Count > 0)
        {
            try
            {
                // Funding-Rate für das erste offene Symbol abfragen (alle BingX-Symbole haben ähnliche Rates)
                var rate = await _restClient.GetFundingRateAsync(positions[0].Symbol).ConfigureAwait(false);
                _currentFundingRate = rate;
                _lastFundingRateUpdate = DateTime.UtcNow;
            }
            catch { /* Funding-Rate-Abfrage ist optional */ }
        }
    }

    protected override Task OnOrderPlacedAsync(Ticker ticker, Side side, decimal quantity)
    {
        // Entry-Fee nur loggen, KEINEN CompletedTrade publizieren
        // (Ghost-Trade + doppelte Fee-Zählung vermeiden - Fee wird beim Close eingerechnet)
        var entryNotional = quantity * ticker.LastPrice;
        var entryFee = entryNotional * TakerFeeRate;
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
            $"LIVE: {ticker.Symbol} Entry-Fee: {entryFee:N4} USDT", ticker.Symbol));
        return Task.CompletedTask;
    }

    protected override void OnTrailingStopMoved(string symbol, decimal oldSl, decimal newSl)
    {
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
            $"LIVE: {symbol} Trailing-SL nachgezogen: {oldSl:N2} → {newSl:N2}", symbol));
    }

    /// <summary>ATI-Lernzustand periodisch in DB persistieren (Schutz gegen App-Crash).</summary>
    protected override async Task OnAtiAutoSaveAsync()
    {
        if (_dbService == null || _ati == null) return;
        try
        {
            var stateJson = _ati.SerializeState();
            await _dbService.SaveAtiStateAsync(stateJson).ConfigureAwait(false);
            if (_eventBus.HasLogSubscribers)
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "ATI",
                    "ATI-Lernzustand automatisch gespeichert"));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "ATI",
                $"ATI Auto-Save fehlgeschlagen: {ex.Message}"));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // WebSocket User-Data-Stream (Live-spezifisch)
    // ═══════════════════════════════════════════════════════════════

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

            _wsClient.UserDataReceived += OnUserDataReceived;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
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
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                        $"ListenKey-Erneuerung fehlgeschlagen: {ex.Message}"));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
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
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "WebSocket",
                        "Account-Update empfangen (Balance/Position geändert)"));
                    break;

                case "ORDER_TRADE_UPDATE":
                    if (root.TryGetProperty("o", out var orderData))
                    {
                        var symbol = orderData.TryGetProperty("s", out var sProp) ? sProp.GetString() : "?";
                        var status = orderData.TryGetProperty("X", out var xProp) ? xProp.GetString() : "?";
                        var oSide = orderData.TryGetProperty("S", out var sideProp) ? sideProp.GetString() : "?";
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "WebSocket",
                            $"Order-Update: {symbol} {oSide} → {status}", symbol));
                    }
                    break;
            }
        }
        catch
        {
            // Parse-Fehler ignorieren - User-Data ist optional
        }
    }

    /// <summary>
    /// Räumt den User-Data-Stream sauber auf: Event-Handler abmelden, Timer stoppen,
    /// ListenKey löschen, WebSocket trennen.
    /// </summary>
    private async Task CleanupUserDataStreamAsync()
    {
        _listenKeyRenewTimer?.Dispose();
        _listenKeyRenewTimer = null;

        if (_wsClient != null)
            _wsClient.UserDataReceived -= OnUserDataReceived;

        if (_wsClient != null && _wsClient.IsUserDataConnected)
        {
            try { await _wsClient.DisconnectUserDataStreamAsync().ConfigureAwait(false); }
            catch { /* Best-effort beim Cleanup */ }
        }

        if (_listenKey != null)
        {
            try { await _restClient.DeleteListenKeyAsync(_listenKey).ConfigureAwait(false); }
            catch { /* Best-effort */ }
            _listenKey = null;
        }
    }

    protected override void DisposeAdditional()
    {
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
    }
}
