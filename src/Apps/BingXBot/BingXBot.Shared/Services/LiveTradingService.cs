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
/// Erbt die gesamte Scan-/SL/TP-/BE-Logik von TradingServiceBase.
/// Nutzt BingXRestClient für Orders und IPublicMarketDataClient für Marktdaten.
/// WARNUNG: Echtes Geld! Nur mit ausreichendem Paper-Testing verwenden.
/// </summary>
public class LiveTradingService : TradingServiceBase
{
    private readonly BingXRestClient _restClient;
    private readonly BingXWebSocketClient? _wsClient;
    private readonly BotDatabaseService? _dbService;

    /// <summary>BingX Taker Fee — wird beim Start via API gelesen, Fallback 0.05%.</summary>
    private decimal _takerFeeRate = 0.0005m;
    private decimal _makerFeeRate = 0.0002m;

    /// <summary>Setzt echte Maker/Taker-Fees vom BingX Account (statt Fallback-Werten).</summary>
    public void SetCommissionRates(decimal takerRate, decimal makerRate)
    {
        _takerFeeRate = takerRate;
        _makerFeeRate = makerRate;
    }

    /// <summary>Kill-Switch: Letzter Refresh-Zeitpunkt (alle 60s erneuern).</summary>
    private DateTime _lastKillSwitchRefresh = DateTime.MinValue;
    private const int KillSwitchTimeoutMs = 120_000;  // 2 Minuten bis Auto-Cancel
    private const int KillSwitchRefreshIntervalSeconds = 60; // Alle 60s refreshen
    private bool _killSwitchDisabled; // True wenn Endpoint nicht unterstützt wird (nach erstem Fehler deaktiviert)

    // Zeitpunkt der Signal-Erstellung (für Grace Period bei Bereinigung verwaister Signale)
    private readonly ConcurrentDictionary<string, DateTime> _signalCreatedAt = new();
    // Eröffnungszeitpunkt pro Position (BingX liefert kein OpenTime in GetPositions)
    private readonly ConcurrentDictionary<string, DateTime> _positionOpenTimes = new();
    private string? _listenKey;
    private PeriodicTimer? _listenKeyRenewTimer;
    private DateTime _lastFundingRateUpdate = DateTime.MinValue;
    // WebSocket-Ticker: Echtzeit-Preise für schnellere SL/TP-Prüfung
    private readonly ConcurrentDictionary<string, decimal> _wsTickerPrices = new();
    /// <summary>True wenn der WebSocket-Ticker-Stream aktiv ist (für schnellere SL/TP-Prüfung).</summary>
    public bool IsWsTickerActive { get; private set; }
    // SK-Buch Workflow 5.3: "Entry wird solange getradet wie er valide ist."
    // Invalidation-Level = Sequenz-Invalidierung (Preis jenseits Point0 ≈ StopLoss).
    // Wenn Preis den Invalidation-Level erreicht BEVOR die Limit-Entry gefüllt wurde → cancel.
    private readonly ConcurrentDictionary<string, (string OrderId, DateTime PlacedAt, decimal InvalidationLevel, bool IsLong)> _pendingLimitOrders = new();
    // Hard-Expiry (Safety-Net): Limit-Order läuft nach 48h ab selbst ohne Invalidierung
    // (Schutz gegen "vergessene" Orders bei Daten-/API-Ausfall, keine Buch-Regel).
    private const int LimitOrderHardExpiryHours = 48;
    // Throttle für SL-Sync auf BingX nach BE-Anpassung (max 1 API-Call pro 30s pro Symbol)
    private readonly ConcurrentDictionary<string, DateTime> _lastTrailingSyncTimes = new();
    // WebSocket-Ticker Event-Handler (gespeichert für sauberes Abmelden in Dispose)
    private Action<string, decimal>? _tickerPriceHandler;

    protected override string LogPrefix => ModePrefix.Length > 0 ? $"LIVE {ModePrefix}" : "LIVE: ";
    protected override string ModeName => "Live-Trading";

    /// <summary>
    /// Modus-Prefix für Log-Nachrichten im Multi-Mode (z.B. "[S] ").
    /// </summary>
    public string ModePrefix { get; set; } = "";

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

        // WebSocket: User-Data-Stream + Ticker-Stream starten (async Wrapper mit try/catch,
        // kein ContinueWith das AggregateException verpackt oder TaskScheduler-Probleme hat)
        if (_wsClient != null)
        {
            _ = SafeStartAsync("User-Data-Stream", () => StartUserDataStreamAsync(_cts!.Token));
            _ = SafeStartAsync("Ticker-Stream", StartTickerStreamAsync);
        }
    }

    /// <summary>
    /// Stoppt das Live-Trading. Offene Positionen bleiben bestehen (User entscheidet).
    /// </summary>
    public override async Task StopAsync()
    {
        if (!_isRunning) return;

        // Cleanup VOR Cancel: DeleteListenKey braucht ein nicht-gecancelltes CancellationToken
        await CleanupUserDataStreamAsync();

        _cts?.Cancel();
        StopBase(BotState.Stopped, "Live-Trading gestoppt. Offene Positionen bleiben bestehen.");
    }

    /// <summary>
    /// Notfall-Stop: ALLE echten Positionen auf BingX sofort schließen!
    /// </summary>
    public override async Task EmergencyStopAsync()
    {
        // CTS NICHT hier canceln — GetPositionsAsync/ClosePositionAsync brauchen funktionierende HTTP-Calls.
        // StopBase() am Ende cancelt den CTS sicher.

        // Emergency: ALLE Positionen sofort schließen!
        // Dedizierter CTS mit 10s Timeout — bei Netzwerkproblemen nicht 90s blockieren
        using var emergencyCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ect = emergencyCts.Token;
        try
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                "NOTFALL-STOP: Schließe alle Positionen auf BingX..."));

            var positions = await _restClient.GetPositionsAsync(ect).ConfigureAwait(false);
            // Ticker für Exit-Preise holen (ein API-Call)
            var tickers = await _publicClient.GetAllTickersAsync(ect).ConfigureAwait(false);
            var tickerMap = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);

            // Zuerst alle offenen Conditional Orders canceln (native SL/TP-Orders),
            // damit sie nicht als Ghost-Orders bestehen bleiben nach dem Position-Close
            try
            {
                // Timeout via ect: GetOpenOrdersAsync hat keinen CT-Parameter,
                // daher Task.WhenAny mit Timeout als Safety-Net
                var orderTask = _restClient.GetOpenOrdersAsync();
                var completed = await Task.WhenAny(orderTask, Task.Delay(5000, ect)).ConfigureAwait(false);
                var openOrders = completed == orderTask ? await orderTask : Array.Empty<Order>();
                foreach (var order in openOrders)
                {
                    if (order.Type is Core.Enums.OrderType.StopMarket or Core.Enums.OrderType.TakeProfitMarket or Core.Enums.OrderType.TakeProfitLimit)
                    {
                        try { await _restClient.CancelOrderAsync(order.OrderId, order.Symbol).ConfigureAwait(false); }
                        catch { /* Best-effort: Order koennte bereits gecancelt sein */ }
                    }
                }
            }
            catch (Exception cancelEx)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                    $"Konnte offene Conditional Orders nicht canceln: {cancelEx.Message}"));
            }

            // Alle Positionen parallel schließen (schnellstmöglich bei Notfall)
            var closeTasks = positions.Select(pos => Task.Run(async () =>
            {
                try
                {
                    await _restClient.ClosePositionAsync(pos.Symbol, pos.Side).ConfigureAwait(false);

                    // CompletedTrade erstellen damit RiskManager Feedback bekommt
                    var exitPrice = tickerMap.GetValueOrDefault(pos.Symbol, pos.MarkPrice);
                    var entryFee = pos.Quantity * pos.EntryPrice * _takerFeeRate;
                    var exitFee = pos.Quantity * exitPrice * _takerFeeRate;
                    var totalFee = entryFee + exitFee;
                    var rawPnl = pos.Side == Side.Buy
                        ? (exitPrice - pos.EntryPrice) * pos.Quantity
                        : (pos.EntryPrice - exitPrice) * pos.Quantity;
                    var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, exitPrice,
                        pos.Quantity, rawPnl - totalFee, totalFee, pos.OpenTime, DateTime.UtcNow,
                        "Notfall-Stop", TradingMode.Live);
                    // Trade-Outcome ZUERST verarbeiten, DANN EventBus → Dashboard-Snapshot sieht aktuelle Counter
                    ProcessCompletedTrade(trade);
                    _eventBus.PublishTrade(trade);

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

    protected override async Task<bool> PlaceOrderOnExchangeAsync(Ticker ticker, Side side, decimal quantity, SignalResult? signal = null, int adaptiveLeverage = 0)
    {
        try
        {
            // SK-VERIFY: [6.3] Isolated Margin VOR jeder Order sicherstellen
            // Ohne Isolated Margin kann ein einzelner Trade das gesamte Konto liquidieren
            try
            {
                await _restClient.SetMarginTypeAsync(ticker.Symbol, MarginType.Isolated)
                    .ConfigureAwait(false);
            }
            catch (Exception marginEx)
            {
                // SK-VERIFY: [6.3] Erwartete Fehler: Position offen (MarginType nicht änderbar)
                // oder bereits Isolated. Unerwartete Fehler loggen.
                var msg = marginEx.Message;
                if (!msg.Contains("isolated", StringComparison.OrdinalIgnoreCase)
                    && !msg.Contains("position", StringComparison.OrdinalIgnoreCase)
                    && !msg.Contains("margin type", StringComparison.OrdinalIgnoreCase))
                {
                    _eventBus.PublishLog(new Core.Models.LogEntry(DateTime.UtcNow,
                        Core.Enums.LogLevel.Warning, "Exchange",
                        $"{ModePrefix}{ticker.Symbol}: SetMarginType(Isolated) fehlgeschlagen: {msg}",
                        ticker.Symbol));
                }
            }

            // Leverage setzen (adaptiv oder kategoriespezifisch)
            var category = Core.Helpers.SymbolClassifier.Classify(ticker.Symbol);
            var catMaxLev = (int)_riskSettings.GetCategorySettings(category).MaxLeverage;
            var leverage = adaptiveLeverage > 0
                ? Math.Min(adaptiveLeverage, catMaxLev)
                : catMaxLev;
            await _restClient.SetLeverageAsync(ticker.Symbol, leverage, side)
                .ConfigureAwait(false);

            // Order platzieren: Limit wenn bevorzugt und Entry-Preis vorhanden, sonst Market
            var useLimit = signal?.PreferLimitOrder == true && signal.EntryPrice.HasValue && signal.EntryPrice.Value > 0;
            var orderType = useLimit ? OrderType.Limit : OrderType.Market;
            var limitPrice = useLimit ? signal!.EntryPrice : null;

            if (useLimit)
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"{ticker.Symbol}: Limit-Order bei {limitPrice:F8} (Pullback-Entry, Maker-Fee)", ticker.Symbol));

            // TP wird NICHT im Haupt-Order gesetzt — stattdessen separate TP-Market-Orders
            // mit spezifischer Quantity (TP1 30% bei 161.8%, TP2 Rest bei 200%)
            // Nativer TP auf Haupt-Order würde 100% schließen und Partial-Close überschreiben
            var order = await _restClient.PlaceOrderAsync(new OrderRequest(
                ticker.Symbol, side, orderType, quantity,
                Price: limitPrice,
                StopLoss: signal?.StopLoss,
                TakeProfit: null),
                lastPrice: ticker.LastPrice)
                .ConfigureAwait(false);

            if (order.Status == OrderStatus.Rejected)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE ORDER ABGELEHNT: {ticker.Symbol} {side}", ticker.Symbol));
                return false;
            }

            // SK-Buch Workflow 5.3: Limit-Order bleibt valid bis Sequenz invalid wird.
            // Invalidation-Level = SignalResult.StopLoss (78.6er gecappt, ≈ Point0).
            if (useLimit && order.OrderId != null && signal?.StopLoss.HasValue == true)
            {
                var isLong = side == Side.Buy;
                _pendingLimitOrders[ticker.Symbol] = (order.OrderId, DateTime.UtcNow, signal.StopLoss.Value, isLong);
            }

            // TP1 + TP2 als LIMIT Reduce-Only Orders auf BingX (stackbar, Maker-Fee 0.02%)
            // Reguläre LIMIT-Orders mit reduceOnly=true: BingX erlaubt beliebig viele pro Position.
            // Bei Entry-Limit-Orders: Überspringen — Position existiert noch nicht (pending).
            // TP wird im PriceTickerLoop nachgeholt sobald die Limit-Order gefüllt ist.
            if (!useLimit && signal?.TakeProfit.HasValue == true && signal.TakeProfit.Value > 0)
            {
                await PlaceTpLimitOrdersAfterFillAsync(ticker.Symbol, side, quantity, signal).ConfigureAwait(false);
            }
            else if (useLimit && signal?.TakeProfit.HasValue == true)
            {
                // Explizit zeigen dass TP bei Limit-Pending noch NICHT auf BingX ist — erst nach Fill.
                // Ohne diesen Hinweis interpretieren User das Trade-Log ("TP1=... | TP2=...") fälschlich
                // als "TP ist gesetzt" und suchen sie vergeblich im BingX-Orderbuch.
                var tp1Str = signal.TakeProfit.Value.ToString("F8");
                var tp2Str = signal.TakeProfit2.HasValue ? signal.TakeProfit2.Value.ToString("F8") : "---";
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"LIVE: {ticker.Symbol} Limit-Order pending @ {limitPrice:F8} — TP1={tp1Str}, TP2={tp2Str} werden erst NACH Fill auf BingX platziert (Maker-Fee, nicht jetzt sichtbar im Orderbuch)",
                    ticker.Symbol));
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

            // Erst Close, dann Cancel (sicherer: bei Close-Fehler bleibt nativer SL als Schutz)
            await _restClient.ClosePositionAsync(symbol, side).ConfigureAwait(false);
            RemoveSignalByKey($"{symbol}_{side}");
            try { await CancelNativeSlTpOrdersAsync(symbol).ConfigureAwait(false); }
            catch { /* Verwaiste Orders sind ungefährlich */ }

            // CompletedTrade erstellen damit RiskManager Feedback bekommt
            if (pos != null)
            {
                var tickers = await _publicClient.GetAllTickersAsync().ConfigureAwait(false);
                var exitPrice = tickers.FirstOrDefault(t => t.Symbol == symbol)?.LastPrice ?? pos.MarkPrice;
                var entryFee = pos.Quantity * pos.EntryPrice * _takerFeeRate;
                var exitFee = pos.Quantity * exitPrice * _takerFeeRate;
                var totalFee = entryFee + exitFee;
                var rawPnl = side == Side.Buy
                    ? (exitPrice - pos.EntryPrice) * pos.Quantity
                    : (pos.EntryPrice - exitPrice) * pos.Quantity;
                var posKey = $"{symbol}_{side}";
                var entryTime = _positionOpenTimes.GetValueOrDefault(posKey, pos.OpenTime);
                var trade = new CompletedTrade(symbol, side, pos.EntryPrice, exitPrice,
                    pos.Quantity, rawPnl - totalFee, totalFee, entryTime, DateTime.UtcNow,
                    "Close-Signal", TradingMode.Live);
                ProcessCompletedTrade(trade);
                _eventBus.PublishTrade(trade);
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
                // Verwaiste native SL/TP-Orders canceln (Position bereits geschlossen)
                await CancelNativeSlTpOrdersAsync(pos.Symbol).ConfigureAwait(false);

                // CompletedTrade trotzdem erstellen (TradeHistory + RiskManager)
                var entryFeeNat = pos.Quantity * pos.EntryPrice * _takerFeeRate;
                var exitFeeNat = pos.Quantity * price * _takerFeeRate;
                var rawPnlNat = pos.Side == Side.Buy
                    ? (price - pos.EntryPrice) * pos.Quantity
                    : (pos.EntryPrice - price) * pos.Quantity;
                var entryTimeNat = _positionOpenTimes.GetValueOrDefault(key, pos.OpenTime);
                var tradeNat = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, price,
                    pos.Quantity, rawPnlNat - entryFeeNat - exitFeeNat, entryFeeNat + exitFeeNat,
                    entryTimeNat, DateTime.UtcNow, $"Native {reason}", TradingMode.Live);
                ProcessCompletedTrade(tradeNat);
                _eventBus.PublishTrade(tradeNat);
                return;
            }

            // Erst Position schließen, DANN native Orders canceln.
            // Reihenfolge bewusst: Wenn Close fehlschlägt, bleibt der native SL als Schutz!
            // Verwaiste Orders nach erfolgreichem Close sind ungefährlich.
            await _restClient.ClosePositionAsync(pos.Symbol, pos.Side).ConfigureAwait(false);
            // Position erfolgreich geschlossen → jetzt verwaiste SL/TP-Orders aufräumen
            try { await CancelNativeSlTpOrdersAsync(pos.Symbol).ConfigureAwait(false); }
            catch { /* Best-effort: Verwaiste Orders sind ungefährlich */ }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                $"LIVE: {pos.Symbol}: {reason} ({pos.Side})", pos.Symbol));

            // CompletedTrade erstellen für TradeHistory
            var entryFee = pos.Quantity * pos.EntryPrice * _takerFeeRate;
            var exitFee = pos.Quantity * price * _takerFeeRate;
            var totalFee = entryFee + exitFee;
            var rawPnl = pos.Side == Side.Buy
                ? (price - pos.EntryPrice) * pos.Quantity
                : (pos.EntryPrice - price) * pos.Quantity;
            var pnl = rawPnl - totalFee;
            var entryTime = _positionOpenTimes.GetValueOrDefault(key, pos.OpenTime);
            var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, price,
                pos.Quantity, pnl, totalFee, entryTime, DateTime.UtcNow, reason, TradingMode.Live);
            ProcessCompletedTrade(trade);
            _eventBus.PublishTrade(trade);
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
            // Partial Close: Gegenorder mit reduzierter Menge
            // WICHTIG: Im Hedge-Mode muss positionSide die ORIGINAL-Seite der Position sein (LONG/SHORT),
            // nicht die Close-Seite. ClosePositionAsync verwendet dafuer die Original-Side.
            // PlaceOrderAsync leitet positionSide aus request.Side ab (Buy→LONG, Sell→SHORT),
            // daher muss hier die Original-Side verwendet werden damit positionSide korrekt ist.
            await _restClient.ClosePartialAsync(pos.Symbol, pos.Side, quantityToClose)
                .ConfigureAwait(false);

            // CompletedTrade für den geschlossenen Teil
            var entryFee = quantityToClose * pos.EntryPrice * _takerFeeRate;
            var exitFee = quantityToClose * price * _takerFeeRate;
            var totalFee = entryFee + exitFee;
            var rawPnl = pos.Side == Side.Buy
                ? (price - pos.EntryPrice) * quantityToClose
                : (pos.EntryPrice - price) * quantityToClose;
            var key = $"{pos.Symbol}_{pos.Side}";
            var entryTime = _positionOpenTimes.GetValueOrDefault(key, pos.OpenTime);
            var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, price,
                quantityToClose, rawPnl - totalFee, totalFee, entryTime, DateTime.UtcNow,
                "Partial Close (TP1)", TradingMode.Live);
            ProcessCompletedTrade(trade);
            _eventBus.PublishTrade(trade);

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                $"LIVE: {pos.Symbol} Partial Close {quantityToClose:F4} @ {price:F8}", pos.Symbol));

            // Serverseitige SL/TP aktualisieren (SL → Break-Even, TP → TP2)
            var posKey = $"{pos.Symbol}_{pos.Side}";
            if (_exitStates.TryGetValue(posKey, out var exitState) && _positionSignals.TryGetValue(posKey, out var updatedSignal))
            {
                try
                {
                    await _restClient.SetPositionSlTpAsync(
                        pos.Symbol, pos.Side,
                        updatedSignal.StopLoss,
                        updatedSignal.TakeProfit).ConfigureAwait(false);

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                        $"LIVE: {pos.Symbol} SL/TP auf BingX aktualisiert (SL={updatedSignal.StopLoss:F8}, TP={updatedSignal.TakeProfit:F8})", pos.Symbol));
                }
                catch (Exception slTpEx)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                        $"LIVE: SL/TP-Update auf BingX fehlgeschlagen: {slTpEx.Message}", pos.Symbol));
                }
            }
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
        _lastTrailingSyncTimes.TryRemove(key, out _);
    }

    protected override void OnSignalsClearedAll()
    {
        _signalCreatedAt.Clear();
        _positionOpenTimes.Clear();
        _lastTrailingSyncTimes.Clear();
    }

    protected override void OnSignalCreated(string key)
    {
        _signalCreatedAt[key] = DateTime.UtcNow;
        _positionOpenTimes[key] = DateTime.UtcNow;
    }

    // ═══════════════════════════════════════════════════════════════
    // Pending Limit Orders Persistenz (TP-Recovery nach App-Neustart)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt einen Snapshot aller pending Limit-Orders für DB-Persistenz zurück.
    /// Enthält TP-Werte aus _positionSignals damit nach Recovery TP platziert werden kann.
    /// </summary>
    public Dictionary<string, PendingLimitOrderState> GetPendingLimitOrdersSnapshot()
    {
        var result = new Dictionary<string, PendingLimitOrderState>();
        foreach (var kvp in _pendingLimitOrders)
        {
            var state = new PendingLimitOrderState
            {
                OrderId = kvp.Value.OrderId,
                PlacedAt = kvp.Value.PlacedAt,
                InvalidationLevel = kvp.Value.InvalidationLevel,
                IsLong = kvp.Value.IsLong
            };

            // TP-Werte aus dem zugehörigen Signal holen (für TP-Platzierung nach Recovery-Fill)
            var expectedSide = kvp.Value.IsLong ? Side.Buy : Side.Sell;
            var posKey = $"{kvp.Key}_{expectedSide}";
            if (_positionSignals.TryGetValue(posKey, out var sig))
            {
                state.TakeProfit = sig.TakeProfit;
                state.TakeProfit2 = sig.TakeProfit2;
                state.DisableSmartBreakeven = sig.DisableSmartBreakeven;
            }

            result[kvp.Key] = state;
        }
        return result;
    }

    /// <summary>
    /// Stellt pending Limit-Orders aus DB-Persistenz wieder her.
    /// Wird beim Start vom LiveTradingManager aufgerufen.
    /// </summary>
    public void RestorePendingLimitOrders(Dictionary<string, PendingLimitOrderState> states)
    {
        foreach (var kvp in states)
        {
            _pendingLimitOrders[kvp.Key] = (kvp.Value.OrderId, kvp.Value.PlacedAt,
                kvp.Value.InvalidationLevel, kvp.Value.IsLong);

            // Signal muss auch existieren damit Fill-Detection das TP setzen kann
            var expectedSide = kvp.Value.IsLong ? Side.Buy : Side.Sell;
            var posKey = $"{kvp.Key}_{expectedSide}";
            if (!_positionSignals.ContainsKey(posKey) && kvp.Value.TakeProfit.HasValue)
            {
                var signal = new SignalResult(
                    kvp.Value.IsLong ? Signal.Long : Signal.Short,
                    0.5m, null,
                    StopLoss: kvp.Value.InvalidationLevel,
                    TakeProfit: kvp.Value.TakeProfit,
                    Reason: "Recovery: Pending Limit-Order wiederhergestellt",
                    TakeProfit2: kvp.Value.TakeProfit2,
                    DisableSmartBreakeven: kvp.Value.DisableSmartBreakeven);
                _positionSignals[posKey] = signal;
                OnSignalCreated(posKey);
            }
        }
    }

    /// <summary>
    /// Platziert TP-Orders für eine bestehende Position (Recovery nach App-Neustart).
    /// Wird vom LiveTradingManager aufgerufen wenn eine Position ohne TP-Orders erkannt wird.
    /// </summary>
    public async Task RecoverTpOrdersAsync(string symbol, Side side, decimal quantity, SignalResult signal)
    {
        await PlaceTpLimitOrdersAfterFillAsync(symbol, side, quantity, signal).ConfigureAwait(false);
    }

    /// <summary>Bei API-Fehler: 60s warten statt 30s (Rate-Limit-Schutz).</summary>
    protected override Task OnScanErrorAsync(CancellationToken ct) =>
        Task.Delay(60_000, ct);

    /// <summary>Verwaiste Signale bereinigen (Grace Period 30s) + Funding-Rate aktualisieren.</summary>
    protected override async Task OnBeforePriceTickerIteration(IReadOnlyList<Position> positions)
    {
        // Kill-Switch: Countdown alle 60s refreshen (Dead-Man-Switch)
        // Bei Bot-Crash oder Netzwerk-Verlust: BingX cancelt nach 120s alle offenen Orders
        // Wird nach erstem Fehler dauerhaft deaktiviert (Endpoint nicht bei allen Account-Typen verfügbar)
        if (!_killSwitchDisabled && (DateTime.UtcNow - _lastKillSwitchRefresh).TotalSeconds >= KillSwitchRefreshIntervalSeconds)
        {
            try
            {
                await _restClient.ActivateKillSwitchAsync(KillSwitchTimeoutMs).ConfigureAwait(false);
                _lastKillSwitchRefresh = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _killSwitchDisabled = true;
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Safety",
                    $"Kill-Switch nicht verfügbar (wird deaktiviert): {ex.Message}"));
            }
        }

        if (_positionSignals.Count > 0)
        {
            var positionKeys = new HashSet<string>(positions.Select(p => $"{p.Symbol}_{p.Side}"));
            var now = DateTime.UtcNow;
            foreach (var key in _positionSignals.Keys)
            {
                if (!positionKeys.Contains(key))
                {
                    // KRITISCH: Signal NICHT als verwaist entfernen solange eine Pending-Limit-Order für
                    // das Symbol existiert. Limit-Orders können Minuten/Stunden unbefüllt bleiben. Ohne
                    // diesen Schutz wird das Signal nach 30s entfernt, dann füllt die Order → Position
                    // existiert, aber kein Signal → Fill-Detection stuck in "retry nächster Tick" Endlos-Loop
                    // → TP wird nie platziert.
                    var symbol = key.Split('_')[0];
                    if (_pendingLimitOrders.ContainsKey(symbol))
                        continue;

                    // Nur entfernen wenn Signal älter als 30 Sekunden (API-Latenz-Grace-Period)
                    if (_signalCreatedAt.TryGetValue(key, out var createdAt) && (now - createdAt).TotalSeconds > 30)
                    {
                        // SK-VERIFY: [6.2] Verwaiste native SL/TP-Orders aufräumen
                        // Wenn User die Position manuell auf BingX schließt, bleiben die nativen Orders im Orderbuch
                        try { await CancelNativeSlTpOrdersAsync(symbol).ConfigureAwait(false); }
                        catch { /* Best-effort: Verwaiste Orders sind ungefährlich */ }
                        RemoveSignalByKey(key);
                    }
                }
            }
        }

        // Pending Limit-Orders: Fill-Detection + Invalidation-Cancel (SK-Buch Workflow 5.3+6.9)
        if (_pendingLimitOrders.Count > 0)
        {
            var now2 = DateTime.UtcNow;
            // Tickers nur laden wenn Invalidation-Check nötig (einer der pending Orders ohne WS-Preis)
            List<Ticker>? tickers = null;
            foreach (var kvp in _pendingLimitOrders)
            {
                // Side direkt aus pending-Order (verhindert Race mit _positionSignals-Initialisierung)
                var expectedSide = kvp.Value.IsLong ? Side.Buy : Side.Sell;
                var filledPos = positions.FirstOrDefault(p => p.Symbol == kvp.Key && p.Side == expectedSide);

                if (filledPos != null && filledPos.Quantity > 0)
                {
                    var posKey = $"{kvp.Key}_{filledPos.Side}";

                    // Race-Schutz: Wenn Signal noch nicht in _positionSignals ist (RunLoop hat's
                    // noch nicht gesetzt), max 6 Ticks (30s) warten — nächster Tick versucht erneut.
                    // Ohne Schutz geht das TP verloren wenn PriceTickerLoop die Position sieht bevor
                    // RunLoop das Signal registriert hat.
                    // Nach 30s: Signal aus Pending-State rekonstruieren (SL aus InvalidationLevel,
                    // kein TP). Verhindert Endlos-Loop wenn Signal gar nicht mehr kommt (z.B. nach
                    // Verwaist-Cleanup oder App-Neustart zwischen Platzierung und Fill ohne TP-State).
                    if (!_positionSignals.TryGetValue(posKey, out var sig))
                    {
                        var pendingAge = (now2 - kvp.Value.PlacedAt).TotalSeconds;
                        if (pendingAge < 30)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                                $"LIVE: {kvp.Key} Limit gefüllt @ {filledPos.EntryPrice:F8}, aber Signal noch nicht registriert — retry nächster Tick", kvp.Key));
                            continue; // _pendingLimitOrders NICHT entfernen
                        }

                        // Signal aus Pending-State rekonstruieren
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"LIVE: {kvp.Key} Signal nach {pendingAge:F0}s nicht registriert — rekonstruiere aus Pending-State (SL={kvp.Value.InvalidationLevel:F8}, TP unbekannt)", kvp.Key));

                        var reconstructedSignal = new SignalResult(
                            kvp.Value.IsLong ? Signal.Long : Signal.Short,
                            0.5m,
                            filledPos.EntryPrice,
                            StopLoss: kvp.Value.InvalidationLevel,
                            TakeProfit: null,
                            Reason: "Rekonstruiert nach Signal-Verlust (Verwaist-Cleanup oder Neustart)",
                            DisableSmartBreakeven: true);

                        _positionSignals[posKey] = reconstructedSignal;
                        OnSignalCreated(posKey);

                        // Nativen SL auf BingX setzen damit Position geschützt ist
                        try
                        {
                            await _restClient.SetPositionSlTpAsync(kvp.Key, filledPos.Side, kvp.Value.InvalidationLevel, null).ConfigureAwait(false);
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                                $"LIVE: {kvp.Key} Nativer SL gesetzt: {kvp.Value.InvalidationLevel:F8} (TP fehlt — PriceTickerLoop-Fallback ohne TP-Wert inaktiv, manuelle Überwachung nötig)", kvp.Key));
                        }
                        catch (Exception slEx)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                                $"LIVE: {kvp.Key} SL-Recovery fehlgeschlagen: {slEx.Message} — Position UNGESCHÜTZT!", kvp.Key));
                        }

                        sig = reconstructedSignal;
                    }

                    _pendingLimitOrders.TryRemove(kvp.Key, out _);

                    // ExitState mit echten Fill-Werten korrigieren (SK-Buch-kritisch):
                    // Bei Order-Platzierung stand ticker.LastPrice als Proxy im ExitState.
                    // Jetzt liefert BingX den tatsächlichen Fill-Preis und die gefüllte Menge.
                    if (_exitStates.TryGetValue(posKey, out var exState))
                    {
                        exState.EntryPrice = filledPos.EntryPrice;
                        exState.OriginalQuantity = filledPos.Quantity;
                        exState.EntryTime = DateTime.UtcNow; // Fill-Zeit als Entry-Zeit
                    }

                    // TP-Limit-Orders nachholen (konnten bei Limit-Entry nicht sofort platziert werden)
                    if (sig.TakeProfit.HasValue && sig.TakeProfit.Value > 0)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                            $"LIVE: {kvp.Key} Limit-Entry @ {filledPos.EntryPrice:F8} gefüllt → TP-Limit-Orders werden platziert", kvp.Key));
                        await PlaceTpLimitOrdersAfterFillAsync(kvp.Key, filledPos.Side, filledPos.Quantity, sig).ConfigureAwait(false);
                    }
                    else
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"LIVE: {kvp.Key} Limit-Entry gefüllt, aber Signal hat kein TakeProfit — nur PriceTickerLoop-Fallback aktiv", kvp.Key));
                    }
                    continue; // Nächste pending Order
                }

                // SK-Buch Workflow 5.3+6.9: Limit-Order läuft bis Sequenz invalid wird.
                // Invalidation = Preis hat den StopLoss-Level erreicht (= ≈Point0, 78.6er).
                // Preis-Quelle: WS-Ticker (live, <1s Lag) → Mark-Price aus Positions → Tickers-Snapshot
                var currentPx = _wsTickerPrices.TryGetValue(kvp.Key, out var wsP) && wsP > 0
                    ? wsP
                    : positions.FirstOrDefault(p => p.Symbol == kvp.Key)?.MarkPrice ?? 0m;
                if (currentPx <= 0)
                {
                    tickers ??= await _publicClient.GetAllTickersAsync().ConfigureAwait(false);
                    currentPx = tickers.FirstOrDefault(t => t.Symbol == kvp.Key)?.LastPrice ?? 0m;
                }

                var invalidated = currentPx > 0 && (kvp.Value.IsLong
                    ? currentPx <= kvp.Value.InvalidationLevel   // Long: Preis fiel auf/unter 78.6er/Point0
                    : currentPx >= kvp.Value.InvalidationLevel); // Short: Preis stieg auf/über 78.6er/Point0

                // Hard-Expiry: Safety-Net gegen vergessene Orders bei Daten-Ausfall (nicht Buch-Regel)
                var hardExpired = (now2 - kvp.Value.PlacedAt).TotalHours >= LimitOrderHardExpiryHours;

                if (invalidated || hardExpired)
                {
                    try
                    {
                        await _restClient.CancelOrderAsync(kvp.Value.OrderId, kvp.Key).ConfigureAwait(false);
                        var reason = invalidated
                            ? $"Sequenz invalid (Preis {currentPx:F8} erreichte Invalidation-Level {kvp.Value.InvalidationLevel:F8})"
                            : $"Hard-Expiry nach {LimitOrderHardExpiryHours}h";
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"{kvp.Key}: Limit-Order gecancellt — {reason}", kvp.Key));
                    }
                    catch { /* Order möglicherweise bereits gefüllt/gecancellt */ }
                    _pendingLimitOrders.TryRemove(kvp.Key, out _);

                    // Prüfe ob Position trotzdem teilweise gefüllt wurde
                    try
                    {
                        var currentPos = positions.FirstOrDefault(p => p.Symbol == kvp.Key);
                        if (currentPos != null && currentPos.Quantity > 0)
                        {
                            // Teilweise gefüllt: TP-Limit-Orders auf BingX canceln (falsche Qty)
                            // und Signal/ExitState mit korrekter Quantity + Fill-Preis aktualisieren
                            await CancelNativeSlTpOrdersAsync(kvp.Key).ConfigureAwait(false);
                            var posKey = $"{kvp.Key}_{currentPos.Side}";
                            if (_exitStates.TryGetValue(posKey, out var es))
                            {
                                es.OriginalQuantity = currentPos.Quantity;
                                es.EntryPrice = currentPos.EntryPrice;
                            }

                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                                $"{kvp.Key}: Partial-Fill erkannt ({currentPos.Quantity:F4}), TP-Orders gecancellt, PriceTickerLoop übernimmt",
                                kvp.Key));
                        }
                        else
                        {
                            // Nicht gefüllt: Signal + ExitState + TP-Orders komplett aufräumen
                            // Suche passenden Key (Symbol_Buy oder Symbol_Sell)
                            foreach (var side in new[] { Side.Buy, Side.Sell })
                            {
                                var posKey = $"{kvp.Key}_{side}";
                                if (_positionSignals.ContainsKey(posKey))
                                {
                                    RemoveSignalByKey(posKey);
                                    try { await CancelNativeSlTpOrdersAsync(kvp.Key).ConfigureAwait(false); }
                                    catch { /* Best-effort */ }
                                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                                        $"{kvp.Key}: Nicht gefüllt, Signal + TP-Orders aufgeräumt", kvp.Key));
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"{kvp.Key}: Cleanup nach Limit-Cancel fehlgeschlagen: {cleanupEx.Message}", kvp.Key));
                    }
                }
            }
        }

        // Funding-Rate periodisch aktualisieren (alle 5 Min pro Symbol)
        if ((DateTime.UtcNow - _lastFundingRateUpdate).TotalMinutes >= 5 && positions.Count > 0)
        {
            _lastFundingRateUpdate = DateTime.UtcNow;
            // Funding-Rates für alle offenen Positionen (verschiedene Symbole können stark variieren)
            var uniqueSymbols = positions.Select(p => p.Symbol).Distinct();
            foreach (var symbol in uniqueSymbols)
            {
                try
                {
                    var rate = await _restClient.GetFundingRateAsync(symbol).ConfigureAwait(false);
                    _fundingRates[symbol] = rate;
                }
                catch { /* Funding-Rate-Abfrage ist optional */ }
            }
        }
    }

    /// <summary>
    /// Platziert TP1 + TP2 als LIMIT Reduce-Only Orders nach dem Entry-Fill.
    /// Stackbar: BingX erlaubt beliebig viele LIMIT-Orders pro Position.
    /// Maker-Fee (0.02%) statt Taker (0.05%).
    /// Wird von Market-Entry UND Limit-Fill-Detection aufgerufen.
    /// Mit Retry: BingX kann Position-Registrierung einige Sekunden verzögern — bis zu 3 Versuche
    /// mit Backoff. Nach Platzierung: Verifizierung via GetOpenOrders (Order-ID muss im Orderbuch sein).
    /// </summary>
    private async Task PlaceTpLimitOrdersAfterFillAsync(string symbol, Side side, decimal fallbackQty, SignalResult signal)
    {
        try
        {
            // Position mit Retry lesen: BingX braucht bei Market-Orders manchmal 1-3s bis die Position
            // in GetPositionsAsync auftaucht. Ohne Retry würde TP-Order mit fallbackQty platziert und
            // könnte als "keine Position" rejected werden (Hedge-Mode mit positionSide=LONG).
            Position? actualPos = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var posAfterOrder = await _restClient.GetPositionsAsync().ConfigureAwait(false);
                actualPos = posAfterOrder.FirstOrDefault(p => p.Symbol == symbol && p.Side == side);
                if (actualPos != null && actualPos.Quantity > 0) break;
                if (attempt < 3)
                    await Task.Delay(1000).ConfigureAwait(false);
            }

            if (actualPos == null || actualPos.Quantity <= 0)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE: {symbol} TP-Platzierung übersprungen — Position nach 3s noch nicht bei BingX registriert (Fallback: Bot-seitig via PriceTickerLoop)", symbol));
                return;
            }

            var actualQty = actualPos.Quantity;

            // SK-Buch: Tp1CloseRatio aus RiskSettings (Default 0.5 = 50%)
            var tp1Ratio = _riskSettings.Tp1CloseRatio;
            var tp2Qty = 0m;
            var hasTp2 = signal.TakeProfit2.HasValue && signal.TakeProfit2.Value > 0
                         && signal.TakeProfit2.Value != signal.TakeProfit!.Value;

            // Wenn TP1 == TP2 (BCKL-Fallback / tp1 auf falscher Seite):
            // TP1 deckt die ganze Position ab, damit keine 50% ungeschützt bleiben.
            var tp1Qty = hasTp2
                ? Math.Round(actualQty * tp1Ratio, 6)
                : Math.Round(actualQty, 6);

            if (hasTp2)
            {
                tp2Qty = signal.DisableSmartBreakeven
                    ? Math.Round(actualQty - tp1Qty, 6)  // SK: Rest (Sequenz abgearbeitet bei TP2)
                    : Math.Round(actualQty * _riskSettings.Tp2CloseRatio, 6);
            }

            // Over-Close Guard: TP1+TP2 darf nie > Position
            if (tp1Qty + tp2Qty > actualQty)
                tp2Qty = Math.Round(actualQty - tp1Qty, 6);

            // TP1 als LIMIT Reduce-Only (mit Retry bei Fehler)
            string? tp1OrderId = null;
            if (tp1Qty > 0)
            {
                tp1OrderId = await PlaceTpWithRetryAsync(symbol, side, tp1Qty, signal.TakeProfit!.Value, "TP1").ConfigureAwait(false);
            }

            // TP2 als LIMIT (stackbar — überschreibt TP1 nicht, mit Retry bei Fehler)
            string? tp2OrderId = null;
            if (hasTp2 && tp2Qty > 0)
            {
                tp2OrderId = await PlaceTpWithRetryAsync(symbol, side, tp2Qty, signal.TakeProfit2!.Value, "TP2").ConfigureAwait(false);
            }

            // Verifizieren dass die platzierten TP-Orders tatsächlich im BingX-Orderbuch stehen
            // (Schutz gegen "stumme" API-Erfolge wo Order zurückgegeben wird aber nicht existiert)
            if (!string.IsNullOrEmpty(tp1OrderId) || !string.IsNullOrEmpty(tp2OrderId))
            {
                try
                {
                    var openOrders = await _restClient.GetOpenOrdersAsync(symbol).ConfigureAwait(false);
                    var liveIds = new HashSet<string>(openOrders.Select(o => o.OrderId));

                    if (!string.IsNullOrEmpty(tp1OrderId) && !liveIds.Contains(tp1OrderId))
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                            $"LIVE: {symbol} TP1 Verify fehlgeschlagen — Order {tp1OrderId} nicht im BingX-Orderbuch! (Fallback: Bot-seitig via PriceTickerLoop)", symbol));
                    }
                    if (!string.IsNullOrEmpty(tp2OrderId) && !liveIds.Contains(tp2OrderId))
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                            $"LIVE: {symbol} TP2 Verify fehlgeschlagen — Order {tp2OrderId} nicht im BingX-Orderbuch! (Fallback: Bot-seitig via PriceTickerLoop)", symbol));
                    }
                }
                catch (Exception verifyEx)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                        $"LIVE: {symbol} TP-Verify übersprungen: {verifyEx.Message}", symbol));
                }
            }
        }
        catch (Exception ex)
        {
            // TP-Orders fehlgeschlagen → Bot-seitiger PriceTickerLoop übernimmt als Fallback
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                $"LIVE: {symbol} TP Limit-Orders fehlgeschlagen (Fallback: Bot-seitig): {ex.Message}", symbol));
        }
    }

    /// <summary>
    /// Platziert eine einzelne TP-Limit-Order mit Retry bei Rejection.
    /// Gibt die OrderId zurück wenn erfolgreich, null bei endgültigem Fehler.
    /// </summary>
    private async Task<string?> PlaceTpWithRetryAsync(string symbol, Side side, decimal quantity, decimal price, string tpLabel)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var order = await _restClient.PlaceTpReduceOnlyLimitAsync(symbol, side, quantity, price).ConfigureAwait(false);

            if (order.Status != OrderStatus.Rejected && !string.IsNullOrEmpty(order.OrderId))
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                    $"LIVE: {symbol} {tpLabel} Limit platziert: {quantity:F8} @ {price:F8} (OrderId={order.OrderId}, Maker-Fee)",
                    symbol));
                return order.OrderId;
            }

            var reason = order.RejectionReason ?? "unbekannt";
            if (attempt < 3)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                    $"LIVE: {symbol} {tpLabel} Versuch {attempt}/3 abgelehnt: {reason} (Qty={quantity:F8}, Preis={price:F8}) — retry in 1.5s",
                    symbol));
                await Task.Delay(1500).ConfigureAwait(false);
            }
            else
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                    $"LIVE: {symbol} {tpLabel} ENDGÜLTIG ABGELEHNT nach 3 Versuchen: {reason} (Qty={quantity:F8}, Preis={price:F8})",
                    symbol));
            }
        }
        return null;
    }

    protected override Task OnOrderPlacedAsync(Ticker ticker, Side side, decimal quantity)
    {
        // Entry-Fee nur loggen, KEINEN CompletedTrade publizieren
        // (Ghost-Trade + doppelte Fee-Zählung vermeiden - Fee wird beim Close eingerechnet)
        var entryNotional = quantity * ticker.LastPrice;
        var entryFee = entryNotional * _takerFeeRate;
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
            $"LIVE: {ticker.Symbol} Entry-Fee: {entryFee:N4} USDT", ticker.Symbol));
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════
    // WebSocket Ticker-Stream (Echtzeit-Preise für SL/TP)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Startet den WebSocket-Ticker-Stream für Echtzeit-Preise.
    /// Erlaubt schnellere SL/TP-Reaktion als 5s REST-Polling.
    /// </summary>
    private async Task StartTickerStreamAsync()
    {
        if (_wsClient == null) return;
        try
        {
            _tickerPriceHandler = (symbol, price) => _wsTickerPrices[symbol] = price;
            _wsClient.TickerPriceReceived += _tickerPriceHandler;
            await _wsClient.SubscribeAllTickersAsync().ConfigureAwait(false);
            IsWsTickerActive = true;
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "WebSocket",
                "Echtzeit-Ticker-Stream aktiv (sub-100ms Latenz)"));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "WebSocket",
                $"Ticker-Stream nicht verfügbar: {ex.Message}. Fallback auf 5s REST-Polling."));
        }
    }

    /// <summary>Gibt den WebSocket-Preis für ein Symbol zurück, falls verfügbar.</summary>
    public decimal? GetWebSocketPrice(string symbol) =>
        _wsTickerPrices.TryGetValue(symbol, out var price) ? price : null;

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

            // ListenKey alle 30 Minuten erneuern, bei 2+ Fehlern Reconnect
            var renewFailures = 0;
            _listenKeyRenewTimer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            while (await _listenKeyRenewTimer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await _restClient.RenewListenKeyAsync(_listenKey).ConfigureAwait(false);
                    renewFailures = 0;
                }
                catch (Exception ex)
                {
                    renewFailures++;
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                        $"ListenKey-Erneuerung fehlgeschlagen ({renewFailures}x): {ex.Message}"));

                    // Bei 2+ Fehlern: Neuen ListenKey erstellen und WS-Verbindung neu aufbauen
                    if (renewFailures >= 2)
                    {
                        try
                        {
                            if (_wsClient.IsUserDataConnected)
                                await _wsClient.DisconnectUserDataStreamAsync().ConfigureAwait(false);

                            _listenKey = await _restClient.CreateListenKeyAsync().ConfigureAwait(false);
                            await _wsClient.ConnectUserDataStreamAsync(_listenKey, ct).ConfigureAwait(false);
                            renewFailures = 0;

                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                                "User-Data-Stream neu verbunden (ListenKey erneuert)"));
                        }
                        catch (Exception reconnectEx)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                                $"User-Data-Stream Reconnect fehlgeschlagen: {reconnectEx.Message}. Fallback: REST-Polling."));
                        }
                    }
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
        {
            _wsClient.UserDataReceived -= OnUserDataReceived;
            if (_tickerPriceHandler != null)
            {
                _wsClient.TickerPriceReceived -= _tickerPriceHandler;
                _tickerPriceHandler = null;
            }
        }
        IsWsTickerActive = false;

        _listenKeyRenewTimer?.Dispose();
        _listenKeyRenewTimer = null;

        // ListenKey löschen (Best-effort, nicht awaiten in Dispose)
        // try-catch: _restClient könnte bereits disposed sein (LiveTradingManager.Dispose Reihenfolge)
        if (_listenKey != null)
        {
            try { _ = _restClient.DeleteListenKeyAsync(_listenKey); }
            catch { /* Best-effort: ObjectDisposedException möglich */ }
            _listenKey = null;
        }
    }

    /// <summary>
    /// Cancelt alle nativen SL/TP-Orders (STOP_MARKET + TAKE_PROFIT_MARKET) für ein Symbol.
    /// Muss VOR Position-Close aufgerufen werden, damit keine Ghost-Orders übrigbleiben.
    /// </summary>

    /// <summary>
    /// Aktualisiert den nativen SL auf BingX wenn SL angepasst wird (halbiert nach 4.1 oder BE nach 4.2).
    /// Mit 3 Retries — bei Crash muss der neue SL auf BingX sein, sonst trägt User Original-SL-Verlust.
    /// </summary>
    protected override async Task OnStopLossAdjustedAsync(string symbol, Side side, decimal newStopLoss)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await _restClient.SetPositionSlTpAsync(symbol, side, newStopLoss, null).ConfigureAwait(false);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"LIVE: {symbol} Nativer SL aktualisiert: {newStopLoss:F8}", symbol));
                return;
            }
            catch (Exception ex)
            {
                if (attempt < 3)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                        $"LIVE: {symbol} SL-Update Versuch {attempt}/3 fehlgeschlagen: {ex.Message} - Retry", symbol));
                    await Task.Delay(2000).ConfigureAwait(false);
                }
                else
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                        $"LIVE: {symbol} KRITISCH: SL-Update konnte nach 3 Versuchen nicht durchgeführt werden: {ex.Message}", symbol));
                    if (_botSettings.EnableDesktopNotifications)
                        _eventBus.PublishNotification("SL-Update FEHLT", $"{symbol}: Nativer SL nicht aktualisiert!");
                }
            }
        }
    }

    /// <summary>Startet einen async Background-Task mit Fehler-Logging (kein ContinueWith nötig).</summary>
    private async Task SafeStartAsync(string name, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "WebSocket",
                $"{name} fehlgeschlagen: {ex.Message}"));
        }
    }

    private async Task CancelNativeSlTpOrdersAsync(string symbol)
    {
        try
        {
            var openOrders = await _restClient.GetOpenOrdersAsync(symbol).ConfigureAwait(false);
            foreach (var order in openOrders)
            {
                if (order.Type is OrderType.StopMarket or OrderType.TakeProfitMarket or OrderType.TakeProfitLimit)
                {
                    try { await _restClient.CancelOrderAsync(order.OrderId, symbol).ConfigureAwait(false); }
                    catch { /* Order möglicherweise bereits gecancelled */ }
                }
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                $"Native SL/TP-Cancel für {symbol} fehlgeschlagen: {ex.Message}", symbol));
        }
    }
}
