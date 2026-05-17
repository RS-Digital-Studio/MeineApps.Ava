using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Services;
using BingXBot.Engine;
using BingXBot.Engine.Risk;
using BingXBot.Exchange;
using BingXBot.Trading.Reconciliation;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace BingXBot.Trading;

/// <summary>
/// Live-Trading-Service: Echte Orders auf BingX platzieren.
/// Erbt die gesamte Scan-/SL/TP-/BE-Logik von TradingServiceBase.
/// Nutzt BingXRestClient für Orders und IPublicMarketDataClient für Marktdaten.
/// WARNUNG: Echtes Geld! Nur mit ausreichendem Paper-Testing verwenden.
/// </summary>
public partial class LiveTradingService : TradingServiceBase
{
    // IExchangeClient (nicht BingXRestClient) damit Reconcile/Order-Flows unit-getestet werden koennen
    // (FakeExchangeClient in Tests). Umgestellt 24.04.2026 (P0-2 Audit).
    private readonly IExchangeClient _restClient;
    private readonly BingXWebSocketClient? _wsClient;
    // _dbService liegt jetzt in TradingServiceBase, damit ProcessCompletedTrade die Trade-Persistenz
    // direkt aus Base anstoßen kann (vorher schrieb der Live-Pfad keine Trades in die DB).

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
    // Kill-Switch temporaer deaktivieren statt permanent (v1.2.5): Nach 15 Min wird ein neuer
    // Aktivierungsversuch gestartet. Bei dauerhaft fehlendem Endpoint laeuft das nur 1x alle 15min
    // — keine merkbare Rate-Limit-Last, aber Dead-Man-Schutz nach temporaerem Netz-Issue wieder an.
    private DateTime _killSwitchDisabledUntil = DateTime.MinValue;
    private const int KillSwitchRetryMinutes = 15;

    // Zeitpunkt der Signal-Erstellung (für Grace Period bei Bereinigung verwaister Signale)
    // internal damit Tests den Zeitstempel fuer Grace-Window-Cases setzen koennen.
    internal readonly ConcurrentDictionary<string, DateTime> _signalCreatedAt = new();
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
    //
    // SK-Buch-Modell: Key-Format ist "{symbol}#{sequenceId}" — damit können
    // beide Entry-Geschwister (Primary 50% / Additional 66.7% mit Suffix _Prim / _Add) für das
    // gleiche Symbol gleichzeitig pending sein. Bei Sequenz-Invalidierung werden BEIDE gecancelt
    // via CancelAllPendingForSequenceAsync(). Legacy-Keys ohne "#" werden toleriert (= "#_").
    // TakeProfit/TakeProfit2: TP-Werte werden im Tuple gehalten, damit bei Signal-Rekonstruktion
    // (Fill erkannt, aber _positionSignals leer nach 30s+) der TP nicht verloren geht.
    // internal damit Tests pending-Entries fuer die Pending-Ausnahme im Reconcile setzen koennen.
    //
    // v1.4.0 Phase 0.7 (Finding 0.7) — zusaetzliche Strategy-Felder am Ende des Tuples:
    // NavPointA / IsGklSetup / GklTimeframe / RunnerHardCap / IsCounterTrendScalp /
    // PositionScaleOverride. Werden bei Inline-Rekonstruktion in OnBeforePriceTickerIteration
    // gelesen, damit A-Bruch-BE / Runner / HighProb-Boost auch nach Signal-Verlust korrekt arbeiten.
    internal readonly ConcurrentDictionary<string, (string OrderId, DateTime PlacedAt, decimal InvalidationLevel, bool IsLong, string Symbol, string? SequenceId, decimal? TakeProfit, decimal? TakeProfit2, decimal NavPointA, bool IsGklSetup, TimeFrame? GklTimeframe, decimal RunnerHardCap, bool IsCounterTrendScalp, decimal? PositionScaleOverride)> _pendingLimitOrders = new();

    /// <summary>
    /// Bildet den Dictionary-Key aus Symbol und SequenceId.
    /// Symbol kann NICHT "#" enthalten (BingX-Format: "BTC-USDT"), daher ist "#" ein sicherer Separator.
    /// Internal fuer Testbarkeit (BuildPendingKey/ExtractSymbolFromPendingKey-Roundtrip-Tests).
    /// </summary>
    internal static string BuildPendingKey(string symbol, string? sequenceId) =>
        $"{symbol}#{sequenceId ?? "_"}";

    /// <summary>
    /// Extrahiert das Symbol aus einem Pending-Key. Legacy-Keys ohne "#" werden als reines Symbol interpretiert.
    /// Internal fuer Testbarkeit.
    /// </summary>
    internal static string ExtractSymbolFromPendingKey(string key)
    {
        var idx = key.IndexOf('#');
        return idx < 0 ? key : key.Substring(0, idx);
    }

    /// <summary>
    /// Bekannte Entry-Suffixe für SequenceId: SK-Buch _Prim/_Add (ab v1.2.5), Legacy _L500/_L618/_L667
    /// (Triple-Entry vor v1.2.5). Reihenfolge ist relevant — längste Matches zuerst, damit
    /// _L500/_L618/_L667 nicht fälschlich einen kürzeren Teil matchen (hier trivial, aber defensiv).
    /// </summary>
    private static readonly string[] SequenceEntrySuffixes =
    {
        "_Prim", "_Add", "_L500", "_L618", "_L667"
    };

    /// <summary>
    /// Extrahiert den Sequenz-Prefix ohne Entry-Suffix (_Prim/_Add, Legacy _L500/_L618/_L667) aus
    /// einer SequenceId. Format laut SequenzKonzeptStrategy: "{symbol}_{navTf}_{point0}_{pointA}_{suffix}".
    /// Rückgabe = Prefix ohne Suffix — matched beide Entry-Geschwister bei StartsWith-Vergleich.
    /// Gibt null zurück wenn sequenceId keinen bekannten Entry-Suffix hat (kein Multi-Entry-Signal).
    /// </summary>
    private static string? GetSequencePrefix(string? sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId)) return null;
        foreach (var suffix in SequenceEntrySuffixes)
        {
            if (sequenceId.EndsWith(suffix, StringComparison.Ordinal))
                return sequenceId.Substring(0, sequenceId.Length - suffix.Length);
        }
        return null;
    }

    /// <summary>
    /// Kanonischer Sequenz-Key (19.04.2026, Stale-Sequence-Cleanup):
    /// Gleich wie <see cref="GetSequencePrefix"/>, aber Fallback auf die vollständige SequenceId
    /// wenn kein Entry-Suffix vorhanden ist (Single-Entry-Strategien haben keinen Suffix).
    /// Wird genutzt um zu erkennen ob zwei Pending-Orders auf DERSELBEN Sequenz basieren
    /// (Geschwister-Orders) oder auf unterschiedlichen (veraltete Sequenz → canceln).
    /// </summary>
    private static string? GetCanonicalSequenceKey(string? sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId)) return null;
        foreach (var suffix in SequenceEntrySuffixes)
        {
            if (sequenceId.EndsWith(suffix, StringComparison.Ordinal))
                return sequenceId.Substring(0, sequenceId.Length - suffix.Length);
        }
        return sequenceId;
    }
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
        IExchangeClient restClient,
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
        _dbService = dbService;  // Property aus TradingServiceBase — Trade-/Equity-Persistenz im Live-Pfad
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

        // Reconcile-Loop (P0-1, 24.04.2026): alle 60 s Bot-State gegen BingX-Realitaet diffen.
        // Schuetzt gegen verschluckte WS-Events, Pi-Crashes waehrend Operation, User-Eingriffe im
        // BingX-UI. Siehe PositionDriftAnalyzer + ReconcilePositionsAsync.
        _ = SafeStartAsync("Reconcile-Loop", () => ReconcileLoopAsync(_cts!.Token));
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

            // Zuerst ALLE offenen Orders canceln: native SL/TP-Orders UND pending Entry-Limits.
            // (18.04.2026 v1.2.4: Vorher nur SL/TP — Entry-Limits blieben offen und konnten nach
            // Notfall-Stop unerwuenscht fuellen. Bei Notfall alles plattmachen, dann Positionen schliessen.)
            try
            {
                // Timeout via ect: GetOpenOrdersAsync hat keinen CT-Parameter,
                // daher Task.WhenAny mit Timeout als Safety-Net
                var orderTask = _restClient.GetOpenOrdersAsync();
                var completed = await Task.WhenAny(orderTask, Task.Delay(5000, ect)).ConfigureAwait(false);
                var openOrders = completed == orderTask ? await orderTask : Array.Empty<Order>();
                foreach (var order in openOrders)
                {
                    if (order.Type is Core.Enums.OrderType.StopMarket
                                   or Core.Enums.OrderType.TakeProfitMarket
                                   or Core.Enums.OrderType.TakeProfitLimit
                                   or Core.Enums.OrderType.Limit)
                    {
                        try { await _restClient.CancelOrderAsync(order.OrderId, order.Symbol).ConfigureAwait(false); }
                        catch { /* Best-effort: Order koennte bereits gecancelt sein */ }
                    }
                }
                // Interne pending-Limit-Tracking-Map leeren, damit kein Rueckstand bei Restart bleibt
                _pendingLimitOrders.Clear();
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

    // PlaceOrderOnExchangeAsync extrahiert in LiveTradingService.OrderPlacement.cs.

    protected override async Task ClosePositionAndPublishAsync(string symbol, Side side)
    {
        try
        {
            // Position-Daten VOR dem Close lesen (für CompletedTrade)
            var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
            var pos = positions.FirstOrDefault(p => p.Symbol == symbol && p.Side == side);

            // Auch Pending-Limit-Entries fuer (Symbol, Side) cancellen — sonst bleibt eine
            // alte Limit-Entry-Order auf BingX und wird beim naechsten Reconcile-Tick gefuellt,
            // direkt nachdem der User die Position manuell zugemacht hat.
            var matchingPending = _pendingLimitOrders
                .Where(kvp => kvp.Value.Symbol == symbol && kvp.Value.IsLong == (side == Side.Buy))
                .Select(kvp => (kvp.Key, kvp.Value.OrderId, kvp.Value.SequenceId))
                .ToList();
            foreach (var (pKey, pOrderId, _) in matchingPending)
            {
                try { await _restClient.CancelOrderAsync(pOrderId, symbol).ConfigureAwait(false); }
                catch { /* moeglicherweise schon gefuellt/gecancelt */ }
                _pendingLimitOrders.TryRemove(pKey, out _);
            }

            // Erst Close, dann Cancel (sicherer: bei Close-Fehler bleibt nativer SL als Schutz)
            await _restClient.ClosePositionAsync(symbol, side).ConfigureAwait(false);
            RemoveSignalByKey($"{symbol}_{side}");
            try { await CancelNativeSlTpOrdersAsync(symbol, side).ConfigureAwait(false); }
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
                var navTf = GetNavigatorTimeframeForKey(posKey);
                var trade = new CompletedTrade(symbol, side, pos.EntryPrice, exitPrice,
                    pos.Quantity, rawPnl - totalFee, totalFee, entryTime, DateTime.UtcNow,
                    "Close-Signal", TradingMode.Live, navTf);
                ProcessCompletedTrade(trade);
                _eventBus.PublishTrade(trade);
            }

            // Persistenz nachziehen: ExitState + Pending-Snapshot auf Platte,
            // sonst kommen die Eintraege beim naechsten Pi-Restart aus der DB zurueck.
            try { await PersistExitStatesAsync().ConfigureAwait(false); }
            catch { /* best-effort */ }
            try { await PersistPendingLimitOrdersAsync().ConfigureAwait(false); }
            catch { /* best-effort */ }

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
                await CancelNativeSlTpOrdersAsync(pos.Symbol, pos.Side).ConfigureAwait(false);

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
            try { await CancelNativeSlTpOrdersAsync(pos.Symbol, pos.Side).ConfigureAwait(false); }
            catch { /* Best-effort: Verwaiste Orders sind ungefährlich */ }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                $"LIVE: {pos.Symbol}: {reason} ({pos.Side})", pos.Symbol));

            // CompletedTrade erstellen für TradeHistory — zentrale Formel aus FeeCalculator
            // (gleiche Berechnung wie Paper/Backtest, verhindert PnL-Drift zwischen Pfaden).
            var totalFee = BingXBot.Core.Services.FeeCalculator.CalculateTotalFee(pos.EntryPrice, price, pos.Quantity, _takerFeeRate);
            var pnl = BingXBot.Core.Services.FeeCalculator.CalculateNetPnl(pos.Side, pos.EntryPrice, price, pos.Quantity, _takerFeeRate);
            var entryTime = _positionOpenTimes.GetValueOrDefault(key, pos.OpenTime);
            var navTf = GetNavigatorTimeframeForKey(key);
            var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, price,
                pos.Quantity, pnl, totalFee, entryTime, DateTime.UtcNow, reason, TradingMode.Live, navTf);
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
            var navTf = GetNavigatorTimeframeForKey(key);
            var trade = new CompletedTrade(pos.Symbol, pos.Side, pos.EntryPrice, price,
                quantityToClose, rawPnl - totalFee, totalFee, entryTime, DateTime.UtcNow,
                "Partial Close (TP1)", TradingMode.Live, navTf);
            ProcessCompletedTrade(trade);
            _eventBus.PublishTrade(trade);

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                $"LIVE: {pos.Symbol} Partial Close {quantityToClose:F4} @ {price:F8}", pos.Symbol));

            // Serverseitige SL/TP aktualisieren (SL → Break-Even, TP → TP2)
            var posKey = $"{pos.Symbol}_{pos.Side}";
            if (_exitStates.TryGetValue(posKey, out var exitState) && _positionSignals.TryGetValue(posKey, out var updatedSignal))
            {
                // Snapshot-Report-Fix Befund 3 / A0.6 — SL-Sanity-Pruefung nach Partial-Close.
                // Hier ist PartialClosed=true frisch gesetzt → BE-Buffer ist erlaubt. Ein SL kilometerweit
                // ueber/unter Entry wuerde aber trotzdem abgelehnt.
                if (updatedSignal.StopLoss.HasValue)
                {
                    var partialSanity = StopLossSanityGuard.Validate(
                        pos.Side, pos.EntryPrice, updatedSignal.StopLoss.Value,
                        breakevenSet: exitState.BreakevenSet, partialClosed: true, runnerActive: exitState.RunnerActive);
                    if (!partialSanity.IsAcceptable)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                            $"LIVE: {pos.Symbol} SL/TP-Push nach Partial-Close abgelehnt — {partialSanity.RejectReason}",
                            pos.Symbol));
                        return;
                    }
                }
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

    // Pending-Limit-Orders-Management extrahiert in LiveTradingService.PendingLimitOrders.cs
    // (Partial-Class-Split, 24.04.2026, P1-1).

    /// <summary>Bei API-Fehler: 60s warten statt 30s (Rate-Limit-Schutz).</summary>
    protected override Task OnScanErrorAsync(CancellationToken ct) =>
        Task.Delay(60_000, ct);

    /// <summary>
    /// Funding-Rate-Prefetch (v1.2.5): Pro Scan die Funding fuer max 50 Crypto-Kandidaten laden,
    /// deren Rate nicht im Cache ist. TradFi-Symbole ausgenommen (oft kein Funding auf BingX).
    /// Parallel mit Semaphore (gleicher Rate-Limit-Budget wie Klines-Loader).
    /// Ergebnis: SkConfluenceScorer + MarketFilter-FundingCheck sehen bei neuen Signalen
    /// die echte Funding-Rate statt 0.
    /// </summary>
    protected override async Task PreloadScanDataAsync(IReadOnlyList<Ticker> candidates, CancellationToken ct)
    {
        // v1.5.4 Phase 7 — 30-s-TTL-Cache. Vorher hat `!_fundingRates.ContainsKey(...)` permanent
        // gecached → Funding lief stundenlang stale. Jetzt: bei abgelaufenem Cache neu fetchen.
        var toLoad = candidates
            .Where(t => !IsFundingRateCacheFresh(t.Symbol)
                        && !Core.Helpers.SymbolClassifier.IsTradFi(t.Symbol))
            .Take(50)
            .ToList();
        if (toLoad.Count == 0) return;

        using var sem = new SemaphoreSlim(5, 5);
        var tasks = toLoad.Select(async t =>
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var rate = await _restClient.GetFundingRateAsync(t.Symbol).ConfigureAwait(false);
                _fundingRates[t.Symbol] = rate;
                _fundingRatesFetchedAt[t.Symbol] = DateTime.UtcNow;
            }
            catch { /* Funding-Load ist best-effort */ }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>Verwaiste Signale bereinigen (Grace Period 30s) + Funding-Rate aktualisieren.</summary>
    protected override async Task OnBeforePriceTickerIteration(IReadOnlyList<Position> positions)
    {
        // Kill-Switch: Countdown alle 60s refreshen (Dead-Man-Switch).
        // Bei Bot-Crash oder Netzwerk-Verlust: BingX cancelt nach 120s alle offenen Orders.
        // Bei Fehler: 15 Minuten Pause statt dauerhaft deaktivieren (v1.2.5) — danach neuer Versuch.
        var killSwitchReady = DateTime.UtcNow >= _killSwitchDisabledUntil
                              && (DateTime.UtcNow - _lastKillSwitchRefresh).TotalSeconds >= KillSwitchRefreshIntervalSeconds;
        if (killSwitchReady)
        {
            try
            {
                await _restClient.ActivateKillSwitchAsync(KillSwitchTimeoutMs).ConfigureAwait(false);
                _lastKillSwitchRefresh = DateTime.UtcNow;
                // Bei erfolgreicher Aktivierung ist der Endpoint wieder verfuegbar → Backoff zuruecksetzen
                if (_killSwitchDisabledUntil > DateTime.MinValue)
                {
                    _killSwitchDisabledUntil = DateTime.MinValue;
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Safety",
                        "Kill-Switch wieder verfuegbar (nach temporaerer Deaktivierung)"));
                }
            }
            catch (Exception ex)
            {
                _killSwitchDisabledUntil = DateTime.UtcNow.AddMinutes(KillSwitchRetryMinutes);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Safety",
                    $"Kill-Switch fehlgeschlagen, Retry in {KillSwitchRetryMinutes}min: {ex.Message}"));
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
                    //
                    // Side aus dem posKey-Format ({symbol}_{Buy|Sell}) einmal extrahieren — sowohl fuer
                    // den Side-spezifischen Pending-Schutz (Finding 0.4) als auch fuer den
                    // Reduce-Only-Filter im Cancel-Aufruf (Finding 0.1).
                    var symbol = key.Split('_')[0];
                    Side? sideOfKey = null;
                    var sideIdx = key.LastIndexOf('_');
                    if (sideIdx >= 0 && Enum.TryParse<Side>(key.AsSpan(sideIdx + 1), out var parsedSide))
                        sideOfKey = parsedSide;

                    // Phase 0.4 Fix (Finding 0.4) — Side-Filter beim Pending-Schutz.
                    // Vor v1.4.0: Long-Signal blieb stehen, wenn IRGENDEINE Pending fuer das Symbol
                    // existierte — auch wenn die Pending in die Gegenrichtung lief (z.B. Short).
                    // Im Hedge-Mode mit Long+Short parallel fuehrte das zu Zombie-Long-Signalen, die
                    // Risiko-Berechnungen (DailyRisk, Recovery-TP) verzerrten.
                    // Triple-Entry-Key {symbol}#{sequenceId} (Pending-Map) hat keine Side-Komponente —
                    // Side liegt im Wert-Tuple (IsLong). Pending-Side wird aus IsLong projiziert.
                    var hasMatchingPending = sideOfKey.HasValue
                        ? _pendingLimitOrders.Values.Any(v =>
                            v.Symbol == symbol
                            && (v.IsLong ? Side.Buy : Side.Sell) == sideOfKey.Value)
                        : _pendingLimitOrders.Values.Any(v => v.Symbol == symbol);
                    if (hasMatchingPending)
                        continue;

                    // Nur entfernen wenn Signal älter als 30 Sekunden (API-Latenz-Grace-Period)
                    if (_signalCreatedAt.TryGetValue(key, out var createdAt) && (now - createdAt).TotalSeconds > 30)
                    {
                        // SK-VERIFY: [6.2] Verwaiste native SL/TP-Orders aufräumen
                        // Wenn User die Position manuell auf BingX schließt, bleiben die nativen Orders im Orderbuch.
                        try { await CancelNativeSlTpOrdersAsync(symbol, sideOfKey).ConfigureAwait(false); }
                        catch { /* Best-effort: Verwaiste Orders sind ungefährlich */ }
                        RemoveSignalByKey(key);
                    }
                }
            }
        }

        // v1.4.0 Phase 0.6 (Finding 0.6) — Stage 3: TP-Place-Retry fuer Positionen, deren
        // initialer TP-Place fehlgeschlagen ist (Position bei BingX nicht binnen 3 s sichtbar).
        // Tickt zusammen mit PriceTickerLoop (5 s). Hard-Timeout 30 s ab erstem Versuch — danach
        // gibt der Bot auf und ueberlaesst der PriceTickerLoop-Fallback-Logik die TP-Detection.
        if (_exitStates.Count > 0)
        {
            const int RetryHardTimeoutSeconds = 30;
            var nowRetry = DateTime.UtcNow;
            foreach (var kvp in _exitStates)
            {
                var es = kvp.Value;
                if (!es.PendingTpRetry) continue;

                // Hard-Timeout: Wenn der erste Versuch > 30 s zurueckliegt, geben wir auf.
                var ageSeconds = (nowRetry - es.PendingTpFirstAttemptUtc).TotalSeconds;
                if (ageSeconds > RetryHardTimeoutSeconds)
                {
                    es.PendingTpRetry = false;
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                        $"LIVE: {es.Symbol} TP-Retry Hard-Timeout ({RetryHardTimeoutSeconds}s) erreicht " +
                        $"nach {es.PendingTpRetryCount} Versuch(en) — Bot-Fallback (PriceTickerLoop) uebernimmt",
                        es.Symbol));
                    continue;
                }

                // Position muss jetzt sichtbar sein, sonst weiter warten.
                var pos = positions.FirstOrDefault(p => p.Symbol == es.Symbol && p.Side == es.Side);
                if (pos == null || pos.Quantity <= 0) continue;

                es.PendingTpRetryCount++;
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"LIVE: {es.Symbol} Stage-3-Retry #{es.PendingTpRetryCount} (Position jetzt sichtbar, qty={pos.Quantity:F8})",
                    es.Symbol));
                try
                {
                    await PlaceTpLimitOrdersAfterFillAsync(es.Symbol, es.Side, pos.Quantity, es.Signal).ConfigureAwait(false);
                }
                catch (Exception retryEx)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                        $"LIVE: {es.Symbol} Stage-3-Retry-Aufruf fehlgeschlagen: {retryEx.Message}",
                        es.Symbol));
                }
            }
        }

        // Pending Limit-Orders: Fill-Detection + Invalidation-Cancel (SK-Buch Workflow 5.3+6.9)
        if (_pendingLimitOrders.Count > 0)
        {
            var now2 = DateTime.UtcNow;
            // Tickers nur laden wenn Invalidation-Check nötig (einer der pending Orders ohne WS-Preis)
            List<Ticker>? tickers = null;
            // Triple-Entry Cascade-Schutz (18.04.2026 v1.2.4): Wenn eine Geschwister-Order bereits
            // per CancelAllPendingForSequenceAsync gecancellt wurde, Prefix merken und Doppel-Cancel
            // in derselben Iteration vermeiden.
            var cascadedPrefixes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in _pendingLimitOrders)
            {
                // Wenn Prefix bereits durch Geschwister-Invalidation cascade-cancelt → diesen Eintrag ueberspringen
                if (!string.IsNullOrEmpty(kvp.Value.SequenceId))
                {
                    var cascadePrefix = GetSequencePrefix(kvp.Value.SequenceId);
                    if (cascadePrefix != null && cascadedPrefixes.Contains(cascadePrefix))
                    {
                        _pendingLimitOrders.TryRemove(kvp.Key, out _);
                        continue;
                    }
                }
                // Side direkt aus pending-Order (verhindert Race mit _positionSignals-Initialisierung)
                var expectedSide = kvp.Value.IsLong ? Side.Buy : Side.Sell;
                // BUGFIX 24.04.2026: kvp.Key ist "{symbol}#{sequenceId}" (BuildPendingKey), aber Positions,
                // REST-APIs und Log-Filter erwarten das reine BingX-Symbol (z.B. "BTC-USDT"). Ohne diese
                // Trennung matchte kein p.Symbol den Key → filledPos immer null → TPs wurden nie gesetzt.
                var sym = kvp.Value.Symbol;
                var filledPos = positions.FirstOrDefault(p => p.Symbol == sym && p.Side == expectedSide);

                if (filledPos != null && filledPos.Quantity > 0)
                {
                    var posKey = $"{sym}_{filledPos.Side}";

                    // Race-Schutz Fill+Invalidation (18.04.2026 v1.2.4): Wenn der Fill-Preis bereits
                    // jenseits des Invalidation-Levels liegt (Preis ist im selben Tick gefuellt UND
                    // hat Point0 durchbrochen — z.B. Flash-Crash durch die gesamte BC-Zone), oeffnen
                    // wir die Position nicht — sofort schliessen, keine TP platzieren. Ohne diesen
                    // Schutz greift der normale SL-Hit-Pfad erst im naechsten Tick und kostet Slippage.
                    var entryBeyondInvalidation = kvp.Value.IsLong
                        ? filledPos.EntryPrice <= kvp.Value.InvalidationLevel
                        : filledPos.EntryPrice >= kvp.Value.InvalidationLevel;
                    if (entryBeyondInvalidation)
                    {
                        var raceReason = $"EntryPrice {filledPos.EntryPrice:F8} bereits jenseits Invalidation-Level {kvp.Value.InvalidationLevel:F8}";
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"LIVE: {sym} Race-Condition Fill+Invalidation — sofortiges Close ({raceReason})",
                            sym));
                        try
                        {
                            await _restClient.ClosePositionAsync(sym, filledPos.Side).ConfigureAwait(false);
                            await CancelNativeSlTpOrdersAsync(sym, filledPos.Side).ConfigureAwait(false);
                        }
                        catch (Exception raceEx)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                                $"LIVE: {sym} Race-Close fehlgeschlagen: {raceEx.Message} — Position evtl. noch offen!",
                                sym));
                        }
                        _pendingLimitOrders.TryRemove(kvp.Key, out _);
                        RemoveSignalByKey(posKey);

                        // Triple-Cascade auch im Race-Fall: Geschwister-Orders sind ebenfalls tot
                        var raceSeqPrefix = GetSequencePrefix(kvp.Value.SequenceId);
                        if (raceSeqPrefix != null && cascadedPrefixes.Add(raceSeqPrefix))
                        {
                            await CancelAllPendingForSequenceAsync(kvp.Value.Symbol, raceSeqPrefix).ConfigureAwait(false);
                        }
                        continue;
                    }

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
                                $"LIVE: {sym} Limit gefüllt @ {filledPos.EntryPrice:F8}, aber Signal noch nicht registriert — retry nächster Tick", sym));
                            continue; // _pendingLimitOrders NICHT entfernen
                        }

                        // Signal aus Pending-State rekonstruieren — TP/TP2 aus Tuple (v1.2.5):
                        // Vorher ging TP verloren, Position lief ohne Exit-Ziel. Jetzt werden die bei
                        // Platzierung gespeicherten TP-Werte uebernommen, sodass Partial-Close/Full-TP
                        // im PriceTickerLoop wieder greifen.
                        var recoveredTp = kvp.Value.TakeProfit;
                        var recoveredTp2 = kvp.Value.TakeProfit2;
                        var tpLogText = recoveredTp.HasValue
                            ? $"TP1={recoveredTp:F8}" + (recoveredTp2.HasValue ? $" TP2={recoveredTp2:F8}" : "")
                            : "TP unbekannt (Legacy-Eintrag ohne TP-Persist)";
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"LIVE: {sym} Signal nach {pendingAge:F0}s nicht registriert — rekonstruiere aus Pending-State (SL={kvp.Value.InvalidationLevel:F8}, {tpLogText})", sym));

                        // v1.4.0 Phase 0.7 (Finding 0.7) — Strategy-Felder direkt aus Pending-Tuple
                        // wiederbeleben (Place- und Restore-Pfade fuellen sie). Vor v1.4.0 fielen
                        // hier NavPointA / RunnerHardCap / GKL / Counter-Trend / PositionScale
                        // weg → A-Bruch-BE feuerte nie, Runner inactive, HighProb-Boost futsch.
                        decimal? recoveredNavPointA = kvp.Value.NavPointA > 0 ? kvp.Value.NavPointA : null;
                        decimal? recoveredHardCap = kvp.Value.RunnerHardCap > 0 ? kvp.Value.RunnerHardCap : null;

                        var reconstructedSignal = new SignalResult(
                            kvp.Value.IsLong ? Signal.Long : Signal.Short,
                            0.5m,
                            filledPos.EntryPrice,
                            StopLoss: kvp.Value.InvalidationLevel,
                            TakeProfit: recoveredTp,
                            Reason: "Rekonstruiert nach Signal-Verlust (Verwaist-Cleanup oder Neustart)",
                            TakeProfit2: recoveredTp2,
                            DisableSmartBreakeven: true,
                            SequenceId: kvp.Value.SequenceId,
                            IsGklSetup: kvp.Value.IsGklSetup,
                            GklTimeframe: kvp.Value.GklTimeframe,
                            NavPointA: recoveredNavPointA,
                            RunnerHardCap: recoveredHardCap,
                            IsCounterTrendScalp: kvp.Value.IsCounterTrendScalp,
                            PositionScaleOverride: kvp.Value.PositionScaleOverride);

                        _positionSignals[posKey] = reconstructedSignal;
                        OnSignalCreated(posKey);

                        // Nativen SL auf BingX setzen damit Position geschützt ist.
                        // TP NICHT hier setzen — PlaceTpLimitOrdersAfterFillAsync unten
                        // uebernimmt das als Reduce-Only-LIMIT (SK-TP1/TP2-Staffelung).
                        try
                        {
                            await _restClient.SetPositionSlTpAsync(sym, filledPos.Side, kvp.Value.InvalidationLevel, null).ConfigureAwait(false);
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                                $"LIVE: {sym} Nativer SL gesetzt: {kvp.Value.InvalidationLevel:F8}", sym));
                        }
                        catch (Exception slEx)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                                $"LIVE: {sym} SL-Recovery fehlgeschlagen: {slEx.Message} — Position UNGESCHÜTZT!", sym));
                        }

                        sig = reconstructedSignal;
                    }

                    _pendingLimitOrders.TryRemove(kvp.Key, out _);

                    // Signal.EntryPrice auf echten Fill-Preis patchen (sig ist record → with).
                    // Vorher enthielt Signal den Limit-Preis, der bei Slippage/Partial-Fill
                    // vom Fill-Preis abweicht. Stufen-Logik (SL halbieren bei 1x, BE bei 2x)
                    // rechnet jetzt konsistent mit dem tatsaechlichen Fill-Entry.
                    sig = sig with { EntryPrice = filledPos.EntryPrice };
                    _positionSignals[posKey] = sig;

                    // ExitState mit echten Fill-Werten korrigieren:
                    // Bei Order-Platzierung stand ticker.LastPrice als Proxy im ExitState.
                    // Jetzt liefert BingX den tatsächlichen Fill-Preis und die gefüllte Menge.
                    if (_exitStates.TryGetValue(posKey, out var exState))
                    {
                        exState.EntryPrice = filledPos.EntryPrice;
                        exState.OriginalQuantity = filledPos.Quantity;
                        exState.EntryTime = DateTime.UtcNow; // Fill-Zeit als Entry-Zeit
                        exState.Signal = sig; // Gepatchtes Signal im ExitState halten
                    }
                    else
                    {
                        // Recovery-Pfad: Pending-Order aus DB recoverd, aber noch kein ExitState
                        // (wurde bei RestorePendingLimitOrders nicht angelegt). Jetzt ist die
                        // Order gefuellt → ExitState anlegen, damit A-Bruch-BE und TP1/TP2 greifen.
                        _exitStates[posKey] = new PositionExitState
                        {
                            Signal = sig,
                            Symbol = sym,
                            Side = filledPos.Side,
                            EntryPrice = filledPos.EntryPrice,
                            OriginalQuantity = filledPos.Quantity,
                            Tp2 = sig.TakeProfit2,
                            EntryTime = DateTime.UtcNow,
                            SequenceId = sig.SequenceId,
                        };
                    }

                    // TP-Limit-Orders nachholen (konnten bei Limit-Entry nicht sofort platziert werden)
                    if (sig.TakeProfit.HasValue && sig.TakeProfit.Value > 0)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Trade",
                            $"LIVE: {sym} Limit-Entry @ {filledPos.EntryPrice:F8} gefüllt → TP-Limit-Orders werden platziert", sym));
                        await PlaceTpLimitOrdersAfterFillAsync(sym, filledPos.Side, filledPos.Quantity, sig).ConfigureAwait(false);
                    }
                    else
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"LIVE: {sym} Limit-Entry gefüllt, aber Signal hat kein TakeProfit — nur PriceTickerLoop-Fallback aktiv", sym));
                    }
                    continue; // Nächste pending Order
                }

                // SK-Buch Workflow 5.3+6.9: Limit-Order läuft bis Sequenz invalid wird.
                // Invalidation = Preis hat den StopLoss-Level erreicht (= ≈Point0, 78.6er).
                // Preis-Quelle: WS-Ticker (live, <1s Lag) → Mark-Price aus Positions → Tickers-Snapshot
                var currentPx = _wsTickerPrices.TryGetValue(sym, out var wsP) && wsP > 0
                    ? wsP
                    : positions.FirstOrDefault(p => p.Symbol == sym)?.MarkPrice ?? 0m;
                if (currentPx <= 0)
                {
                    if (tickers == null)
                    {
                        var fetched = await _publicClient.GetAllTickersAsync().ConfigureAwait(false);
                        tickers = fetched?.ToList() ?? new List<Ticker>();
                    }
                    currentPx = tickers.FirstOrDefault(t => t.Symbol == sym)?.LastPrice ?? 0m;
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
                        await _restClient.CancelOrderAsync(kvp.Value.OrderId, sym).ConfigureAwait(false);
                        var reason = invalidated
                            ? $"Sequenz invalid (Preis {currentPx:F8} erreichte Invalidation-Level {kvp.Value.InvalidationLevel:F8})"
                            : $"Hard-Expiry nach {LimitOrderHardExpiryHours}h";
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"{sym}: Limit-Order gecancellt — {reason}", sym));
                    }
                    catch { /* Order möglicherweise bereits gefüllt/gecancellt */ }
                    _pendingLimitOrders.TryRemove(kvp.Key, out _);

                    // Triple-Entry Cascade: Bei Invalidierung einer Entry-Geschwister-Order sind die
                    // Geschwister (_Prim / _Add mit gleichem Prefix) mathematisch ebenfalls tot.
                    if (invalidated || hardExpired)
                    {
                        var sequencePrefix = GetSequencePrefix(kvp.Value.SequenceId);
                        if (sequencePrefix != null && cascadedPrefixes.Add(sequencePrefix))
                        {
                            await CancelAllPendingForSequenceAsync(kvp.Value.Symbol, sequencePrefix).ConfigureAwait(false);
                        }
                    }

                    // Prüfe ob Position trotzdem teilweise gefüllt wurde
                    try
                    {
                        var currentPos = positions.FirstOrDefault(p => p.Symbol == sym);
                        if (currentPos != null && currentPos.Quantity > 0)
                        {
                            // Teilweise gefüllt: TP-Limit-Orders auf BingX canceln (falsche Qty)
                            // und Signal/ExitState mit korrekter Quantity + Fill-Preis aktualisieren
                            await CancelNativeSlTpOrdersAsync(sym, currentPos.Side).ConfigureAwait(false);
                            var posKey = $"{sym}_{currentPos.Side}";
                            if (_exitStates.TryGetValue(posKey, out var es))
                            {
                                es.OriginalQuantity = currentPos.Quantity;
                                es.EntryPrice = currentPos.EntryPrice;
                            }

                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                                $"{sym}: Partial-Fill erkannt ({currentPos.Quantity:F4}), TP-Orders gecancellt, PriceTickerLoop übernimmt",
                                sym));
                        }
                        else
                        {
                            // Nicht gefüllt: Signal + ExitState + TP-Orders komplett aufräumen
                            // Suche passenden Key (Symbol_Buy oder Symbol_Sell)
                            foreach (var side in new[] { Side.Buy, Side.Sell })
                            {
                                var posKey = $"{sym}_{side}";
                                if (_positionSignals.ContainsKey(posKey))
                                {
                                    RemoveSignalByKey(posKey);
                                    try { await CancelNativeSlTpOrdersAsync(sym, side).ConfigureAwait(false); }
                                    catch { /* Best-effort */ }
                                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                                        $"{sym}: Nicht gefüllt, Signal + TP-Orders aufgeräumt", sym));
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                            $"{sym}: Cleanup nach Limit-Cancel fehlgeschlagen: {cleanupEx.Message}", sym));
                    }
                }
            }

            // Periodisches Save (18.04.2026 v1.2.4): Nach jeder Iteration den aktuellen Pending-State
            // persistieren, falls Cancels/Fills passiert sind. Best-effort — fire-and-forget.
            _ = PersistPendingLimitOrdersAsync();
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
                    _fundingRatesFetchedAt[symbol] = DateTime.UtcNow;
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
    // PlaceTpLimitOrdersAfterFillAsync / PlaceTpWithRetryAsync / OnOrderPlacedAsync
    // extrahiert in LiveTradingService.OrderPlacement.cs.

    // ═══════════════════════════════════════════════════════════════
    // WebSocket Ticker-Stream (Echtzeit-Preise für SL/TP)
    // ═══════════════════════════════════════════════════════════════

    // WebSocket-Stream-Methoden extrahiert in LiveTradingService.WebSocket.cs
    // (Partial-Class-Split, 24.04.2026, P1-1).

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

    // OnStopLossAdjustedAsync extrahiert in LiveTradingService.SlTpManager.cs.

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

    // Reconcile-Loop extrahiert in LiveTradingService.Reconcile.cs (Partial-Class-Split, 24.04.2026).

    // CancelNativeSlTpOrdersAsync extrahiert in LiveTradingService.SlTpManager.cs.
}
