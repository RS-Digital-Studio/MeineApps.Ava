using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.ATI;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingXBot.Services;

/// <summary>
/// Orchestriert 3 parallele Trading-Modi (Scalping M15, DayTrading H1, Swing H4).
/// Jeder Modus hat eigene Strategie + Scanner-Settings, aber geteilten RiskManager + API-Client.
/// </summary>
public class MultiModeOrchestrator : IDisposable
{
    private readonly IPublicMarketDataClient _publicClient;
    private readonly BotEventBus _eventBus;
    private readonly RiskSettings _riskSettings;
    private readonly BotSettings _botSettings;
    private readonly BotDatabaseService? _dbService;
    private readonly AdaptiveTradingIntelligence? _ati;

    // Geteilter RiskManager: Zählt Positionen + Drawdown über ALLE Modi
    private RiskManager? _sharedRiskManager;

    // 3 Service-Instanzen (Paper oder Live)
    private readonly Dictionary<TradingModePreset, TradingServiceBase> _services = new();
    private readonly Dictionary<TradingModePreset, StrategyManager> _strategyManagers = new();
    private readonly Dictionary<TradingModePreset, ScannerSettings> _scannerSettings = new();

    // Welche Modi aktiv sind
    public IReadOnlyDictionary<TradingModePreset, TradingServiceBase> ActiveServices => _services;
    public bool IsAnyRunning => _services.Values.Any(s => s.IsRunning);

    // Für Live-Trading: Geteilter RestClient
    private Exchange.BingXRestClient? _restClient;

    public MultiModeOrchestrator(
        IPublicMarketDataClient publicClient,
        BotEventBus eventBus,
        RiskSettings riskSettings,
        BotSettings botSettings,
        BotDatabaseService? dbService = null,
        AdaptiveTradingIntelligence? ati = null)
    {
        _publicClient = publicClient;
        _eventBus = eventBus;
        _riskSettings = riskSettings;
        _botSettings = botSettings;
        _dbService = dbService;
        _ati = ati;
    }

    /// <summary>
    /// Erstellt und startet alle 3 Modi im Paper-Modus.
    /// </summary>
    public void StartPaper(decimal initialBalance = 10_000m)
    {
        _sharedRiskManager = new RiskManager(_riskSettings, NullLogger<RiskManager>.Instance);

        foreach (var mode in new[] { TradingModePreset.Scalping, TradingModePreset.DayTrading, TradingModePreset.Swing })
        {
            var scanSettings = CreateScannerSettings(mode);
            var stratManager = CreateStrategyManager(mode);
            _scannerSettings[mode] = scanSettings;
            _strategyManagers[mode] = stratManager;

            var service = new PaperTradingService(
                _publicClient, stratManager, _riskSettings, scanSettings, _eventBus, _botSettings);
            service.RiskManagerOverride = _sharedRiskManager;

            if (_ati != null)
            {
                _ati.RegisterStrategies(StrategyFactory.AvailableStrategies.Select(StrategyFactory.Create));
                service.ATI = _ati;
            }

            _services[mode] = service;
            ((PaperTradingService)service).Start(initialBalance);

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"[{ModeLabel(mode)}] Paper-Trading gestartet ({scanSettings.ScanTimeFrame})"));
        }
    }

    /// <summary>
    /// Erstellt und startet alle 3 Modi im Live-Modus.
    /// restClient muss bereits verbunden sein.
    /// </summary>
    public void StartLive(Exchange.BingXRestClient restClient)
    {
        _restClient = restClient;
        _sharedRiskManager = new RiskManager(_riskSettings, NullLogger<RiskManager>.Instance);

        foreach (var mode in new[] { TradingModePreset.Scalping, TradingModePreset.DayTrading, TradingModePreset.Swing })
        {
            var scanSettings = CreateScannerSettings(mode);
            var stratManager = CreateStrategyManager(mode);
            _scannerSettings[mode] = scanSettings;
            _strategyManagers[mode] = stratManager;

            var service = new LiveTradingService(
                restClient, _publicClient, stratManager, _riskSettings, scanSettings, _eventBus, _botSettings,
                dbService: _dbService);
            service.RiskManagerOverride = _sharedRiskManager;

            if (_ati != null)
            {
                _ati.RegisterStrategies(StrategyFactory.AvailableStrategies.Select(StrategyFactory.Create));
                service.ATI = _ati;
            }

            _services[mode] = service;
            service.Start();

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"[{ModeLabel(mode)}] Live-Trading gestartet ({scanSettings.ScanTimeFrame})"));
        }
    }

    /// <summary>Stoppt alle Modi.</summary>
    public async Task StopAllAsync()
    {
        foreach (var (mode, service) in _services)
        {
            try
            {
                await service.StopAsync();
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                    $"[{ModeLabel(mode)}] Gestoppt"));
            }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Engine",
                    $"[{ModeLabel(mode)}] Stop fehlgeschlagen: {ex.Message}"));
            }
        }
        _services.Clear();
        _strategyManagers.Clear();
        _scannerSettings.Clear();
    }

    /// <summary>Stoppt einen einzelnen Modus.</summary>
    public async Task StopModeAsync(TradingModePreset mode)
    {
        if (_services.TryGetValue(mode, out var service))
        {
            await service.StopAsync();
            _services.Remove(mode);
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"[{ModeLabel(mode)}] Gestoppt"));
        }
    }

    /// <summary>Notfall-Stop: Alle Positionen schließen über alle Modi.</summary>
    public async Task EmergencyStopAllAsync()
    {
        foreach (var (mode, service) in _services)
        {
            try { await service.EmergencyStopAsync(); }
            catch { /* Best-effort */ }
        }
        _services.Clear();
    }

    /// <summary>Geteilter RiskManager (für UI Rolling-Metriken).</summary>
    public RiskManager? SharedRiskManager => _sharedRiskManager;

    private ScannerSettings CreateScannerSettings(TradingModePreset mode)
    {
        var preset = TradingModeDefaults.GetScannerPreset(mode);
        return new ScannerSettings
        {
            ScanTimeFrame = preset.ScanTimeFrame,
            MinVolume24h = preset.MinVolume24h,
            MinPriceChange = preset.MinPriceChange,
            MaxResults = preset.MaxResults,
            UseM15EntryTiming = preset.UseM15EntryTiming,
            // Watchlist vom User übernehmen (gleich für alle Modi)
            Whitelist = _botSettings.Scanner?.Whitelist ?? new(),
            Blacklist = _botSettings.Scanner?.Blacklist ?? new()
        };
    }

    private StrategyManager CreateStrategyManager(TradingModePreset mode)
    {
        var strategy = StrategyFactory.Create("CryptoTrendPro");
        if (strategy is CryptoTrendProStrategy ctp)
            ctp.ApplyPreset(mode);

        var manager = new StrategyManager();
        manager.SetStrategy(strategy);
        return manager;
    }

    private static string ModeLabel(TradingModePreset mode) => mode switch
    {
        TradingModePreset.Scalping => "S",
        TradingModePreset.DayTrading => "D",
        TradingModePreset.Swing => "W",
        _ => "?"
    };

    public void Dispose()
    {
        foreach (var service in _services.Values)
            service.Dispose();
        _services.Clear();
    }
}
