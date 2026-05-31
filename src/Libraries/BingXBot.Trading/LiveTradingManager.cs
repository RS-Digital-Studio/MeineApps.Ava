using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Services;
using BingXBot.Engine;
using BingXBot.Engine.Strategies;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBot.Trading;

/// <summary>
/// Verwaltet die Live-Trading-Infrastruktur: Client-Erstellung, Verbindung,
/// Service-Lifecycle-Manager für Live-Trading. Kapselt die gesamte Live-spezifische
/// Logik die sonst im DashboardViewModel lag.
/// </summary>
public class LiveTradingManager : IDisposable
{
    private readonly ISecureStorageService? _secureStorage;
    private readonly IPublicMarketDataClient? _publicClient;
    private readonly StrategyManager _strategyManager;
    private readonly RiskSettings _riskSettings;
    private readonly ScannerSettings _scannerSettings;
    private readonly BotEventBus _eventBus;
    private readonly BotDatabaseService? _dbService;
    private readonly BotSettings _botSettings;

    // Wiederverwendbare Infrastruktur (vermeidet Socket-Exhaustion und Semaphore-Leaks bei Start/Stop)
    private HttpClient? _httpClient;
    private RateLimiter? _rateLimiter;
    private BingXRestClient? _restClient;
    private LiveTradingService? _service;
    private bool _disposed;

    /// <summary>Name der aktuell laufenden Strategie — fuer RuntimeState-Tagging (Loss-Streak-Reset bei Wechsel).</summary>
    private string? _activeStrategyName;

    /// <summary>Aktueller REST-Client (null wenn nicht verbunden).</summary>
    public BingXRestClient? RestClient => _restClient;

    /// <summary>
    /// True wenn ein SecureStorage hinterlegt ist und BingX-Credentials dort persistiert sind.
    /// Vermeidet Reflection-Zugriffe auf das private Feld in Consumern (z.B. LocalBotControlService).
    /// </summary>
    public bool HasCredentials => _secureStorage?.HasCredentials ?? false;

    /// <summary>Echte Taker-Fee-Rate vom BingX Account (geladen bei Connect). Fallback 0.05%.</summary>
    public decimal CommissionTakerRate { get; private set; } = 0.0005m;
    /// <summary>Echte Maker-Fee-Rate vom BingX Account (geladen bei Connect). Fallback 0.02%.</summary>
    public decimal CommissionMakerRate { get; private set; } = 0.0002m;

    /// <summary>Aktueller Live-Trading-Service (null wenn nicht gestartet).</summary>
    public LiveTradingService? Service => _service;

    /// <summary>Ob aktuell eine Live-Verbindung besteht.</summary>
    public bool IsConnected => _restClient != null;

    /// <summary>Ob der Live-Trading-Service läuft.</summary>
    public bool IsRunning => _service?.IsRunning ?? false;

    public LiveTradingManager(
        ISecureStorageService? secureStorage,
        IPublicMarketDataClient? publicClient,
        StrategyManager strategyManager,
        RiskSettings riskSettings,
        ScannerSettings scannerSettings,
        BotEventBus eventBus,
        BotSettings botSettings,
        BotDatabaseService? dbService = null,
        Local.ScannerResultsCache? scannerCache = null)
    {
        _secureStorage = secureStorage;
        _publicClient = publicClient;
        _strategyManager = strategyManager;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _eventBus = eventBus;
        _botSettings = botSettings;
        _dbService = dbService;
        _scannerCache = scannerCache;
    }

    private readonly Local.ScannerResultsCache? _scannerCache;

    /// <summary>
    /// Verbindet sich mit BingX, testet die Verbindung und gibt Account-Info + offene Positionen zurück.
    /// Wirft Exception bei Fehler.
    /// </summary>
    public async Task<ConnectResult> ConnectAsync()
    {
        if (_secureStorage == null || !_secureStorage.HasCredentials)
            throw new InvalidOperationException("API-Keys nicht konfiguriert");

        var creds = await _secureStorage.LoadCredentialsAsync();
        if (creds == null)
            throw new InvalidOperationException("API-Keys konnten nicht entschlüsselt werden");

        // HttpClient + RateLimiter wiederverwenden (Socket-Exhaustion + Semaphore-Leaks vermeiden)
        _httpClient ??= new HttpClient();
        _rateLimiter ??= new RateLimiter();
        var logger = NullLogger<BingXRestClient>.Instance;
        _restClient = new BingXRestClient(creds.Value.ApiKey, creds.Value.ApiSecret, _httpClient, _rateLimiter, logger);

        // Server-Zeit synchronisieren (BingX Error 100421 bei Systemzeit-Abweichung >5s)
        await _restClient.SyncServerTimeAsync();

        // Symbol-Info-Cache laden (Quantity/Price-Precision, Min-Order-Größe pro Symbol)
        await _restClient.InitializeSymbolInfoAsync();

        // Commission-Rates laden (echte Maker/Taker-Fees statt hardcoded)
        try
        {
            var (taker, maker) = await _restClient.GetCommissionRateAsync();
            CommissionTakerRate = taker;
            CommissionMakerRate = maker;
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Info, "Account",
                $"Fees geladen: Taker={taker:P3}, Maker={maker:P3}"));
        }
        catch (Exception feeEx)
        {
            // Wichtig: NICHT stillschweigend schlucken. Bei VIP-Account (reduzierte Fees) wuerden
            // PnL-Berechnungen sonst dauerhaft mit falschen Standard-Raten (Taker 0.05% / Maker 0.02%)
            // laufen. Log ermoeglicht es dem User die Diskrepanz nach dem ersten Trade zu erkennen.
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, Core.Enums.LogLevel.Warning, "Account",
                $"Commission-Rates konnten nicht geladen werden ({feeEx.Message}) — Fallback auf Standard "
                + $"Taker={CommissionTakerRate:P3} / Maker={CommissionMakerRate:P3}. PnL koennte bei VIP-Account abweichen."));
        }

        // Verbindung testen
        var account = await _restClient.GetAccountInfoAsync();
        var positions = await _restClient.GetPositionsAsync();

        // Hedge-Modus erkennen + automatisch umschalten für TradFi.
        // Scanner-Flag wird an den tatsächlich durchgesetzten Hedge-Status gekoppelt: Bei One-Way
        // würde TradFi zwar scannen, aber jede Order würde von BingX rejected werden (Log-Spam +
        // verschwendete API-Calls). Nur bei echtem Hedge-Modus TradFi scannen.
        var isHedge = await _restClient.IsHedgeModeAsync();
        if (!isHedge)
        {
            if (positions.Count == 0)
            {
                var switched = await _restClient.SetHedgeModeAsync(true);
                if (switched)
                {
                    isHedge = true;
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                        "Position-Modus automatisch auf Hedge (Zwei-Wege) umgestellt für TradFi-Support"));
                }
                else
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                        "Hedge-Modus konnte nicht aktiviert werden (BingX-API-Fehler). TradFi bleibt für diese Session deaktiviert."));
                }
            }
            else
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                    $"BingX steht auf One-Way und kann nicht umgeschaltet werden ({positions.Count} offene Position(en)). Schließe alle Positionen und starte den Bot neu — TradFi bleibt für diese Session deaktiviert."));
            }
        }
        _scannerSettings.IsHedgeModeActive = isHedge;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
            $"Live-Verbindung hergestellt. Balance: {account.Balance:N2} USDT | Modus: {(isHedge ? "Hedge (TradFi möglich)" : "One-Way (nur Krypto)")}"));

        if (positions.Count > 0)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"{positions.Count} offene Position(en) gefunden"));
        }

        return new ConnectResult(account, positions);
    }

    /// <summary>
    /// Erstellt und startet den LiveTradingService mit der angegebenen Strategie.
    /// ConnectAsync() muss vorher aufgerufen worden sein.
    /// </summary>
    public async Task StartAsync(string strategyName)
    {
        if (_restClient == null)
            throw new InvalidOperationException("Nicht verbunden. ConnectAsync() zuerst aufrufen.");

        if (_publicClient == null)
            throw new InvalidOperationException("Marktdaten-Client nicht verfügbar");

        // LiveTradingService erstellen
        _service = new LiveTradingService(
            _restClient,
            _publicClient,
            _strategyManager,
            _riskSettings,
            _scannerSettings,
            _eventBus,
            _botSettings,
            dbService: _dbService);
        _service.SetScannerResultsCache(_scannerCache);

        // Echte Fee-Rates vom Account setzen (statt hardcoded)
        _service.SetCommissionRates(CommissionTakerRate, CommissionMakerRate);

        // Phase 18 / B2 — Heartbeat-Persist verdrahten (DB-Service vorhanden).
        if (_dbService != null)
            _service.HeartbeatPersist = utc => _dbService.SaveLastHeartbeatAsync(utc);

        // Strategie aktivieren (Multi-TF Standalone: kein Preset mehr)
        var strategy = StrategyFactory.Create(strategyName);
        _strategyManager.SetStrategy(strategy);
        _activeStrategyName = strategyName;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
            $"LIVE-TRADING AKTIV mit Strategie '{strategyName}'. Echtes Geld!"));

        // SK-VERIFY: [6.1] Runtime-State und ExitStates wiederherstellen VOR Start
        if (_dbService == null)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                "State-Recovery übersprungen: Keine Datenbank verfügbar"));
            _service.Start();
            await RecoverOpenPositionsAsync();
            return;
        }
        try
        {
            var runtimeState = await _dbService.LoadRuntimeStateAsync();
            if (runtimeState.HasValue)
            {
                if (runtimeState.Value.StrategyName != strategyName)
                {
                    // Strategiewechsel (oder erstmaliger Start mit Strategie-Tagging): Die Loss-Streak +
                    // Tages-Trades der vorherigen Strategie sind fuer die aktuelle irrelevant → zuruecksetzen.
                    // Sonst startet eine frische Strategie mit der geerbten Loss-Streak-Pause der alten.
                    _service.RestoreRuntimeState(0, 0);
                    await _dbService.SaveRuntimeStateAsync(0, 0, strategyName);
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                        $"Strategie '{strategyName}' (vorher '{runtimeState.Value.StrategyName ?? "unbekannt"}'): Loss-Streak + Tages-Trades zurueckgesetzt"));
                }
                else
                {
                    _service.RestoreRuntimeState(runtimeState.Value.TradesToday, runtimeState.Value.ConsecutiveLosses);
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Recovery",
                        $"Runtime-State wiederhergestellt: {runtimeState.Value.TradesToday} Trades heute, {runtimeState.Value.ConsecutiveLosses} Verluste in Folge"));
                }
            }
            var savedExitStates = await _dbService.LoadExitStatesAsync();
            if (savedExitStates is { Count: > 0 })
            {
                _service.RestoreExitStates(savedExitStates);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Recovery",
                    $"ExitStates wiederhergestellt: {savedExitStates.Count} Position(en) mit Phase/OriginalQty/BE-State"));
            }

            // Pending Limit-Orders wiederherstellen (TP-Recovery nach App-Neustart)
            // ABGLEICH MIT BINGX: DB kann stale Orders enthalten (manuell gecancelt, gefüllt, expired).
            // Nur Orders wiederherstellen die tatsächlich noch auf BingX offen sind.
            var savedPendingOrders = await _dbService.LoadPendingLimitOrdersAsync();
            if (savedPendingOrders is { Count: > 0 })
            {
                var reconciledOrders = await ReconcilePendingLimitOrdersAsync(savedPendingOrders);
                if (reconciledOrders.Count > 0)
                {
                    _service.RestorePendingLimitOrders(reconciledOrders);
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Recovery",
                        $"Pending Limit-Orders wiederhergestellt: {reconciledOrders.Count} Order(s) mit TP-Werten"));
                }
                else if (savedPendingOrders.Count > 0)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Recovery",
                        $"Keine Pending-Orders wiederhergestellt: {savedPendingOrders.Count} DB-Einträge waren veraltet (nicht mehr auf BingX)"));
                }
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                $"State-Recovery fehlgeschlagen (nicht kritisch): {ex.Message}"));
        }

        _service.Start();

        // Offene Positionen prüfen und SL/Breakeven setzen + TP-Recovery
        await RecoverOpenPositionsAsync();
    }

    /// <summary>
    /// Prüft alle offenen Positionen beim Start und setzt fehlende SL/Auto-Breakeven.
    /// Holt fehlende TP-Orders nach (z.B. wenn App zwischen Limit-Order und Fill neugestartet wurde).
    /// Stellt sicher dass Positionen die vor dem Neustart eröffnet wurden geschützt sind.
    /// </summary>
    private async Task RecoverOpenPositionsAsync()
    {
        try
        {
            var positions = await _restClient!.GetPositionsAsync();
            if (positions.Count == 0) return;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"Prüfe {positions.Count} offene Position(en) auf fehlende SL/Breakeven/TP..."));

            var tickers = await _publicClient!.GetAllTickersAsync();
            var tickerMap = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);

            // Alle offenen Orders EINMAL laden (statt pro Position → weniger API-Calls)
            var allOpenOrders = await _restClient.GetOpenOrdersAsync();
            // 24.04.2026 (bingxbot-Audit): Frueher hier `tpOrdersBySymbol`-Dictionary das jede LIMIT-Order
            // ohne StopPrice als TP zaehlte — dabei aber nicht zwischen Entry-Limits und TP-Limits
            // unterschied. Bei Triple-Entry-Restbestaenden (mehrere offene Long-Buy-Limits) waere die
            // Zaehlung ueberhoeht. Zugleich war das Dictionary an `RecoverMissingTpOrdersAsync` uebergeben,
            // dort aber NIE GELESEN — der Bug hatte keine Wirkung. Entfernt: kein toter Code mehr,
            // RecoverMissingTpOrdersAsync nutzt seine eigene side-aware Heuristik (`isTpForLong/Short`).

            foreach (var pos in positions)
            {
                if (!tickerMap.TryGetValue(pos.Symbol, out var currentPrice) || currentPrice <= 0)
                    continue;

                // SL/TP aus BingX-Orders lesen (brauchen wir für SK-BE-Check + Signal-Registrierung)
                try
                {
                    decimal? slPrice = null, tpPrice = null;
                    foreach (var order in allOpenOrders.Where(o => o.Symbol == pos.Symbol))
                    {
                        if (order.Type == OrderType.StopMarket && order.StopPrice.HasValue)
                            slPrice = order.StopPrice.Value;
                        if (order.Type is OrderType.TakeProfitMarket or OrderType.TakeProfitLimit && order.StopPrice.HasValue)
                            tpPrice = order.StopPrice.Value;
                    }

                    // SK-Buch BE-Recovery: Wenn Gewinn ≥ 2× SL-Distanz → BE setzen (Workflow 4.2)
                    // Nutzt den aktuellen BingX-SL als slDist-Basis (Fallback: kein BE wenn SL fehlt)
                    if (slPrice.HasValue && pos.EntryPrice > 0)
                    {
                        var slDist = Math.Abs(pos.EntryPrice - slPrice.Value);
                        var currentProfit = pos.Side == Side.Buy
                            ? currentPrice - pos.EntryPrice
                            : pos.EntryPrice - currentPrice;

                        var isLongSlBelowEntry = pos.Side == Side.Buy && slPrice.Value < pos.EntryPrice;
                        var isShortSlAboveEntry = pos.Side == Side.Sell && slPrice.Value > pos.EntryPrice;
                        var slStillAtRisk = isLongSlBelowEntry || isShortSlAboveEntry;

                        if (slDist > 0 && currentProfit >= slDist * 2m && slStillAtRisk)
                        {
                            var beSl = pos.Side == Side.Buy
                                ? pos.EntryPrice * 1.0015m
                                : pos.EntryPrice * 0.9985m;
                            // Snapshot-Report-Fix Befund 3 / A0.6 — Sanity-Guard auch im Recovery-Pfad.
                            // breakevenSet=true weil wir hier bewusst auf BE gehen; verhindert exotische
                            // Pi-Floating-Point-Faelle in denen 1.0015 × Tick-Rounding den Buffer reisst.
                            // Bei Reject NUR den Push ueberspringen, NICHT die Signal-Registrierung darunter.
                            var beSanity = StopLossSanityGuard.Validate(
                                pos.Side, pos.EntryPrice, beSl,
                                breakevenSet: true, partialClosed: false, runnerActive: false);
                            if (!beSanity.IsAcceptable)
                            {
                                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                                    $"{pos.Symbol}: BE-Push abgelehnt — {beSanity.RejectReason}",
                                    pos.Symbol));
                            }
                            else
                            {
                                try
                                {
                                    await _restClient.SetPositionSlTpAsync(pos.Symbol, pos.Side, beSl, null);
                                    slPrice = beSl; // Signal-Registrierung unten nutzt den neuen SL
                                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Recovery",
                                        $"{pos.Symbol}: SK-Buch BE gesetzt (Gewinn >= 2× SL-Distanz, Workflow 4.2) → SL={beSl:F8}",
                                        pos.Symbol));
                                }
                                catch (Exception ex)
                                {
                                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                                        $"{pos.Symbol}: BE-SL fehlgeschlagen: {ex.Message}", pos.Symbol));
                                }
                            }
                        }
                    }

                    // Signal im Service registrieren damit das UI die Werte anzeigt
                    if (slPrice.HasValue || tpPrice.HasValue)
                    {
                        var signal = new SignalResult(
                            pos.Side == Side.Buy ? Signal.Long : Signal.Short,
                            0.5m, pos.EntryPrice, slPrice, tpPrice, "Recovery: Aus BingX-Orders wiederhergestellt");
                        _service!.RestorePositionSignal(pos.Symbol, pos.Side, signal);

                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Recovery",
                            $"{pos.Symbol}: Wiederhergestellt (SL={slPrice?.ToString("F8") ?? "---"}, TP={tpPrice?.ToString("F8") ?? "---"})"));
                    }
                    else
                    {
                        // Kein SL auf BingX → Standard-SL berechnen (ATR-basiert wie bei Eröffnung)
                        var recoverySl = await CalculateStandardSlAsync(pos);
                        try
                        {
                            await _restClient.SetPositionSlTpAsync(pos.Symbol, pos.Side, recoverySl, null);
                            var signal = new SignalResult(
                                pos.Side == Side.Buy ? Signal.Long : Signal.Short,
                                0.5m, pos.EntryPrice, recoverySl, null, "Recovery: Standard-SL gesetzt (ATR-basiert)");
                            _service!.RestorePositionSignal(pos.Symbol, pos.Side, signal);

                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                                $"{pos.Symbol}: Standard-SL gesetzt → SL={recoverySl:F8}",
                                pos.Symbol));
                        }
                        catch (Exception emergEx)
                        {
                            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Recovery",
                                $"{pos.Symbol}: KRITISCH - SL konnte nicht gesetzt werden: {emergEx.Message}. Position UNGESCHÜTZT!",
                                pos.Symbol));
                            if (_botSettings.EnableDesktopNotifications)
                                _eventBus.PublishNotification("POSITION UNGESCHÜTZT", $"{pos.Symbol}: SL konnte nicht gesetzt werden!");
                        }
                    }

                    // TP-Recovery: Position hat ExitState mit TP-Werten aber keine TP-Orders auf BingX.
                    // Passiert wenn App zwischen Limit-Order-Platzierung und Fill neugestartet wurde
                    // und die Limit-Order inzwischen gefüllt wurde (TP wurde nie als Reduce-Only platziert).
                    await RecoverMissingTpOrdersAsync(pos, allOpenOrders);
                }
                catch { /* Best-effort */ }
            }

            // DB aufräumen: Pending Limit-Orders clearen die jetzt als Positionen existieren
            if (_dbService != null)
            {
                try { await _dbService.ClearPendingLimitOrdersAsync(); }
                catch { /* best-effort */ }
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                $"Position-Recovery fehlgeschlagen: {ex.Message}"));
        }
    }

    /// <summary>
    /// Prüft ob eine Position TP-Orders braucht und platziert sie nach.
    /// Wird aufgerufen wenn eine Position existiert aber keine TP-Orders auf BingX gefunden werden.
    /// TP-Werte kommen aus dem wiederhergestellten ExitState (enthält das Original-Signal mit TP1/TP2).
    /// </summary>
    private async Task RecoverMissingTpOrdersAsync(Position pos, IReadOnlyList<Order> allOpenOrders)
    {
        if (_service == null) return;

        // Prüfen ob bereits TP-Orders existieren (Limit Reduce-Only)
        // BingX zeigt TP-Limit-Orders als reguläre LIMIT-Orders im Orderbuch
        var hasExistingTpOrders = false;
        foreach (var order in allOpenOrders)
        {
            if (order.Symbol != pos.Symbol) continue;
            // TP-Limit-Orders: Sell-Limit für Long, Buy-Limit für Short (Reduce-Only)
            if (order.Type == OrderType.Limit)
            {
                var isTpForLong = pos.Side == Side.Buy && order.Side == Side.Sell;
                var isTpForShort = pos.Side == Side.Sell && order.Side == Side.Buy;
                if (isTpForLong || isTpForShort)
                {
                    hasExistingTpOrders = true;
                    break;
                }
            }
            // Native TP-Orders (TakeProfitMarket/TakeProfitLimit)
            if (order.Type is OrderType.TakeProfitMarket or OrderType.TakeProfitLimit)
            {
                hasExistingTpOrders = true;
                break;
            }
        }

        if (hasExistingTpOrders) return;

        // Keine TP-Orders auf BingX → prüfen ob ExitState TP-Werte hat
        var posKey = $"{pos.Symbol}_{pos.Side}";
        var exitStates = _service.GetExitStatesSnapshot();
        if (!exitStates.TryGetValue(posKey, out var exitState)) return;
        if (exitState.Signal?.TakeProfit is not > 0) return;

        // TP-Werte vorhanden aber keine Orders auf BingX → TP nachholen
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
            $"{pos.Symbol}: Position ohne TP-Orders erkannt → platziere TP1/TP2 (Recovery)",
            pos.Symbol));

        try
        {
            // Nutze die PlaceTpLimitOrdersAfterFillAsync vom LiveTradingService
            // via das Signal das bereits in _positionSignals wiederhergestellt ist
            var signal = exitState.Signal;
            await _service.RecoverTpOrdersAsync(pos.Symbol, pos.Side, pos.Quantity, signal);

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Recovery",
                $"{pos.Symbol}: TP-Orders erfolgreich nachgeholt (TP1={signal.TakeProfit:F8}, TP2={signal.TakeProfit2:F8})",
                pos.Symbol));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                $"{pos.Symbol}: TP-Recovery fehlgeschlagen (Fallback: Bot-seitig via PriceTickerLoop): {ex.Message}",
                pos.Symbol));
        }
    }

    /// <summary>
    /// Gleicht gespeicherte Pending-Orders mit dem aktuellen BingX-Orderbuch ab.
    /// Entfernt Einträge deren OrderId nicht mehr auf BingX existiert (gefüllt, gecancelt, expired)
    /// und aktualisiert die DB mit der bereinigten Liste.
    /// Bei API-Fehler: Fallback auf die gespeicherten Orders (best-effort, kein Abbruch).
    /// </summary>
    private async Task<Dictionary<string, PendingLimitOrderState>> ReconcilePendingLimitOrdersAsync(
        Dictionary<string, PendingLimitOrderState> savedStates)
    {
        if (_restClient == null || savedStates.Count == 0) return savedStates;

        try
        {
            // Alle aktuell offenen Orders von BingX holen (ohne Symbol-Filter → eine API-Call)
            var allOpenOrders = await _restClient.GetOpenOrdersAsync();
            var liveOrderIds = new HashSet<string>(allOpenOrders.Select(o => o.OrderId));

            var valid = new Dictionary<string, PendingLimitOrderState>();
            var staleSymbols = new List<string>();
            foreach (var kvp in savedStates)
            {
                if (liveOrderIds.Contains(kvp.Value.OrderId))
                {
                    valid[kvp.Key] = kvp.Value;
                }
                else
                {
                    staleSymbols.Add(kvp.Key);
                }
            }

            if (staleSymbols.Count > 0)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Recovery",
                    $"{staleSymbols.Count} Pending-Order(s) nicht mehr auf BingX → verworfen: {string.Join(", ", staleSymbols)}"));

                // DB mit abgeglichener Liste aktualisieren damit wir beim nächsten Start sauber starten
                if (_dbService != null)
                {
                    try
                    {
                        if (valid.Count > 0)
                            await _dbService.SavePendingLimitOrdersAsync(valid);
                        else
                            await _dbService.ClearPendingLimitOrdersAsync();
                    }
                    catch { /* Best-effort: DB-Update nicht kritisch */ }
                }
            }

            return valid;
        }
        catch (Exception ex)
        {
            // Bei API-Fehler: Fallback auf gespeicherte Orders (kein Abbruch).
            // Sicherer Default als kompletter Verlust der Pending-Orders — Invalidierung
            // greift ohnehin im PriceTickerLoop wenn Orders wirklich stale sind.
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                $"Pending-Order-Abgleich mit BingX fehlgeschlagen: {ex.Message} — nutze DB-Einträge unverändert"));
            return savedStates;
        }
    }

    /// <summary>
    /// Stoppt das Live-Trading. Offene Positionen bleiben auf BingX bestehen.
    /// _restClient bleibt erhalten bis Dispose() — verhindert NullRef in nachlaufenden Tasks.
    /// </summary>
    public async Task StopAsync()
    {
        // Kill-Switch deaktivieren (sauberer Stop → keine verwaisten Cancels)
        // Nur wenn der Endpoint unterstützt wird (manche Accounts haben cancelAllAfter nicht)
        if (_restClient != null)
        {
            try { await _restClient.DeactivateKillSwitchAsync(); }
            catch { /* Endpoint nicht verfügbar oder Netzwerkfehler — kein Problem beim Stop */ }
        }

        // SK-VERIFY: [6.1] ExitStates + Runtime-State VOR StopAsync speichern (danach gecleart)
        if (_service != null && _dbService != null)
        {
            try
            {
                var exitStates = _service.GetExitStatesSnapshot();
                if (exitStates.Count > 0)
                    await _dbService.SaveExitStatesAsync(exitStates);
                var (tradesToday, losses) = _service.GetRuntimeStateSnapshot();
                await _dbService.SaveRuntimeStateAsync(tradesToday, losses, _activeStrategyName);

                // Pending Limit-Orders persistieren (TP-Recovery nach Neustart)
                var pendingOrders = _service.GetPendingLimitOrdersSnapshot();
                if (pendingOrders.Count > 0)
                    await _dbService.SavePendingLimitOrdersAsync(pendingOrders);
                else
                    await _dbService.ClearPendingLimitOrdersAsync();
            }
            catch { /* Best-effort: DB-Fehler darf Stop nicht blockieren */ }
        }

        if (_service != null)
        {
            await _service.StopAsync();
            _service.Dispose();
            _service = null;
        }
        // _restClient NICHT null setzen — PriceTickerLoop könnte noch eine letzte Iteration laufen.
        // Wird bei Dispose() oder erneutem ConnectAsync() aufgeräumt.

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
            "Live-Trading gestoppt. Offene Positionen bleiben auf BingX bestehen."));
    }

    /// <summary>
    /// Notfall-Stop: Schließt ALLE echten Positionen auf BingX sofort.
    /// Wartet auf vollständiges Dispose des Services bevor _restClient freigegeben wird.
    /// </summary>
    public async Task EmergencyStopAsync()
    {
        if (_service != null)
        {
            // SK-VERIFY: [6.1] ExitStates + Runtime-State auch bei EmergencyStop speichern
            try
            {
                if (_dbService != null)
                {
                    var exitStates = _service.GetExitStatesSnapshot();
                    if (exitStates.Count > 0)
                        await _dbService.SaveExitStatesAsync(exitStates);
                    var (tradesToday, losses) = _service.GetRuntimeStateSnapshot();
                    await _dbService.SaveRuntimeStateAsync(tradesToday, losses, _activeStrategyName);
                    // EmergencyStop schließt alle Positionen → pending orders nicht mehr relevant
                    await _dbService.ClearPendingLimitOrdersAsync();
                }
            }
            catch { /* Best-effort: DB-Fehler darf EmergencyStop nicht blockieren */ }

            // EmergencyStopAsync() wartet intern auf Task.WhenAll(closeTasks) —
            // _restClient wird erst NACH vollständigem Abschluss genullt.
            await _service.EmergencyStopAsync();
            _service.Dispose();
            _service = null;
        }
        // Jetzt sicher: Alle Close-Tasks sind abgeschlossen, _restClient kann null werden
        _restClient = null;
    }

    /// <summary>
    /// Stellt SL/TP-Signale für bestehende Positionen nach App-Neustart wieder her.
    /// Liest offene Conditional Orders von BingX und registriert sie im Service.
    /// </summary>
    public async Task RestorePositionSignalsAsync(IReadOnlyList<Position> positions)
    {
        if (_restClient == null || _service == null) return;

        try
        {
            var openOrders = await _restClient.GetOpenOrdersAsync();
            var restored = 0;

            foreach (var pos in positions)
            {
                decimal? sl = null;
                decimal? tp = null;

                foreach (var order in openOrders)
                {
                    if (order.Symbol != pos.Symbol) continue;
                    if (order.Type == OrderType.StopMarket && order.StopPrice.HasValue)
                        sl = order.StopPrice.Value;
                    else if (order.Type is OrderType.TakeProfitMarket or OrderType.TakeProfitLimit && order.StopPrice.HasValue)
                        tp = order.StopPrice.Value;
                }

                if (sl.HasValue || tp.HasValue)
                {
                    var signal = new SignalResult(
                        pos.Side == Side.Buy ? Signal.Long : Signal.Short,
                        0.5m, pos.EntryPrice, sl, tp,
                        "Wiederhergestellt nach Neustart");

                    _service.RegisterPositionSignal(pos.Symbol, pos.Side, signal, pos.MarkPrice);
                    restored++;

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                        $"{pos.Symbol}: SL/TP wiederhergestellt (SL={sl?.ToString("G8") ?? "---"} TP={tp?.ToString("G8") ?? "---"})",
                        pos.Symbol));
                }
            }

            if (restored > 0)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                    $"{restored} Position(en) mit SL/TP wiederhergestellt"));
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                $"SL/TP-Wiederherstellung fehlgeschlagen: {ex.Message}"));
        }
    }

    /// <summary>
    /// Berechnet den Standard-SL wie bei Tradeeröffnung: ATR-basiert mit vol-adaptiven Multiplikatoren.
    /// Fallback auf 3% vom Entry wenn keine Candle-Daten verfügbar.
    /// </summary>
    private async Task<decimal> CalculateStandardSlAsync(Position pos)
    {
        try
        {
            var candles = await _publicClient!.GetKlinesAsync(
                pos.Symbol, _scannerSettings.ScanTimeFrame,
                DateTime.UtcNow.AddHours(-100), DateTime.UtcNow).ConfigureAwait(false);

            if (candles.Count >= 20)
            {
                var atr = Engine.Indicators.IndicatorHelper.CalculateAtr(candles);
                if (atr.Count > 0 && atr[^1].HasValue && atr[^1]!.Value > 0)
                {
                    var atrValue = atr[^1]!.Value;
                    var atrPercentile = Engine.Indicators.IndicatorHelper.CalculateAtrPercentile(candles);
                    // Multi-TF Standalone: Einheitlicher SL-Multiplikator (2× ATR als pragmatischer Notfall-SL).
                    // Höher bei extremer Vola, niedriger bei ruhigem Markt.
                    var slMult = atrPercentile switch
                    {
                        < 20 => 1.5m,
                        < 50 => 1.8m,
                        < 75 => 2.0m,
                        _ => 2.5m,
                    };

                    var sl = pos.Side == Side.Buy
                        ? pos.EntryPrice - atrValue * slMult
                        : pos.EntryPrice + atrValue * slMult;

                    // Mindestens 0.5% Abstand (Spread-Schutz)
                    var minDist = pos.EntryPrice * 0.005m;
                    if (Math.Abs(pos.EntryPrice - sl) < minDist)
                        sl = pos.Side == Side.Buy ? pos.EntryPrice - minDist : pos.EntryPrice + minDist;

                    return sl;
                }
            }
        }
        catch { /* Candle-Laden fehlgeschlagen → Fallback */ }

        // Fallback: 3% vom Entry (skaliert mit Leverage, mindestens 1.5%)
        var fallbackPercent = Math.Max(0.015m, pos.Leverage > 0 ? 0.03m / pos.Leverage : 0.03m);
        return pos.Side == Side.Buy
            ? pos.EntryPrice * (1m - fallbackPercent)
            : pos.EntryPrice * (1m + fallbackPercent);
    }

    // 24.04.2026 Phase-4-Audit m3: CalculateRecoveryAtrAsync entfernt — Dead-Code, nirgends aufgerufen.
    // Die einzige BE-Recovery (RecoverOpenPositionsAsync Zeilen 304-339) nutzt direkt den BingX-SL als
    // Distanz-Basis, kein ATR-Recompute mehr noetig.

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service?.Dispose();
        _service = null;
        _restClient = null;
        _rateLimiter?.Dispose();
        _rateLimiter = null;
        _httpClient?.Dispose();
        _httpClient = null;
    }
}

/// <summary>Ergebnis von LiveTradingManager.ConnectAsync().</summary>
public record ConnectResult(AccountInfo Account, IReadOnlyList<Position> Positions);
