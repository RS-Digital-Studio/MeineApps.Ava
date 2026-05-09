using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Trading.Local;

/// <summary>
/// Adapter: BotEventBus (In-Process-Events) -> IBotEventStream (DTO-Events).
/// Wird auf dem Server direkt genutzt UND im BotHubEventForwarder, der die DTOs
/// weiter an SignalR-Clients sendet.
///
/// Manche Events haben noch keinen Producer im EventBus (PositionUpdated, EquityUpdate,
/// TickerUpdate, BtcPriceUpdate, ScannerResult, BacktestProgress) — die werden in Phase 3
/// vom Server-Hub direkt aus Trading-Services gepublished.
/// </summary>
public sealed class LocalBotEventStream : IBotEventStream
{
    private readonly BotEventBus _bus;
    private readonly BotSettings _botSettings;
    private readonly IBacktestControlService? _backtest;
    private readonly ISettingsService? _settings;

    public ConnectionStatus Connection { get; private set; } = ConnectionStatus.Connected;

    public event Action<ConnectionStatus>? ConnectionChanged;
    public event Action<BotStateChangedDto>? BotStateChanged;
    public event Action<TradeDto>? TradeOpened;
    public event Action<TradeDto>? TradeClosed;
    public event Action<PositionDto>? PositionUpdated;
    public event Action<EquityPointDto>? EquityUpdate;
    public event Action<MarginWarningDto>? MarginWarning;
    public event Action<TickerUpdateDto>? TickerUpdate;
    public event Action<TickerUpdateDto>? BtcPriceUpdate;
    public event Action<ScannerResultDto>? ScannerResult;
    public event Action<LogEntryDto>? LogEmitted;
    public event Action<ActivityFeedDto>? ActivityFeed;
    public event Action<BacktestProgressDto>? BacktestProgress;
    public event Action<BacktestResultDto>? BacktestCompleted;
    public event Action<FullSettingsDto>? SettingsChanged;
    public event Action<ConnectionDegradedDto>? ConnectionDegraded;
    /// <summary>v1.5.2 Phase 4 — Decision-Trail-Forward.</summary>
    public event Action<EvaluationDecisionDto>? EvaluationDecided;
    /// <summary>Phase 18 / H2 — News-Service-Health-Edge-Transition.</summary>
    public event Action<NewsServiceDegradedDto>? NewsServiceDegraded;

    /// <summary>
    /// Wird vom ServerHealthWatchdog aufgerufen wenn sich der BingX-Verbindungsstatus aendert.
    /// Publik damit der HostedService ohne zusaetzlichen EventBus-Hop direkt pushen kann.
    /// </summary>
    public void PublishConnectionDegraded(ConnectionDegradedDto dto) => ConnectionDegraded?.Invoke(dto);

    /// <summary>Phase 18 / H2 — Wird vom RiskManager (via TradingServiceBase) bei Edge-Transition gerufen.</summary>
    public void PublishNewsServiceDegraded(NewsServiceDegradedDto dto) => NewsServiceDegraded?.Invoke(dto);

    public LocalBotEventStream(
        BotEventBus bus,
        BotSettings botSettings,
        IBacktestControlService? backtest = null,
        ISettingsService? settings = null)
    {
        _bus = bus;
        _botSettings = botSettings;
        _backtest = backtest;
        _settings = settings;

        _bus.BotStateChanged += HandleBotState;
        _bus.TradeCompleted += HandleTradeCompleted;
        _bus.LogEmitted += HandleLog;
        _bus.MarginWarning += HandleMarginWarning;

        // v1.3.0 K1: Die sechs Live-Events (TradeOpened/PositionUpdated/EquityUpdate/
        // TickerUpdate/BtcPriceUpdate/ScannerResult) werden jetzt durch den EventBus
        // gepublished — LocalBotEventStream mappt auf DTOs, BotHubEventForwarder sendet
        // das an alle SignalR-Clients.
        _bus.TradeOpened += HandleTradeOpened;
        _bus.PositionUpdated += HandlePositionUpdated;
        _bus.EquityUpdate += HandleEquityUpdate;
        _bus.TickerUpdate += HandleTickerUpdate;
        _bus.BtcPriceUpdate += HandleBtcPriceUpdate;
        _bus.ScannerSweep += HandleScannerSweep;
        // v1.5.2 Phase 4 — Decision-Trail Forward auf den IBotEventStream.
        _bus.EvaluationDecided += HandleEvaluationDecided;
        // Phase 18 / H2 — News-Service-Health Forward.
        _bus.NewsServiceHealthChanged += HandleNewsServiceHealthChanged;

        // v1.3.0 K1: Backtest-Progress + Completed werden vom LocalBacktestService-eigenen
        // Progress-Callback gefeuert. Wir subscriben optional hier — im Client-Standalone
        // wird Backtest lokal ausgefuehrt, der VM subscribed direkt. Im Server-Modus aber
        // muessen wir die Events in den Hub-Strom haengen, damit der Desktop-Client sie sieht.
        if (_backtest != null)
        {
            _backtest.ProgressReceived += HandleBacktestProgress;
            _backtest.Completed += HandleBacktestCompleted;
        }

        // v1.3.0 H1: Settings-Change-Propagation fuer Multi-Client-Sync.
        // Wenn ein Client Settings aendert, feuert LocalSettingsService.SettingsChanged —
        // wir leiten das weiter an IBotEventStream.SettingsChanged, das vom BotHubEventForwarder
        // an alle SignalR-Clients gepushed wird.
        if (_settings != null)
        {
            _settings.SettingsChanged += HandleSettingsChanged;
        }
    }

    private void HandleBacktestProgress(BacktestProgressDto dto) => BacktestProgress?.Invoke(dto);
    private void HandleBacktestCompleted(BacktestResultDto dto) => BacktestCompleted?.Invoke(dto);
    private void HandleSettingsChanged(FullSettingsDto dto) => SettingsChanged?.Invoke(dto);

    /// <summary>v1.5.2 Phase 4 — Mappt Domain-Decision auf Wire-DTO und feuert das Stream-Event.</summary>
    private void HandleEvaluationDecided(object? sender, BingXBot.Core.Diagnostics.EvaluationDecision d)
    {
        EvaluationDecided?.Invoke(new EvaluationDecisionDto(
            UtcTimestamp: d.UtcTimestamp,
            Symbol: d.Symbol,
            Tf: (int)d.Tf,
            SequenceState: d.SequenceState,
            Point0: d.Point0,
            PointA: d.PointA,
            PointB: d.PointB,
            Triggered: d.Triggered,
            RejectionReason: d.RejectionReason,
            ConfluenceScore: d.ConfluenceScore,
            ConfluenceCategories: d.ConfluenceCategories,
            HardFiltersFailed: d.HardFiltersFailed));
    }

    /// <summary>Phase 18 / H2 — Mappt News-Service-Health-Edge auf Wire-DTO und feuert das Stream-Event.</summary>
    private void HandleNewsServiceHealthChanged(object? sender, (bool IsDegraded, int FailureCount, string? Reason) e)
    {
        NewsServiceDegraded?.Invoke(new NewsServiceDegradedDto(
            IsDegraded: e.IsDegraded,
            FailureCount: e.FailureCount,
            Reason: e.Reason,
            TimestampUtc: DateTime.UtcNow));
    }

    private void HandleBotState(object? sender, BotState state)
    {
        BotStateChanged?.Invoke(new BotStateChangedDto(
            State: state,
            Mode: _botSettings.LastMode,
            Reason: null,
            TimestampUtc: DateTime.UtcNow));
    }

    private void HandleTradeCompleted(object? sender, CompletedTrade trade)
    {
        var dto = trade.ToDto(id: 0, strategyName: _botSettings.LastStrategyName);
        TradeClosed?.Invoke(dto);
    }

    private void HandleLog(object? sender, LogEntry entry)
    {
        var dto = entry.ToDto();
        LogEmitted?.Invoke(dto);

        // Activity-Feed: Subset der Logs, die den Nutzer direkt interessieren
        if (IsActivity(entry))
        {
            ActivityFeed?.Invoke(new ActivityFeedDto(
                TimestampUtc: entry.Timestamp,
                Category: entry.Category,
                Message: entry.Message,
                Level: entry.Level,
                Symbol: entry.Symbol));
        }
    }

    private static bool IsActivity(LogEntry entry) =>
        entry.Category is "Engine" or "Trade" or "Risk" or "Recovery"
        || entry.Level is LogLevel.Warning or LogLevel.Error;

    private void HandleMarginWarning(object? sender, MarginWarningArgs args)
    {
        MarginWarning?.Invoke(new MarginWarningDto(
            Symbol: args.Symbol,
            CurrentPrice: args.CurrentPrice,
            LiquidationPrice: args.LiquidationPrice,
            DistancePercent: args.DistancePercent,
            TimestampUtc: DateTime.UtcNow));
    }

    private void HandleTradeOpened(object? sender, TradeOpenedArgs args)
    {
        // Entry als "Trade" mappen (Exit-Felder noch leer) — Client rendert das als neue Position
        // im Dashboard. TradeClosed kommt separat ueber HandleTradeCompleted. NavigatorTimeframe
        // kommt jetzt aus TradeOpenedArgs (vorher hardcoded H4 = falsche Darstellung fuer Multi-TF).
        var p = args.Position;
        var dto = new TradeDto(
            Id: 0,
            Symbol: p.Symbol,
            Side: p.Side,
            EntryPrice: p.EntryPrice,
            ExitPrice: 0m,
            Quantity: p.Quantity,
            Pnl: 0m,
            PnlPercent: 0m,
            Fee: 0m,
            EntryTimeUtc: p.OpenTime,
            ExitTimeUtc: p.OpenTime,
            Reason: "Entry",
            Mode: _botSettings.LastMode,
            StrategyName: _botSettings.LastStrategyName,
            NavigatorTimeframe: args.NavigatorTimeframe);
        TradeOpened?.Invoke(dto);
    }

    private void HandlePositionUpdated(object? sender, PositionSnapshotArgs args) =>
        PositionUpdated?.Invoke(args.Position.ToDto(
            stopLoss: args.StopLoss,
            takeProfit: args.TakeProfit,
            liquidationPrice: args.LiquidationPrice,
            smartBreakevenArmed: args.IsSmartBreakevenArmed,
            strategyName: args.StrategyName));

    private void HandleEquityUpdate(object? sender, EquityPoint point) =>
        EquityUpdate?.Invoke(point.ToDto());

    private void HandleTickerUpdate(object? sender, Ticker t) =>
        TickerUpdate?.Invoke(new TickerUpdateDto(t.Symbol, t.LastPrice, DateTime.UtcNow));

    private void HandleBtcPriceUpdate(object? sender, Ticker t) =>
        BtcPriceUpdate?.Invoke(new TickerUpdateDto(t.Symbol, t.LastPrice, DateTime.UtcNow));

    private void HandleScannerSweep(object? sender, ScannerSweepArgs args)
    {
        // Mapping ScannerCandidate (Core-Trading-Schicht) -> ScannerSymbolDto (Contracts-Schicht).
        var symbols = args.Candidates
            .Select(c => new ScannerSymbolDto(
                Symbol: c.Symbol,
                Price: c.Price,
                Volume24h: c.Volume24h,
                PriceChangePercent: c.PriceChangePercent,
                Score: c.Score,
                SuggestedSide: c.SuggestedSide,
                Reason: c.Reason))
            .ToList();
        ScannerResult?.Invoke(new ScannerResultDto(
            NavigatorTimeframe: args.NavigatorTimeframe,
            TimestampUtc: DateTime.UtcNow,
            Symbols: symbols));
    }

    // Alte Publish-Methoden entfernt (v1.3.0 K1): Die Trading-Services pushen jetzt ueber
    // den BotEventBus, nicht mehr direkt an den Stream. Fuer Backtest-Progress/Completed:
    // siehe Subscribe auf IBacktestControlService.
    public void PublishBacktestProgress(BacktestProgressDto dto) => BacktestProgress?.Invoke(dto);
    public void PublishBacktestResult(BacktestResultDto dto) => BacktestCompleted?.Invoke(dto);

    public Task StartAsync(CancellationToken ct = default)
    {
        Connection = ConnectionStatus.Connected;
        ConnectionChanged?.Invoke(Connection);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        Connection = ConnectionStatus.Disconnected;
        ConnectionChanged?.Invoke(Connection);
        return Task.CompletedTask;
    }

    public Task SubscribeSymbolAsync(string symbol, CancellationToken ct = default) => Task.CompletedTask;
    public Task UnsubscribeSymbolAsync(string symbol, CancellationToken ct = default) => Task.CompletedTask;
    public Task SetLogFilterAsync(LogLevel minLevel, CancellationToken ct = default) => Task.CompletedTask;

    public void Dispose()
    {
        _bus.BotStateChanged -= HandleBotState;
        _bus.TradeCompleted -= HandleTradeCompleted;
        _bus.LogEmitted -= HandleLog;
        _bus.MarginWarning -= HandleMarginWarning;
        _bus.TradeOpened -= HandleTradeOpened;
        _bus.PositionUpdated -= HandlePositionUpdated;
        _bus.EquityUpdate -= HandleEquityUpdate;
        _bus.TickerUpdate -= HandleTickerUpdate;
        _bus.BtcPriceUpdate -= HandleBtcPriceUpdate;
        _bus.ScannerSweep -= HandleScannerSweep;
        _bus.EvaluationDecided -= HandleEvaluationDecided;
        _bus.NewsServiceHealthChanged -= HandleNewsServiceHealthChanged;
        if (_backtest != null)
        {
            _backtest.ProgressReceived -= HandleBacktestProgress;
            _backtest.Completed -= HandleBacktestCompleted;
        }
        if (_settings != null)
        {
            _settings.SettingsChanged -= HandleSettingsChanged;
        }
        // 24.04.2026 Robustness #5: ConnectionChanged-Event explizit feuern,
        // damit subscribed Komponenten (Status-Anzeigen, Reconnect-Logik) den
        // Disconnect mitbekommen — sonst zeigen sie weiter "Connected" obwohl
        // der Stream tot ist.
        Connection = ConnectionStatus.Disconnected;
        try { ConnectionChanged?.Invoke(Connection); }
        catch { /* Subscriber-Exception darf Dispose nicht abbrechen */ }
    }
}
