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

        // Verbindung testen
        var account = await _restClient.GetAccountInfoAsync();
        var positions = await _restClient.GetPositionsAsync();

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
            $"Live-Verbindung hergestellt. Balance: {account.Balance:N2} USDT"));

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

        // Strategie aktivieren
        var strategy = StrategyFactory.Create(strategyName);
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
    }

    /// <summary>
    /// Stoppt das Live-Trading. Offene Positionen bleiben auf BingX bestehen.
    /// </summary>
    public async Task StopAsync()
    {
        await SaveAtiStateAsync();

        if (_service != null)
        {
            await _service.StopAsync();
            _service.Dispose();
            _service = null;
        }
        _restClient = null;

        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
            "Live-Trading gestoppt. Offene Positionen bleiben auf BingX bestehen."));
    }

    /// <summary>
    /// Notfall-Stop: Schließt ALLE echten Positionen auf BingX sofort.
    /// </summary>
    public async Task EmergencyStopAsync()
    {
        if (_service != null)
        {
            await _service.EmergencyStopAsync();
            _service.Dispose();
            _service = null;
        }
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
                    else if (order.Type == OrderType.TakeProfitMarket && order.StopPrice.HasValue)
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service?.Dispose();
        _service = null;
        _restClient = null;
        _httpClient?.Dispose();
        _httpClient = null;
    }
}

/// <summary>Ergebnis von LiveTradingManager.ConnectAsync().</summary>
public record ConnectResult(AccountInfo Account, IReadOnlyList<Position> Positions);
