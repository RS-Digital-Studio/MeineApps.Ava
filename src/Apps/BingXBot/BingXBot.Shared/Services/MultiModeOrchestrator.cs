using System.Collections.Concurrent;
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

    // 3 Service-Instanzen (Paper oder Live) - ConcurrentDictionary für Thread-Safety
    // (StopModeAsync kann parallel zu IsAnyRunning aufgerufen werden)
    private readonly ConcurrentDictionary<TradingModePreset, TradingServiceBase> _services = new();
    private readonly ConcurrentDictionary<TradingModePreset, StrategyManager> _strategyManagers = new();
    private readonly ConcurrentDictionary<TradingModePreset, ScannerSettings> _scannerSettings = new();

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

        // ATI-Strategien einmal registrieren (nicht pro Modus, da RegisterStrategies ClearStrategies aufruft)
        if (_ati != null)
            _ati.RegisterStrategies(StrategyFactory.AvailableStrategies.Select(StrategyFactory.Create));

        // Kapital auf 3 Modi aufteilen (jeder handelt mit seinem Anteil)
        var balancePerMode = Math.Floor(initialBalance / 3m);

        foreach (var mode in new[] { TradingModePreset.Scalping, TradingModePreset.DayTrading, TradingModePreset.Swing })
        {
            var scanSettings = CreateScannerSettings(mode);
            var riskSettings = CreateRiskSettings(mode);
            var stratManager = CreateStrategyManager(mode);
            _scannerSettings[mode] = scanSettings;
            _strategyManagers[mode] = stratManager;

            var service = new PaperTradingService(
                _publicClient, stratManager, riskSettings, scanSettings, _eventBus, _botSettings);
            service.RiskManagerOverride = _sharedRiskManager;
            service.ModePrefix = $"[{ModeLabel(mode)}] ";

            if (_ati != null)
                service.ATI = _ati;

            _services[mode] = service;
            service.Start(balancePerMode);

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"[{ModeLabel(mode)}] Paper-Trading gestartet ({scanSettings.ScanTimeFrame}, {balancePerMode:N0} USDT, Scan alle {scanSettings.ScanIntervalSeconds}s, MaxHold={riskSettings.MaxHoldHours}h)"));
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

        // ATI-Strategien einmal registrieren (nicht pro Modus, da RegisterStrategies ClearStrategies aufruft)
        if (_ati != null)
            _ati.RegisterStrategies(StrategyFactory.AvailableStrategies.Select(StrategyFactory.Create));

        foreach (var mode in new[] { TradingModePreset.Scalping, TradingModePreset.DayTrading, TradingModePreset.Swing })
        {
            var scanSettings = CreateScannerSettings(mode);
            var riskSettings = CreateRiskSettings(mode);
            var stratManager = CreateStrategyManager(mode);
            _scannerSettings[mode] = scanSettings;
            _strategyManagers[mode] = stratManager;

            var service = new LiveTradingService(
                restClient, _publicClient, stratManager, riskSettings, scanSettings, _eventBus, _botSettings,
                dbService: _dbService);
            service.RiskManagerOverride = _sharedRiskManager;
            service.ModePrefix = $"[{ModeLabel(mode)}] ";

            if (_ati != null)
                service.ATI = _ati;

            _services[mode] = service;
            service.Start();

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"[{ModeLabel(mode)}] Live-Trading gestartet ({scanSettings.ScanTimeFrame}, Scan alle {scanSettings.ScanIntervalSeconds}s, MaxHold={riskSettings.MaxHoldHours}h)"));
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
        if (_services.TryRemove(mode, out var service))
        {
            await service.StopAsync();
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"[{ModeLabel(mode)}] Gestoppt"));
        }
    }

    /// <summary>Pausiert alle laufenden Modi.</summary>
    public void PauseAll()
    {
        foreach (var service in _services.Values)
            service.Pause();
    }

    /// <summary>Setzt alle pausierten Modi fort.</summary>
    public void ResumeAll()
    {
        foreach (var service in _services.Values)
            service.Resume();
    }

    /// <summary>Ob mindestens ein Service pausiert ist.</summary>
    public bool IsAnyPaused => _services.Values.Any(s => s.IsPaused);

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

    /// <summary>
    /// Aggregiert Account-Daten aller Paper-Services (Summe Balance, Positionen aus allen Modi).
    /// Gibt null zurück wenn keine Services aktiv sind.
    /// </summary>
    public async Task<(AccountInfo? Account, IReadOnlyList<Position> Positions)?> GetAggregatedPaperAccountAsync()
    {
        var paperServices = _services.Values
            .OfType<PaperTradingService>()
            .Where(s => s.Exchange != null)
            .ToList();

        if (paperServices.Count == 0) return null;

        decimal totalBalance = 0, totalAvailable = 0, totalUnrealized = 0;
        var allPositions = new List<Position>();

        foreach (var service in paperServices)
        {
            var account = await service.Exchange!.GetAccountInfoAsync();
            var positions = await service.Exchange!.GetPositionsAsync();
            totalBalance += account.Balance;
            totalAvailable += account.AvailableBalance;
            totalUnrealized += account.UnrealizedPnl;
            allPositions.AddRange(positions);
        }

        var aggregated = new AccountInfo(totalBalance, totalAvailable, totalUnrealized, 0m);
        return (aggregated, allPositions);
    }

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

    /// <summary>
    /// Erstellt mode-spezifische RiskSettings: Übernimmt globale Basis-Werte (Drawdown-Limits, Exposure etc.)
    /// und überschreibt nur die pro-Modus variierenden Parameter (Haltezeit, Cooldown, TP-Ratios, RRR).
    /// </summary>
    private RiskSettings CreateRiskSettings(TradingModePreset mode)
    {
        var riskPreset = TradingModeDefaults.GetRiskPreset(mode);
        return new RiskSettings
        {
            // Globale Werte vom User übernehmen
            MaxDailyDrawdownPercent = _riskSettings.MaxDailyDrawdownPercent,
            MaxTotalDrawdownPercent = _riskSettings.MaxTotalDrawdownPercent,
            MaxOpenPositions = _riskSettings.MaxOpenPositions,
            MaxOpenPositionsPerSymbol = _riskSettings.MaxOpenPositionsPerSymbol,
            UseAdaptiveLeverage = _riskSettings.UseAdaptiveLeverage,
            CheckCorrelation = _riskSettings.CheckCorrelation,
            MaxCorrelation = _riskSettings.MaxCorrelation,
            EnableTrailingStop = _riskSettings.EnableTrailingStop,
            TrailingStopPercent = _riskSettings.TrailingStopPercent,
            EnableMultiStageExit = _riskSettings.EnableMultiStageExit,
            EnableCooldownEscalation = _riskSettings.EnableCooldownEscalation,
            EnableEquityCurveTrading = _riskSettings.EnableEquityCurveTrading,
            EquityCurvePeriod = _riskSettings.EquityCurvePeriod,
            EnableMomentumDecay = _riskSettings.EnableMomentumDecay,
            MinLiquidationDistancePercent = _riskSettings.MinLiquidationDistancePercent,
            MaxNetExposurePercent = _riskSettings.MaxNetExposurePercent,
            ConsiderFundingRate = _riskSettings.ConsiderFundingRate,
            MaxAdverseFundingRatePercent = _riskSettings.MaxAdverseFundingRatePercent,
            MaxTradesPerDay = _riskSettings.MaxTradesPerDay,

            // Mode-spezifische Werte aus Preset
            MaxPositionSizePercent = riskPreset.MaxPositionSizePercent,
            MaxMarginPerTradePercent = riskPreset.MaxMarginPerTradePercent,
            MaxLeverage = riskPreset.MaxLeverage,
            CooldownHours = riskPreset.CooldownHours,
            MaxCooldownHours = riskPreset.MaxCooldownHours,
            MaxHoldHours = riskPreset.MaxHoldHours,
            MaxHoldHoursAfterTp1 = riskPreset.MaxHoldHoursAfterTp1,
            Tp1CloseRatio = riskPreset.Tp1CloseRatio,
            Tp2CloseRatio = riskPreset.Tp2CloseRatio,
            SmartBreakevenAtrMultiplier = riskPreset.SmartBreakevenAtrMultiplier,
            MinRiskRewardRatio = riskPreset.MinRiskRewardRatio,
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
