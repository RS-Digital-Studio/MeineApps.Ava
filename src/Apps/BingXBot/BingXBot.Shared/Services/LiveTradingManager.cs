using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.ATI;
using BingXBot.Engine.Strategies;
using BingXBot.Exchange;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBot.Services;

/// <summary>
/// Verwaltet die Live-Trading-Infrastruktur: Client-Erstellung, Verbindung,
/// Service-Lifecycle und ATI-Integration. Kapselt die gesamte Live-spezifische
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
    private readonly AdaptiveTradingIntelligence? _ati;
    private readonly BotSettings _botSettings;

    // Wiederverwendbare Infrastruktur (vermeidet Socket-Exhaustion und Semaphore-Leaks bei Start/Stop)
    private HttpClient? _httpClient;
    private RateLimiter? _rateLimiter;
    private BingXRestClient? _restClient;
    private LiveTradingService? _service;
    private bool _disposed;

    /// <summary>Aktueller REST-Client (null wenn nicht verbunden).</summary>
    public BingXRestClient? RestClient => _restClient;

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
        AdaptiveTradingIntelligence? ati = null)
    {
        _secureStorage = secureStorage;
        _publicClient = publicClient;
        _strategyManager = strategyManager;
        _riskSettings = riskSettings;
        _scannerSettings = scannerSettings;
        _eventBus = eventBus;
        _botSettings = botSettings;
        _dbService = dbService;
        _ati = ati;
    }

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
        catch { /* Fallback auf Standard-Raten */ }

        // Verbindung testen
        var account = await _restClient.GetAccountInfoAsync();
        var positions = await _restClient.GetPositionsAsync();

        // Hedge-Modus erkennen + automatisch umschalten für TradFi
        var isHedge = await _restClient.IsHedgeModeAsync();
        if (_scannerSettings.EnableTradFi && !isHedge)
        {
            // Automatisch auf Hedge umschalten (nur möglich wenn keine Positionen offen)
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
                        "Hedge-Modus konnte nicht aktiviert werden. TradFi-Trading nicht möglich."));
                }
            }
            else
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                    $"TradFi deaktiviert: Hedge-Modus erfordert 0 offene Positionen (aktuell: {positions.Count}). Schließe alle Positionen und starte den Bot neu."));
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

        // LiveTradingService erstellen (dbService für ATI Auto-Save)
        _service = new LiveTradingService(
            _restClient,
            _publicClient,
            _strategyManager,
            _riskSettings,
            _scannerSettings,
            _eventBus,
            _botSettings,
            dbService: _dbService);

        // Echte Fee-Rates vom Account setzen (statt hardcoded)
        _service.SetCommissionRates(CommissionTakerRate, CommissionMakerRate);

        // Strategie aktivieren + Trading-Modus-Preset anwenden
        var strategy = StrategyFactory.Create(strategyName);
        if (strategy is CryptoTrendProStrategy ctp)
        {
            var preset = _botSettings.LastTradingModePreset;
            ctp.ApplyPreset(preset);
        }
        _strategyManager.SetStrategy(strategy);

        // ATI-Integration
        if (_ati != null)
        {
            _ati.RegisterStrategies(StrategyFactory.AvailableStrategies.Select(StrategyFactory.Create));
            _service.ATI = _ati;
            await LoadAtiStateAsync();

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "ATI",
                $"Adaptive Trading Intelligence aktiviert ({StrategyFactory.AvailableStrategies.Length} Strategien im Ensemble)"));
        }

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
            $"LIVE-TRADING AKTIV mit Strategie '{strategyName}'. Echtes Geld!"));

        _service.Start();

        // Offene Positionen prüfen und SL/Breakeven setzen
        await RecoverOpenPositionsAsync();
    }

    /// <summary>
    /// Prüft alle offenen Positionen beim Start und setzt fehlende SL/Auto-Breakeven.
    /// Stellt sicher dass Positionen die vor dem Neustart eröffnet wurden geschützt sind.
    /// </summary>
    private async Task RecoverOpenPositionsAsync()
    {
        try
        {
            var positions = await _restClient!.GetPositionsAsync();
            if (positions.Count == 0) return;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"Prüfe {positions.Count} offene Position(en) auf fehlende SL/Breakeven..."));

            var tickers = await _publicClient!.GetAllTickersAsync();
            var tickerMap = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);

            foreach (var pos in positions)
            {
                if (!tickerMap.TryGetValue(pos.Symbol, out var currentPrice) || currentPrice <= 0)
                    continue;

                // PnL berechnen
                var pnlPercent = pos.Side == Side.Buy
                    ? (currentPrice - pos.EntryPrice) / pos.EntryPrice * 100m
                    : (pos.EntryPrice - currentPrice) / pos.EntryPrice * 100m;

                // Auto-Breakeven prüfen: Gewinn% >= Leverage%
                if (pnlPercent >= pos.Leverage && pos.Leverage > 0)
                {
                    // Breakeven = Entry + Round-Trip-Fees (0.1%) + Sicherheitspuffer
                    var beSl = pos.Side == Side.Buy
                        ? pos.EntryPrice * 1.0015m
                        : pos.EntryPrice * 0.9985m;

                    try
                    {
                        await _restClient.SetPositionSlTpAsync(pos.Symbol, pos.Side, beSl, null);
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Recovery",
                            $"{pos.Symbol}: Auto-Breakeven gesetzt (PnL={pnlPercent:F1}%, Lev={pos.Leverage}x) → SL={beSl:F8}",
                            pos.Symbol));
                    }
                    catch (Exception ex)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                            $"{pos.Symbol}: Breakeven-SL fehlgeschlagen: {ex.Message}", pos.Symbol));
                    }
                }
                // SL/TP aus BingX-Orders lesen und ins Signal-Dictionary schreiben (für UI-Anzeige)
                try
                {
                    var orders = await _restClient.GetOpenOrdersAsync(pos.Symbol);
                    decimal? slPrice = null, tpPrice = null;
                    foreach (var order in orders)
                    {
                        if (order.Type == OrderType.StopMarket && order.StopPrice.HasValue)
                            slPrice = order.StopPrice.Value;
                        if (order.Type is OrderType.TakeProfitMarket or OrderType.TakeProfitLimit && order.StopPrice.HasValue)
                            tpPrice = order.StopPrice.Value;
                    }

                    // Signal im Service registrieren damit das UI die Werte anzeigt
                    if (slPrice.HasValue || tpPrice.HasValue)
                    {
                        var signal = new SignalResult(
                            pos.Side == Side.Buy ? Signal.Long : Signal.Short,
                            0.5m, pos.EntryPrice, slPrice, tpPrice, "Recovery: Aus BingX-Orders wiederhergestellt");
                        _service!.RestorePositionSignal(pos.Symbol, pos.Side, signal);

                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Recovery",
                            $"{pos.Symbol}: Wiederhergestellt (SL={slPrice?.ToString("F8") ?? "---"}, TP={tpPrice?.ToString("F8") ?? "---"}, PnL={pnlPercent:F1}%)"));
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
                                $"{pos.Symbol}: Standard-SL gesetzt → SL={recoverySl:F8}. PnL={pnlPercent:F1}%",
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
                }
                catch { /* Best-effort */ }
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                $"Position-Recovery fehlgeschlagen: {ex.Message}"));
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

        await SaveAtiStateAsync();

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
            // ATI-Lernzustand retten bevor alles geschlossen wird —
            // EmergencyStop kann durch Crash/Absturz ausgelöst werden, State sonst verloren
            await SaveAtiStateAsync();

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

    // === ATI-Persistenz ===

    /// <summary>Setzt den ATI-Lernzustand komplett zurück und löscht ihn aus der DB.</summary>
    public async Task ResetAtiStateAsync()
    {
        if (_ati == null) return;
        _ati.Reset();
        if (_dbService != null)
        {
            try
            {
                await _dbService.SaveAtiStateAsync(""); // Leerer State = DB-Eintrag überschreiben
            }
            catch { /* Ignorieren wenn DB nicht bereit */ }
        }
        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "ATI",
            "Lernzustand komplett zurückgesetzt (alle Buckets, Gewichte und Transitions gelöscht)"));
    }

    /// <summary>Lädt ATI-Lernzustand aus der DB.</summary>
    public async Task LoadAtiStateAsync()
    {
        if (_ati == null || _dbService == null) return;
        try
        {
            var json = await _dbService.LoadAtiStateAsync();
            if (!string.IsNullOrEmpty(json))
            {
                _ati.DeserializeState(json);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "ATI",
                    "Lernzustand wiederhergestellt"));
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "ATI",
                $"Lernzustand konnte nicht geladen werden: {ex.Message}"));
        }
    }

    /// <summary>Speichert ATI-Lernzustand in die DB.</summary>
    public async Task SaveAtiStateAsync()
    {
        if (_ati == null || _dbService == null) return;
        try
        {
            var json = _ati.SerializeState();
            await _dbService.SaveAtiStateAsync(json);
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "ATI",
                "Lernzustand gespeichert"));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "ATI",
                $"Lernzustand konnte nicht gespeichert werden: {ex.Message}"));
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
                    var (slMult, _, _, _) = TradingModeDefaults.GetVolAdaptiveMultipliers(
                        _botSettings.LastTradingModePreset, atrPercentile);

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
