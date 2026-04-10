using System.Collections.Concurrent;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine;
using BingXBot.Engine.ATI;
using BingXBot.Engine.Indicators;
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
        // Paper-Trading unterstützt immer Hedge-Modus (SimulatedExchange erlaubt Long+Short)
        _isHedgeModeActive = true;
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
            service.SuppressBotStateEvents = true; // Orchestrator publiziert BotState zentral

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
    public async Task StartLiveAsync(Exchange.BingXRestClient restClient)
    {
        _restClient = restClient;
        // Hedge-Modus aus BingX abfragen (TradFi braucht Hedge-Modus, Error 101414 bei One-Way)
        _isHedgeModeActive = _botSettings.Scanner?.IsHedgeModeActive ?? false;
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
            service.SuppressBotStateEvents = true; // Orchestrator publiziert BotState zentral

            if (_ati != null)
                service.ATI = _ati;

            _services[mode] = service;
            service.Start();

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Engine",
                $"[{ModeLabel(mode)}] Live-Trading gestartet ({scanSettings.ScanTimeFrame}, Scan alle {scanSettings.ScanIntervalSeconds}s, MaxHold={riskSettings.MaxHoldHours}h)"));
        }

        // Offene Positionen wiederherstellen (SL/TP-Signale setzen, Breakeven prüfen)
        await RecoverOpenPositionsAsync(restClient, _publicClient).ConfigureAwait(false);
    }

    /// <summary>Stoppt alle Modi. Publiziert BotState nur einmal (nicht 3x).</summary>
    public async Task StopAllAsync()
    {
        foreach (var (mode, service) in _services)
        {
            try
            {
                // SuppressBotStateEvents: StopBase() soll nicht 3x BotState.Stopped publizieren
                service.SuppressBotStateEvents = true;
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

        // Einmal BotState publizieren für alle Modi
        _eventBus.PublishBotState(Core.Enums.BotState.Stopped);
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

    /// <summary>Pausiert alle laufenden Modi. Publiziert BotState einmal zentral.</summary>
    public void PauseAll()
    {
        foreach (var service in _services.Values)
            service.Pause();
        _eventBus.PublishBotState(Core.Enums.BotState.Paused);
    }

    /// <summary>Setzt alle pausierten Modi fort. Publiziert BotState einmal zentral.</summary>
    public void ResumeAll()
    {
        foreach (var service in _services.Values)
            service.Resume();
        _eventBus.PublishBotState(Core.Enums.BotState.Running);
    }

    /// <summary>Ob mindestens ein Service pausiert ist.</summary>
    public bool IsAnyPaused => _services.Values.Any(s => s.IsPaused);

    /// <summary>Notfall-Stop: Alle Positionen schließen über alle Modi (parallel für Schnelligkeit).</summary>
    public async Task EmergencyStopAllAsync()
    {
        var tasks = _services.Select(async kvp =>
        {
            try
            {
                kvp.Value.SuppressBotStateEvents = true;
                await kvp.Value.EmergencyStopAsync();
            }
            catch { /* Best-effort */ }
        });
        await Task.WhenAll(tasks);
        _services.Clear();
        _eventBus.PublishBotState(Core.Enums.BotState.EmergencyStop);
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

    /// <summary>Ob der BingX-Account im Hedge-Modus ist. Paper=true (SimExchange unterstützt Hedge), Live=aus API.</summary>
    private bool _isHedgeModeActive;

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
            OnlyTopByVolume = preset.OnlyTopByVolume,
            TopCoinsCount = preset.TopCoinsCount,
            // Watchlist vom User übernehmen (gleich für alle Modi)
            Whitelist = _botSettings.Scanner?.Whitelist ?? new(),
            Blacklist = _botSettings.Scanner?.Blacklist ?? new(),
            // TradFi-Settings vom User übernehmen (gleich für alle Modi)
            // Default: Alle 5 Kategorien — konsistent mit ScannerSettings-Default
            EnableTradFi = _botSettings.Scanner?.EnableTradFi ?? false,
            EnabledCategories = _botSettings.Scanner?.EnabledCategories ?? new()
            {
                Core.Enums.MarketCategory.Crypto, Core.Enums.MarketCategory.Commodity,
                Core.Enums.MarketCategory.Index, Core.Enums.MarketCategory.Forex,
                Core.Enums.MarketCategory.Stock
            },
            MinVolume24hTradFi = _botSettings.Scanner?.MinVolume24hTradFi ?? 1_000_000m,
            IsHedgeModeActive = _isHedgeModeActive
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

            // Mode-spezifische Werte: User-Einstellung hat Vorrang (konsistent mit Single-Mode),
            // Preset als Fallback wenn User-Wert nicht gesetzt/default ist
            MaxPositionSizePercent = riskPreset.MaxPositionSizePercent,
            MaxMarginPerTradePercent = riskPreset.MaxMarginPerTradePercent,
            MaxLeverage = _riskSettings.MaxLeverage,
            CooldownHours = riskPreset.CooldownHours,
            MaxCooldownHours = riskPreset.MaxCooldownHours,
            MaxHoldHours = riskPreset.MaxHoldHours,
            MaxHoldHoursAfterTp1 = riskPreset.MaxHoldHoursAfterTp1,
            Tp1CloseRatio = riskPreset.Tp1CloseRatio,
            Tp2CloseRatio = riskPreset.Tp2CloseRatio,
            SmartBreakevenAtrMultiplier = riskPreset.SmartBreakevenAtrMultiplier,
            MinRiskRewardRatio = riskPreset.MinRiskRewardRatio,

            // Per-Markt Leverage-Settings vom User übernehmen (gleich für alle Modi)
            CategorySettings = _riskSettings.CategorySettings,
        };
    }

    private StrategyManager CreateStrategyManager(TradingModePreset mode)
    {
        // Strategie-Name aus BotSettings übernehmen (vom User im Dashboard gewählt)
        var strategyName = _botSettings.LastStrategyName ?? "CryptoTrendPro";
        var strategy = StrategyFactory.Create(strategyName);

        // ApplyPreset für alle Strategien die es unterstützen
        if (strategy is CryptoTrendProStrategy ctp)
            ctp.ApplyPreset(mode);
        else if (strategy is SequenzKonzeptStrategy sk)
            sk.ApplyPreset(mode);

        var manager = new StrategyManager();
        manager.SetStrategy(strategy);
        return manager;
    }

    /// <summary>
    /// Stellt SL/TP-Signale für bestehende Positionen nach App-Neustart wieder her.
    /// Liest offene Conditional Orders von BingX und registriert sie in ALLEN aktiven Services.
    /// Setzt Auto-Breakeven für Positionen die weit genug im Gewinn sind.
    /// </summary>
    public async Task RecoverOpenPositionsAsync(Exchange.BingXRestClient restClient, IPublicMarketDataClient publicClient)
    {
        try
        {
            var positions = await restClient.GetPositionsAsync();
            if (positions.Count == 0) return;

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Recovery",
                $"[Multi] Prüfe {positions.Count} offene Position(en) auf fehlende SL/Breakeven..."));

            var tickers = await publicClient.GetAllTickersAsync();
            var tickerMap = tickers.ToDictionary(t => t.Symbol, t => t.LastPrice);
            var openOrders = await restClient.GetOpenOrdersAsync();

            foreach (var pos in positions)
            {
                if (!tickerMap.TryGetValue(pos.Symbol, out var currentPrice) || currentPrice <= 0)
                    continue;

                // Auto-Breakeven prüfen: Gewinn% >= Leverage%
                var pnlPercent = pos.Side == Core.Enums.Side.Buy
                    ? (currentPrice - pos.EntryPrice) / pos.EntryPrice * 100m
                    : (pos.EntryPrice - currentPrice) / pos.EntryPrice * 100m;

                if (pnlPercent >= pos.Leverage && pos.Leverage > 0)
                {
                    // Breakeven = Entry + Round-Trip-Fees (0.1%) + Sicherheitspuffer
                    var beSl = pos.Side == Core.Enums.Side.Buy
                        ? pos.EntryPrice * 1.0015m
                        : pos.EntryPrice * 0.9985m;
                    try
                    {
                        await restClient.SetPositionSlTpAsync(pos.Symbol, pos.Side, beSl, null);
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Trade, "Recovery",
                            $"[Multi] {pos.Symbol}: Auto-Breakeven gesetzt (PnL={pnlPercent:F1}%) → SL={beSl:F8}",
                            pos.Symbol));
                    }
                    catch { /* Best-effort */ }
                }

                // SL/TP aus BingX-Orders lesen
                decimal? slPrice = null, tpPrice = null;
                foreach (var order in openOrders)
                {
                    if (order.Symbol != pos.Symbol) continue;
                    if (order.Type == Core.Enums.OrderType.StopMarket && order.StopPrice.HasValue)
                        slPrice = order.StopPrice.Value;
                    if (order.Type is Core.Enums.OrderType.TakeProfitMarket or Core.Enums.OrderType.TakeProfitLimit && order.StopPrice.HasValue)
                        tpPrice = order.StopPrice.Value;
                }

                // Signal nur in EINEM Service registrieren (sonst triggern alle 3 PriceTickerLoops
                // gleichzeitig OnSlTpHitAsync für dieselbe Position → dreifacher Close-Versuch)
                if (slPrice.HasValue || tpPrice.HasValue)
                {
                    var signal = new Core.Models.SignalResult(
                        pos.Side == Core.Enums.Side.Buy ? Core.Enums.Signal.Long : Core.Enums.Signal.Short,
                        0.5m, pos.EntryPrice, slPrice, tpPrice, "Recovery: Aus BingX-Orders wiederhergestellt");

                    var targetService = _services.Values.FirstOrDefault();
                    targetService?.RestorePositionSignal(pos.Symbol, pos.Side, signal);

                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Recovery",
                        $"[Multi] {pos.Symbol}: Wiederhergestellt (SL={slPrice?.ToString("F8") ?? "---"}, TP={tpPrice?.ToString("F8") ?? "---"})"));
                }
                else
                {
                    // Kein SL auf BingX → Standard-SL berechnen (ATR-basiert wie bei Eröffnung)
                    var recoverySl = await CalculateRecoverySlAsync(pos, publicClient);
                    try
                    {
                        await restClient.SetPositionSlTpAsync(pos.Symbol, pos.Side, recoverySl, null);
                        var signal = new Core.Models.SignalResult(
                            pos.Side == Core.Enums.Side.Buy ? Core.Enums.Signal.Long : Core.Enums.Signal.Short,
                            0.5m, pos.EntryPrice, recoverySl, null, "Recovery: Standard-SL gesetzt (ATR-basiert)");
                        var targetService = _services.Values.FirstOrDefault();
                        targetService?.RestorePositionSignal(pos.Symbol, pos.Side, signal);

                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                            $"[Multi] {pos.Symbol}: Standard-SL gesetzt → SL={recoverySl:F8}. PnL={pnlPercent:F1}%",
                            pos.Symbol));
                    }
                    catch (Exception emergEx)
                    {
                        _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Recovery",
                            $"[Multi] {pos.Symbol}: KRITISCH - SL konnte nicht gesetzt werden: {emergEx.Message}",
                            pos.Symbol));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Recovery",
                $"[Multi] Position-Recovery fehlgeschlagen: {ex.Message}"));
        }
    }

    /// <summary>
    /// Sucht den Service der ein Signal für die gegebene Position hat.
    /// Nötig für manuellen Close im Multi-Mode (Dashboard).
    /// </summary>
    public TradingServiceBase? FindServiceForPosition(string symbol, Core.Enums.Side side)
    {
        foreach (var service in _services.Values)
        {
            if (service.GetPositionSignal(symbol, side) != null)
                return service;
        }
        return _services.Values.FirstOrDefault(); // Fallback: erster Service
    }

    private static string ModeLabel(TradingModePreset mode) => mode switch
    {
        TradingModePreset.Scalping => "S",
        TradingModePreset.DayTrading => "D",
        TradingModePreset.Swing => "W",
        _ => "?"
    };

    /// <summary>Berechnet ATR-basierten Recovery-SL (gleiche Logik wie bei Tradeeröffnung).</summary>
    private async Task<decimal> CalculateRecoverySlAsync(Position pos, IPublicMarketDataClient publicClient)
    {
        try
        {
            // Ersten verfügbaren ScannerSettings-Timeframe verwenden
            var timeFrame = _scannerSettings.Values.FirstOrDefault()?.ScanTimeFrame ?? Core.Enums.TimeFrame.H4;
            var candles = await publicClient.GetKlinesAsync(
                pos.Symbol, timeFrame, DateTime.UtcNow.AddHours(-100), DateTime.UtcNow).ConfigureAwait(false);

            if (candles.Count >= 20)
            {
                var atr = IndicatorHelper.CalculateAtr(candles);
                if (atr.Count > 0 && atr[^1].HasValue && atr[^1]!.Value > 0)
                {
                    var atrValue = atr[^1]!.Value;
                    var atrPercentile = IndicatorHelper.CalculateAtrPercentile(candles);
                    var (slMult, _, _, _) = TradingModeDefaults.GetVolAdaptiveMultipliers(
                        _botSettings.LastTradingModePreset, atrPercentile);

                    var sl = pos.Side == Core.Enums.Side.Buy
                        ? pos.EntryPrice - atrValue * slMult
                        : pos.EntryPrice + atrValue * slMult;

                    var minDist = pos.EntryPrice * 0.005m;
                    if (Math.Abs(pos.EntryPrice - sl) < minDist)
                        sl = pos.Side == Core.Enums.Side.Buy ? pos.EntryPrice - minDist : pos.EntryPrice + minDist;
                    return sl;
                }
            }
        }
        catch { /* Candle-Laden fehlgeschlagen → Fallback */ }

        var fallbackPercent = Math.Max(0.015m, pos.Leverage > 0 ? 0.03m / pos.Leverage : 0.03m);
        return pos.Side == Core.Enums.Side.Buy
            ? pos.EntryPrice * (1m - fallbackPercent)
            : pos.EntryPrice * (1m + fallbackPercent);
    }

    public void Dispose()
    {
        // StopBase() für jeden Service aufrufen damit _positionSignals geleert und BotState publiziert werden.
        // StopAsync() ist synchron in TradingServiceBase (nur CTS-Cancel + State-Cleanup).
        foreach (var service in _services.Values)
        {
            try { service.StopAsync().GetAwaiter().GetResult(); }
            catch { /* Best-effort beim Dispose */ }
            service.Dispose();
        }
        _services.Clear();
    }
}
